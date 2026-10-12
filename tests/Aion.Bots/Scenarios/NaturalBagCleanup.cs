using Aion.Bots.Scenarios.Classes;
using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>One stack the cleanup discards, and why.</summary>
public sealed record NaturalBagDiscard(int ObjectId, int ItemId, long Count, string Reason);

/// <summary>
/// The operator, 2026-10-12 (docs/natural-muster.md, rule MR-f): "We need to trash anything no longer needed from all
/// inventories any time we do a gear check or restock supplies." Pure: the caller supplies the client-observed
/// inventory, the class's kit and what its inventory rules call surplus.
/// No longer needed is:
/// a lower tier of a kit family, once the kit's own tier is owned (the Minor and Lesser potions beside the Major one);
/// the speed scroll of the other kind (Awakening for a class that keeps Courage up, and the other way round);
/// a power shard that is not the best the character can use, once it is out of its slot;
/// the powder of a skill level the character has left behind;
/// and what the class's inventory rules would sell: surplus gear and accessories, and what the class cannot use.
/// What is worn, what a leg protects and what an open quest needs is never touched.
/// </summary>
public static class NaturalBagCleanup
{
	public const string Diagnostic = "bag-cleanup";

	/// <summary>The Odella powders, lowest first; a higher one is the reagent of a later skill level.</summary>
	private static readonly int[] Powders = [169300003, 169300004, 169300005, 169300006, 169300007];

	/// <summary>The items a kit row supersedes: the lower tiers that serve its purpose. A tier above the row's is kept; the
	/// character grows into it.</summary>
	private static IEnumerable<int> Superseded(NaturalHelpSupply row)
	{
		switch (row.Family)
		{
			// The potion policies list every tier combat drinks, the best first; the kit's row is the best of them.
			case "life-potion": return NaturalClassGearTable.LifePotionIds.Where(id => id != row.ItemId);
			case "mana-serum": return NaturalIshalgenPotionPolicy.ManaPotionIds.Where(id => id != row.ItemId);
			case "powder": return Powders.Where(id => id < row.ItemId);
			case "running" or "anti-shock" or "awakening" or "courage" or "dp-jelly":
				int tier = NaturalHelpItemPolicy.All.FirstOrDefault(item => item.ItemId == row.ItemId)?.Tier ?? 0;
				// The Movement Speed scroll fills the Running scroll's effect slot and is never chosen.
				return NaturalHelpItemPolicy.All.Where(item => item.Tier < tier &&
					(item.Family == row.Family || row.Family == "running" && item.Family == "movement-speed")).Select(item => item.ItemId);
			default: return [];
		}
	}

	/// <param name="surplus">The sale list of the class's inventory rules (<see cref="NaturalInventoryPlan.Sales"/>).</param>
	/// <param name="keep">Item ids never discarded: what the leg protects and what an open quest needs.</param>
	public static IReadOnlyList<NaturalBagDiscard> Plan(IReadOnlyCollection<BotInventoryItem> inventory, int level, NaturalHelpItemRules help,
		IEnumerable<NaturalInventoryDecision> surplus, IReadOnlySet<int> keep, Func<int, NaturalShardInfo?> shard)
	{
		if (level <= NaturalHelpItemAllowlist.StarterMaxLevel) return [];
		long Owned(int itemId) => inventory.Where(item => item.ItemId == itemId).Sum(item => item.Count);
		var reasons = new Dictionary<int, string>();
		NaturalHelpSupply[] kit = [.. help.Kit.Where(row => row.FromLevel <= level && level <= row.ToLevel && Owned(row.ItemId) > 0)];
		foreach (NaturalHelpSupply row in kit)
			foreach (int itemId in Superseded(row))
				reasons.TryAdd(itemId, $"superseded-{row.Family}");
		string other = help.SharedSlotFamily == "courage" ? "awakening" : "courage";
		if (kit.Any(row => row.Family == help.SharedSlotFamily))
			foreach (NaturalHelpItem scroll in NaturalHelpItemPolicy.All.Where(item => item.Family == other))
				reasons.TryAdd(scroll.ItemId, "other-class-scroll");
		// A kit item is never superseded by another row's family.
		foreach (NaturalHelpSupply row in kit) reasons.Remove(row.ItemId);
		// A shard with less damage than the best one the character can use. One with more is for a later level.
		int bestBoost = NaturalPowerShardPolicy.Decide(inventory, level, modeOn: true, shard).BestItemId is int best ? shard(best)!.Boost : 0;
		Dictionary<int, string> sold = surplus.GroupBy(decision => decision.ObjectId).ToDictionary(group => group.Key, group => group.First().Reason);
		var discards = new List<NaturalBagDiscard>();
		foreach (BotInventoryItem item in inventory.Where(item => item.Count > 0 && item.Details.EquippedSlot.GetValueOrDefault() == 0 &&
			item.ItemId != BotWorldModel.KinahItemId && !keep.Contains(item.ItemId)).OrderBy(item => item.ObjectId))
		{
			string? reason = shard(item.ItemId) is { } info
				? info.Boost < bestBoost ? "lesser-power-shard" : null
				: reasons.GetValueOrDefault(item.ItemId) ?? sold.GetValueOrDefault(item.ObjectId);
			if (reason != null) discards.Add(new(item.ObjectId, item.ItemId, item.Count, reason));
		}
		return discards;
	}
}
