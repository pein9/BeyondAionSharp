using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.World;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

/// <summary>
/// CP-22: the gear rules of the class profile. That the Priest's and the Cleric's rules decide every item as the inline
/// code did is the golden gear test's job (NaturalGearGoldenTests); these tests cover what the rules add and name.
/// </summary>
public sealed class NaturalGearRulesTests
{
	private const long Main = 1, Sub = 2;

	[Fact]
	public void TheItemCarriesTheWholeRestrictRowAndItsOldMembersReadTheirColumns()
	{
		int[] restrict = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17];
		int[] maximum = [0, 0, 0, 0, 0, 0, 0, 0, 0, 30, 40, 0, 0, 0, 0, 0, 0];
		var item = new NaturalItem(1, "MACE", restrict, maximum, "PC_ALL", 100, 1, 10, 20, 30, 4);
		Assert.Equal((10, 30, 11, 40), (item.RequiredLevel, item.MaximumLevel, item.ClericLevel, item.ClericMaximumLevel));
		foreach (PlayerClass playerClass in new[] { PlayerClass.WARRIOR, PlayerClass.SCOUT, PlayerClass.MAGE, PlayerClass.PRIEST,
			PlayerClass.CLERIC, PlayerClass.CHANTER, PlayerClass.ENGINEER, PlayerClass.ARTIST })
		{
			Assert.Equal(restrict[playerClass.GetClassId()], item.RequiredLevelFor(playerClass));
			Assert.Equal(maximum[playerClass.GetClassId()], item.MaximumLevelFor(playerClass));
		}
		// A row that stops short, or no restrict_max row at all, reads as 0, as the two old columns did.
		var shortRow = item with { Restrict = [1, 1, 1], RestrictMax = [] };
		Assert.Equal((0, 0, 0, 0), (shortRow.RequiredLevel, shortRow.MaximumLevel, shortRow.ClericLevel, shortRow.ClericMaximumLevel));
	}

	[Fact]
	public void ThePriestAndClericRulesAreTheTwoRuleSetsThePolicyHeld()
	{
		NaturalGearRules priest = NaturalGearRules.Priest, cleric = NaturalGearRules.Cleric;
		Assert.Same(priest, NaturalPriestProfile.Priest.Gear);
		Assert.Same(cleric, NaturalPriestProfile.Cleric.Gear);
		Assert.Equal((PlayerClass.PRIEST, 9, false, "best-usable-priest-upgrade"), (priest.Class, priest.HighestRequiredLevel, priest.AfterAscension, priest.EquipReason));
		Assert.Equal((PlayerClass.CLERIC, null, true, "best-usable-cleric-upgrade"), (cleric.Class, cleric.HighestRequiredLevel, cleric.AfterAscension, cleric.EquipReason));
		// The Cleric's gear is the Priest's and more: accessories, the staff, the shield and chain.
		Assert.Equal(18, priest.GearGroups.Count);
		Assert.True(priest.GearGroups.IsSubsetOf(cleric.GearGroups));
		Assert.Equal(new[] { "BELT", "CH_GLOVE", "CH_HEADS", "CH_PANTS", "CH_SHOES", "CH_SHOULDER", "CH_TORSO", "EARRING", "HEAD", "NECKLACE", "RING",
			"SHIELD", "STAFF" }, cleric.GearGroups.Except(priest.GearGroups).Order(StringComparer.Ordinal));
		// Supplies: the starter potions, the bought elixir and the level 1-9 kit; the Cleric adds every approved and known help item.
		Assert.Equal(new[] { 162000002, 162000006, 162000007, 162000052, 164000067, 164000076 }, priest.Supplies.Order());
		Assert.Equal(priest.Supplies.Concat(NaturalHelpItemAllowlist.AllLevels.Select(supply => supply.ItemId))
			.Concat(NaturalHelpItemPolicy.All.Select(help => help.ItemId)).Concat(NaturalIshalgenPotionPolicy.ManaPotionIds)
			.Concat([NaturalIshalgenPotionPolicy.LesserLifePotionId, NaturalIshalgenPotionPolicy.LifePotionId]).Distinct().Order(), cleric.Supplies.Order());
		// Both keep the operator's staff rule.
		Assert.Equal(("STAFF", "STAFF"), (priest.HandRuleGroup, cleric.HandRuleGroup));
	}

	[Fact]
	public void SlotsScoresAndTheLevelCapFollowTheRules()
	{
		NaturalGearRules priest = NaturalGearRules.Priest, cleric = NaturalGearRules.Cleric;
		NaturalItem Item(string group, int level, int quality = 1, int boost = 0, int itemLevel = 0, int price = 0) =>
			new(1, group, Enumerable.Repeat(level, 17).ToArray(), [], "PC_ALL", price, quality, 5, 9, boost, 4, itemLevel);
		// A staff is no Priest gear; for the Cleric it is the weapon and a shield the off hand.
		Assert.Equal((null, "WEAPON"), (priest.Slot(Item("STAFF", 1)), cleric.Slot(Item("STAFF", 1))));
		Assert.Equal((null, "SUB"), (priest.Slot(Item("SHIELD", 1)), cleric.Slot(Item("SHIELD", 1))));
		Assert.Equal(("WEAPON", "WEAPON"), (priest.Slot(Item("MACE", 1)), cleric.Slot(Item("MACE", 1))));
		Assert.Equal(("HEAD", "HEAD", null, "HEAD"), (priest.Slot(Item("CL_HEADS", 1)), cleric.Slot(Item("LT_HEADS", 1)), priest.Slot(Item("HEAD", 1)), cleric.Slot(Item("HEAD", 1))));
		Assert.Equal(("TORSO", "TORSO", null, "RING"), (priest.Slot(Item("RB_TORSO", 1)), cleric.Slot(Item("CH_TORSO", 1)), priest.Slot(Item("RING", 1)), cleric.Slot(Item("RING", 1))));
		Assert.Null(cleric.Slot(Item("SWORD", 1)));
		// The Priest never considers gear above required level 9; the Cleric has no cap.
		Assert.True(priest.Usable(Item("MACE", 9), 20));
		Assert.False(priest.Usable(Item("MACE", 10), 20));
		Assert.True(cleric.Usable(Item("MACE", 10), 20));
		Assert.False(cleric.Usable(Item("MACE", 21), 20));
		Assert.False(cleric.Usable(Item("MACE", 5, quality: 0), 20));
		Assert.False(cleric.Usable(Item("MACE", 5) with { Race = "ELYOS" }, 20));
		Assert.False(cleric.Usable(Item("MACE", 5) with { RestrictMax = Enumerable.Repeat(15, 17).ToArray() }, 20));
		// The two score formulas.
		Assert.Equal(7 * 1_000_000L + 2 * 100_000 + 30 * 100 + 9 * 10 + 5, priest.Score(Item("MACE", 7, quality: 2, boost: 30)));
		Assert.Equal(30 * 1_000_000L + 9 * 1_000 + 5, cleric.Score(Item("STAFF", 7, quality: 2, boost: 30)));
		Assert.Equal(12 * 1_000_000L + 2 * 100_000 + 450, cleric.Score(Item("CH_TORSO", 7, quality: 2, itemLevel: 12, price: 450)));
	}

	[Fact]
	public void TheExpectedSkillsAreTheClassesLevelOneSkillsAndItsCatalogByLevel()
	{
		IReadOnlyDictionary<int, BotSkill> Learned(params int[] ids) => ids.ToDictionary(id => id, id => new BotSkill((ushort)id, 1, 0, 0, 0, 0));
		NaturalGearRules priest = NaturalGearRules.Priest;
		Assert.Equal(new[] { 39, 40, 41, 103 }, priest.ExpectedSkillIds);
		Assert.Same(NaturalPriestSkills.All, priest.SkillCatalog);
		Assert.True(priest.AutoLearnedSkillsObserved(1, Learned(39, 40, 41, 103, 1838, 4012)));
		Assert.False(priest.AutoLearnedSkillsObserved(1, Learned(39, 40, 41, 1838, 4012)));
		Assert.False(priest.AutoLearnedSkillsObserved(3, Learned(39, 40, 41, 103, 1838, 4012)));
		Assert.True(priest.AutoLearnedSkillsObserved(3, Learned(39, 40, 41, 103, 1838, 4012, 1614)));
		// No check was ever defined for the Cleric: its rules expect nothing.
		Assert.Empty(NaturalGearRules.Cleric.ExpectedSkillIds);
		Assert.True(NaturalGearRules.Cleric.AutoLearnedSkillsObserved(26, Learned()));
	}

	[Fact]
	public void TheHandRuleOfTheEquipmentCheckComesFromTheRules()
	{
		var items = new Dictionary<int, NaturalGearInfo>
		{
			[1] = new(Main | Sub, 1, 1, true, "MACE", 100),      // worn
			[2] = new(Main | Sub, 10, 10, true, "STAFF", 260),
			[3] = new(Main | Sub, 12, 12, true, "MACE", 300),
			[4] = new(Sub, 12, 12, true, "SHIELD"),
		};
		NaturalGearInfo? Describe(int id) => items.GetValueOrDefault(id);
		BotInventoryItem Bag(int id, long worn = 0) => new(id, id, "", 1, 0, "", (ushort)worn, false) { Details = BotItemDetails.Empty with { EquippedSlot = worn } };
		BotInventoryItem[] inventory = [Bag(1, Main), Bag(2), Bag(3), Bag(4)];
		// The staff rule, by default and when the Priest line's rules are named: the staff takes the hands, the better mace and the shield stay.
		(int, long)[] staff = [(2, Main)];
		Assert.Equal(staff, NaturalGearPolicy.SelectUpgrades(inventory, 12, Describe, 0).Select(upgrade => (upgrade.ItemId, upgrade.Slot)));
		Assert.Equal(staff, NaturalGearPolicy.SelectUpgrades(inventory, 12, Describe, 0, rules: NaturalGearRules.Cleric).Select(upgrade => (upgrade.ItemId, upgrade.Slot)));
		// Rules without a hand rule wear by item level: the level-12 mace and the shield.
		var none = new NaturalGearRules
		{
			Class = PlayerClass.WARRIOR, GearGroups = new HashSet<string>(), WeaponGroups = new HashSet<string>(), Score = _ => 0,
			EquipReason = "none", Supplies = new HashSet<int>(),
		};
		Assert.Equal(new (int, long)[] { (3, Main), (4, Sub) },
			NaturalGearPolicy.SelectUpgrades(inventory, 12, Describe, 0, rules: none).Select(upgrade => (upgrade.ItemId, upgrade.Slot)));
		// Rules that name another group apply the same rule to it.
		var maces = new NaturalGearRules
		{
			Class = PlayerClass.WARRIOR, GearGroups = new HashSet<string>(), WeaponGroups = new HashSet<string>(), Score = _ => 0,
			EquipReason = "none", Supplies = new HashSet<int>(), HandRuleGroup = "MACE",
		};
		Assert.Equal(new (int, long)[] { (3, Main) },
			NaturalGearPolicy.SelectUpgrades(inventory, 12, Describe, 0, rules: maces).Select(upgrade => (upgrade.ItemId, upgrade.Slot)));
	}
}
