using System.Xml.Linq;
using Aion.Bots.Scenarios;

namespace Aion.GameServer.Tests;

/// <summary>AK-01: the Altgard Leg 5 (Kaibech's Campsite, Idun's Lake and MuMu Village) contract against shipped data and its
/// compiled plans.</summary>
public sealed class NaturalAltgardLeg5ContractTests
{
	private static readonly NaturalAltgardContract Leg5 = NaturalAltgardContract.LoadLeg("l5");
	private static readonly IReadOnlyDictionary<int, QuestRunPlan> Plans = NaturalAltgardContract.LoadPlans("l5");

	[Fact]
	public void Leg5StartsWhereLeg4EndsAndPinsQuestDataTheBindAndTheEndpoint()
	{
		NaturalAltgardContract leg4 = NaturalAltgardContract.LoadLeg("l4");
		Assert.Equal(leg4.JavaReference, Leg5.JavaReference);
		Assert.Equal((Leg5.Start.Contract, Leg5.Start.Snapshot), ("natural-altgard-l4-contract.json", leg4.Endpoint.Snapshot));
		Assert.Equal(leg4.Start.CompletedQuestIds.Concat(leg4.Endpoint.CompletedQuestIds).Order(), Leg5.Start.CompletedQuestIds.Order());
		Assert.Equal((leg4.Start.MapId, leg4.Start.Race, leg4.Start.Class), (Leg5.Start.MapId, Leg5.Start.Race, Leg5.Start.Class));
		// Leg 4 ends bound at Basfelt (AB-Q5); Leg 5 starts there and stays bound there: neither hub has an obelisk.
		Assert.Equal(leg4.Endpoint.BindNpcId, Leg5.Start.BindNpcId);
		Assert.Equal((leg4.Bind!.NpcId, leg4.Bind.Price, leg4.Bind.AcceptRange), (Leg5.Bind!.NpcId, Leg5.Bind.Price, Leg5.Bind.AcceptRange));
		Assert.Equal(leg4.Bind.Position, Leg5.Bind.Position);
		Assert.Equal(Leg5.Bind!.NpcId, Leg5.Endpoint.BindNpcId);
		Assert.True(Leg5.Start.Level >= leg4.Endpoint.MinimumLevel);
		Assert.Equal((leg4.Hub.Key, leg4.Hub.MapId, leg4.Hub.Radius), (Leg5.Hub.Key, Leg5.Hub.MapId, Leg5.Hub.Radius));
		Assert.Equal(leg4.Hub.Anchor, Leg5.Hub.Anchor);
		Assert.Empty(Leg5.Start.StartedQuestIds ?? []);

		XElement questData = XDocument.Load(Data("quest_data", "quest_data.xml")).Root!;
		XElement scripts = XDocument.Load(Data("quest_script_data", "altgard.xml")).Root!;
		Assert.Equal(13, Leg5.Quests.Length);
		foreach (NaturalAltgardQuest expected in Leg5.Quests)
		{
			XElement quest = Quest(questData, expected.Id);
			Assert.Equal(expected.Category, (string?)quest.Attribute("category"));
			Assert.Equal(expected.MinimumLevel, (int?)quest.Attribute("minlevel_permitted"));
			Assert.Equal(Leg5.Start.Race, (string?)quest.Attribute("race_permitted"));
			Assert.All(quest.Elements("rewards"), reward => Assert.Equal(expected.RewardExperience, (int?)reward.Attribute("exp")));
			int[] finished = quest.Elements("start_conditions").Elements("finished").Select(node => (int)node.Attribute("quest_id")!).ToArray();
			Assert.Equal(expected.Prerequisite is { } prerequisite ? [prerequisite] : [], finished);
			Assert.True(expected.MinimumLevel <= Leg5.Start.Level);
			// Every Leg 5 quest is a template quest (Java has no custom handler); its start NPC is the template's.
			Assert.True(expected.IsTemplate);
			XElement script = Assert.Single(scripts.Elements(expected.Template!), node => (int?)node.Attribute("id") == expected.Id);
			Assert.Equal(expected.StartNpcId, (int?)script.Attribute("start_npc_ids"));
			QuestRunPlan plan = Plans[expected.Id];
			Assert.Equal((expected.Template, expected.StartNpcId), (plan.Template, Assert.Single(plan.StartNpcs).Id));
			// A prerequisite is either in this leg, before the quest, or done before the leg (Q2231, Q24112).
			if (expected.Prerequisite is int before)
				Assert.True(Array.IndexOf(Leg5.Order, before) is int at && at >= 0 ? at < Array.IndexOf(Leg5.Order, expected.Id)
					: Leg5.Start.CompletedQuestIds.Contains(before), $"Q{expected.Id} before Q{before}");
		}
		Assert.Equal(Leg5.Quests.Select(quest => quest.Id).Order(), Plans.Keys.Order());

		// AK-Q2 (a): Q2242 and Q24233 are finished but held for Gemyu (Gerger) and Suthran (the fortress).
		Assert.Equal([2242, 24233], Leg5.HeldList.Select(held => held.QuestId).Order());
		foreach (NaturalAltgardHeld held in Leg5.HeldList)
		{
			Assert.Equal(held.EndNpcId, Assert.Single(Plans[held.QuestId].EndNpcs).Id);
			Assert.NotEqual(Leg5.Quest(held.QuestId).StartNpcId, held.EndNpcId);
		}
		Assert.Equal(Leg5.Order.Except([2242, 24233]).Order(), Leg5.Endpoint.CompletedQuestIds.Order());
		Assert.Equal([2242, 24233], Leg5.Endpoint.HeldQuestIds!.Order());
		Assert.True(Leg5.Endpoint.InHub);
		Assert.Equal("altgard-l5", Leg5.Endpoint.Snapshot);
	}

	[Fact]
	public void EveryQuestsTargetsAndGiversLieInItsAreas()
	{
		foreach (NaturalAltgardQuest quest in Leg5.Quests)
		{
			NaturalAltgardArea area = Leg5.Area(quest.Area!);
			int[] targets = Plans[quest.Id].Steps.SelectMany(step => step.Npcs.Select(npc => npc.Id)
				.Concat(step.Sources.Select(source => source.NpcId).OfType<int>())).Distinct().ToArray();
			// Every kill or collection target with an Altgard spawn has one inside the quest's area (a delivery's area is where it
			// ends). Q24230's and Q24231's L15 variants (210505, 210507) have no spawn in 4.8 at all.
			int[] spawned = targets.Where(id => !Plans[quest.Id].EndNpcs.Any(npc => npc.Id == id) && Spots(id).Any()).ToArray();
			Assert.True(spawned.Length > 0 || Plans[quest.Id].Template == "report_to", $"Q{quest.Id} has no spawned target");
			foreach (int target in spawned)
				Assert.Contains(Spots(target), spot => area.Contains(spot[0], spot[1], spot[2]));
			if (Leg5.HeldList.SingleOrDefault(held => held.QuestId == quest.Id) is { } kept)
				Assert.True(kept.QuestId == 2242 ? Leg5.Area("gerger").Contains(Spots(kept.EndNpcId).First()[0], Spots(kept.EndNpcId).First()[1], Spots(kept.EndNpcId).First()[2])
					: Leg5.Area("fortress-suthran").Contains(Spots(kept.EndNpcId).First()[0], Spots(kept.EndNpcId).First()[1], Spots(kept.EndNpcId).First()[2]));
		}
		// Brodir and Anmurnerk give every Stop 6 quest; Kaibech and Mantigar the Stop 5 ones; Vovetirn the east ones.
		foreach ((int npc, string key) in new[] { (832821, "idun-brodir"), (832822, "idun-brodir"), (203610, "kaibech-grounds"),
			(203611, "kaibech-grounds"), (203630, "vovetirn-outlaws") })
			Assert.Contains(Spots(npc), spot => Leg5.Area(key).Contains(spot[0], spot[1], spot[2]));
	}

	[Fact]
	public void TheRingCarriersFollowTheirShippedHoursAndDrops()
	{
		XElement questData = XDocument.Load(Data("quest_data", "quest_data.xml")).Root!;
		XElement ringQuest = Quest(questData, 2292);
		Assert.Equal(6, Leg5.TimedSpawnList.Length);
		foreach (NaturalAltgardTimedSpawn carrier in Leg5.TimedSpawnList)
		{
			XElement spawn = Assert.Single(Spawns().Descendants("spawn"), node => (int?)node.Attribute("npc_id") == carrier.NpcId);
			XElement hours = spawn.Element("temporary_spawn")!;
			Assert.Equal(carrier.SpawnHour, int.Parse(((string)hours.Attribute("spawn_time")!).Split('.')[0], System.Globalization.CultureInfo.InvariantCulture));
			Assert.Equal(carrier.DespawnHour, int.Parse(((string)hours.Attribute("despawn_time")!).Split('.')[0], System.Globalization.CultureInfo.InvariantCulture));
			Assert.Equal(carrier.RespawnSeconds, (int)spawn.Attribute("respawn_time")!);
			float[] spot = Assert.Single(spawn.Elements("spot")).Let(Spot);
			Assert.Equal(spot, carrier.Position);
			Assert.True(Leg5.Area(carrier.Area).Contains(spot[0], spot[1], spot[2]));
			Assert.Contains(ringQuest.Elements("quest_drop"), drop => (int?)drop.Attribute("npc_id") == carrier.NpcId &&
				(int?)drop.Attribute("item_id") == carrier.ItemId);
		}
		// Each ring has two carriers; the Love Ring's are both night carriers, so it needs the game's night (AK-Q3).
		int[] rings = ringQuest.Descendants("collect_item").Select(item => (int)item.Attribute("item_id")!).ToArray();
		Assert.All(rings, ring => Assert.Equal(2, Leg5.TimedSpawnList.Count(carrier => carrier.ItemId == ring)));
		Assert.All(Leg5.TimedSpawnList.Where(carrier => carrier.ItemId == 122000041), carrier =>
		{
			Assert.True(carrier.PresentAt(23) && carrier.PresentAt(2));
			Assert.False(carrier.PresentAt(12));
		});
		Assert.Contains(Leg5.TimedSpawnList, carrier => carrier.ItemId == 122000039 && carrier.PresentAt(12));
		// Q2292's choice: the second earring (Turquoise, magic boost) for the Cleric.
		NaturalAltgardRewardChoice choice = Assert.Single(Leg5.RewardChoiceList);
		Assert.Equal((2292, "SELECTED_QUEST_REWARD2", 120001521), (choice.QuestId, choice.Action, choice.ItemId));
		Assert.Equal(choice.ItemId, (int)ringQuest.Descendants("selectable_reward_item").ElementAt(1).Attribute("item_id")!);
	}

	private static float[] Spot(XElement spot) => [(float)spot.Attribute("x")!, (float)spot.Attribute("y")!, (float)spot.Attribute("z")!];

	private static IEnumerable<float[]> Spots(int npcId) =>
		Spawns().Descendants("spawn").Where(spawn => (int?)spawn.Attribute("npc_id") == npcId).Elements("spot").Select(Spot);

	private static XElement Spawns() => XDocument.Load(Data("spawns", "Npcs/220030000_Altgard.xml")).Root!;
	private static XElement Quest(XElement root, int id) => Assert.Single(root.Elements("quest"), quest => (int?)quest.Attribute("id") == id);
	private static string Data(params string[] parts) => Path.Combine([Path.Combine(Root(), "game-server/data/static_data"), .. parts]);
	private static string Root() => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
}

internal static class Leg5ContractTestExtensions
{
	public static TResult Let<T, TResult>(this T value, Func<T, TResult> map) => map(value);
}
