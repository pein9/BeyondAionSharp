using System.Reflection;
using System.Text.Json;
using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.Commons.Database;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Admin;
using Aion.GameServer.Services.Items;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunE8Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		var token = timeout.Token;
		await using var session = new SimulationL0Session(fixture, policy, "b01", 83, "Aesimstorage");
		session.BeginStep("s00", "login-create-enter");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token);
		await session.EnterWorldAsync(token);
		await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
		await WarehouseScenario.RunAsync(new SimWarehouseDriver(this, fixture, session), token);
		policy.AssertClean();
	}

	private sealed class SimWarehouseDriver(SimulationFastScenarioTests owner, SimulationWorldFixture world, SimulationL0Session session) : IWarehouseScenarioDriver
	{
		private int step;
		public BotApi Api => session.Api;
		public async Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token)
		{
			session.BeginStep($"s{++step:D2}", action);
			Console.WriteLine($"E8 s{step:D2}: {action}");
			try { await operation(token); }
			catch (Exception exception)
			{
				throw new InvalidOperationException($"Warehouse step {action} failed. Recent packets:{Environment.NewLine}" +
					string.Join(Environment.NewLine, session.PacketHistory.TakeLast(20).Select(p => p.PacketType.Name + " " + JsonSerializer.Serialize(p.Fields))), exception);
			}
		}
		public async Task<int> PrepareAsync(CancellationToken token)
		{
			var player = world.World.GetPlayer(session.CharacterId);
			Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
			foreach (var grant in WarehouseScenario.Grants) Assert.Equal(0, ItemService.AddItem(player, grant.Id, grant.Count));
			var point = WarehouseScenario.Position;
			await owner.TeleportForSetupAsync(session, player, WarehouseScenario.MapId, point.X - 5, point.Y, point.Z, token);
			var npc = player.GetPosition().GetWorldMapInstance().GetNpcs(WarehouseScenario.NpcId).First(n => n.IsSpawned());
			await session.MoveToPositionAsync(new BotPosition(npc.GetX() - 1, npc.GetY(), npc.GetZ(), 0), token);
			return npc.GetObjectId();
		}
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => session.SendPacketAsync(packet, token);
		public Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token) => session.WaitForPacketAsync(type, token, predicate);
		public async Task SynchronizeAsync(CancellationToken token)
		{
			await SendAsync(GameClientPackets.TimeCheck(unchecked((int)world.Clock.NowMillis)), token);
			await WaitAsync(typeof(SM_TIME_CHECK), _ => true, token);
		}
		public async Task VerifyPersistenceAsync(CancellationToken token)
		{
			VerifyOracle();
			var cube = Api.World.Inventory.OrderBy(p => p.Key).ToArray();
			var regular = Api.World.Warehouses[1].Items.OrderBy(p => p.Key).ToArray();
			var account = Api.World.Warehouses[2].Items.OrderBy(p => p.Key).ToArray();
			int? capacity = Api.World.Warehouses[1].CharacterCapacity;
			await session.QuitAsync(token);
			await session.VerifyOfflineAsync(token);
			await session.WaitForReentryAsync(token);
			await session.ReloginAndVerifyPersistenceAsync(token);
			await session.EnterWorldAsync(token);
			await SynchronizeAsync(token);
			Assert.Equal(cube, Api.World.Inventory.OrderBy(p => p.Key));
			Assert.Equal(regular, Api.World.Warehouses[1].Items.OrderBy(p => p.Key));
			Assert.Equal(account, Api.World.Warehouses[2].Items.OrderBy(p => p.Key));
			Assert.Equal(capacity, Api.World.Warehouses[1].CharacterCapacity);
			VerifyOracle();
			foreach (var (type, ownerId, expected) in new[]
			{
				(StorageType.CUBE, session.CharacterId, cube),
				(StorageType.REGULAR_WAREHOUSE, session.CharacterId, regular),
				(StorageType.ACCOUNT_WAREHOUSE, 83, account)
			})
			{
				var saved = InventoryDAO.LoadItems(ownerId, type).OrderBy(i => i.GetObjectId()).ToArray();
				Assert.Equal(expected.Length, saved.Length);
				for (int i = 0; i < saved.Length; i++)
				{
					Assert.Equal(expected[i].Key, saved[i].GetObjectId());
					Assert.Equal(expected[i].Value.ItemId, saved[i].GetItemId());
					Assert.Equal(expected[i].Value.Count, saved[i].GetItemCount());
					Assert.Equal(expected[i].Value.EquipmentSlot, unchecked((ushort)saved[i].GetEquipmentSlot()));
				}
			}
			using var connection = DatabaseFactory.GetConnection();
			connection.Open();
			using var command = connection.CreateCommand();
			command.CommandText = "SELECT wh_npc_expands, wh_bonus_expands FROM players WHERE id=@id";
			command.Parameters.AddWithValue("@id", session.CharacterId);
			using var reader = command.ExecuteReader();
			Assert.True(reader.Read());
			Assert.Equal(capacity, 24 + 8 * (reader.GetInt32(0) + reader.GetInt32(1)));
			Assert.False(reader.Read());
		}
		private void VerifyOracle()
		{
			var player = world.World.GetPlayer(session.CharacterId);
			Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
			var admin = typeof(AdminHttpService);
			object inventory = admin.Assembly.GetType("Aion.GameServer.Services.Admin.AdminInventory")!.GetMethod("Read")!.Invoke(null, [player])!;
			object warehouse = admin.GetMethod("SnapshotWarehouse", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [player])!;
			var response = JsonSerializer.SerializeToElement(new { ok = true, online = true, recipientCharacterId = session.CharacterId, inventory, warehouse },
				new JsonSerializerOptions(JsonSerializerDefaults.Web));
			BotWarehouseOracle.Verify(session.CharacterId, Api.World, response);
		}
	}
}
