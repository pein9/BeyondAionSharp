using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

/// <summary>AB-01: the Altgard Leg 4 (Basfelt Village) contract against shipped data, its plans and the C# handlers.</summary>
public sealed class NaturalAltgardLeg4ContractTests
{
	private static readonly NaturalAltgardContract Leg4 = NaturalAltgardContract.LoadLeg("l4");

	[Fact]
	public void Leg4StartsWhereLeg3EndsAndPinsQuestDataChainsTheBindAndTheEndpoint()
	{
		NaturalAltgardContract leg3 = NaturalAltgardContract.LoadLeg("l3");
		Assert.Equal(leg3.JavaReference, Leg4.JavaReference);
		Assert.Equal((Leg4.Start.Contract, Leg4.Start.Snapshot), ("natural-altgard-l3-contract.json", leg3.Endpoint.Snapshot));
		// Q2217 was added by the isolated Leg 3 correction on 2026-10-03.
		// Leg 4 still supports its immutable historical altgard-l3 input,
		// captured before that correction; newer input preserves Q2217 too.
		Assert.Equal(leg3.Start.CompletedQuestIds.Concat(leg3.Endpoint.CompletedQuestIds).Except([2217]).Order(), Leg4.Start.CompletedQuestIds.Order());
		Assert.Equal((leg3.Start.MapId, leg3.Start.Race, leg3.Start.Class, leg3.Start.BindNpcId),
			(Leg4.Start.MapId, Leg4.Start.Race, Leg4.Start.Class, Leg4.Start.BindNpcId));
		Assert.True(Leg4.Start.Level >= leg3.Endpoint.MinimumLevel);
		Assert.Equal(leg3.Start.StartedQuestIds, Leg4.Start.StartedQuestIds);
		Assert.Equal(leg3.Start.LockedQuestIds, Leg4.Start.LockedQuestIds);
		// The hub is Leg 3's endpoint (Nokir); Basfelt is also the town now, and the bind moves here on arrival (AB-Q5).
		Assert.Equal(leg3.Endpoint.Anchor, Leg4.Hub.Anchor);
		Assert.Equal(Leg4.Hub.Anchor, Leg4.Town!.Anchor);
		NaturalAltgardBind bind = Leg4.Bind!;
		Assert.True(bind.OnArrival);
		AssertSpawn(bind.NpcId, bind.Position);
		Assert.Equal("obelisk", (string?)Template(bind.NpcId).Attribute("name"));
		Assert.True(Leg4.Area("basfelt").Contains(bind.Position[0], bind.Position[1], bind.Position[2]));
		Assert.Equal(bind.NpcId, Leg4.Endpoint.BindNpcId);
		XElement bindPoint = Assert.Single(XDocument.Load(Data("bind_points", "bind_points.xml")).Root!.Descendants("bind_point"),
			node => (int?)node.Attribute("npcid") == bind.NpcId);
		Assert.Equal(bind.Price, (int)bindPoint.Attribute("price")!);

		XElement questData = XDocument.Load(Data("quest_data", "quest_data.xml")).Root!;
		foreach (NaturalAltgardQuest expected in Leg4.Quests)
		{
			XElement quest = Quest(questData, expected.Id);
			Assert.Equal(expected.Category, (string?)quest.Attribute("category"));
			Assert.Equal(expected.MinimumLevel, (int?)quest.Attribute("minlevel_permitted"));
			Assert.Equal(Leg4.Start.Race, (string?)quest.Attribute("race_permitted"));
			Assert.All(quest.Elements("rewards"), reward => Assert.Equal(expected.RewardExperience, (int?)reward.Attribute("exp")));
			int[] finished = quest.Elements("start_conditions").Elements("finished").Select(node => (int)node.Attribute("quest_id")!).ToArray();
			Assert.Equal(expected.Prerequisite is { } prerequisite ? [prerequisite] : [], finished);
			Assert.True(expected.MinimumLevel <= Leg4.Start.Level);
		}
		for (int index = 0; index < Leg4.Order.Length; index++)
			if (Leg4.Quest(Leg4.Order[index]).Prerequisite is int before)
				Assert.True(Array.IndexOf(Leg4.Order, before) is int at && at >= 0 && at < index, $"Q{Leg4.Order[index]} before Q{before}");
		// AB-Q4: the Gornak chain and the campaign are in; the campaign is the snapshot's started mission.
		Assert.Subset(Leg4.Order.ToHashSet(), new HashSet<int> { 2227, 2291, 24013 });
		Assert.Equal(new[] { 24013 }, Leg4.Start.StartedQuestIds);
		Assert.Equal("MISSION", Leg4.Quest(24013).Category);

		// AB-Q1 (a): every quest done, and the leg ends at Basfelt, in the hub.
		Assert.Equal(Leg4.Order.Order(), Leg4.Endpoint.CompletedQuestIds.Order());
		Assert.True(Leg4.Endpoint.InHub);
		Assert.Null(Leg4.Endpoint.Anchor);
		Assert.Equal("altgard-l4", Leg4.Endpoint.Snapshot);
		Assert.Empty(Leg4.Excluded);
		Assert.Null(Leg4.Flight);
		Assert.Null(Leg4.AirKills);
	}

	[Fact]
	public void TemplateQuestsHaveCompiledPlansWhoseNpcsAndTargetsAreOnTheirGrounds()
	{
		IReadOnlyDictionary<int, QuestRunPlan> plans = NaturalAltgardContract.LoadPlans("l4");
		Assert.Equal(Leg4.Quests.Where(quest => quest.IsTemplate).Select(quest => quest.Id).Order(), plans.Keys.Order());
		XElement scripts = XDocument.Load(Data("quest_script_data", "altgard.xml")).Root!;
		foreach (NaturalAltgardQuest quest in Leg4.Quests.Where(quest => quest.IsTemplate))
		{
			QuestRunPlan plan = plans[quest.Id];
			Assert.Equal(quest.Template, plan.Template);
			XElement script = Assert.Single(scripts.Elements(quest.Template!), node => (int?)node.Attribute("id") == quest.Id);
			Assert.Equal(quest.StartNpcId, (int?)script.Attribute("start_npc_ids"));
			Assert.Equal(quest.StartNpcId, Assert.Single(plan.StartNpcs).Id);
			NaturalAltgardArea ground = Leg4.Area(quest.Area!);
			QuestRunNpc[] targets = plan.Steps.Where(step => step.Kind is "kill" or "collect")
				.SelectMany(step => step.Npcs.Concat(step.Sources.Select(source => source.Npc).OfType<QuestRunNpc>())).ToArray();
			Assert.All(targets, target => Assert.Contains(target.Positions, at => at.MapId == Leg4.Hub.MapId && ground.Contains(at.X, at.Y, at.Z)));
		}
		// The deliveries: Garuntat -> Gornak (Q2226) and back (Q2291).
		Assert.Equal(203639, Assert.Single(plans[2226].EndNpcs).Id);
		Assert.Equal(203617, Assert.Single(plans[2291].EndNpcs).Id);
	}

	[Fact]
	public void ScriptedStepsMatchSpawnsTalkRangesHandlersAndAreas()
	{
		foreach (NaturalAltgardStep step in Leg4.Steps)
		{
			string handler = Handler(Leg4.Quest(step.QuestId).Handler);
			Assert.True(Registers(handler, step.NpcId), $"{step.Key}: {step.NpcId} is not registered");
			Assert.Equal(step.TalkRange, (int)Template(step.NpcId).Element("talk_info")!.Attribute("distance")! + 1);
			AssertSpawn(step.NpcId, step.Position);
			Assert.True(Leg4.Area(step.Area!).Contains(step.Position[0], step.Position[1], step.Position[2]), step.Key);
			foreach (string action in step.Actions.Where(action => action is not ("QUEST_ACCEPT_1" or "USE_OBJECT") && !action.Contains("REWARD", StringComparison.Ordinal)))
				Assert.Contains($"DialogAction.{action}", handler, StringComparison.Ordinal);
			foreach (int page in step.Pages.Where(page => page is not (5 or 10)))
				Assert.Contains($"SendQuestDialog(env, {page})", handler, StringComparison.Ordinal);
			if (step.MovieId is { } movie)
				Assert.Contains($"PlayQuestMovie(env, {movie})", handler, StringComparison.Ordinal);
			if (step.ReceivesItemId is { } item)
				Assert.Contains(item.ToString(), handler, StringComparison.Ordinal);
			Assert.False(step.Flight);
		}
		// Q2239's Vovetirn SETPRO1 re-sends page 10 instead of closing, and the check jumps var 1 -> 3.
		string antidote = Handler("altgard/_2239MalodorAntidote.cs");
		Assert.Contains("GiveQuestItem(env, 182203227, 1)", antidote, StringComparison.Ordinal);
		Assert.Contains("qs.GetQuestVarById(0) + 2", antidote, StringComparison.Ordinal);
		// The reward choices sit in the steps that take them, and point at the right item.
		XElement questData = XDocument.Load(Data("quest_data", "quest_data.xml")).Root!;
		XElement items = XDocument.Load(Data("items", "item_templates.xml")).Root!;
		Assert.Equal(3, Leg4.RewardChoiceList.Length);
		foreach (NaturalAltgardRewardChoice choice in Leg4.RewardChoiceList)
		{
			int[] selectable = Quest(questData, choice.QuestId).Descendants("selectable_reward_item").Select(node => (int)node.Attribute("item_id")!).ToArray();
			Assert.Equal(choice.ItemId, selectable[NaturalAscensionContract.DialogActionId(choice.Action) - DialogAction.SELECTED_QUEST_REWARD1]);
			Assert.Equal(choice.ItemGroup, (string?)Assert.Single(items.Elements("item_template"), node => (int?)node.Attribute("id") == choice.ItemId).Attribute("item_group"));
			Assert.Contains(Leg4.StepsFor(choice.QuestId), step => step.Actions.Contains(choice.Action));
		}
	}

	[Fact]
	public void HuntsTimersSpawnsTheAvoidanceAndTheZoneItemUseMatchTheHandlers()
	{
		// Custom kill counters.
		string[] huntCode =
		[
			"QuestService.QuestTimerStart(env, 600)", "DefaultOnKillEvent(env, mobs, 0, 5)", "var == 0 && targetId == questKillNpcId",
			"DefaultOnKillEvent(env, mobs, 3, 7)",
		];
		Assert.Equal(4, Leg4.HuntList.Length);
		foreach ((NaturalAltgardHunt hunt, string code) in Leg4.HuntList.Zip(huntCode))
		{
			string handler = Handler(Leg4.Quest(hunt.QuestId).Handler);
			Assert.Contains(code, handler, StringComparison.Ordinal);
			Assert.All(hunt.NpcIds, npc => Assert.Contains(npc.ToString(), handler, StringComparison.Ordinal));
			Assert.All(hunt.NpcIds, npc => Assert.Contains(Spots(npc), at => Leg4.Area(hunt.Area).Contains(at[0], at[1], at[2])));
		}
		Assert.Contains("DefaultOnKillEvent(env, mobs, 7, true)", Handler("altgard/_24013PoisonInTheWaters.cs"), StringComparison.Ordinal);

		// The timers: Q2288 600 s from SETPRO1, abandoned at the end or on logout; Q2230 1,800 s, a new chance, the tusks taken.
		NaturalAltgardTimer money = Leg4.TimerList.Single(timer => timer.QuestId == 2288);
		string moneyCode = Handler("altgard/_2288MoneyWhereYourMouthIs.cs");
		Assert.Contains($"QuestService.QuestTimerStart(env, {money.Seconds})", moneyCode, StringComparison.Ordinal);
		Assert.Contains("SETPRO1", Leg4.Steps.Single(step => step.Key == money.StartStep).Actions);
		Assert.Contains("qe.RegisterOnQuestTimerEnd(questId)", moneyCode, StringComparison.Ordinal);
		Assert.Contains("qe.RegisterOnLogOut(questId)", moneyCode, StringComparison.Ordinal);
		Assert.Equal(2, CountOf(moneyCode, "QuestService.AbandonQuest(player, questId)"));
		NaturalAltgardTimer wager = Leg4.TimerList.Single(timer => timer.QuestId == 2230);
		string wagerCode = Handler("altgard/_2230AFriendlyWager.cs");
		Assert.Contains($"questDurationTime = {wager.Seconds}", wagerCode, StringComparison.Ordinal);
		Assert.Contains($"questDropItemId = {Assert.Single(wager.LostItemIds)}", wagerCode, StringComparison.Ordinal);
		Assert.Contains($"SendQuestDialog(env, {wager.ExpiredPage})", wagerCode, StringComparison.Ordinal);
		Assert.Contains($"DialogAction.{wager.NewChanceAction}", wagerCode, StringComparison.Ordinal);
		Assert.Equal(("new-chance", "abandon"), (wager.OnExpiry, wager.OnLogout));

		// Infernus: the burner with the incense plays movie 67; its end spawns him for five minutes.
		NaturalAltgardSpawn infernus = Assert.Single(Leg4.SpawnList);
		string monster = Handler("altgard/_2223AMythicalMonster.cs");
		Assert.Contains($"UseQuestObject(env, {infernus.AtVar}, {infernus.AtVar}, false, 0, 0, 0, {infernus.RequiresItemId}, 1, {infernus.MovieId}, true)",
			monster, StringComparison.Ordinal);
		Assert.Contains($"SpawnForFiveMinutes({infernus.NpcId}, env.GetPlayer().GetWorldMapInstance(), (float)1547.1047, (float)894.2969, (float)248.019",
			monster, StringComparison.Ordinal);
		Assert.Equal(300, infernus.LifetimeSeconds);
		Assert.Equal([1547.1047f, 894.2969f, 248.019f], infernus.Position);
		Assert.Contains($"SendQuestDialog(env, {infernus.RefillPage})", monster, StringComparison.Ordinal);
		Assert.Equal(("EXPERT", "13"), ((string?)Template(infernus.NpcId).Attribute("rank"), (string?)Template(infernus.NpcId).Attribute("level")));
		XElement burner = Assert.Single(Spawns().Descendants("spawn"), node => (int?)node.Attribute("npc_id") == infernus.TriggerNpcId);
		Assert.Equal(infernus.TriggerRespawnSeconds, (int)burner.Attribute("respawn_time")!);
		Assert.True(Leg4.Area(infernus.Area).Contains(infernus.Position[0], infernus.Position[1], infernus.Position[2]));

		// Komu: his horn only at Q2289 var 7, and an hour to respawn.
		NaturalAltgardAvoid komu = Assert.Single(Leg4.AvoidList);
		XElement komuSpawn = Assert.Single(Spawns().Descendants("spawn"), node => (int?)node.Attribute("npc_id") == komu.NpcId);
		Assert.Equal(komu.RespawnSeconds, (int)komuSpawn.Attribute("respawn_time")!);
		NaturalAltgardCollection horn = Leg4.CollectionList.Single(collection => collection.QuestId == komu.QuestId);
		Assert.Equal(komu.UntilVar, horn.AtVar);
		Assert.Equal(komu.NpcId, Assert.Single(Assert.Single(horn.Items).SourceNpcIds));

		// Q24013's poison, used inside its zone at var 2, spawns two Feral Black Claw Sharpeyes.
		NaturalAltgardItemUse poison = Leg4.RequiredItemUse;
		string campaign = Handler("altgard/_24013PoisonInTheWaters.cs");
		Assert.False(poison.Anywhere);
		Assert.Contains($"qe.RegisterQuestItem({poison.ItemId}, questId)", campaign, StringComparison.Ordinal);
		Assert.Contains($"ZoneName.Get(\"{poison.Zone}\")", campaign, StringComparison.Ordinal);
		Assert.Contains($"UseQuestItem(env, item, {poison.Var}, {poison.NextVar}, false)", campaign, StringComparison.Ordinal);
		Assert.Equal(poison.SpawnCount, CountOf(campaign, $"Spawn({poison.SpawnsNpcId}, player"));
		XElement zone = Assert.Single(XDocument.Load(Data("zones", "zones_220030000.xml")).Root!.Elements("zone"),
			node => (string?)node.Attribute("name") == poison.Zone);
		Assert.True(Leg4.Area("black-claws").Contains(poison.ZoneAnchor![0], poison.ZoneAnchor[1], poison.ZoneAnchor[2]));
		// AB-08: the contract carries the zone's polygon and height band exactly, and the anchor lies inside them.
		XElement points = zone.Element("points")!;
		Assert.Equal(points.Elements("point").Select(point => $"{(float)point.Attribute("x")!},{(float)point.Attribute("y")!}"),
			poison.ZonePolygon!.Select(point => $"{point[0]},{point[1]}"));
		Assert.Equal([(float)points.Attribute("bottom")!, (float)points.Attribute("top")!], poison.ZoneBand!);
		Assert.True(poison.InZone(poison.ZoneAnchor[0], poison.ZoneAnchor[1], poison.ZoneAnchor[2], margin: 3));
		// Where the run-7 road first stood inside the zone, and points outside it (east, north, above the band).
		Assert.True(poison.InZone(1688.3f, 242.7f, 285.9f, margin: 2));
		Assert.False(poison.InZone(1738, 236, 287));
		Assert.False(poison.InZone(1688, 262, 286));
		Assert.False(poison.InZone(1688.3f, 242.7f, 361));
	}

	[Fact]
	public void CollectionsAndTheBeehivesMatchQuestData()
	{
		XElement questData = XDocument.Load(Data("quest_data", "quest_data.xml")).Root!;
		foreach (NaturalAltgardCollection collection in Leg4.CollectionList)
		{
			XElement quest = Quest(questData, collection.QuestId);
			foreach (NaturalAltgardCollectedItem item in collection.Items)
			{
				Assert.Equal(item.Count, (int?)quest.Descendants("collect_item").Single(node => (int?)node.Attribute("item_id") == item.ItemId).Attribute("count"));
				XElement[] drops = quest.Descendants("quest_drop").Where(drop => (int?)drop.Attribute("item_id") == item.ItemId).ToArray();
				Assert.Equal(item.SourceNpcIds.Order(), drops.Select(drop => (int)drop.Attribute("npc_id")!).Order());
				Assert.All(drops, drop => Assert.Equal(collection.AtVar, (int?)drop.Attribute("collecting_step") ?? 0));
			}
		}
		NaturalAltgardObjectUse hives = Assert.Single(Leg4.ObjectUseList);
		Assert.Equal("quest_use_item", (string?)Template(hives.NpcId).Attribute("ai"));
		XElement spawn = Assert.Single(Spawns().Descendants("spawn"), node => (int?)node.Attribute("npc_id") == hives.NpcId);
		Assert.Equal((hives.SpawnCount, hives.RespawnSeconds), (spawn.Elements("spot").Count(), (int)spawn.Attribute("respawn_time")!));
		Assert.True(hives.SpawnCount >= hives.Uses);
		Assert.All(Spots(hives.NpcId), at => Assert.True(Leg4.Area(hives.Area!).Contains(at[0], at[1], at[2])));
		Assert.Contains($"RegisterQuestNpc({hives.NpcId})", Handler("altgard/_2232TheBrokenHoneyJar.cs"), StringComparison.Ordinal);
	}

	/// <summary>The handler registers the NPC, by its id or through a constant holding it (Shania's questStartNpcId).</summary>
	private static bool Registers(string handler, int npcId) =>
		handler.Contains($"RegisterQuestNpc({npcId})", StringComparison.Ordinal) ||
		System.Text.RegularExpressions.Regex.Matches(handler, $@"(\w+) = {npcId};").Any(constant =>
			handler.Contains($"RegisterQuestNpc({constant.Groups[1].Value})", StringComparison.Ordinal));

	private static int CountOf(string text, string value)
	{
		int count = 0;
		for (int at = text.IndexOf(value, StringComparison.Ordinal); at >= 0; at = text.IndexOf(value, at + value.Length, StringComparison.Ordinal))
			count++;
		return count;
	}

	private static float[] Spot(XElement spot) => [(float)spot.Attribute("x")!, (float)spot.Attribute("y")!, (float)spot.Attribute("z")!];

	private static IEnumerable<float[]> Spots(int npcId) =>
		Spawns().Descendants("spawn").Where(spawn => (int?)spawn.Attribute("npc_id") == npcId).Elements("spot").Select(Spot);

	private static void AssertSpawn(int npcId, float[] position) =>
		Assert.Contains(Spots(npcId), spot => MathF.Abs(spot[0] - position[0]) < 0.01f && MathF.Abs(spot[1] - position[1]) < 0.01f &&
			MathF.Abs(spot[2] - position[2]) < 0.01f);

	private static XElement Template(int npcId) => Assert.Single(XDocument.Load(Data("npcs", "npc_templates.xml")).Root!.Elements("npc_template"),
		node => (int?)node.Attribute("npc_id") == npcId);

	private static XElement Spawns() => XDocument.Load(Data("spawns", "Npcs/220030000_Altgard.xml")).Root!;
	private static XElement Quest(XElement root, int id) => Assert.Single(root.Elements("quest"), quest => (int?)quest.Attribute("id") == id);
	private static string Handler(string relative) => File.ReadAllText(Path.Combine(Root(), "src/Aion.GameServer/Handlers/Quest", relative));
	private static string Data(params string[] parts) => Path.Combine([Path.Combine(Root(), "game-server/data/static_data"), .. parts]);
	private static string Root() => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
}
