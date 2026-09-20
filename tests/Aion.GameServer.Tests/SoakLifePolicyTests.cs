using Aion.Bots.Scenarios;

namespace Aion.GameServer.Tests;

public sealed class SoakLifePolicyTests
{
	[Theory]
	[InlineData(50)]
	[InlineData(200)]
	[InlineData(500)]
	public void CapacityPopulationCoversEverySubjectZoneAndRequiredActivity(int subjects)
	{
		var manifest = ScenarioManifest.Load(ScenarioManifest.FindDefaultPath());
		var cohorts = SoakLifePolicy.CreatePopulation(subjects, manifest);
		Assert.Equal(subjects / 2, cohorts.Count);
		Assert.Equal(Enumerable.Range(1, subjects), cohorts.SelectMany(pair => new[] { pair.FirstSubject, pair.SecondSubject }));
		Assert.Equal(new[] { 110010000, 120010000, 210010000, 220010000, 400010000 }, cohorts.Select(pair => pair.MapId).Distinct().Order());
		Assert.Equal(Enum.GetValues<SoakActivity>().Order(), cohorts.SelectMany(pair => pair.Actions).Select(action => action.Activity).Distinct().Order());
		foreach (var pair in cohorts)
		{
			Assert.Equal(pair.MapId == 400010000, pair.FirstRace != pair.SecondRace);
			if (pair.FirstRace != pair.SecondRace)
				Assert.DoesNotContain(pair.Actions, action => action.Activity is SoakActivity.Group or SoakActivity.Trade or SoakActivity.Duel);
		}
		foreach (int map in new[] { 210010000, 220010000 })
		{
			var channels = cohorts.Where(pair => pair.MapId == map).GroupBy(SoakLifePolicy.StarterChannel).ToArray();
			Assert.Equal(Enumerable.Range(0, 5), channels.Select(group => group.Key).Order());
			Assert.All(channels, group => Assert.Equal(subjects / 50, group.Count()));
		}
	}

	[Fact]
	public void SeededChoicesAreIndependentOfInterleavingAndCoverEveryActivityPerCycle()
	{
		var pairs = SoakLifePolicy.CreatePopulation(50, ScenarioManifest.Load(ScenarioManifest.FindDefaultPath()));
		var expected = new SoakLifePolicy(73, pairs[0]);
		var actual = new SoakLifePolicy(73, pairs[0]);
		var unrelated = new SoakLifePolicy(73, pairs[1]);
		long sequence = 0;
		for (int cycle = 0; cycle < 20; cycle++)
		{
			var seen = new HashSet<SoakActivity>();
			foreach (var unused in pairs[0].Actions)
			{
				for (int i = 0; i < cycle; i++) unrelated.Next();
				SoakDecision decision = actual.Next();
				Assert.Equal(expected.Next(), decision);
				Assert.Equal(++sequence, decision.Sequence);
				Assert.InRange(decision.ThinkTime.TotalMilliseconds, 1000, 5000);
				Assert.True(seen.Add(decision.Action.Activity));
			}
			Assert.Equal(pairs[0].Actions.Count, seen.Count);
		}
	}

	[Fact]
	public void DifferentSeedsAndCohortsProduceDifferentStreams()
	{
		var pair = SoakLifePolicy.CreatePopulation(50, ScenarioManifest.Load(ScenarioManifest.FindDefaultPath()))[0];
		var first = new SoakLifePolicy(1, pair);
		var second = new SoakLifePolicy(2, pair);
		var third = new SoakLifePolicy(1, pair with { Number = 6 });
		var a = Enumerable.Range(0, 30).Select(_ => first.Next()).ToArray();
		Assert.False(a.SequenceEqual(Enumerable.Range(0, 30).Select(_ => second.Next())));
		Assert.False(a.SequenceEqual(Enumerable.Range(0, 30).Select(_ => third.Next())));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void CompletedQuestIsRemovedFromQueuedAndFutureCyclesWithoutLosingOtherActions(bool selectQuestFirst)
	{
		var pair = SoakLifePolicy.CreatePopulation(50, ScenarioManifest.Load(ScenarioManifest.FindDefaultPath()))[0];
		var expected = new SoakLifePolicy(73, pair);
		SoakDecision[] cycle = Enumerable.Range(0, pair.Actions.Count).Select(_ => expected.Next()).ToArray();
		int consumed = selectQuestFirst ? Array.FindIndex(cycle, decision => decision.Action.Activity == SoakActivity.Quest) + 1 : 0;
		var policy = new SoakLifePolicy(73, pair);
		for (int i = 0; i < consumed; i++) Assert.Equal(cycle[i], policy.Next());
		// Populate a cycle before retiring its still-queued quest, if necessary.
		if (!selectQuestFirst)
		{
			Assert.NotEqual(SoakActivity.Quest, cycle[0].Action.Activity);
			Assert.Equal(cycle[0], policy.Next());
			consumed = 1;
		}
		policy.CompleteQuestJourney();
		long sequence = consumed;
		foreach (var queued in cycle.Skip(consumed).Where(decision => decision.Action.Activity != SoakActivity.Quest))
		{
			var decision = policy.Next();
			Assert.Equal(queued.Action, decision.Action);
			Assert.Equal(++sequence, decision.Sequence);
		}
		var remaining = pair.Actions.Where(action => action.Activity != SoakActivity.Quest).Select(action => action.Activity).Order().ToArray();
		for (int i = 0; i < 20; i++)
		{
			var decisions = Enumerable.Range(0, remaining.Length).Select(_ => policy.Next()).ToArray();
			Assert.Equal(remaining, decisions.Select(decision => decision.Action.Activity).Order());
			foreach (var decision in decisions)
			{
				Assert.Equal(++sequence, decision.Sequence);
				Assert.InRange(decision.ThinkTime.TotalMilliseconds, 1000, 5000);
			}
		}
	}

	[Fact]
	public void QuestRetirementIsDeterministicAndRejectsInvalidOrRepeatedCompletion()
	{
		var pairs = SoakLifePolicy.CreatePopulation(50, ScenarioManifest.Load(ScenarioManifest.FindDefaultPath()));
		var first = new SoakLifePolicy(73, pairs[0]);
		var second = new SoakLifePolicy(73, pairs[0]);
		first.CompleteQuestJourney();
		second.CompleteQuestJourney();
		for (int i = 0; i < 100; i++)
		{
			var decision = first.Next();
			Assert.Equal(second.Next(), decision);
			Assert.NotEqual(SoakActivity.Quest, decision.Action.Activity);
		}
		Assert.Throws<InvalidOperationException>(first.CompleteQuestJourney);
		Assert.Throws<InvalidOperationException>(new SoakLifePolicy(73, pairs[2]).CompleteQuestJourney);
		var questOnly = new SoakLifePolicy(73, pairs[0] with { Actions = [new(SoakActivity.Quest, "Q1")] });
		Assert.Throws<InvalidOperationException>(questOnly.CompleteQuestJourney);
		Assert.Equal(SoakActivity.Quest, questOnly.Next().Action.Activity);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(8)]
	[InlineData(11)]
	[InlineData(1002)]
	public void InvalidOrIncompletePopulationsAreRejected(int subjects)
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => SoakLifePolicy.CreatePopulation(subjects,
			ScenarioManifest.Load(ScenarioManifest.FindDefaultPath())));
	}
}
