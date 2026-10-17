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
	string Action, string Reason, string Stat = NaturalAbyssCoinArmorPolicy.DefenceStat);

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

	public const string DefenceStat = "physical-defence", BoostStat = "magic-boost";

	/// <summary>NR-38b: the weapon rule of a class other than the contract's: its own weapon number
	/// (<see cref="Classes.NaturalGearRules.WeaponNumber"/>), given as the function the staff's magic boost is given as.</summary>
	public const string ClassWeaponStat = "weapon-number";

	/// <summary>NR-54a: a shield is compared by the same number of the class's gear rules, and bought on its own tab.</summary>
	public const string ShieldStat = "shield-number";

	/// <summary>"Better" is the leg's default: more physical defence. A tie is not better, so nothing is bought for it.
	/// AX-12c: the tier's staff by the staff rule, more magic boost than the worn staff.</summary>
	public static NaturalAbyssCoinManifest Plan(NaturalAbyssCoinArmor armor, NaturalAbyssCoinTier tier, IReadOnlyList<NaturalJourneyItem> inventory,
		Func<int, int> physicalDefence, Func<int, int> staffMagicBoost, Func<int, (int Level, int Grade)>? rank = null)
	{
		// L2 (docs/level-31-profiles.md): with a rank, a piece of a higher level and no lower grade beats the worn one and a
		// lower one does not; at the same level the number decides. At the check-in at 31 an Elite Rank 7 piece's +16 defence
		// tied it with the Elite Rank 6 one, and twelve level-26 pieces stayed on.
		int Ranked(int coin, NaturalJourneyItem? held)
		{
			if (rank == null || held == null) return 0;
			(int Level, int Grade) offered = rank(coin), worn = rank(held.ItemId);
			if (offered.Level > worn.Level && offered.Grade >= worn.Grade) return 1;
			if (offered.Level < worn.Level && offered.Grade <= worn.Grade) return -1;
			return 0;
		}
		string Levels(int coin, NaturalJourneyItem held) => $"a level-{rank!(coin).Level} piece against the worn level-{rank(held.ItemId).Level}";
		if (armor.Better != DefenceStat || armor.StaffBetter is not (BoostStat or ClassWeaponStat))
			throw new InvalidDataException($"Unknown coin gear rule '{armor.Better}', '{armor.StaffBetter}'.");
		// NR-38b: the contract's class compares a staff by magic boost; another class its weapon by its own number.
		(string weaponName, string statName) = armor.StaffBetter == BoostStat ? ("staff", "magic boost") : ("weapon", "weapon number");
		var slots = new List<NaturalAbyssCoinSlot>();
		foreach (NaturalCoinGearPurchase piece in tier.Pieces)
		{
			NaturalJourneyItem? worn = inventory.FirstOrDefault(item => item.EquipmentSlot != NotWorn && (item.EquipmentSlot & piece.Slot) != 0);
			int wornDefence = worn == null ? 0 : physicalDefence(worn.ItemId), coinDefence = physicalDefence(piece.ItemId);
			bool owned = inventory.Any(item => item.ItemId == piece.ItemId);
			int ranked = worn?.ItemId == piece.ItemId ? 0 : Ranked(piece.ItemId, worn);
			(string action, string reason) = worn?.ItemId == piece.ItemId ? ("keep", "The piece is worn.")
				: ranked < 0 ? ("keep", $"The worn piece is better: {Levels(piece.ItemId, worn!)}.")
				: ranked > 0 ? (owned ? "wear" : "buy", $"{(owned ? "Owned and not worn: " : "")}{Levels(piece.ItemId, worn!)}.")
				: coinDefence <= wornDefence ? ("keep", coinDefence == wornDefence
					? $"A tie at {coinDefence} defence is not better." : $"The worn piece has {wornDefence} defence against {coinDefence}.")
				: owned ? ("wear", $"Owned and not worn: {coinDefence} defence against {wornDefence}.")
				: ("buy", $"{coinDefence} defence against {wornDefence} worn.");
			slots.Add(new(piece.Slot, worn?.ItemId ?? 0, wornDefence, piece.ItemId, coinDefence, piece.Cost, action, reason));
		}
		if (armor.Weapons && tier.Staff is { } staff)
		{
			NaturalJourneyItem? held = inventory.FirstOrDefault(item => item.EquipmentSlot != NotWorn && (item.EquipmentSlot & 1) != 0);
			int heldBoost = held == null ? 0 : staffMagicBoost(held.ItemId), coinBoost = staffMagicBoost(staff.ItemId);
			if (coinBoost <= 0) throw new InvalidDataException($"The coin {weaponName} {staff.ItemId} has no {statName}.");
			int rankedStaff = held?.ItemId == staff.ItemId ? 0 : Ranked(staff.ItemId, held);
			bool staffOwned = inventory.Any(item => item.ItemId == staff.ItemId);
			(string action, string reason) = held?.ItemId == staff.ItemId ? ("keep", $"The {weaponName} is worn.")
				: rankedStaff < 0 ? ("keep", $"The worn {weaponName} is better: {Levels(staff.ItemId, held!)}.")
				: rankedStaff > 0 ? (staffOwned ? "wear" : "buy", $"{(staffOwned ? "Owned and not worn: " : "")}{Levels(staff.ItemId, held!)}.")
				: coinBoost <= heldBoost ? ("keep", coinBoost == heldBoost
					? $"A tie at {coinBoost} {statName} is not better." : $"The worn {weaponName} has {heldBoost} {statName} against {coinBoost}.")
				: inventory.Any(item => item.ItemId == staff.ItemId) ? ("wear", $"Owned and not worn: {coinBoost} {statName} against {heldBoost}.")
				: ("buy", $"{coinBoost} {statName} against {heldBoost} worn.");
			slots.Add(new(staff.Slot, held?.ItemId ?? 0, heldBoost, staff.ItemId, coinBoost, staff.Cost, action, reason, armor.StaffBetter));
		}
		long coinsOwned = inventory.Where(item => item.ItemId == armor.CoinItemId).Sum(item => item.Count);
		// NR-54a: the tier's shield, for a class that holds one. 2026-10-12: it is bought when it is better, like every
		// other piece; the supplied coins have no limit.
		if (armor.Weapons && tier.Shield is { } shield)
		{
			NaturalJourneyItem? held = inventory.FirstOrDefault(item => item.EquipmentSlot == shield.Slot);
			int heldNumber = held == null ? 0 : staffMagicBoost(held.ItemId), coinNumber = staffMagicBoost(shield.ItemId);
			int rankedShield = held?.ItemId == shield.ItemId ? 0 : Ranked(shield.ItemId, held);
			bool shieldOwned = inventory.Any(item => item.ItemId == shield.ItemId);
			(string action, string reason) = held?.ItemId == shield.ItemId ? ("keep", "The shield is worn.")
				: rankedShield < 0 ? ("keep", $"The worn shield is better: {Levels(shield.ItemId, held!)}.")
				: rankedShield > 0 ? (shieldOwned ? "wear" : "buy", $"{(shieldOwned ? "Owned and not worn: " : "")}{Levels(shield.ItemId, held!)}.")
				: coinNumber <= heldNumber ? ("keep", coinNumber == heldNumber
					? $"A tie at {coinNumber} is not better." : $"The worn shield has {heldNumber} against {coinNumber}.")
				: inventory.Any(item => item.ItemId == shield.ItemId) ? ("wear", $"Owned and not worn: {coinNumber} against {heldNumber}.")
				: ("buy", $"{coinNumber} against {heldNumber} worn.");
			slots.Add(new(shield.Slot, held?.ItemId ?? 0, heldNumber, shield.ItemId, coinNumber, shield.Cost, action, reason, ShieldStat));
		}
		// 2026-10-12: the tier's weapon again, for the off hand of a class that holds two. A copy is spare when it is owned
		// beyond the one the main hand holds or is about to hold.
		if (armor.Weapons && tier.OffHand is { } second)
		{
			NaturalJourneyItem? held = inventory.FirstOrDefault(item => item.EquipmentSlot == second.Slot);
			int heldNumber = held == null ? 0 : staffMagicBoost(held.ItemId), coinNumber = staffMagicBoost(second.ItemId);
			NaturalAbyssCoinSlot? main = slots.FirstOrDefault(slot => slot.Stat == armor.StaffBetter && slot.CoinItemId == second.ItemId);
			int forMain = main is { Action: "wear" } || main is { Action: "keep" } && main.WornItemId == second.ItemId ? 1 : 0;
			int forOffHand = held?.ItemId == second.ItemId ? 1 : 0;
			bool spare = inventory.Count(item => item.ItemId == second.ItemId) > forMain + forOffHand;
			int rankedSecond = held?.ItemId == second.ItemId ? 0 : Ranked(second.ItemId, held);
			(string action, string reason) = held?.ItemId == second.ItemId ? ("keep", "The second weapon is worn.")
				: rankedSecond < 0 ? ("keep", $"The worn second weapon is better: {Levels(second.ItemId, held!)}.")
				: rankedSecond > 0 ? (spare ? "wear" : "buy", $"{(spare ? "Owned and not worn: " : "")}{Levels(second.ItemId, held!)}.")
				: coinNumber <= heldNumber ? ("keep", coinNumber == heldNumber
					? $"A tie at {coinNumber} weapon number is not better." : $"The worn second weapon has {heldNumber} weapon number against {coinNumber}.")
				: spare ? ("wear", $"Owned and not worn: {coinNumber} weapon number against {heldNumber}.")
				: ("buy", $"{coinNumber} weapon number against {heldNumber} in the off hand.");
			slots.Add(new(second.Slot, held?.ItemId ?? 0, heldNumber, second.ItemId, coinNumber, second.Cost, action, reason, armor.StaffBetter));
		}
		return new(tier.Level, tier.Name, [.. slots], coinsOwned);
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
			["pieceDefence"] = slot.CoinDefence, ["cost"] = slot.Cost, ["action"] = slot.Action, ["reason"] = slot.Reason, ["stat"] = slot.Stat,
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
		var bought = new List<NaturalAbyssCoinPurchase>();
		foreach (NaturalAbyssCoinSlot slot in manifest.Buys)
		{
			// The armor is on the vendor's chain tab and the staff on its weapon tab. NR-54a: the shield on a tab of its own.
			int tab = slot.Stat == NaturalAbyssCoinArmorPolicy.DefenceStat ? armor.GoodsListId
				: slot.Stat == NaturalAbyssCoinArmorPolicy.ShieldStat ? armor.ShieldGoodsListId : armor.StaffGoodsListId;
			var offered = data.GoodsListDataDh.GetGoodsListById(tab)?.GetItemIdList() ?? [];
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
			if (trade.NpcType != TradeNpcType.REWARD.Index() || !trade.ShowBuyTab || !trade.Tabs.Contains(tab))
				throw new InvalidDataException($"The observed shop does not offer the audited tab {tab}.");
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
