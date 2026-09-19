using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public interface IBrokerScenarioDriver
{
	BotApi Api { get; }
	int NpcObjectId { get; }
	string CharacterName { get; }
	Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token);
	Task SendAsync(BotClientPacket packet, CancellationToken token);
	Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token);
	Task SynchronizeAsync(CancellationToken token);
	Task DelayAsync(TimeSpan delay, CancellationToken token);
	Task VerifyPersistenceAsync(CancellationToken token);
}

/// <summary>E9: level-zero seller/buyer, client-observed listings, partial/full purchases and exact payments.</summary>
public static class BrokerScenario
{
	public const int MapId = 110010000, NpcId = 798009, ItemId = 152000102;
	public const long UnitPrice = 1000;
	public static BotPosition Position { get; } = new(1423.63f, 1394.88f, 573.471f, 40);

	public static async Task RunAsync(IBrokerScenarioDriver seller, IBrokerScenarioDriver buyer, CancellationToken token = default)
	{
		foreach (var driver in new[] { seller, buyer })
			await driver.StepAsync("open-broker-and-confirm-no-existing-registrations", async ct =>
			{
				await OpenAsync(driver, ct);
				await RegisteredAsync(driver, ct);
				Require(driver.Api.World.BrokerRegistered.Count == 0, "Broker subject has pre-existing registrations.");
			}, token);
		var source = seller.Api.World.Inventory.Values.Single(i => i.ItemId == ItemId);
		Require(source.Count == 10, "Broker seller needs exactly ten raw ore.");
		long initialTotalKinah = seller.Api.World.Kinah + buyer.Api.World.Kinah;
		long fees = 0;
		BotBrokerListing listing = null!;
		await seller.StepAsync("preview-prices-and-register-four-item-stack", async ct =>
		{
			await seller.SendAsync(GameClientPackets.BrokerSellWindow(source.ObjectId), ct);
			var quote = await WaitAsync(seller, 7, ct);
			Require(quote.Get<int>("objectId") == source.ObjectId && quote.Get<long>("lowestPrice") == 0 && quote.Get<long>("highestPrice") == 0,
				"Fresh broker market already has prices for the scenario item.");
			(listing, long fee) = await RegisterAsync(seller, source, 4, ct);
			fees += fee;
			Require(listing.DaysLeft is 7 or 8, "Default broker registration is not approximately eight days.");
			await RegisteredAsync(seller, ct);
			Require(seller.Api.World.BrokerRegistered.Count == 1 && seller.Api.World.BrokerRegistered.ContainsKey(listing.ObjectId), "Registered-list refresh lost the listing.");
		}, token);
		await buyer.StepAsync("search-absent-item-then-find-seller-listing", async ct =>
		{
			await SearchAsync(buyer, 152000103, ct);
			Require(buyer.Api.World.BrokerSearch is { TotalCount: 0, Items.Count: 0 }, "Search returned a different item template.");
			await SearchAsync(buyer, ItemId, ct);
			AssertSearch(buyer, listing.ObjectId, 4, seller.CharacterName);
		}, token);
		foreach (int quantity in new[] { 1, 3 })
			await buyer.StepAsync($"buy-{quantity}-items-and-check-remainder", async ct =>
			{
				var expected = buyer.Api.World.Inventory.ToDictionary();
				await buyer.SendAsync(GameClientPackets.BuyBrokerItem(buyer.NpcObjectId, listing.ObjectId, quantity), ct);
				await buyer.SynchronizeAsync(ct);
				var added = buyer.Api.World.Inventory.Values.Single(i => i.ItemId == ItemId && !expected.ContainsKey(i.ObjectId));
				Require(added.Count == quantity, "Buyer received a wrong broker quantity.");
				if (quantity == 3) Require(added.ObjectId == listing.ObjectId, "Final purchase changed the original listing's item identity.");
				else Require(added.ObjectId != listing.ObjectId, "Partial purchase reused the still-listed stack identity.");
				expected.Add(added.ObjectId, source with { ObjectId = added.ObjectId, Count = quantity, EquipmentSlot = ushort.MaxValue });
				Pay(expected, UnitPrice * quantity);
				Equal(expected, buyer, "purchase");
				await SearchAsync(buyer, ItemId, ct);
				if (quantity == 1) AssertSearch(buyer, listing.ObjectId, 3, seller.CharacterName);
				else Require(buyer.Api.World.BrokerSearch is { TotalCount: 0, Items.Count: 0 }, "Sold-out listing remains searchable.");
				await seller.SynchronizeAsync(ct);
				Require(seller.Api.World.BrokerSettlementIcon && seller.Api.World.BrokerSettledKinah == (quantity == 1 ? UnitPrice : 4 * UnitPrice), "Seller did not receive the settlement notification.");
			}, token);
		await seller.StepAsync("inspect-two-sales-and-collect-exact-proceeds", async ct =>
		{
			await SettlementsAsync(seller, ct);
			var rows = seller.Api.World.BrokerSettlements!.Items;
			Require(rows.Count == 2 && rows.Select(r => r.Count).Order().SequenceEqual(new long[] { 1, 3 }), "Partial and full sale settlement quantities differ.");
			Require(rows.All(r => r.ItemId == ItemId && r.Proceeds == r.Count * UnitPrice && r.RepeatedCount == r.Count), "Wrong settlement prices or item templates.");
			var expected = seller.Api.World.Inventory.ToDictionary(); Pay(expected, -4 * UnitPrice);
			await seller.SendAsync(GameClientPackets.SettleBrokerAccount(seller.NpcObjectId), ct);
			await seller.SynchronizeAsync(ct);
			Equal(expected, seller, "settlement");
			Require(!seller.Api.World.BrokerSettlementIcon && seller.Api.World.BrokerSettledKinah == 0 && seller.Api.World.BrokerSettlements is { TotalCount: 0, Items.Count: 0 }, "Collected sales were not cleared.");
		}, token);
		await seller.VerifyPersistenceAsync(token);
		await seller.StepAsync("reopen-broker-after-settlement-relogin", ct => OpenAsync(seller, ct), token);
		await buyer.VerifyPersistenceAsync(token);
		await seller.StepAsync("register-two-items-and-cancel-without-refunding-fee", async ct =>
		{
			var remaining = seller.Api.World.Inventory[source.ObjectId];
			var (canceled, fee) = await RegisterAsync(seller, remaining, 2, ct); fees += fee;
			var expected = seller.Api.World.Inventory.ToDictionary();
			expected.Add(canceled.ObjectId, source with { ObjectId = canceled.ObjectId, Count = 2, EquipmentSlot = ushort.MaxValue });
			await seller.SendAsync(GameClientPackets.CancelBrokerItem(seller.NpcObjectId, canceled.ObjectId), ct);
			var receipt = await WaitAsync(seller, 4, ct);
			Require(receipt.Get<byte>("result") == 0 && receipt.Get<int>("objectId") == canceled.ObjectId, "Wrong cancellation receipt.");
			await seller.SynchronizeAsync(ct);
			Equal(expected, seller, "cancellation");
			Require(seller.Api.World.BrokerRegistered.Count == 0, "Canceled listing remains registered.");
			Require(seller.Api.World.Kinah + buyer.Api.World.Kinah == initialTotalKinah - fees, "Broker operation created/lost kinah beyond registration fees.");
			Require(seller.Api.World.Inventory.Values.Where(i => i.ItemId == ItemId).Sum(i => i.Count) == 6, "Seller did not retain six unsold ore.");
		}, token);
		await seller.VerifyPersistenceAsync(token);
	}

	/// <summary>SIM-only expiry window; caller scopes the existing registration-days config to zero.</summary>
	public static async Task RunExpiryAsync(IBrokerScenarioDriver seller, CancellationToken token)
	{
		await seller.StepAsync("expire-unsold-registration-on-virtual-broker-tick", async ct =>
		{
			await OpenAsync(seller, ct);
			var item = seller.Api.World.Inventory.Values.Single(i => i.ItemId == ItemId && i.Count == 2);
			var (listing, _) = await RegisterAsync(seller, item, 2, ct);
			Require(listing.DaysLeft == 0, "Expiry profile did not use zero-day registrations.");
			await RegisteredAsync(seller, ct);
			Require(seller.Api.World.BrokerRegistered.ContainsKey(item.ObjectId), "Registration expired before the periodic clock tick.");
			await seller.DelayAsync(TimeSpan.FromSeconds(61), ct);
			await seller.SynchronizeAsync(ct);
			await RegisteredAsync(seller, ct);
			Require(seller.Api.World.BrokerRegistered.Count == 0, "Expired item remains registered.");
			await SettlementsAsync(seller, ct);
			var expired = seller.Api.World.BrokerSettlements!.Items;
			Require(expired.Count == 1 && expired[0].ItemId == ItemId && expired[0].Count == 2 && expired[0].Proceeds == 0,
				"Expired item was sold, duplicated or lost instead of becoming an unsold settlement.");
			var expected = seller.Api.World.Inventory.ToDictionary(); expected.Add(item.ObjectId, item);
			await seller.SendAsync(GameClientPackets.SettleBrokerAccount(seller.NpcObjectId), ct);
			await seller.SynchronizeAsync(ct);
			Equal(expected, seller, "expired-item collection");
			Require(!seller.Api.World.BrokerSettlementIcon && seller.Api.World.BrokerSettlements is { TotalCount: 0, Items.Count: 0 }, "Expired settlement was not consumed.");
		}, token);
		await seller.VerifyPersistenceAsync(token);
	}

	private static async Task<(BotBrokerListing, long)> RegisterAsync(IBrokerScenarioDriver driver, BotInventoryItem item, long count, CancellationToken token)
	{
		var expected = driver.Api.World.Inventory.ToDictionary();
		long baseFee = (long)(UnitPrice * count * 0.02f);
		long fee = baseFee < 10 ? 10 : (driver.Api.World.VendorPrices ?? throw new InvalidDataException("Missing broker service prices.")).ServicePrice(baseFee);
		Pay(expected, fee);
		if (item.Count == count) expected.Remove(item.ObjectId); else expected[item.ObjectId] = item with { Count = item.Count - count };
		await driver.SendAsync(GameClientPackets.RegisterBrokerItem(driver.NpcObjectId, item.ObjectId, UnitPrice, count, true), token);
		var receipt = await WaitAsync(driver, 3, token);
		Require(receipt.Get<byte>("message") == 0, "Broker registration refused.");
		var listing = receipt.Get<BotBrokerListing[]>("items").Single();
		Require(listing.ItemId == item.ItemId && listing.Count == count && listing.RepeatedCount == count && listing.TotalPrice == UnitPrice * count && listing.SplittingAvailable, "Wrong broker registration receipt.");
		await driver.SynchronizeAsync(token);
		Equal(expected, driver, "registration");
		return (listing, fee);
	}
	private static async Task OpenAsync(IBrokerScenarioDriver driver, CancellationToken token)
	{
		await driver.SendAsync(driver.Api.Target(driver.NpcObjectId), token);
		await driver.SendAsync(driver.Api.TalkTo(driver.NpcObjectId), token);
		await driver.WaitAsync(typeof(SM_DIALOG_WINDOW), p => p.Get<int>("targetObjectId") == driver.NpcObjectId, token);
		await driver.SendAsync(driver.Api.SelectDialog(driver.NpcObjectId, DialogAction.OPEN_VENDOR), token);
		await driver.WaitAsync(typeof(SM_DIALOG_WINDOW), p => p.Get<int>("targetObjectId") == driver.NpcObjectId && p.Get<ushort>("dialogPageId") == 13, token);
	}
	private static async Task RegisteredAsync(IBrokerScenarioDriver driver, CancellationToken token)
	{
		await driver.SendAsync(GameClientPackets.BrokerRegistered(driver.NpcObjectId), token); await WaitAsync(driver, 1, token);
	}
	private static async Task SearchAsync(IBrokerScenarioDriver driver, int itemId, CancellationToken token)
	{
		await driver.SendAsync(GameClientPackets.BrokerSearch(driver.NpcObjectId, 4, 0, 0, itemId), token); await WaitAsync(driver, 0, token);
	}
	private static async Task SettlementsAsync(IBrokerScenarioDriver driver, CancellationToken token)
	{
		await driver.SendAsync(GameClientPackets.BrokerSettlements(driver.NpcObjectId), token);
		await driver.WaitAsync(typeof(SM_BROKER_SERVICE), p => p.Get<byte>("action") == 5 && !p.Get<bool>("iconOnly"), token);
	}
	private static Task<DecodedBotServerPacket> WaitAsync(IBrokerScenarioDriver driver, byte action, CancellationToken token) =>
		driver.WaitAsync(typeof(SM_BROKER_SERVICE), p => p.Get<byte>("action") == action, token);
	private static void AssertSearch(IBrokerScenarioDriver driver, int id, long count, string seller)
	{
		var page = driver.Api.World.BrokerSearch ?? throw new InvalidDataException("Missing broker search response.");
		Require(page.TotalCount == 1 && page.Items.Count == 1 && page.Page == 0, "Broker search returned the wrong number/page of listings.");
		var item = page.Items[0];
		Require(item.ObjectId == id && item.ItemId == ItemId && item.Count == count && item.TotalPrice == count * UnitPrice
			&& item.AverageUnitPrice == UnitPrice && item.Seller == seller && item.SplittingAvailable, "Broker search fields differ from the registered stack.");
	}
	private static void Pay(Dictionary<int, BotInventoryItem> items, long amount)
	{
		var kinah = items.Values.Single(i => i.ItemId == BotWorldModel.KinahItemId); items[kinah.ObjectId] = kinah with { Count = kinah.Count - amount };
	}
	private static void Equal(Dictionary<int, BotInventoryItem> expected, IBrokerScenarioDriver driver, string action)
	{
		var actual = driver.Api.World.Inventory;
		var differences = expected.Keys.Union(actual.Keys).Order().Where(id => expected.GetValueOrDefault(id) != actual.GetValueOrDefault(id))
			.Select(id => $"{id}: expected {expected.GetValueOrDefault(id)}; actual {actual.GetValueOrDefault(id)}").ToArray();
		Require(differences.Length == 0, $"Unexpected inventory delta after broker {action}: {string.Join(Environment.NewLine, differences)}");
	}
	private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
