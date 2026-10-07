using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

/// <summary>
/// CP-07: the shipped facts the Ishalgen hub binds rest on: the two obelisks, their fees, the Soul Healer the death rule
/// finds beside each, and which quest hubs they serve.
/// </summary>
public sealed class NaturalIshalgenHubBindTests
{
	private const int VillageObelisk = 700063, OutpostObelisk = 700064, Linevir = 203512, Rusalka = 203680;

	[Theory]
	[InlineData(VillageObelisk, 43)]
	[InlineData(OutpostObelisk, 134)]
	public void TheBindFeeIsTheBindPointsShippedPrice(int obelisk, int fee)
	{
		XElement point = Assert.Single(XDocument.Load(Data("bind_points", "bind_points.xml")).Root!.Elements("bind_point"),
			node => (int?)node.Attribute("npcid") == obelisk);
		Assert.Equal(fee, (int?)point.Attribute("price"));
	}

	[Theory]
	[InlineData(VillageObelisk, Linevir)]
	[InlineData(OutpostObelisk, Rusalka)]
	public void TheDeathRuleFindsTheSoulHealerBesideEachObelisk(int obelisk, int healer)
	{
		// The death rule knows a Soul Healer by its title and takes the nearest within its search radius of the revive point.
		XElement templates = XDocument.Load(Data("npcs", "npc_templates.xml")).Root!;
		int[] soulHealers = templates.Elements("npc_template")
			.Where(node => (int?)node.Attribute("title_id") == NaturalServicePolicy.SoulHealerTitleId)
			.Select(node => (int)node.Attribute("npc_id")!).ToArray();
		Dictionary<int, BotPosition> spawns = Spawns();
		(int NpcId, BotPosition Position)[] onMap = soulHealers.Where(spawns.ContainsKey).Select(id => (id, spawns[id])).ToArray();
		Assert.Equal(new[] { Linevir, Rusalka }, onMap.Select(entry => entry.NpcId).Order());
		(int NpcId, BotPosition Position)? found = NaturalServicePolicy.NearestSoulHealer(onMap, spawns[obelisk]);
		Assert.Equal(healer, found?.NpcId);
		Assert.True(NaturalServicePolicy.Distance(found!.Value.Position, spawns[obelisk]) < 5);
		// With no bind a revive lands at the map's first spawn point, where the rule finds no Soul Healer.
		Assert.Null(NaturalServicePolicy.NearestSoulHealer(onMap, new BotPosition(571.0388f, 2787.342f, 299.875f, 0)));
	}

	[Fact]
	public void TheVillageHubBindsAtTheVillageAndTheOutpostsTwoHubsAtTheOutpost()
	{
		Dictionary<int, BotPosition> spawns = Spawns();
		NaturalIshalgenHubPolicy.Hub Hub(string name) => Assert.Single(NaturalIshalgenHubPolicy.Hubs, hub => hub.Name == name);
		// The obelisks stand in the hubs they serve; the outpost one is the only obelisk near mijou and anturoon.
		Assert.True(NaturalServicePolicy.Distance(spawns[VillageObelisk], Hub("aldelle").Center) < 40);
		Assert.True(NaturalServicePolicy.Distance(spawns[OutpostObelisk], Hub("mijou").Center) < 10);
		Assert.True(NaturalServicePolicy.Distance(spawns[OutpostObelisk], Hub("anturoon").Center) < 150);
		// The campaign's first village quest and its first outpost quest are where each bind is first asked for.
		Assert.Equal("aldelle", NaturalIshalgenHubPolicy.ForQuest(2100)?.Name);
		Assert.Equal("mijou", NaturalIshalgenHubPolicy.ForQuest(2003)?.Name);
		Assert.Equal("anturoon", NaturalIshalgenHubPolicy.ForQuest(2005)?.Name);
		// The two obelisks are far more than the 20 m inside which Java refuses a second bind.
		Assert.True(NaturalServicePolicy.Distance(spawns[VillageObelisk], spawns[OutpostObelisk]) > 800);
	}

	private static Dictionary<int, BotPosition> Spawns() =>
		XDocument.Load(Data("spawns", "Npcs", "220010000_Ishalgen.xml")).Root!.Descendants("spawn")
			.Where(spawn => spawn.Element("spot") != null)
			.GroupBy(spawn => (int)spawn.Attribute("npc_id")!)
			.ToDictionary(group => group.Key, group =>
			{
				XElement spot = group.First().Element("spot")!;
				return new BotPosition((float)spot.Attribute("x")!, (float)spot.Attribute("y")!, (float)spot.Attribute("z")!, 0);
			});

	private static string Data(params string[] parts) => Path.Combine([Path.GetFullPath(Path.Combine(
		Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../..")), "game-server/data/static_data", .. parts]);
}
