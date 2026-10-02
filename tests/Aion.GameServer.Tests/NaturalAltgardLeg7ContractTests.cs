using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

public sealed class NaturalAltgardLeg7ContractTests
{
	private static readonly NaturalAltgardContract Leg = NaturalAltgardContract.LoadLeg("l7");
	private static readonly IReadOnlyDictionary<int, QuestRunPlan> Plans = NaturalAltgardContract.LoadPlans("l7");
	private static string Data(params string[] path) => Path.Combine([RealStaticData.RepoRoot(), "game-server", "data", "static_data", .. path]);
	private static XElement Spawns(int map) => XDocument.Load(Directory.GetFiles(Data("spawns", "Npcs"), $"{map}_*.xml").Single()).Root!;
	private static float[][] Spots(int npc, int map = 220030000) => Spawns(map).Descendants("spawn")
		.Where(node => (int?)node.Attribute("npc_id") == npc).Elements("spot")
		.Select(node => new[] { (float)node.Attribute("x")!, (float)node.Attribute("y")!, (float)node.Attribute("z")! }).ToArray();

	[Fact]
	public void StartMatchesLeg6AndTheEndpointKeepsTheApprovedHeldQuests()
	{
		NaturalAltgardContract previous = NaturalAltgardContract.LoadLeg("l6");
		Assert.Equal(previous.Start.CompletedQuestIds.Concat(previous.Endpoint.CompletedQuestIds).Order(), Leg.Start.CompletedQuestIds.Order());
		Assert.Equal(("natural-altgard-l6-contract.json", previous.Endpoint.Snapshot, previous.Endpoint.BindNpcId),
			(Leg.Start.Contract, Leg.Start.Snapshot, Leg.Start.BindNpcId));
		Assert.Equal(21, Leg.Start.Level);
		Assert.Equal(new[] { 2262, 24115, 24233, 2900, 24014, 24015 }.Order(), Leg.Start.StartedQuestIds!.Order());
		Assert.Equal([2146, 24115], Leg.Endpoint.HeldQuestIds!.Order());
		Assert.Equal(2146, Assert.Single(Leg.HeldList).QuestId);
		Assert.Equal(Leg.Order.Except([2146]).Order(), Leg.Endpoint.CompletedQuestIds.Order());
		Assert.Equal(13, Leg.Endpoint.CompletedQuestIds.Length);
		Assert.Equal((700065, "altgard-l7", 60f), (Leg.Endpoint.BindNpcId, Leg.Endpoint.Snapshot, Leg.Endpoint.Radius));
		Assert.Equal(Assert.Single(Spots(700065)), Leg.Bind!.Position);
		XElement bind = XDocument.Load(Data("bind_points", "bind_points.xml")).Descendants("bind_point")
			.Single(node => (int?)node.Attribute("npcid") == 700065);
		Assert.Equal((int)bind.Attribute("price")!, Leg.Bind.Price);
		Assert.Equal(203579, Leg.Town!.VendorNpcId);
		XElement merchant = XDocument.Load(Data("npcs", "npc_templates.xml")).Descendants("npc_template")
			.Single(node => (int?)node.Attribute("npc_id") == Leg.Town.VendorNpcId);
		Assert.Contains("3", ((string)merchant.Element("talk_info")!.Attribute("func_dialogs")!).Split(' '));
	}

	[Fact]
	public void QuestsAndTemplatePlansMatchTheShippedDataAndTheirGrounds()
	{
		XElement data = XDocument.Load(Data("quest_data", "quest_data.xml")).Root!;
		foreach (NaturalAltgardQuest expected in Leg.Quests)
		{
			XElement quest = data.Elements("quest").Single(node => (int?)node.Attribute("id") == expected.Id);
			Assert.Equal((expected.Category, expected.MinimumLevel, expected.RewardExperience),
				((string?)quest.Attribute("category"), (int)quest.Attribute("minlevel_permitted")!, (int)quest.Element("rewards")!.Attribute("exp")!));
			Assert.Equal(expected.Prerequisite is int before ? [before] : [],
				quest.Elements("start_conditions").Elements("finished").Select(node => (int)node.Attribute("quest_id")!).ToArray());
			if (expected.Prerequisite is int prerequisite)
				Assert.True(Leg.Start.CompletedQuestIds.Contains(prerequisite) || Array.IndexOf(Leg.Order, prerequisite) < Array.IndexOf(Leg.Order, expected.Id));
			if (!expected.IsTemplate) continue;
			QuestRunPlan plan = Plans[expected.Id];
			Assert.Equal((expected.Template, expected.StartNpcId), (plan.Template, Assert.Single(plan.StartNpcs).Id));
			NaturalAltgardArea area = Leg.Area(expected.Area!);
			if (plan.Template == "report_to" || expected.Id == 24233)
				Assert.Contains(Spots(Assert.Single(plan.EndNpcs).Id), at => area.Contains(at[0], at[1], at[2]));
			else
				foreach (int npc in plan.Steps.Where(step => step.Kind is "kill" or "collect")
					.SelectMany(step => step.Npcs.Select(npc => npc.Id).Concat(step.Sources.Select(source => source.NpcId).OfType<int>())).Distinct())
					Assert.Contains(Spots(npc), at => area.Contains(at[0], at[1], at[2]));
		}
		Assert.Equal(Leg.Quests.Where(quest => quest.IsTemplate).Select(quest => quest.Id).Order(), Plans.Keys.Order());
	}

	[Fact]
	public void ScriptsTripTimerCollectionAndSpiritPoolMatchTheHandlersAndSpawns()
	{
		foreach (NaturalAltgardStep step in Leg.Steps)
		{
			string path = Directory.GetFiles(Path.Combine(RealStaticData.RepoRoot(), "src", "Aion.GameServer", "Handlers", "Quest", "altgard"), $"_{step.QuestId}*.cs").Single();
			string source = File.ReadAllText(path);
			Assert.Contains($"{step.NpcId}", source);
			Assert.Contains(Spots(step.NpcId, Leg.StepMap(step)), at => at.SequenceEqual(step.Position));
			foreach (int page in step.Pages.Where(page => page is not (5 or 10))) Assert.Contains($"SendQuestDialog(env, {page})", source);
			foreach (string action in step.Actions.Where(action => action.StartsWith("SETPRO", StringComparison.Ordinal))) Assert.Contains($"DialogAction.{action}", source);
		}
		NaturalAltgardMapTrip trip = Assert.Single(Leg.MapTripList);
		Assert.Equal((120010000, 203581, 7, 500), (trip.MapId, trip.TeleporterNpcId, trip.LocationId, trip.Fare));
		XElement location = XDocument.Load(Data("npc_teleporter.xml")).Descendants("teleporter_template")
			.Single(node => (string?)node.Attribute("npc_ids") == $"{trip.TeleporterNpcId}").Descendants("telelocation")
			.Single(node => (int?)node.Attribute("loc_id") == trip.LocationId);
		Assert.Equal(trip.Fare, (int)location.Attribute("price")!);
		Assert.Equal([204206, 204075], Leg.Steps.Where(step => Leg.StepMap(step) == trip.MapId).Select(step => step.NpcId));
		NaturalAltgardTimer timer = Assert.Single(Leg.TimerList);
		Assert.Equal((2263, 300, "abandon", "abandon"), (timer.QuestId, timer.Seconds, timer.OnExpiry, timer.OnLogout));
		Assert.Equal("OFFER", Leg.Steps.Single(step => step.Key == timer.StartStep).ExpectedStatus);
		NaturalAltgardCollectedItem pollen = Assert.Single(Assert.Single(Leg.CollectionList).Items);
		Assert.Equal((182203242, 3), (pollen.ItemId, pollen.Count));
		Assert.Equal([210444, 210500], pollen.SourceNpcIds);
		XElement pool = Spawns(220030000).Descendants("spawn").Single(node => (int?)node.Attribute("npc_id") == 203682);
		Assert.Equal(1, (int)pool.Attribute("pool")!);
		Assert.Equal(2, pool.Elements("spot").Count());
		Assert.All(Spots(203682), at => Assert.True(Leg.Area("zemurru").Contains(at[0], at[1], at[2])));
	}
}
