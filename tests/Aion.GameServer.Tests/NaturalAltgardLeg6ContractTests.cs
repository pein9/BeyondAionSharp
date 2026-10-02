using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

/// <summary>AG-01: the Altgard Leg 6 (Gerger Village and Trader's Berth) contract against shipped data, its compiled plans and the
/// custom handlers its three scripted quests run on.</summary>
public sealed class NaturalAltgardLeg6ContractTests
{
	private static readonly NaturalAltgardContract Leg6 = NaturalAltgardContract.LoadLeg("l6");
	private static readonly IReadOnlyDictionary<int, QuestRunPlan> Plans = NaturalAltgardContract.LoadPlans("l6");
	private static readonly int[] Scripted = [2247, 2284, 2252];

	[Fact]
	public void Leg6StartsWhereLeg5EndsAndPinsQuestDataTheBindAndTheEndpoint()
	{
		NaturalAltgardContract leg5 = NaturalAltgardContract.LoadLeg("l5");
		Assert.Equal(leg5.JavaReference, Leg6.JavaReference);
		Assert.Equal((Leg6.Start.Contract, Leg6.Start.Snapshot), ("natural-altgard-l5-contract.json", leg5.Endpoint.Snapshot));
		Assert.Equal(leg5.Start.CompletedQuestIds.Concat(leg5.Endpoint.CompletedQuestIds).Order(), Leg6.Start.CompletedQuestIds.Order());
		Assert.Equal((leg5.Start.MapId, leg5.Start.Race, leg5.Start.Class), (Leg6.Start.MapId, Leg6.Start.Race, Leg6.Start.Class));
		Assert.Equal(leg5.Endpoint.BindNpcId, Leg6.Start.BindNpcId);
		Assert.True(Leg6.Start.Level >= leg5.Endpoint.MinimumLevel);
		// Leg 5's held hand-ins are still started; Q2242's is this leg's (Gemyu, Gerger), Q24233's a later one's (Suthran).
		Assert.Subset((Leg6.Start.StartedQuestIds ?? []).ToHashSet(), leg5.Endpoint.HeldQuestIds!.ToHashSet());

		// AG-Q1 (a): the leg works out of Trader's Berth and binds at its obelisk (700821) for its shipped price.
		XElement bindPoints = XDocument.Load(Data("bind_points", "bind_points.xml")).Root!;
		NaturalAltgardBind bind = Leg6.Bind!;
		Assert.True(bind.OnArrival);
		Assert.Equal(bind.Price, (long)bindPoints.Elements("bind_point").Single(point => (int)point.Attribute("npcid")! == bind.NpcId).Attribute("price")!);
		Assert.Equal(bind.Position, Assert.Single(Spots(bind.NpcId)));
		Assert.Equal(bind.Position, Leg6.Hub.Anchor);
		Assert.Equal((bind.NpcId, "altgard-l6"), (Leg6.Endpoint.BindNpcId, Leg6.Endpoint.Snapshot));
		// The town's merchant (the cube service) stands by the obelisk.
		Assert.Equal(798030, Leg6.Town!.VendorNpcId);
		Assert.Contains(Spots(798030), spot => Leg6.Area("berth").Contains(spot[0], spot[1], spot[2]));

		XElement questData = XDocument.Load(Data("quest_data", "quest_data.xml")).Root!;
		XElement scripts = XDocument.Load(Data("quest_script_data", "altgard.xml")).Root!;
		Assert.Equal(12, Leg6.Quests.Length);
		foreach (NaturalAltgardQuest expected in Leg6.Quests)
		{
			XElement quest = Quest(questData, expected.Id);
			Assert.Equal(expected.Category, (string?)quest.Attribute("category"));
			Assert.Equal(expected.MinimumLevel, (int?)quest.Attribute("minlevel_permitted"));
			Assert.Equal(Leg6.Start.Race, (string?)quest.Attribute("race_permitted"));
			// Q2252 has two reward groups (the spirit's and the drakie's); the contract carries the first.
			Assert.Equal(expected.RewardExperience, (int?)quest.Elements("rewards").First().Attribute("exp"));
			int[] finished = quest.Elements("start_conditions").Elements("finished").Select(node => (int)node.Attribute("quest_id")!).ToArray();
			Assert.Equal(expected.Prerequisite is { } prerequisite ? [prerequisite] : [], finished);
			Assert.True(expected.MinimumLevel <= Leg6.Start.Level);
			if (expected.Prerequisite is int before)
				Assert.True(Array.IndexOf(Leg6.Order, before) is int at && at >= 0 ? at < Array.IndexOf(Leg6.Order, expected.Id)
					: Leg6.Start.CompletedQuestIds.Contains(before), $"Q{expected.Id} before Q{before}");
			if (Scripted.Contains(expected.Id))
			{
				Assert.False(expected.IsTemplate);
				Assert.True(File.Exists(Handler(expected.Id)), expected.Handler);
				continue;
			}
			Assert.True(expected.IsTemplate);
			XElement script = Assert.Single(scripts.Elements(expected.Template!), node => (int?)node.Attribute("id") == expected.Id);
			Assert.Equal(expected.StartNpcId, (int?)script.Attribute("start_npc_ids"));
			QuestRunPlan plan = Plans[expected.Id];
			Assert.Equal((expected.Template, expected.StartNpcId), (plan.Template, Assert.Single(plan.StartNpcs).Id));
		}
		Assert.Equal(Leg6.Quests.Where(quest => quest.IsTemplate).Select(quest => quest.Id).Order(), Plans.Keys.Order());

		// AG-Q2 (a): Q24115 (Banatisai, the Heart of Impetusium) and Q2262 (Mabrunerk, the East Gate) are taken and held.
		Assert.Equal([2262, 24115], Leg6.HeldList.Select(held => held.QuestId).Order());
		foreach (NaturalAltgardHeld held in Leg6.HeldList)
		{
			Assert.Equal(held.EndNpcId, Assert.Single(Plans[held.QuestId].EndNpcs).Id);
			Assert.Contains(Spots(held.EndNpcId), spot => Leg6.Area(held.Hub).Contains(spot[0], spot[1], spot[2]));
		}
		Assert.Equal(Leg6.Order.Except([2262, 24115]).Order(), Leg6.Endpoint.CompletedQuestIds.Order());
		Assert.Equal([2262, 24115], Leg6.Endpoint.HeldQuestIds!.Order());
		Assert.True(Leg6.Endpoint.InHub);
	}

	[Fact]
	public void EveryTemplateQuestsTargetsLieInItsArea()
	{
		foreach (NaturalAltgardQuest quest in Leg6.Quests.Where(quest => quest.IsTemplate))
		{
			NaturalAltgardArea area = Leg6.Area(quest.Area!);
			QuestRunPlan plan = Plans[quest.Id];
			int[] targets = plan.Steps.SelectMany(step => step.Npcs.Select(npc => npc.Id).Concat(step.Sources.Select(source => source.NpcId).OfType<int>()))
				.Distinct().Where(id => !plan.EndNpcs.Any(npc => npc.Id == id) && Spots(id).Any()).ToArray();
			Assert.True(targets.Length > 0 || plan.Template == "report_to", $"Q{quest.Id} has no spawned target");
			foreach (int target in targets)
				Assert.Contains(Spots(target), spot => area.Contains(spot[0], spot[1], spot[2]));
			// A delivery's area is where it ends.
			if (plan.Template == "report_to")
				Assert.Contains(Spots(Assert.Single(plan.EndNpcs).Id), spot => area.Contains(spot[0], spot[1], spot[2]));
		}
	}

	[Fact]
	public void TheScriptedStepsFollowTheirHandlers()
	{
		foreach (NaturalAltgardStep step in Leg6.Steps)
		{
			string source = File.ReadAllText(Handler(step.QuestId));
			Assert.Contains($"{step.NpcId}", source);
			Assert.Equal(Assert.Single(Spots(step.NpcId)), step.Position);
			Assert.Contains(Spots(step.NpcId), spot => Leg6.Area(step.Area!).Contains(spot[0], spot[1], spot[2]));
			foreach (int page in step.Pages.Where(page => page != 5))
				Assert.Contains($"SendQuestDialog(env, {page})", source);
			if (step.Pages.Contains(5)) Assert.Contains("SendQuestEndDialog(env)", source);
			foreach (string action in step.Actions.Where(action => action.StartsWith("SETPRO", StringComparison.Ordinal)))
				Assert.Contains($"DialogAction.{action}", source);
			if (step.ReceivesItemId is int item) Assert.Contains($"{item}", source);
		}
		// Every scripted quest has its offer, and the two that end in a talk have their reward step.
		foreach (int quest in Scripted)
			Assert.Single(Leg6.StepsFor(quest), step => step.ExpectedStatus == "OFFER");
		Assert.Equal([2252, 2284], Leg6.Steps.Where(step => step.ExpectedStatus == "REWARD").Select(step => step.QuestId).Order());
	}

	[Fact]
	public void TheEscortAndTheSpawnFollowTheirHandlersAndSpawns()
	{
		// Q2284: the second disguised Germir (798041) starts the follow at var 1 (SETPRO3 -> var 2) to Babarunerk; reaching him
		// (20 m of his spawn, Java CoordinateDestinationChecker) sets REWARD at var 2; losing him (50 m, FollowingNpcCheckTask) sets
		// var 1 back. 798041 exists only 04:00-21:00 (temporary_spawn).
		NaturalAltgardEscort escort = Assert.Single(Leg6.EscortList);
		string escape = File.ReadAllText(Handler(escort.QuestId));
		Assert.Contains($"DefaultStartFollowEvent(env, (Npc)env.GetVisibleObject(), {escort.GoalNpcId}, {escort.StartVar}, {escort.FollowVar})", escape);
		Assert.Contains($"DefaultFollowEndEvent(env, {escort.FollowVar}, {escort.SuccessVar}, true)", escape);
		Assert.Contains($"DefaultFollowEndEvent(env, {escort.FollowVar}, {escort.LostVar}, false)", escape);
		Assert.Equal((20f, 50f), (escort.GoalRadius, escort.Leash));
		Assert.Equal(Assert.Single(Spots(escort.GoalNpcId)), escort.Goal);
		XElement follower = Assert.Single(Spawns().Descendants("spawn"), spawn => (int?)spawn.Attribute("npc_id") == escort.FollowerNpcId);
		XElement hours = follower.Element("temporary_spawn")!;
		Assert.Equal((escort.FollowerSpawnHour, escort.FollowerDespawnHour),
			((int?)int.Parse(((string)hours.Attribute("spawn_time")!).Split('.')[0]), (int?)int.Parse(((string)hours.Attribute("despawn_time")!).Split('.')[0])));
		Assert.Equal(escort.FollowerRespawnSeconds, (int)follower.Attribute("respawn_time")!);
		Assert.Equal(escort.StartStep, Assert.Single(Leg6.StepsFor(escort.QuestId), step => step.Var == escort.StartVar).Key);

		// Q2252: the Bones of Minushan (700060), used with Sinood's item, spawn Minushan's Spirit (95%) or Drakie for 3 minutes.
		NaturalAltgardSpawn spawn = Assert.Single(Leg6.SpawnList);
		string legend = File.ReadAllText(Handler(spawn.QuestId));
		Assert.Contains($"questKillNpc1Id = {spawn.NpcId}", legend);
		Assert.Contains($"questKillNpc2Id = {Assert.Single(spawn.AlternateNpcIds!)}", legend);
		Assert.Contains($"questStep1NpcId = {spawn.TriggerNpcId}", legend);
		Assert.Contains($"questActionItemId = {spawn.RequiresItemId}", legend);
		Assert.Contains($"int spawnTime = {spawn.LifetimeSeconds / 60}", legend);
		Assert.Contains($"SendQuestDialog(env, {spawn.RefillPage})", legend);
		Assert.Null(spawn.MovieId);
		Assert.Equal(Assert.Single(Spots(spawn.TriggerNpcId)), spawn.Position);
	}

	[Fact]
	public void TheBerthIsTwoHubFlightsFromTheBasfeltBind()
	{
		// AG-00: from where Leg 5 left the Cleric (bound at Basfelt), the leg's bind is Hrold's flight and then the fortress's.
		NaturalAltgardContract leg5 = NaturalAltgardContract.LoadLeg("l5");
		float[] basfelt = leg5.Bind!.Position, berth = Leg6.Bind!.Position;
		NaturalAirlineJourney journey = Assert.IsType<NaturalAirlineJourney>(NaturalAirlineRoutes.Journey(NaturalAirlineRoutes.Load(Root()),
			Leg6.Hub.MapId, new BotPosition(basfelt[0], basfelt[1], basfelt[2], 0), new BotPosition(berth[0], berth[1], berth[2], 0)));
		Assert.Equal([203683, 203561], journey.Flights.Select(flight => flight.NpcId));
	}

	private static IEnumerable<float[]> Spots(int npcId) =>
		Spawns().Descendants("spawn").Where(spawn => (int?)spawn.Attribute("npc_id") == npcId).Elements("spot")
			.Select(spot => new[] { (float)spot.Attribute("x")!, (float)spot.Attribute("y")!, (float)spot.Attribute("z")! });

	private static XElement Spawns() => XDocument.Load(Data("spawns", "Npcs/220030000_Altgard.xml")).Root!;
	private static XElement Quest(XElement root, int id) => Assert.Single(root.Elements("quest"), quest => (int?)quest.Attribute("id") == id);
	private static string Handler(int questId) =>
		Path.Combine(Root(), "src/Aion.GameServer/Handlers/Quest", Leg6.Quest(questId).Handler!);
	private static string Data(params string[] parts) => Path.Combine([Path.Combine(Root(), "game-server/data/static_data"), .. parts]);
	private static string Root() => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
}
