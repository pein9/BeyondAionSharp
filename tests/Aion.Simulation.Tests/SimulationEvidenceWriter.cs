using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aion.Bots.Protocol;
using Aion.Bots.Tracing;
using Aion.Commons.Logging;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

/// <summary>Retains SIM policy observations without changing their failure/allowance decisions.</summary>
public sealed class SimulationEvidenceWriter : IDisposable
{
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
	private readonly string directory;
	private readonly string run;
	private readonly int seed;
	private readonly string gitSha;
	private readonly string profile;
	private readonly VirtualThreadPool clock;
	private readonly TimeProvider wallClock;
	private readonly StreamWriter output;
	private readonly SimulationResourceWriter resources;
	private readonly Dictionary<string, string> statuses = new(StringComparer.Ordinal);
	private readonly Dictionary<long, string> active = [];
	private readonly Dictionary<(string Scenario, string Bot, string Account), BotActionTraceWriter> traces = [];
	private readonly HashSet<string> bundles = new(StringComparer.Ordinal);
	private readonly object gate = new();
	private long started;
	private long completed;
	private bool disposed;

	public string AllowlistPath { get; }

	public SimulationEvidenceWriter(string directory, string run, int seed, string gitSha, string profile,
		VirtualThreadPool clock, string ledgerPath, string allowlistPath, TimeProvider? wallClock = null)
	{
		this.directory = Path.GetFullPath(directory);
		this.run = Id(run);
		this.seed = seed;
		this.gitSha = gitSha;
		this.profile = profile;
		this.clock = clock;
		this.wallClock = wallClock ?? TimeProvider.System;
		Directory.CreateDirectory(this.directory);
		byte[] ledger = File.ReadAllBytes(ledgerPath);
		using (var document = JsonDocument.Parse(ledger))
		{
			foreach (var entry in document.RootElement.EnumerateArray())
			{
				string fingerprint = entry.GetProperty("fp").GetString()!;
				string status = entry.GetProperty("status").GetString()!;
				if (!Regex.IsMatch(fingerprint, "^[a-f0-9]{8}$") || status is not ("new" or "tracked" or "fixed") ||
					!statuses.TryAdd(fingerprint, status)) throw new InvalidDataException("Invalid/duplicate SIM ledger classification.");
			}
		}
		byte[] allowance = File.ReadAllBytes(allowlistPath);
		AllowlistPath = Path.Combine(this.directory, "sim-allowlist-at-start.json");
		WriteNew(Path.Combine(this.directory, "sim-ledger-at-start.json"), ledger);
		WriteNew(AllowlistPath, allowance);
		output = new StreamWriter(new FileStream(Path.Combine(this.directory, "sim-problems.jsonl"),
			FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
		try
		{
			Write(new { schemaVersion = 1, @event = "run-started", run, mode = "SIM", seed, gitSha, profile, startedUtc = this.wallClock.GetUtcNow(),
				ledgerSha256 = Convert.ToHexStringLower(SHA256.HashData(ledger)),
				allowlistSha256 = Convert.ToHexStringLower(SHA256.HashData(allowance)) });
			resources = new SimulationResourceWriter(this.directory, run, seed, gitSha, profile, clock);
		}
		catch { output.Dispose(); throw; }
	}

	public long BeginPolicy(string scenario)
	{
		lock (gate)
		{
			ObjectDisposedException.ThrowIf(disposed, this);
			string identity = Id(scenario);
			long id = ++started;
			active.Add(id, identity);
			Write(new { @event = "policy-started", run, policy = id, scenario });
			resources.Sample("policy-started", scenario, id);
			return id;
		}
	}

	public void CompletePolicy(long id, bool assertedClean, bool assertionPassed,
		IReadOnlyList<SimulationProblem> problems, IReadOnlyList<bool> allowed, IReadOnlyList<CapturedLogEntry> context)
	{
		lock (gate)
		{
			ObjectDisposedException.ThrowIf(disposed, this);
			if (!active.TryGetValue(id, out string? scenario) || problems.Count != allowed.Count)
				throw new InvalidOperationException("SIM evidence policy/count mismatch.");
			var observations = problems.Select((problem, index) =>
			{
				if (!Regex.IsMatch(problem.Fingerprint, "\\A[a-f0-9]{8}\\z"))
					throw new InvalidDataException("Invalid SIM problem fingerprint.");
				string disposition = allowed[index] ? "ALLOWLISTED" : statuses.GetValueOrDefault(problem.Fingerprint) switch
				{
					"tracked" => "KNOWN", "fixed" => "REGRESSED", _ => "NEW",
				};
				if (disposition == "NEW" && bundles.Add(problem.Fingerprint)) WriteBundle(scenario, problem, context);
				return new { problem.Fingerprint, disposition, allowlisted = allowed[index], problem.Kind,
					problem.Server, level = problem.Level.ToString(), problem.Message, problem.ExceptionText, problem.Bot, problem.Step, problem.ObservedAt };
			}).ToArray();
			Write(new { @event = "policy-completed", run, policy = id, scenario, assertedClean, assertionPassed,
				virtualMillis = clock.NowMillis, observations });
			resources.Sample("policy-completed", scenario, id);
			active.Remove(id);
			completed++;
		}
	}

	public void TraceAction(string scenario, string bot, string account, string step, string action)
	{
		lock (gate)
		{
			Trace(scenario, bot, account).WriteAction(step, action);
			resources.Sample("bot-action", scenario, bot: bot, account: account, step: step);
		}
	}

	public void TraceSent(string scenario, string bot, string account, string step, BotClientPacket packet)
	{
		lock (gate) Trace(scenario, bot, account).WriteSent(step, packet);
	}

	public void TraceReceived(string scenario, string bot, string account, string step, DecodedBotServerPacket packet)
	{
		lock (gate) Trace(scenario, bot, account).WriteReceived(step, packet);
	}

	private BotActionTraceWriter Trace(string scenario, string bot, string account)
	{
		ObjectDisposedException.ThrowIf(disposed, this);
		var key = (Id(scenario), Id(bot), Id(account));
		if (!traces.TryGetValue(key, out var trace))
		{
			Directory.CreateDirectory(Path.Combine(directory, "bots"));
			trace = new BotActionTraceWriter(new FileStream(TracePath(key), FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite),
				run, bot, account, wallClock, virtualTime: () => TimeSpan.FromMilliseconds(clock.NowMillis));
			traces.Add(key, trace);
		}
		return trace;
	}

	private string TracePath((string Scenario, string Bot, string Account) key) =>
		Path.Combine(directory, "bots", $"{key.Scenario}.{key.Bot}.{key.Account}.trace.jsonl");

	private void WriteBundle(string scenario, SimulationProblem problem, IReadOnlyList<CapturedLogEntry> context)
	{
		string path = Path.Combine(directory, "problems", problem.Fingerprint);
		if (Directory.Exists(path)) throw new IOException("SIM repro directory already exists; refusing to overwrite evidence.");
		Directory.CreateDirectory(path);
		File.WriteAllText(Path.Combine(path, "stack.txt"), (problem.ExceptionText ?? "<no stack captured>") + "\n");
		var entries = context.ToArray();
		int match = Array.FindIndex(entries, entry => entry.Timestamp == problem.ObservedAt);
		if (match < 0) match = Array.FindIndex(entries, entry => entry.Fingerprint.Value == problem.Fingerprint);
		int start = match < 0 ? Math.Max(0, context.Count - 200) : Math.Max(0, match - 100);
		File.WriteAllLines(Path.Combine(path, "server-context.log"), context.Skip(start).Take(200).Select(entry =>
			$"{entry.Timestamp:O} {entry.Level} {entry.Category} {entry.Message.Replace('\r', ' ').Replace('\n', ' ')}"));
		var selected = new List<(DateTimeOffset Timestamp, string Line)>();
		foreach (var key in traces.Keys.Where(key => key.Scenario == scenario && (string.IsNullOrEmpty(problem.Bot) || key.Bot == problem.Bot)))
		{
			// Share with the still-open trace writer; File.ReadLines alone fails on Windows.
			using var stream = new FileStream(TracePath(key), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			using var reader = new StreamReader(stream);
			var tail = new Queue<(DateTimeOffset Timestamp, string Line)>();
			while (reader.ReadLine() is { } line)
			{
				using var json = JsonDocument.Parse(line);
				DateTimeOffset timestamp = json.RootElement.GetProperty("ts").GetDateTimeOffset();
				if (problem.ObservedAt != null && timestamp > problem.ObservedAt) continue;
				tail.Enqueue((timestamp, line));
				if (tail.Count > 50) tail.Dequeue();
			}
			selected.AddRange(tail);
		}
		string[] traceLines = selected.OrderBy(row => row.Timestamp).TakeLast(50).Select(row => row.Line).ToArray();
		File.WriteAllLines(Path.Combine(path, "bot-trace.jsonl"), traceLines);
		File.WriteAllText(Path.Combine(path, "metadata.json"), JsonSerializer.Serialize(new
		{
			run, mode = "SIM", scenario, fingerprint = problem.Fingerprint, server = problem.Server,
			problem.Bot, problem.Step, seed, gitSha, configProfile = profile, traceRecords = traceLines.Length,
			contextRecords = Math.Min(200, context.Count - start), virtualMillis = clock.NowMillis,
		}, JsonOptions));
		File.WriteAllText(Path.Combine(path, "draft-backlog.md"),
			$"### {problem.Fingerprint} — SIM {scenario}\n\nRun `{run}`, source `{gitSha}`, seed `{seed}`, profile `{profile}`.\n\n" +
			"Tracking: TODO. See stack.txt, server-context.log, bot-trace.jsonl and metadata.json.\n");
	}

	private void Write(object value) => output.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
	private static void WriteNew(string path, byte[] bytes)
	{
		using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
		stream.Write(bytes);
	}
	private static string Id(string value) => Regex.IsMatch(value, "\\A[A-Za-z0-9][A-Za-z0-9_-]*\\z")
		? value : throw new ArgumentException("Invalid SIM evidence identity.", nameof(value));

	public void Dispose()
	{
		lock (gate)
		{
			if (disposed) return;
			disposed = true;
			try { Write(new { @event = "run-completed", run, policiesStarted = started, policiesCompleted = completed, activePolicies = active.Keys.ToArray() }); }
			finally
			{
				try { resources.Dispose(); }
				finally
				{
					foreach (var trace in traces.Values) trace.Dispose();
					output.Dispose();
				}
			}
		}
	}
}
