using Aion.Bots.Scenarios;
using Aion.Bots.World;
using System.Xml;
using System.Xml.Linq;

namespace Aion.GameServer.Tests;

public sealed class NaturalIshalgenInventoryPolicyTests
{
	private static readonly Lazy<NaturalIshalgenInventoryPolicy> Catalog = new(() =>
	{
		string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
		return NaturalIshalgenInventoryPolicy.Load(root,
			[100100011, 110300292, 113300278, 110100009, 110500003, 152000451, 169300002,
			162000002, 162000007, 160000001, 164002116]);
	});

	// NA-09: what a new Cleric carries into Altgard: Aldelle Mace, Karmic Staff, the Ishalgen accessories, a Destiny Card,
	// the bridge's supplies, and ordinary loot.
	private static readonly Lazy<NaturalIshalgenInventoryPolicy> Bridge = new(() =>
	{
		string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
		return NaturalIshalgenInventoryPolicy.Load(root,
			[100100025, 101500498, 122000869, 121000749, 123000864, 182203009, 162000053, 169300003, 160002273,
			162001057, 169300002, 122001285, 122000039]);
	});

	[Fact]
	public void TheCeremonyRewardIsTheOperatorsStaffAndTheBridgeTurnInsHaveNoChoice()
	{
		// quest_data.xml Q2009 priest_selectable_reward: Karmic Warhammer, then Karmic Staff (OD-5).
		Assert.Equal(1, Bridge.Value.ChooseReward(2009, 9, []));
		Assert.All(new[] { 2008, 2904, 24010 }, quest => Assert.Equal(-1, Bridge.Value.ChooseReward(quest, 10, [])));
	}

	[Fact]
	public void TheClericKeepsTheStaffWornAccessoriesAndBridgeSuppliesAndSellsTheReplacedMace()
	{
		BotInventoryItem[] bag =
		[
			Item(1, 101500498, equipped: 1), Item(2, 100100025), Item(3, 122000869, equipped: 256), Item(4, 182203009, mask: 0),
			Item(5, 162000053, 12), Item(6, 169300003, 30), Item(7, 160002273, 5), Item(8, 162001057, 4, mask: 0),
			Item(9, 169300002, 20),
		];
		NaturalInventoryPlan plan = Bridge.Value.Decide(bag, 10, 27, cleric: true);
		string Reason(int obj) => plan.Decisions.Single(d => d.ObjectId == obj).Reason;
		Assert.Equal("currently-equipped", Reason(1));
		Assert.Equal("currently-equipped", Reason(3));
		Assert.Equal("quest-protected", Reason(4));
		Assert.All(new[] { 5, 6, 7 }, obj => Assert.Equal("combat-supply", Reason(obj)));
		// The replaced Aldelle Mace and ordinary loot go to the vendor; nothing protected does.
		Assert.Equal(new[] { 2, 9 }, plan.Sales.Select(d => d.ObjectId).ToArray());
		Assert.Equal("surplus-gear", Reason(2));
	}

	[Fact]
	public void TheClericSellsOutgrownAccessoriesButNotAQuestsRingOrOneForLater()
	{
		// AK-Q4 (b): with the upgrades worn, an accessory still in the cube is surplus (the Spirit Ring and Shania's Crystal
		// Ring wear at any level). Q2292's Passion Ring needs level 16, so at 12 it waits; at 19 an open Q2292 still needs it
		// (AK-08: the runner wore it as an upgrade and the claim would have failed).
		BotInventoryItem[] bag = [Item(1, 122000869), Item(2, 122000039), Item(3, 122001285)];
		NaturalInventoryPlan early = Bridge.Value.Decide(bag, 12, 27, cleric: true);
		string Reason(NaturalInventoryPlan plan, int obj) => plan.Decisions.Single(d => d.ObjectId == obj).Reason;
		Assert.Equal("surplus-accessory", Reason(early, 1));
		Assert.Equal("accessory-for-later", Reason(early, 2));
		Assert.Equal(new[] { 1, 3 }, early.Sales.Select(d => d.ObjectId).ToArray());
		NaturalInventoryPlan questing = Bridge.Value.Decide(bag, 19, 27, cleric: true, questNeeded: new HashSet<int> { 122000039 });
		Assert.Equal("quest-needed", Reason(questing, 2));
		Assert.Equal(new[] { 1, 3 }, questing.Sales.Select(d => d.ObjectId).ToArray());
	}

	[Fact]
	public void TheClericNeverSellsItsSuppliedHelpItems()
	{
		// NA-21: every approved help item, help scroll and potion tier is a combat supply, never shop fodder.
		int[] ids = NaturalHelpItemAllowlist.Approved.Select(supply => supply.ItemId)
			.Concat(NaturalHelpItemPolicy.All.Select(help => help.ItemId)).Distinct().ToArray();
		BotInventoryItem[] bag = ids.Select((id, index) => Item(index + 1, id, 10)).ToArray();
		NaturalInventoryPlan plan = Bridge.Value.Decide(bag, 10, 60, cleric: true);
		Assert.Empty(plan.Sales);
		Assert.All(plan.Decisions, decision => Assert.Equal("combat-supply", decision.Reason));
	}

	[Fact]
	public void AnUnwornStaffIsTheClericsUpgradeButNotThePriests()
	{
		BotInventoryItem[] bag = [Item(1, 100100025, equipped: 1), Item(2, 101500498)];
		Assert.Equal("best-usable-cleric-upgrade", Bridge.Value.Decide(bag, 10, 27, cleric: true).Decisions.Single(d => d.ObjectId == 2).Reason);
		// The frozen Priest rules are unchanged: a Priest has no staff mastery, so the staff is not its gear.
		Assert.Equal("unneeded-or-unusable", Bridge.Value.Decide(bag, 9, 27).Decisions.Single(d => d.ObjectId == 2).Reason);
		NaturalItem staff = Bridge.Value.Item(101500498), mace = Bridge.Value.Item(100100025);
		Assert.True(staff.UsableByClericAt(10));
		Assert.True(staff.ClericGearScore > mace.ClericGearScore);
		Assert.Equal("WEAPON", staff.ClericGearSlot);
		Assert.True(Bridge.Value.Item(122000869).IsAccessory);
	}

	[Fact]
	public void TheObservedClassSelectsTheClericRules()
	{
		var world = new BotWorldModel();
		Assert.False(NaturalIshalgenInventoryPolicy.IsCleric(world));
		// CP-23: an unobserved class is the line's starter, so the accepted line decides by the Priest's rules.
		Assert.Same(Aion.Bots.Scenarios.Classes.NaturalGearRules.Priest, Bridge.Value.GearRules(world));
		world.Apply(Packet<Aion.GameServer.Network.Aion.ServerPackets.SM_STATS_INFO>(("objectId", 7), ("level", (ushort)10),
			("expNeeded", 900L), ("expRecoverable", 0L), ("expShown", 500L), ("maxHp", 669), ("currentHp", 669), ("maxMp", 1200),
			("currentMp", 1200), ("maxDp", (ushort)4000), ("dp", (ushort)0), ("maxFp", 60), ("currentFp", 60)));
		world.Apply(Packet<Aion.GameServer.Network.Aion.ServerPackets.SM_PLAYER_INFO>(("objectId", 7), ("x", 0f), ("y", 0f), ("z", 0f),
			("heading", (byte)0), ("name", "Asimnjour"), ("state", (ushort)0), ("race", (byte)1),
			("playerClass", Aion.GameServer.Model.PlayerClassExtensions.GetClassId(Aion.GameServer.Model.PlayerClass.CLERIC))));
		Assert.True(NaturalIshalgenInventoryPolicy.IsCleric(world));
		// CP-23: Decide(world) takes the observed class's gear rules.
		Assert.Same(Aion.Bots.Scenarios.Classes.NaturalGearRules.Cleric, Bridge.Value.GearRules(world));
		BotInventoryItem[] bag = [Item(1, 100100025, equipped: 1), Item(2, 101500498)];
		Assert.Equal(Bridge.Value.Decide(bag, 10, 27, cleric: true).Decisions,
			Bridge.Value.Decide(bag, 10, 27, Bridge.Value.GearRules(world)).Decisions);
	}

	[Fact]
	public void TheRewardChoiceIsScoredByTheRulesItIsGivenAndByThePriestsByDefault()
	{
		// CP-23: the accepted line's profiles both name the Priest's rules, so every accepted pick stays as it was.
		Assert.Same(Aion.Bots.Scenarios.Classes.NaturalGearRules.Priest, Aion.Bots.Scenarios.Classes.NaturalPriestProfile.Priest.RewardGear);
		Assert.Same(Aion.Bots.Scenarios.Classes.NaturalGearRules.Priest, Aion.Bots.Scenarios.Classes.NaturalPriestProfile.Cleric.RewardGear);
		int differences = 0;
		foreach (int quest in Enumerable.Range(2000, 1000))
		for (int level = 1; level <= 26; level++)
		{
			int byDefault = Bridge.Value.ChooseReward(quest, level, []);
			Assert.Equal(byDefault, Bridge.Value.ChooseReward(quest, level, [], Aion.Bots.Scenarios.Classes.NaturalGearRules.Priest));
			if (Bridge.Value.ChooseReward(quest, level, [], Aion.Bots.Scenarios.Classes.NaturalGearRules.Cleric) != byDefault) differences++;
		}
		// Another class's rules do pick differently somewhere: the input is used.
		Assert.True(differences > 0);
		// The ceremony pick is a contract pin whatever the rules.
		Assert.Equal(Bridge.Value.ChooseReward(2009, 10, []), Bridge.Value.ChooseReward(2009, 10, [], Aion.Bots.Scenarios.Classes.NaturalGearRules.Cleric));
	}

	private static Aion.Bots.Protocol.DecodedBotServerPacket Packet<T>(params (string Name, object? Value)[] fields) =>
		new(typeof(T), fields.ToDictionary(field => field.Name, field => field.Value, StringComparer.Ordinal));

	private static BotInventoryItem Item(int obj, int id, long count = 1, ushort mask = 4, long equipped = 0) =>
		new(obj, id, "item", count, mask, "", 0, false)
		{ Details = new BotItemDetails(EquippedSlot: equipped) };

	[Fact]
	public void ProtectsQuestGatheringAndCombatSuppliesWhileSellingOtherItems()
	{
		BotInventoryItem[] bag =
		[
			Item(1, 152000451, 3), Item(2, 162000002, 100), Item(3, 162000007, 100),
			Item(4, 169300002, 20), Item(5, 160000001, 12), Item(6, 164002116, 50),
			Item(7, 110500003), Item(8, 110300292, equipped: 8),
		];
		NaturalInventoryPlan plan = Catalog.Value.Decide(bag, 1, 27);
		Assert.Equal(7, plan.Occupied);
		Assert.Equal(20, plan.FreeSlots);
		Assert.Equal("quest-protected", plan.Decisions.Single(d => d.ObjectId == 1).Reason);
		Assert.All(plan.Decisions.Where(d => d.ObjectId is 2 or 3), d => Assert.Equal("combat-supply", d.Reason));
		Assert.Equal(new[] { 4, 7 }, plan.Sales.Select(d => d.ObjectId).ToArray());
		Assert.All(plan.Decisions.Where(d => d.ObjectId is 5 or 6), d => Assert.Equal("not-sellable", d.Reason));
		Assert.Equal("currently-equipped", plan.Decisions.Single(d => d.ObjectId == 8).Reason);
	}

	[Fact]
	public void EquipsOnlyPriestUsableUpgradeAndSellsOutclassedGear()
	{
		BotInventoryItem[] bag =
		[
			Item(10, 100100011, equipped: 1), Item(11, 100100493), // level-3 mace reward
			Item(12, 110100009), Item(13, 110500003), // cloth versus plate
		];
		NaturalInventoryPlan plan = Catalog.Value.Decide(bag, 3, 27);
		Assert.Contains(plan.Equips, d => d.ObjectId == 11);
		Assert.Contains(plan.Equips, d => d.ObjectId == 12);
		Assert.Contains(plan.Sales, d => d.ObjectId == 13);
		Assert.DoesNotContain(plan.Sales, d => d.ObjectId == 10);
	}

	[Fact]
	public void RewardChoiceUsesUsableUpgradeThenVendorValueWithoutBuying()
	{
		Assert.Equal(2, Catalog.Value.ChooseReward(2002, 3, [Item(10, 100100011, equipped: 1)]));
		Assert.Equal(-1, Catalog.Value.ChooseReward(2133, 2, []));
		string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
		var included = NaturalIshalgenContract.LoadDefault().Quests.Select(quest => quest.Id).ToHashSet();
		var selectable = XDocument.Load(Path.Combine(root, "game-server/data/static_data/quest_data/quest_data.xml"))
			.Root!.Elements("quest").Where(quest => included.Contains((int)quest.Attribute("id")!))
			.Select(quest => (Id: (int)quest.Attribute("id")!, Count: quest.Elements("rewards").FirstOrDefault()?
				.Elements("selectable_reward_item").Count() ?? 0)).Where(pair => pair.Count > 0).ToArray();
		Assert.Equal(10, selectable.Length);
		Assert.All(selectable, pair => Assert.InRange(Catalog.Value.ChooseReward(pair.Id, 9, []), 0, pair.Count - 1));
	}

	[Fact]
	public void CubePressureRequiresThreeOpenSlotsAndVendorIsActiveInShippedData()
	{
		Assert.True(Catalog.Value.Decide(Enumerable.Range(1, 25).Select(i => Item(i, 169300002)), 1, 27).CubePressure);
		string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
		XElement spawns = XDocument.Load(Path.Combine(root,
			"game-server/data/static_data/spawns/Npcs/220010000_Ishalgen.xml")).Root!;
		Assert.Single(spawns.Descendants("spawn"), spawn => (int)spawn.Attribute("npc_id")! == 203526);
		using XmlReader reader = XmlReader.Create(Path.Combine(root,
			"game-server/data/static_data/npcs/npc_templates.xml"));
		while (reader.Read() && !(reader.NodeType == XmlNodeType.Element && reader.Name == "npc_template" &&
			reader.GetAttribute("npc_id") == "203526")) { }
		Assert.Equal("203526", reader.GetAttribute("npc_id"));
		using XmlReader subtree = reader.ReadSubtree();
		XElement vendor = XElement.Load(subtree);
		Assert.Equal("2 3", (string?)vendor.Element("talk_info")?.Attribute("func_dialogs"));
	}

	[Fact]
	public void NeverSellsUnknownOrUnsellableAndRequiresObservedAutoLearnedSkills()
	{
		NaturalInventoryPlan plan = Catalog.Value.Decide([Item(1, 999999999), Item(2, 169300002, mask: 0)], 1, 27);
		Assert.Empty(plan.Sales);
		Assert.Equal("unknown-static-item", plan.Decisions[0].Reason);
		Assert.Equal("not-sellable", plan.Decisions[1].Reason);
		var levelOne = new[] { 39, 40, 41, 103, 1838, 4012 }
			.ToDictionary(id => id, id => new BotSkill((ushort)id, 1, 0, 0, 0, 0));
		Assert.True(Catalog.Value.AutoLearnedPriestSkillsObserved(1, levelOne));
		Assert.False(Catalog.Value.AutoLearnedPriestSkillsObserved(3, levelOne));
	}
}
