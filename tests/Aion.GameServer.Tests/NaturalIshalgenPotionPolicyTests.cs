using System.Xml;
using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

public sealed class NaturalIshalgenPotionPolicyTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
	private static BotInventoryItem Item(int objectId, int templateId, long count) =>
		new(objectId, templateId, "potion", count, 4, "", 0, false);

	[Fact]
	public void UsesTimedHealingAtEightyPercentBeforeSeventyPercentSelfHeal()
	{
		var learned = new Dictionary<int, BotSkill> { [1838] = new(1838, 1, 0, 0, 0, 0) };
		var state = new NaturalCombatObservation(3, 80, 100, 100, 100, false, true, 10, 71,
			learned, new Dictionary<int, DateTimeOffset>(), HasHotPotion: true, HotPotionReady: true);
		Assert.Equal("hot-potion", NaturalPriestCombatPolicy.Decide(state, Now).Action);
		Assert.Equal("cast-self", NaturalPriestCombatPolicy.Decide(state with { Hp = 70, HotPotionReady = false }, Now).Action);
		Assert.Equal("cast-self", NaturalPriestCombatPolicy.Decide(state with { Hp = 70, HotPotionActive = true }, Now).Action);
		Assert.Equal("retreat", NaturalPriestCombatPolicy.Decide(state with { Hp = 30 }, Now).Action);
		Assert.NotEqual("hot-potion", NaturalPriestCombatPolicy.Decide(state with { Hp = 81 }, Now).Action);
		Assert.NotEqual("hot-potion", NaturalPriestCombatPolicy.Decide(state with
		{
			Aggro = false, TargetObjectId = null, TargetDistance = null,
		}, Now).Action);
	}

	[Fact]
	public void SelectsObservedStarterFirstAndRestocksVendorReserveOnlyWhenLowAndAffordable()
	{
		BotInventoryItem[] bag = [Item(3, NaturalIshalgenPotionPolicy.VendorLifeElixirId, 1),
			Item(2, NaturalIshalgenPotionPolicy.StarterLifePotionId, 100)];
		Assert.Equal(2, NaturalIshalgenPotionPolicy.SelectOwnedPotion(bag)?.ObjectId);
		Assert.Equal(3, NaturalIshalgenPotionPolicy.SelectOwnedPotion(
			[Item(2, NaturalIshalgenPotionPolicy.StarterLifePotionId, 0), bag[0]])?.ObjectId);
		Assert.False(NaturalIshalgenPotionPolicy.NeedsRestock(bag));
		Assert.True(NaturalIshalgenPotionPolicy.NeedsRestock(
			[Item(2, NaturalIshalgenPotionPolicy.StarterLifePotionId, 4), bag[0]]));
		Assert.Equal(2, NaturalIshalgenPotionPolicy.AffordablePurchaseCount(1, 1000, 500));
		Assert.Equal(0, NaturalIshalgenPotionPolicy.AffordablePurchaseCount(6, 1000, 500));
		Assert.Equal(0, NaturalIshalgenPotionPolicy.AffordablePurchaseCount(0, 499, 500));
		Assert.Equal(1, NaturalIshalgenPotionPolicy.AffordablePurchaseCount(0, 500, 500));
		Assert.Equal(12, NaturalIshalgenPotionPolicy.AffordablePurchaseCount(0, 10000, 500));
		Assert.True(NaturalIshalgenPotionPolicy.HasActiveHealing(
			[new BotVisibleEffect(1, 9889, 1, 1, 1000)]));
		Assert.True(NaturalIshalgenPotionPolicy.HasActiveHealing(
			[new BotVisibleEffect(1, 10202, 1, 1, 1000)]));
		Assert.False(NaturalIshalgenPotionPolicy.HasActiveHealing(null));
	}

	[Fact]
	public void PurchasedElixirIsProtectedFromJunkSales()
	{
		string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
		var inventory = NaturalIshalgenInventoryPolicy.Load(root,
			[NaturalIshalgenPotionPolicy.VendorLifeElixirId]);
		NaturalInventoryPlan plan = inventory.Decide(
			[Item(7, NaturalIshalgenPotionPolicy.VendorLifeElixirId, 3)], 3, 27);
		Assert.Equal("combat-supply", Assert.Single(plan.Decisions).Reason);
		Assert.Empty(plan.Sales);
	}

	[Fact]
	public void BothActiveIshalgenVendorsSellTheShippedTimedHealingElixir()
	{
		string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
		XElement goods = XDocument.Load(Path.Combine(root,
			"game-server/data/static_data/goodslists/goodslists.xml")).Root!;
		XElement list = Assert.Single(goods.Elements("list"), node => (int)node.Attribute("id")! == 721);
		Assert.Contains(list.Elements("item"), item => (int)item.Attribute("id")! ==
			NaturalIshalgenPotionPolicy.VendorLifeElixirId);
		XElement trades = XDocument.Load(Path.Combine(root,
			"game-server/data/static_data/npc_trade_list.xml")).Root!;
		XElement spawns = XDocument.Load(Path.Combine(root,
			"game-server/data/static_data/spawns/Npcs/220010000_Ishalgen.xml")).Root!;
		foreach (var vendor in NaturalIshalgenPotionPolicy.Vendors)
		{
			XElement trade = Assert.Single(trades.Elements("tradelist_template"),
				node => (int)node.Attribute("npc_id")! == vendor.NpcId);
			Assert.Contains(trade.Elements("tradelist"), node => (int)node.Attribute("id")! == 721);
			XElement spawn = Assert.Single(spawns.Descendants("spawn"),
				node => (int)node.Attribute("npc_id")! == vendor.NpcId);
			XElement spot = Assert.Single(spawn.Elements("spot"));
			Assert.Equal(vendor.Position.X, (float)spot.Attribute("x")!);
			Assert.Equal(vendor.Position.Y, (float)spot.Attribute("y")!);
		}
		using XmlReader reader = XmlReader.Create(Path.Combine(root,
			"game-server/data/static_data/items/item_templates.xml"));
		while (reader.Read() && !(reader.NodeType == XmlNodeType.Element && reader.Name == "item_template" &&
			reader.GetAttribute("id") == NaturalIshalgenPotionPolicy.VendorLifeElixirId.ToString())) { }
		Assert.Equal("Minor Life Elixir", reader.GetAttribute("name"));
		using XmlReader subtree = reader.ReadSubtree();
		XElement template = XElement.Load(subtree);
		Assert.Equal(10202, (int)template.Element("actions")!.Element("skilluse")!.Attribute("skillid")!);
		Assert.Equal(60000, (int)template.Element("uselimits")!.Attribute("usedelay")!);
		Assert.Equal(NaturalIshalgenPotionPolicy.SharedUseDelayId,
			(int)template.Element("uselimits")!.Attribute("usedelayid")!);
		using XmlReader skills = XmlReader.Create(Path.Combine(root,
			"game-server/data/static_data/skills/skill_templates.xml"));
		while (skills.Read() && !(skills.NodeType == XmlNodeType.Element && skills.Name == "skill_template" &&
			skills.GetAttribute("skill_id") == "10202")) { }
		using XmlReader skillSubtree = skills.ReadSubtree();
		XElement healing = XElement.Load(skillSubtree);
		Assert.Equal(20000, (int)healing.Element("effects")!.Element("heal")!.Attribute("duration2")!);
		Assert.Equal(2000, (int)healing.Element("effects")!.Element("heal")!.Attribute("checktime")!);
	}
}
