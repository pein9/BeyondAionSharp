using Aion.Bots.Scenarios;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

public sealed class NaturalIshalgenDecisionLoopTests
{
	private static readonly NaturalIshalgenContract Contract = new(220010000, 2008, 9,
	[
		new(2000, 1, []),
		new(2101, 1, []),
		new(2103, 1, [2101]),
		new(2001, 3, []),
	]);

	[Fact]
	public void FrozenContractLoadsExactlyTheReviewedQuestSet()
	{
		NaturalIshalgenContract actual = NaturalIshalgenContract.LoadDefault();
		Assert.Equal(220010000, actual.MapId);
		Assert.Equal(41, actual.Quests.Length);
		Assert.DoesNotContain(actual.Quests, quest => quest.Id is 2107 or 2122 or 2136);
		Assert.Equal([2102], actual.Quests.Single(quest => quest.Id == 2103).Prerequisites);
	}

	[Fact]
	public void ChoosesOnlyEligibleWorkAndExplainsUnknownStarter()
	{
		NaturalDecision decision = NaturalIshalgenDecisionEngine.Decide(Contract, Observe(), 1);
		Assert.Equal("find-quest-starter", decision.SelectedAction);
		Assert.Equal(2000, decision.SelectedQuestId);
		Assert.Equal("awaiting-capability", decision.Outcome);
		Assert.Contains(decision.Quests.Single(quest => quest.QuestId == 2103).Checks,
			check => check.Rule == "prerequisites" && check.Verdict == "blocked");
		Assert.Contains(decision.Quests.Single(quest => quest.QuestId == 2001).Checks,
			check => check.Rule == "level" && check.Verdict == "blocked");
		Assert.Contains(decision.Quests.Single(quest => quest.QuestId == 2000).Checks,
			check => check.Rule == "starter-observation" && check.Verdict == "unknown");
	}

	[Fact]
	public void ActiveQuestWinsAndCompletedPrerequisiteUnlocksSuccessor()
	{
		var state = Observe(level: 3, completed: [2101], active: [new BotQuestState(2001, 3, 0, 0, null)]);
		NaturalDecision decision = NaturalIshalgenDecisionEngine.Decide(Contract, state, 7);
		Assert.Equal("continue-quest", decision.SelectedAction);
		Assert.Equal(2001, decision.SelectedQuestId);
		Assert.Equal("candidate", decision.Quests.Single(quest => quest.QuestId == 2103).Verdict);
		Assert.Equal("complete", decision.Quests.Single(quest => quest.QuestId == 2101).Verdict);
	}

	[Fact]
	public void StopsAtUntouchedAscensionAndRejectsCrossedBoundary()
	{
		int[] all = Contract.Quests.Select(quest => quest.Id).ToArray();
		var atBoundary = Observe(level: 9, completed: all,
			active: [new BotQuestState(2008, 3, 0, 0, null)]) with
		{
			Position = new BotPosition(378.74f, 1895.46f, 328.838f, 0),
			ObservedObjects = [new BotKnownObject(42, BotKnownObjectKind.Npc,
				new BotPosition(378.74f, 1895.46f, 328.838f, 0), 203550)],
		};
		Assert.Equal("complete", NaturalIshalgenDecisionEngine.Decide(Contract, atBoundary, 1).Outcome);
		Assert.Equal("approach-ascension-npc", NaturalIshalgenDecisionEngine.Decide(Contract,
			atBoundary with { Position = new BotPosition(400f, 1895.46f, 328.838f, 0) }, 1).SelectedAction);
		Assert.Equal("approach-ascension-npc", NaturalIshalgenDecisionEngine.Decide(Contract,
			atBoundary with { ObservedObjects = [] }, 1).SelectedAction);
		Assert.Equal("blocked", NaturalIshalgenDecisionEngine.Decide(Contract,
			atBoundary with { Quests = new Dictionary<int, BotQuestState> { [2008] = new(2008, 3, 1, 0, null) } }, 1).Outcome);
		Assert.Equal("blocked", NaturalIshalgenDecisionEngine.Decide(Contract,
			atBoundary with { Level = 10 }, 1).Outcome);
		Assert.Equal("blocked", NaturalIshalgenDecisionEngine.Decide(Contract,
			atBoundary with { CompletedQuestIds = all.Append(2008).ToHashSet() }, 1).Outcome);
	}

	[Fact]
	public void RaeQuestAllowsOnlyItsObservedTemporaryInstanceStep()
	{
		NaturalIshalgenContract full = NaturalIshalgenContract.LoadDefault();
		NaturalIshalgenObservation ataxiar = Observe(level: 5,
			active: [new BotQuestState(2002, 3, 99, 0, null)]) with { MapId = 320010000 };
		NaturalDecision decision = NaturalIshalgenDecisionEngine.Decide(full, ataxiar, 1);
		Assert.Equal("continue-quest", decision.SelectedAction);
		Assert.Equal(2002, decision.SelectedQuestId);
		Assert.Contains(decision.GlobalChecks, check => check.Rule == "quest-transport" && check.Verdict == "pass");
		Assert.Equal("blocked", NaturalIshalgenDecisionEngine.Decide(full,
			ataxiar with { Quests = new Dictionary<int, BotQuestState> { [2002] = new(2002, 3, 12, 0, null) } }, 2).Outcome);
		Assert.Equal("blocked", NaturalIshalgenDecisionEngine.Decide(full,
			ataxiar with { MapId = 320020000 }, 3).Outcome);
	}

	[Fact]
	public async Task RefreshIsBoundedAndEveryDecisionIsRecorded()
	{
		var driver = new FakeDriver(Observe() with { JournalObserved = false });
		NaturalDecision decision = await NaturalIshalgenDecisionLoop.RunAsync(Contract, driver);
		Assert.Equal("blocked", decision.Outcome);
		Assert.Equal(2, driver.RefreshCount);
		Assert.Equal([1, 2, 3], driver.Recorded.Select(item => item.Sequence));
		Assert.All(driver.Recorded, item => Assert.Equal("refresh-observation", item.SelectedAction));

		var recovering = new FakeDriver(Observe() with { Fresh = false, JournalObserved = false })
		{
			OnRefresh = state => state with { JournalObserved = true },
		};
		NaturalDecision selected = await NaturalIshalgenDecisionLoop.RunAsync(Contract, recovering);
		Assert.Equal("find-quest-starter", selected.SelectedAction);
		Assert.Equal(1, recovering.RefreshCount);
		Assert.Equal(2, recovering.Recorded.Count);
	}

	private static NaturalIshalgenObservation Observe(ushort level = 1, int[]? completed = null,
		BotQuestState[]? active = null) => new(true, true, true, 220010000, level, false,
		(active ?? []).ToDictionary(quest => quest.QuestId), (completed ?? []).ToHashSet());

	private sealed class FakeDriver(NaturalIshalgenObservation observation) : INaturalIshalgenDecisionDriver
	{
		private NaturalIshalgenObservation state = observation;
		public int RefreshCount { get; private set; }
		public List<NaturalDecision> Recorded { get; } = [];
		public Func<NaturalIshalgenObservation, NaturalIshalgenObservation>? OnRefresh { get; init; }
		public NaturalIshalgenObservation Observe(bool fresh) => state with { Fresh = fresh };
		public Task RefreshAsync(CancellationToken token)
		{
			RefreshCount++;
			if (OnRefresh != null) state = OnRefresh(state);
			return Task.CompletedTask;
		}
		public void Record(NaturalDecision decision) => Recorded.Add(decision);
	}
}
