using System.Text.Json;
using System.Threading.Channels;
using Aion.LogWatch;

namespace Aion.Commons.Tests;

public sealed partial class ProblemWatcherTests
{
	[Theory]
	[InlineData("normal", 0)]
	[InlineData("no-opt-in", 1)]
	[InlineData("second-death", 1)]
	[InlineData("other-container", 1)]
	[InlineData("other-project", 1)]
	[InlineData("oom", 1)]
	[InlineData("server-error", 1)]
	[InlineData("tracked-error", 1)]
	[InlineData("tracked-second-death", 1)]
	[InlineData("missing-restart", 1)]
	[InlineData("missing-heartbeat", 1)]
	[InlineData("missing-timestamp", 1)]
	[InlineData("pre-kill-heartbeat", 1)]
	public async Task InjectedCrashIsNarrowAndNeverHidesOtherFailures(string variant, int expectedFailure)
	{
		using var run = new WatcherRun();
		var options = run.Options() with { ExpectGameServerCrash = variant != "no-opt-in" };
		if (variant.StartsWith("tracked-", StringComparison.Ordinal))
		{
			string fingerprint = variant == "tracked-error" ? "1234abcd" :
				Aion.Commons.Logging.LogFingerprint.Create("Container process event: die", null, "Container gameserver die code=137.").Value;
			run.WriteLedger(JsonSerializer.Serialize(new[] { new
			{
				fp = fingerprint, firstSeenSha = "1111111", lastSeenSha = "2222222", lastSeenRun = "older",
				count = 1, status = "tracked", tracking = "TEST-existing-problem",
			} }));
		}
		var at = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
		string container = new('a', 64);
		File.WriteAllText(Path.Combine(options.RunDirectory, "game-server-crash-plan.json"), JsonSerializer.Serialize(new
		{
			schemaVersion = 1, run = "test", project = "aion-bots-test", containerId = container,
			armedUtc = at, killDeadlineUtc = at.AddSeconds(30), recoveryDeadlineUtc = at.AddSeconds(180),
		}));
		var state = new ProblemWatcher.WatcherState(options);
		state.ReadFiles();
		Assert.Equal(options.ExpectGameServerCrash, File.Exists(Path.Combine(options.RunDirectory, "game-server-crash-armed.json")));
		void Docker(string action, int seconds, string? id = null, string? code = null)
		{
			var channel = Channel.CreateUnbounded<DockerLine>();
			var record = new Dictionary<string, object?>
			{
				["action"] = action, ["id"] = id ?? container, ["service"] = "gameserver",
				["attributes"] = new Dictionary<string, object?> { ["exitCode"] = code },
			};
			if (variant != "missing-timestamp") record["time"] = at.AddSeconds(seconds);
			if (variant == "pre-kill-heartbeat") record["time"] = at.AddSeconds(seconds).ToUnixTimeSeconds();
			channel.Writer.TryWrite(new DockerLine("event", "docker", JsonSerializer.Serialize(record), false)
			{ ProjectName = variant == "other-project" ? "aion-bots-other" : options.ProjectName });
			state.ReadDocker(channel.Reader);
		}
		Docker("die", 1, code: "137");
		if (variant is "second-death" or "tracked-second-death") Docker("die", 2, code: "137");
		if (variant == "other-container") Docker("die", 2, new string('b', 64), "137");
		if (variant == "oom") Docker("oom", 2);
		if (variant is "server-error" or "tracked-error") { run.WriteProblem("1234abcd"); state.ReadFiles(); }
		if (variant != "missing-restart") Docker("start", variant == "pre-kill-heartbeat" ? 1 : 40);
		if (variant != "missing-heartbeat")
		{
			run.WriteEvent(JsonSerializer.Serialize(new { ts = at.AddSeconds(variant == "pre-kill-heartbeat" ? 1.750 : 41), srv = "gs", run = "test", cat = "Heartbeat", tpl = "Server heartbeat", msg = "heartbeat" }));
			state.ReadFiles();
		}
		await state.WriteSummaryAsync(CancellationToken.None);
		Assert.Equal(expectedFailure != 0, state.FailingProblemCount > 0);
		using var summary = JsonDocument.Parse(File.ReadAllText(run.SummaryPath));
		Assert.Equal(expectedFailure != 0, summary.RootElement.GetProperty("failed").GetBoolean());
		if (variant == "pre-kill-heartbeat") Assert.False(summary.RootElement.GetProperty("expectedGameServerCrash").GetBoolean());
		if (variant == "normal")
		{
			Assert.Equal(2, summary.RootElement.GetProperty("expectedProcessEvents").GetInt32());
			Assert.True(summary.RootElement.GetProperty("expectedGameServerCrash").GetBoolean());
			Assert.Equal(0, summary.RootElement.GetProperty("suppressed").GetInt32());
			Assert.Contains("EXPECTED_FAULT PROCESS gs action=die", run.ReadDigest(), StringComparison.Ordinal);
		}
	}

	[Fact]
	public async Task CrashOptInWithoutAPlanFailsClosed()
	{
		using var run = new WatcherRun();
		Assert.Equal(1, await ProblemWatcher.RunAsync(run.Options() with { ExpectGameServerCrash = true }));
		Assert.Contains("No valid game-server crash plan was armed", run.ReadDigest(), StringComparison.Ordinal);
	}

	[Fact]
	public async Task PlannedGapIsOnlyForGsAndNormalHeartbeatMonitoringResumes()
	{
		using var run = new WatcherRun();
		var options = run.Options() with { ExpectGameServerCrash = true };
		var at = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
		string container = new('a', 64);
		string planJson = JsonSerializer.Serialize(new
		{
			schemaVersion = 1, run = "test", project = "aion-bots-test", containerId = container,
			armedUtc = at, killDeadlineUtc = at.AddSeconds(30), recoveryDeadlineUtc = at.AddSeconds(180),
		});
		File.WriteAllText(Path.Combine(options.RunDirectory, "game-server-crash-plan.json"), planJson);
		var state = new ProblemWatcher.WatcherState(options);
		void Heartbeat(string server, int seconds)
		{
			File.AppendAllText(Path.Combine(options.RunDirectory, "logs", "gs", "gs.events.jsonl"),
				JsonSerializer.Serialize(new { ts = at.AddSeconds(seconds), srv = server, run = "test", cat = "Heartbeat", tpl = "Server heartbeat", msg = "heartbeat" }) + "\n");
			state.ReadFiles();
		}
		void Docker(string action, int seconds)
		{
			var channel = Channel.CreateUnbounded<DockerLine>();
			channel.Writer.TryWrite(new DockerLine("event", "docker", JsonSerializer.Serialize(new
			{
				action, id = container, service = "gameserver", time = at.AddSeconds(seconds),
				attributes = new { exitCode = "137" },
			}), false) { ProjectName = options.ProjectName });
			state.ReadDocker(channel.Reader);
		}
		Heartbeat("gs", 0);
		using (var receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(options.RunDirectory, "game-server-crash-armed.json"))))
			Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(planJson))),
				receipt.RootElement.GetProperty("planSha256").GetString());
		Docker("die", 1);
		state.CheckHeartbeats(at.AddSeconds(25));
		Assert.Equal(0, state.FailingProblemCount);
		Heartbeat("ls", 0);
		state.CheckHeartbeats(at.AddSeconds(25));
		Assert.True(state.FailingProblemCount > 0); // Other servers never enter the planned gap.
		Docker("start", 40);
		Heartbeat("gs", 41);
		state.CheckHeartbeats(at.AddSeconds(62));
		await state.WriteSummaryAsync(CancellationToken.None);
		Assert.Contains("NEW HEARTBEAT ls", run.ReadDigest(), StringComparison.Ordinal);
		Assert.Contains("REPEAT gs", run.ReadDigest(), StringComparison.Ordinal); // The shared heartbeat fingerprint repeats.
		using var summary = JsonDocument.Parse(File.ReadAllText(run.SummaryPath));
		Assert.Equal(2, summary.RootElement.GetProperty("total").GetInt32());
		Assert.True(summary.RootElement.GetProperty("expectedGameServerCrash").GetBoolean());
		Assert.True(summary.RootElement.GetProperty("failed").GetBoolean());
	}
}
