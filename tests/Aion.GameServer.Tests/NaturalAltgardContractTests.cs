using System.Globalization;
using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

/// <summary>AF-01: the Altgard Leg 1 contract against shipped static data, the compiled plans and the ported handlers.</summary>
public sealed class NaturalAltgardContractTests
{
	private const string SpawnFile = "Npcs/220030000_Altgard.xml";

	[Fact]
	public void ContractStartsAtTheBridgeEndpointAndPinsQuestDataLevelGatesAndTheEndpoint()
	{
		NaturalAltgardContract contract = NaturalAltgardContract.LoadDefault();
		NaturalAscensionContract bridge = NaturalAscensionContract.LoadDefault();
		Assert.Equal(bridge.JavaReference, contract.JavaReference);
		Assert.Equal("natural-ascension-contract.json", contract.Start.Contract);
		Assert.Equal((bridge.Endpoint.MapId, bridge.Endpoint.Class, bridge.Endpoint.MinimumLevel, bridge.Endpoint.BindNpcId),
			(contract.Start.MapId, contract.Start.Class, contract.Start.Level, contract.Start.BindNpcId));
		Assert.Equal(bridge.Endpoint.CompletedQuestIds, contract.Start.CompletedQuestIds);
		Assert.Equal(bridge.Start.Race, contract.Start.Race);
		Assert.Equal(contract.Start.MapId, contract.Hub.MapId);
		Assert.Equal(bridge.Bind.Position, contract.Hub.Anchor);

		XElement questData = XDocument.Load(Data("quest_data", "quest_data.xml")).Root!;
		foreach (NaturalAltgardQuest expected in contract.Quests)
		{
			XElement quest = Quest(questData, expected.Id);
			Assert.Equal(expected.Category, (string?)quest.Attribute("category"));
			Assert.Equal(expected.MinimumLevel, (int?)quest.Attribute("minlevel_permitted"));
			Assert.Equal(contract.Start.Race, (string?)quest.Attribute("race_permitted"));
			Assert.All(quest.Elements("rewards"), reward => Assert.Equal(expected.RewardExperience, (int?)reward.Attribute("exp")));
			int[] finished = quest.Element("start_conditions")?.Elements("finished").Select(node => (int)node.Attribute("quest_id")!).ToArray() ?? [];
			Assert.Equal(expected.Prerequisite is { } prerequisite ? [prerequisite] : [], finished);
		}

		// The order is a plan the decision engine may reorder, but it never lists a quest before its gate or prerequisite.
		for (int index = 0; index < contract.Order.Length; index++)
		{
			NaturalAltgardQuest quest = contract.Quest(contract.Order[index]);
			Assert.True(index == 0 || contract.Quest(contract.Order[index - 1]).MinimumLevel <= quest.MinimumLevel);
			if (quest.Prerequisite is { } before)
			{
				int at = Array.IndexOf(contract.Order, before);
				Assert.True(at >= 0 && at < index, $"Q{quest.Id} is ordered before its prerequisite Q{before}");
			}
		}

		// Level gates: the level 10 quests alone lift a level 10 Cleric to 11, the level of Q2207, Q2208 and Q24011.
		// Every Leg 1 quest together does not reach 12, so Leg 1 sets no level target beyond 11.
		long[] levels = XDocument.Load(Data("player_experience_table.xml")).Root!.Elements("exp").Select(node => (long)node).ToArray();
		long levelTen = levels[contract.Start.Level - 1];
		long levelTenQuests = contract.Quests.Where(quest => quest.MinimumLevel == contract.Start.Level).Sum(quest => (long)quest.RewardExperience);
		Assert.True(levelTen + levelTenQuests >= levels[contract.Endpoint.MinimumLevel - 1]);
		Assert.Equal(contract.Endpoint.MinimumLevel, contract.Quests.Max(quest => quest.MinimumLevel));
		Assert.True(levelTen + contract.Quests.Sum(quest => (long)quest.RewardExperience) < levels[contract.Endpoint.MinimumLevel]);

		// Q24011 is the campaign that follows Q24010; the snapshot holds it LOCKED until level 11.
		NaturalAltgardQuest campaign = Assert.Single(contract.Quests, quest => quest.Category == "MISSION");
		Assert.Equal(new[] { campaign.Id }, contract.Start.LockedQuestIds);
		Assert.Null(campaign.StartNpcId);
		Assert.Contains("DefaultOnQuestCompletedEvent(env, 24010)", Handler(campaign.Handler), StringComparison.Ordinal);
		Assert.Contains("DefaultOnLevelChangedEvent(player, 24010)", Handler(campaign.Handler), StringComparison.Ordinal);

		Assert.Equal(contract.Order.Order(), contract.Endpoint.CompletedQuestIds.Order());
		Assert.Equal(contract.Start.MapId, contract.Endpoint.MapId);
		Assert.True(contract.Endpoint.InHub && contract.Endpoint.Alive);
		foreach (NaturalAltgardExclusion excluded in contract.Excluded)
			Assert.Equal(12, (int?)Quest(questData, excluded.Id).Attribute("minlevel_permitted"));
	}

	[Fact]
	public void TemplateQuestsHaveCompiledPlansWhoseTargetsAreOnTheIceLake()
	{
		NaturalAltgardContract contract = NaturalAltgardContract.LoadDefault();
		IReadOnlyDictionary<int, QuestRunPlan> plans = NaturalAltgardContract.LoadPlans();
		Assert.Equal(contract.Quests.Where(quest => quest.IsTemplate).Select(quest => quest.Id).Order(), plans.Keys.Order());

		XElement scripts = XDocument.Load(Data("quest_script_data", "altgard.xml")).Root!;
		NaturalAltgardArea lake = contract.Area("ice-lake");
		foreach (NaturalAltgardQuest quest in contract.Quests.Where(quest => quest.IsTemplate))
		{
			QuestRunPlan plan = plans[quest.Id];
			Assert.Equal(quest.Template, plan.Template);
			XElement script = Assert.Single(scripts.Elements(quest.Template!), node => (int?)node.Attribute("id") == quest.Id);
			Assert.Equal(quest.StartNpcId, (int?)script.Attribute("start_npc_ids"));
			Assert.Equal(quest.StartNpcId, Assert.Single(plan.StartNpcs).Id);
			Assert.Equal(quest.MinimumLevel, plan.MinimumLevel);
			Assert.Equal("ice-lake", quest.Area);
			Assert.True(Distance(Assert.Single(Assert.Single(plan.StartNpcs).Positions), contract.Hub.Anchor) <= contract.Hub.Radius);

			// Every kill target and drop source has spawns on the Ice Lake, the fortress's own hunting ground.
			QuestRunNpc[] targets = plan.Steps.Where(step => step.Kind is "kill" or "collect").SelectMany(step => step.Npcs.Concat(step.Sources.Select(source => source.Npc).OfType<QuestRunNpc>())).ToArray();
			Assert.NotEmpty(targets);
			Assert.All(targets, target => Assert.Contains(target.Positions, at => at.MapId == contract.Hub.MapId && lake.Contains(at.X, at.Y, at.Z)));
			Assert.All(QuestRunBook.Build(plan).Operations, operation => Assert.Contains(operation.Kind, new[]
			{
				QuestRunOperationKind.Prepare, QuestRunOperationKind.StartAtNpc, QuestRunOperationKind.Kill,
				QuestRunOperationKind.CollectQuestDrop, QuestRunOperationKind.Report, QuestRunOperationKind.ClaimReward,
			}));
		}
	}

	[Fact]
	public void ScriptedStepsMatchSpawnsTalkRangesHandlersAndAreas()
	{
		NaturalAltgardContract contract = NaturalAltgardContract.LoadDefault();
		XElement templates = XDocument.Load(Data("npcs", "npc_templates.xml")).Root!;
		XElement[] flyZone = FlyZonePoints(contract.RequiredFlight.Zones[0].Name);
		foreach (NaturalAltgardStep step in contract.Steps)
		{
			NaturalAltgardQuest quest = contract.Quest(step.QuestId);
			Assert.False(quest.IsTemplate);
			string handler = Handler(quest.Handler);
			// Q24011 registers its talk NPCs from an array, so the id is checked as text, not as a call.
			Assert.Contains(step.NpcId.ToString(CultureInfo.InvariantCulture), handler[..handler.IndexOf("OnDialogEvent", StringComparison.Ordinal)], StringComparison.Ordinal);
			XElement template = Assert.Single(templates.Elements("npc_template"), node => (int?)node.Attribute("npc_id") == step.NpcId);
			Assert.Equal(step.TalkRange, (int)template.Element("talk_info")!.Attribute("distance")! + 1);
			AssertSpawn(step.NpcId, step.Position);

			// Offers, talks and reward selections are generic; every other action is a branch in the handler.
			foreach (string action in step.Actions.Where(action => action is not ("USE_OBJECT" or "QUEST_ACCEPT_1") && !action.Contains("REWARD", StringComparison.Ordinal)))
				Assert.Contains($"DialogAction.{action}", handler, StringComparison.Ordinal);
			foreach (int page in step.Pages)
				Assert.Contains($"SendQuestDialog(env, {page})", handler, StringComparison.Ordinal);
			if (step.MovieId is { } movie)
				Assert.Contains($"PlayQuestMovie(env, {movie})", handler, StringComparison.Ordinal);
			if (step.ReceivesItemId is { } item)
				Assert.Contains($"GiveQuestItem(env, {item}, 1)", handler, StringComparison.Ordinal);
			if (step.Area is { } area)
				Assert.True(contract.Area(area).Contains(step.Position[0], step.Position[1], step.Position[2]), step.Key);
			else
				Assert.True(Distance(step.Position, contract.Hub.Anchor) <= contract.Hub.Radius, step.Key);
			// Flight steps are above the fortress inside its FLY zone; nothing else needs a flight.
			Assert.Equal(step.Area == "borender-rock", step.Flight);
			if (step.Flight)
				Assert.True(InPolygon(flyZone, step.Position[0], step.Position[1]) && step.Position[2] <= contract.RequiredFlight.Zones[0].Top);
		}

		// Every scripted quest is offered by an NPC except the campaign, and every one is finished at an NPC.
		foreach (NaturalAltgardQuest quest in contract.Quests.Where(quest => !quest.IsTemplate))
		{
			NaturalAltgardStep[] steps = contract.StepsFor(quest.Id).ToArray();
			Assert.Equal(quest.StartNpcId, steps.SingleOrDefault(step => step.ExpectedStatus == "OFFER")?.NpcId);
			Assert.Contains(steps, step => step.ExpectedStatus == "REWARD" || step.Actions.Contains("SELECT_QUEST_REWARD"));
		}

		// D26: Borender's Q2209 step exists only because the C# handler registers him for the talk event.
		NaturalAltgardStep borender = Assert.Single(contract.Steps, step => step.Correction != null);
		Assert.Equal(("D26", 2209, 203572), (borender.Correction, borender.QuestId, borender.NpcId));
		Assert.Contains("qe.RegisterQuestNpc(203572).AddOnTalkEvent(questId);", Handler("altgard/_2209TheScribbler.cs"), StringComparison.Ordinal);

		// The dungeon lies below the FLY zone's floor, so no dungeon step can be flown to.
		Assert.True(contract.Area("fortress-dungeon").Max[2] < contract.RequiredFlight.Zones[0].Bottom);
	}

	[Fact]
	public void ItemUseAirKillsRewardChoiceAndFlightMatchShippedData()
	{
		NaturalAltgardContract contract = NaturalAltgardContract.LoadDefault();

		// Q2208: the Mau Secret Remedy is handed out on acceptance and works anywhere, three seconds, var 0 -> 1.
		string mau = Handler(contract.Quest(contract.RequiredItemUse.QuestId).Handler);
		Assert.Contains($"GiveQuestItem(env, {contract.RequiredItemUse.ItemId}, 1)", mau, StringComparison.Ordinal);
		Assert.Contains($"qe.RegisterQuestItem({contract.RequiredItemUse.ItemId}, questId)", mau, StringComparison.Ordinal);
		Assert.Contains($"}}, {contract.RequiredItemUse.UseMillis}L);", mau, StringComparison.Ordinal);
		Assert.Contains($"qs.SetQuestVarById(0, {contract.RequiredItemUse.NextVar})", mau, StringComparison.Ordinal);
		Assert.True(contract.RequiredItemUse.Anywhere);
		Assert.DoesNotContain("IsInsideZone", mau, StringComparison.Ordinal);

		// Q24011: every Abyss Fungus floats over the fortress; the kills from var 2 run to the reward at var 6.
		string fungus = Handler(contract.Quest(contract.RequiredAirKills.QuestId).Handler);
		Assert.Contains($"qe.RegisterQuestNpc({contract.RequiredAirKills.NpcId}).AddOnKillEvent(questId)", fungus, StringComparison.Ordinal);
		Assert.Contains($"var > 0 && var < {contract.RequiredAirKills.RewardVar}", fungus, StringComparison.Ordinal);
		Assert.Contains($"ChangeQuestStep(env, {contract.RequiredAirKills.RewardVar}, {contract.RequiredAirKills.RewardVar}, true)", fungus, StringComparison.Ordinal);
		Assert.Equal(contract.RequiredAirKills.RewardVar - contract.RequiredAirKills.FromVar + 1, contract.RequiredAirKills.KillsAfterBorender);
		Assert.Equal(contract.RequiredAirKills.FromVar, contract.Steps.Single(step => step.Key == "q24011-v1-borender").Var + 1);
		XElement[] spots = Spawns().Descendants("spawn").Where(spawn => (int?)spawn.Attribute("npc_id") == contract.RequiredAirKills.NpcId).Elements("spot").ToArray();
		Assert.Equal(contract.RequiredAirKills.SpawnCount, spots.Length);
		XElement[] flyZone = FlyZonePoints(contract.RequiredFlight.Zones[0].Name);
		Assert.All(spots, spot =>
		{
			(float x, float y, float z) = ((float)spot.Attribute("x")!, (float)spot.Attribute("y")!, (float)spot.Attribute("z")!);
			Assert.True(contract.Area("fungus-air").Contains(x, y, z) && InPolygon(flyZone, x, y));
		});

		// The reward: the Cleric's chain boots, found by its position in the selectable list.
		XElement quest = Quest(XDocument.Load(Data("quest_data", "quest_data.xml")).Root!, contract.RewardChoice.QuestId);
		int[] selectable = quest.Descendants("selectable_reward_item").Select(node => (int)node.Attribute("item_id")!).ToArray();
		int index = NaturalAscensionContract.DialogActionId(contract.RewardChoice.Action) - DialogAction.SELECTED_QUEST_REWARD1;
		Assert.Equal(contract.RewardChoice.ItemId, selectable[index]);
		XElement item = Assert.Single(XDocument.Load(Data("items", "item_templates.xml")).Root!.Elements("item_template"),
			node => (int?)node.Attribute("id") == contract.RewardChoice.ItemId);
		Assert.Equal(contract.RewardChoice.ItemGroup, (string?)item.Attribute("item_group"));
		Assert.Contains(contract.RewardChoice.Action, contract.Steps.Single(step => step.Key == "q24011-reward-valurion").Actions);

		// Work items the handlers declare but never hand out.
		foreach ((int questId, int workItem) in contract.UnhandedWorkItemIds)
		{
			Assert.Equal(workItem, (int?)quest.Document!.Root!.Elements("quest").Single(node => (int?)node.Attribute("id") == questId)
				.Descendants("quest_work_item").Single().Attribute("item_id"));
			Assert.DoesNotContain(workItem.ToString(CultureInfo.InvariantCulture), Handler(contract.Quest(questId).Handler), StringComparison.Ordinal);
		}

		// Flight: the only two FLY zones in Altgard, the map's water level, the reuse delay and the base flight time.
		XElement[] zones = XDocument.Load(Data("zones", "zones_220030000.xml")).Root!.Elements("zone")
			.Where(zone => (string?)zone.Attribute("zone_type") == "FLY").ToArray();
		Assert.Equal(contract.RequiredFlight.Zones.Select(zone => zone.Name).Order(), zones.Select(zone => (string)zone.Attribute("name")!).Order());
		foreach (NaturalAltgardFlightZone expected in contract.RequiredFlight.Zones)
		{
			XElement points = zones.Single(zone => (string?)zone.Attribute("name") == expected.Name).Element("points")!;
			Assert.Equal((expected.Bottom, expected.Top), ((float)points.Attribute("bottom")!, (float)points.Attribute("top")!));
		}
		Assert.True(InPolygon(flyZone, contract.Hub.Anchor[0], contract.Hub.Anchor[1]) && contract.Hub.Anchor[2] >= contract.RequiredFlight.Zones[0].Bottom);
		XElement map = Assert.Single(XDocument.Load(Data("world_maps.xml")).Root!.Elements("map"), node => (int?)node.Attribute("id") == contract.Hub.MapId);
		Assert.Equal(contract.RequiredFlight.WaterLevel, (float)map.Attribute("water_level")!);
		Assert.Contains($"FLY_REUSE_TIME = {contract.RequiredFlight.ReuseMillis};",
			File.ReadAllText(Path.Combine(Root(), "src/Aion.GameServer/Controllers/FlyController.cs")), StringComparison.Ordinal);
		Assert.Equal(contract.RequiredFlight.MaxFlightTime, CustomConfig.BASE_FLYTIME);
		Assert.Contains($"gameserver.base.flytime = {contract.RequiredFlight.MaxFlightTime}",
			File.ReadAllText(Path.Combine(Root(), "game-server/config/main/custom.properties")), StringComparison.Ordinal);
	}

	[Fact]
	public void ContractJavaReferenceMatchesThePortState()
	{
		using var state = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "docs/upstream-port-state.json")));
		Assert.Equal(NaturalAltgardContract.LoadDefault().JavaReference, state.RootElement.GetProperty("lastCompletedJavaCommit").GetString());
	}

	private static XElement[] FlyZonePoints(string name) =>
		XDocument.Load(Data("zones", "zones_220030000.xml")).Root!.Elements("zone")
			.Single(zone => (string?)zone.Attribute("name") == name).Element("points")!.Elements("point").ToArray();

	private static bool InPolygon(XElement[] points, float x, float y)
	{
		bool inside = false;
		for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
		{
			float xi = (float)points[i].Attribute("x")!, yi = (float)points[i].Attribute("y")!;
			float xj = (float)points[j].Attribute("x")!, yj = (float)points[j].Attribute("y")!;
			if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
				inside = !inside;
		}
		return inside;
	}

	private static float Distance(QuestRunPosition at, float[] anchor) => Distance([at.X, at.Y, at.Z], anchor);

	private static float Distance(float[] at, float[] anchor) =>
		MathF.Sqrt(MathF.Pow(at[0] - anchor[0], 2) + MathF.Pow(at[1] - anchor[1], 2));

	private static void AssertSpawn(int npcId, float[] position) =>
		Assert.Contains(Spawns().Descendants("spawn").Where(spawn => (int?)spawn.Attribute("npc_id") == npcId).Elements("spot"), spot =>
			MathF.Abs((float)spot.Attribute("x")! - position[0]) < 0.01f &&
			MathF.Abs((float)spot.Attribute("y")! - position[1]) < 0.01f &&
			MathF.Abs((float)spot.Attribute("z")! - position[2]) < 0.01f);

	private static XElement Spawns() => XDocument.Load(Data("spawns", SpawnFile)).Root!;
	private static XElement Quest(XElement root, int id) => Assert.Single(root.Elements("quest"), quest => (int?)quest.Attribute("id") == id);
	private static string Handler(string relative) => File.ReadAllText(Path.Combine(Root(), "src/Aion.GameServer/Handlers/Quest", relative));
	private static string Data(params string[] parts) => Path.Combine([Path.Combine(Root(), "game-server/data/static_data"), .. parts]);
	private static string Root() => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
}
