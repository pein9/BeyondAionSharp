using System.Text.Json;
using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>The operator's frozen CG shopping scope; no general gear-buying permission.</summary>
public sealed record NaturalCoinGear(int QuestId, int Completions, int CoinItemId, int IncomingCoins,
	int RewardCoins, int EndpointCoins, int VendorNpcId, int GoodsListId, int StaffItemId,
	int[] ProtectedItemIds, NaturalCoinGearPurchase[] Purchases, NaturalCoinGearSlot[] BodySlots,
	int SealedBundleId, int ForbiddenStigmaSkillId)
{
	public int Cost => Purchases.Sum(p => p.Cost);

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
			}) || !ProtectedItemIds.Contains(111101650) || !ProtectedItemIds.Contains(StaffItemId) ||
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
		NaturalJourneyItem? staff = before.Inventory?.SingleOrDefault(i => i.ItemId == gear.StaffItemId && i.EquipmentSlot == 3);
		if (Reward != null || Purchases.Length != 0 || oldCount != 0 || newCount != gear.Completions ||
			oldCoins != gear.IncomingCoins || newCoins - oldCoins != gear.RewardCoins || staff == null ||
			after.Inventory?.Any(i => i.ObjectId == staff.ObjectId && i.ItemId == gear.StaffItemId && i.EquipmentSlot == 3) != true)
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
		if (inventory.Count(i => i.ItemId == gear.StaffItemId && i.EquipmentSlot == 3) != 1)
			return Block("coin-staff-changed", "Keep the original two-handed staff equipped before every coin transaction.");
		if (NaturalCoinGearProgress.Count(state, gear.QuestId) != gear.Completions)
			return Block("coin-repeat-count", "Q2293 must have exactly one completed repeat, irrespective of repeatability.");
		if (state.ItemCounts.GetValueOrDefault(gear.SealedBundleId) != 1 || state.SkillIds?.Contains(gear.ForbiddenStigmaSkillId) == true)
			return Block("coin-stigma-protection", "Retain the sealed stigma bundle and use only actually learned regular skills.");
		NaturalCoinGearProgress progress = state.CoinGearProgress ?? NaturalCoinGearProgress.Empty;
		if (progress.Reward is not { BeforeCompletions: 0, AfterCompletions: 1, BeforeCoins: 18, AfterCoins: 23 })
			return Block("coin-receipts-missing", "The five-coin reward receipt is required before shopping or accepting the endpoint.");
		if (!inventory.Any(i => i.ObjectId == progress.StaffObjectId && i.ItemId == gear.StaffItemId && i.EquipmentSlot == 3))
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
			!inventory.Any(i => i.ItemId == 111101650 && i.EquipmentSlot is 0 or 65535))
			return Block("coin-loadout-incomplete", "The five chain body slots, retained cloth gloves and 19-coin balance are required.");
		return new("coin-gear-complete", "complete", "One Q2293 completion and all three exact purchase/equip receipts reconcile.");
	}
}
