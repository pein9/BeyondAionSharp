using System.Text.Json;
using Aion.LogWatch;

namespace Aion.Commons.Tests;

public sealed partial class ProblemWatcherTests
{
	[Fact]
	public async Task ContinuousWatchingDetectsServersThatNeverEmitTheirFirstHeartbeat()
	{
		using var run = new WatcherRun();
		var state = new ProblemWatcher.WatcherState(run.Options() with { Duration = TimeSpan.FromMinutes(1) });
		state.CheckHeartbeats(DateTimeOffset.UtcNow.AddSeconds(31));
		await state.WriteSummaryAsync(CancellationToken.None);
		Assert.Contains("NEW HEARTBEAT gs", run.ReadDigest(), StringComparison.Ordinal);
		Assert.Contains("REPEAT ls", run.ReadDigest(), StringComparison.Ordinal);
		Assert.Contains("REPEAT cs", run.ReadDigest(), StringComparison.Ordinal);
		using var summary = JsonDocument.Parse(File.ReadAllText(run.SummaryPath));
		Assert.Equal(3, summary.RootElement.GetProperty("total").GetInt32());
		Assert.True(state.FailingProblemCount > 0);
	}

	[Fact]
	public async Task SnapshotDoesNotInventMissingServers()
	{
		using var run = new WatcherRun();
		var state = new ProblemWatcher.WatcherState(run.Options());
		state.CheckHeartbeats(DateTimeOffset.UtcNow.AddMinutes(10));
		await state.WriteSummaryAsync(CancellationToken.None);
		Assert.Equal(0, state.FailingProblemCount);
		Assert.DoesNotContain("HEARTBEAT", run.ReadDigest(), StringComparison.Ordinal);
	}

	[Fact]
	public async Task ReplayedOldHeartbeatDoesNotClearTheActiveAlert()
	{
		using var run = new WatcherRun();
		var at = DateTimeOffset.UtcNow.AddMinutes(-1);
		AppendHeartbeat(run, at);
		var state = new ProblemWatcher.WatcherState(run.Options());
		state.ReadFiles();
		state.CheckHeartbeats(at.AddSeconds(20));
		AppendHeartbeat(run, at); // Duplicate and older records cannot announce recovery.
		AppendHeartbeat(run, at.AddSeconds(-1));
		state.ReadFiles();
		state.CheckHeartbeats(at.AddSeconds(21));
		await state.WriteSummaryAsync(CancellationToken.None);
		using var summary = JsonDocument.Parse(File.ReadAllText(run.SummaryPath));
		Assert.Equal(1, summary.RootElement.GetProperty("total").GetInt32());
	}

	[Fact]
	public async Task DelayedButNewerHeartbeatDoesNotClearTheActiveAlert()
	{
		using var run = new WatcherRun();
		var at = DateTimeOffset.UtcNow.AddMinutes(-1);
		AppendHeartbeat(run, at);
		var state = new ProblemWatcher.WatcherState(run.Options());
		state.ReadFiles();
		state.CheckHeartbeats(at.AddSeconds(20));
		AppendHeartbeat(run, at.AddSeconds(1)); // Still well over twenty seconds old when read.
		state.ReadFiles();
		state.CheckHeartbeats(at.AddSeconds(22));
		await state.WriteSummaryAsync(CancellationToken.None);
		using var summary = JsonDocument.Parse(File.ReadAllText(run.SummaryPath));
		Assert.Equal(1, summary.RootElement.GetProperty("total").GetInt32());
	}

	[Fact]
	public async Task FreshRecoveryRearmsTheThresholdWithoutClearingTheRunFailure()
	{
		using var run = new WatcherRun();
		var at = DateTimeOffset.UtcNow;
		AppendHeartbeat(run, at.AddSeconds(-30));
		var state = new ProblemWatcher.WatcherState(run.Options());
		state.ReadFiles();
		state.CheckHeartbeats(at);
		AppendHeartbeat(run, at);
		state.ReadFiles();
		state.CheckHeartbeats(at.AddSeconds(19.999));
		state.CheckHeartbeats(at.AddSeconds(20));
		state.CheckHeartbeats(at.AddSeconds(21));
		await state.WriteSummaryAsync(CancellationToken.None);
		using var summary = JsonDocument.Parse(File.ReadAllText(run.SummaryPath));
		Assert.Equal(2, summary.RootElement.GetProperty("total").GetInt32());
		Assert.True(state.FailingProblemCount > 0);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task TrackedHeartbeatStillFailsUnlessExplicitlyAllowlisted(bool allowlisted)
	{
		using var run = new WatcherRun();
		string fp = Aion.Commons.Logging.LogFingerprint.Create("Heartbeat missed for {Server}", null, "Heartbeat missed for 30 s.").Value;
		run.WriteLedger(JsonSerializer.Serialize(new[] { new
		{
			fp, firstSeenSha = "1111111", lastSeenSha = "2222222", lastSeenRun = "older",
			count = 1, status = "tracked", tracking = "TEST-hang",
		} }));
		if (allowlisted)
			run.WriteAllowlist(JsonSerializer.Serialize(new[] { new
			{
				fp, reason = "Synthetic test", owner = "e2e", tracking = "TEST-hang",
				modes = new[] { "LIVE" }, servers = new[] { "gs" }, maxCount = 1, expires = "2099-12-31",
			} }));
		AppendHeartbeat(run, DateTimeOffset.UtcNow.AddSeconds(-30));
		Assert.Equal(allowlisted ? 0 : 1, await ProblemWatcher.RunAsync(run.Options()));
	}

	private static void AppendHeartbeat(WatcherRun run, DateTimeOffset timestamp) =>
		File.AppendAllText(Path.Combine(run.Options().RunDirectory, "logs", "gs", "gs.events.jsonl"),
			JsonSerializer.Serialize(new { lvl = "INFO", ts = timestamp, srv = "gs", run = "test",
				cat = "ServerHeartbeat", tpl = "Server heartbeat", msg = "Server heartbeat" }) + "\n");

	[Theory]
	[InlineData("--heartbeat-timeout-seconds", "19")]
	[InlineData("--heartbeat-timeout-seconds", "301")]
	[InlineData("--initial-heartbeat-timeout-seconds", "19")]
	[InlineData("--initial-heartbeat-timeout-seconds", "601")]
	[InlineData("--heartbeat-timeout-seconds", "NaN")]
	public void HeartbeatThresholdOptionsRejectUnsafeValues(string option, string value)
	{
		using var run = new WatcherRun();
		Assert.Throws<ArgumentException>(() => WatchOptions.Parse(
			["--run", "test", "--run-dir", run.Options().RunDirectory, option, value]));
	}

	[Fact]
	public async Task ConfiguredHeartbeatThresholdIsUsedAndRecorded()
	{
		using var run = new WatcherRun();
		var options = WatchOptions.Parse(["--run", "test", "--run-dir", run.Options().RunDirectory,
			"--heartbeat-timeout-seconds", "40", "--initial-heartbeat-timeout-seconds", "60"]);
		var at = DateTimeOffset.UtcNow;
		AppendHeartbeat(run, at);
		var state = new ProblemWatcher.WatcherState(run.Options() with
		{
			MissingHeartbeatThreshold = options.MissingHeartbeatThreshold,
			InitialHeartbeatThreshold = options.InitialHeartbeatThreshold,
		});
		state.ReadFiles();
		state.CheckHeartbeats(at.AddSeconds(39.999));
		int before = state.FailingProblemCount;
		state.CheckHeartbeats(at.AddSeconds(40));
		await state.WriteSummaryAsync(CancellationToken.None);
		Assert.Equal(0, before);
		Assert.True(state.FailingProblemCount > 0);
		using var summary = JsonDocument.Parse(File.ReadAllText(run.SummaryPath));
		Assert.Equal(40, summary.RootElement.GetProperty("heartbeatTimeoutSeconds").GetDouble());
		Assert.Equal(60, summary.RootElement.GetProperty("initialHeartbeatTimeoutSeconds").GetDouble());
	}
}
