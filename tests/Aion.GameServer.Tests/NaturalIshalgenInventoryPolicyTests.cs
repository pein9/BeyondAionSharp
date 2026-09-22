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
