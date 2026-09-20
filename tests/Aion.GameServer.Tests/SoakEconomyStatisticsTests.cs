using Aion.Bots.Scenarios;
using Aion.LiveBots;
using System.Text.Json;

namespace Aion.GameServer.Tests;

public sealed class SoakEconomyStatisticsTests
{
	private static SoakEconomyStatistics Population(int subjects = 20) => new(Enumerable.Range(1, subjects)
		.Select(subject => new SoakEconomySubject($"b{subject:D2}", subject % 2 == 0 ? "Gather" : "Craft")));

	[Fact]
	public void MissingSubjectsOrPartialPrefixesNeverPassEvenWithPlausibleObservedRates()
	{
		var statistics = Population();
		for (int trial = 0; trial < 200; trial++) statistics.Observe("b01", "Craft", .8, trial % 5 != 0);
		var report = statistics.Snapshot();
		Assert.Equal("insufficient", report.Status);
		Assert.Equal(200, report.Prefixes.Single(prefix => prefix.Bot == "b01").Observed);
		Assert.Equal(20, report.Prefixes.Single(prefix => prefix.Bot == "b01").Samples);
		Assert.False(report.OverallSoakAccepted);
	}

	[Fact]
	public void FixedPrefixesStayFixedButLaterBiasedAttemptsStillFailWholeStreamTest()
	{
		var statistics = Population();
		for (int subject = 1; subject <= 20; subject++)
			for (int trial = 0; trial < 20; trial++) statistics.Observe($"b{subject:D2}", subject % 2 == 0 ? "Gather" : "Craft", .8, trial % 5 != 0);
		var before = statistics.Snapshot();
		Assert.Equal("passed", before.Status);
		Assert.All(before.Tests, test => Assert.True(test.LogEvidence < test.LogThreshold));
		for (int trial = 0; trial < 1000; trial++) statistics.Observe("b01", "Craft", .8, false);
		var after = statistics.Snapshot();
		Assert.Equal(before.Tests.Where(test => test.Scope == "fixed-prefix"), after.Tests.Where(test => test.Scope == "fixed-prefix"));
		Assert.Equal("failed", after.Status);
		Assert.Equal("failed", after.Tests.Single(test => test.Kind == "Craft" && test.Scope == "whole-stream").Status);
		Assert.Equal(20, after.Prefixes.Single(prefix => prefix.Bot == "b01").Samples);
		Assert.False(after.OverallSoakAccepted);
		JsonSerializer.Serialize(after); // No infinity/NaN evidence, even after large likelihood ratios.
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void ForcedSuccessAndFailureMutationsAreRejectedInBothDirections(bool forcedSuccess)
	{
		var statistics = Population();
		for (int subject = 1; subject <= 20; subject++)
			for (int trial = 0; trial < 20; trial++) statistics.Observe($"b{subject:D2}", subject % 2 == 0 ? "Gather" : "Craft", .8, forcedSuccess);
		Assert.Equal("failed", statistics.Snapshot().Status);
		Assert.All(statistics.Snapshot().Tests.Where(test => test.Scope == "fixed-prefix"), test => Assert.Equal("failed", test.Status));
	}

	[Fact]
	public void SkillDependentExpectationsArePerAttemptAndEnrollmentOrderDoesNotChangeEvidence()
	{
		var first = Population(); var second = Population();
		for (int trial = 0; trial < 20; trial++)
		{
			for (int subject = 1; subject <= 20; subject++) Add(first, subject, trial);
			for (int subject = 20; subject >= 1; subject--) Add(second, subject, trial);
		}
		var a = first.Snapshot(); var b = second.Snapshot();
		Assert.Equal(a.Prefixes, b.Prefixes);
		Assert.Equal(a.Status, b.Status);
		for (int i = 0; i < a.Tests.Count; i++) Assert.Equal(a.Tests[i].LogEvidence, b.Tests[i].LogEvidence, 10);
		static void Add(SoakEconomyStatistics statistics, int subject, int trial)
		{
			double probability = trial < 10 ? .7 : .9;
			bool success = trial < 10 ? trial % 10 >= 3 : trial % 10 >= 1;
			statistics.Observe($"b{subject:D2}", subject % 2 == 0 ? "Gather" : "Craft", probability, success);
		}
	}

	[Fact]
	public void GuaranteedOutcomesAreExactButCannotReplaceRandomnessExposure()
	{
		var statistics = Population();
		for (int subject = 1; subject <= 20; subject++)
			for (int trial = 0; trial < 20; trial++) statistics.Observe($"b{subject:D2}", subject % 2 == 0 ? "Gather" : "Craft", 1, true);
		Assert.Equal("insufficient", statistics.Snapshot().Status);
		statistics.Observe("b01", "Craft", 1, false); // Even after the statistical prefix, impossible outcomes fail.
		Assert.Equal(1, statistics.Snapshot().ImpossibleOutcomes);
		Assert.Equal("failed", statistics.Snapshot().Status);
	}

	[Fact]
	public void InvalidProbabilitiesAndUnenrolledSubjectsCannotPolluteEvidence()
	{
		var statistics = Population();
		foreach (double p in new[] { double.NaN, double.PositiveInfinity, -.1, 1.1 })
			Assert.Throws<ArgumentOutOfRangeException>(() => statistics.Observe("b01", "Craft", p, true));
		Assert.Throws<InvalidDataException>(() => statistics.Observe("b99", "Craft", .8, true));
		Assert.Throws<InvalidDataException>(() => statistics.Observe("b01", "Gather", .8, true));
		Assert.Throws<ArgumentException>(() => new SoakEconomyStatistics([new("b01", "Vendor")]));
		Assert.All(statistics.Snapshot().Prefixes, prefix => Assert.Equal(0, prefix.Samples));
	}

	[Fact]
	public async Task ParallelSubjectsKeepBoundedCompletePrefixes()
	{
		var statistics = Population(100);
		await Task.WhenAll(Enumerable.Range(1, 100).Select(subject => Task.Run(() =>
		{
			for (int trial = 0; trial < 100; trial++) statistics.Observe($"b{subject:D2}", subject % 2 == 0 ? "Gather" : "Craft", .8, trial % 5 != 0);
		})));
		Assert.Equal("passed", statistics.Snapshot().Status);
		Assert.All(statistics.Snapshot().Prefixes, prefix => { Assert.Equal(100, prefix.Observed); Assert.Equal(20, prefix.Samples); });
	}

	[Fact]
	public async Task RejectionReachesProblemWatcherRatherThanOnlyProcessStderr()
	{
		string path = Path.Combine(Path.GetTempPath(), "aion-economy-test-" + Guid.NewGuid().ToString("N") + ".jsonl");
		try
		{
			var statistics = Population();
			await using (var writer = new LiveBotProblemWriter(path))
			{
				await LiveBotRunner.ValidateSoakEconomyAsync("test", writer, statistics.Snapshot());
				statistics.Observe("b01", "Craft", 1, false);
				await Assert.ThrowsAsync<InvalidDataException>(() => LiveBotRunner.ValidateSoakEconomyAsync("test", writer, statistics.Snapshot()));
			}
			using var row = JsonDocument.Parse(Assert.Single(await File.ReadAllLinesAsync(path)));
			Assert.Equal("economy-statistics", row.RootElement.GetProperty("kind").GetString());
			Assert.Equal("soak-economy", row.RootElement.GetProperty("step").GetString());
		}
		finally { File.Delete(path); }
	}
}
