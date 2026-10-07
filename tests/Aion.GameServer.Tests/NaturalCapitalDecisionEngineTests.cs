using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

public sealed class NaturalCapitalDecisionEngineTests
{
	private static readonly Lazy<NaturalCapitalContract> Contract = new(NaturalCapitalContract.LoadDefault);
	private static NaturalAscensionObservation State(int[] pending, (int Quest, byte Status, int Var)[] active,
		int map = 120010000, int[]? items = null) => new(true, map, 10, PlayerClass.CLERIC.GetClassId(), false,
		active.Append((2904, (byte)3, 0)).ToDictionary(q => q.Item1, q => new BotQuestState(q.Item1, q.Item2, q.Item3, 0, null)),
		Contract.Value.CompletedQuestIds.Except(pending).Concat([2008, 2009]).ToHashSet(), 250000, null,
		(items ?? []).ToDictionary(id => id, _ => 1L), false);
	private static NaturalCapitalDecision Decide(NaturalAscensionObservation state) => NaturalCapitalDecisionEngine.Decide(Contract.Value, state);

	[Fact]
	public void InterruptedRibbonContactContinuesAtTherfWithoutSkippingContacts()
	{
		var state = State([2912, 2914], [(2912, 3, 1)]);
		Assert.Equal(("talk", 204088), (Decide(state).Action, Decide(state).Step!.NpcId));
		Assert.Equal(204240, Decide(State([2912, 2914], [(2912, 3, 2)])).Step!.NpcId);
		Assert.Equal(204236, Decide(State([2912, 2914], [(2912, 3, 3)])).Step!.NpcId);
	}

	[Fact]
	public void ConventRewardAndReturnPrecedeTheFinalSupplyHandIn()
	{
		var state = State([29004, 2953], [(29004, 4, 2), (2953, 3, 1)], map: 120020000);
		Assert.Equal(798700, Decide(state).Step!.NpcId);
		var returned = State([2953], [(2953, 3, 1)], map: 120020000);
		Assert.Equal(("portal", 730269), (Decide(returned).Action, Decide(returned).Portal!.NpcId));
		Assert.Equal(204191, Decide(returned with { MapId = 120010000 }).Step!.NpcId);
		Assert.Equal(("portal", 730268), (Decide(state with { MapId = 120010000 }).Action,
			Decide(state with { MapId = 120010000 }).Portal!.NpcId));
	}

	[Fact]
	public void OnlyTheSuppliedManualCanAdvanceTheBookQuest()
	{
		var missing = State([29048], [(29048, 3, 0)]);
		Assert.Equal("blocked", Decide(missing).Action);
		Assert.Equal("read-book", Decide(missing with { ItemCounts = new Dictionary<int, long> { [182212217] = 1 } }).Action);
		Assert.Equal(NaturalCapitalSteps.BookReward.Key, Decide(State([29048], [(29048, 4, 1)])).Step!.Key);
	}

	[Theory]
	[InlineData(2911, 4, 3)]
	[InlineData(2913, 3, 0)]
	[InlineData(2915, 3, 0)]
	public void UnapprovedBlessingBranchesAreRefused(int quest, byte status, int var) =>
		Assert.Equal("blocked", Decide(State([2911, 2912, 2914], [(quest, status, var)])).Action);

	[Fact]
	public void InterruptedApprovedRewardsResumeAndCompletedRepeatablesStayDone()
	{
		var reward = Decide(State([2911, 2912, 2914], [(2911, 4, 2)]));
		Assert.Equal((204193, "REWARD"), (reward.Step!.NpcId, reward.Step.ExpectedStatus));
		Assert.Equal(204147, Decide(State([2914], [(2914, 3, 1)])).Step!.NpcId);
		Assert.Equal("complete", Decide(State([], [])).Action);
		Assert.Equal("blocked", Decide(State([], [], map: 220030000)).Action);
	}

	[Fact]
	public void TheCapitalPassStaysTheClericsAndNamesTheClassItRefuses()
	{
		// CP-27: a level-10 Chanter with the ceremony done is blocked, with the reason that names it.
		var state = State([2912], [(2912, 3, 1)]);
		NaturalCapitalDecision chanter = Decide(state with { ClassId = PlayerClass.CHANTER.GetClassId() });
		Assert.Equal(("blocked", "The capital pass requires the completed level-10 Cleric ceremony; the character is CHANTER."),
			(chanter.Action, chanter.Reason));
		Assert.Equal("The capital pass requires the completed level-10 Cleric ceremony; the character is TEMPLAR.",
			Decide(state with { ClassId = PlayerClass.TEMPLAR.GetClassId() }).Reason);
		Assert.Equal("The capital pass requires the completed level-10 Cleric ceremony; the character is unknown class 250.",
			Decide(state with { ClassId = 250 }).Reason);
		// A Cleric short of the ceremony reads as before.
		Assert.Equal(("blocked", "The capital pass requires the completed level-10 Cleric ceremony."),
			(Decide(state with { Level = 9 }).Action, Decide(state with { Level = 9 }).Reason));
		Assert.Equal("The capital pass requires the completed level-10 Cleric ceremony.",
			Decide(state with { CompletedQuestIds = new HashSet<int> { 2008 } }).Reason);
		Assert.NotEqual("blocked", Decide(state).Action);
	}

	[Fact]
	public void DeathRecoveryKeepsPriorityOverIdentityAndMapGates()
	{
		var state = State([2912], [(2912, 3, 1)], map: 220010000);
		Assert.Equal("travel-city", Decide(state).Action);
		Assert.Equal("recover", Decide(state with { IsDead = true, MapId = 320020000 }).Action);
		Assert.Equal("blocked", Decide(state with { ClassId = PlayerClass.PRIEST.GetClassId() }).Action);
		Assert.Equal("blocked", Decide(state with { CompletedQuestIds = new HashSet<int> { 2008 } }).Action);
		Assert.Equal("blocked", Decide(state with { Quests = new Dictionary<int, BotQuestState> { [2904] = new(2904, 3, 1, 0, null) } }).Action);
	}

	[Fact]
	public void CheckpointScopesRequireSimAndCannotReplaceOtherDiagnostics()
	{
		var valid = new NaturalJourneyOptions(AscensionBridge: true, CapitalStage: "first");
		NaturalCapitalDecisionEngine.ValidateScope(valid, "SIM-natural");
		Assert.Throws<InvalidOperationException>(() => NaturalCapitalDecisionEngine.ValidateScope(valid, "LIVE-natural"));
		Assert.Throws<InvalidOperationException>(() => NaturalCapitalDecisionEngine.ValidateScope(valid with { AscensionBridge = false }, "SIM-natural"));
		Assert.Throws<InvalidOperationException>(() => NaturalCapitalDecisionEngine.ValidateScope(valid with { AltgardLegId = "l1" }, "SIM-natural"));
		Assert.Throws<InvalidOperationException>(() => NaturalCapitalDecisionEngine.ValidateScope(valid with { Encounter = NaturalMauEncounter.HatataAlone }, "SIM-natural"));
	}
}
