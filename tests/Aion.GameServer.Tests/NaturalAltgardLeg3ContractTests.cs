using System.Xml.Linq;
using Aion.Bots.Scenarios;

namespace Aion.GameServer.Tests;

/// <summary>AC-01: the Altgard Leg 3 (Manir's Campsite) contract against shipped data, the C# handlers and the escort engine.</summary>
public sealed class NaturalAltgardLeg3ContractTests
{
	private static readonly NaturalAltgardContract Leg3 = NaturalAltgardContract.LoadLeg("l3");

	[Fact]
	public void Leg3StartsWhereLeg2EndsAndPinsQuestDataChainsAndTheEndpoint()
	{
		NaturalAltgardContract leg2 = NaturalAltgardContract.LoadLeg("l2");
		Assert.Equal(leg2.JavaReference, Leg3.JavaReference);
		Assert.Equal((Leg3.Start.Contract, Leg3.Start.Snapshot), ("natural-altgard-l2-contract.json", leg2.Endpoint.Snapshot));
		Assert.Equal(leg2.Start.CompletedQuestIds.Concat(leg2.Endpoint.CompletedQuestIds).Order(), Leg3.Start.CompletedQuestIds.Order());
		Assert.Equal((leg2.Start.MapId, leg2.Start.Race, leg2.Start.Class, leg2.Start.BindNpcId),
			(Leg3.Start.MapId, Leg3.Start.Race, Leg3.Start.Class, Leg3.Start.BindNpcId));
		Assert.True(Leg3.Start.Level >= leg2.Endpoint.MinimumLevel);
		// The hub is Leg 2's endpoint (Manir); the town stays the fortress.
		Assert.Equal(leg2.Endpoint.Anchor, Leg3.Hub.Anchor);
		Assert.Equal((leg2.Town!.Key, leg2.Town.Radius), (Leg3.Town!.Key, Leg3.Town.Radius));
		Assert.Equal(leg2.Town.Anchor, Leg3.Town.Anchor);

		XElement questData = XDocument.Load(Data("quest_data", "quest_data.xml")).Root!;
		foreach (NaturalAltgardQuest expected in Leg3.Quests)
		{
			XElement quest = Quest(questData, expected.Id);
			Assert.Equal(expected.Category, (string?)quest.Attribute("category"));
			Assert.Equal(expected.MinimumLevel, (int?)quest.Attribute("minlevel_permitted"));
			Assert.Equal(Leg3.Start.Race, (string?)quest.Attribute("race_permitted"));
			Assert.All(quest.Elements("rewards"), reward => Assert.Equal(expected.RewardExperience, (int?)reward.Attribute("exp")));
			// No reward to choose in Leg 3, so the contract has no reward choice.
			Assert.Empty(quest.Descendants("selectable_reward_item"));
			int[] finished = quest.Element("start_conditions")?.Elements("finished").Select(node => (int)node.Attribute("quest_id")!).ToArray() ?? [];
			Assert.Equal(expected.Prerequisite is { } prerequisite ? [prerequisite] : [], finished);
			Assert.True(expected.MinimumLevel <= Leg3.Start.Level);
			Assert.Equal(expected.Id is 24111 or 2217, expected.IsTemplate);
		}
		Assert.Equal([24111, 2217, 2221, 2290, 2222], Leg3.Order);
		// Q24111 (D32): taken from Olenja at Moslan Crossroad, the primer from the suspicious document at Manir's Dock,
		// handed in to Nokir where Leg 3 ends, so it is held for the end like a hand-in there (area "basfelt").
		IReadOnlyDictionary<int, QuestRunPlan> plans = NaturalAltgardContract.LoadPlans("l3");
		Assert.Equal([2217, 24111], plans.Keys.Order());
		QuestRunPlan dock = plans[24111];
		Assert.Equal(24111, dock.Id);
		Assert.Equal("item_collecting", dock.Template);
		NaturalAltgardQuest primer = Leg3.Quest(24111);
		Assert.Equal((primer.StartNpcId, primer.Area), (Assert.Single(dock.StartNpcs).Id, "basfelt"));
		Assert.Equal(203631, Assert.Single(dock.EndNpcs).Id);
		// Q2217 shares Olenja's pickup and finishes at Gefion in Basfelt. The
		// modern Q24012 already in the incoming snapshot satisfies one of the
		// two optional Java finished-quest conditions; Q2013 is retired.
		QuestRunPlan letter = plans[2217];
		Assert.Equal("report_to", letter.Template);
		Assert.Equal(203606, Assert.Single(letter.StartNpcs).Id);
		Assert.Equal(203616, Assert.Single(letter.EndNpcs).Id);
		Assert.Equal(new[] { 24012, 2013 }, letter.FinishedQuestGroups.SelectMany(group => group));
		Assert.Contains(Leg3.Quest(2217).Prerequisite!.Value, Leg3.Start.CompletedQuestIds);
		Assert.DoesNotContain(2013, Leg3.Start.CompletedQuestIds);
		Assert.Equal("basfelt", Leg3.Quest(2217).Area);

		// The snapshot's open campaign (Q24013, Idun's Lake) and the locked ones stay outside Leg 3.
		Assert.All(Leg3.Start.StartedQuestIds!.Concat(Leg3.Start.LockedQuestIds), id =>
		{
			Assert.DoesNotContain(id, Leg3.Order);
			Assert.Equal("MISSION", (string?)Quest(questData, id).Attribute("category"));
		});

		// AC-Q1: Leg 3 ends at Basfelt beside Nokir, where Q2222 is handed in.
		Assert.Equal(Leg3.Order.Order(), Leg3.Endpoint.CompletedQuestIds.Order());
		Assert.False(Leg3.Endpoint.InHub);
		AssertSpawn(203631, Leg3.Endpoint.Anchor!);
		Assert.True(Leg3.Area("basfelt").Contains(Leg3.Endpoint.Anchor![0], Leg3.Endpoint.Anchor[1], Leg3.Endpoint.Anchor[2]));
		Assert.Equal("altgard-l3", Leg3.Endpoint.Snapshot);
		Assert.Empty(Leg3.Excluded);
		Assert.Null(Leg3.RewardChoice);
		Assert.Null(Leg3.Flight);
		Assert.Null(Leg3.ItemUse);
		Assert.Null(Leg3.AirKills);
	}

	[Fact]
	public void ScriptedStepsMatchSpawnsTalkRangesHandlersAndAreas()
	{
		XElement templates = XDocument.Load(Data("npcs", "npc_templates.xml")).Root!;
		foreach (NaturalAltgardStep step in Leg3.Steps)
		{
			string handler = Handler(Leg3.Quest(step.QuestId).Handler);
			Assert.Contains($"RegisterQuestNpc({step.NpcId})", handler, StringComparison.Ordinal);
			XElement template = Assert.Single(templates.Elements("npc_template"), node => (int?)node.Attribute("npc_id") == step.NpcId);
			Assert.Equal(step.TalkRange, (int)template.Element("talk_info")!.Attribute("distance")! + 1);
			AssertSpawn(step.NpcId, step.Position);
			Assert.True(Leg3.Area(step.Area!).Contains(step.Position[0], step.Position[1], step.Position[2]), step.Key);
			foreach (string action in step.Actions.Where(action => action != "QUEST_ACCEPT_1" && !action.Contains("REWARD", StringComparison.Ordinal)))
				Assert.Contains($"DialogAction.{action}", handler, StringComparison.Ordinal);
			foreach (int page in step.Pages.Where(page => page != 5))
				Assert.Contains($"SendQuestDialog(env, {page})", handler, StringComparison.Ordinal);
			Assert.False(step.Flight);
		}
		// The two hand-ins without a reward window: Groken's (Q2221) and Nokir's (Q2222) take SELECT_QUEST_REWARD.
		Assert.Contains("ChangeQuestStep(env, 2, 2, true)", Handler("altgard/_2221ManirsUncle.cs"), StringComparison.Ordinal);
		Assert.Contains("qs.SetStatus(QuestStatus.REWARD)", Handler("altgard/_2222ManirsMessage.cs"), StringComparison.Ordinal);
		Assert.Contains("DefaultCloseDialog(env, 3, 3, true, true)", Handler("altgard/_2290GrokensEscape.cs"), StringComparison.Ordinal);
	}

	[Fact]
	public void GrokensSafeIsAOneShotQuestObjectBesideCommanderMohen()
	{
		NaturalAltgardObjectUse safe = Assert.Single(Leg3.ObjectUseList);
		string handler = Handler(Leg3.Quest(safe.QuestId).Handler);
		Assert.Contains($"RegisterQuestNpc({safe.NpcId})", handler, StringComparison.Ordinal);
		Assert.Contains($"qe.RegisterOnGetItem({safe.LootItemId}, questId)", handler, StringComparison.Ordinal);
		Assert.Contains($"DefaultOnGetItemEvent(env, {safe.FromVar}, {safe.ToVar}, false)", handler, StringComparison.Ordinal);
		Assert.Contains($"SendQuestDialog(env, {safe.DialogPage})", handler, StringComparison.Ordinal);
		Assert.Contains($"DialogAction.{safe.CloseAction}", handler, StringComparison.Ordinal);
		Assert.Contains($"RemoveQuestItem(env, {safe.LootItemId}, 1)", handler, StringComparison.Ordinal);
		// The loot is a quest_drop at the use step with no chance (100%); QuestItemNpcAI kills the object after the use.
		XElement drop = Assert.Single(Quest(XDocument.Load(Data("quest_data", "quest_data.xml")).Root!, safe.QuestId).Elements("quest_drop"));
		Assert.Equal((safe.NpcId, safe.LootItemId, safe.FromVar), ((int)drop.Attribute("npc_id")!, (int?)drop.Attribute("item_id"),
			(int)drop.Attribute("collecting_step")!));
		Assert.Null(drop.Attribute("chance"));
		XElement template = Assert.Single(XDocument.Load(Data("npcs", "npc_templates.xml")).Root!.Elements("npc_template"),
			node => (int?)node.Attribute("npc_id") == safe.NpcId);
		Assert.Equal("quest_use_item", (string?)template.Attribute("ai"));
		Assert.Equal("true", (string?)template.Element("talk_info")!.Attribute("is_dialog"));
		Assert.True(safe.Disappears);
		XElement spawn = Assert.Single(Spawns().Descendants("spawn"), node => (int?)node.Attribute("npc_id") == safe.NpcId);
		Assert.Equal((safe.SpawnCount, safe.RespawnSeconds), (spawn.Elements("spot").Count(), (int)spawn.Attribute("respawn_time")!));
		float[] at = Spot(spawn.Elements("spot").Single());
		Assert.True(Leg3.Area(safe.Area!).Contains(at[0], at[1], at[2]));
		// Commander Mohen (EXPERT) stands within 6 m of the safe.
		Assert.Contains(Spots(210431), mohen => Distance(mohen, at) <= 6);
		Assert.True(Leg3.Area("safe-camp").Contains(at[0], at[1], at[2]));
	}

	[Fact]
	public void TheEscortMatchesTheHandlerAndJavasFollowRules()
	{
		NaturalAltgardEscort escort = Assert.Single(Leg3.EscortList);
		string handler = Handler(Leg3.Quest(escort.QuestId).Handler);
		// Both starts: SELECT1_1 on the offer, QUEST_SELECT at the lost var.
		Assert.Equal(2, CountOf(handler, $"DefaultStartFollowEvent(env, (Npc)env.GetVisibleObject(), {escort.GoalNpcId}, {escort.LostVar}, {escort.FollowVar})"));
		Assert.Equal("SELECT1_1", Leg3.Steps.Single(step => step.Key == escort.StartStep).Actions[^1]);
		NaturalAltgardStep restart = Leg3.Steps.Single(step => step.Key == escort.RestartStep);
		Assert.Equal((escort.LostVar, escort.FollowerNpcId, "QUEST_SELECT"), (restart.Var, restart.NpcId, Assert.Single(restart.Actions)));
		Assert.Contains($"DefaultFollowEndEvent(env, {escort.FollowVar}, {escort.SuccessVar}, false, {escort.MovieId})", handler, StringComparison.Ordinal);
		Assert.Contains($"DefaultFollowEndEvent(env, {escort.FollowVar}, {escort.LostVar}, false)", handler, StringComparison.Ordinal);
		Assert.Contains($"ChangeQuestStep(env, {escort.FollowVar}, {escort.LostVar})", handler, StringComparison.Ordinal);
		Assert.Contains("qe.RegisterOnLogOut(questId)", handler, StringComparison.Ordinal);
		Assert.Contains("qe.RegisterAddOnReachTargetEvent(questId)", handler, StringComparison.Ordinal);
		Assert.Contains("qe.RegisterAddOnLostTargetEvent(questId)", handler, StringComparison.Ordinal);
		Assert.Equal(escort.SuccessVar, Leg3.Steps.Single(step => step.Key == "q2290-v3-manir").Var);

		// The goal is the goal npc's first (only) spawn, at the dock.
		float[] goal = Spot(Assert.Single(Spawns().Descendants("spawn").Where(node => (int?)node.Attribute("npc_id") == escort.GoalNpcId)
			.Elements("spot")));
		Assert.Equal(escort.Goal, goal);
		Assert.True(Leg3.Area("dock").Contains(goal[0], goal[1], goal[2]));
		// Groken: the "following" AI (his end deletes him and schedules the respawn), his run speed, his spawn.
		XElement follower = Assert.Single(XDocument.Load(Data("npcs", "npc_templates.xml")).Root!.Elements("npc_template"),
			node => (int?)node.Attribute("npc_id") == escort.FollowerNpcId);
		Assert.Equal("following", (string?)follower.Attribute("ai"));
		Assert.Equal(escort.FollowerRunSpeed, (float)follower.Descendants("speeds").Single().Attribute("run")!);
		XElement spawn = Assert.Single(Spawns().Descendants("spawn"), node => (int?)node.Attribute("npc_id") == escort.FollowerNpcId);
		Assert.Equal(escort.FollowerRespawnSeconds, (int)spawn.Attribute("respawn_time")!);
		float[] start = Spot(spawn.Elements("spot").Single());
		Assert.True(Leg3.Area(escort.Area).Contains(start[0], start[1], start[2]));
		Assert.InRange(Distance(start, goal), 100, escort.Leash * 3);

		// The C# follow engine, a port of Java's: 50 m leash, 20 m of the goal's first spawn, every 1,000 ms.
		string check = Source("Questengine/Task/FollowingNpcCheckTask.cs");
		Assert.Contains($"PositionUtil.IsInRange(player, npc, {escort.Leash})", check, StringComparison.Ordinal);
		Assert.Contains($"PositionUtil.IsInRange(follower, x, y, z, {escort.GoalRadius})", Source("Questengine/Task/Checker/CoordinateDestinationChecker.cs"),
			StringComparison.Ordinal);
		Assert.Contains($"TimeSpan.FromMilliseconds({escort.CheckMillis})", Source("Questengine/Task/QuestTasks.cs"), StringComparison.Ordinal);
		Assert.Contains("GetFirstSpawnByNpcId", Source("Questengine/Task/QuestTasks.cs"), StringComparison.Ordinal);
		Assert.All(escort.ClearAreas, area => Leg3.Area(area));
		Assert.Equal(3, escort.MaxAttempts); // AC-Q2
	}

	private static int CountOf(string text, string value)
	{
		int count = 0;
		for (int at = text.IndexOf(value, StringComparison.Ordinal); at >= 0; at = text.IndexOf(value, at + value.Length, StringComparison.Ordinal))
			count++;
		return count;
	}

	private static float Distance(float[] a, float[] b) => MathF.Sqrt(MathF.Pow(a[0] - b[0], 2) + MathF.Pow(a[1] - b[1], 2));

	private static float[] Spot(XElement spot) => [(float)spot.Attribute("x")!, (float)spot.Attribute("y")!, (float)spot.Attribute("z")!];

	private static IEnumerable<float[]> Spots(int npcId) =>
		Spawns().Descendants("spawn").Where(spawn => (int?)spawn.Attribute("npc_id") == npcId).Elements("spot").Select(Spot);

	private static void AssertSpawn(int npcId, float[] position) =>
		Assert.Contains(Spots(npcId), spot => MathF.Abs(spot[0] - position[0]) < 0.01f && MathF.Abs(spot[1] - position[1]) < 0.01f &&
			MathF.Abs(spot[2] - position[2]) < 0.01f);

	private static XElement Spawns() => XDocument.Load(Data("spawns", "Npcs/220030000_Altgard.xml")).Root!;
	private static XElement Quest(XElement root, int id) => Assert.Single(root.Elements("quest"), quest => (int?)quest.Attribute("id") == id);
	private static string Handler(string relative) => File.ReadAllText(Path.Combine(Root(), "src/Aion.GameServer/Handlers/Quest", relative));
	private static string Source(string relative) => File.ReadAllText(Path.Combine(Root(), "src/Aion.GameServer", relative));
	private static string Data(params string[] parts) => Path.Combine([Path.Combine(Root(), "game-server/data/static_data"), .. parts]);
	private static string Root() => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
}
