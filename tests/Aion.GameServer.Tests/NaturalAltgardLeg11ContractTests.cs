using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

public sealed class NaturalAltgardLeg11ContractTests
{
	private static readonly NaturalAltgardContract Leg = NaturalAltgardContract.LoadLeg("l11");
	private static readonly IReadOnlyDictionary<int, NaturalTemplateObjective> Objectives =
		NaturalTemplateObjective.From(NaturalAltgardContract.LoadPlans("l11"));
	private static string Data(params string[] path) => Path.Combine([RealStaticData.RepoRoot(), "game-server", "data", "static_data", .. path]);

	[Fact]
	public void ContractRetainsTheFullIncomingJournalAndPinsTheRealQuestMechanics()
	{
		NaturalAltgardContract prior = NaturalAltgardContract.LoadLeg("l10");
		Assert.All(prior.Start.CompletedQuestIds.Concat(prior.Order), id => Assert.Contains(id, Leg.Start.CompletedQuestIds));
		Assert.Equal(143, Leg.Start.CompletedQuestIds.Length);
		Assert.Equal(("altgard-l10", 24, 700065), (Leg.Start.Snapshot, Leg.Start.Level, Leg.Start.BindNpcId));
		Assert.Equal([2900], Leg.Order);
		Assert.Empty(Objectives);
		Assert.Empty(Leg.InstanceTripList); // Java creates the solo instance from the Skuld dialog.
		Assert.Equal(4, Leg.Steps.Count(step => step.Teleport != null));
		Assert.Equal(220010000, Leg.Destiny!.KillTeleport.MapId); // Fifth teleport comes from kill credit.
		Assert.Equal([0, 1, 2, 3, 4, 95, 96, 99, 97, 9, 10], Leg.Steps.Where(s => s.ExpectedStatus == "START").Select(s => s.Var!.Value));
		Assert.Equal(("altgard-l11", 24, 700065, 60f),
			(Leg.Endpoint.Snapshot, Leg.Endpoint.MinimumLevel, Leg.Endpoint.BindNpcId, Leg.Endpoint.Radius));
		NaturalAltgardDestiny d = Leg.Destiny!;
		Assert.Equal((156, 140000001, 11504, 1L << 30, 1000, 204263, 3169, 300, 98, 9, 4),
			(d.MovieId, d.StoneItemId, d.StigmaSkillId, d.StigmaSlot, d.InstallationBaseFee, d.EnemyNpcId, d.EnemyHp,
			 d.LifetimeSeconds, d.FightVar, d.KillVar, d.ResetVar));
		XElement q = Assert.Single(XDocument.Load(Data("quest_data", "quest_data.xml")).Descendants("quest"),
			n => (int?)n.Attribute("id") == 2900);
		Assert.Equal((20, "MISSION", 228880), ((int)q.Attribute("minlevel_permitted")!, (string)q.Attribute("category")!,
			(int)q.Element("rewards")!.Attribute("exp")!));
		Assert.Null(q.Attribute("use_class_reward")); // Missing defaults to 0: the legacy class list is inactive.
		Assert.Null(Leg.RewardChoice);
		Assert.Equal(["USE_OBJECT", "SELECTED_QUEST_NOREWARD"], Leg.Steps.Single(s => s.Key == "q2900-reward").Actions);
		XElement items = XDocument.Load(Data("items", "item_templates.xml")).Root!;
		Assert.NotNull(Assert.Single(items.Elements(), n => (int?)n.Attribute("id") == d.StoneItemId).Element("stigma"));
		Assert.Null(Assert.Single(items.Elements(), n => (int?)n.Attribute("id") == d.LegacyRewardId).Element("stigma"));
	}

	[Theory]
	[InlineData(220030000, 3, 0, "travel-to-map", "q2900-heimdall", 120010000)]
	[InlineData(120010000, 3, 0, "talk", "q2900-heimdall", 120010000)]
	[InlineData(220010000, 3, 1, "talk", "q2900-munin", 220010000)]
	[InlineData(220010000, 3, 2, "talk", "q2900-urd", 220010000)]
	[InlineData(220010000, 3, 3, "talk", "q2900-verdandi", 220010000)]
	[InlineData(220030000, 3, 4, "travel-to-map", "q2900-enter", 220010000)]
	[InlineData(220010000, 3, 4, "talk", "q2900-enter", 220010000)]
	[InlineData(320070000, 3, 95, "talk", "q2900-movie", 320070000)]
	[InlineData(320070000, 3, 96, "talk", "q2900-stone", 320070000)]
	[InlineData(320070000, 3, 99, "equip-stigma", "q2900-equip", 320070000)]
	[InlineData(320070000, 3, 97, "talk", "q2900-spawn", 320070000)]
	[InlineData(320070000, 3, 98, "destiny-fight", null, 320070000)]
	[InlineData(220010000, 3, 9, "talk", "q2900-skuld-return", 220010000)]
	[InlineData(220010000, 3, 10, "talk", "q2900-munin-return", 220010000)]
	[InlineData(220030000, 4, 10, "travel-to-map", "q2900-reward", 120010000)]
	[InlineData(120010000, 4, 10, "talk", "q2900-reward", 120010000)]
	public void FullQuestValuesSelectTheRecipientOrActualEvent(int map, byte status, int var, string action, string? key, int targetMap)
	{
		NaturalAltgardDecision next = Decide(State(map, status, var));
		Assert.Equal((action, key, targetMap), (next.Action, next.StepKey, next.MapId));
	}

	[Theory]
	[InlineData(95)]
	[InlineData(96)]
	[InlineData(97)]
	[InlineData(98)]
	[InlineData(99)]
	public void OutsideInstanceStateMustRecoverBeforeAnyInstanceDialog(int var) =>
		Assert.Equal("recover-destiny", Decide(State(220030000, 3, var)).Action);

	[Fact]
	public void DeathPrefersTheFortressEvenWhenSelfRevivalIsAvailable()
	{
		NaturalAltgardDecision next = Decide(State(320070000, 3, 4) with { IsDead = true, CanRebirth = true });
		Assert.Equal(("revive-at-bind", 220030000), (next.Action, next.MapId));
		Assert.Equal("recover-destiny", Decide(State(320070000, 3, 4)).Action);
	}

	[Fact]
	public void OwnedStoneCannotRepeatTheUnguardedGiveAndMissingStoneNeedsRecovery()
	{
		Assert.Equal("recover-destiny", Decide(State(320070000, 3, 96) with { ItemCounts = new Dictionary<int, long> { [140000001] = 1 } }).Action);
		Assert.Equal("recover-destiny", Decide(State(320070000, 3, 99) with { ItemCounts = new Dictionary<int, long>() }).Action);
		Assert.Equal("town-service", Decide(State(220030000, 3, 0) with { FreeCubeSlots = 5 }).Action);
	}

	[Fact]
	public void EndpointRequiresRetainedRewardsCleanupAndEveryIncomingCompletion()
	{
		NaturalAltgardObservation done = State(220030000, 5, 10) with
		{
			CompletedQuestIds = Leg.Start.CompletedQuestIds.Append(2900).ToHashSet(),
			ItemCounts = new Dictionary<int, long> { [188053787] = 1 }, SkillIds = new HashSet<int> { 1842 },
		};
		Assert.Equal("leg-complete", Decide(done).Action);
		Assert.Equal("lost-journal", Decide(done with { CompletedQuestIds = done.CompletedQuestIds.Where(id => id != 24016).ToHashSet() }).Action);
		Assert.Equal("stigma-cleanup", Decide(done with { SkillIds = new HashSet<int> { 1842, 11504 } }).Action);
		Assert.Equal("stigma-cleanup", Decide(done with { ItemCounts = new Dictionary<int, long> { [140000001] = 1 } }).Action);
		Assert.Equal("missing-reward", Decide(done with { ItemCounts = new Dictionary<int, long>() }).Action);
		Assert.Equal("unexpected-legacy-reward", Decide(done with { ItemCounts = new Dictionary<int, long> { [188053787] = 1, [140000098] = 1 } }).Action);
		NaturalAltgardDecision back = Decide(done with { MapId = 120010000 });
		Assert.Equal(("travel-to-map", 220030000), (back.Action, back.MapId));
	}

	[Theory]
	[InlineData(PlayerClass.CLERIC, 20, "l11", true)]
	[InlineData(PlayerClass.CLERIC, 24, "l11", true)]
	[InlineData(PlayerClass.CLERIC, 19, "l11", false)]
	[InlineData(PlayerClass.CLERIC, 24, "l10", false)]
	[InlineData(PlayerClass.CLERIC, 24, null, false)]
	[InlineData(PlayerClass.PRIEST, 9, "l11", false)]
	[InlineData(PlayerClass.CHANTER, 24, "l11", false)]
	public void SpaceIdentityIsOnlyTheApprovedLevelTwentyClericLeg(PlayerClass playerClass, int level, string? leg, bool accepted)
	{
		if (accepted) Assert.Equal(NaturalJourneyStage.AscensionCleric, NaturalJourneyIdentityRules.Classify(playerClass, level, 320070000, leg));
		else Assert.Throws<InvalidDataException>(() => NaturalJourneyIdentityRules.Classify(playerClass, level, 320070000, leg));
	}

	[Fact]
	public void EarlierLegsStillUseSixBitCounters()
	{
		var packed = new BotQuestState(2281, 3, 3 | 5 << 6 | 5 << 12, 0, null);
		Assert.Equal(3, NaturalAltgardContract.LoadLeg("l10").QuestVar(packed));
		Assert.Equal(99, Leg.QuestVar(new(2900, 3, 99, 0, null)));
		Assert.Equal(99, Leg.QuestVar(new(2900, 3, 99 | 2 << 24, 0, null)));
	}

	private static NaturalAltgardDecision Decide(NaturalAltgardObservation state) => NaturalAltgardDecisionEngine.Decide(Leg, state, Objectives, 1);
	private static NaturalAltgardObservation State(int map, byte status, int var)
	{
		float[] at = Leg.Bind!.Position;
		var position = new BotPosition(at[0], at[1], at[2], 0);
		return new(true, map, 24, false, new Dictionary<int, BotQuestState> { [2900] = new(2900, status, var, 0, null) },
			Leg.Start.CompletedQuestIds.ToHashSet(), position,
			var == 99 ? new Dictionary<int, long> { [140000001] = 1 } : new Dictionary<int, long>(),
			new BotBindPoint(220030000, position, 0));
	}
}
