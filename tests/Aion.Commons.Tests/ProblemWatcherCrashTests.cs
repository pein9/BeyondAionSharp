using System.Text.Json;
using System.Threading.Channels;
using Aion.LogWatch;

namespace Aion.Commons.Tests;

public sealed partial class ProblemWatcherTests
{
	public static IEnumerable<object[]> CrashCases()
	{
		foreach (string server in new[] { "gs", "cs", "ls" })
		foreach (string variant in new[] { "normal", "no-opt-in", "second-death", "other-container", "other-project",
			"oom", "server-error", "tracked-error", "tracked-second-death", "missing-restart", "missing-heartbeat",
			"missing-timestamp", "pre-kill-heartbeat" })
			yield return [variant, variant == "normal" ? 0 : 1, server];
	}

	[Theory]
	[MemberData(nameof(CrashCases))]
	public async Task InjectedCrashIsNarrowAndNeverHidesOtherFailures(string variant, int expectedFailure, string server)
	{
		using var run = new WatcherRun();
		string service = server switch { "gs" => "gameserver", "cs" => "chatserver", _ => "loginserver" };
		string prefix = server switch { "gs" => "game-server", "cs" => "chat-server", _ => "login-server" };
		string summaryProperty = server switch { "gs" => "expectedGameServerCrash", "cs" => "expectedChatServerCrash", _ => "expectedLoginServerCrash" };
		var options = run.Options() with { ExpectGameServerCrash = server == "gs" && variant != "no-opt-in",
			ExpectChatServerCrash = server == "cs" && variant != "no-opt-in",
			ExpectLoginServerCrash = server == "ls" && variant != "no-opt-in" };
		if (variant.StartsWith("tracked-", StringComparison.Ordinal))
		{
			string fingerprint = variant == "tracked-error" ? "1234abcd" :
				Aion.Commons.Logging.LogFingerprint.Create("Container process event: die", null, $"Container {service} die code=137.").Value;
			run.WriteLedger(JsonSerializer.Serialize(new[] { new
			{
				fp = fingerprint, firstSeenSha = "1111111", lastSeenSha = "2222222", lastSeenRun = "older",
				count = 1, status = "tracked", tracking = "TEST-existing-problem",
			} }));
		}
		var at = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
		string container = new('a', 64);
		File.WriteAllText(Path.Combine(options.RunDirectory, prefix + "-crash-plan.json"), JsonSerializer.Serialize(new
		{
			schemaVersion = 1, run = "test", project = "aion-bots-test", containerId = container,
			armedUtc = at, killDeadlineUtc = at.AddSeconds(30), recoveryDeadlineUtc = at.AddSeconds(180),
		}));
		var state = new ProblemWatcher.WatcherState(options);
		state.ReadFiles();
		Assert.Equal(options.ExpectsServerCrash, File.Exists(Path.Combine(options.RunDirectory, prefix + "-crash-armed.json")));
		void Docker(string action, int seconds, string? id = null, string? code = null)
		{
			var channel = Channel.CreateUnbounded<DockerLine>();
			var record = new Dictionary<string, object?>
			{
				["action"] = action, ["id"] = id ?? container, ["service"] = service,
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
			WriteInstanceHeartbeat(run, server, at.AddSeconds(variant == "pre-kill-heartbeat" ? 1.750 : 41));
			state.ReadFiles();
		}
		await state.WriteSummaryAsync(CancellationToken.None);
		Assert.Equal(expectedFailure != 0, state.FailingProblemCount > 0);
		using var summary = JsonDocument.Parse(File.ReadAllText(run.SummaryPath));
		Assert.Equal(expectedFailure != 0, summary.RootElement.GetProperty("failed").GetBoolean());
		if (variant == "pre-kill-heartbeat") Assert.False(summary.RootElement.GetProperty(summaryProperty).GetBoolean());
		if (variant == "normal")
		{
			Assert.Equal(2, summary.RootElement.GetProperty("expectedProcessEvents").GetInt32());
			Assert.True(summary.RootElement.GetProperty(summaryProperty).GetBoolean());
			Assert.Equal(0, summary.RootElement.GetProperty("suppressed").GetInt32());
			Assert.Contains($"EXPECTED_FAULT PROCESS {server} action=die", run.ReadDigest(), StringComparison.Ordinal);
		}
	}

	[Theory]
	[InlineData("gs", "game")]
	[InlineData("cs", "chat")]
	[InlineData("ls", "login")]
	public async Task CrashOptInWithoutAPlanFailsClosed(string server, string prefix)
	{
		using var run = new WatcherRun();
		Assert.Equal(1, await ProblemWatcher.RunAsync(run.Options() with { ExpectGameServerCrash = server == "gs",
			ExpectChatServerCrash = server == "cs", ExpectLoginServerCrash = server == "ls" }));
		Assert.Contains($"No valid {prefix}-server crash plan was armed", run.ReadDigest(), StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(true, false, false)]
	[InlineData(false, true, false)]
	[InlineData(false, false, true)]
	[InlineData(true, true, false)]
	[InlineData(true, false, true)]
	[InlineData(false, true, true)]
	[InlineData(true, true, true)]
	[InlineData(false, false, false)]
	public void CrashSelectionIsExplicitAndMutuallyExclusive(bool game, bool chat, bool login)
	{
		using var run = new WatcherRun();
		string[] args = ["--run", "test", "--run-dir", run.Options().RunDirectory,
			"--expect-game-server-crash", game.ToString(), "--expect-chat-server-crash", chat.ToString(),
			"--expect-login-server-crash", login.ToString()];
		if ((game ? 1 : 0) + (chat ? 1 : 0) + (login ? 1 : 0) > 1)
		{
			Assert.Throws<ArgumentException>(() => WatchOptions.Parse(args));
			Assert.Throws<ArgumentException>(() => new ProblemWatcher.WatcherState(run.Options() with
				{ ExpectGameServerCrash = game, ExpectChatServerCrash = chat, ExpectLoginServerCrash = login }));
			return;
		}
		var parsed = WatchOptions.Parse(args);
		Assert.Equal(login, parsed.ExpectLoginServerCrash);
		Assert.Equal(game || chat || login, parsed.ExpectsServerCrash);
		Assert.Equal(login ? "ls" : chat ? "cs" : "gs", parsed.CrashServer);
		Assert.Equal(login ? "login-server" : chat ? "chat-server" : "game-server", parsed.CrashPrefix);
	}

	[Fact]
	public void TwoServerCrashOptInsCannotBroadenTheFaultScope()
	{
		using var run = new WatcherRun();
		Assert.Throws<ArgumentException>(() => new ProblemWatcher.WatcherState(run.Options() with
			{ ExpectGameServerCrash = true, ExpectChatServerCrash = true }));
		var args = new[] { "--run", "test", "--run-dir", run.Options().RunDirectory, "--expect-chat-server-crash", "true" };
		Assert.True(WatchOptions.Parse(args).ExpectChatServerCrash);
		Assert.Throws<ArgumentException>(() => WatchOptions.Parse([..args, "--expect-game-server-crash", "true"]));
	}

	[Theory]
	[InlineData("gs", "ls")]
	[InlineData("gs", "gs2")]
	[InlineData("ls", "gs")]
	[InlineData("ls", "cs")]
	[InlineData("ls", "gs2")]
	[InlineData("ls", "cs2")]
	public async Task PlannedGapIsOnlyForSelectedServerAndNormalHeartbeatMonitoringResumes(string target, string otherServer)
	{
		using var run = new WatcherRun();
		var options = run.Options() with { ExpectGameServerCrash = target == "gs", ExpectLoginServerCrash = target == "ls",
			SecondGameServer = otherServer is "gs2" or "cs2" };
		string prefix = target == "gs" ? "game-server" : "login-server";
		var at = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
		string container = new('a', 64);
		string planJson = JsonSerializer.Serialize(new
		{
			schemaVersion = 1, run = "test", project = "aion-bots-test", containerId = container,
			armedUtc = at, killDeadlineUtc = at.AddSeconds(30), recoveryDeadlineUtc = at.AddSeconds(180),
		});
		File.WriteAllText(Path.Combine(options.RunDirectory, prefix + "-crash-plan.json"), planJson);
		var state = new ProblemWatcher.WatcherState(options);
		void Heartbeat(string server, int seconds)
		{
			WriteInstanceHeartbeat(run, server, at.AddSeconds(seconds));
			state.ReadFiles();
		}
		void Docker(string action, int seconds)
		{
			var channel = Channel.CreateUnbounded<DockerLine>();
			channel.Writer.TryWrite(new DockerLine("event", "docker", JsonSerializer.Serialize(new
			{
				action, id = container, service = target == "gs" ? "gameserver" : "loginserver", time = at.AddSeconds(seconds),
				attributes = new { exitCode = "137" },
			}), false) { ProjectName = options.ProjectName });
			state.ReadDocker(channel.Reader);
		}
		Heartbeat(target, 0);
		using (var receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(options.RunDirectory, prefix + "-crash-armed.json"))))
			Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(planJson))),
				receipt.RootElement.GetProperty("planSha256").GetString());
		Docker("die", 1);
		state.CheckHeartbeats(at.AddSeconds(25));
		Assert.Equal(0, state.FailingProblemCount);
		Heartbeat(otherServer, 0);
		state.CheckHeartbeats(at.AddSeconds(25));
		Assert.True(state.FailingProblemCount > 0); // Other servers never enter the planned gap.
		Docker("start", 40);
		Heartbeat(target, 41);
		state.CheckHeartbeats(at.AddSeconds(62));
		await state.WriteSummaryAsync(CancellationToken.None);
		Assert.Contains($"NEW HEARTBEAT {otherServer}", run.ReadDigest(), StringComparison.Ordinal);
		Assert.Contains("REPEAT " + target, run.ReadDigest(), StringComparison.Ordinal); // The shared heartbeat fingerprint repeats.
		using var summary = JsonDocument.Parse(File.ReadAllText(run.SummaryPath));
		Assert.Equal(2, summary.RootElement.GetProperty("total").GetInt32());
		Assert.True(summary.RootElement.GetProperty(target == "gs" ? "expectedGameServerCrash" : "expectedLoginServerCrash").GetBoolean());
		Assert.True(summary.RootElement.GetProperty("failed").GetBoolean());
	}
}
