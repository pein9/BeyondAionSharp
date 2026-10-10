using System.Text.Json;
using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>The operator's frozen CG shopping scope; no general gear-buying permission.</summary>
public sealed record NaturalCoinGear(int QuestId, int Completions, int CoinItemId, int IncomingCoins,
	int RewardCoins, int EndpointCoins, int VendorNpcId, int GoodsListId, int StaffItemId,
	int[] ProtectedItemIds, NaturalCoinGearPurchase[] Purchases, NaturalCoinGearSlot[] BodySlots,
	int SealedBundleId, int ForbiddenStigmaSkillId)
{
	/// <summary>The accepted run's cloth gloves, Altgard Legionary Gloves: the pair the contract names.</summary>
	public const int ContractGlovesItemId = 111101650;
	public const ushort GlovesSlot = 16;

	/// <summary>
	/// NR-19a: the gloves the handguards purchase replaces, which stay in the bag to the end of the leg. A run that starts
	/// from an accepted snapshot has the contract's own pair. The continuous journey binds this to the pair the character
	/// wears as the leg starts (NaturalAltgardContinuation.BindIncoming); 0 when it wears none, and then none is asked for.
	/// </summary>
	public int ReplacedGlovesItemId { get; init; } = ContractGlovesItemId;

	/// <summary>NR-38a: the slot the retained weapon is worn in: 3, both hands, for the contract's staff; a one-hand
	/// weapon's is 1.</summary>
	public ushort WeaponSlot { get; init; } = 3;

	/// <summary>The equipment slot of a shield: the off hand beside a one-hand weapon.</summary>
	public const ushort OffHandSlot = 2;

	/// <summary>NR-54a: the vendor's trade tab that holds the manifest's shield; 0 when the scope buys none.</summary>
	public int ShieldGoodsListId { get; init; }

	/// <summary>The equipment slot of each body piece a coin manifest names.</summary>
	public static readonly IReadOnlyDictionary<string, ushort> BodySlotMasks = new Dictionary<string, ushort>
	{
		["TORSO"] = 8, ["GLOVE"] = 16, ["SHOES"] = 32, ["SHOULDER"] = 2048, ["PANTS"] = 4096,
	};

	public int Cost => Purchases.Sum(p => p.Cost);

	/// <summary>
	/// NR-38a: the same scope for a class other than the contract's, made when the leg is taken up. The vendor and the
	/// pieces are the class's manifest (NR-38); it buys the pieces its gear rules score above what it wears in the slot,
	/// in the manifest's order, while the coins it has and the quest's reward pay for them. The weapon it holds is the
	/// one it must keep, whatever it is. The coin counts are what is observed. The rest of the scope is unchanged.
	/// </summary>
	/// <param name="inventory">What the character owns and wears as the leg starts.</param>
	/// <param name="score">The class's gear score of an item id.</param>
	/// <param name="goodsListId">The vendor's trade tab that holds the manifest's armor.</param>
	/// <param name="shieldGoodsListId">NR-54a: the tab that holds the manifest's shield, for a class that holds one.</param>
	public NaturalCoinGear ForClass(NaturalCoinManifest manifest, IReadOnlyList<NaturalJourneyItem> inventory, Func<int, long> score,
		int goodsListId, int shieldGoodsListId = 0)
	{
		NaturalJourneyItem weapon = inventory.SingleOrDefault(item => item.EquipmentSlot is 1 or 3)
			?? throw new InvalidDataException("The coin-gear leg needs a weapon held in the main hand.");
		int incoming = checked((int)inventory.Where(item => item.ItemId == CoinItemId).Sum(item => item.Count));
		int balance = incoming + RewardCoins;
		var purchases = new List<NaturalCoinGearPurchase>();
		var body = new List<NaturalCoinGearSlot>();
		int replacedGloves = 0;
		foreach (NaturalCoinPiece piece in manifest.Armor)
		{
			ushort slot = BodySlotMasks[piece.Slot];
			NaturalJourneyItem? worn = inventory.FirstOrDefault(item => item.EquipmentSlot == slot);
			if ((worn == null || score(piece.ItemId) > score(worn.ItemId)) && piece.Cost <= balance)
			{
				balance -= piece.Cost;
				purchases.Add(new(piece.ItemId, piece.Cost, slot));
				body.Add(new(slot, piece.ItemId));
				if (slot == GlovesSlot) replacedGloves = worn?.ItemId ?? 0;
			}
			else if (worn != null) body.Add(new(slot, worn.ItemId));
		}
		// NR-54a: the manifest's shield, when it beats the one held, from the coins the armor leaves. No coin is added for it.
		bool buysShield = false;
		if (manifest.Shield is { } shield)
		{
			NaturalJourneyItem? held = inventory.FirstOrDefault(item => item.EquipmentSlot == OffHandSlot);
			buysShield = (held == null || score(shield.ItemId) > score(held.ItemId)) && shield.Cost <= balance;
			if (buysShield)
			{
				balance -= shield.Cost;
				purchases.Add(new(shield.ItemId, shield.Cost, OffHandSlot));
				body.Add(new(OffHandSlot, shield.ItemId));
			}
			else if (held != null) body.Add(new(OffHandSlot, held.ItemId));
		}
		return this with
		{
			ShieldGoodsListId = buysShield ? shieldGoodsListId : 0,
			VendorNpcId = manifest.VendorNpcId, GoodsListId = goodsListId, StaffItemId = weapon.ItemId, WeaponSlot = weapon.EquipmentSlot,
			IncomingCoins = incoming, EndpointCoins = balance, Purchases = [.. purchases], BodySlots = [.. body],
			ProtectedItemIds = [.. ProtectedItemIds.Concat(purchases.Select(purchase => purchase.ItemId)).Append(weapon.ItemId).Distinct().Order()],
			ReplacedGlovesItemId = replacedGloves,
		};
	}

	public void Validate(NaturalAltgardContract contract)
	{
		if (QuestId != 2293 || Completions != 1 || CoinItemId != 186000006 || IncomingCoins != 18 || RewardCoins != 5 ||
			EndpointCoins != 19 || VendorNpcId != 203689 || GoodsListId != 985 || StaffItemId != 101501357 ||
			SealedBundleId != 188053787 || ForbiddenStigmaSkillId != 11504 ||
			!contract.Order.SequenceEqual([QuestId]) || Purchases.Length != 3 ||
			!Purchases.OrderBy(p => p.ItemId).SequenceEqual(new NaturalCoinGearPurchase[]
			{
				new(111501065, 1, 16), new(112501015, 1, 2048), new(113501074, 2, 4096),
			}) || Cost != 4 || BodySlots.Length != 5 ||
			!BodySlots.OrderBy(s => s.Slot).SequenceEqual(new NaturalCoinGearSlot[]
			{
				new(8, 110551139), new(16, 111501065), new(32, 114501726), new(2048, 112501015), new(4096, 113501074),
			}) || !ProtectedItemIds.Contains(ContractGlovesItemId) || !ProtectedItemIds.Contains(StaffItemId) ||
			Purchases.Any(p => !ProtectedItemIds.Contains(p.ItemId)))
			throw new InvalidDataException("Coin gear differs from the approved three-armour/four-coin scope.");
	}
}

public sealed record NaturalCoinGearPurchase(int ItemId, int Cost, ushort Slot);
public sealed record NaturalCoinGearSlot(ushort Slot, int ItemId);
public sealed record NaturalCoinRewardReceipt(byte BeforeCompletions, byte AfterCompletions, long BeforeCoins, long AfterCoins);
public sealed record NaturalCoinPurchaseReceipt(int ItemId, int ObjectId, long BeforeCoins, long AfterCoins, long Kinah);

/// <summary>Client-observed transaction receipts. Save alongside checkpoints; never replay them to restore server state.</summary>
public sealed record NaturalCoinGearProgress(NaturalCoinRewardReceipt? Reward, NaturalCoinPurchaseReceipt[] Purchases, int StaffObjectId = 0)
{
	public static NaturalCoinGearProgress Empty => new(null, []);

	/// <summary>Restore diagnostic receipts only after an actual login observes the complete purchased endpoint.
	/// This never grants items, applies saved state or authorizes another transaction.</summary>
	public static NaturalCoinGearProgress ReadVerifiedEndpoint(string path, int characterId,
		NaturalCoinGear gear, NaturalAltgardObservation state)
	{
		using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
		JsonElement root = document.RootElement;
		if (!root.TryGetProperty("verified", out JsonElement verified) || verified.ValueKind != JsonValueKind.True ||
			!root.TryGetProperty("CharacterId", out JsonElement identity) || identity.GetInt32() != characterId)
			throw new InvalidDataException("Coin endpoint receipts are unverified or belong to another character.");
		NaturalCoinGearProgress progress = root.GetProperty("after").GetProperty("CoinGearProgress")
			.Deserialize<NaturalCoinGearProgress>() ?? throw new InvalidDataException("Coin endpoint has no receipts.");
		NaturalCoinGearDecision check = NaturalCoinGearPolicy.Decide(gear, state with { CoinGearProgress = progress });
		if (check.Action != "coin-gear-complete")
			throw new InvalidDataException($"Restored coin receipts disagree with the actual endpoint: {check.Reason}");
		return progress;
	}

	public NaturalCoinGearProgress ObserveReward(NaturalCoinGear gear, NaturalAltgardObservation before, NaturalAltgardObservation after)
	{
		byte oldCount = Count(before, gear.QuestId), newCount = Count(after, gear.QuestId);
		long oldCoins = Coins(before, gear), newCoins = Coins(after, gear);
		NaturalJourneyItem? staff = before.Inventory?.SingleOrDefault(i => i.ItemId == gear.StaffItemId && i.EquipmentSlot == gear.WeaponSlot);
		if (Reward != null || Purchases.Length != 0 || oldCount != 0 || newCount != gear.Completions ||
			oldCoins != gear.IncomingCoins || newCoins - oldCoins != gear.RewardCoins || staff == null ||
			after.Inventory?.Any(i => i.ObjectId == staff.ObjectId && i.ItemId == gear.StaffItemId && i.EquipmentSlot == gear.WeaponSlot) != true)
			throw new InvalidDataException("Q2293 did not produce the single approved completion and five-coin receipt.");
		return this with { Reward = new(oldCount, newCount, oldCoins, newCoins), StaffObjectId = staff.ObjectId };
	}

	public NaturalCoinGearProgress ObservePurchase(NaturalCoinGear gear, int itemId,
		NaturalAltgardObservation before, NaturalAltgardObservation after)
	{
		NaturalCoinGearPurchase item = gear.Purchases.Single(p => p.ItemId == itemId);
		NaturalJourneyItem[] added = (after.Inventory ?? []).Where(i => i.ItemId == itemId &&
			!(before.Inventory ?? []).Any(old => old.ObjectId == i.ObjectId)).ToArray();
		long oldCoins = Coins(before, gear), newCoins = Coins(after, gear);
		if (Reward == null || Purchases.Any(p => p.ItemId == itemId) || before.ItemCounts.GetValueOrDefault(itemId) != 0 ||
			after.ItemCounts.GetValueOrDefault(itemId) != 1 || added.Length != 1 || added[0].Count != 1 ||
			oldCoins != Reward.AfterCoins - Purchases.Sum(p => p.BeforeCoins - p.AfterCoins) ||
			oldCoins - newCoins != item.Cost || before.Kinah != after.Kinah ||
			Count(before, gear.QuestId) != gear.Completions || Count(after, gear.QuestId) != gear.Completions)
			throw new InvalidDataException($"Coin purchase {itemId} lacks the exact item, currency and unchanged-Kinah receipt.");
		return this with { Purchases = [.. Purchases, new(itemId, added[0].ObjectId, oldCoins, newCoins, before.Kinah)] };
	}

	internal static byte Count(NaturalAltgardObservation state, int questId) => state.CompletedQuestCounts?.GetValueOrDefault(questId) ?? 0;
	internal static long Coins(NaturalAltgardObservation state, NaturalCoinGear gear) => state.ItemCounts.GetValueOrDefault(gear.CoinItemId);
}

public sealed record NaturalCoinGearDecision(string Action, string Outcome, string Reason, int? ItemId = null);

public static class NaturalCoinGearPolicy
{
	public static NaturalCoinGearDecision Decide(NaturalCoinGear gear, NaturalAltgardObservation state)
	{
		NaturalCoinGearDecision Block(string action, string reason) => new(action, "blocked", reason);
		NaturalJourneyItem[] inventory = state.Inventory ?? [];
		if (inventory.Count(i => i.ItemId == gear.StaffItemId && i.EquipmentSlot == gear.WeaponSlot) != 1)
			return Block("coin-staff-changed", "Keep the original two-handed staff equipped before every coin transaction.");
		if (NaturalCoinGearProgress.Count(state, gear.QuestId) != gear.Completions)
			return Block("coin-repeat-count", "Q2293 must have exactly one completed repeat, irrespective of repeatability.");
		if (state.ItemCounts.GetValueOrDefault(gear.SealedBundleId) != 1 || state.SkillIds?.Contains(gear.ForbiddenStigmaSkillId) == true)
			return Block("coin-stigma-protection", "Retain the sealed stigma bundle and use only actually learned regular skills.");
		NaturalCoinGearProgress progress = state.CoinGearProgress ?? NaturalCoinGearProgress.Empty;
		// NR-38a: the scope's own counts: 18 and 23 for the contract's class.
		if (progress.Reward is not { BeforeCompletions: 0, AfterCompletions: 1 } reward || reward.BeforeCoins != gear.IncomingCoins ||
			reward.AfterCoins != gear.IncomingCoins + gear.RewardCoins)
			return Block("coin-receipts-missing", "The five-coin reward receipt is required before shopping or accepting the endpoint.");
		if (!inventory.Any(i => i.ObjectId == progress.StaffObjectId && i.ItemId == gear.StaffItemId && i.EquipmentSlot == gear.WeaponSlot))
			return Block("coin-staff-changed", "The retained staff must have the same object identity as before the reward.");
		if (progress.Purchases.Select(p => p.ItemId).Distinct().Count() != progress.Purchases.Length ||
			progress.Purchases.Any(p => !gear.Purchases.Any(i => i.ItemId == p.ItemId && p.BeforeCoins - p.AfterCoins == i.Cost)))
			return Block("coin-purchase-receipts", "The saved shopping receipts do not match the approved manifest.");
		long expected = progress.Reward.AfterCoins - progress.Purchases.Sum(p => p.BeforeCoins - p.AfterCoins);
		if (NaturalCoinGearProgress.Coins(state, gear) != expected)
			return Block("coin-balance-changed", "Observed Iron Coins do not reconcile with the reward and purchase receipts.");
		foreach (NaturalCoinGearPurchase purchase in gear.Purchases)
		{
			NaturalCoinPurchaseReceipt? receipt = progress.Purchases.SingleOrDefault(p => p.ItemId == purchase.ItemId);
			NaturalJourneyItem[] owned = inventory.Where(i => i.ItemId == purchase.ItemId).ToArray();
			if (receipt == null)
			{
				if (owned.Length != 0) return Block("coin-unrecorded-purchase", "Reconcile the owned armour with its real debit before spending again.");
				if (state.FreeCubeSlots is not int free || free < 1)
					return Block("coin-cube-space", "Observe at least one free main-cube slot before buying armour.");
				if (expected < purchase.Cost) return Block("coin-insufficient-funds", "The observed coin balance cannot fund the approved purchase.");
				return new("coin-purchase", "planned", $"Buy one {purchase.ItemId} from Lohaban for {purchase.Cost} Iron Coins.", purchase.ItemId);
			}
			if (owned.Length != 1 || owned[0].ObjectId != receipt.ObjectId || owned[0].Count != 1)
				return Block("coin-purchased-item-missing", "The purchased object in the receipt must still be owned.");
			if (owned[0].EquipmentSlot != purchase.Slot)
				return new("coin-equip", "planned", $"Equip the observed armour object {receipt.ObjectId} in slot {purchase.Slot}.", purchase.ItemId);
		}
		if (expected != gear.EndpointCoins || gear.BodySlots.Any(s => !inventory.Any(i => i.ItemId == s.ItemId && i.EquipmentSlot == s.Slot)) ||
			gear.ReplacedGlovesItemId != 0 && !inventory.Any(i => i.ItemId == gear.ReplacedGlovesItemId && i.EquipmentSlot is 0 or 65535))
			return Block("coin-loadout-incomplete", "The five chain body slots, the replaced gloves in the bag and 19-coin balance are required.");
		return new("coin-gear-complete", "complete", "One Q2293 completion and all three exact purchase/equip receipts reconcile.");
	}
}
