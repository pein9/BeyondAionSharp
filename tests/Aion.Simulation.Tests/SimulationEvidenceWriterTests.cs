using System.Security.Cryptography;
using System.Text.Json;
using Aion.Bots.Protocol;
using Aion.Commons.Logging;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.TestKit;
using Microsoft.Extensions.Logging;

namespace Aion.Simulation.Tests;

public sealed class SimulationEvidenceWriterTests : IDisposable
{
	private readonly string directory = Path.Combine(Path.GetTempPath(), "aion-sim-evidence-" + Guid.NewGuid().ToString("N"));
	private string Ledger => Path.Combine(directory, "ledger.json");
	private string Allowance => Path.Combine(directory, "allowlist.json");
	private string Output => Path.Combine(directory, "run");

	public SimulationEvidenceWriterTests()
	{
		Directory.CreateDirectory(directory);
		File.WriteAllText(Ledger, "[]");
		File.WriteAllText(Allowance, "[]");
	}

	private SimulationEvidenceWriter Writer(VirtualThreadPool clock, TimeProvider? time = null) =>
		new(Output, "sim-test", 17, "source-sha", "sim-fast", clock, Ledger, Allowance, time);
	private SimulationLogPolicy Policy(VirtualThreadPool clock, SimulationEvidenceWriter writer) =>
		new("sim-test", "S0", clock, writer.AllowlistPath, evidence: writer);
	private JsonElement[] Rows(string name = "sim-problems.jsonl")
	{
		using var stream = new FileStream(Path.Combine(Output, name), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		using var reader = new StreamReader(stream);
		var rows = new List<JsonElement>();
		while (reader.ReadLine() is { } line) rows.Add(JsonSerializer.Deserialize<JsonElement>(line));
		return rows.ToArray();
	}
	private static SimulationProblem Problem(string fp = "1234abcd") =>
		new(fp, "log", "gs", LogLevel.Error, "failed", "System.Exception: full stack", "b01", "s01");

	[Fact]
	public async Task CleanNestedPoliciesHaveDistinctFlushedReceiptsAndFrozenInputs()
	{
		await using var clock = new VirtualThreadPool(strict: true);
		using (var writer = Writer(clock))
		{
			using (var outer = Policy(clock, writer))
			{
				using (var inner = Policy(clock, writer)) inner.AssertClean();
				outer.AssertClean();
			}
			Assert.Equal(5, Rows().Length); // Available before writer disposal.
		}
		var rows = Rows();
		Assert.Equal("run-started", rows[0].GetProperty("event").GetString());
		Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Ledger))), rows[0].GetProperty("ledgerSha256").GetString());
		Assert.Equal(File.ReadAllBytes(Allowance), File.ReadAllBytes(Path.Combine(Output, "sim-allowlist-at-start.json")));
		Assert.Equal(new long[] { 1, 2 }, rows.Where(r => r.GetProperty("event").GetString() == "policy-started").Select(r => r.GetProperty("policy").GetInt64()));
		Assert.Equal(2, rows[^1].GetProperty("policiesCompleted").GetInt32());
		Assert.Empty(rows[^1].GetProperty("activePolicies").EnumerateArray());
		var resources = Rows("sim-resources.jsonl");
		Assert.Equal(new[] { "run-started", "policy-started", "policy-started", "policy-completed", "policy-completed", "run-completed" },
			resources.Where(r => r.GetProperty("event").GetString() == "sample").Select(r => r.GetProperty("trigger").GetString()));
	}

	[Theory]
	[InlineData("new", false, "NEW")]
	[InlineData("tracked", false, "KNOWN")]
	[InlineData("fixed", false, "REGRESSED")]
	[InlineData("new", true, "ALLOWLISTED")]
	public async Task ClassificationIsFrozenAndNeverWritesTheGlobalLedger(string status, bool allowed, string expected)
	{
		await using var clock = new VirtualThreadPool(strict: true);
		File.WriteAllText(Ledger, JsonSerializer.Serialize(new[] { new { fp = "1234abcd", status } }));
		using (var writer = Writer(clock))
		{
			File.WriteAllText(Ledger, "[]");
			writer.CompletePolicy(writer.BeginPolicy("S0"), true, allowed, [Problem()], [allowed], []);
		}
		Assert.Equal("[]", File.ReadAllText(Ledger));
		Assert.Equal(expected, Rows()[2].GetProperty("observations")[0].GetProperty("disposition").GetString());
		Assert.Equal(expected == "NEW", Directory.Exists(Path.Combine(Output, "problems", "1234abcd")));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task DisposeExportsErrorsEvenWhenCleanAssertionIsNotReachedOrErrorArrivesAfterIt(bool assertFirst)
	{
		await using var clock = new VirtualThreadPool(strict: true);
		using (var writer = Writer(clock))
		{
			using var policy = Policy(clock, writer);
			if (assertFirst) policy.AssertClean();
			using (policy.BeginBotStep("b01", "cleanup"))
				AionLog.For("SIM_TEST").LogError(new ApplicationException("original exception"), "cleanup failed");
		}
		var terminal = Rows()[2];
		Assert.Equal(assertFirst, terminal.GetProperty("assertionPassed").GetBoolean());
		var problem = Assert.Single(terminal.GetProperty("observations").EnumerateArray());
		Assert.Equal("cleanup", problem.GetProperty("step").GetString());
		Assert.Contains("ApplicationException: original exception", problem.GetProperty("exceptionText").GetString());
		string bundle = Path.Combine(Output, "problems", problem.GetProperty("fingerprint").GetString()!);
		Assert.Equal(5, Directory.GetFiles(bundle).Length);
		Assert.Contains("original exception", File.ReadAllText(Path.Combine(bundle, "stack.txt")));
	}

	[Fact]
	public async Task AssertionFailureAndVirtualFaultAreExportedWithoutLosingOriginalException()
	{
		await using var clock = new VirtualThreadPool(strict: false);
		using (var writer = Writer(clock))
		{
			using var policy = Policy(clock, writer);
			policy.ObserveAction("b01", "account", "s01", "login");
			policy.ObserveSent("b01", "account", "s01", GameClientPackets.Ping());
			policy.ObservePacket("b01", "s01", new DecodedBotServerPacket(typeof(SM_PONG), new Dictionary<string, object?>()), "account");
			clock.Schedule(_ => throw new InvalidOperationException("timer failed"), TimeSpan.Zero);
			clock.Advance(TimeSpan.Zero);
			var failure = Assert.Throws<SimulationLogPolicyException>(policy.AssertClean);
			Assert.Contains("timer failed", failure.Message);
		}
		var terminal = Rows()[2];
		Assert.False(terminal.GetProperty("assertionPassed").GetBoolean());
		Assert.Equal("virtual-timer", terminal.GetProperty("observations")[0].GetProperty("kind").GetString());
		Assert.Equal(new[] { "action", ">", "<" }, Rows("bots/S0.b01.account.trace.jsonl").Select(r => r.GetProperty("dir").GetString()));
		Assert.Single(Rows("sim-resources.jsonl"), r => r.TryGetProperty("trigger", out var trigger) && trigger.GetString() == "bot-action");
	}

	[Fact]
	public async Task PolicyUsesFrozenAllowanceAndReportsOverflowSeparately()
	{
		await using var clock = new VirtualThreadPool(strict: true);
		string fp;
		using (var probe = new SimulationLogPolicy("probe", "S0", clock, Allowance))
		{
			AionLog.For("SIM_TEST").LogError("known problem");
			fp = Assert.Single(Assert.Throws<SimulationLogPolicyException>(probe.AssertClean).Problems).Fingerprint;
		}
		File.WriteAllText(Allowance, JsonSerializer.Serialize(new[] { new { fp, owner = "tests", reason = "negative control",
			tracking = "P10-10", modes = new[] { "SIM" }, servers = new[] { "gs" }, maxCount = 1, expires = "2099-01-01" } }));
		using (var writer = Writer(clock))
		{
			File.WriteAllText(Allowance, "[]");
			using var policy = Policy(clock, writer);
			AionLog.For("SIM_TEST").LogError("known problem");
			AionLog.For("SIM_TEST").LogError("known problem");
			Assert.Single(Assert.Throws<SimulationLogPolicyException>(policy.AssertClean).Problems);
		}
		var observations = Rows()[2].GetProperty("observations").EnumerateArray().ToArray();
		Assert.Equal(new[] { "ALLOWLISTED", "NEW" }, observations.Select(r => r.GetProperty("disposition").GetString()));
	}

	[Fact]
	public async Task ContextPrefersThisOccurrenceTimestampOverAnOlderMatchingFingerprint()
	{
		await using var clock = new VirtualThreadPool(strict: true);
		var stamp = DateTimeOffset.Parse("2026-09-20T12:00:00Z");
		var fp = LogFingerprint.Create("same problem", null, "same problem");
		var context = Enumerable.Range(0, 400).Select(i => new CapturedLogEntry(stamp.AddSeconds(i), "SIM_TEST", LogLevel.Error,
			default, "same problem", $"line-{i}", null, fp, new Dictionary<string, string>())).ToArray();
		using (var writer = Writer(clock))
			writer.CompletePolicy(writer.BeginPolicy("S0"), true, false, [Problem(fp.Value) with { ObservedAt = stamp.AddSeconds(250) }], [false], context);
		var lines = File.ReadAllLines(Path.Combine(Output, "problems", fp.Value, "server-context.log"));
		Assert.Contains("line-150", lines[0]);
		Assert.Contains("line-349", lines[^1]);
	}

	[Fact]
	public async Task ReproTailsAreBoundedScopedBeforeObservationAndDoNotOverwriteFirstFailure()
	{
		await using var clock = new VirtualThreadPool(strict: true);
		var time = new MutableTimeProvider();
		using (var writer = Writer(clock, time))
		{
			for (int i = 0; i < 75; i++) writer.TraceAction("S0", "b01", "account", "s01", $"before-{i}");
			writer.TraceAction("S0", "b02", "other", "s01", "unrelated");
			writer.TraceAction("M1", "b01", "account", "s01", "other-scenario");
			var observed = time.GetUtcNow();
			time.Now = time.Now.AddSeconds(1);
			writer.TraceAction("S0", "b01", "account", "s02", "after");
			var context = Enumerable.Range(0, 400).Select(i => new CapturedLogEntry(observed, "SIM_TEST", LogLevel.Information,
				default, "context", $"line-{i}", null, LogFingerprint.Create("context", null, "context"), new Dictionary<string, string>())).ToArray();
			writer.CompletePolicy(writer.BeginPolicy("S0"), true, false, [Problem() with { ObservedAt = observed }], [false], context);
			writer.CompletePolicy(writer.BeginPolicy("S0"), true, false, [Problem() with { ExceptionText = "replacement" }], [false], []);
		}
		string bundle = Path.Combine(Output, "problems", "1234abcd");
		string[] trace = File.ReadAllLines(Path.Combine(bundle, "bot-trace.jsonl"));
		Assert.Equal(50, trace.Length);
		Assert.Contains("before-25", trace[0]);
		Assert.Contains("before-74", trace[^1]);
		Assert.Equal(200, File.ReadAllLines(Path.Combine(bundle, "server-context.log")).Length);
		Assert.Contains("full stack", File.ReadAllText(Path.Combine(bundle, "stack.txt")));
	}

	[Fact]
	public async Task ExportFailureStillRestoresLoggerScopeAndDisposeIsIdempotent()
	{
		await using var clock = new VirtualThreadPool(strict: true);
		using var capture = new CapturingLoggerProvider();
		using var factory = LoggerFactory.Create(builder => builder.AddProvider(capture));
		using var original = AionLog.OverrideFactory(factory);
		using var writer = Writer(clock);
		var policy = Policy(clock, writer);
		writer.Dispose();
		Assert.Throws<ObjectDisposedException>(policy.Dispose);
		policy.Dispose();
		AionLog.For("RESTORED").LogError("after export failure");
		Assert.Contains(capture.Entries, row => row.Category == "RESTORED");
		Assert.Equal(1, Rows()[^1].GetProperty("activePolicies").GetArrayLength());
	}

	[Fact]
	public async Task ReusedEvidenceDirectoryIsRejectedInsteadOfOverwritten()
	{
		await using var clock = new VirtualThreadPool(strict: true);
		using (var writer = Writer(clock)) { }
		byte[] original = File.ReadAllBytes(Path.Combine(Output, "sim-problems.jsonl"));
		Assert.Throws<IOException>(() => Writer(clock));
		Assert.Equal(original, File.ReadAllBytes(Path.Combine(Output, "sim-problems.jsonl")));
	}

	[Fact]
	public async Task FailedPolicyConstructionRestoresLoggerFactory()
	{
		await using var clock = new VirtualThreadPool(strict: true);
		using var capture = new CapturingLoggerProvider();
		using var factory = LoggerFactory.Create(builder => builder.AddProvider(capture));
		using var original = AionLog.OverrideFactory(factory);
		using var writer = Writer(clock);
		writer.Dispose();
		Assert.Throws<ObjectDisposedException>(() => Policy(clock, writer));
		AionLog.For("RESTORED").LogError("after construction failure");
		Assert.Contains(capture.Entries, row => row.Category == "RESTORED");
	}

	[Theory]
	[InlineData("../escape")]
	[InlineData("bad\n")]
	public async Task InvalidPathIdentitiesAreRejected(string value)
	{
		await using var clock = new VirtualThreadPool(strict: true);
		using var writer = Writer(clock);
		Assert.Throws<ArgumentException>(() => writer.BeginPolicy(value));
		Assert.Throws<ArgumentException>(() => writer.TraceAction("S0", value, "account", "s01", "bad"));
		Assert.Throws<InvalidDataException>(() => writer.CompletePolicy(writer.BeginPolicy("S0"), true, false, [Problem(value)], [false], []));
	}

	[Fact]
	public async Task ExistingReproCannotBeSilentlyReplaced()
	{
		await using var clock = new VirtualThreadPool(strict: true);
		using var writer = Writer(clock);
		string bundle = Path.Combine(Output, "problems", "1234abcd");
		Directory.CreateDirectory(bundle);
		File.WriteAllText(Path.Combine(bundle, "stack.txt"), "original evidence");
		Assert.Throws<IOException>(() => writer.CompletePolicy(writer.BeginPolicy("S0"), true, false, [Problem()], [false], []));
		Assert.Equal("original evidence", File.ReadAllText(Path.Combine(bundle, "stack.txt")));
	}

	private sealed class MutableTimeProvider : TimeProvider
	{
		public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-09-20T12:00:00Z");
		public override DateTimeOffset GetUtcNow() => Now;
	}

	public void Dispose() => Directory.Delete(directory, recursive: true);
}
