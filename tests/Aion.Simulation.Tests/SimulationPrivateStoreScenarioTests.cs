using System.Text.Json;
using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Admin;
using Aion.GameServer.Services.Items;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunE10Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		var token = timeout.Token;
		await using var seller = new SimulationL0Session(fixture, policy, "b01", 86, "Aesimshopa");
		await using var buyer = new SimulationL0Session(fixture, policy, "b02", 87, "Aesimshopb");
		int offset = 0;
		foreach (var session in new[] { seller, buyer })
		{
			session.BeginStep("s00", "login-create-enter-and-prepare-private-store");
			await session.LoginAndAuthenticateAsync(token); await session.CreateCharacterAsync(token); await session.EnterWorldAsync(token);
			await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
			var player = fixture.World.GetPlayer(session.CharacterId);
			Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
			Assert.Equal(0, ItemService.AddItem(player, BotWorldModel.KinahItemId, 100_000));
			if (session == seller) Assert.Equal(0, ItemService.AddItem(player, PrivateStoreScenario.ItemId, 10));
			var p = PrivateStoreScenario.Position;
			await TeleportForSetupAsync(session, player, PrivateStoreScenario.MapId, p.X - 1 + offset, p.Y, p.Z, token);
			await session.MoveToPositionAsync(p with { X = p.X + offset }, token);
			offset += 2;
		}
		await PrivateStoreScenario.RunAsync(new SimPrivateStoreDriver(fixture, seller), new SimPrivateStoreDriver(fixture, buyer), token);
		policy.AssertClean();
	}

	private sealed class SimPrivateStoreDriver(SimulationWorldFixture world, SimulationL0Session session) : IPrivateStoreScenarioDriver
	{
		private int step;
		public BotApi Api => session.Api;
		public int CharacterId => session.CharacterId;
		public BotPosition CurrentPosition => session.CurrentPosition;
		public async Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token)
		{
			session.BeginStep($"s{++step:D2}", action); Console.WriteLine($"E10 {CharacterId} s{step:D2}: {action}");
			try { await operation(token); }
			catch (Exception exception)
			{
				throw new InvalidOperationException($"Private-store {CharacterId} step {action}. Recent packets:{Environment.NewLine}" +
					string.Join(Environment.NewLine, session.PacketHistory.TakeLast(20).Select(p => p.PacketType.Name + " " + JsonSerializer.Serialize(p.Fields))), exception);
			}
		}
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => session.SendPacketAsync(packet, token);
		public async Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token)
		{
			await session.DrainServerPacketsAsync(token);
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(5));
			try { return await session.WaitForPacketAsync(type, timeout.Token, predicate); }
			catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException($"Private-store step did not receive {type.Name}."); }
		}
		public Task MoveAsync(BotPosition position, CancellationToken token) => session.MoveToPositionAsync(position, token);
		public async Task SynchronizeAsync(CancellationToken token)
		{
			await SendAsync(GameClientPackets.TimeCheck(unchecked((int)world.Clock.NowMillis)), token); await WaitAsync(typeof(SM_TIME_CHECK), _ => true, token);
		}
		public Task VerifyPersistenceAsync(CancellationToken token) => StepAsync("verify-private-store-closure-and-persistence", async ct =>
		{
			await SynchronizeAsync(ct); VerifyOracle();
			var expected = Api.World.Inventory.OrderBy(p => p.Key).ToArray();
			await session.QuitAsync(ct); await session.VerifyOfflineAsync(ct); await session.WaitForReentryAsync(ct);
			await session.ReloginAndVerifyPersistenceAsync(ct); await session.EnterWorldAsync(ct); await SynchronizeAsync(ct);
			Assert.Equal(expected, Api.World.Inventory.OrderBy(p => p.Key)); VerifyOracle();
			var saved = InventoryDAO.LoadItems(CharacterId, StorageType.CUBE).OrderBy(i => i.GetObjectId()).ToArray();
			Assert.Equal(expected.Length, saved.Length);
			for (int i = 0; i < saved.Length; i++)
			{
				Assert.Equal(expected[i].Key, saved[i].GetObjectId()); Assert.Equal(expected[i].Value.ItemId, saved[i].GetItemId());
				Assert.Equal(expected[i].Value.Count, saved[i].GetItemCount());
			}
		}, token);
		private void VerifyOracle()
		{
			var player = world.World.GetPlayer(CharacterId);
			Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel()); Assert.Null(player.GetStore());
			object inventory = typeof(AdminHttpService).Assembly.GetType("Aion.GameServer.Services.Admin.AdminInventory")!.GetMethod("Read")!.Invoke(null, [player])!;
			var response = JsonSerializer.SerializeToElement(new { ok = true, online = true, recipientCharacterId = CharacterId, inventory }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
			BotInventoryOracle.Verify(CharacterId, Api.World.Inventory.Values.ToArray(), Api.World.Kinah, response, Api.World.CubeExpansion);
		}
	}
}
