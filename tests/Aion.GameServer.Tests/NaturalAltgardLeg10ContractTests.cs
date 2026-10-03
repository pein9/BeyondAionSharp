using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

public sealed class NaturalAltgardLeg10ContractTests
{
	private static readonly NaturalAltgardContract Leg = NaturalAltgardContract.LoadLeg("l10");
	private static readonly IReadOnlyDictionary<int, QuestRunPlan> Plans = NaturalAltgardContract.LoadPlans("l10");
	private static readonly IReadOnlyDictionary<int, NaturalTemplateObjective> Objectives = NaturalTemplateObjective.From(Plans);
	private static string Data(params string[] path) => Path.Combine([RealStaticData.RepoRoot(), "game-server", "data", "static_data", .. path]);

	[Fact]
	public void StartRetainsLeg9AndTheFortressEndpointFinishesEveryApprovedQuest()
	{
		NaturalAltgardContract previous = NaturalAltgardContract.LoadLeg("l9");
		Assert.Equal(previous.Start.CompletedQuestIds.Concat(previous.Endpoint.CompletedQuestIds).Order(), Leg.Start.CompletedQuestIds.Order());
		Assert.Equal(("natural-altgard-l9-contract.json", "altgard-l9", 22, 700067),
			(Leg.Start.Contract, Leg.Start.Snapshot, Leg.Start.Level, Leg.Start.BindNpcId));
		Assert.Equal([2900, 24014, 24015], Leg.Start.StartedQuestIds!);
		Assert.Equal([24016], Leg.Start.LockedQuestIds);
		Assert.Equal(new[] { 2273, 2277, 2280, 2281, 2282, 2283, 24014, 24015, 24016 }, Leg.Order);
		Assert.Equal(Leg.Order, Leg.Endpoint.CompletedQuestIds);
		Assert.Equal((959700, "altgard-l10", 22, 700065, 60f),
			(Leg.Quests.Sum(q => q.RewardExperience), Leg.Endpoint.Snapshot, Leg.Endpoint.MinimumLevel, Leg.Endpoint.BindNpcId, Leg.Endpoint.Radius));
		Assert.Empty(Leg.Endpoint.HeldQuestIds!);
		Assert.Equal(2900, Assert.Single(Leg.Excluded).Id);
		Assert.Equal(Leg.Bind!.Position, Leg.Endpoint.Anchor);
		Assert.Equal(203579, Leg.Town!.VendorNpcId);
		XElement bind = Assert.Single(XDocument.Load(Data("bind_points", "bind_points.xml")).Descendants("bind_point"),
			n => (int?)n.Attribute("npcid") == Leg.Bind.NpcId);
		Assert.Equal((int)bind.Attribute("price")!, Leg.Bind.Price);
	}

	[Fact]
	public void PlansAndCampaignFactsMatchTheShippedSourcesRewardsAndZone()
	{
		Assert.Equal(Leg.Quests.Where(q => q.IsTemplate).Select(q => q.Id).Order(), Plans.Keys.Order());
		XElement data = XDocument.Load(Data("quest_data", "quest_data.xml")).Root!;
		XElement scripts = XDocument.Load(Data("quest_script_data", "altgard.xml")).Root!;
		foreach (NaturalAltgardQuest expected in Leg.Quests)
		{
			XElement quest = Assert.Single(data.Elements("quest"), n => (int?)n.Attribute("id") == expected.Id);
			Assert.Equal((expected.Category, expected.MinimumLevel, expected.RewardExperience),
				((string?)quest.Attribute("category"), (int)quest.Attribute("minlevel_permitted")!, (int)quest.Element("rewards")!.Attribute("exp")!));
			if (!expected.IsTemplate) continue;
			XElement script = Assert.Single(scripts.Elements(expected.Template!), n => (int?)n.Attribute("id") == expected.Id);
			Assert.Equal(expected.StartNpcId, (int?)script.Attribute("start_npc_ids"));
			Assert.Equal(expected.Template, Plans[expected.Id].Template);
			Assert.Equal(expected.PrerequisiteList.Order(), quest.Elements("start_conditions").Elements("finished")
				.Select(n => (int)n.Attribute("quest_id")!).Order());
			NaturalAltgardArea area = Leg.Area(expected.Area!);
			foreach (QuestRunNpc npc in Plans[expected.Id].Steps.Where(s => s.Kind is "kill" or "collect")
				.SelectMany(s => s.Npcs.Concat(s.Sources.Select(source => source.Npc).OfType<QuestRunNpc>())))
				if (npc.Positions.Any(p => p.MapId == Leg.Hub.MapId))
					Assert.Contains(npc.Positions, p => p.MapId == Leg.Hub.MapId && area.Contains(p.X, p.Y, p.Z));
		}
		Assert.Equal(new[] { 24011, 24012, 24013, 24014, 24015 }, Leg.Quest(24016).PrerequisiteList);
		NaturalAltgardZoneStep zoneStep = Assert.Single(Leg.ZoneStepList);
		XElement zone = Assert.Single(XDocument.Load(Data("zones", "zones_quest.xml")).Descendants("zone"),
			n => (string?)n.Attribute("name") == zoneStep.Zone);
		XElement sphere = zone.Element("sphere")!;
		Assert.Equal(new[] { (float)sphere.Attribute("x")!, (float)sphere.Attribute("y")!, (float)sphere.Attribute("z")! }, zoneStep.Anchor);
		Assert.Equal((float)sphere.Attribute("r")!, zoneStep.Radius);
		NaturalAltgardCollection collection = Assert.Single(Leg.CollectionList);
		XElement orb = Assert.Single(data.Elements("quest"), n => (int?)n.Attribute("id") == collection.QuestId);
		Assert.Equal(5, collection.AtVar);
		Assert.Equal(orb.Elements("quest_drop").Select(n => (int)n.Attribute("npc_id")!).Order(),
			Assert.Single(collection.Items).SourceNpcIds.Order());
		Assert.All(orb.Elements("quest_drop"), n => Assert.Equal(collection.AtVar, (int)n.Attribute("collecting_step")!));
		foreach (NaturalAltgardRewardChoice choice in Leg.RewardChoiceList)
		{
			XElement quest = Assert.Single(data.Elements("quest"), n => (int?)n.Attribute("id") == choice.QuestId);
			int index = int.Parse(choice.Action["SELECTED_QUEST_REWARD".Length..]) - 1;
			Assert.Equal(choice.ItemId, (int)quest.Element("rewards")!.Elements("selectable_reward_item").ElementAt(index).Attribute("item_id")!);
		}
		XElement items = XDocument.Load(Data("items", "item_templates.xml")).Root!;
		Assert.All(Leg.RewardChoiceList, choice => Assert.Equal(choice.ItemGroup,
			(string?)Assert.Single(items.Elements("item_template"), n => (int?)n.Attribute("id") == choice.ItemId).Attribute("item_group")));
	}

	[Fact]
	public void InstanceAndCityTripsUseTheOrdinaryShippedPortalsAndRecipients()
	{
		NaturalAltgardInstanceTrip trip = Assert.Single(Leg.InstanceTripList);
		Assert.Equal((24016, 320030000, 1, 2, 1, 210753, 13, 14, 154),
			(trip.QuestId, trip.MapId, trip.FromVar, trip.EnterVar, trip.ResetVar, trip.BossNpcId, trip.SpawnVar, trip.KillVar, trip.MovieId));
		XElement portals = XDocument.Load(Data("portals", "portal_template2.xml")).Root!;
		XElement path = Assert.Single(Assert.Single(portals.Elements("portal_use"), n => (int?)n.Attribute("npc_id") == trip.PortalNpcId).Elements("portal_path"));
		XElement gate = Assert.Single(path.Elements("quest_req"), n => (int?)n.Attribute("quest_id") == trip.QuestId);
		Assert.Equal(trip.FromVar, (int)gate.Attribute("quest_step")!);
		XElement loc = Assert.Single(XDocument.Load(Data("portals", "portal_loc.xml")).Descendants("portal_loc"),
			n => (int?)n.Attribute("loc_id") == (int)path.Attribute("loc_id")!);
		Assert.Equal(trip.MapId, (int)loc.Attribute("world_id")!);
		Assert.Equal(new[] { (float)loc.Attribute("x")!, (float)loc.Attribute("y")!, (float)loc.Attribute("z")! }, trip.Arrival);
		XElement restrictions = Assert.Single(XDocument.Load(Data("instance_cooltimes", "instance_cooltimes.xml")).Descendants("instance_cooltime"),
			n => (int?)n.Attribute("worldId") == trip.MapId && (string?)n.Attribute("race") == "ASMODIANS");
		Assert.Equal(1, (int)restrictions.Element("max_member_dark")!);
		Assert.Equal(0, (int)restrictions.Element("ent_cool_time")!);
		Assert.Equal((700089, 3000, 700184, 5000), (trip.PortalNpcId, trip.UseMillis, trip.ExitNpcId, trip.ExitUseMillis));
		XElement npcData = XDocument.Load(Data("npcs", "npc_templates.xml")).Root!;
		foreach (var (npcId, useMillis) in new[] { (trip.PortalNpcId, trip.UseMillis), (trip.ExitNpcId, trip.ExitUseMillis) })
			Assert.Equal(useMillis, 1000 * (int)Assert.Single(npcData.Elements("npc_template"), n => (int?)n.Attribute("npc_id") == npcId)
				.Element("talk_info")!.Attribute("delay")!);
		Assert.All(Leg.ObjectUseList, use => Assert.Equal(trip.MapId, use.MapId));
		Assert.Equal(trip.MapId, Leg.HuntList.Single(h => h.QuestId == trip.QuestId).MapId);
		Assert.Equal(trip.MovieId, Leg.ObjectUseList.Single(use => use.Key == "q24016-gate").MovieId);
		QuestRunNpc vidar = Assert.Single(Plans[2283].EndNpcs);
		Assert.Equal(204052, vidar.Id);
		QuestRunPosition ordinary = Assert.Single(vidar.Positions, p => !p.ConditionalEvent);
		Assert.Equal((120010000, 1275.37f, 1168.61f, 215.215f), (ordinary.MapId, ordinary.X, ordinary.Y, ordinary.Z));
		Assert.Equal((120010000, 203581, 7, 500),
			(Leg.MapTripList[0].MapId, Leg.MapTripList[0].TeleporterNpcId, Leg.MapTripList[0].LocationId, Leg.MapTripList[0].Fare));
	}

	[Theory]
	[InlineData(3, 5, 5, true)]
	[InlineData(3, 0, 0, false)]
	[InlineData(2, 5, 5, false)]
	[InlineData(3, 4, 5, false)]
	[InlineData(3, 5, 4, false)]
	public void FightersRequireEveryIndependentKillCounter(int seekers, int guards, int scratchers, bool done)
	{
		int packed = seekers | guards << 6 | scratchers << 12;
		NaturalAltgardObservation state = Observe(2281, 220030000, 3, packed);
		Assert.Equal(done, Objectives[2281].IsDone(state.Quests[2281], state.ItemCounts));
		Assert.Equal(done ? "template-claim" : "template-work", NaturalAltgardDecisionEngine.Decide(Leg, state, Objectives, 1).Action);
	}

	[Theory]
	[InlineData(24011)]
	[InlineData(24012)]
	[InlineData(24013)]
	[InlineData(24014)]
	[InlineData(24015)]
	public void FinalCampaignNeedsAllFivePreviousCampaigns(int unfinished)
	{
		NaturalAltgardObservation state = Observe(24016, 220030000, 3, 0);
		state = state with { CompletedQuestIds = state.CompletedQuestIds.Where(id => id != unfinished).ToHashSet() };
		Assert.Equal("gated", NaturalAltgardDecisionEngine.Decide(Leg, state, Objectives, 1, new HashSet<int> { 24016 }).Action);
	}

	[Theory]
	[InlineData(220030000, 3, 0, false, "talk", "q24016-v0-suthran", 220030000)]
	[InlineData(220030000, 3, 1, false, "enter-instance", null, 320030000)]
	[InlineData(320030000, 3, 2, false, "use-object", "q24016-guardian", 320030000)]
	[InlineData(320030000, 3, 1, false, "leave-instance", null, 320030000)]
	[InlineData(320030000, 3, 13, false, "hunt", null, 320030000)]
	[InlineData(320030000, 3, 14, false, "use-object", "q24016-gate", 320030000)]
	[InlineData(220030000, 4, 14, false, "talk", "q24016-reward-suthran", 220030000)]
	[InlineData(320030000, 4, 14, false, "travel-to-map", "q24016-reward-suthran", 220030000)]
	[InlineData(320030000, 3, 13, true, "revive-at-bind", null, 220030000)]
	public void InstanceBranchStaysOnItsMapAndUnprotectedDeathReturnsToTheWorkingBind(int map, byte status, int var, bool dead,
		string action, string? key, int targetMap)
	{
		NaturalAltgardObservation state = Observe(24016, map, status, var) with { IsDead = dead };
		NaturalAltgardDecision next = NaturalAltgardDecisionEngine.Decide(Leg, state, Objectives, 1);
		Assert.Equal((action, key, targetMap), (next.Action, next.StepKey, next.MapId));
	}

	[Fact]
	public void LearnedSelfRevivalKeepsTheClericInTheInstanceThenRequiresOrdinaryExitAndReentry()
	{
		NaturalAltgardObservation dead = Observe(24016, 320030000, 3, 1) with { IsDead = true, CanRebirth = true };
		NaturalAltgardDecision revive = NaturalAltgardDecisionEngine.Decide(Leg, dead, Objectives, 1);
		Assert.Equal(("revive-in-place", 320030000), (revive.Action, revive.MapId));
		Assert.Equal("leave-instance", NaturalAltgardDecisionEngine.Decide(Leg, dead with { IsDead = false }, Objectives, 2).Action);
		Assert.Equal("enter-instance", NaturalAltgardDecisionEngine.Decide(Leg, dead with { IsDead = false, MapId = 220030000 }, Objectives, 3).Action);
		Assert.Equal(new[] { 2467.6052f, 2548.0076f, 316.12375f }, Leg.Steps.Single(step => step.Key == "q24016-v0-suthran").Teleport!.Position);
	}

	private static NaturalAltgardObservation Observe(int pending, int map, byte status, int packed)
	{
		HashSet<int> completed = Leg.Start.CompletedQuestIds.Concat(Leg.Order.Where(id => id != pending)).ToHashSet();
		var quests = new Dictionary<int, BotQuestState> { [pending] = new(pending, status, packed, 0, null), [2900] = new(2900, 3, 0, 0, null) };
		float[] at = Leg.Bind!.Position;
		var position = new BotPosition(at[0], at[1], at[2], 0);
		return new(true, map, 22, false, quests, completed, position, new Dictionary<int, long>(),
			new BotBindPoint(220030000, position, 0));
	}
}
