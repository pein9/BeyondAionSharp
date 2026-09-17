using System.Globalization;
using System.Text;
using System.Text.Json;
using Aion.Commons.Logging;

namespace Aion.LogWatch;

public static class ProblemWatcher
{
	private static readonly string[] Servers = ["gs", "ls", "cs"];
	private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
	private static readonly TimeSpan MissingHeartbeatThreshold = TimeSpan.FromSeconds(20);

	public static async Task<int> RunAsync(WatchOptions options, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(options);
		var watcher = new WatcherState(options);
		var docker = options.DockerEnabled ? new DockerFollowers() : null;

		var startedAt = DateTimeOffset.UtcNow;
		var snapshotDeadline = options.Duration == TimeSpan.Zero && options.DockerEnabled
			? startedAt + TimeSpan.FromMilliseconds(750)
			: startedAt;
		try
		{
			docker?.Start(options);
			do
			{
				watcher.DiscoverTraceFiles();
				watcher.ReadFiles();
				if (docker != null)
					watcher.ReadDocker(docker.Lines);
				watcher.CheckHeartbeats(DateTimeOffset.UtcNow);

				if (ShouldStop(options, startedAt, snapshotDeadline))
					break;
				await Task.Delay(PollInterval, cancellationToken);
			}
			while (true);
		}
		finally
		{
			try
			{
				if (docker != null)
				{
					await Task.Delay(PollInterval, CancellationToken.None);
					watcher.ReadDocker(docker.Lines);
					await docker.DisposeAsync();
					watcher.ReadDocker(docker.Lines);
				}
			}
			finally
			{
				await watcher.WriteSummaryAsync(CancellationToken.None);
			}
		}

		return options.Mode == WatchMode.Enforce && watcher.FailingProblemCount > 0 ? 1 : 0;
	}

	private static bool ShouldStop(WatchOptions options, DateTimeOffset startedAt, DateTimeOffset snapshotDeadline)
	{
		if (options.StopFile != null && File.Exists(options.StopFile))
			return true;
		var now = DateTimeOffset.UtcNow;
		return options.Duration switch
		{
			null => false,
			{ } duration when duration == TimeSpan.Zero => now >= snapshotDeadline,
			{ } duration => now - startedAt >= duration,
		};
	}

	private sealed class WatcherState
	{
		private readonly WatchOptions options;
		private readonly LogProblemAllowlist allowlist;
		private readonly Dictionary<string, LedgerEntry> ledger;
		private readonly Dictionary<string, FileTail> traceTails = new(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, FileTail> eventTails = new(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, FileTail> problemTails = new(StringComparer.OrdinalIgnoreCase);
		private readonly FileTail botProblems;
		private readonly Dictionary<string, List<BotStep>> stepsByAccount = new(StringComparer.Ordinal);
		private readonly Dictionary<string, int> allowlistCounts = new(StringComparer.Ordinal);
		private readonly Dictionary<string, int> problemCounts = new(StringComparer.Ordinal);
		private readonly Dictionary<string, DateTimeOffset> lastHeartbeats = new(StringComparer.Ordinal);
		private readonly HashSet<string> heartbeatAlerts = new(StringComparer.Ordinal);
		private readonly StreamWriter digest;
		private int totalProblems;
		private int suppressedProblems;
		private int newProblems;
		private int knownProblems;
		private int regressedProblems;
		private int repeatedProblems;

		public WatcherState(WatchOptions options)
		{
			this.options = options;
			allowlist = LogProblemAllowlist.Load(options.AllowlistPath);
			ledger = LoadLedger(options.LedgerPath);
			botProblems = new FileTail(Path.Combine(options.RunDirectory, "bot.problems.jsonl"));
			foreach (var server in Servers)
			{
				eventTails.Add(server, new FileTail(Path.Combine(options.RunDirectory, "logs", server, $"{server}.events.jsonl")));
				problemTails.Add(server, new FileTail(Path.Combine(options.RunDirectory, "logs", server, $"{server}.problems.jsonl")));
			}

			var digestPath = Path.Combine(options.RunDirectory, "digest.log");
			digest = new StreamWriter(new FileStream(digestPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
				new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
			{
				AutoFlush = true,
				NewLine = "\n",
			};
		}

		public int FailingProblemCount => newProblems + regressedProblems;

		public void DiscoverTraceFiles()
		{
			var directory = Path.Combine(options.RunDirectory, "bots");
			if (!Directory.Exists(directory))
				return;
			foreach (var path in Directory.EnumerateFiles(directory, "*.trace.jsonl"))
				traceTails.TryAdd(Path.GetFullPath(path), new FileTail(path));
		}

		public void ReadFiles()
		{
			foreach (var tail in traceTails.Values)
				foreach (var line in tail.ReadNewLines())
					ReadJsonLine("bot trace", line, () => ReadTrace(line));

			foreach (var line in botProblems.ReadNewLines())
				ReadJsonLine("bot problem", line, () => ReadBotProblem(line));

			foreach (var server in Servers)
			{
				foreach (var line in eventTails[server].ReadNewLines())
					ReadJsonLine($"{server} event", line, () => ReadServerEvent(line));
				foreach (var line in problemTails[server].ReadNewLines())
					ReadJsonLine($"{server} problem", line, () => ReadServerProblem(line));
			}
		}

		public void ReadDocker(System.Threading.Channels.ChannelReader<DockerLine> reader)
		{
			while (reader.TryRead(out var line))
			{
				if (line.Source == "event")
					ReadDockerEvent(line.Line);
				else
					ReadDockerLog(line);
			}
		}

		public void CheckHeartbeats(DateTimeOffset now)
		{
			foreach (var (server, lastSeen) in lastHeartbeats)
			{
				if (now - lastSeen < MissingHeartbeatThreshold || !heartbeatAlerts.Add(server))
					continue;
				var message = $"Heartbeat missed for {Math.Floor((now - lastSeen).TotalSeconds):0} s.";
				AddSynthetic(now, server, "HEARTBEAT", "Heartbeat missed for {Server}", message, kind: "heartbeat");
			}
		}

		public async Task WriteSummaryAsync(CancellationToken cancellationToken)
		{
			digest.Dispose();
			var summary = new
			{
				run = options.Run,
				mode = options.Mode.ToString().ToLowerInvariant(),
				total = totalProblems,
				suppressed = suppressedProblems,
				@new = newProblems,
				known = knownProblems,
				regressed = regressedProblems,
				repeated = repeatedProblems,
				failed = options.Mode == WatchMode.Enforce && FailingProblemCount > 0,
			};
			await using var output = File.Create(Path.Combine(options.RunDirectory, "logwatch-summary.json"));
			await JsonSerializer.SerializeAsync(output, summary,
				new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
		}

		private void ReadTrace(string line)
		{
			using var document = JsonDocument.Parse(line);
			var root = document.RootElement;
			if (!BelongsToRun(root))
				return;
			var step = new BotStep(
				WatchProblem.RequiredString(root, "bot"),
				WatchProblem.RequiredString(root, "account"),
				WatchProblem.RequiredString(root, "step"),
				WatchProblem.ReadTimestamp(root),
				WatchProblem.RequiredString(root, "dir"),
				WatchProblem.RequiredString(root, "packet"));
			if (!stepsByAccount.TryGetValue(step.Account, out var steps))
			{
				steps = [];
				stepsByAccount.Add(step.Account, steps);
			}
			steps.Add(step);

			if (step.Direction != "<" || step.Packet != "SM_SYSTEM_MESSAGE" ||
				!root.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Object ||
				!fields.TryGetProperty("name", out var nameProperty) || nameProperty.ValueKind != JsonValueKind.String)
				return;
			var name = nameProperty.GetString()!;
			if (!options.UnexpectedRefusals.Contains(name))
				return;
			var message = $"Unexpected refusal system message {name}.";
			AddSynthetic(step.Timestamp, "bots", "ERROR", $"Unexpected refusal system message {name}", message,
				step.Account, bot: step.Bot, step: step.Step, kind: "unexpected-refusal");
		}

		private void ReadBotProblem(string line)
		{
			using var document = JsonDocument.Parse(line);
			var root = document.RootElement;
			if (!BelongsToRun(root))
				return;
			var message = WatchProblem.RequiredString(root, "msg");
			var kind = WatchProblem.RequiredString(root, "kind");
			var fingerprint = LogFingerprint.Create($"Bot problem: {kind}", null, $"Bot problem: {kind}");
			AddProblem(new WatchProblem(
				WatchProblem.ReadTimestamp(root),
				"bots",
				"ERROR",
				fingerprint.Value,
				fingerprint.NormalizedTemplate,
				message,
				WatchProblem.OptionalString(root, "exType"),
				fingerprint.Frame,
				WatchProblem.RequiredString(root, "account"),
				null,
				Bot: WatchProblem.RequiredString(root, "bot"),
				Step: WatchProblem.RequiredString(root, "step"),
				Kind: kind,
				Stack: WatchProblem.OptionalString(root, "stack")));
		}

		private void ReadServerEvent(string line)
		{
			using var document = JsonDocument.Parse(line);
			var root = document.RootElement;
			if (!BelongsToRun(root))
				return;
			var template = WatchProblem.RequiredString(root, "tpl");
			var message = WatchProblem.RequiredString(root, "msg");
			var category = WatchProblem.RequiredString(root, "cat");
			if (!template.Contains("heartbeat", StringComparison.OrdinalIgnoreCase) &&
				!message.Contains("heartbeat", StringComparison.OrdinalIgnoreCase) &&
				!category.Contains("heartbeat", StringComparison.OrdinalIgnoreCase))
				return;
			var server = WatchProblem.RequiredString(root, "srv");
			lastHeartbeats[server] = WatchProblem.ReadTimestamp(root);
			heartbeatAlerts.Remove(server);
		}

		private void ReadServerProblem(string line)
		{
			using var document = JsonDocument.Parse(line);
			if (BelongsToRun(document.RootElement))
				AddProblem(WatchProblem.FromServerJson(document.RootElement));
		}

		private void ReadDockerLog(DockerLine line)
		{
			var (timestamp, text) = SplitDockerTimestamp(line.Line);
			var server = MapService(line.Service);
			if (text.Contains("Unhandled exception.", StringComparison.Ordinal))
				AddSynthetic(timestamp, server, "PROCESS", "Unhandled exception.", text, kind: "process");
			if (line.Service == "mysql" && text.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase))
				AddSynthetic(timestamp, "mysql", "ERROR", "MySQL error", text, kind: "mysql");
		}

		private void ReadDockerEvent(string line)
		{
			if (string.IsNullOrWhiteSpace(line) || line[0] != '{')
				return;
			ReadJsonLine("Docker event", line, () =>
			{
				using var document = JsonDocument.Parse(line);
				var root = document.RootElement;
				var action = OptionalProperty(root, "action") ?? OptionalProperty(root, "Action") ??
					OptionalProperty(root, "status") ?? OptionalProperty(root, "Status");
				if (action is not ("die" or "oom" or "restart"))
					return;
				var service = OptionalProperty(root, "service") ?? ReadEventAttribute(root, "com.docker.compose.service") ?? "docker";
				var exitCode = ReadEventAttribute(root, "exitCode") ?? ReadEventAttribute(root, "exitcode");
				var message = $"Container {service} {action}" + (exitCode == null ? "." : $" code={exitCode}.");
				AddSynthetic(ReadDockerEventTimestamp(root), MapService(service), "PROCESS",
					$"Container process event: {action}", message, kind: "process");
			});
		}

		private void ReadJsonLine(string source, string line, Action read)
		{
			try
			{
				read();
			}
			catch (Exception exception) when (exception is JsonException or InvalidDataException or System.FormatException)
			{
				AddSynthetic(DateTimeOffset.UtcNow, "watcher", "ERROR", $"Malformed {source} JSONL record",
					$"Malformed {source} JSONL record: {exception.Message}", kind: "watcher-input");
			}
		}

		private void AddSynthetic(DateTimeOffset timestamp, string server, string level, string template, string message,
			string? account = null, string? bot = null, string? step = null, string? kind = null)
		{
			var fingerprint = LogFingerprint.Create(template, null, message);
			AddProblem(new WatchProblem(timestamp, server, level, fingerprint.Value,
				fingerprint.NormalizedTemplate, message, null, fingerprint.Frame, account, null,
				Bot: bot, Step: step, Kind: kind));
		}

		private void AddProblem(WatchProblem original)
		{
			totalProblems++;
			var problem = JoinStep(original);
			var allowlistEntry = allowlist.Entries.FirstOrDefault(entry =>
				entry.Fingerprint == problem.Fingerprint &&
				entry.Modes.Contains("LIVE", StringComparer.OrdinalIgnoreCase) &&
				entry.Servers.Contains(problem.Server, StringComparer.OrdinalIgnoreCase));
			if (allowlistEntry != null)
			{
				var count = allowlistCounts.GetValueOrDefault(problem.Fingerprint) + 1;
				allowlistCounts[problem.Fingerprint] = count;
				if (count <= allowlistEntry.MaxCount)
				{
					suppressedProblems++;
					return;
				}
			}

			var occurrence = problemCounts.GetValueOrDefault(problem.Fingerprint) + 1;
			problemCounts[problem.Fingerprint] = occurrence;
			if (occurrence > 1)
			{
				repeatedProblems++;
				digest.WriteLine($"{FormatTimestamp(problem.Timestamp)} REPEAT {problem.Server} fp={problem.Fingerprint} n={occurrence}");
				return;
			}

			ledger.TryGetValue(problem.Fingerprint, out var ledgerEntry);
			var disposition = ledgerEntry?.Status switch
			{
				"tracked" => "KNOWN",
				"fixed" => "REGRESSED",
				_ => "NEW",
			};
			switch (disposition)
			{
				case "KNOWN": knownProblems++; break;
				case "REGRESSED": regressedProblems++; break;
				default: newProblems++; break;
			}

			var details = new StringBuilder();
			if (ledgerEntry?.Tracking is { Length: > 0 } tracking)
				details.Append(" tracking=").Append(tracking);
			if (ledgerEntry?.FixedIn is { Length: > 0 } fixedIn)
				details.Append(" fixedIn=").Append(fixedIn);
			if (problem.Bot != null)
				details.Append(" bot=").Append(problem.Bot);
			if (problem.Step != null)
				details.Append(" step=").Append(problem.Step);
			if (problem.Operation != null)
				details.Append(" op=").Append(problem.Operation);
			if (problem.Category != null)
				details.Append(" cat=").Append(problem.Category);
			if (IsInherited(problem))
				details.Append(" inherited=true");

			var exception = FormatException(problem);
			digest.WriteLine($"{FormatTimestamp(problem.Timestamp)} {disposition} {problem.Level} {problem.Server} " +
				$"fp={problem.Fingerprint}{details} | {OneLine(problem.Message)}{exception}");
		}

		private WatchProblem JoinStep(WatchProblem problem)
		{
			if (problem.Bot != null || problem.Account == null || !stepsByAccount.TryGetValue(problem.Account, out var steps))
				return problem;
			var step = steps
				.Where(candidate => candidate.Timestamp <= problem.Timestamp)
				.MaxBy(candidate => candidate.Timestamp);
			return step == null ? problem : problem with { Bot = step.Bot, Step = step.Step };
		}

		private static bool IsInherited(WatchProblem problem)
		{
			if (problem.Timer == null)
				return false;
			var separator = problem.Timer.IndexOf('@');
			var kind = separator < 0 ? problem.Timer : problem.Timer[..separator];
			if (kind.Equals("fixed-rate", StringComparison.OrdinalIgnoreCase))
				return true;
			if (separator < 0 || !DateTimeOffset.TryParse(problem.Timer[(separator + 1)..],
				CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var scheduledAt))
				return false;
			return problem.Timestamp - scheduledAt > TimeSpan.FromSeconds(30);
		}

		private bool BelongsToRun(JsonElement root)
		{
			var run = WatchProblem.OptionalString(root, "run");
			return run == null || run == options.Run;
		}

		private static Dictionary<string, LedgerEntry> LoadLedger(string path)
		{
			var entries = new Dictionary<string, LedgerEntry>(StringComparer.Ordinal);
			if (!File.Exists(path))
				return entries;
			using var document = JsonDocument.Parse(File.ReadAllText(path));
			if (document.RootElement.ValueKind != JsonValueKind.Array)
				throw new InvalidDataException($"Known-problem ledger '{path}' must contain a JSON array.");
			foreach (var root in document.RootElement.EnumerateArray())
			{
				var entry = new LedgerEntry(
					WatchProblem.RequiredString(root, "fp"),
					WatchProblem.RequiredString(root, "status"),
					WatchProblem.OptionalString(root, "tracking"),
					WatchProblem.OptionalString(root, "fixedIn"));
				if (!entries.TryAdd(entry.Fingerprint, entry))
					throw new InvalidDataException($"Known-problem ledger fingerprint '{entry.Fingerprint}' is duplicated.");
			}
			return entries;
		}

		private static (DateTimeOffset Timestamp, string Text) SplitDockerTimestamp(string line)
		{
			var separator = line.IndexOf(' ');
			if (separator > 0 && DateTimeOffset.TryParse(line[..separator], CultureInfo.InvariantCulture,
				DateTimeStyles.AssumeUniversal, out var timestamp))
				return (timestamp, line[(separator + 1)..]);
			return (DateTimeOffset.UtcNow, line);
		}

		private static DateTimeOffset ReadDockerEventTimestamp(JsonElement root)
		{
			foreach (var name in new[] { "time", "Time" })
			{
				if (!root.TryGetProperty(name, out var value))
					continue;
				if (value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), out var parsed))
					return parsed;
				if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var seconds))
					return DateTimeOffset.FromUnixTimeSeconds(seconds);
			}
			return DateTimeOffset.UtcNow;
		}

		private static string? ReadEventAttribute(JsonElement root, string name)
		{
			foreach (var containerName in new[] { "attributes", "Attributes" })
			{
				if (root.TryGetProperty(containerName, out var attributes) && attributes.ValueKind == JsonValueKind.Object)
					return OptionalProperty(attributes, name);
			}
			if (root.TryGetProperty("Actor", out var actor) && actor.ValueKind == JsonValueKind.Object &&
				actor.TryGetProperty("Attributes", out var actorAttributes) && actorAttributes.ValueKind == JsonValueKind.Object)
				return OptionalProperty(actorAttributes, name);
			return null;
		}

		private static string? OptionalProperty(JsonElement root, string name)
		{
			if (!root.TryGetProperty(name, out var property))
				return null;
			return property.ValueKind switch
			{
				JsonValueKind.String => property.GetString(),
				JsonValueKind.Number => property.GetRawText(),
				_ => null,
			};
		}

		private static string MapService(string service) => service switch
		{
			"gameserver" => "gs",
			"loginserver" => "ls",
			"chatserver" => "cs",
			_ => service,
		};

		private static string FormatTimestamp(DateTimeOffset value) =>
			value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

		private static string OneLine(string value) => value.Replace('\r', ' ').Replace('\n', ' ');

		private static string FormatException(WatchProblem problem)
		{
			if (problem.ExceptionType == null && (problem.Frame == null || problem.Frame == "<unknown>"))
				return string.Empty;
			var type = problem.ExceptionType?.Split('.').Last();
			var frame = problem.Frame?.Split('.').Last();
			return $" | {type ?? "<none>"} @ {frame ?? "<unknown>"}";
		}
	}
}
