using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public interface ITradeInVendorScenarioDriver
{
	BotApi Api { get; }
	Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token);
	Task<int> ApproachAsync(int mapId, int npcId, BotPosition position, CancellationToken token);
	Task SendAsync(BotClientPacket packet, CancellationToken token);
	Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token);
	Task SynchronizeAsync(CancellationToken token);
	Task VerifyPersistenceAsync(CancellationToken token);
}

/// <summary>E11: real trade-in materials, character purchase limits and shared vendor stock.</summary>
public static class TradeInVendorScenario
{
	public const int TradeInMapId = 110070000, TradeInNpcId = 205315, TradeInTab = 39, ChestId = 188051574;
	public const int InsigniaId = 186000130, CourageId = 186000137;
	public const int LimitedMapId = 110010000, LimitedNpcId = 798426, LimitedTab = 5020, FluxId = 152011087;
	public const int LimitedSellRate = 6000;
	public static BotPosition TradeInPosition { get; } = new(483.3091f, 251.0723f, 126.9759f, 60);
	public static BotPosition LimitedPosition { get; } = new(1843, 1492, 590.125f, 0);

	public static async Task RunAsync(IReadOnlyList<ITradeInVendorScenarioDriver> buyers, CancellationToken token = default)
	{
		Require(buyers.Count == 3, "E11 needs three independent buyers to distinguish individual limits from shared stock.");
		var first = buyers[0]; int tradeIn = 0;
		await first.StepAsync("approach-trade-in-vendor-and-inspect-tabs", async ct =>
		{
			tradeIn = await first.ApproachAsync(TradeInMapId, TradeInNpcId, TradeInPosition, ct);
			await OpenAsync(first, tradeIn, tradeIn: true, ct);
			Require(first.Api.World.TradeIn is { TargetObjectId: var id, BuyPriceModifier: 100, PriceRate: 100 } window
				&& id == tradeIn && window.Tabs.SequenceEqual(new[] { TradeInTab }), "Unexpected trade-in catalog.");
		}, token);
		await first.StepAsync("trade-two-chests-for-both-insignia-types", async ct =>
		{
			var expected = first.Api.World.Inventory.ToDictionary();
			var insignia = expected.Values.Single(i => i.ItemId == InsigniaId);
			var courage = expected.Values.Single(i => i.ItemId == CourageId);
			Require(insignia.Count == 29400 && courage.Count == 1200 && expected.Values.All(i => i.ItemId != ChestId), "Incorrect E11 trade-in setup.");
			await first.SendAsync(GameClientPackets.BuyTradeIn(tradeIn, 0, ChestId, 2, insignia.ObjectId, courage.ObjectId), ct);
			await first.SynchronizeAsync(ct);
			expected[insignia.ObjectId] = insignia with { Count = 9800 };
			expected[courage.ObjectId] = courage with { Count = 400 };
			AddNewProduct(expected, first, ChestId, 2);
			Equal(expected, first, "two trade-in chests");
		}, token);
		await first.VerifyPersistenceAsync(token);
		await first.StepAsync("trade-last-materials-and-merge-existing-chest-stack", async ct =>
		{
			tradeIn = await first.ApproachAsync(TradeInMapId, TradeInNpcId, TradeInPosition, ct);
			await OpenAsync(first, tradeIn, tradeIn: true, ct);
			var expected = first.Api.World.Inventory.ToDictionary();
			var insignia = expected.Values.Single(i => i.ItemId == InsigniaId);
			var courage = expected.Values.Single(i => i.ItemId == CourageId);
			var chest = expected.Values.Single(i => i.ItemId == ChestId);
			await first.SendAsync(GameClientPackets.BuyTradeIn(tradeIn, 0, ChestId, 1, insignia.ObjectId, courage.ObjectId), ct);
			await first.SynchronizeAsync(ct);
			expected.Remove(insignia.ObjectId); expected.Remove(courage.ObjectId);
			expected[chest.ObjectId] = chest with { Count = 3 };
			Equal(expected, first, "last trade-in materials");
		}, token);

		int[] vendors = new int[buyers.Count];
		for (int index = 0; index < buyers.Count; index++)
		{
			var buyer = buyers[index]; int buyerIndex = index;
			await buyer.StepAsync("approach-limited-stock-vendor", async ct =>
			{
				vendors[buyerIndex] = await buyer.ApproachAsync(LimitedMapId, LimitedNpcId, LimitedPosition, ct);
				await OpenAsync(buyer, vendors[buyerIndex], tradeIn: false, ct);
				AssertStock(buyer, 0, 2);
			}, token);
		}
		Require(vendors.Distinct().Count() == 1, "Buyers must share the same actual vendor.");
		await PurchaseAsync(first, vendors[0], token);
		await RejectAsync(first, vendors[0], "reject-character-limit-with-stock-still-available", token);
		await first.VerifyPersistenceAsync(token);
		await first.StepAsync("character-limit-survives-relogin", async ct =>
		{
			vendors[0] = await first.ApproachAsync(LimitedMapId, LimitedNpcId, LimitedPosition, ct);
			await OpenAsync(first, vendors[0], tradeIn: false, ct); AssertStock(first, 1, 1);
		}, token);
		await RejectAsync(first, vendors[0], "reject-character-limit-after-relogin", token);
		await PurchaseAsync(buyers[1], vendors[1], token);
		await buyers[2].StepAsync("fresh-buyer-observes-sold-out-stock", async ct =>
		{
			await OpenAsync(buyers[2], vendors[2], tradeIn: false, ct); AssertStock(buyers[2], 0, 0);
		}, token);
		await RejectAsync(buyers[2], vendors[2], "reject-exhausted-shared-stock-for-unlimited-buyer", token);
		foreach (var buyer in buyers) await buyer.VerifyPersistenceAsync(token);
	}

	private static async Task PurchaseAsync(ITradeInVendorScenarioDriver buyer, int npc, CancellationToken token) =>
		await buyer.StepAsync("buy-one-limited-item-and-refresh-stock", async ct =>
		{
			await OpenAsync(buyer, npc, tradeIn: false, ct);
			var stock = buyer.Api.World.Trade!.LimitedItems.Single(i => i.ItemId == FluxId);
			Require(stock.BuyCount == 0 && stock.SellLimit > 0, "Buyer cannot make its first limited purchase.");
			var expected = buyer.Api.World.Inventory.ToDictionary();
			var kinah = expected.Values.Single(i => i.ItemId == BotWorldModel.KinahItemId);
			var prices = buyer.Api.World.VendorPrices ?? throw new InvalidDataException("Missing SM_PRICES.");
			int combinedModifier = buyer.Api.World.Trade.BuyPriceModifier;
			Require(combinedModifier * 100 % LimitedSellRate == 0, "Combined vendor modifier is not exactly reversible for this data contract.");
			long price = prices.BuyListPrice(VendorScenario.ReadBasePrice(FluxId), combinedModifier * 100 / LimitedSellRate, LimitedSellRate, 1);
			Require(price > 0 && kinah.Count > price, "Invalid limited-item price or missing setup funds.");
			await buyer.SendAsync(buyer.Api.Buy(npc, [(FluxId, 1)]), ct); await buyer.SynchronizeAsync(ct);
			expected[kinah.ObjectId] = kinah with { Count = kinah.Count - price };
			AddNewProduct(expected, buyer, FluxId, 1); Equal(expected, buyer, "limited purchase");
			await OpenAsync(buyer, npc, tradeIn: false, ct); AssertStock(buyer, 1, stock.SellLimit - 1);
		}, token);

	private static async Task RejectAsync(ITradeInVendorScenarioDriver buyer, int npc, string action, CancellationToken token) =>
		await buyer.StepAsync(action, async ct =>
		{
			var expected = buyer.Api.World.Inventory.ToDictionary(); int messageStart = buyer.Api.World.SystemMessages.Count;
			await buyer.SendAsync(buyer.Api.Buy(npc, [(FluxId, 1)]), ct);
			await buyer.WaitAsync(typeof(SM_SYSTEM_MESSAGE), p => p.Get<string>("name") == "STR_MSG_LIMITED_BUYING_CANT_SELECT_NO_ITEMS", ct);
			await buyer.SynchronizeAsync(ct); Equal(expected, buyer, action);
			Require(buyer.Api.World.SystemMessages.Skip(messageStart).Select(m => m.Name).SequenceEqual(new[] { "STR_MSG_LIMITED_BUYING_CANT_SELECT_NO_ITEMS" }),
				"Limited purchase must refuse exactly once without side effects.");
		}, token);

	private static async Task OpenAsync(ITradeInVendorScenarioDriver buyer, int npc, bool tradeIn, CancellationToken token)
	{
		await buyer.SendAsync(buyer.Api.Target(npc), token);
		await buyer.SendAsync(buyer.Api.TalkTo(npc), token);
		await buyer.WaitAsync(typeof(SM_DIALOG_WINDOW), p => p.Get<int>("targetObjectId") == npc, token);
		await buyer.SendAsync(buyer.Api.SelectDialog(npc, (ushort)(tradeIn ? DialogAction.TRADE_IN : DialogAction.BUY)), token);
		await buyer.WaitAsync(tradeIn ? typeof(SM_TRADE_IN_LIST) : typeof(SM_TRADELIST), p => p.Get<int>("targetObjectId") == npc, token);
	}
	private static void AssertStock(ITradeInVendorScenarioDriver buyer, int purchased, int remaining)
	{
		var trade = buyer.Api.World.Trade ?? throw new InvalidDataException("Missing limited-vendor window.");
		Require(trade.Tabs.Contains(LimitedTab), "Missing limited-vendor goods tab.");
		var stock = trade.LimitedItems.Single(i => i.ItemId == FluxId);
		Require(stock.BuyCount == purchased && stock.SellLimit == remaining, $"Expected bought={purchased}, stock={remaining}; received {stock}.");
	}
	private static void AddNewProduct(Dictionary<int, BotInventoryItem> expected, ITradeInVendorScenarioDriver buyer, int itemId, long count)
	{
		var product = buyer.Api.World.Inventory.Values.Single(i => i.ItemId == itemId);
		Require(!expected.ContainsKey(product.ObjectId) && product.Count == count && product.EquipmentSlot == 65535, "Product identity/count/slot is incorrect.");
		expected.Add(product.ObjectId, product);
	}
	private static void Equal(Dictionary<int, BotInventoryItem> expected, ITradeInVendorScenarioDriver buyer, string action)
	{
		var actual = buyer.Api.World.Inventory;
		var differences = expected.Keys.Union(actual.Keys).Order().Where(id => expected.GetValueOrDefault(id) != actual.GetValueOrDefault(id))
			.Select(id => $"{id}: expected {expected.GetValueOrDefault(id)}; actual {actual.GetValueOrDefault(id)}").ToArray();
		Require(differences.Length == 0, $"Unexpected inventory delta after {action}: {string.Join(Environment.NewLine, differences)}");
	}
	private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
