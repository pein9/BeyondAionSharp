using Aion.Bots.Scenarios.Classes;
using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>What the client knows about an equippable item from its tooltip: the slots it fits (bit mask),
/// the level this character needs to wear it (-1 when its class cannot), its item level, and whether it
/// fits this character's race.</summary>
/// <param name="Group">The item group for an item that goes in a hand (<c>STAFF</c>, <c>MACE</c>, <c>SHIELD</c>).</param>
/// <param name="MagicBoost">A weapon's magic boost; the staff rule ranks staffs by it.</param>
/// <param name="MinimumDamage">CP-29: a weapon's damage and its flat physical-attack bonus, for a table rule's physical stat.</param>
public sealed record NaturalGearInfo(long ValidSlots, int RequiredLevel, int ItemLevel, bool RaceAllowed,
	string? Group = null, int MagicBoost = 0, int MinimumDamage = 0, int MaximumDamage = 0, int PhysicalAttack = 0)
{
	public bool IsStaff => Group == "STAFF";
	public bool GoesInAHand => (ValidSlots & (NaturalGearPolicy.MainHand | NaturalGearPolicy.SubHand)) != 0;
}

/// <summary>One equip request: put <see cref="ObjectId"/> into the single slot <see cref="Slot"/>.</summary>
public sealed record NaturalGearUpgrade(int ObjectId, int ItemId, long Slot, int ItemLevel, int? ReplacesItemLevel);

/// <summary>
/// Wear the best gear in the bag, the way the recorded human Priest did at Nalto: a quest reward that sits
/// unused in the cube (a level-8 pair of shoes, a level-7 ring, a level-3 mace) goes on as soon as the
/// character can wear it. For every slot, the highest item-level bag item that fits it, that this class and
/// race may wear at this level, replaces what is worn there when it is higher (or fills an empty slot). The
/// server still checks everything (Java Equipment.equipItem); an item it refuses is simply not worn.
/// <para>
/// The staff rule (the operator, 2026-10-06, AX-Q1: "always choose a staff, and equip whatever staff we have
/// that has more magic boost. This is the rule ALL THE TIME"). Once the character owns a staff it can wear,
/// the hands hold a staff, and the one with the most magic boost: item level does not decide, and no mace or
/// shield goes on. Before it owns one (the Priest in Ishalgen), the hands follow the rule above.
/// </para>
/// </summary>
public static class NaturalGearPolicy
{
	public const long MainHand = 1, SubHand = 2;

	/// <param name="offHandSlots">Slot bits that can never be requested directly (the off-hand swap set).</param>
	/// <param name="refused">Items the server already refused; never asked again.</param>
	/// <param name="rules">CP-22: the class's gear rules, for the hand rule; the Priest line's (the staff) when not given.</param>
	public static IReadOnlyList<NaturalGearUpgrade> SelectUpgrades(IEnumerable<BotInventoryItem> inventory, int level,
		Func<int, NaturalGearInfo?> describe, long offHandSlots, IReadOnlySet<int>? refused = null, NaturalGearRules? rules = null)
	{
		ArgumentNullException.ThrowIfNull(inventory);
		ArgumentNullException.ThrowIfNull(describe);
		NaturalGearRules gearRules = rules ?? NaturalGearRules.Priest;
		string? handGroup = gearRules.HandRuleGroup;
		bool HandItem(NaturalGearInfo? info) => handGroup != null && info?.Group == handGroup;
		// CP-29: a rule in table form ranks a slot by its own score, never asks the server for an item the class has no
		// mastery for, and holds only its own weapon groups, with nothing in the off hand. Every other rule ranks by item
		// level and asks, as before.
		bool table = gearRules.IsTable;
		long Rank(NaturalGearInfo info) => gearRules.UpgradeScore is { } score ? score(info) : info.ItemLevel;
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
				info.RequiredLevel <= level && (!table || gearRules.Wears(info.Group)))
			.OrderByDescending(c => Rank(c.Info!)).ThenBy(c => c.Item.ObjectId).ToArray();
		NaturalGearInfo? held = worn.TryGetValue(MainHand, out var hand) ? describe(hand.Item.ItemId) : null;
		bool staffRule = HandItem(held) || candidates.Any(c => HandItem(c.Info));
		if (staffRule)
		{
			// The staff with the most magic boost; a staff already held stays unless a bag staff has more.
			var best = candidates.Where(c => HandItem(c.Info))
				.OrderByDescending(c => c.Info!.MagicBoost).ThenByDescending(c => c.Info!.ItemLevel).ThenBy(c => c.Item.ObjectId)
				.FirstOrDefault();
			if (best.Item != null && (!HandItem(held) || best.Info!.MagicBoost > held!.MagicBoost))
			{
				upgrades.Add(new NaturalGearUpgrade(best.Item.ObjectId, best.Item.ItemId, MainHand, best.Info!.ItemLevel,
					worn.TryGetValue(MainHand, out var replaced) ? replaced.Level : null));
				worn[MainHand] = worn[SubHand] = (best.Item, best.Info.ItemLevel, Rank(best.Info));
			}
		}
		if (table)
		{
			// The best weapon of the class's groups, by the rule's score; the held one stays unless a bag weapon beats it.
			var best = candidates.FirstOrDefault(c => c.Info!.GoesInAHand && gearRules.WeaponGroups.Contains(c.Info.Group ?? ""));
			bool holdsItsWeapon = held != null && gearRules.WeaponGroups.Contains(held.Group ?? "");
			if (best.Item != null && (!holdsItsWeapon || Rank(best.Info!) > Rank(held!)))
			{
				upgrades.Add(new NaturalGearUpgrade(best.Item.ObjectId, best.Item.ItemId, MainHand, best.Info!.ItemLevel,
					worn.TryGetValue(MainHand, out var replaced) ? replaced.Level : null));
				worn[MainHand] = (best.Item, best.Info.ItemLevel, Rank(best.Info));
			}
		}
		foreach (var (item, info) in candidates)
		{
			if ((staffRule || table) && info!.GoesInAHand) continue;
			// A one-handed weapon always lands in the main hand without dual wield (Java moves it there).
			long slots = info!.ValidSlots & ~offHandSlots;
			if ((slots & MainHand) != 0) slots = MainHand;
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

	/// <summary>The staff rule at a reward choice: of the items offered, the staff this character can wear with the most
	/// magic boost, or null when no usable staff is offered and the leg's own choice stands.</summary>
	public static int? ChooseStaffReward(IEnumerable<int> offered, int level, Func<int, NaturalGearInfo?> describe) =>
		offered.Select(id => (Id: id, Info: describe(id)))
			.Where(choice => choice.Info is { IsStaff: true, RaceAllowed: true } info && info.RequiredLevel > 0 && info.RequiredLevel <= level)
			.OrderByDescending(choice => choice.Info!.MagicBoost).ThenBy(choice => choice.Id)
			.Select(choice => (int?)choice.Id).FirstOrDefault();

	private static IEnumerable<long> Bits(long mask)
	{
		for (int bit = 0; bit < 63; bit++)
			if ((mask & (1L << bit)) != 0) yield return 1L << bit;
	}
}
