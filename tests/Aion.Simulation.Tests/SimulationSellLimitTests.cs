using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Items;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunL7Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2)); var token = timeout.Token;
		Assert.True(CustomConfig.LIMITS_ENABLED); Assert.False(CustomConfig.LIMITS_ENABLE_DYNAMIC_CAP);
		await using var session = new SimulationL0Session(fixture, policy, "b01", 118, "Simselllimit");
		session.BeginStep("s00", "login-create-enter");
		await session.LoginAndAuthenticateAsync(token); await session.CreateCharacterAsync(token); await session.EnterWorldAsync(token); await session.SynchronizeAsync(token);
		var player = fixture.World.GetPlayer(session.CharacterId);
		Assert.Equal(0, player.AccessLevel); Assert.Equal(1, player.GetLevel());
		Assert.Equal(SellLimitScenario.DailyCap, SellLimitExtensions.GetSellLimit(player));
		await SellLimitScenario.RunAsync(new SimSellLimitDriver(this, fixture, session), token);
		session.BeginStep("s09", "quit-and-confirm-offline"); await session.QuitAsync(token); await session.VerifyOfflineAsync(token); policy.AssertClean();
	}
	private sealed class SimSellLimitDriver(SimulationFastScenarioTests owner, SimulationWorldFixture world, SimulationL0Session session) : ISellLimitScenarioDriver
	{
		private int step;
		public BotApi Api => session.Api;
		public long BasePrice => VendorScenario.ReadBasePrice(SellLimitScenario.ItemId);
		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token)
		{
			session.BeginStep($"s{++step:D2}", action); Console.WriteLine($"L7 s{step:D2}: {action}"); return operation(token);
		}
		public async Task<int> PrepareAsync(CancellationToken token)
		{
			var player = world.World.GetPlayer(session.CharacterId);
			Assert.Equal(0, ItemService.AddItem(player, SellLimitScenario.ItemId, SellLimitScenario.SetupCount));
			Assert.Equal(DataManager.ITEM_DATA.GetItemTemplate(SellLimitScenario.ItemId).GetPrice(), BasePrice);
			var point = VendorScenario.Position;
			await owner.TeleportForSetupAsync(session, player, 210010000, point.X - 5, point.Y, point.Z, token);
			var npc = Assert.Single(player.GetPosition().GetWorldMapInstance().GetNpcs(VendorScenario.VendorId), n => n.IsSpawned());
			await session.MoveToPositionAsync(new BotPosition(npc.GetX() - 1, npc.GetY(), npc.GetZ(), 0), token);
			return npc.GetObjectId();
		}
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => session.SendPacketAsync(packet, token);
		public Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token) => session.WaitForPacketAsync(type, token, predicate);
		public Task SynchronizeAsync(CancellationToken token) => session.SynchronizeAsync(token);
		public Task VerifyServerStateAsync(IReadOnlyDictionary<int, long> expected, CancellationToken token)
		{
			token.ThrowIfCancellationRequested(); var player = world.World.GetPlayer(session.CharacterId);
			Assert.Equal(0, player.AccessLevel);
			var actual = player.GetInventory().GetItemsWithKinah().Concat(player.GetEquipment().GetEquippedItems())
				.GroupBy(i => i.GetItemId()).ToDictionary(g => g.Key, g => g.Sum(i => i.GetItemCount()));
			Assert.Equal(expected.OrderBy(p => p.Key), actual.OrderBy(p => p.Key)); return Task.CompletedTask;
		}
		public async Task ReloginAsync(CancellationToken token)
		{
			await session.QuitAsync(token); await session.VerifyOfflineAsync(token); await session.WaitForReentryAsync(token);
			await session.ReloginAndVerifyPersistenceAsync(token); await session.EnterWorldAsync(token); await session.SynchronizeAsync(token);
		}
	}
}
