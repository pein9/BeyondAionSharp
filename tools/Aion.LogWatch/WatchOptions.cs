using System.Globalization;

namespace Aion.LogWatch;

public enum WatchMode
{
	Enforce,
	Record,
}

public sealed record WatchOptions(
	string Run,
	string RunDirectory,
	string ProjectName,
	string ComposeFile,
	string AllowlistPath,
	string LedgerPath,
	WatchMode Mode,
	TimeSpan? Duration,
	string? StopFile,
	bool DockerEnabled,
	bool FullRun,
	IReadOnlySet<string> UnexpectedRefusals)
{
	public bool ExpectGameServerCrash { get; init; }
	public bool ExpectChatServerCrash { get; init; }
	public bool ExpectLoginServerCrash { get; init; }
	internal bool ExpectsServerCrash => ExpectGameServerCrash || ExpectChatServerCrash || ExpectLoginServerCrash;
	internal string CrashServer => ExpectLoginServerCrash ? "ls" : ExpectChatServerCrash ? "cs" : "gs";
	internal string CrashPrefix => ExpectLoginServerCrash ? "login-server" : ExpectChatServerCrash ? "chat-server" : "game-server";
	public bool SecondGameServer { get; init; }
	internal IReadOnlyList<string> Servers => SecondGameServer ? ["gs", "gs2", "ls", "cs", "cs2"] : ["gs", "ls", "cs"];
	internal IReadOnlyList<string> DockerServices => SecondGameServer
		? ["loginserver", "chatserver", "chatserver2", "gameserver", "gameserver2", "mysql"]
		: ["loginserver", "chatserver", "gameserver", "mysql"];
	public TimeSpan MissingHeartbeatThreshold { get; init; } = TimeSpan.FromSeconds(20);
	public TimeSpan InitialHeartbeatThreshold { get; init; } = TimeSpan.FromSeconds(30);

	internal void ValidateHeartbeatThresholds()
	{
		if ((ExpectGameServerCrash ? 1 : 0) + (ExpectChatServerCrash ? 1 : 0) + (ExpectLoginServerCrash ? 1 : 0) > 1)
			throw new ArgumentException("Only one explicitly selected server crash may be expected in a run.");
		if (MissingHeartbeatThreshold < TimeSpan.FromSeconds(20) || MissingHeartbeatThreshold > TimeSpan.FromSeconds(300))
			throw new ArgumentException("--heartbeat-timeout-seconds must be between 20 and 300 (producer interval: 10 seconds).");
		if (InitialHeartbeatThreshold < TimeSpan.FromSeconds(20) || InitialHeartbeatThreshold > TimeSpan.FromSeconds(600))
			throw new ArgumentException("--initial-heartbeat-timeout-seconds must be between 20 and 600.");
	}
	public const string Usage = "Usage: dotnet run --project tools/Aion.LogWatch -- --run <id> --run-dir <path> " +
		"[--project aion-bots-<id>] [--compose-file docker/docker-compose.bots.yml] " +
		"[--allowlist parity-artifacts/e2e/log-allowlist.json] [--ledger parity-artifacts/e2e/known-problems.json] " +
		"[--mode enforce|record] [--duration-seconds N] [--stop-file path] [--no-docker true|false] " +
		"[--full-run true|false] [--unexpected-refusals STR_SKILL_NOT_READY,...] [--expect-game-server-crash true|false] " +
		"[--heartbeat-timeout-seconds 20] [--initial-heartbeat-timeout-seconds 30] [--second-game-server true|false] " +
		"[--expect-chat-server-crash true|false] [--expect-login-server-crash true|false]";

	public static WatchOptions Parse(string[] args)
	{
		var values = new Dictionary<string, string>(StringComparer.Ordinal);
		for (var index = 0; index < args.Length; index += 2)
		{
			if (!args[index].StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
				throw new ArgumentException($"Every option needs a value; stopped at '{args[index]}'.");
			if (!values.TryAdd(args[index][2..], args[index + 1]))
				throw new ArgumentException($"Option '{args[index]}' was supplied more than once.");
		}

		var known = new HashSet<string>(StringComparer.Ordinal)
		{
			"run", "run-dir", "project", "compose-file", "allowlist", "ledger", "mode",
			"duration-seconds", "stop-file", "no-docker", "full-run", "unexpected-refusals", "expect-game-server-crash",
			"heartbeat-timeout-seconds", "initial-heartbeat-timeout-seconds",
			"second-game-server", "expect-chat-server-crash", "expect-login-server-crash",
		};
		var unknown = values.Keys.FirstOrDefault(key => !known.Contains(key));
		if (unknown != null)
			throw new ArgumentException($"Unknown option '--{unknown}'.");

		var run = Required(values, "run");
		var runDirectory = Path.GetFullPath(Required(values, "run-dir"));
		if (!Directory.Exists(runDirectory))
			throw new ArgumentException($"Run directory does not exist: {runDirectory}");
		var project = Get(values, "project", $"aion-bots-{run}");
		if (!project.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
			throw new ArgumentException("--project may contain only ASCII letters, digits, '-' and '_'.");

		var mode = Get(values, "mode", "enforce") switch
		{
			"enforce" => WatchMode.Enforce,
			"record" => WatchMode.Record,
			var value => throw new ArgumentException($"--mode must be enforce or record, got '{value}'."),
		};
		var durationSeconds = Integer(values, "duration-seconds", -1);
		if (durationSeconds < -1)
			throw new ArgumentException("--duration-seconds must be -1 (until stopped), 0 (snapshot), or positive.");
		var dockerEnabled = !Boolean(values, "no-docker", false);
		var refusals = Get(values, "unexpected-refusals", "STR_SKILL_NOT_READY")
			.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
			.ToHashSet(StringComparer.Ordinal);

		var options = new WatchOptions(
			run,
			runDirectory,
			project,
			Path.GetFullPath(Get(values, "compose-file", "docker/docker-compose.bots.yml")),
			Path.GetFullPath(Get(values, "allowlist", "parity-artifacts/e2e/log-allowlist.json")),
			Path.GetFullPath(Get(values, "ledger", "parity-artifacts/e2e/known-problems.json")),
			mode,
			durationSeconds < 0 ? null : TimeSpan.FromSeconds(durationSeconds),
			values.TryGetValue("stop-file", out var stopFile) ? Path.GetFullPath(stopFile) : null,
			dockerEnabled,
			Boolean(values, "full-run", false),
			refusals)
		{
			ExpectGameServerCrash = Boolean(values, "expect-game-server-crash", false),
			ExpectChatServerCrash = Boolean(values, "expect-chat-server-crash", false),
			ExpectLoginServerCrash = Boolean(values, "expect-login-server-crash", false),
			SecondGameServer = Boolean(values, "second-game-server", false),
			MissingHeartbeatThreshold = TimeSpan.FromSeconds(Integer(values, "heartbeat-timeout-seconds", 20)),
			InitialHeartbeatThreshold = TimeSpan.FromSeconds(Integer(values, "initial-heartbeat-timeout-seconds", 30)),
		};
		options.ValidateHeartbeatThresholds();
		return options;
	}

	private static string Required(IReadOnlyDictionary<string, string> values, string name) =>
		values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
			? value
			: throw new ArgumentException($"Missing required option '--{name}'.");

	private static string Get(IReadOnlyDictionary<string, string> values, string name, string fallback) =>
		values.TryGetValue(name, out var value) ? value : fallback;

	private static int Integer(IReadOnlyDictionary<string, string> values, string name, int fallback)
	{
		if (!values.TryGetValue(name, out var raw))
			return fallback;
		return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
			? value
			: throw new ArgumentException($"--{name} must be an integer, got '{raw}'.");
	}

	private static bool Boolean(IReadOnlyDictionary<string, string> values, string name, bool fallback)
	{
		if (!values.TryGetValue(name, out var raw))
			return fallback;
		return bool.TryParse(raw, out var value)
			? value
			: throw new ArgumentException($"--{name} must be true or false, got '{raw}'.");
	}
}
