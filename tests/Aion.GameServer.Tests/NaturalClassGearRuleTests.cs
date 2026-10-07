using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.World;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

/// <summary>
/// CP-29: the table form of the gear rules, for every class but the Priest and the Cleric. What a class can wear comes
/// from its mastery rows; its weapon is ranked by one stat and its armor by its type order; the same score decides the
/// equipment check, keep or sell, and the reward choice.
/// </summary>
public sealed class NaturalClassGearRuleTests
{
	private const long Main = 1, Sub = 2;
	private const int Bandage = 169300002, StarterManaPotion = 162000007, SpiritRing = 122000869, RaidersShield = 115000024;
	private const int KarmicWarhammer = 100100495, KarmicStaff = 101500498;

	// The ten Ishalgen quests whose reward choice depends on the class, each at a level its character holds it at.
	private static readonly (int Quest, int Level, string Kind)[] RewardQuests =
	[
		(2100, 3, "weapon"), (2001, 4, "armor"), (2002, 5, "weapon"), (2117, 6, "consumable"), (2124, 7, "consumable"),
		(2005, 8, "armor"), (2006, 8, "armor"), (2134, 8, "weapon"), (2129, 9, "armor"), (2007, 9, "armor"),
	];

	private static readonly Dictionary<PlayerClass, int> StarterWeapon = new()
	{
		[PlayerClass.WARRIOR] = 100000094, [PlayerClass.SCOUT] = 100200112, [PlayerClass.MAGE] = 100600034,
		[PlayerClass.ENGINEER] = 101800181, [PlayerClass.ARTIST] = 102000194,
	};

	private static readonly int[] KnownItems =
	[
		.. StarterWeapon.Values, Bandage, StarterManaPotion, SpiritRing, RaidersShield, KarmicWarhammer, KarmicStaff,
		.. NaturalClassGearTable.LifePotionIds, .. NaturalHelpItemAllowlist.Starter.Select(supply => supply.ItemId),
		// Boromer's and Anturoon shoes: robe, leather and chain at item levels 4 and 8.
		114100794, 114300804, 114500766, 114100795, 114300805, 114500767,
	];

	private static readonly Lazy<NaturalClassLineContract> Lines = new(NaturalClassLineContract.LoadDefault);
	private static readonly Lazy<NaturalIshalgenInventoryPolicy> Policy = new(() =>
		NaturalIshalgenInventoryPolicy.Load(RealStaticData.RepoRoot(), KnownItems));

	public static TheoryData<PlayerClass> Starters => new(NaturalClassGearTable.Starters.Select(table => table.Class));

	private static NaturalClassGearTable Table(PlayerClass playerClass) => NaturalClassGearTable.Starters.Single(table => table.Class == playerClass);
	private static NaturalGearRules Rules(PlayerClass playerClass) => Table(playerClass).Rules(Lines.Value);

	[Fact]
	public void TheFiveStartersHaveTheDefaultsOfTheOperatorsAnswer()
	{
		// CP-Q10: sword or mace and chain; dagger and leather; spellbook and robe; pistol and leather; harp and robe.
		Assert.Equal(new[] { PlayerClass.WARRIOR, PlayerClass.SCOUT, PlayerClass.MAGE, PlayerClass.ENGINEER, PlayerClass.ARTIST },
			NaturalClassGearTable.Starters.Select(table => table.Class));
		(string Weapons, NaturalWeaponStat Stat, string Armor) Read(PlayerClass playerClass) =>
			(string.Join(',', Table(playerClass).WeaponGroups), Table(playerClass).WeaponStat, Table(playerClass).ArmorTypes[0]);
		Assert.Equal(("SWORD,MACE", NaturalWeaponStat.Physical, "CHAIN"), Read(PlayerClass.WARRIOR));
		Assert.Equal(("DAGGER", NaturalWeaponStat.Physical, "LEATHER"), Read(PlayerClass.SCOUT));
		Assert.Equal(("SPELLBOOK", NaturalWeaponStat.Magical, "ROBE"), Read(PlayerClass.MAGE));
		Assert.Equal(("GUN", NaturalWeaponStat.Magical, "LEATHER"), Read(PlayerClass.ENGINEER));
		Assert.Equal(("HARP", NaturalWeaponStat.Magical, "ROBE"), Read(PlayerClass.ARTIST));
		Assert.All(NaturalClassGearTable.Starters, table => Assert.Equal(new[] { 162000052, 162000057, 169000003 }, table.ConsumableOrder));
	}

	[Theory]
	[MemberData(nameof(Starters))]
	public async Task AClassWearsWhatItsMasteryRowsUnlockAndTheServerAgrees(PlayerClass playerClass)
	{
		NaturalClassGearTable table = Table(playerClass);
		NaturalGearRules rules = Rules(playerClass);
		NaturalStarterClass row = Lines.Value.Starter(playerClass);
		Assert.True(rules.IsTable);
		Assert.Equal((playerClass, (int?)9, false, (string?)null, $"best-usable-{playerClass.ToString().ToLowerInvariant()}-upgrade"),
			(rules.Class, rules.HighestRequiredLevel, rules.AfterAscension, rules.HandRuleGroup, rules.EquipReason));
		Assert.Equal(row.Masteries.Select(mastery => mastery.Unlocks).Order(), rules.MasteryUnlocks!.Order());
		Assert.Equal(row.Masteries.Select(mastery => mastery.SkillId).Order(), rules.ExpectedSkillIds);
		Assert.Equal(table.WeaponGroups.Order(), rules.WeaponGroups.Order());
		Assert.Empty(rules.OffHandGroups);
		// Gear: the weapon groups and every body piece and head of the table's armor types; no shield, no accessory.
		Assert.Equal(table.WeaponGroups.Concat(Enum.GetNames<ItemGroup>().Where(group =>
				new[] { "_TORSO", "_GLOVE", "_SHOULDER", "_PANTS", "_SHOES", "_HEADS" }.Any(part => group.EndsWith(part, StringComparison.Ordinal)) &&
				table.ArmorTypes.Contains(Enum.Parse<ItemGroup>(group).GetItemSubType().ToString()))).Order(StringComparer.Ordinal),
			rules.GearGroups.Order(StringComparer.Ordinal));
		Assert.DoesNotContain("SHIELD", rules.GearGroups);
		Assert.DoesNotContain("RING", rules.GearGroups);
		Assert.All(rules.GearGroups, group => Assert.True(rules.Wears(group), $"{playerClass} gear group {group} is not wearable."));

		// The server's own rule (Equipment.CheckAvailableEquipSkills): a group with mastery skills needs one of them.
		SkillData skills = (await RealStaticData.LoadAsync()).StaticData.SkillDataDh;
		HashSet<int> held = row.Masteries.Select(mastery => mastery.SkillId).ToHashSet();
		foreach (ItemGroup group in Enum.GetValues<ItemGroup>())
		{
			ISet<int> needed = skills.GetMasterySkills(group);
			Assert.True((needed.Count == 0 || needed.Overlaps(held)) == rules.Wears(group.ToString()), $"{playerClass} and {group} disagree with the server.");
		}
		Assert.False(rules.Wears(null));
		Assert.False(rules.Wears("NOT_A_GROUP"));
	}

	[Theory]
	[MemberData(nameof(Starters))]
	public void TheTenClassDependentRewardsAreTheClasssWeaponArmorOrConsumable(PlayerClass playerClass)
	{
		NaturalClassGearTable table = Table(playerClass);
		NaturalGearRules rules = Rules(playerClass);
		NaturalIshalgenInventoryPolicy policy = Policy.Value;
		// What the character carries: its starter weapon in the hand, the starter potions and bandages, and the kit.
		var bag = new List<BotInventoryItem> { Item(1, StarterWeapon[playerClass], equipped: Main), Item(2, 162000002, 100),
			Item(3, StarterManaPotion, 100), Item(4, Bandage, 20) };
		bag.AddRange(NaturalHelpItemAllowlist.Starter.Select((supply, index) => Item(10 + index, supply.ItemId, supply.TopUpTo)));
		int next = 100;
		foreach ((int quest, int level, string kind) in RewardQuests)
		{
			int[] offered = Offered(quest);
			int index = policy.ChooseReward(quest, level, bag, rules);
			Assert.InRange(index, 0, offered.Length - 1);
			NaturalItem picked = policy.Item(offered[index]);
			switch (kind)
			{
				case "weapon":
					Assert.Contains(picked.Group, table.WeaponGroups);
					Assert.True(rules.Usable(picked, level), $"{playerClass} cannot wear its Q{quest} pick at level {level}.");
					// The best of the offered weapons of its groups, by the rule's score.
					Assert.Equal(offered.Select(policy.Item).Where(item => table.WeaponGroups.Contains(item.Group)).Max(rules.Score), rules.Score(picked));
					break;
				case "armor":
					Assert.Equal(table.ArmorTypes[0], Enum.Parse<ItemGroup>(picked.Group).GetItemSubType().ToString());
					Assert.True(rules.Usable(picked, level));
					break;
				default:
					Assert.Equal(162000052, picked.Id);
					break;
			}
			bag.Add(Item(next++, picked.Id));
			AssertNothingWearableOrProtectedIsSold(policy, rules, bag, level);
			// Wear what the plan says to wear, as the equipment check would, so the next choice is made against it.
			foreach (NaturalInventoryDecision equip in policy.Decide(bag, level, 27, rules).Equips)
			{
				string slot = rules.Slot(policy.Item(equip.ItemId))!;
				for (int i = 0; i < bag.Count; i++)
					if (bag[i].ObjectId == equip.ObjectId) bag[i] = Item(bag[i].ObjectId, bag[i].ItemId, equipped: slot == "WEAPON" ? Main : 1 << 10);
					else if (bag[i].Details.EquippedSlot.GetValueOrDefault() != 0 && policy.Item(bag[i].ItemId) is { } worn && rules.Slot(worn) == slot)
						bag[i] = Item(bag[i].ObjectId, bag[i].ItemId);
			}
			AssertNothingWearableOrProtectedIsSold(policy, rules, bag, level);
		}
		// By level 9 the hand holds the Aldelle weapon of the class and the five body slots hold its armor type.
		NaturalItem[] wornNow = bag.Where(item => item.Details.EquippedSlot.GetValueOrDefault() != 0).Select(item => policy.Item(item.ItemId)).ToArray();
		Assert.Contains(wornNow, item => rules.Slot(item) == "WEAPON" && item.ItemLevel == 8 && table.WeaponGroups.Contains(item.Group));
		Assert.Equal(new[] { "GLOVE", "PANTS", "SHOES", "TORSO" }, wornNow.Select(rules.Slot).Where(slot => slot != "WEAPON").Order());
		// The old weapons are surplus and may go; the bandages are no supply (CP-Q11).
		NaturalInventoryPlan last = policy.Decide(bag, 9, 27, rules);
		Assert.Contains(last.Sales, sale => sale.ItemId == StarterWeapon[playerClass] && sale.Reason == "surplus-gear");
		Assert.DoesNotContain(Bandage, rules.Supplies);
		Assert.Equal("sell", last.Decisions.Single(decision => decision.ItemId == Bandage).Action);
	}

	[Fact]
	public void TheWarriorTakesTheMaceAtQ2100AndNeverTheShield()
	{
		// CP-Q10 (answered 2026-10-07): the weapon, not the shield. By the stat the mace (20-30) is above the sword (20-26).
		NaturalGearRules warrior = Rules(PlayerClass.WARRIOR);
		NaturalIshalgenInventoryPolicy policy = Policy.Value;
		int[] offered = Offered(2100);
		Assert.Contains(RaidersShield, offered);
		BotInventoryItem[] bag = [Item(1, StarterWeapon[PlayerClass.WARRIOR], equipped: Main)];
		Assert.Equal(100100024, offered[policy.ChooseReward(2100, 3, bag, warrior)]);
		// Holding the mace already, so that nothing offered is an upgrade, it still takes a weapon of its groups and not
		// the shield.
		BotInventoryItem[] holdsMace = [Item(1, 100100024, equipped: Main)];
		Assert.Contains(policy.Item(offered[policy.ChooseReward(2100, 3, holdsMace, warrior)]).Group, NaturalClassGearTable.Warrior.WeaponGroups);
		Assert.True(warrior.Wears("SHIELD"));
		Assert.Null(warrior.Slot(policy.Item(RaidersShield)));
		// Each other starter takes its own group there.
		foreach ((PlayerClass playerClass, string group) in new[] { (PlayerClass.SCOUT, "DAGGER"), (PlayerClass.MAGE, "SPELLBOOK"),
			(PlayerClass.ENGINEER, "GUN"), (PlayerClass.ARTIST, "HARP") })
			Assert.Equal(group, policy.Item(offered[policy.ChooseReward(2100, 3, [Item(1, StarterWeapon[playerClass], equipped: Main)], Rules(playerClass))]).Group);
	}

	[Fact]
	public void AnItemForALaterLevelIsChosenAndKeptNotSold()
	{
		// Q2134's Aldelle weapons need level 8. A level-7 Mage still takes the spellbook, and the plan holds it.
		NaturalGearRules mage = Rules(PlayerClass.MAGE);
		NaturalIshalgenInventoryPolicy policy = Policy.Value;
		BotInventoryItem[] bag = [Item(1, StarterWeapon[PlayerClass.MAGE], equipped: Main)];
		int[] offered = Offered(2134);
		NaturalItem picked = policy.Item(offered[policy.ChooseReward(2134, 7, bag, mage)]);
		Assert.Equal(("SPELLBOOK", 8), (picked.Group, picked.RequiredLevelFor(PlayerClass.MAGE)));
		Assert.False(mage.Usable(picked, 7));
		Assert.True(mage.UsableNowOrLater(picked, 7));
		NaturalInventoryDecision early = policy.Decide([.. bag, Item(2, picked.Id)], 7, 27, mage).Decisions.Single(decision => decision.ObjectId == 2);
		Assert.Equal(("hold", "gear-for-later"), (early.Action, early.Reason));
		Assert.Equal("equip", policy.Decide([.. bag, Item(2, picked.Id)], 8, 27, mage).Decisions.Single(decision => decision.ObjectId == 2).Action);
		// Under the Priest's rules the same held mace-for-later would be surplus: the table form is what keeps it.
		Assert.False(NaturalGearRules.Priest.IsTable);
		// An accessory is kept: the equipment check wears it by item level.
		NaturalInventoryDecision ring = policy.Decide([.. bag, Item(3, SpiritRing)], 5, 27, mage).Decisions.Single(decision => decision.ObjectId == 3);
		Assert.Equal(("hold", "accessory-kept"), (ring.Action, ring.Reason));
		// Supplies: the six life potions and the kit, and nothing else.
		Assert.Equal(NaturalClassGearTable.LifePotionIds.Concat(NaturalHelpItemAllowlist.Starter.Select(supply => supply.ItemId)).Distinct().Order(), mage.Supplies.Order());
	}

	[Fact]
	public async Task ThePhysicalStatRanksTheTwoKarmicWeaponsPerSwing()
	{
		// CP-Q7, on its default: per swing, the mean damage plus the flat physical-attack bonus. Staff 58-88, mean 73.
		// Warhammer 44-66 with 7 physical attack, 55 plus 7.
		NaturalItem staff = Policy.Value.Item(KarmicStaff), warhammer = Policy.Value.Item(KarmicWarhammer);
		Assert.Equal((58, 88, 0, "STAFF"), (staff.MinimumDamage, staff.MaximumDamage, staff.PhysicalAttack, staff.Group));
		Assert.Equal((44, 66, 7, "MACE"), (warhammer.MinimumDamage, warhammer.MaximumDamage, warhammer.PhysicalAttack, warhammer.Group));
		Assert.Equal((2 * 73, 2 * (55 + 7)), (NaturalClassGearTable.PhysicalStat(58, 88, 0), NaturalClassGearTable.PhysicalStat(44, 66, 7)));
		// A Chanter's table, as CP-32 will write it: mace or staff by the physical stat. The staff ranks first.
		var chanter = new NaturalClassGearTable(PlayerClass.CHANTER, ["MACE", "STAFF"], NaturalWeaponStat.Physical,
			["CHAIN", "LEATHER", "ROBE", "CLOTHES"], NaturalClassGearTable.DefaultConsumableOrder).Rules(Lines.Value);
		Assert.True(chanter.Score(staff) > chanter.Score(warhammer));
		Assert.Null(chanter.HighestRequiredLevel);
		Assert.True(chanter.Wears("SHIELD") && chanter.Wears("STAFF") && chanter.Wears("CL_TORSO") && !chanter.Wears("PL_TORSO") && !chanter.Wears("SWORD"));
		// The second class's masteries take the place of the starter's they name: mace 46 for 39, leather 48 for 41, robe 106 for 103.
		Assert.Equal(new[] { 40, 46, 48, 49, 50, 89, 106 }, chanter.ExpectedSkillIds);
		// The Cleric's own rule ranks them by magic boost and agrees; it is not a table and does not change.
		Assert.False(NaturalGearRules.Cleric.IsTable);
		Assert.Null(NaturalGearRules.Cleric.UpgradeScore);

		// One score: the tooltip view the equipment check reads gives the same number as the item the plan reads.
		var templates = (await RealStaticData.LoadAsync()).StaticData.ItemDataDh;
		foreach (PlayerClass playerClass in StarterWeapon.Keys.Append(PlayerClass.CHANTER))
		{
			NaturalGearRules rules = playerClass == PlayerClass.CHANTER ? chanter : Rules(playerClass);
			foreach (int id in KnownItems.Concat(RewardQuests.SelectMany(row => Offered(row.Quest))).Distinct())
				if (NaturalInventoryCheck.Describe(templates.GetItemTemplate(id), playerClass, Race.ASMODIANS) is { } info &&
					Policy.Value.Item(id) is { } item && rules.IsGear(item))
					Assert.Equal(rules.Score(item), rules.UpgradeScore!(info));
		}
		NaturalGearInfo tooltip = NaturalInventoryCheck.Describe(templates.GetItemTemplate(KarmicWarhammer), PlayerClass.CHANTER, Race.ASMODIANS)!;
		Assert.Equal((44, 66, 7), (tooltip.MinimumDamage, tooltip.MaximumDamage, tooltip.PhysicalAttack));
	}

	[Fact]
	public void TheEquipmentCheckAsksOnlyForWhatTheClassCanWearAndRanksByTheRule()
	{
		var items = new Dictionary<int, NaturalGearInfo>
		{
			[1] = new(Main | Sub, 1, 1, true, "SWORD", 0, 16, 20),          // the Training Sword
			[2] = new(Main | Sub, 3, 3, true, "SWORD", 0, 20, 26),
			[3] = new(Main | Sub, 3, 3, true, "MACE", 100, 20, 30),
			[4] = new(Sub, 3, 3, true, "SHIELD"),
			[5] = new(Main | Sub, 3, 8, true, "SPELLBOOK", 150, 38, 43),
			[6] = new(32, 1, 8, true, "LT_SHOES"),
			[7] = new(32, 1, 8, true, "RB_SHOES"),
			[8] = new(32, 1, 4, true, "CH_SHOES"),
			[9] = new(64 | 128, 1, 7, true, "RING"),
			[10] = new(Main | Sub, 1, 1, true, "DAGGER", 0, 15, 17),
			[11] = new(Main | Sub, 1, 1, true, "SPELLBOOK", 80, 20, 23),
		};
		NaturalGearInfo? Describe(int id) => items.GetValueOrDefault(id);
		BotInventoryItem Bag(int id, long worn = 0) => new(id, id, "", 1, 0, "", (ushort)worn, false) { Details = BotItemDetails.Empty with { EquippedSlot = worn } };
		(int, long)[] Upgrades(BotInventoryItem[] inventory, NaturalGearRules? rules, IReadOnlySet<int>? refused = null) =>
			NaturalGearPolicy.SelectUpgrades(inventory, 9, Describe, Sub, refused, rules).Select(upgrade => (upgrade.ItemId, upgrade.Slot)).ToArray();

		// A Warrior holding the Training Sword: the mace by the stat, chain shoes over the higher leather pair, the ring; no
		// shield, no spellbook. Without a rule the item level decides and the server is asked for the spellbook.
		BotInventoryItem[] warriorBag = [Bag(1, Main), Bag(2), Bag(3), Bag(4), Bag(5), Bag(6), Bag(7), Bag(8), Bag(9)];
		Assert.Equal(new (int, long)[] { (3, Main), (8, 32), (9, 64) }, Upgrades(warriorBag, Rules(PlayerClass.WARRIOR)));
		Assert.Equal((5, Main), Upgrades(warriorBag, null)[0]);
		// With chain worn, a leather pair of a higher item level is no upgrade; with leather worn, chain is.
		Assert.Empty(Upgrades([Bag(1, Main), Bag(8, 32), Bag(6)], Rules(PlayerClass.WARRIOR)));
		Assert.Equal(new (int, long)[] { (8, 32) }, Upgrades([Bag(1, Main), Bag(6, 32), Bag(8)], Rules(PlayerClass.WARRIOR)));
		// Holding the mace, the sword of the same level is not asked for.
		Assert.Empty(Upgrades([Bag(3, Main), Bag(2), Bag(4)], Rules(PlayerClass.WARRIOR)));

		// A Mage: leather and chain are filtered out before the server is asked, so the robe pair is worn in the same
		// check. Before, the leather pair (the lower object id at the same item level) took the slot and was refused.
		BotInventoryItem[] mageBag = [Bag(11, Main), Bag(6), Bag(7), Bag(8), Bag(5), Bag(3)];
		Assert.Equal(new (int, long)[] { (5, Main), (7, 32) }, Upgrades(mageBag, Rules(PlayerClass.MAGE)));
		Assert.Equal((6, 32L), Upgrades(mageBag, null).Single(upgrade => upgrade.Item2 == 32));
		// A refused item is still never asked for again.
		Assert.Equal(new (int, long)[] { (5, Main) }, Upgrades(mageBag, Rules(PlayerClass.MAGE), new HashSet<int> { 7 }));

		// A Scout holds its dagger: a sword it has the mastery for is not its weapon group. Leather is its armor.
		Assert.Equal(new (int, long)[] { (6, 32) }, Upgrades([Bag(10, Main), Bag(2), Bag(6), Bag(7), Bag(8)], Rules(PlayerClass.SCOUT)));
		// With nothing of its group in the hand, its own group is taken.
		Assert.Equal(new (int, long)[] { (10, Main) }, Upgrades([Bag(10), Bag(2)], Rules(PlayerClass.SCOUT)));
	}

	[Fact]
	public void ATableThatNamesWhatTheClassHasNoMasteryForIsRefused()
	{
		NaturalClassGearTable mage = NaturalClassGearTable.Mage;
		Assert.Contains("MAGE gear table names SWORD", Assert.Throws<InvalidDataException>(
			() => (mage with { WeaponGroups = ["SPELLBOOK", "SWORD"] }).Rules(Lines.Value)).Message, StringComparison.Ordinal);
		Assert.Contains("MAGE gear table names LEATHER", Assert.Throws<InvalidDataException>(
			() => (mage with { ArmorTypes = ["ROBE", "LEATHER"] }).Rules(Lines.Value)).Message, StringComparison.Ordinal);
		Assert.Throws<InvalidDataException>(() => (mage with { ArmorTypes = [] }).Rules(Lines.Value));
		Assert.Throws<InvalidDataException>(() => (mage with { WeaponGroups = ["SPELLBOOK", "SPELLBOOK"] }).Rules(Lines.Value));
		// The Priest and the Cleric keep the rules of CP-22: no table, no filter, nothing kept for later.
		foreach (NaturalGearRules rules in new[] { NaturalGearRules.Priest, NaturalGearRules.Cleric })
		{
			Assert.False(rules.IsTable);
			Assert.Null(rules.MasteryUnlocks);
			Assert.Null(rules.UpgradeScore);
			Assert.Empty(rules.ConsumableOrder);
			Assert.True(rules.Wears("PL_TORSO") && rules.Wears(null));
		}
	}

	/// <summary>No protected supply and no accessory is sold, and a sold item is one the class will not wear or one that
	/// another owned item of its slot beats or equals.</summary>
	private static void AssertNothingWearableOrProtectedIsSold(NaturalIshalgenInventoryPolicy policy, NaturalGearRules rules,
		List<BotInventoryItem> bag, int level)
	{
		NaturalInventoryPlan plan = policy.Decide(bag, level, 27, rules);
		foreach (NaturalInventoryDecision sale in plan.Sales)
		{
			NaturalItem sold = policy.Item(sale.ItemId);
			Assert.DoesNotContain(sale.ItemId, rules.Supplies);
			Assert.False(sold.IsAccessory, $"The {rules.Class} sells accessory {sale.ItemId}.");
			if (!rules.UsableNowOrLater(sold, level)) continue;
			Assert.Contains(bag, other => other.ObjectId != sale.ObjectId && policy.Item(other.ItemId) is { } kept &&
				rules.UsableNowOrLater(kept, level) && rules.Slot(kept) == rules.Slot(sold) && rules.Score(kept) >= rules.Score(sold));
		}
		Assert.All(bag.Where(item => rules.Supplies.Contains(item.ItemId)), supply =>
			Assert.Equal("hold", plan.Decisions.Single(decision => decision.ObjectId == supply.ObjectId).Action));
	}

	private static int[] Offered(int quest)
	{
		System.Xml.Linq.XElement data = QuestData.Value.Elements("quest").Single(node => (int)node.Attribute("id")! == quest);
		return data.Elements("rewards").First().Elements("selectable_reward_item").Select(node => (int)node.Attribute("item_id")!).ToArray();
	}

	private static readonly Lazy<System.Xml.Linq.XElement> QuestData = new(() => System.Xml.Linq.XDocument.Load(
		Path.Combine(RealStaticData.RepoRoot(), "game-server/data/static_data/quest_data/quest_data.xml")).Root!);

	private static BotInventoryItem Item(int objectId, int itemId, long count = 1, long equipped = 0) =>
		new(objectId, itemId, "item", count, 4, "", 0, false) { Details = new BotItemDetails(EquippedSlot: equipped) };
}
