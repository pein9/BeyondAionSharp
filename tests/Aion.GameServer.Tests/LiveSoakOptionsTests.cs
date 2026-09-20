using Aion.Bots.Scenarios;
using Aion.LiveBots;

namespace Aion.GameServer.Tests;

public sealed class LiveSoakOptionsTests
{
	[Fact]
	public void DefaultSoakRequestsWholeWorkloadForTwoHours()
	{
		var options = Parse();
		Assert.Equal(7200, options.SoakSeconds);
		Assert.Equal(Enum.GetValues<SoakActivity>(), options.SoakActivities);
		Assert.Equal(10, options.BotCount);
	}

	[Fact]
	public void ExplicitDiagnosticPreservesActivitySelectionAndDuration()
	{
		var options = Parse("--soak-seconds", "180", "--soak-activities", "Group,Trade,Relog,CrashDisconnect", "--bots", "200");
		Assert.Equal(180, options.SoakSeconds);
		Assert.Equal(200, options.BotCount);
		Assert.Equal(new[] { SoakActivity.Group, SoakActivity.Trade, SoakActivity.Relog, SoakActivity.CrashDisconnect }, options.SoakActivities);
	}

	[Theory]
	[InlineData("--soak-seconds", "0")]
	[InlineData("--soak-seconds", "7201")]
	[InlineData("--soak-activities", "")]
	[InlineData("--soak-activities", "5")]
	[InlineData("--soak-activities", "Group,Group")]
	[InlineData("--soak-activities", "group")]
	[InlineData("--bots", "11")]
	public void InvalidSelectionFailsClosed(string key, string value) => Assert.ThrowsAny<ArgumentException>(() => Parse(key, value));

	[Fact]
	public void OrdinaryScenarioCannotSilentlyIgnoreSoakFlags() => Assert.Throws<ArgumentException>(() =>
		LiveBotOptions.Parse(["--run", "test", "--output", "run/test", "--scenario", "connect", "--soak-seconds", "180"]));

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task UnavailableOrEmptyCohortWorkFailsBeforeOpeningBotSessions(bool emptyCohort)
	{
		string output = Path.Combine(Path.GetTempPath(), "aion-soak-test-" + Guid.NewGuid().ToString("N"));
		try
		{
			var options = Parse() with { OutputDirectory = output };
			if (emptyCohort) options = options with { SoakActivities = [SoakActivity.Group] };
			else options = options with { SoakActivities = [(SoakActivity)999] };
			var error = await Assert.ThrowsAsync<InvalidOperationException>(() => LiveBotRunner.RunAsync(options));
			Assert.Contains(emptyCohort ? "without work" : "not implemented", error.Message);
			Assert.Empty(Directory.GetFiles(Path.Combine(output, "bots")));
			Assert.False(File.Exists(Path.Combine(output, "soak-runtime.json")));
		}
		finally
		{
			// Exact unique directory created only by this test.
			if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
		}
	}

	private static LiveBotOptions Parse(params string[] additional) => LiveBotOptions.Parse(
		["--run", "test", "--output", "run/test", "--scenario", "SOAK", "--git-sha", "test", .. additional]);
}
