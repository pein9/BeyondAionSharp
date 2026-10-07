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

/// <summary>
/// CP-29: the hand-written part of a class's table gear rule (docs/natural-class-profiles.md): the weapon groups it holds,
/// how it ranks a weapon, the armor types it wears, best first, and the consumables it takes at a reward, best first. What
/// the class can wear at all is not written here: it is read from the class's mastery rows in the class-line contract,
/// because the server refuses an item of a group the character has no mastery skill for (Java Equipment.equipItem,
/// checkAvailableEquipSkills). The Priest and the Cleric keep the rules of CP-22 and never use a table.
/// </summary>
/// <param name="WeaponGroups">The item groups the class holds in its hand. It may be able to wear more.</param>
/// <param name="ArmorTypes">The mastery names of the armor it wears (<c>CHAIN</c>, <c>LEATHER</c>, <c>ROBE</c>,
/// <c>CLOTHES</c>, <c>PLATE</c>), best first.</param>
public sealed record NaturalClassGearTable(PlayerClass Class, IReadOnlyList<string> WeaponGroups, NaturalWeaponStat WeaponStat,
	IReadOnlyList<string> ArmorTypes, IReadOnlyList<int> ConsumableOrder)
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

	// The defaults of CP-Q10 for the five new starters. Each holds one weapon and nothing in the off hand.
	public static NaturalClassGearTable Warrior { get; } = new(PlayerClass.WARRIOR, ["SWORD", "MACE"], NaturalWeaponStat.Physical,
		["CHAIN", "LEATHER", "ROBE", "CLOTHES"], DefaultConsumableOrder);
	public static NaturalClassGearTable Scout { get; } = new(PlayerClass.SCOUT, ["DAGGER"], NaturalWeaponStat.Physical,
		["LEATHER", "ROBE", "CLOTHES"], DefaultConsumableOrder);
	public static NaturalClassGearTable Mage { get; } = new(PlayerClass.MAGE, ["SPELLBOOK"], NaturalWeaponStat.Magical,
		["ROBE", "CLOTHES"], DefaultConsumableOrder);
	public static NaturalClassGearTable Engineer { get; } = new(PlayerClass.ENGINEER, ["GUN"], NaturalWeaponStat.Magical,
		["LEATHER", "ROBE", "CLOTHES"], DefaultConsumableOrder);
	public static NaturalClassGearTable Artist { get; } = new(PlayerClass.ARTIST, ["HARP"], NaturalWeaponStat.Magical,
		["ROBE", "CLOTHES"], DefaultConsumableOrder);

	/// <summary>The default table of each new starter.</summary>
	public static IReadOnlyList<NaturalClassGearTable> Starters { get; } = [Warrior, Scout, Mage, Engineer, Artist];

	/// <summary>The physical stat of CP-Q7, doubled so that it stays whole: minimum plus maximum damage is twice the
	/// mean of a swing, and the flat physical-attack bonus is counted twice with it.</summary>
	public static int PhysicalStat(int minimumDamage, int maximumDamage, int physicalAttack) => minimumDamage + maximumDamage + 2 * physicalAttack;

	/// <summary>
	/// The one score of the rule, for the equipment check, keep or sell, and the reward choice. Higher is better among
	/// items of one slot. A weapon: the class's stat, then item level. Armor: the type's place in the class's order, then
	/// item level. Anything else that is worn (an accessory): item level.
	/// </summary>
	public long Score(string group, int itemLevel, int minimumDamage, int maximumDamage, int magicBoost, int physicalAttack)
	{
		if (WeaponGroups.Contains(group))
			return WeaponStat == NaturalWeaponStat.Physical
				? PhysicalStat(minimumDamage, maximumDamage, physicalAttack) * 1_000_000L + itemLevel * 1_000L + maximumDamage
				: magicBoost * 1_000_000L + maximumDamage * 1_000L + minimumDamage;
		int place = ArmorPlace(group);
		return (place < 0 ? 0 : ArmorTypes.Count - place) * 1_000_000_000L + itemLevel * 1_000_000L;
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
	/// The class's gear rules in table form. The class's mastery rows say what it can wear: a starter's own rows, and for a
	/// second class its starter's rows with its own. A table that names a weapon group or an armor type the class has no
	/// mastery for is refused by name.
	/// </summary>
	/// <param name="kit">The help kit whose items the class keeps; the level 1-9 kit when not given.</param>
	/// <param name="skillCatalog">The catalog whose skills must be observed as learned by their level; none when not given.</param>
	public NaturalGearRules Rules(NaturalClassLineContract contract, IEnumerable<NaturalHelpSupply>? kit = null, NaturalPriestSkill[]? skillCatalog = null)
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
		string[] armorGroups = Enum.GetNames<ItemGroup>().Where(group => ArmorPlace(group) >= 0).ToArray();
		NaturalClassGearTable table = this;
		// A second class's mastery takes the place of the starter's it names.
		HashSet<int> replaced = masteries.Where(mastery => mastery.ReplacesSkillId != null).Select(mastery => mastery.ReplacesSkillId!.Value).ToHashSet();
		return new NaturalGearRules
		{
			Class = Class,
			GearGroups = WeaponGroups.Concat(armorGroups).ToHashSet(),
			WeaponGroups = WeaponGroups.ToHashSet(),
			// A starter cannot pass level 9, so gear for a later level is not its gear.
			HighestRequiredLevel = starter ? 9 : null,
			Score = item => table.Score(item.Group, item.ItemLevel, item.MinimumDamage, item.MaximumDamage, item.MagicBoost, item.PhysicalAttack),
			UpgradeScore = info => table.Score(info.Group ?? "", info.ItemLevel, info.MinimumDamage, info.MaximumDamage, info.MagicBoost, info.PhysicalAttack),
			EquipReason = $"best-usable-{Class.ToString().ToLowerInvariant()}-upgrade",
			Supplies = LifePotionIds.Concat((kit ?? NaturalHelpItemAllowlist.Starter).Select(supply => supply.ItemId)).ToHashSet(),
			HandRuleGroup = null,
			MasteryUnlocks = unlocks,
			ConsumableOrder = ConsumableOrder,
			ExpectedSkillIds = masteries.Select(mastery => mastery.SkillId).Where(id => !replaced.Contains(id)).Distinct().Order().ToArray(),
			SkillCatalog = skillCatalog ?? [],
		};
	}
}
