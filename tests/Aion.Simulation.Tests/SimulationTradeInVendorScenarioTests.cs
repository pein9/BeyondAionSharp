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
	private async Task RunE11Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3)); var token = timeout.Token;
		await using var first = new SimulationL0Session(fixture, policy, "b01", 88, "Aesimvenda");
		await using var second = new SimulationL0Session(fixture, policy, "b02", 89, "Aesimvendb");
		await using var third = new SimulationL0Session(fixture, policy, "b03", 90, "Aesimvendc");
		var sessions = new[] { first, second, third };
		foreach (var session in sessions)
		{
			session.BeginStep("s00", "login-create-enter-and-prepare-vendor-funds");
			await session.LoginAndAuthenticateAsync(token); await session.CreateCharacterAsync(token); await session.EnterWorldAsync(token);
			await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
			var player = fixture.World.GetPlayer(session.CharacterId);
			Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
			Assert.Equal(0, ItemService.AddItem(player, BotWorldModel.KinahItemId, 20_000_000));
			if (session == first)
			{
				Assert.Equal(0, ItemService.AddItem(player, TradeInVendorScenario.InsigniaId, 29400));
				Assert.Equal(0, ItemService.AddItem(player, TradeInVendorScenario.CourageId, 1200));
			}
		}
		await TradeInVendorScenario.RunAsync(sessions.Select(s => (ITradeInVendorScenarioDriver)new SimTradeInDriver(this, fixture, s)).ToArray(), token);
		policy.AssertClean();
	}

	private sealed class SimTradeInDriver(SimulationFastScenarioTests owner, SimulationWorldFixture world, SimulationL0Session session) : ITradeInVendorScenarioDriver
	{
		private int step;
		public BotApi Api => session.Api;
		public async Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token)
		{
			session.BeginStep($"s{++step:D2}", action); Console.WriteLine($"E11 {session.CharacterId} s{step:D2}: {action}");
			try { await operation(token); }
			catch (Exception exception)
			{
				throw new InvalidOperationException($"E11 step {action}. Recent packets:{Environment.NewLine}" +
					string.Join(Environment.NewLine, session.PacketHistory.TakeLast(20).Select(p => p.PacketType.Name + " " + JsonSerializer.Serialize(p.Fields))), exception);
			}
		}
		public async Task<int> ApproachAsync(int mapId, int npcId, BotPosition position, CancellationToken token)
		{
			var player = world.World.GetPlayer(session.CharacterId);
			await owner.TeleportForSetupAsync(session, player, mapId, position.X - 5, position.Y, position.Z, token);
			var npc = Assert.Single(player.GetPosition().GetWorldMapInstance().GetNpcs(npcId), n => n.IsSpawned());
			await session.MoveToPositionAsync(position with { X = position.X - 1 }, token);
			await SynchronizeAsync(token); return npc.GetObjectId();
		}
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => session.SendPacketAsync(packet, token);
		public async Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token)
		{
			await session.DrainServerPacketsAsync(token);
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(5));
			try { return await session.WaitForPacketAsync(type, timeout.Token, predicate); }
			catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException($"E11 did not receive {type.Name}."); }
		}
		public async Task SynchronizeAsync(CancellationToken token)
		{
			await SendAsync(GameClientPackets.TimeCheck(unchecked((int)world.Clock.NowMillis)), token); await WaitAsync(typeof(SM_TIME_CHECK), _ => true, token);
		}
		public Task VerifyPersistenceAsync(CancellationToken token) => StepAsync("verify-vendor-inventory-persistence", async ct =>
		{
			await SynchronizeAsync(ct); VerifyOracle();
			var expected = Api.World.Inventory.OrderBy(p => p.Key).ToArray();
			await session.QuitAsync(ct); await session.VerifyOfflineAsync(ct); await session.WaitForReentryAsync(ct);
			await session.ReloginAndVerifyPersistenceAsync(ct); await session.EnterWorldAsync(ct); await SynchronizeAsync(ct);
			Assert.Equal(expected, Api.World.Inventory.OrderBy(p => p.Key)); VerifyOracle();
			var saved = InventoryDAO.LoadItems(session.CharacterId, StorageType.CUBE).OrderBy(i => i.GetObjectId()).ToArray();
			Assert.Equal(expected.Length, saved.Length);
			for (int i = 0; i < saved.Length; i++)
			{
				Assert.Equal(expected[i].Key, saved[i].GetObjectId()); Assert.Equal(expected[i].Value.ItemId, saved[i].GetItemId());
				Assert.Equal(expected[i].Value.Count, saved[i].GetItemCount());
			}
		}, token);
		private void VerifyOracle()
		{
			var player = world.World.GetPlayer(session.CharacterId);
			Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
			object inventory = typeof(AdminHttpService).Assembly.GetType("Aion.GameServer.Services.Admin.AdminInventory")!.GetMethod("Read")!.Invoke(null, [player])!;
			var response = JsonSerializer.SerializeToElement(new { ok = true, online = true, recipientCharacterId = session.CharacterId, inventory }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
			BotInventoryOracle.Verify(session.CharacterId, Api.World.Inventory.Values.ToArray(), Api.World.Kinah, response, Api.World.CubeExpansion);
		}
	}
}
