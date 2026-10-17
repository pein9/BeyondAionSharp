using System.Collections.Concurrent;
using System.Xml.Linq;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>One stack the cleanup disposes of, and why.</summary>
public sealed record NaturalBagDiscard(int ObjectId, int ItemId, long Count, string Reason);

/// <summary>What the cleanup decided for the stacks in the cube: what goes, and what stays with the reason it stays.</summary>
public sealed record NaturalBagPlan(IReadOnlyList<NaturalBagDiscard> Discards, IReadOnlyList<NaturalBagDiscard> Kept);

/// <summary>
/// The quests that refer to an item outside their rewards, from the shipped quest data: an item one of them needs is a
/// quest's item, and it is needed while one of them is being worked.
/// </summary>
public static class NaturalQuestItemIndex
{
	private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<int, int[]>> Loaded = new();

	public static IReadOnlyDictionary<int, int[]> Load(string root) => Loaded.GetOrAdd(root, static path =>
	{
		var index = new Dictionary<int, HashSet<int>>();
		foreach (XElement quest in XDocument.Load(Path.Combine(path, "game-server/data/static_data/quest_data/quest_data.xml")).Root!.Elements("quest"))
			foreach (XAttribute item in quest.DescendantsAndSelf().Where(node => !node.AncestorsAndSelf().Any(up =>
				up.Name.LocalName.Contains("reward", StringComparison.Ordinal))).Attributes("item_id"))
			{
				if (!index.TryGetValue((int)item, out HashSet<int>? quests)) index[(int)item] = quests = [];
				quests.Add((int)quest.Attribute("id")!);
			}
		return index.ToDictionary(pair => pair.Key, pair => pair.Value.Order().ToArray());
	});
}

/// <summary>
/// The operator, 2026-10-12 (docs/natural-muster.md, rule MR-f): "We need to trash anything no longer needed from all
/// inventories any time we do a gear check or restock supplies", and then: "We need to clean up things we don't need.
/// We either need to use it or get rid of it." Pure: the caller supplies the client-observed inventory, the class's kit,
/// what its inventory rules decide and the quests it is working.
/// A stack in the cube stays only when a rule uses it: the kit of the level; a potion, scroll or shard the class's
/// policies drink, read or wear, while the kit's own tier is not owned in its place; the Tea of Repose and the
/// experience amulet (<see cref="NaturalBoostItems"/>); gear the gear rules will wear, now or at a later level; what the
/// leg being played names; and an item of a quest that is being worked. Everything else goes: sold when a merchant is
/// at hand and takes it, discarded otherwise. What is worn is never touched. This changes when crafting and gathering
/// are trained: their materials have no use until then.
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

	/// <summary>What a rule of the class uses at this level, by item: the kit, and what its policies would reach for
	/// without the kit.</summary>
	private static HashSet<int> Used(NaturalHelpItemRules help, int level)
	{
		NaturalHelpSupply[] kit = [.. help.Kit.Where(row => row.FromLevel <= level && level <= row.ToLevel)];
		HashSet<int> used = [.. kit.Select(row => row.ItemId), .. NaturalClassGearTable.LifePotionIds];
		if (help.ManaPotion(level)) used.UnionWith(NaturalIshalgenPotionPolicy.ManaPotionIds);
		used.UnionWith(NaturalHelpItemPolicy.All.Where(item => item.Family == help.SharedSlotFamily ||
			item.Family is "running" or "anti-shock" or "dp-jelly").Select(item => item.ItemId));
		if (kit.FirstOrDefault(row => row.Family == "powder") is { } powder) used.UnionWith(Powders.Where(id => id >= powder.ItemId));
		used.UnionWith(NaturalBoostItems.All.Where(boost => boost.FromLevel <= level && level <= boost.ToLevel).Select(boost => boost.ItemId));
		return used;
	}

	/// <param name="decisions">What the class's inventory rules decide for each stack (<see cref="NaturalInventoryPlan.Decisions"/>),
	/// asked with the needs of the leg being played and with no protection of a leg that is over.</param>
	/// <param name="needed">Item ids the leg being played names: never disposed of.</param>
	/// <param name="questItems">The quests that refer to each item (<see cref="NaturalQuestItemIndex"/>).</param>
	/// <param name="workedQuests">The quests in the journal that are being worked: started or ready to hand in, and not
	/// one the class line leaves out.</param>
	public static NaturalBagPlan Plan(IReadOnlyCollection<BotInventoryItem> inventory, int level, NaturalHelpItemRules help,
		IEnumerable<NaturalInventoryDecision> decisions, IReadOnlySet<int> needed, Func<int, NaturalShardInfo?> shard,
		IReadOnlyDictionary<int, int[]> questItems, IReadOnlySet<int> workedQuests)
	{
		if (level <= NaturalHelpItemAllowlist.StarterMaxLevel) return new([], []);
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
		HashSet<int> used = Used(help, level);
		// A shard with less damage than the best one the character can use. One with more is for a later level.
		int bestBoost = NaturalPowerShardPolicy.Decide(inventory, level, modeOn: true, shard).BestItemId is int best ? shard(best)!.Boost : 0;
		Dictionary<int, NaturalInventoryDecision> decided = decisions.GroupBy(decision => decision.ObjectId).ToDictionary(group => group.Key, group => group.First());
		// A quest's item is a leftover once no quest that names it is worked. One no quest's data names came from a quest's
		// script: it is kept while any quest is worked.
		bool Leftover(int itemId) => questItems.GetValueOrDefault(itemId) is { Length: > 0 } quests
			? !quests.Any(workedQuests.Contains) : workedQuests.Count == 0;
		var discards = new List<NaturalBagDiscard>();
		var kept = new List<NaturalBagDiscard>();
		foreach (BotInventoryItem item in inventory.Where(item => item.Count > 0 && item.Details.EquippedSlot.GetValueOrDefault() == 0 &&
			item.ItemId != BotWorldModel.KinahItemId).OrderBy(item => item.ObjectId))
		{
			NaturalInventoryDecision? decision = decided.GetValueOrDefault(item.ObjectId);
			(bool goes, string reason) = needed.Contains(item.ItemId) ? (false, "leg-needs-it")
				// NL-3: an item a worked quest's data names stays whatever the inventory rules call it: Q2731's documents
				// (182205602, its work item) are not sellable and were thrown out at a restock before Glati took them.
				: questItems.GetValueOrDefault(item.ItemId) is { Length: > 0 } named && named.Any(workedQuests.Contains) ? (false, "quest-worked")
				: shard(item.ItemId) is { } info ? info.Boost < bestBoost ? (true, "lesser-power-shard") : (false, "power-shard")
				: reasons.TryGetValue(item.ItemId, out string? superseded) ? (true, superseded)
				: decision == null ? (false, "undecided")
				// The class rules know the potions of the Priest line's tables; another class drinks them too.
				: decision.Action == "sell" ? decision.Reason == "unneeded-or-unusable" && used.Contains(item.ItemId) ? (false, "used") : (true, decision.Reason)
				: decision.Reason == "quest-protected" ? Leftover(item.ItemId) ? (true, "quest-leftover") : (false, "quest-worked")
				: decision.Reason is "not-sellable" or "combat-supply" ? used.Contains(item.ItemId) ? (false, "used") : (true, "unused")
				: (false, decision.Reason);
			(goes ? discards : kept).Add(new(item.ObjectId, item.ItemId, item.Count, reason));
		}
		return new(discards, kept);
	}
}

/// <summary>A consumable a bot uses rather than throws away: when it is used is the rule's, not a kit row's.</summary>
/// <param name="SkillId">The item's use skill, by which its effect is seen.</param>
public sealed record NaturalBoostItem(int ItemId, string Family, int SkillId, int FromLevel, int ToLevel);

/// <summary>
/// MR-f, "use it or get rid of it": the two things in every cube that are worth using. The Tea of Repose refills the
/// Energy of Repose, from which a kill pays extra experience (restrict row 10); it is drunk when that energy is all but
/// gone. The Lodas Amulet III adds a fifth to hunting experience for two hours (to level 49, one use in four hours);
/// one is used before a pull when none is at work. Pure: the caller supplies what the client observes.
/// </summary>
public static class NaturalBoostItems
{
	public const string ReposeTea = "repose-tea", ExperienceAmulet = "xp-amulet", Diagnostic = "boost-item-used";

	/// <summary>The tea is drunk when no more than this share of the Energy of Repose is left, in percent.</summary>
	public const int ReposeLeftPercent = 5;

	public static readonly NaturalBoostItem[] All =
	[
		new(162001057, ReposeTea, 10491, 10, 65),
		new(169620005, ExperienceAmulet, 10249, 1, 49),
	];

	/// <param name="ready">The item's use delay has run out (the client's timing).</param>
	public static NaturalBoostItem? Decide(int level, IReadOnlyDictionary<int, long> owned, long repose, long maxRepose,
		IReadOnlyCollection<int> activeSkillIds, bool beforePull, Func<NaturalBoostItem, bool> ready)
	{
		foreach (NaturalBoostItem boost in All.Where(boost => boost.FromLevel <= level && level <= boost.ToLevel && owned.GetValueOrDefault(boost.ItemId) > 0))
		{
			bool wanted = boost.Family == ReposeTea ? maxRepose > 0 && repose * 100 <= maxRepose * ReposeLeftPercent
				: beforePull && !activeSkillIds.Contains(boost.SkillId);
			if (wanted && ready(boost)) return boost;
		}
		return null;
	}
}
