using System.Text.Json;
using Aion.LogWatch;

namespace Aion.Commons.Tests;

public sealed class ProblemWatcherTests
{
	[Fact]
	public void FixTrailerParserRequiresAnExactTrailerLine()
	{
		const string fingerprint = "1234abcd";
		var log = "1111111111111111111111111111111111111111\u001fNot a trailer Fixes-Fingerprint: 1234abcd\u001e" +
			"2222222222222222222222222222222222222222\u001fFix the defect\n\nFixes-Fingerprint: 1234abcd\n\u001e";

		Assert.Equal(
			"2222222222222222222222222222222222222222",
			KnownProblemLedger.FindFixingCommitInLog(log, fingerprint));
		Assert.Null(KnownProblemLedger.FindFixingCommitInLog(log, "deadbeef"));
	}

	[Fact]
	public async Task ServerProblemJoinsLatestEarlierBotStepAndMarksFixedRateWorkInherited()
	{
		using var run = new WatcherRun();
		run.WriteTrace("""
			{"ts":"2026-09-17T12:34:55.000Z","vt":null,"run":"test","bot":"b01","account":"b01r0917","step":"s14","dir":"action","packet":"SelectDialog","fields":{}}
			{"ts":"2026-09-17T12:34:57.000Z","vt":null,"run":"test","bot":"b01","account":"b01r0917","step":"s15","dir":"action","packet":"Move","fields":{}}
			""");
		run.WriteProblem("1234abcd", timer: "fixed-rate", account: "b01r0917");

		var exitCode = await ProblemWatcher.RunAsync(run.Options());

		Assert.Equal(1, exitCode);
		var digest = run.ReadDigest();
		Assert.Contains("NEW ERROR gs fp=1234abcd bot=b01 step=s14", digest, StringComparison.Ordinal);
		Assert.Contains("op=CM_DIALOG_SELECT cat=QuestEngine inherited=true", digest, StringComparison.Ordinal);
	}

	[Fact]
	public async Task LiveAllowlistSuppressesUpToItsCountLimit()
	{
		using var run = new WatcherRun();
		run.WriteAllowlist("""
			[{"fp":"1234abcd","reason":"Synthetic test","owner":"e2e","tracking":"TEST-1","modes":["LIVE"],"servers":["gs"],"maxCount":1,"expires":"2099-12-31"}]
			""");
		run.WriteProblem("1234abcd");

		var exitCode = await ProblemWatcher.RunAsync(run.Options());

		Assert.Equal(0, exitCode);
		Assert.Contains("ALLOWLISTED ERROR gs fp=1234abcd tracking=TEST-1", run.ReadDigest(), StringComparison.Ordinal);
		using var summary = JsonDocument.Parse(File.ReadAllText(run.SummaryPath));
		Assert.Equal(1, summary.RootElement.GetProperty("suppressed").GetInt32());
	}

	[Fact]
	public async Task LiveAllowlistCountOverrunRemainsAProblem()
	{
		using var run = new WatcherRun();
		run.WriteAllowlist("""
			[{"fp":"1234abcd","reason":"Synthetic test","owner":"e2e","tracking":"TEST-1","modes":["LIVE"],"servers":["gs"],"maxCount":1,"expires":"2099-12-31"}]
			""");
		run.WriteProblem("1234abcd", count: 2);

		var exitCode = await ProblemWatcher.RunAsync(run.Options());

		Assert.Equal(1, exitCode);
		Assert.Contains("NEW ERROR gs fp=1234abcd", run.ReadDigest(), StringComparison.Ordinal);
		using var summary = JsonDocument.Parse(File.ReadAllText(run.SummaryPath));
		Assert.Equal(1, summary.RootElement.GetProperty("suppressed").GetInt32());
		Assert.Equal(1, summary.RootElement.GetProperty("new").GetInt32());
	}

	[Theory]
	[InlineData("tracked", "KNOWN", 0)]
	[InlineData("fixed", "REGRESSED", 1)]
	public async Task LedgerControlsDisposition(string status, string expected, int expectedExitCode)
	{
		using var run = new WatcherRun();
		run.WriteLedger($$"""
			[{"fp":"1234abcd","firstSeenSha":"1111111","lastSeenSha":"2222222","lastSeenRun":"older","count":3,"status":"{{status}}","tracking":"TEST-2","fixedIn":"deadbeef"}]
			""");
		run.WriteProblem("1234abcd");

		var exitCode = await ProblemWatcher.RunAsync(run.Options());

		Assert.Equal(expectedExitCode, exitCode);
		Assert.Contains($" {expected} ERROR gs fp=1234abcd", run.ReadDigest(), StringComparison.Ordinal);
	}

	[Fact]
	public async Task NewProblemUpdatesLedgerAndWritesReproductionBundle()
	{
		using var run = new WatcherRun();
		run.WriteProvenance("abcdef123", seed: 47, profile: "docker-test");
		var traces = Enumerable.Range(0, 55).Select(index => JsonSerializer.Serialize(new
		{
			ts = new DateTimeOffset(2026, 9, 17, 12, 34, 0, TimeSpan.Zero).AddSeconds(index),
			vt = (long?)null,
			run = "test",
			bot = "b01",
			account = "b01r0917",
			step = $"s{index:D2}",
			dir = "action",
			packet = "Move",
			fields = new { },
		}));
		run.WriteTrace(string.Join('\n', traces));
		run.WriteServerLog(Enumerable.Range(0, 220)
			.Select(index => index == 110 ? "Synthetic failure" : $"context {index}"));
		run.WriteProblem("1234abcd", account: "b01r0917");

		var exitCode = await ProblemWatcher.RunAsync(run.Options());

		Assert.Equal(1, exitCode);
		using var ledger = JsonDocument.Parse(File.ReadAllText(run.LedgerPath));
		var entry = Assert.Single(ledger.RootElement.EnumerateArray());
		Assert.Equal("abcdef123", entry.GetProperty("firstSeenSha").GetString());
		Assert.Equal("test", entry.GetProperty("lastSeenRun").GetString());
		Assert.Equal(1, entry.GetProperty("count").GetInt64());
		Assert.Equal("new", entry.GetProperty("status").GetString());

		var bundle = run.ProblemDirectory("1234abcd");
		Assert.Contains("Synthetic stack", File.ReadAllText(Path.Combine(bundle, "stack.txt")), StringComparison.Ordinal);
		var context = File.ReadAllLines(Path.Combine(bundle, "server-context.log"));
		Assert.Equal(200, context.Length);
		Assert.Contains("Synthetic failure", context);
		Assert.Equal(50, File.ReadAllLines(Path.Combine(bundle, "bot-trace.jsonl")).Length);
		using var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(bundle, "metadata.json")));
		Assert.Equal(47, metadata.RootElement.GetProperty("seed").GetInt32());
		Assert.Equal("docker-test", metadata.RootElement.GetProperty("configProfile").GetString());
		Assert.Contains("Tracking: TODO", File.ReadAllText(Path.Combine(bundle, "draft-backlog.md")), StringComparison.Ordinal);
	}

	[Fact]
	public async Task UnexpectedRefusalInBotTraceIsAProblem()
	{
		using var run = new WatcherRun();
		run.WriteTrace("""
			{"ts":"2026-09-17T12:34:55.000Z","vt":null,"run":"test","bot":"b02","account":"b02r0917","step":"s08","dir":"<","packet":"SM_SYSTEM_MESSAGE","fields":{"name":"STR_SKILL_NOT_READY","params":[]}}
			""");

		var exitCode = await ProblemWatcher.RunAsync(run.Options());

		Assert.Equal(1, exitCode);
		Assert.Contains("NEW ERROR bots", run.ReadDigest(), StringComparison.Ordinal);
		Assert.Contains("bot=b02 step=s08", run.ReadDigest(), StringComparison.Ordinal);
		Assert.Contains("Unexpected refusal system message STR_SKILL_NOT_READY", run.ReadDigest(), StringComparison.Ordinal);
	}

	[Fact]
	public async Task HeartbeatCheckActivatesOnlyAfterAHeartbeatWasObserved()
	{
		using var run = new WatcherRun();
		run.WriteEvent($$"""
			{"lvl":"INFO","ts":"{{DateTimeOffset.UtcNow.AddSeconds(-30):O}}","srv":"gs","run":"test","cat":"ServerHeartbeat","tpl":"Server heartbeat","msg":"Server heartbeat"}
			""");

		var exitCode = await ProblemWatcher.RunAsync(run.Options());

		Assert.Equal(1, exitCode);
		Assert.Contains("NEW HEARTBEAT gs", run.ReadDigest(), StringComparison.Ordinal);
		Assert.Contains("Heartbeat missed for 30 s", run.ReadDigest(), StringComparison.Ordinal);
	}

	[Fact]
	public async Task MalformedInputBecomesAProblemInsteadOfCrashingTheWatcher()
	{
		using var run = new WatcherRun();
		run.WriteTrace("{not-json}");

		var exitCode = await ProblemWatcher.RunAsync(run.Options());

		Assert.Equal(1, exitCode);
		Assert.Contains("Malformed bot trace JSONL record", run.ReadDigest(), StringComparison.Ordinal);
	}

	private sealed class WatcherRun : IDisposable
	{
		private readonly string directory = Path.Combine(Path.GetTempPath(), $"aion-logwatch-{Guid.NewGuid():N}");

		public WatcherRun()
		{
			Directory.CreateDirectory(Path.Combine(directory, "bots"));
			Directory.CreateDirectory(Path.Combine(directory, "logs", "gs"));
			WriteAllowlist("[]");
		}

		public string SummaryPath => Path.Combine(directory, "logwatch-summary.json");
		public string LedgerPath => Path.Combine(directory, "ledger.json");

		public WatchOptions Options() => new(
			"test",
			directory,
			"aion-bots-test",
			Path.Combine(directory, "compose.yml"),
			Path.Combine(directory, "allowlist.json"),
			Path.Combine(directory, "ledger.json"),
			WatchMode.Enforce,
			TimeSpan.Zero,
			null,
			DockerEnabled: false,
			FullRun: false,
			new HashSet<string>(["STR_SKILL_NOT_READY"], StringComparer.Ordinal));

		public void WriteAllowlist(string json) => File.WriteAllText(Path.Combine(directory, "allowlist.json"), json + "\n");

		public void WriteLedger(string json) => File.WriteAllText(Path.Combine(directory, "ledger.json"), json + "\n");

		public void WriteTrace(string json) => File.WriteAllText(Path.Combine(directory, "bots", "b01.trace.jsonl"), json + "\n");

		public void WriteProvenance(string gitSha, int seed, string profile) => File.WriteAllText(
			Path.Combine(directory, "bots-run.json"),
			JsonSerializer.Serialize(new { gitSha, seed, configProfile = profile }) + "\n");

		public void WriteServerLog(IEnumerable<string> lines) => File.WriteAllLines(
			Path.Combine(directory, "logs", "gs", "server_console.log"), lines);

		public void WriteEvent(string json) => File.WriteAllText(Path.Combine(directory, "logs", "gs", "gs.events.jsonl"), json + "\n");

		public void WriteProblem(string fingerprint, string? timer = null, string? account = null, int count = 1)
		{
			var record = new
			{
				lvl = "ERROR",
				ts = "2026-09-17T12:34:56.000Z",
				srv = "gs",
				run = "test",
				acct = account,
				op = "CM_DIALOG_SELECT",
				cat = "QuestEngine",
				timer,
				fp = fingerprint,
				tpl = "Synthetic failure",
				msg = "Synthetic failure",
				exType = "System.InvalidOperationException",
				frame = "Aion.GameServer.QuestEngine.OnDialog",
				stack = "Synthetic stack",
			};
			File.WriteAllLines(Path.Combine(directory, "logs", "gs", "gs.problems.jsonl"),
				Enumerable.Repeat(JsonSerializer.Serialize(record), count));
		}

		public string ReadDigest() => File.ReadAllText(Path.Combine(directory, "digest.log"));

		public string ProblemDirectory(string fingerprint) => Path.Combine(directory, "problems", fingerprint);

		public void Dispose()
		{
			if (Directory.Exists(directory))
				Directory.Delete(directory, recursive: true);
		}
	}
}
