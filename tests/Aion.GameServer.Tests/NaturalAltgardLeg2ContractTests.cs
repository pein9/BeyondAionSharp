using System.Globalization;
using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

/// <summary>AM-01: the Altgard Leg 2 (Moslan Crossroad) contract against shipped data, its plans and the C# handlers.</summary>
public sealed class NaturalAltgardLeg2ContractTests
{
	private static readonly NaturalAltgardContract Leg2 = NaturalAltgardContract.LoadLeg("l2");

	[Fact]
	public void Leg2StartsWhereLeg1EndsAndPinsQuestDataChainsAndTheEndpoint()
	{
		NaturalAltgardContract leg1 = NaturalAltgardContract.LoadLeg("l1");
		NaturalAscensionContract bridge = NaturalAscensionContract.LoadDefault();
		Assert.Equal(leg1.JavaReference, Leg2.JavaReference);
		Assert.Equal((Leg2.Start.Contract, Leg2.Start.Snapshot), ("natural-altgard-contract.json", leg1.Endpoint.Snapshot));
		Assert.Equal(bridge.Endpoint.CompletedQuestIds.Concat(leg1.Endpoint.CompletedQuestIds).Order(), Leg2.Start.CompletedQuestIds.Order());
		Assert.Equal((leg1.Start.MapId, leg1.Start.Race, leg1.Start.Class, leg1.Start.BindNpcId),
			(Leg2.Start.MapId, Leg2.Start.Race, Leg2.Start.Class, Leg2.Start.BindNpcId));
		Assert.True(Leg2.Start.Level >= leg1.Endpoint.MinimumLevel);
		// The town is Leg 1's hub: the bind, rests in town and restocks stay at the fortress (AM-Q2).
		Assert.Equal((leg1.Hub.Key, leg1.Hub.MapId, leg1.Hub.Radius), (Leg2.Town!.Key, Leg2.Town.MapId, Leg2.Town.Radius));
		Assert.Equal(leg1.Hub.Anchor, Leg2.Town.Anchor);

		XElement questData = XDocument.Load(Data("quest_data", "quest_data.xml")).Root!;
		foreach (NaturalAltgardQuest expected in Leg2.Quests)
		{
			XElement quest = Quest(questData, expected.Id);
			Assert.Equal(expected.Category, (string?)quest.Attribute("category"));
			Assert.Equal(expected.MinimumLevel, (int?)quest.Attribute("minlevel_permitted"));
			Assert.Equal(Leg2.Start.Race, (string?)quest.Attribute("race_permitted"));
			Assert.All(quest.Elements("rewards"), reward => Assert.Equal(expected.RewardExperience, (int?)reward.Attribute("exp")));
			int[] finished = quest.Element("start_conditions")?.Elements("finished").Select(node => (int)node.Attribute("quest_id")!).ToArray() ?? [];
			Assert.Equal(expected.Prerequisite is { } prerequisite ? [prerequisite] : [], finished);
			// Every Leg 2 quest is open from the start level: no level gate is left (the snapshot is level 13).
			Assert.True(expected.MinimumLevel <= Leg2.Start.Level);
		}
		for (int index = 0; index < Leg2.Order.Length; index++)
			if (Leg2.Quest(Leg2.Order[index]).Prerequisite is int before)
				Assert.True(Array.IndexOf(Leg2.Order, before) is int at && at >= 0 && at < index, $"Q{Leg2.Order[index]} before Q{before}");

		// Q24012 follows Q24011 (Leg 1) and is already started in the snapshot.
		NaturalAltgardQuest campaign = Assert.Single(Leg2.Quests, quest => quest.Category == "MISSION");
		Assert.Equal(new[] { campaign.Id }, Leg2.Start.StartedQuestIds);
		Assert.Contains("DefaultOnQuestCompletedEvent(env, 24010)", Handler(campaign.Handler), StringComparison.Ordinal);

		// AM-Q1: Leg 2 ends at Manir's Campsite, where Q2215 is handed in.
		Assert.Equal(Leg2.Order.Order(), Leg2.Endpoint.CompletedQuestIds.Order());
		Assert.False(Leg2.Endpoint.InHub);
		AssertSpawn(203607, Leg2.Endpoint.Anchor!);
		Assert.True(Leg2.Area("manirs-campsite").Contains(Leg2.Endpoint.Anchor![0], Leg2.Endpoint.Anchor[1], Leg2.Endpoint.Anchor[2]));
		Assert.Empty(Leg2.Excluded);
		Assert.Null(Leg2.Flight);
		Assert.Null(Leg2.ItemUse);
		Assert.Null(Leg2.AirKills);
	}

	[Fact]
	public void TemplateQuestsHaveCompiledPlansWhoseNpcsAndTargetsAreOnTheirGrounds()
	{
		IReadOnlyDictionary<int, QuestRunPlan> plans = NaturalAltgardContract.LoadPlans("l2");
		Assert.Equal(Leg2.Quests.Where(quest => quest.IsTemplate).Select(quest => quest.Id).Order(), plans.Keys.Order());
		XElement scripts = XDocument.Load(Data("quest_script_data", "altgard.xml")).Root!;
		NaturalAltgardArea crossroad = Leg2.Area("crossroad");
		foreach (NaturalAltgardQuest quest in Leg2.Quests.Where(quest => quest.IsTemplate))
		{
			QuestRunPlan plan = plans[quest.Id];
			Assert.Equal(quest.Template, plan.Template);
			XElement script = Assert.Single(scripts.Elements(quest.Template!), node => (int?)node.Attribute("id") == quest.Id);
			Assert.Equal(quest.StartNpcId, (int?)script.Attribute("start_npc_ids"));
			Assert.Equal(quest.StartNpcId, Assert.Single(plan.StartNpcs).Id);
			// Everything but Q2210 (Rion, in the fortress) is taken at the crossroad.
			QuestRunPosition start = Assert.Single(Assert.Single(plan.StartNpcs).Positions);
			if (quest.Id == 2210)
				Assert.True(Distance([start.X, start.Y, start.Z], Leg2.Town!.Anchor) <= Leg2.Town.Radius);
			else
				Assert.True(crossroad.Contains(start.X, start.Y, start.Z), $"Q{quest.Id} starter at ({start.X}, {start.Y})");

			NaturalAltgardArea ground = Leg2.Area(quest.Area!);
			QuestRunNpc[] targets = plan.Steps.Where(step => step.Kind is "kill" or "collect")
				.SelectMany(step => step.Npcs.Concat(step.Sources.Select(source => source.Npc).OfType<QuestRunNpc>())).ToArray();
			Assert.All(targets, target => Assert.Contains(target.Positions, at => at.MapId == Leg2.Hub.MapId && ground.Contains(at.X, at.Y, at.Z)));
			Assert.All(QuestRunBook.Build(plan).Operations, operation => Assert.Contains(operation.Kind, new[]
			{
				QuestRunOperationKind.Prepare, QuestRunOperationKind.StartAtNpc, QuestRunOperationKind.Kill,
				QuestRunOperationKind.CollectQuestDrop, QuestRunOperationKind.UseQuestObject, QuestRunOperationKind.Report,
				QuestRunOperationKind.ClaimReward,
			}));
		}
		// The two deliveries end at Loriniah and at Manir.
		Assert.Equal(203605, Assert.Single(plans[2210].EndNpcs).Id);
		Assert.Equal(203607, Assert.Single(plans[2215].EndNpcs).Id);
	}

	[Fact]
	public void ScriptedStepsObjectsZoneCollectionsPoisonAndRewardMatchTheHandlers()
	{
		XElement templates = XDocument.Load(Data("npcs", "npc_templates.xml")).Root!;
		foreach (NaturalAltgardStep step in Leg2.Steps)
		{
			string handler = Handler(Leg2.Quest(step.QuestId).Handler);
			Assert.Contains($"RegisterQuestNpc({step.NpcId})", handler, StringComparison.Ordinal);
			XElement template = Assert.Single(templates.Elements("npc_template"), node => (int?)node.Attribute("npc_id") == step.NpcId);
			Assert.Equal(step.TalkRange, (int)template.Element("talk_info")!.Attribute("distance")! + 1);
			AssertSpawn(step.NpcId, step.Position);
			Assert.True(Leg2.Area(step.Area!).Contains(step.Position[0], step.Position[1], step.Position[2]), step.Key);
			foreach (string action in step.Actions.Where(action => action is not ("USE_OBJECT" or "QUEST_ACCEPT_1") && !action.Contains("REWARD", StringComparison.Ordinal)))
				Assert.Contains($"DialogAction.{action}", handler, StringComparison.Ordinal);
			foreach (int page in step.Pages.Where(page => page != 5))
				Assert.Contains($"SendQuestDialog(env, {page})", handler, StringComparison.Ordinal);
			if (step.MovieId is { } movie)
				Assert.Contains($"PlayQuestMovie(env, {movie})", handler, StringComparison.Ordinal);
			Assert.False(step.Flight);
		}
		// Q24012's hand-in: CHECK_USER_HAS_QUEST_ITEM goes to var 6 and REWARD, and opens page 5 (the reward window).
		Assert.Contains("CheckQuestItems(env, 5, 6, true, 5, 10001)", Handler("altgard/_24012AnOminousCrop.cs"), StringComparison.Ordinal);

		// Q2213: the Okaru Tree's loot is the log, and getting it moves var 0 -> 1 and poisons.
		string poisonRoot = Handler("altgard/_2213PoisonRootPotentFruit.cs");
		NaturalAltgardObjectUse tree = Leg2.ObjectUseList.Single(use => use.QuestId == 2213);
		Assert.Contains($"qe.AddHandlerSideQuestDrop(questId, {tree.NpcId}, {tree.LootItemId}, 1, 100)", poisonRoot, StringComparison.Ordinal);
		Assert.Contains($"qe.RegisterOnGetItem({tree.LootItemId}, questId)", poisonRoot, StringComparison.Ordinal);
		Assert.Contains($"DefaultOnGetItemEvent(env, {tree.FromVar}, {tree.ToVar}, false)", poisonRoot, StringComparison.Ordinal);
		AssertSpawn(tree.NpcId, null, Leg2.Area(tree.Area!));
		NaturalAltgardPoison poison = Assert.Single(Leg2.PoisonList);
		Assert.Contains($"ApplyEffectDirectly({poison.SkillId}, player, player)", poisonRoot, StringComparison.Ordinal);
		Assert.Contains($"RemoveEffect({poison.SkillId})", poisonRoot, StringComparison.Ordinal);
		Assert.Contains(Leg2.Steps.Single(step => step.Key == poison.RemovedByStep).Actions, action => action == "SELECT_QUEST_REWARD");
		XElement skill = Assert.Single(XDocument.Load(Data("skills", "skill_templates.xml")).Root!.Elements("skill_template"),
			node => (int?)node.Attribute("skill_id") == poison.SkillId);
		XElement dot = skill.Descendants("poison").Single();
		Assert.Equal((poison.Damage, poison.TickMillis, poison.DurationMillis),
			((int)dot.Attribute("value")!, (int)dot.Attribute("checktime")!, (int)dot.Attribute("duration2")!));
		Assert.Equal(-poison.SpeedDown, (int)skill.Descendants("change").Single(change => (string?)change.Attribute("stat") == "SPEED").Attribute("value")!);
		Assert.Equal(poison.Dispellable, (int?)skill.Attribute("req_dispel_level") < 99);

		// Q24012: the farmland zone step, three cart uses (each cart gone once used), and the drops from var 5.
		string crop = Handler("altgard/_24012AnOminousCrop.cs");
		NaturalAltgardZoneStep zone = Assert.Single(Leg2.ZoneStepList);
		Assert.Contains($"RegisterOnEnterZone(ZoneName.Get(\"{zone.Zone}\"), questId)", crop, StringComparison.Ordinal);
		Assert.Contains($"ChangeQuestStep(env, {zone.FromVar}, {zone.ToVar})", crop, StringComparison.Ordinal);
		Assert.Contains(XDocument.Load(Data("zones", "zones_220030000.xml")).Root!.Elements("zone"), node => (string?)node.Attribute("name") == zone.Zone);
		NaturalAltgardObjectUse carts = Leg2.ObjectUseList.Single(use => use.QuestId == 24012);
		Assert.Contains($"var >= {carts.FromVar} && var < {carts.ToVar}", crop, StringComparison.Ordinal);
		Assert.Contains("UseQuestObject(env, var, var + 1, false, true)", crop, StringComparison.Ordinal);
		Assert.Equal(carts.ToVar - carts.FromVar, carts.Uses);
		XElement cartSpawn = Assert.Single(Spawns().Descendants("spawn"), spawn => (int?)spawn.Attribute("npc_id") == carts.NpcId);
		Assert.Equal(carts.RespawnSeconds, (int?)cartSpawn.Attribute("respawn_time"));
		Assert.Equal(carts.SpawnCount, cartSpawn.Elements("spot").Count());
		Assert.True(carts.SpawnCount >= carts.Uses);
		Assert.All(cartSpawn.Elements("spot"), spot => Assert.True(Leg2.Area(carts.Area!).Contains(
			(float)spot.Attribute("x")!, (float)spot.Attribute("y")!, (float)spot.Attribute("z")!)));
		NaturalAltgardCollection collection = Assert.Single(Leg2.CollectionList);
		Assert.Equal(carts.ToVar, collection.AtVar);
		XElement quest = Quest(XDocument.Load(Data("quest_data", "quest_data.xml")).Root!, collection.QuestId);
		foreach (NaturalAltgardCollectedItem item in collection.Items)
		{
			Assert.Equal(item.Count, (int?)quest.Descendants("collect_item").Single(node => (int?)node.Attribute("item_id") == item.ItemId).Attribute("count"));
			int[] sources = quest.Descendants("quest_drop").Where(drop => (int?)drop.Attribute("item_id") == item.ItemId)
				.Select(drop => (int)drop.Attribute("npc_id")!).ToArray();
			Assert.Equal(item.SourceNpcIds.Order(), sources.Order());
			Assert.All(quest.Descendants("quest_drop").Where(drop => (int?)drop.Attribute("item_id") == item.ItemId),
				drop => Assert.Equal(collection.AtVar, (int?)drop.Attribute("collecting_step")));
		}

		// The reward: the Cleric's chain hauberk, found by its place in the selectable list.
		int[] selectable = quest.Descendants("selectable_reward_item").Select(node => (int)node.Attribute("item_id")!).ToArray();
		int index = NaturalAscensionContract.DialogActionId(Leg2.RewardChoice.Action) - DialogAction.SELECTED_QUEST_REWARD1;
		Assert.Equal(Leg2.RewardChoice.ItemId, selectable[index]);
		Assert.Equal(Leg2.RewardChoice.ItemGroup, (string?)Assert.Single(XDocument.Load(Data("items", "item_templates.xml")).Root!
			.Elements("item_template"), node => (int?)node.Attribute("id") == Leg2.RewardChoice.ItemId).Attribute("item_group"));
		Assert.Contains(Leg2.RewardChoice.Action, Leg2.Steps.Single(step => step.Key == "q24012-v5-loriniah").Actions);
	}

	private static float Distance(float[] a, float[] b) => MathF.Sqrt(MathF.Pow(a[0] - b[0], 2) + MathF.Pow(a[1] - b[1], 2));

	private static void AssertSpawn(int npcId, float[]? position, NaturalAltgardArea? within = null) =>
		Assert.Contains(Spawns().Descendants("spawn").Where(spawn => (int?)spawn.Attribute("npc_id") == npcId).Elements("spot"), spot =>
		{
			(float x, float y, float z) = ((float)spot.Attribute("x")!, (float)spot.Attribute("y")!, (float)spot.Attribute("z")!);
			return position is { } at
				? MathF.Abs(x - at[0]) < 0.01f && MathF.Abs(y - at[1]) < 0.01f && MathF.Abs(z - at[2]) < 0.01f
				: within!.Contains(x, y, z);
		});

	private static XElement Spawns() => XDocument.Load(Data("spawns", "Npcs/220030000_Altgard.xml")).Root!;
	private static XElement Quest(XElement root, int id) => Assert.Single(root.Elements("quest"), quest => (int?)quest.Attribute("id") == id);
	private static string Handler(string relative) => File.ReadAllText(Path.Combine(Root(), "src/Aion.GameServer/Handlers/Quest", relative));
	private static string Data(params string[] parts) => Path.Combine([Path.Combine(Root(), "game-server/data/static_data"), .. parts]);
	private static string Root() => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
}
