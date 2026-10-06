using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Tradelist;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

/// <summary>One slot of a coin armor manifest: what is worn there against the tier's piece.</summary>
/// <param name="Action"><c>buy</c> (the piece beats what is worn), <c>wear</c> (it is owned and not worn yet) or <c>keep</c>.</param>
public sealed record NaturalAbyssCoinSlot(ushort Slot, int WornItemId, int WornDefence, int CoinItemId, int CoinDefence, int Cost,
	string Action, string Reason);

/// <summary>AX-06: a tier of Bronze Coin armor compared slot by slot with what the Cleric wears (the operator, 2026-10-06:
/// buy "only if any of it is better than what we are wearing"). The missing coins are the supply the help mechanism adds.</summary>
public sealed record NaturalAbyssCoinManifest(int Level, string Name, NaturalAbyssCoinSlot[] Slots, long CoinsOwned)
{
	public NaturalAbyssCoinSlot[] Buys => Slots.Where(slot => slot.Action == "buy").ToArray();
	public NaturalAbyssCoinSlot[] Wears => Slots.Where(slot => slot.Action == "wear").ToArray();
	public int Cost => Buys.Sum(slot => slot.Cost);
	public long CoinsToSupply => Math.Max(0, Cost - CoinsOwned);
	/// <summary>Nothing on offer beats what is worn.</summary>
	public bool Done => Slots.All(slot => slot.Action == "keep");
}

/// <summary>One piece bought from the coin vendor, as the client saw it paid.</summary>
public sealed record NaturalAbyssCoinPurchase(int Level, int ItemId, int ObjectId, ushort Slot, int Cost, long CoinsBefore, long CoinsAfter,
	long KinahBefore, long KinahAfter);

public static class NaturalAbyssCoinArmorPolicy
{
	public const ushort NotWorn = ushort.MaxValue;

	/// <summary>A piece's physical defence as its tooltip shows it.</summary>
	public static int PhysicalDefence(ItemTemplate? template) =>
		template?.GetModifiers()?.Where(modifier => modifier.GetName() == StatEnum.PHYSICAL_DEFENSE).Sum(modifier => modifier.GetValue()) ?? 0;

	/// <summary>"Better" is the leg's default: more physical defence. A tie is not better, so nothing is bought for it.</summary>
	public static NaturalAbyssCoinManifest Plan(NaturalAbyssCoinArmor armor, NaturalAbyssCoinTier tier, IReadOnlyList<NaturalJourneyItem> inventory,
		Func<int, int> physicalDefence)
	{
		if (armor.Better != "physical-defence") throw new InvalidDataException($"Unknown coin armor rule '{armor.Better}'.");
		var slots = new List<NaturalAbyssCoinSlot>();
		foreach (NaturalCoinGearPurchase piece in tier.Pieces)
		{
			NaturalJourneyItem? worn = inventory.FirstOrDefault(item => item.EquipmentSlot != NotWorn && (item.EquipmentSlot & piece.Slot) != 0);
			int wornDefence = worn == null ? 0 : physicalDefence(worn.ItemId), coinDefence = physicalDefence(piece.ItemId);
			bool owned = inventory.Any(item => item.ItemId == piece.ItemId);
			(string action, string reason) = worn?.ItemId == piece.ItemId ? ("keep", "The piece is worn.")
				: coinDefence <= wornDefence ? ("keep", coinDefence == wornDefence
					? $"A tie at {coinDefence} defence is not better." : $"The worn piece has {wornDefence} defence against {coinDefence}.")
				: owned ? ("wear", $"Owned and not worn: {coinDefence} defence against {wornDefence}.")
				: ("buy", $"{coinDefence} defence against {wornDefence} worn.");
			slots.Add(new(piece.Slot, worn?.ItemId ?? 0, wornDefence, piece.ItemId, coinDefence, piece.Cost, action, reason));
		}
		return new(tier.Level, tier.Name, [.. slots], inventory.Where(item => item.ItemId == armor.CoinItemId).Sum(item => item.Count));
	}
}

/// <summary>The client side of the coin vendor: the reward shop the Altgard coin-gear leg proved (CG-03), for this leg's manifest.</summary>
public static class NaturalAbyssCoinArmorSteps
{
	public const string ManifestDiagnostic = "coin-armor-manifest", PurchaseDiagnostic = "coin-armor-purchase";

	public static Dictionary<string, object?> Row(NaturalAbyssCoinManifest manifest) => new()
	{
		["level"] = manifest.Level, ["tier"] = manifest.Name, ["cost"] = manifest.Cost, ["coinsOwned"] = manifest.CoinsOwned,
		["coinsToSupply"] = manifest.CoinsToSupply,
		["slots"] = manifest.Slots.Select(slot => new Dictionary<string, object?>
		{
			["slot"] = slot.Slot, ["worn"] = slot.WornItemId, ["wornDefence"] = slot.WornDefence, ["piece"] = slot.CoinItemId,
			["pieceDefence"] = slot.CoinDefence, ["cost"] = slot.Cost, ["action"] = slot.Action, ["reason"] = slot.Reason,
		}).ToArray(),
	};

	/// <summary>Open the vendor's reward shop, check that it offers every piece at the manifest's price in coins and nothing
	/// else, and buy the pieces one at a time. Java's reward shop takes no Kinah (TradeService.performBuyFromShop).</summary>
	public static async Task<NaturalAbyssCoinPurchase[]> BuyAsync(INaturalJourneySession session, StaticData data, NaturalAbyssCoinArmor armor,
		NaturalAbyssCoinManifest manifest, int vendor, CancellationToken token)
	{
		BotWorldModel world = session.Api.World;
		long Coins() => world.Inventory.Values.Where(item => item.ItemId == armor.CoinItemId).Sum(item => item.Count);
		if (!world.Objects.TryGetValue(vendor, out BotKnownObject? known) || known.TemplateId != armor.VendorNpcId)
			throw new InvalidDataException("The observed seller is not the approved coin vendor.");
		if (manifest.Cost > Coins()) throw new InvalidDataException($"The manifest costs {manifest.Cost} coins and the Cleric holds {Coins()}.");
		var offered = data.GoodsListDataDh.GetGoodsListById(armor.GoodsListId)?.GetItemIdList() ?? [];
		var bought = new List<NaturalAbyssCoinPurchase>();
		foreach (NaturalAbyssCoinSlot slot in manifest.Buys)
		{
			Acquisition? cost = data.ItemDataDh.GetItemTemplate(slot.CoinItemId)?.GetAcquisition();
			if (!offered.Contains(slot.CoinItemId) || cost == null || cost.Type != AcquisitionType.REWARD || cost.ItemId != armor.CoinItemId ||
				cost.ItemCount != slot.Cost || cost.Ap != 0)
				throw new InvalidDataException($"The reward offer differs for {slot.CoinItemId}.");
			await NaturalDialogProtocol.OpenAsync(session, vendor, token);
			await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == vendor);
			await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(vendor, 2), token);
			await session.WaitForPacketAsync(typeof(SM_TRADELIST), token, packet => packet.Get<int>("targetObjectId") == vendor);
			BotTradeWindow trade = world.Trade ?? throw new InvalidDataException("No observed reward shop.");
			// Java writes TradeNpcType.index(), not its ordinal.
			if (trade.NpcType != TradeNpcType.REWARD.Index() || !trade.ShowBuyTab || !trade.Tabs.Contains(armor.GoodsListId))
				throw new InvalidDataException("The observed shop does not offer the audited chain tab.");
			long coins = Coins(), kinah = world.Kinah;
			HashSet<int> before = world.Inventory.Values.Where(item => item.ItemId == slot.CoinItemId).Select(item => item.ObjectId).ToHashSet();
			await session.SendPacketAsync(GameClientPackets.BuyItem(vendor, 15, [(slot.CoinItemId, 1)]), token);
			await session.SynchronizeAsync(token);
			await session.SendPacketAsync(session.Api.CloseDialog(vendor), token);
			BotInventoryItem[] added = world.Inventory.Values.Where(item => item.ItemId == slot.CoinItemId && !before.Contains(item.ObjectId)).ToArray();
			if (added is not [{ Count: 1 } piece] || coins - Coins() != slot.Cost || world.Kinah != kinah)
				throw new InvalidDataException($"The purchase of {slot.CoinItemId} was not observed: {added.Length} new, coins {coins} -> {Coins()}, " +
					$"Kinah {kinah} -> {world.Kinah}.");
			var purchase = new NaturalAbyssCoinPurchase(manifest.Level, slot.CoinItemId, piece.ObjectId, slot.Slot, slot.Cost, coins, Coins(), kinah, world.Kinah);
			bought.Add(purchase);
			session.TraceDiagnostic(PurchaseDiagnostic, new Dictionary<string, object?>
			{
				["level"] = purchase.Level, ["itemId"] = purchase.ItemId, ["objectId"] = purchase.ObjectId, ["slot"] = purchase.Slot,
				["cost"] = purchase.Cost, ["coinsBefore"] = purchase.CoinsBefore, ["coinsAfter"] = purchase.CoinsAfter, ["kinah"] = purchase.KinahAfter,
			});
		}
		return [.. bought];
	}
}
