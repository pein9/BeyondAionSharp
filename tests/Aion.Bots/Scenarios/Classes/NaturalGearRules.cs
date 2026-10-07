using Aion.Bots.World;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// CP-22: what a class treats as gear, how it ranks it and what it keeps (docs/natural-class-profiles.md). The inventory
/// policy's one decision path, the item's own gear members and the equipment check's hand rule all read these rules;
/// nothing else tests for a class. The Priest's and the Cleric's rules are the two rule sets the policy held inline.
/// </summary>
public sealed class NaturalGearRules
{
	/// <summary>The class whose column of an item's restrict row gives the level it may wear the item at.</summary>
	public required PlayerClass Class { get; init; }

	/// <summary>The item groups the class treats as gear.</summary>
	public required IReadOnlySet<string> GearGroups { get; init; }

	/// <summary>Gear groups that go in the weapon slot.</summary>
	public required IReadOnlySet<string> WeaponGroups { get; init; }

	/// <summary>Gear groups that go in the off hand.</summary>
	public IReadOnlySet<string> OffHandGroups { get; init; } = new HashSet<string>();

	/// <summary>Gear above this required level is not considered at all (the Ishalgen Priest stops at 9); null for no cap.</summary>
	public int? HighestRequiredLevel { get; init; }

	/// <summary>Higher is better, among items of one slot.</summary>
	public required Func<NaturalItem, long> Score { get; init; }

	/// <summary>The reason an equip decision carries.</summary>
	public required string EquipReason { get; init; }

	/// <summary>Consumables the class keeps and never sells.</summary>
	public required IReadOnlySet<int> Supplies { get; init; }

	/// <summary>The rules from Ascension on: a leg's protected items and retained staff and an open quest's needs are
	/// honored, the bridge's supplies are kept, and an accessory is either kept for a later level or sold as surplus.</summary>
	public bool AfterAscension { get; init; }

	/// <summary>The hand rule of the equipment check: once the character owns a wearable item of this group, the hands
	/// hold one, the one with the most magic boost (the operator's staff rule, AX-Q1). Null for no such rule.</summary>
	public string? HandRuleGroup { get; init; }

	/// <summary>Skills every character of the class has from level 1, beside its catalog's auto-learned ones.</summary>
	public IReadOnlyList<int> ExpectedSkillIds { get; init; } = [];

	/// <summary>The catalog whose skills must be observed as learned by their level.</summary>
	public NaturalPriestSkill[] SkillCatalog { get; init; } = [];

	public bool IsGear(NaturalItem item) => GearGroups.Contains(item.Group);

	/// <summary>The slot name gear of one kind competes for; null for what is not gear.</summary>
	public string? Slot(NaturalItem item) => !IsGear(item) ? null
		: WeaponGroups.Contains(item.Group) ? "WEAPON"
		: OffHandGroups.Contains(item.Group) ? "SUB"
		: item.Group == "HEAD" || item.Group.EndsWith("_HEADS", StringComparison.Ordinal) ? "HEAD"
		: item.IsAccessory ? item.Group
		: item.Group[(item.Group.IndexOf('_') + 1)..];

	/// <summary>The class may wear the item at this level. Every natural line is Asmodian.</summary>
	public bool Usable(NaturalItem item, int level)
	{
		int required = item.RequiredLevelFor(Class), maximum = item.MaximumLevelFor(Class);
		return IsGear(item) && item.Quality > 0 && required > 0 && (HighestRequiredLevel is not int cap || required <= cap) &&
			required <= level && (maximum == 0 || level <= maximum) && item.Race is ("PC_ALL" or "ASMODIANS");
	}

	/// <summary>The class's level 1 skills and every catalog skill up to this level are in the observed skill list.</summary>
	public bool AutoLearnedSkillsObserved(int level, IReadOnlyDictionary<int, BotSkill> learned) =>
		ExpectedSkillIds.Concat(SkillCatalog.Where(skill => skill.MinimumLevel <= level).Select(skill => (int)skill.Id)).All(learned.ContainsKey);

	private static readonly string[] PriestGearGroups =
	[
		"MACE", "RB_TORSO", "RB_GLOVE", "RB_SHOULDER", "RB_PANTS", "RB_SHOES",
		"CL_TORSO", "CL_GLOVE", "CL_SHOULDER", "CL_PANTS", "CL_SHOES", "CL_HEADS",
		"LT_TORSO", "LT_GLOVE", "LT_SHOULDER", "LT_PANTS", "LT_SHOES", "LT_HEADS",
	];

	// The starter HP and MP potions, the bought timed healing, and (CP-06) the supplied items of the level 1-9 kit. The
	// event scrolls of its manifest need no entry: they cannot be sold. The starter's bandages are not a supply: no class
	// uses one (CP-Q11).
	private static readonly int[] PriestSupplies = [162000002, 162000007, 162000052,
		.. NaturalHelpItemAllowlist.Starter.Select(supply => supply.ItemId)];

	/// <summary>The Ishalgen Priest: a mace, robes, cloth and leather, up to required level 9, ranked by required level,
	/// then quality, then the weapon's magic boost and damage.</summary>
	public static NaturalGearRules Priest { get; } = new()
	{
		Class = PlayerClass.PRIEST,
		GearGroups = PriestGearGroups.ToHashSet(),
		WeaponGroups = new HashSet<string> { "MACE" },
		HighestRequiredLevel = 9,
		Score = item => (long)item.RequiredLevelFor(PlayerClass.PRIEST) * 1_000_000 + (long)item.Quality * 100_000
			+ (long)item.MagicBoost * 100 + item.MaximumDamage * 10L + item.MinimumDamage,
		EquipReason = "best-usable-priest-upgrade",
		Supplies = PriestSupplies.ToHashSet(),
		HandRuleGroup = "STAFF",
		// Cloth, leather and mace mastery and the basic attack, then every Priest skill by its level.
		ExpectedSkillIds = [39, 40, 41, 103],
		SkillCatalog = NaturalPriestSkills.All,
	};

	/// <summary>NA-09: after Ascension the Cleric also wears chain, shields and staves, and its accessories are gear the
	/// journey keeps. A Cleric casts: a weapon ranks by magic boost, then damage; armor by item level, then quality.
	/// NA-21: the approved help items, every help scroll and every potion combat drinks are supplies too.</summary>
	public static NaturalGearRules Cleric { get; } = new()
	{
		Class = PlayerClass.CLERIC,
		GearGroups = PriestGearGroups.Concat(["RING", "EARRING", "NECKLACE", "BELT", "STAFF", "SHIELD", "HEAD",
			"CH_TORSO", "CH_GLOVE", "CH_SHOULDER", "CH_PANTS", "CH_SHOES", "CH_HEADS"]).ToHashSet(),
		WeaponGroups = new HashSet<string> { "MACE", "STAFF" },
		OffHandGroups = new HashSet<string> { "SHIELD" },
		Score = item => Cleric!.Slot(item) == "WEAPON"
			? (long)item.MagicBoost * 1_000_000 + item.MaximumDamage * 1_000L + item.MinimumDamage
			: (long)item.ItemLevel * 1_000_000 + (long)item.Quality * 100_000 + item.Price,
		EquipReason = "best-usable-cleric-upgrade",
		Supplies = PriestSupplies
			.Concat(NaturalHelpItemAllowlist.AllLevels.Select(supply => supply.ItemId))
			.Concat(NaturalHelpItemPolicy.All.Select(help => help.ItemId))
			.Concat(NaturalIshalgenPotionPolicy.ManaPotionIds)
			.Concat([NaturalIshalgenPotionPolicy.LesserLifePotionId, NaturalIshalgenPotionPolicy.LifePotionId]).ToHashSet(),
		AfterAscension = true,
		HandRuleGroup = "STAFF",
	};
}
