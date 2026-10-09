using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Items.Enums;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// CP-22: what a class treats as gear, how it ranks it and what it keeps (docs/natural-class-profiles.md). The inventory
/// policy's one decision path, the item's own gear members and the equipment check all read these rules; nothing else
/// tests for a class. CP-29a: every class's rules are built from its <see cref="NaturalClassGearTable"/>.
/// </summary>
public sealed class NaturalGearRules
{
	/// <summary>The class whose column of an item's restrict row gives the level it may wear the item at.</summary>
	public required PlayerClass Class { get; init; }

	/// <summary>The item groups the class treats as gear.</summary>
	public required IReadOnlySet<string> GearGroups { get; init; }

	/// <summary>Gear groups that go in the weapon slot.</summary>
	public required IReadOnlySet<string> WeaponGroups { get; init; }

	/// <summary>Gear above this required level is not considered at all (the Ishalgen Priest stops at 9); null for no cap.</summary>
	public int? HighestRequiredLevel { get; init; }

	/// <summary>Higher is better, among items of one slot.</summary>
	public required Func<NaturalItem, long> Score { get; init; }

	/// <summary>The reason an equip decision carries.</summary>
	public required string EquipReason { get; init; }

	/// <summary>Consumables the class keeps and never sells.</summary>
	public required IReadOnlySet<int> Supplies { get; init; }

	/// <summary>Skills every character of the class has from level 1: its masteries.</summary>
	public IReadOnlyList<int> ExpectedSkillIds { get; init; } = [];

	/// <summary>CP-29: what the class's mastery skills let it wear (weapon groups, armor types, <c>SHIELD</c>). The rules
	/// never ask the server for an item of a group the class has no mastery for.</summary>
	public required IReadOnlySet<string> MasteryUnlocks { get; init; }

	/// <summary>CP-68: what the equipment check puts in the off hand beside a one-hand weapon; nothing for every class today.</summary>
	public NaturalOffHand OffHand { get; init; }

	/// <summary>NR-03: a class that holds two weapons may hold this one in either hand: a one-hand weapon of its groups
	/// (the item group's own answer, as Java ItemTemplate.isOneHandWeapon reads it).</summary>
	public bool IsSecondWeapon(NaturalItem item) => OffHand == NaturalOffHand.SecondWeapon && WeaponGroups.Contains(item.Group) &&
		Enum.TryParse(item.Group, out ItemGroup parsed) && parsed.GetItemSubType() == ItemSubType.ONE_HAND;

	/// <summary><see cref="Score"/> over the client's tooltip view of an item, for the equipment check.</summary>
	public required Func<NaturalGearInfo, long> UpgradeScore { get; init; }

	/// <summary>The pick among consumables at a reward, best first.</summary>
	public IReadOnlyList<int> ConsumableOrder { get; init; } = [];

	public bool IsGear(NaturalItem item) => GearGroups.Contains(item.Group);

	/// <summary>The slot name gear of one kind competes for; null for what is not gear.</summary>
	public string? Slot(NaturalItem item) => !IsGear(item) ? null
		: WeaponGroups.Contains(item.Group) ? "WEAPON"
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

	/// <summary>
	/// CP-29: the class has the mastery skill the server asks for before it lets an item of this group be worn (Java
	/// Equipment.checkAvailableEquipSkills): the group needs none, or the class's mastery rows unlock it.
	/// </summary>
	public bool Wears(string? group)
	{
		if (!Enum.TryParse(group, out ItemGroup parsed)) return false;
		if (!parsed.RequiresMastery()) return true;
		if (parsed == ItemGroup.SHIELD) return MasteryUnlocks.Contains("SHIELD");
		if (parsed.GetEquipType() == EquipType.WEAPON) return MasteryUnlocks.Contains(group!);
		ItemSubType type = parsed.GetItemSubType();
		return type == ItemSubType.ALL_ARMOR || MasteryUnlocks.Contains(type.ToString());
	}

	/// <summary>The item goes in the class's hands: a weapon of its groups, or a shield when it has the shield mastery.
	/// A leg that retains the held weapon keeps these (the coin-gear and Haramel legs).</summary>
	public bool GoesInItsHands(NaturalItem item) => WeaponGroups.Contains(item.Group) ||
		item.Group == "SHIELD" && MasteryUnlocks.Contains("SHIELD");

	/// <summary>The class may wear the item at this level or will at a later one: <see cref="Usable"/> without the
	/// character's level as a floor.</summary>
	public bool UsableNowOrLater(NaturalItem item, int level)
	{
		int required = item.RequiredLevelFor(Class), maximum = item.MaximumLevelFor(Class);
		return IsGear(item) && Wears(item.Group) && item.Quality > 0 && required > 0 && (HighestRequiredLevel is not int cap || required <= cap) &&
			(maximum == 0 || level <= maximum) && item.Race is ("PC_ALL" or "ASMODIANS");
	}

	/// <summary>The class's level 1 skills are in the observed skill list. NR-18: the catalog's skills by level were
	/// checked here too while the Priest's table was typed by hand; a generated catalog needs the run's data.</summary>
	public bool AutoLearnedSkillsObserved(int level, IReadOnlyDictionary<int, BotSkill> learned) =>
		ExpectedSkillIds.All(learned.ContainsKey);

	/// <summary>The Ishalgen Priest: its table and the level 1-9 kit.</summary>
	public static NaturalGearRules Priest { get; } = NaturalClassGearTable.Priest.Rules(NaturalClassLineContract.LoadDefault(),
		NaturalHelpItemAllowlist.Starter);

	/// <summary>The Cleric: its table and the approved help items of every level (OD-13).</summary>
	public static NaturalGearRules Cleric { get; } = NaturalClassGearTable.Cleric.Rules(NaturalClassLineContract.LoadDefault(),
		NaturalHelpItemAllowlist.AllLevels);
}
