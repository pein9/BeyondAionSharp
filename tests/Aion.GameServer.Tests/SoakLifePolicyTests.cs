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
