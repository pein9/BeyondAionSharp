using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Trade;
using Aion.GameServer.Utils;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunE3Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(fixture, policy, "b01", 46, "Aesimvendor");
		session.BeginStep("s00", "login-create-enter");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token);
		await session.EnterWorldAsync(token);
		await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		var driver = new SimVendorDriver(this, fixture, session, player);
		await VendorScenario.RunAsync(driver, token);
		int prices = PricesConfig.DEFAULT_PRICES, taxes = PricesConfig.DEFAULT_TAXES, modifier = PricesConfig.DEFAULT_MODIFIER;
		try
		{
			// Exercise different live PricesService state too; the bot receives the normal price notification.
			PricesConfig.DEFAULT_PRICES += 31;
			PricesConfig.DEFAULT_TAXES += 13;
			PricesConfig.DEFAULT_MODIFIER += 7;
			await VendorScenario.RunAsync(driver, token);
		}
		finally
		{
			PricesConfig.DEFAULT_PRICES = prices;
			PricesConfig.DEFAULT_TAXES = taxes;
			PricesConfig.DEFAULT_MODIFIER = modifier;
		}
		policy.AssertClean();
	}

	private sealed class SimVendorDriver(SimulationFastScenarioTests owner, SimulationWorldFixture world,
		SimulationL0Session session, Player player) : IVendorScenarioDriver
	{
		private int step;
		public BotApi Api => session.Api;
		public long BasePrice => DataManager.ITEM_DATA.GetItemTemplate(VendorScenario.ItemId).GetPrice();
		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token)
		{
			session.BeginStep($"s{++step:D2}", action);
			return operation(token);
		}
		public async Task<int> PrepareAsync(CancellationToken token)
		{
			var point = VendorScenario.Position;
			await owner.TeleportForSetupAsync(session, player, 210010000, point.X - 5, point.Y, point.Z, token);
			var npc = player.GetPosition().GetWorldMapInstance().GetNpcs(VendorScenario.VendorId).First(npc => npc.IsSpawned());
			Assert.Equal(100, DataManager.TRADE_LIST_DATA.GetTradeListTemplate(VendorScenario.VendorId).GetSellPriceRate());
			Assert.Equal(VendorScenario.ReadBasePrice(), BasePrice);
			await session.MoveToPositionAsync(new BotPosition(npc.GetX() - 1, npc.GetY(), npc.GetZ(), 0), token);
			PacketSendUtility.SendPacket(player, new SM_PRICES());
			await SynchronizeAsync(token);
			Assert.Equal(new BotVendorPrices(PricesService.GetGlobalPrices(player.GetRace()),
				PricesService.GetGlobalPricesModifier(), PricesService.GetTaxes(player.GetRace())), Api.World.VendorPrices);
			Assert.Equal(PricesService.GetBuyPrice(BasePrice, player.GetRace()),
				Api.World.VendorPrices!.BuyPrice(BasePrice, PricesService.GetVendorBuyModifier()));
			return npc.GetObjectId();
		}
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => session.SendPacketAsync(packet, token);
		public Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token) =>
			session.WaitForPacketAsync(type, token, predicate);
		public async Task SynchronizeAsync(CancellationToken token)
		{
			await session.SendPacketAsync(GameClientPackets.TimeCheck(unchecked((int)world.Clock.NowMillis)), token);
			await session.WaitForPacketAsync(typeof(SM_TIME_CHECK), token);
		}
		public Task VerifyServerStateAsync(IReadOnlyDictionary<int, long> expected, CancellationToken token)
		{
			Assert.Null(player.GetInteractionTask());
			var actual = player.GetInventory().GetItemsWithKinah().Concat(player.GetEquipment().GetEquippedItems())
				.GroupBy(item => item.GetItemId()).ToDictionary(group => group.Key, group => group.Sum(item => item.GetItemCount()));
			Assert.Equal(expected.OrderBy(pair => pair.Key), actual.OrderBy(pair => pair.Key));
			Assert.Empty(Aion.GameServer.Services.RepurchaseService.GetInstance().GetRepurchaseItems(player.GetObjectId()));
			return Task.CompletedTask;
		}
	}
}
