using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

public sealed class NaturalAltgardLeg9ContractTests
{
	private static readonly NaturalAltgardContract Leg = NaturalAltgardContract.LoadLeg("l9");
	private static readonly IReadOnlyDictionary<int, QuestRunPlan> Plans = NaturalAltgardContract.LoadPlans("l9");
	private static string Data(params string[] path) => Path.Combine([RealStaticData.RepoRoot(), "game-server", "data", "static_data", .. path]);

	[Fact]
	public void StartPreservesLeg8AndEndpointHandsInBothHeldQuests()
	{
		NaturalAltgardContract previous = NaturalAltgardContract.LoadLeg("l8");
		Assert.Equal(previous.Start.CompletedQuestIds.Concat(previous.Endpoint.CompletedQuestIds).Order(), Leg.Start.CompletedQuestIds.Order());
		Assert.Equal(("natural-altgard-l8-contract.json", "altgard-l8", 700822, 21),
			(Leg.Start.Contract, Leg.Start.Snapshot, Leg.Start.BindNpcId, Leg.Start.Level));
		Assert.Equal(new[] { 2146, 24115, 2900, 24014, 24015 }.Order(), Leg.Start.StartedQuestIds!.Order());
		Assert.Equal([24016], Leg.Start.LockedQuestIds);
		Assert.Empty(Leg.Endpoint.HeldQuestIds!);
		Assert.Equal(Leg.Order.Order(), Leg.Endpoint.CompletedQuestIds.Order());
		Assert.Equal((9, 254866, "altgard-l9", 700067, 60f),
			(Leg.Order.Length, Leg.Quests.Sum(quest => quest.RewardExperience), Leg.Endpoint.Snapshot, Leg.Endpoint.BindNpcId, Leg.Endpoint.Radius));
		XElement spot = Assert.Single(XDocument.Load(Data("spawns", "Npcs", "220030000_Altgard.xml")).Descendants("spawn")
			.Where(node => (int?)node.Attribute("npc_id") == 700067).Elements("spot"));
		Assert.Equal(new[] { (float)spot.Attribute("x")!, (float)spot.Attribute("y")!, (float)spot.Attribute("z")! }, Leg.Bind!.Position);
		Assert.Equal(813, Leg.Bind.Price);
		Assert.Equal(203579, Leg.Town!.VendorNpcId);
	}

	[Fact]
	public void TemplatesAndSourcesMatchShippedQuestDataAndTheApprovedD32Plan()
	{
		XElement data = XDocument.Load(Data("quest_data", "quest_data.xml")).Root!;
		foreach (NaturalAltgardQuest expected in Leg.Quests)
		{
			XElement quest = data.Elements("quest").Single(node => (int?)node.Attribute("id") == expected.Id);
			Assert.Equal((expected.Category, expected.MinimumLevel, expected.RewardExperience),
				((string?)quest.Attribute("category"), (int)quest.Attribute("minlevel_permitted")!, (int)quest.Element("rewards")!.Attribute("exp")!));
			Assert.Equal(expected.Prerequisite is int before ? [before] : [],
				quest.Elements("start_conditions").Elements("finished").Select(node => (int)node.Attribute("quest_id")!).ToArray());
			QuestRunPlan plan = Plans[expected.Id];
			Assert.Equal((expected.Template, expected.StartNpcId), (plan.Template, Assert.Single(plan.StartNpcs).Id));
			NaturalAltgardArea area = Leg.Area(expected.Area!);
			foreach (QuestRunPosition source in plan.Steps.Where(step => step.Kind is "kill" or "collect").SelectMany(step =>
				step.Npcs.Concat(step.Sources.Select(source => source.Npc).OfType<QuestRunNpc>())).SelectMany(npc => npc.Positions))
				Assert.True(area.Contains(source.X, source.Y, source.Z), $"Q{expected.Id}: source outside {area.Key}.");
		}
		Assert.Equal(Leg.Order.Order(), Plans.Keys.Order());
		Assert.Empty(Leg.Steps);
		Assert.Equal(File.ReadAllText(Path.Combine(RealStaticData.RepoRoot(), "parity-artifacts/e2e/retail-quest-plans/24115.json")),
			File.ReadAllText(Path.Combine(RealStaticData.RepoRoot(), "parity-artifacts/e2e/natural-altgard-l9-plans/24115.json")));
	}

	[Fact]
	public void LindhelmUsesHisOrdinaryCitySpawnAndTheExistingTeleporterTrip()
	{
		QuestRunNpc recipient = Assert.Single(Plans[2258].EndNpcs);
		Assert.Equal(204190, recipient.Id);
		QuestRunPosition at = Assert.Single(recipient.Positions, position => !position.ConditionalEvent);
		Assert.Equal((120010000, 1275.69f, 1290.25f, 209.052f), (at.MapId, at.X, at.Y, at.Z));
		NaturalAltgardMapTrip trip = Assert.Single(Leg.MapTripList);
		Assert.Equal((at.MapId, 203581, 7, 500), (trip.MapId, trip.TeleporterNpcId, trip.LocationId, trip.Fare));
	}

	[Theory]
	[InlineData(220030000, false, "travel-to-map", 120010000)]
	[InlineData(120010000, false, "template-claim", 120010000)]
	[InlineData(120010000, true, "travel-to-map", 220030000)]
	public void CityTemplateClaimTravelsHandsInAndReturns(int map, bool delivered, string action, int targetMap)
	{
		HashSet<int> completed = Leg.Start.CompletedQuestIds.Concat(Leg.Order.Where(id => delivered || id != 2258)).ToHashSet();
		var quests = new Dictionary<int, BotQuestState> { [2258] = new(2258, delivered ? (byte)5 : (byte)3, 0, 0, null) };
		float[] home = Leg.Hub.Anchor;
		var at = new BotPosition(home[0], home[1], home[2], 0);
		var state = new NaturalAltgardObservation(true, map, 21, false, quests, completed, at,
			new Dictionary<int, long>(), Bind: new BotBindPoint(Leg.Hub.MapId, at, 0));
		IReadOnlyDictionary<int, NaturalTemplateObjective> objectives = NaturalTemplateObjective.From(Plans);
		Assert.Equal(120010000, objectives[2258].ClaimMapId);
		NaturalAltgardDecision next = NaturalAltgardDecisionEngine.Decide(Leg, state, objectives, 1);
		Assert.Equal((action, targetMap), (next.Action, next.MapId));
		if (!delivered) Assert.Equal(2258, next.QuestId);
	}
}
