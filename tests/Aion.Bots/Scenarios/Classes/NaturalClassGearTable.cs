using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Items.Enums;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>How a table-ruled class ranks a weapon.</summary>
public enum NaturalWeaponStat
{
	/// <summary>Per swing: the mean of minimum and maximum damage plus the item's flat physical-attack bonus (CP-Q7).</summary>
	Physical,
	/// <summary>Magic boost, then maximum damage.</summary>
	Magical,
}

/// <summary>CP-68: what a class holds in its off hand beside a one-hand weapon.</summary>
public enum NaturalOffHand
{
	/// <summary>Nothing. Every class's table today.</summary>
	None,
	/// <summary>The best shield it owns; the class needs the shield mastery.</summary>
	Shield,
	/// <summary>The best other one-hand weapon of its groups, once a dual-wield skill is observed.</summary>
	SecondWeapon,
}

/// <summary>
/// CP-29: the hand-written part of a class's gear rule (docs/natural-class-profiles.md): the weapon groups it holds, best
/// first, how it ranks a weapon within a group, the armor types it prefers, best first, the consumables it takes at a
/// reward, best first, and the consumables it keeps. What the class can wear at all is not written here: it is read from
/// the class's mastery rows in the class-line contract, because the server refuses an item of a group the character has
/// no mastery skill for (Java Equipment.equipItem, checkAvailableEquipSkills). CP-29a: every class has a table, the
/// Priest and the Cleric included, and a rule that changes at Ascension is the second class's own table.
/// </summary>
/// <param name="WeaponGroups">The item groups the class holds in its hand, best first: a weapon of an earlier group
/// outranks every weapon of a later one, whatever its numbers. It may be able to wear more.</param>
/// <param name="ArmorTypes">The mastery names of the armor it wears (<c>CHAIN</c>, <c>LEATHER</c>, <c>ROBE</c>,
/// <c>CLOTHES</c>, <c>PLATE</c>), best first. Item level ranks a piece first, as the recorded human Priest wore level-8
/// robe leggings over level-1 leather; of two pieces of one item level, the earlier type wins (CP-Q24).</param>
/// <param name="Supplies">Consumables the class keeps beside the life potions and its help kit; none when not given.</param>
/// <param name="OffHand">CP-68: the off-hand mode; nothing when not given. Turning one on for a class is the operator's
/// decision and re-records that class's scope.</param>
public sealed record NaturalClassGearTable(PlayerClass Class, IReadOnlyList<string> WeaponGroups, NaturalWeaponStat WeaponStat,
	IReadOnlyList<string> ArmorTypes, IReadOnlyList<int> ConsumableOrder, IReadOnlyList<int>? Supplies = null,
	NaturalOffHand OffHand = NaturalOffHand.None)
{
	private static readonly string[] ArmorParts = ["_TORSO", "_GLOVE", "_SHOULDER", "_PANTS", "_SHOES", "_HEADS"];

	/// <summary>At a reward that offers consumables: a life elixir, then a mana elixir, then a power shard.</summary>
	public static readonly IReadOnlyList<int> DefaultConsumableOrder = [NaturalIshalgenPotionPolicy.VendorLifeElixirId, 162000057, 169000003];

	/// <summary>The life potions and elixirs every class keeps. No class keeps a bandage (CP-Q11).</summary>
	public static readonly IReadOnlyList<int> LifePotionIds =
	[
		NaturalIshalgenPotionPolicy.StarterLifePotionId, NaturalIshalgenPotionPolicy.VendorLifeElixirId,
		NaturalIshalgenPotionPolicy.LesserLifeElixirId, NaturalIshalgenPotionPolicy.LesserLifePotionId,
		NaturalIshalgenPotionPolicy.LifePotionId, NaturalIshalgenPotionPolicy.MajorLifePotionId,
	];

	/// <summary>What a class of the Priest line keeps from Ascension on: every mana potion combat drinks, what is left of
	/// the level 1-9 kit, and every help scroll and food the help-item policy knows (NA-21, OD-13).</summary>
	public static readonly IReadOnlyList<int> PriestLineSupplies =
	[
		.. NaturalIshalgenPotionPolicy.ManaPotionIds,
		.. NaturalHelpItemAllowlist.Starter.Select(supply => supply.ItemId),
		.. NaturalHelpItemPolicy.All.Select(help => help.ItemId),
	];

	// CP-29a, the operator's gear rules of 2026-10-07 for the Priest types: leather before Ascension, chain after; the
	// staff with the most magic boost after Ascension (the staff rule, AX-Q1), which is the weapon order here. The Priest
	// keeps its mace by magic boost.
	public static NaturalClassGearTable Priest { get; } = new(PlayerClass.PRIEST, ["MACE"], NaturalWeaponStat.Magical,
		["LEATHER", "ROBE", "CLOTHES"], DefaultConsumableOrder, NaturalIshalgenPotionPolicy.ManaPotionIds);
	public static NaturalClassGearTable Cleric { get; } = new(PlayerClass.CLERIC, ["STAFF", "MACE"], NaturalWeaponStat.Magical,
		["CHAIN", "LEATHER", "ROBE", "CLOTHES"], DefaultConsumableOrder, PriestLineSupplies);
	public static NaturalClassGearTable Chanter { get; } = new(PlayerClass.CHANTER, ["STAFF", "MACE"], NaturalWeaponStat.Magical,
		["CHAIN", "LEATHER", "ROBE", "CLOTHES"], DefaultConsumableOrder, PriestLineSupplies);

	// The defaults of CP-Q10 for the five new starters. Each holds one weapon and nothing in the off hand, but the Scout.
	public static NaturalClassGearTable Warrior { get; } = new(PlayerClass.WARRIOR, ["SWORD", "MACE"], NaturalWeaponStat.Physical,
		["CHAIN", "LEATHER", "ROBE", "CLOTHES"], DefaultConsumableOrder);
	// NR-04 (the operator, 2026-10-08: "Yes two daggers, they don't have to match"): a second dagger in the off hand once
	// the dual-wield skill is observed, which the Scout learns at level 5.
	public static NaturalClassGearTable Scout { get; } = new(PlayerClass.SCOUT, ["DAGGER"], NaturalWeaponStat.Physical,
		["LEATHER", "ROBE", "CLOTHES"], DefaultConsumableOrder, OffHand: NaturalOffHand.SecondWeapon);
	// CP-46: the Mage casts from mana, so it keeps the mana potions too.
	public static NaturalClassGearTable Mage { get; } = new(PlayerClass.MAGE, ["SPELLBOOK"], NaturalWeaponStat.Magical,
		["ROBE", "CLOTHES"], DefaultConsumableOrder, NaturalIshalgenPotionPolicy.ManaPotionIds);
	public static NaturalClassGearTable Engineer { get; } = new(PlayerClass.ENGINEER, ["GUN"], NaturalWeaponStat.Magical,
		["LEATHER", "ROBE", "CLOTHES"], DefaultConsumableOrder);
	// CP-47: the Artist casts from mana, so it keeps the mana potions too.
	public static NaturalClassGearTable Artist { get; } = new(PlayerClass.ARTIST, ["HARP"], NaturalWeaponStat.Magical,
		["ROBE", "CLOTHES"], DefaultConsumableOrder, NaturalIshalgenPotionPolicy.ManaPotionIds);

	/// <summary>The default table of each new starter.</summary>
	public static IReadOnlyList<NaturalClassGearTable> Starters { get; } = [Warrior, Scout, Mage, Engineer, Artist];

	/// <summary>The physical stat of CP-Q7, doubled so that it stays whole: minimum plus maximum damage is twice the
	/// mean of a swing, and the flat physical-attack bonus is counted twice with it.</summary>
	public static int PhysicalStat(int minimumDamage, int maximumDamage, int physicalAttack) => minimumDamage + maximumDamage + 2 * physicalAttack;

	/// <summary>
	/// The one score of the rule, for the equipment check, keep or sell, and the reward choice. Higher is better among
	/// items of one slot. A weapon: the group's place in the class's order, then the class's stat, then item level. Armor:
	/// item level, then the type's place in the class's order. Anything else that is worn (an accessory): item level.
	/// </summary>
	public long Score(string group, int itemLevel, int minimumDamage, int maximumDamage, int magicBoost, int physicalAttack)
	{
		for (int index = 0; index < WeaponGroups.Count; index++)
			if (WeaponGroups[index] == group)
				return (WeaponGroups.Count - index) * 1_000_000_000_000L + (WeaponStat == NaturalWeaponStat.Physical
					? PhysicalStat(minimumDamage, maximumDamage, physicalAttack) * 1_000_000L + itemLevel * 1_000L + maximumDamage
					: magicBoost * 1_000_000L + maximumDamage * 1_000L + minimumDamage);
		int place = ArmorPlace(group);
		return itemLevel * 1_000_000L + (place < 0 ? 0 : ArmorTypes.Count - place);
	}

	/// <summary>Where an armor group's type stands in the class's order; -1 for a group that is not armor of one of its types.</summary>
	private int ArmorPlace(string group)
	{
		if (!ArmorParts.Any(part => group.EndsWith(part, StringComparison.Ordinal)) || !Enum.TryParse(group, out ItemGroup parsed)) return -1;
		string type = parsed.GetItemSubType().ToString();
		for (int index = 0; index < ArmorTypes.Count; index++)
			if (ArmorTypes[index] == type) return index;
		return -1;
	}

	/// <summary>
	/// The class's gear rules. The class's mastery rows say what it can wear: a starter's own rows, and for a
	/// second class its starter's rows with its own. A table that names a weapon group or an armor type the class has no
	/// mastery for is refused by name.
	/// </summary>
	/// <param name="kit">The help kit whose items the class keeps; the level 1-9 kit when not given.</param>
	public NaturalGearRules Rules(NaturalClassLineContract contract, IEnumerable<NaturalHelpSupply>? kit = null)
	{
		ArgumentNullException.ThrowIfNull(contract);
		bool starter = Class.IsStartingClass();
		NaturalStarterClass first = contract.Starter(Class.GetStartingClass());
		NaturalClassMastery[] masteries = starter ? first.Masteries : [.. first.Masteries, .. contract.Second(Class).Masteries];
		HashSet<string> unlocks = masteries.Select(mastery => mastery.Unlocks).ToHashSet();
		foreach (string named in WeaponGroups.Concat(ArmorTypes))
			if (!unlocks.Contains(named))
				throw new InvalidDataException($"The {Class} gear table names {named}, which the class has no mastery for.");
		if (WeaponGroups.Count == 0 || ArmorTypes.Count == 0 || WeaponGroups.Distinct().Count() != WeaponGroups.Count ||
			ArmorTypes.Distinct().Count() != ArmorTypes.Count)
			throw new InvalidDataException($"The {Class} gear table needs weapon groups and armor types, each named once.");
		if (OffHand == NaturalOffHand.Shield && !unlocks.Contains("SHIELD"))
			throw new InvalidDataException($"The {Class} gear table holds a shield, which the class has no mastery for.");
		string[] armorGroups = Enum.GetNames<ItemGroup>().Where(group => ArmorPlace(group) >= 0).ToArray();
		NaturalClassGearTable table = this;
		// A second class's mastery takes the place of the starter's it names.
		HashSet<int> replaced = masteries.Where(mastery => mastery.ReplacesSkillId != null).Select(mastery => mastery.ReplacesSkillId!.Value).ToHashSet();
		return new NaturalGearRules
		{
			Class = Class,
			// NR-03: a class that holds a shield treats shields as gear: the best one is kept and worn, the rest are spare.
			GearGroups = WeaponGroups.Concat(armorGroups).Concat(OffHand == NaturalOffHand.Shield ? ["SHIELD"] : []).ToHashSet(),
			WeaponGroups = WeaponGroups.ToHashSet(),
			// A starter cannot pass level 9, so gear for a later level is not its gear.
			HighestRequiredLevel = starter ? 9 : null,
			Score = item => table.Score(item.Group, item.ItemLevel, item.MinimumDamage, item.MaximumDamage, item.MagicBoost, item.PhysicalAttack),
			UpgradeScore = info => table.Score(info.Group ?? "", info.ItemLevel, info.MinimumDamage, info.MaximumDamage, info.MagicBoost, info.PhysicalAttack),
			EquipReason = $"best-usable-{Class.ToString().ToLowerInvariant()}-upgrade",
			Supplies = LifePotionIds.Concat((kit ?? NaturalHelpItemAllowlist.Starter).Select(supply => supply.ItemId))
				.Concat(Supplies ?? []).ToHashSet(),
			MasteryUnlocks = unlocks,
			OffHand = OffHand,
			ConsumableOrder = ConsumableOrder,
			ExpectedSkillIds = masteries.Select(mastery => mastery.SkillId).Where(id => !replaced.Contains(id)).Distinct().Order().ToArray(),
		};
	}
}
