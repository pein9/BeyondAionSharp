using Aion.Bots.Scenarios.Classes;
using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>What the client knows about an equippable item from its tooltip: the slots it fits (bit mask),
/// the level this character needs to wear it (-1 when its class cannot), its item level, and whether it
/// fits this character's race.</summary>
/// <param name="Group">The item group (<c>STAFF</c>, <c>MACE</c>, <c>SHIELD</c>, <c>CH_TORSO</c>).</param>
/// <param name="MagicBoost">A weapon's magic boost; a class that casts ranks its weapons by it.</param>
/// <param name="MinimumDamage">CP-29: a weapon's damage and its flat physical-attack bonus, for a table rule's physical stat.</param>
/// <param name="OneHandWeapon">CP-68: a weapon that takes one hand, so the other may hold a shield or a second weapon.</param>
public sealed record NaturalGearInfo(long ValidSlots, int RequiredLevel, int ItemLevel, bool RaceAllowed,
	string? Group = null, int MagicBoost = 0, int MinimumDamage = 0, int MaximumDamage = 0, int PhysicalAttack = 0,
	bool OneHandWeapon = false)
{
	public bool GoesInAHand => (ValidSlots & (NaturalGearPolicy.MainHand | NaturalGearPolicy.SubHand)) != 0;
}

/// <summary>One equip request: put <see cref="ObjectId"/> into the single slot <see cref="Slot"/>.</summary>
public sealed record NaturalGearUpgrade(int ObjectId, int ItemId, long Slot, int ItemLevel, int? ReplacesItemLevel);

/// <summary>
/// Wear the best gear in the bag, the way the recorded human Priest did at Nalto: a quest reward that sits
/// unused in the cube (a level-8 pair of shoes, a level-7 ring, a level-3 mace) goes on as soon as the
/// character can wear it. For every slot, the bag item the class's gear rules score highest, that this class and
/// race may wear at this level, replaces what is worn there when it scores higher (or fills an empty slot). The
/// server still checks everything (Java Equipment.equipItem); an item it refuses is simply not worn.
/// <para>
/// The hand holds the best weapon of the class's own groups and nothing goes in the off hand. The staff rule (the
/// operator, 2026-10-06, AX-Q1: "always choose a staff, and equip whatever staff we have that has more magic
/// boost. This is the rule ALL THE TIME") is the Cleric's weapon order, staff before mace (CP-29a): once the
/// character owns a staff it can wear, the hands hold a staff, the one with the most magic boost.
/// </para>
/// <para>
/// CP-68: a class's rules may name an off-hand mode (<see cref="NaturalOffHand"/>). Beside a one-hand weapon in the main
/// hand, the off hand then holds the best shield, or the best other one-hand weapon of the class's groups. The second
/// weapon is asked for only when a dual-wield skill is observed: without it the server moves a one-hand weapon asked
/// into the off hand to the main hand, with no message (Java Equipment.equipItem). No class has a mode turned on.
/// </para>
/// </summary>
public static class NaturalGearPolicy
{
	public const long MainHand = 1, SubHand = 2;

	/// <summary>CP-68: the auto-learned passive skills with the dual-wield effect (skill_templates.xml, effect wpndual;
	/// the Scout learns 55 at level 5). The stigma skills with the effect are left out: no natural line takes a stigma.</summary>
	public static readonly IReadOnlySet<int> DualWieldSkillIds = new HashSet<int> { 55, 70, 76, 82, 143, 144, 171, 207 };

	/// <param name="offHandSlots">Slot bits that can never be requested directly (the off-hand swap set).</param>
	/// <param name="refused">Items the server already refused; never asked again.</param>
	/// <param name="rules">CP-22: the class's gear rules; the Priest's when not given.</param>
	/// <param name="dualWield">CP-68: a skill of <see cref="DualWieldSkillIds"/> is in the observed skill list.</param>
	public static IReadOnlyList<NaturalGearUpgrade> SelectUpgrades(IEnumerable<BotInventoryItem> inventory, int level,
		Func<int, NaturalGearInfo?> describe, long offHandSlots, IReadOnlySet<int>? refused = null, NaturalGearRules? rules = null,
		bool dualWield = false)
	{
		ArgumentNullException.ThrowIfNull(inventory);
		ArgumentNullException.ThrowIfNull(describe);
		NaturalGearRules gearRules = rules ?? NaturalGearRules.Priest;
		// CP-29: the rules rank a slot by their own score, never ask the server for an item the class has no mastery for,
		// and hold only the class's own weapon groups, with nothing in the off hand.
		long Rank(NaturalGearInfo info) => gearRules.UpgradeScore(info);
		BotInventoryItem[] items = inventory.ToArray();
		// What is worn now, by single slot bit.
		var worn = new Dictionary<long, (BotInventoryItem Item, int Level, long Rank)>();
		foreach (BotInventoryItem item in items)
		{
			long mask = item.Details.EquippedSlot ?? 0;
			if (mask <= 0 || describe(item.ItemId) is not NaturalGearInfo info) continue;
			foreach (long bit in Bits(mask))
				worn[bit] = (item, info.ItemLevel, Rank(info));
		}
		var upgrades = new List<NaturalGearUpgrade>();
		var candidates = items
			.Where(item => (item.Details.EquippedSlot ?? 0) <= 0 && refused?.Contains(item.ObjectId) != true)
			.Select(item => (Item: item, Info: describe(item.ItemId)))
			.Where(c => c.Info is { ValidSlots: > 0, RaceAllowed: true } info && info.RequiredLevel > 0 &&
				info.RequiredLevel <= level && gearRules.Wears(info.Group))
			.OrderByDescending(c => Rank(c.Info!)).ThenBy(c => c.Item.ObjectId).ToArray();
		NaturalGearInfo? held = worn.TryGetValue(MainHand, out var hand) ? describe(hand.Item.ItemId) : null;
		// The best weapon of the class's groups, by the rule's score; the held one stays unless a bag weapon beats it.
		var bestWeapon = candidates.FirstOrDefault(c => c.Info!.GoesInAHand && gearRules.WeaponGroups.Contains(c.Info.Group ?? ""));
		bool holdsItsWeapon = held != null && gearRules.WeaponGroups.Contains(held.Group ?? "");
		if (bestWeapon.Item != null && (!holdsItsWeapon || Rank(bestWeapon.Info!) > Rank(held!)))
		{
			upgrades.Add(new NaturalGearUpgrade(bestWeapon.Item.ObjectId, bestWeapon.Item.ItemId, MainHand, bestWeapon.Info!.ItemLevel,
				worn.TryGetValue(MainHand, out var replaced) ? replaced.Level : null));
			worn[MainHand] = (bestWeapon.Item, bestWeapon.Info.ItemLevel, Rank(bestWeapon.Info));
		}
		if (gearRules.OffHand != NaturalOffHand.None && worn.TryGetValue(MainHand, out var main) &&
			describe(main.Item.ItemId) is { OneHandWeapon: true })
		{
			// CP-68: the off hand beside a one-hand weapon, by the class's mode. A two-hand weapon fills both slots, so
			// what the off hand shows of the weapon that was held before the pick above is no longer there.
			bool Fits(NaturalGearInfo info) => gearRules.OffHand == NaturalOffHand.Shield ? info.Group == "SHIELD"
				: dualWield && info.OneHandWeapon && gearRules.WeaponGroups.Contains(info.Group ?? "");
			(BotInventoryItem Item, int Level, long Rank)? offHand = worn.TryGetValue(SubHand, out var sub) &&
				sub.Item.ObjectId != hand.Item?.ObjectId && describe(sub.Item.ItemId) is { } subInfo && Fits(subInfo) ? sub : null;
			var bestOffHand = candidates.FirstOrDefault(c => c.Item.ObjectId != main.Item.ObjectId && Fits(c.Info!));
			if (bestOffHand.Item != null && (offHand == null || Rank(bestOffHand.Info!) > offHand.Value.Rank))
			{
				upgrades.Add(new NaturalGearUpgrade(bestOffHand.Item.ObjectId, bestOffHand.Item.ItemId, SubHand, bestOffHand.Info!.ItemLevel,
					offHand?.Level));
				worn[SubHand] = (bestOffHand.Item, bestOffHand.Info.ItemLevel, Rank(bestOffHand.Info));
			}
		}
		foreach (var (item, info) in candidates)
		{
			if (info!.GoesInAHand) continue;
			long slots = info.ValidSlots & ~offHandSlots;
			long? best = null;
			long bestRank = long.MaxValue;
			int bestLevel = 0;
			foreach (long bit in Bits(slots))
			{
				(int wornLevel, long wornRank) = worn.TryGetValue(bit, out var current) ? (current.Level, current.Rank) : (0, 0L);
				if (wornRank < bestRank) { bestRank = wornRank; bestLevel = wornLevel; best = bit; }
			}
			if (best is not long slot || Rank(info) <= bestRank) continue;
			upgrades.Add(new NaturalGearUpgrade(item.ObjectId, item.ItemId, slot, info.ItemLevel,
				worn.ContainsKey(slot) ? bestLevel : null));
			worn[slot] = (item, info.ItemLevel, Rank(info));
		}
		return upgrades;
	}

	private static IEnumerable<long> Bits(long mask)
	{
		for (int bit = 0; bit < 63; bit++)
			if ((mask & (1L << bit)) != 0) yield return 1L << bit;
	}
}
