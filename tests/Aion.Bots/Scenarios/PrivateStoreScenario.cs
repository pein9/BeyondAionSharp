using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public interface IPrivateStoreScenarioDriver
{
	BotApi Api { get; }
	int CharacterId { get; }
	BotPosition CurrentPosition { get; }
	Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token);
	Task SendAsync(BotClientPacket packet, CancellationToken token);
	Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token);
	Task SynchronizeAsync(CancellationToken token);
	Task MoveAsync(BotPosition position, CancellationToken token);
	Task VerifyPersistenceAsync(CancellationToken token);
}

/// <summary>E10: packet-only private-store offers, a peer purchase, explicit close before movement, and sell-out.</summary>
public static class PrivateStoreScenario
{
	public const int MapId = 210010000, ItemId = 152000102;
	public const long UnitPrice = 1000;
	public const string StoreName = "Ore for adventurers – 4.8";
	public static BotPosition Position { get; } = new(1213, 1040, 140.756f, 0);

	public static async Task RunAsync(IPrivateStoreScenarioDriver seller, IPrivateStoreScenarioDriver buyer, CancellationToken token = default)
	{
		await seller.SynchronizeAsync(token); await buyer.SynchronizeAsync(token);
		var source = seller.Api.World.Inventory.Values.Single(i => i.ItemId == ItemId);
		Require(source.Count == 10 && buyer.Api.World.Inventory.Values.All(i => i.ItemId != ItemId), "Incorrect private-store item setup.");
		long totalKinah = seller.Api.World.Kinah + buyer.Api.World.Kinah;
		await OpenAsync(4, token);
		await BuyAsync(2, soldOut: false, token);
		await seller.StepAsync("close-store-then-move-with-peer-observation", async ct =>
		{
			var before = seller.Api.World.Inventory.ToDictionary();
			var origin = seller.CurrentPosition;
			// Java/C# CM_MOVE do not close stores. The client must explicitly close before walking.
			await seller.SendAsync(GameClientPackets.PrivateStore(), ct);
			await ObserveEmotionAsync(EmotionType.CLOSE_PRIVATESHOP, ct);
			await seller.MoveAsync(origin with { X = origin.X + 1 }, ct);
			await buyer.WaitAsync(typeof(SM_MOVE), p => p.Get<int>("objectId") == seller.CharacterId && Math.Abs(p.Get<float>("x") - origin.X - 1) < 0.1f, ct);
			Require(!seller.Api.World.OpenPrivateStores.Contains(seller.CharacterId) && !buyer.Api.World.OpenPrivateStores.Contains(seller.CharacterId), "Closed store remains open to a client.");
			Require(!buyer.Api.World.PrivateStoreNames.ContainsKey(seller.CharacterId) && !buyer.Api.World.PrivateStoreListings.ContainsKey(seller.CharacterId), "Peer retained the closed store's name or catalog.");
			Equal(before, seller, "closing and moving");
		}, token);
		await OpenAsync(2, token);
		await BuyAsync(2, soldOut: true, token);
		Require(seller.Api.World.Kinah + buyer.Api.World.Kinah == totalKinah, "Private store failed kinah conservation.");
		Require(seller.Api.World.Inventory.Values.Where(i => i.ItemId == ItemId).Sum(i => i.Count) == 6
			&& buyer.Api.World.Inventory.Values.Where(i => i.ItemId == ItemId).Sum(i => i.Count) == 4, "Private store failed item conservation.");
		await seller.VerifyPersistenceAsync(token); await buyer.VerifyPersistenceAsync(token);

		async Task OpenAsync(ushort count, CancellationToken cancellationToken)
		{
			await seller.StepAsync($"open-and-name-store-with-{count}-ore", async ct =>
			{
				var before = seller.Api.World.Inventory.ToDictionary();
				await seller.SendAsync(GameClientPackets.PrivateStore(new PrivateStoreOffer(source.ObjectId, ItemId, count, UnitPrice)), ct);
				await ObserveEmotionAsync(EmotionType.OPEN_PRIVATESHOP, ct);
				await seller.SendAsync(GameClientPackets.PrivateStoreName(StoreName), ct);
				foreach (var observer in new[] { seller, buyer })
				{
					await observer.WaitAsync(typeof(SM_PRIVATE_STORE_NAME), p => p.Get<int>("sellerObjectId") == seller.CharacterId, ct);
					Require(observer.Api.World.PrivateStoreNames[seller.CharacterId] == StoreName, "Store name differs between clients.");
					Require(observer.Api.World.OpenPrivateStores.Contains(seller.CharacterId), "Store-open notification was not applied.");
				}
				await RefreshAsync(count, ct);
				Equal(before, seller, "opening"); // Listing is not inventory escrow.
			}, cancellationToken);
		}
		async Task BuyAsync(ushort count, bool soldOut, CancellationToken cancellationToken)
		{
			await buyer.StepAsync(soldOut ? "buy-remaining-stock-and-observe-auto-close" : "buy-partial-stock-and-refresh-list", async ct =>
			{
				var expectedSeller = seller.Api.World.Inventory.ToDictionary();
				var expectedBuyer = buyer.Api.World.Inventory.ToDictionary();
				var original = expectedSeller[source.ObjectId];
				expectedSeller[source.ObjectId] = original with { Count = original.Count - count };
				Pay(expectedSeller, -count * UnitPrice); Pay(expectedBuyer, count * UnitPrice);
				await buyer.SendAsync(GameClientPackets.BuyItem(seller.CharacterId, 0, [(0, count)]), ct); // Store indices, not template IDs.
				if (soldOut) await ObserveEmotionAsync(EmotionType.CLOSE_PRIVATESHOP, ct);
				await buyer.SynchronizeAsync(ct); await seller.SynchronizeAsync(ct);
				var prior = expectedBuyer.Values.SingleOrDefault(i => i.ItemId == ItemId);
				if (prior is null)
				{
					var added = buyer.Api.World.Inventory.Values.Single(i => i.ItemId == ItemId);
					Require(added.ObjectId != source.ObjectId && !expectedBuyer.ContainsKey(added.ObjectId), "Partial sale reused the seller's inventory identity.");
					expectedBuyer.Add(added.ObjectId, source with { ObjectId = added.ObjectId, Count = count, EquipmentSlot = ushort.MaxValue });
				}
				else expectedBuyer[prior.ObjectId] = prior with { Count = prior.Count + count };
				Equal(expectedSeller, seller, "sale"); Equal(expectedBuyer, buyer, "purchase");
				if (soldOut)
				{
					Require(!seller.Api.World.OpenPrivateStores.Contains(seller.CharacterId) && !buyer.Api.World.OpenPrivateStores.Contains(seller.CharacterId), "Sold-out shop remains open.");
				}
				else await RefreshAsync(2, ct);
			}, cancellationToken);
		}
		async Task RefreshAsync(ushort count, CancellationToken ct)
		{
			await buyer.SendAsync(buyer.Api.Target(seller.CharacterId), ct);
			await buyer.SendAsync(buyer.Api.SelectDialog(seller.CharacterId, DialogAction.BUY), ct);
			await buyer.WaitAsync(typeof(SM_PRIVATE_STORE), p => p.Get<bool>("hasStore") && p.Get<int>("sellerObjectId") == seller.CharacterId, ct);
			var rows = buyer.Api.World.PrivateStoreListings[seller.CharacterId];
			Require(rows.Count == 1, "Private-store catalog has an unexpected row count.");
			var row = rows[0]; var inventoryItem = seller.Api.World.Inventory[source.ObjectId];
			Require(row.ObjectId == source.ObjectId && row.ItemId == ItemId && row.Count == count && row.UnitPrice == UnitPrice
				&& row.General.ItemCount == inventoryItem.Count && row.General.ItemMask == inventoryItem.ItemMask
				&& row.General.Creator == inventoryItem.Creator && row.Details == inventoryItem.Details, "Private-store offer or item metadata differs.");
		}
		async Task ObserveEmotionAsync(EmotionType emotion, CancellationToken ct)
		{
			foreach (var observer in new[] { seller, buyer })
				await observer.WaitAsync(typeof(SM_EMOTION), p => p.Get<int>("senderObjectId") == seller.CharacterId && p.Get<byte>("emotionType") == (byte)emotion, ct);
		}
	}
	private static void Pay(Dictionary<int, BotInventoryItem> items, long amount)
	{
		var kinah = items.Values.Single(i => i.ItemId == BotWorldModel.KinahItemId); items[kinah.ObjectId] = kinah with { Count = kinah.Count - amount };
	}
	private static void Equal(Dictionary<int, BotInventoryItem> expected, IPrivateStoreScenarioDriver driver, string action) =>
		Require(expected.OrderBy(p => p.Key).SequenceEqual(driver.Api.World.Inventory.OrderBy(p => p.Key)), $"Unexpected inventory delta after private-store {action}.");
	private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
