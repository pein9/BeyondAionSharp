using System.Globalization;
using System.Xml;
using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public interface IVendorScenarioDriver
{
	BotApi Api { get; }
	long BasePrice { get; }
	Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken cancellationToken);
	Task<int> PrepareAsync(CancellationToken cancellationToken);
	Task SendAsync(BotClientPacket packet, CancellationToken cancellationToken);
	Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken cancellationToken);
	Task SynchronizeAsync(CancellationToken cancellationToken);
	Task VerifyServerStateAsync(IReadOnlyDictionary<int, long> expected, CancellationToken cancellationToken);
}

/// <summary>E3: buy three bandages, sell two, repurchase those two, retaining all unrelated items and exact kinah.</summary>
public static class VendorScenario
{
	public const int VendorId = 798007;
	public const int ItemId = 169300002;
	public static BotPosition Position { get; } = new(851.671f, 1252.67f, 118.833f, 0);

	public static async Task RunAsync(IVendorScenarioDriver driver, CancellationToken cancellationToken = default)
	{
		int vendor = 0;
		await driver.StepAsync("approach-vendor", async token =>
		{
			vendor = await driver.PrepareAsync(token);
			await driver.SynchronizeAsync(token);
		}, cancellationToken);
		Dictionary<int, long> expected = Totals(driver.Api.World);
		BotVendorPrices prices = driver.Api.World.VendorPrices ?? throw new InvalidDataException("No SM_PRICES was observed.");
		long buyCost = 0, sellReward = 0;
		await driver.StepAsync("buy-three-bandages", async token =>
		{
			await driver.SendAsync(driver.Api.TalkTo(vendor), token);
			await driver.WaitAsync(typeof(SM_DIALOG_WINDOW), packet => packet.Get<int>("targetObjectId") == vendor, token);
			await driver.SendAsync(driver.Api.SelectDialog(vendor, 2), token);
			var trade = await driver.WaitAsync(typeof(SM_TRADELIST), packet => packet.Get<int>("targetObjectId") == vendor, token);
			Require(trade.Get<int[]>("tabs").Contains(132), "Vendor did not offer the expected goods tab.");
			// Minalinerk's trade-list sell rate is 100, so the displayed buy modifier is the vendor modifier.
			buyCost = prices.BuyPrice(driver.BasePrice, trade.Get<int>("buyPriceModifier")) * 3;
			Require(buyCost > 0 && expected.GetValueOrDefault(BotWorldModel.KinahItemId) >= buyCost, "Invalid price or insufficient setup kinah.");
			await driver.SendAsync(driver.Api.Buy(vendor, [(ItemId, 3)]), token);
			expected[ItemId] = expected.GetValueOrDefault(ItemId) + 3;
			expected[BotWorldModel.KinahItemId] -= buyCost;
			await WaitForItemCountAsync(driver, ItemId, expected[ItemId], token);
			await VerifyAsync(driver, expected, token);
		}, cancellationToken);
		await driver.StepAsync("sell-two-bandages", async token =>
		{
			await driver.SendAsync(driver.Api.SelectDialog(vendor, 3), token);
			var sell = await driver.WaitAsync(typeof(SM_SELL_ITEM), packet => packet.Get<int>("targetObjectId") == vendor, token);
			sellReward = BotVendorPrices.SellPrice(driver.BasePrice, sell.Get<int>("buyPriceRate")) * 2;
			Require(sellReward > 0, "Sell-price probe requires a nonzero reward.");
			BotInventoryItem item = driver.Api.World.Inventory.Values.Single(item => item.ItemId == ItemId);
			await driver.SendAsync(driver.Api.Sell(vendor, [(item.ObjectId, 2)]), token);
			expected[ItemId] -= 2;
			expected[BotWorldModel.KinahItemId] += sellReward;
			await WaitForItemCountAsync(driver, BotWorldModel.KinahItemId, expected[BotWorldModel.KinahItemId], token);
			await VerifyAsync(driver, expected, token);
		}, cancellationToken);
		await driver.StepAsync("repurchase-two-bandages", async token =>
		{
			await driver.SendAsync(driver.Api.SelectDialog(vendor, 70), token);
			var repurchase = await driver.WaitAsync(typeof(SM_REPURCHASE), packet => packet.Get<int>("targetObjectId") == vendor, token);
			var items = repurchase.Get<List<IReadOnlyDictionary<string, object?>>>("items");
			Require(items.Count == 1, $"Expected one repurchase stack, found {items.Count}.");
			var item = items[0];
			Require((int)item["itemId"]! == ItemId && (long)item["itemCount"]! == 2 && (long)item["repurchasePrice"]! == sellReward,
				"Repurchase did not preserve the sold item/count/price.");
			await driver.SendAsync(GameClientPackets.BuyItem(vendor, 2, [((int)item["objectId"]!, 2)]), token);
			expected[ItemId] += 2;
			expected[BotWorldModel.KinahItemId] -= sellReward;
			await WaitForItemCountAsync(driver, ItemId, expected[ItemId], token);
			await VerifyAsync(driver, expected, token);
			await driver.SendAsync(driver.Api.SelectDialog(vendor, 70), token);
			var empty = await driver.WaitAsync(typeof(SM_REPURCHASE), packet => packet.Get<int>("targetObjectId") == vendor, token);
			Require(empty.Get<List<IReadOnlyDictionary<string, object?>>>("items").Count == 0, "Repurchased stack remains buyable a second time.");
			await driver.SendAsync(driver.Api.CloseDialog(vendor), token);
			await driver.SynchronizeAsync(token);
			await driver.VerifyServerStateAsync(expected, token);
		}, cancellationToken);
	}

	private static async Task WaitForItemCountAsync(IVendorScenarioDriver driver, int itemId, long count, CancellationToken token)
	{
		BotInventoryItem? item = driver.Api.World.Inventory.Values.SingleOrDefault(item => item.ItemId == itemId);
		if (item?.Count == count)
			return;
		await driver.WaitAsync(item == null ? typeof(SM_INVENTORY_ADD_ITEM) : typeof(SM_INVENTORY_UPDATE_ITEM),
			_ => driver.Api.World.Inventory.Values.Where(value => value.ItemId == itemId).Sum(value => value.Count) == count, token);
	}

	private static async Task VerifyAsync(IVendorScenarioDriver driver, Dictionary<int, long> expected, CancellationToken token)
	{
		await driver.SynchronizeAsync(token);
		Require(expected.OrderBy(pair => pair.Key).SequenceEqual(Totals(driver.Api.World).OrderBy(pair => pair.Key)),
			$"Vendor inventory mismatch. Expected {string.Join(',', expected.OrderBy(pair => pair.Key))}; " +
			$"actual {string.Join(',', Totals(driver.Api.World).OrderBy(pair => pair.Key))}.");
	}
	private static Dictionary<int, long> Totals(BotWorldModel world) => world.Inventory.Values.GroupBy(item => item.ItemId)
		.ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
	private static void Require(bool condition, string message)
	{
		if (!condition) throw new InvalidDataException(message);
	}

	/// <summary>Static client knowledge comes from the checked-in item data, not a hard-coded purchase price.</summary>
	public static long ReadBasePrice(int itemId = ItemId)
	{
		string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "..", ".."));
		using XmlReader reader = XmlReader.Create(Path.Combine(root, "game-server", "data", "static_data", "items", "item_templates.xml"));
		while (reader.Read())
			if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "item_template" && reader.GetAttribute("id") == itemId.ToString(CultureInfo.InvariantCulture))
				return long.Parse(reader.GetAttribute("price") ?? throw new InvalidDataException("Vendor probe item has no price."), CultureInfo.InvariantCulture);
		throw new InvalidDataException($"Probe item {itemId} was not found.");
	}
}
