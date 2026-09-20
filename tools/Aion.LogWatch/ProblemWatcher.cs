using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aion.Commons.Logging;

namespace Aion.LogWatch;

public static class ProblemWatcher
{
	private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

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

	internal sealed class WatcherState
	{
		private readonly WatchOptions options;
		private readonly LogProblemAllowlist allowlist;
		private readonly KnownProblemLedger ledger;
		private readonly Dictionary<string, FileTail> traceTails = new(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, FileTail> eventTails = new(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, FileTail> problemTails = new(StringComparer.OrdinalIgnoreCase);
		private readonly FileTail botProblems;
		private readonly BotTraceHistory traceHistory;
		private readonly Dictionary<string, int> allowlistCounts = new(StringComparer.Ordinal);
		private readonly Dictionary<string, int> problemCounts = new(StringComparer.Ordinal);
		private readonly Dictionary<string, WatchProblem> newProblemSamples = new(StringComparer.Ordinal);
		private readonly Dictionary<string, DateTimeOffset> lastHeartbeats = new(StringComparer.Ordinal);
		private readonly Dictionary<string, string> lastHeartbeatRecords = new(StringComparer.Ordinal);
		private readonly HangDiagnostics hangDiagnostics;
		private readonly HashSet<string> heartbeatAlerts = new(StringComparer.Ordinal);
		private readonly DateTimeOffset watchingStarted = DateTimeOffset.UtcNow;
		private int knownHeartbeatProblems;
		private readonly StreamWriter digest;
		private int totalProblems;
		private int suppressedProblems;
		private int newProblems;
		private int knownProblems;
		private int regressedProblems;
		private int repeatedProblems;
		private ServerCrashExpectation? expectedCrash;
		private bool crashPlanRead;
		private bool crashFailureReported;
		private bool crashGapReported;
		private int expectedProcessEvents;

		public WatcherState(WatchOptions options, IDiagnosticCommand? diagnosticCommand = null)
		{
			options.ValidateHeartbeatThresholds();
			this.options = options;
			hangDiagnostics = new HangDiagnostics(options, diagnosticCommand ?? new BoundedDiagnosticCommand());
			traceHistory = new BotTraceHistory(options.Run);
			allowlist = LogProblemAllowlist.Load(options.AllowlistPath);
			ledger = KnownProblemLedger.Load(options.LedgerPath);
			botProblems = new FileTail(Path.Combine(options.RunDirectory, "bot.problems.jsonl"));
			foreach (var server in options.Servers)
			{
				string producer = server switch { "gs2" => "gs", "cs2" => "cs", _ => server };
				eventTails.Add(server, new FileTail(Path.Combine(options.RunDirectory, "logs", server, $"{producer}.events.jsonl")));
				problemTails.Add(server, new FileTail(Path.Combine(options.RunDirectory, "logs", server, $"{producer}.problems.jsonl")));
			}

			var digestPath = Path.Combine(options.RunDirectory, "digest.log");
			digest = new StreamWriter(new FileStream(digestPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
				new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
			{
				AutoFlush = true,
				NewLine = "\n",
			};
		}

		public int FailingProblemCount => newProblems + regressedProblems +
			(options.ExpectsServerCrash ? knownProblems : knownHeartbeatProblems) + (crashFailureReported ? 1 : 0);

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
			if (options.ExpectsServerCrash && !crashPlanRead)
			{
				string path = Path.Combine(options.RunDirectory, options.CrashPrefix + "-crash-plan.json");
				if (File.Exists(path))
				{
					crashPlanRead = true;
					ReadJsonLine(options.CrashPrefix + " crash plan", "", () =>
					{
						string json = File.ReadAllText(path);
						expectedCrash = ServerCrashExpectation.Load(json, options.Run, options.ProjectName, DateTimeOffset.UtcNow, options.CrashServer);
						string receipt = Path.Combine(options.RunDirectory, options.CrashPrefix + "-crash-armed.json");
						File.WriteAllText(receipt + ".tmp", JsonSerializer.Serialize(new
						{
							schemaVersion = 1, run = options.Run, armedUtc = DateTimeOffset.UtcNow,
							planSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))),
						}));
						File.Move(receipt + ".tmp", receipt); // The controller must await this exact-plan receipt before killing.
					});
				}
			}
			foreach (var (path, tail) in traceTails)
				foreach (var line in tail.ReadNewLines())
					ReadJsonLine("bot trace", line, () => ReadTrace(path, line));

			foreach (var line in botProblems.ReadNewLines())
				ReadJsonLine("bot problem", line, () => ReadBotProblem(line));

			foreach (var server in options.Servers)
			{
				foreach (var line in eventTails[server].ReadNewLines())
					ReadJsonLine($"{server} event", line, () => ReadServerEvent(line, server));
				foreach (var line in problemTails[server].ReadNewLines())
					ReadJsonLine($"{server} problem", line, () => ReadServerProblem(line, server));
			}
		}

		public void ReadDocker(System.Threading.Channels.ChannelReader<DockerLine> reader)
		{
			while (reader.TryRead(out var line))
			{
				if (line.Source == "event")
					ReadDockerEvent(line.Line, line.ProjectName);
				else
					ReadDockerLog(line);
			}
		}

		public void CheckHeartbeats(DateTimeOffset now)
		{
			CheckCrashCompletion(now, final: false);
			foreach (var server in options.Servers)
			{
				bool observed = lastHeartbeats.TryGetValue(server, out var lastSeen);
				// A snapshot may inspect partial historical artifacts. A continuing watcher
				// expects every server, including one whose heartbeat producer never started.
				if (!observed && options.Duration == TimeSpan.Zero) continue;
				if (!observed) lastSeen = watchingStarted;
				if (expectedCrash?.ExpectsHeartbeatGap(server, now) == true)
				{
					if (!crashGapReported)
						digest.WriteLine($"{FormatTimestamp(now)} EXPECTED_FAULT HEARTBEAT {options.CrashServer} bounded restart window.");
					crashGapReported = true;
					continue;
				}
				var threshold = observed ? options.MissingHeartbeatThreshold : options.InitialHeartbeatThreshold;
				if (now - lastSeen < threshold || !heartbeatAlerts.Add(server))
					continue;
				var message = observed ? $"Heartbeat missed for {Math.Floor((now - lastSeen).TotalSeconds):0} s." :
					$"Initial heartbeat not observed within {threshold.TotalSeconds:0} s of watcher startup.";
				AddSynthetic(now, server, "HEARTBEAT", observed ? "Heartbeat missed for {Server}" :
					"Initial heartbeat missing for {Server}", message, kind: "heartbeat");
				hangDiagnostics.Observe(new(server, now, observed ? lastSeen : null,
					lastHeartbeatRecords.GetValueOrDefault(server), options.InitialHeartbeatThreshold.TotalSeconds,
					options.MissingHeartbeatThreshold.TotalSeconds));
			}
		}

		public async Task WriteSummaryAsync(CancellationToken cancellationToken)
		{
			var diagnostics = await hangDiagnostics.CompleteAsync();
			// Collection may still be running when the owner requests stop. Preserve
			// problems written during that bounded drain before finalizing evidence.
			DiscoverTraceFiles();
			ReadFiles();
			CheckCrashCompletion(DateTimeOffset.UtcNow, final: true);
			foreach (var diagnostic in diagnostics)
				digest.WriteLine($"{FormatTimestamp(DateTimeOffset.UtcNow)} HANG_DIAGNOSTICS {diagnostic.Server} " +
					$"status={diagnostic.Status} path={diagnostic.Directory} failure={OneLine(diagnostic.Failure ?? "none")}");
			digest.Dispose();
			var provenance = RunProvenance.Load(options.RunDirectory);
			ledger.RecordRun(problemCounts, provenance, options.Run);
			if (options.FullRun && FailingProblemCount == 0)
			{
				await ledger.MarkFixedAfterGreenFullRunAsync(
					problemCounts.Keys.ToHashSet(StringComparer.Ordinal),
					provenance.GitSha,
					cancellationToken);
			}
			ledger.Save();
			var bundleWriter = new ProblemBundleWriter(options.RunDirectory, options.Run, provenance);
			foreach (var sample in newProblemSamples.Values)
				bundleWriter.Write(JoinStep(sample), traceHistory.ContextBefore(sample.Account, sample.Timestamp));
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
				expectedProcessEvents,
				heartbeatTimeoutSeconds = options.MissingHeartbeatThreshold.TotalSeconds,
				initialHeartbeatTimeoutSeconds = options.InitialHeartbeatThreshold.TotalSeconds,
				servers = options.Servers,
				hangDiagnostics = diagnostics.Select(d => new { server = d.Server, status = d.Status, directory = d.Directory, failure = d.Failure }),
				expectedGameServerCrash = options.ExpectGameServerCrash ? expectedCrash?.Complete == true : (bool?)null,
				expectedChatServerCrash = options.ExpectChatServerCrash ? expectedCrash?.Complete == true : (bool?)null,
				failed = options.Mode == WatchMode.Enforce && FailingProblemCount > 0,
				retainedTraceRecords = traceHistory.RetainedRecords,
				retainedTraceCharacters = traceHistory.RetainedCharacters,
			};
			await using var output = File.Create(Path.Combine(options.RunDirectory, "logwatch-summary.json"));
			await JsonSerializer.SerializeAsync(output, summary,
				new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
		}

		private void CheckCrashCompletion(DateTimeOffset now, bool final)
		{
			if (!options.ExpectsServerCrash || crashFailureReported) return;
			string? failure = expectedCrash?.Failure(now, final);
			if (final && expectedCrash == null) failure = $"No valid {options.CrashPrefix} crash plan was armed.";
			if (failure == null) return;
			crashFailureReported = true;
			AddSynthetic(now, "watcher", "ERROR", $"Planned {options.CrashPrefix} crash was not completed", failure, kind: "fault-expectation");
		}

		private void ReadTrace(string path, string line)
		{
			using var document = JsonDocument.Parse(line);
			var root = document.RootElement;
			if (!BelongsToRun(root))
				return;
			var step = BotStep.FromJson(root, line);
			traceHistory.Observe(path, step);

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

		private void ReadServerEvent(string line, string server)
		{
			using var document = JsonDocument.Parse(line);
			var root = document.RootElement;
			if (!BelongsToRun(root))
				return;
			var template = WatchProblem.RequiredString(root, "tpl");
			// Timer census and diagnostics share the producer's category. Only the
			// heartbeat event itself establishes liveness or supplies its metrics.
			if (template != "Server heartbeat" && !template.StartsWith("Server heartbeat:", StringComparison.Ordinal))
				return;
			ValidateProducer(root, server);
			var timestamp = WatchProblem.ReadTimestamp(root);
			if (lastHeartbeats.TryGetValue(server, out var previous) && timestamp <= previous) return;
			lastHeartbeats[server] = timestamp;
			lastHeartbeatRecords[server] = line.Length <= 16384 ? line : line[..16384] + " [truncated]";
			expectedCrash?.ObserveHeartbeat(server, timestamp);
			// File catch-up can deliver a newer but still stale sample. It is not recovery.
			if (DateTimeOffset.UtcNow - timestamp < options.MissingHeartbeatThreshold)
				heartbeatAlerts.Remove(server);
		}

		private void ReadServerProblem(string line, string server)
		{
			using var document = JsonDocument.Parse(line);
			if (BelongsToRun(document.RootElement))
			{
				ValidateProducer(document.RootElement, server);
				AddProblem(WatchProblem.FromServerJson(document.RootElement) with { Server = server });
			}
		}

		private static void ValidateProducer(JsonElement root, string server)
		{
			// Both GS instances use the unchanged production producer name. Their
			// separate mounted directories establish instance identity, never arrival order.
			string expected = server switch { "gs2" => "gs", "cs2" => "cs", _ => server };
			if (WatchProblem.RequiredString(root, "srv") != expected)
				throw new InvalidDataException($"Log producer does not match the {server} source directory.");
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

		private void ReadDockerEvent(string line, string? sourceProject)
		{
			if (string.IsNullOrWhiteSpace(line) || line[0] != '{')
				return;
			ReadJsonLine("Docker event", line, () =>
			{
				using var document = JsonDocument.Parse(line);
				var root = document.RootElement;
				var action = OptionalProperty(root, "action") ?? OptionalProperty(root, "Action") ??
					OptionalProperty(root, "status") ?? OptionalProperty(root, "Status");
				if (action is not ("die" or "oom" or "restart" or "start"))
					return;
				var service = OptionalProperty(root, "service") ?? ReadEventAttribute(root, "com.docker.compose.service") ?? "docker";
				var exitCode = ReadEventAttribute(root, "exitCode") ?? ReadEventAttribute(root, "exitcode");
				var container = OptionalProperty(root, "id") ?? OptionalProperty(root, "ID");
				if (container == null && root.TryGetProperty("Actor", out var actor)) container = OptionalProperty(actor, "ID");
				var project = ReadEventAttribute(root, "com.docker.compose.project") ?? sourceProject;
				var timestamp = TryReadDockerEventTimestamp(root);
				if (timestamp is { } at && expectedCrash?.ObserveDocker(project, container, service, action, exitCode, at) == true)
				{
					expectedProcessEvents++;
					digest.WriteLine($"{FormatTimestamp(at)} EXPECTED_FAULT PROCESS {options.CrashServer} action={action} container={container}.");
					return;
				}
				if (action == "start") return;
				var message = $"Container {service} {action}" + (exitCode == null ? "." : $" code={exitCode}.");
				AddSynthetic(timestamp ?? DateTimeOffset.UtcNow, MapService(service), "PROCESS",
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
					WriteProblemLine(problem, "ALLOWLISTED", allowlistEntry.Tracking, fixedIn: null);
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

			ledger.TryGet(problem.Fingerprint, out var ledgerEntry);
			var disposition = ledgerEntry?.Status switch
			{
				"tracked" => "KNOWN",
				"fixed" => "REGRESSED",
				_ => "NEW",
			};
			switch (disposition)
			{
				case "KNOWN":
					knownProblems++;
					if (problem.Kind == "heartbeat") knownHeartbeatProblems++;
					break;
				case "REGRESSED": regressedProblems++; break;
				default:
					newProblems++;
					newProblemSamples.TryAdd(problem.Fingerprint, problem);
					break;
			}

			WriteProblemLine(problem, disposition, ledgerEntry?.Tracking, ledgerEntry?.FixedIn);
		}

		private void WriteProblemLine(WatchProblem problem, string disposition, string? tracking, string? fixedIn)
		{
			var details = new StringBuilder();
			if (!string.IsNullOrEmpty(tracking))
				details.Append(" tracking=").Append(tracking);
			if (!string.IsNullOrEmpty(fixedIn))
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
			if (problem.Bot != null || problem.Account == null)
				return problem;
			var step = traceHistory.LatestBefore(problem.Account, problem.Timestamp);
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

		private static (DateTimeOffset Timestamp, string Text) SplitDockerTimestamp(string line)
		{
			var separator = line.IndexOf(' ');
			if (separator > 0 && DateTimeOffset.TryParse(line[..separator], CultureInfo.InvariantCulture,
				DateTimeStyles.AssumeUniversal, out var timestamp))
				return (timestamp, line[(separator + 1)..]);
			return (DateTimeOffset.UtcNow, line);
		}

		private static DateTimeOffset? TryReadDockerEventTimestamp(JsonElement root)
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
			return null;
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
			"gameserver2" => "gs2",
			"chatserver2" => "cs2",
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
