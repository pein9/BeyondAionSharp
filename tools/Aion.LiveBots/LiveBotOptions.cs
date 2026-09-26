using Aion.Bots.Dashboard;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using Aion.Bots.Scenarios;
using Aion.Bots.Transport;

namespace Aion.LiveBots;

public sealed record LiveBotOptions(
	string Run,
	string OutputDirectory,
	IPEndPoint LoginEndPoint,
	IPEndPoint GameEndPoint,
	IPEndPoint ChatEndPoint,
	Uri AdminBaseUri,
	string AdminToken,
	int BotCount,
	IReadOnlyList<string> Scenarios,
	IReadOnlyList<ScenarioDefinition> ScenarioDefinitions,
	TimeSpan ConnectTimeout,
	TimeSpan StepTimeout,
	int Seed,
	string GitSha,
	string Profile,
	string TimeZone,
	TimeSpan ReentryDelay)
{
	public int SoakSeconds { get; init; } = 7200;
	public IReadOnlyList<SoakActivity> SoakActivities { get; init; } = Enum.GetValues<SoakActivity>();
	public int DashboardPort { get; init; }
	public int DecisionViewSeconds { get; init; } = 30;
	internal LiveBotDashboardState Dashboard { get; } = new();
	public const string Usage = "Usage: dotnet run --project tools/Aion.LiveBots -- --run <id> --output <run-dir> " +
		"[--scenario manifest-id[,manifest-id]] [--bots N] [--host 127.0.0.1] [--login-port 12106] " +
		"[--login-host IP] [--chat-host IP] [--game-port 17777] [--chat-port 11241] [--admin-port 17780] [--admin-token TOKEN] " +
		"[--connect-timeout-seconds 10] [--step-timeout-seconds 15] [--seed N] [--git-sha SHA] " +
		"[--profile deterministic] [--time-zone ID] [--reentry-seconds 10] [--dashboard-port 0] [--decision-view-seconds 30] " +
		"[--soak-seconds 7200] [--soak-activities Quest,Gather,Craft,Vendor,Trade,Group,Duel,Pvp,Relog,CrashDisconnect]";

	public static LiveBotOptions Parse(string[] args)
	{
		var values = new Dictionary<string, string>(StringComparer.Ordinal);
		for (var i = 0; i < args.Length; i += 2)
		{
			if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
				throw new ArgumentException($"Every option needs a value; stopped at '{args[i]}'.");
			if (!values.TryAdd(args[i][2..], args[i + 1]))
				throw new ArgumentException($"Option '{args[i]}' was supplied more than once.");
		}

		var run = Required(values, "run");
		if (!run.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
			throw new ArgumentException("--run may contain only ASCII letters, digits, '-' and '_'.");
		var output = Path.GetFullPath(Required(values, "output"));
		var host = IPAddress.Parse(Get(values, "host", "127.0.0.1"));
		var loginHost = IPAddress.Parse(Get(values, "login-host", host.ToString()));
		var chatHost = IPAddress.Parse(Get(values, "chat-host", host.ToString()));
		var loginPort = PositiveInt(values, "login-port", EnvironmentPort("AION_BOT_LOGIN_PORT", 12106), 65535);
		var gamePort = PositiveInt(values, "game-port", EnvironmentPort("AION_BOT_GAME_PORT", 17777), 65535);
		var chatPort = PositiveInt(values, "chat-port", EnvironmentPort("AION_BOT_CHAT_PORT", 11241), 65535);
		var adminPort = PositiveInt(values, "admin-port", EnvironmentPort("AION_BOT_ADMIN_PORT", 17780), 65535);
		var bots = PositiveInt(values, "bots", 1, BotIdentity.MaximumSubjects);
		var connectSeconds = PositiveInt(values, "connect-timeout-seconds", 10, 3600);
		var stepSeconds = PositiveInt(values, "step-timeout-seconds", 15, 3600);
		var seed = Int(values, "seed", 1);
		var scenarios = Get(values, "scenario", "connect")
			.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
		if (scenarios.Length == 0 || scenarios.Distinct(StringComparer.Ordinal).Count() != scenarios.Length)
			throw new ArgumentException("--scenario must contain one or more distinct manifest ids.");
		ScenarioManifest manifest = ScenarioManifest.Load(ScenarioManifest.FindDefaultPath());
		ScenarioDefinition[] scenarioDefinitions;
		try
		{
			scenarioDefinitions = scenarios.Select(manifest.Get).ToArray();
		}
		catch (KeyNotFoundException exception)
		{
			throw new ArgumentException(exception.Message, "scenario", exception);
		}
		ScenarioDefinition? nonLive = scenarioDefinitions.FirstOrDefault(definition => !definition.Modes.Contains(ScenarioMode.Live));
		if (nonLive != null)
			throw new ArgumentException($"Scenario '{nonLive.Id}' does not support LIVE mode.", "scenario");
		if (scenarios.Any(scenario => scenario is "L0" or "canaries") && scenarios.Length != 1)
			throw new ArgumentException("L0 and canaries are coordinated scenarios and must be run by themselves.");
		bots = Math.Max(bots, scenarioDefinitions.Max(definition => definition.Bots));
		if (bots > BotIdentity.MaximumSubjects)
			throw new ArgumentException($"Scenario requires more than {BotIdentity.MaximumSubjects} subject bots.", "scenario");
		if (scenarios.Contains("O1", StringComparer.Ordinal) && (scenarios.Length != 1 || bots != 1 || stepSeconds < 1050))
			throw new ArgumentException("O1 must run alone with one subject and at least 1050 seconds per step.");
		if (scenarios.Any(scenario => scenario is "NI-01" or "NI-02" or "NI-03" or "NI-04" or "NI-05" or "NI-06" or "NI-09") && (scenarios.Length != 1 || bots != 1))
			throw new ArgumentException("Natural Ishalgen scenarios must run alone with exactly one retained subject.");
		if (scenarios.Contains("B2", StringComparer.Ordinal) && (scenarios.Length != 1 || bots != 2))
			throw new ArgumentException("B2 must run alone with exactly two subjects (plus its director).");
		if (scenarios.Contains("B2F", StringComparer.Ordinal) && (scenarios.Length != 1 || bots != 2 || stepSeconds < 180))
			throw new ArgumentException("B2F must run alone with two subjects and at least 180 seconds per step.");
		if (scenarios.Contains("B3", StringComparer.Ordinal) && (scenarios.Length != 1 || bots != 1 || stepSeconds < 120))
			throw new ArgumentException("B3 must run alone with one subject, its director and at least 120 seconds per step.");
		var reentrySeconds = PositiveInt(values, "reentry-seconds", 10, 3600);
		var dashboardPort = NonNegativeInt(values, "dashboard-port", 0, 65535);
		var decisionViewSeconds = NonNegativeInt(values, "decision-view-seconds", 30, 600);
		if (values.ContainsKey("decision-view-seconds") && !scenarios.Any(scenario => scenario is "NI-02" or "NI-03" or "NI-04" or "NI-05" or "NI-06"))
			throw new ArgumentException("--decision-view-seconds requires a Natural Ishalgen gameplay scenario.");
		if (scenarios.Contains("B4", StringComparer.Ordinal) && (scenarios.Length != 1 || bots != 5 || stepSeconds < 180))
			throw new ArgumentException("B4 must run alone with five subjects and at least 180 seconds per step.");
		int soakSeconds = PositiveInt(values, "soak-seconds", 7200, 7200);
		SoakActivity[] soakActivities = Enum.GetValues<SoakActivity>();
		if (values.TryGetValue("soak-activities", out string? selection))
		{
			string[] names = selection.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
			if (names.Length == 0 || names.Any(name => !Enum.GetNames<SoakActivity>().Contains(name, StringComparer.Ordinal)))
				throw new ArgumentException("--soak-activities requires named SoakActivity values.");
			soakActivities = names.Select(Enum.Parse<SoakActivity>).ToArray();
			if (soakActivities.Distinct().Count() != soakActivities.Length) throw new ArgumentException("Duplicate soak activity.");
		}
		if (scenarios.Contains("SOAK", StringComparer.Ordinal))
		{
			if (scenarios.Length != 1) throw new ArgumentException("SOAK must run alone.");
			_ = SoakLifePolicy.CreatePopulation(bots, manifest);
		}
		else if (values.ContainsKey("soak-seconds") || values.ContainsKey("soak-activities"))
			throw new ArgumentException("Soak options require --scenario SOAK.");

		var known = new HashSet<string>(StringComparer.Ordinal)
		{
			"run", "output", "host", "login-host", "chat-host", "login-port", "game-port", "chat-port", "admin-port", "admin-token",
			"bots", "scenario", "connect-timeout-seconds", "step-timeout-seconds", "reentry-seconds",
			"seed", "git-sha", "profile", "time-zone", "dashboard-port", "decision-view-seconds",
			"soak-seconds", "soak-activities",
		};
		var unknown = values.Keys.FirstOrDefault(key => !known.Contains(key));
		if (unknown != null)
			throw new ArgumentException($"Unknown option '--{unknown}'.");

		return new LiveBotOptions(
			run,
			output,
			new IPEndPoint(loginHost, loginPort),
			new IPEndPoint(host, gamePort),
			new IPEndPoint(chatHost, chatPort),
			new Uri($"http://{(host.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? $"[{host}]" : host.ToString())}:{adminPort}/"),
			Get(values, "admin-token", Environment.GetEnvironmentVariable("AION_BOT_ADMIN_TOKEN") ?? "aion-bots-local-token"),
			bots,
			scenarios,
			scenarioDefinitions,
			TimeSpan.FromSeconds(connectSeconds),
			TimeSpan.FromSeconds(stepSeconds),
			seed,
			Get(values, "git-sha", ResolveGitSha()),
			Get(values, "profile", "deterministic"),
			Get(values, "time-zone", TimeZoneInfo.Local.Id),
			TimeSpan.FromSeconds(reentrySeconds))
		{
			SoakSeconds = soakSeconds,
			SoakActivities = soakActivities,
			DashboardPort = dashboardPort,
			DecisionViewSeconds = decisionViewSeconds,
		};
	}

	private static string Required(IReadOnlyDictionary<string, string> values, string name) =>
		values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
			? value
			: throw new ArgumentException($"Missing required option '--{name}'.");

	private static string Get(IReadOnlyDictionary<string, string> values, string name, string fallback) =>
		values.TryGetValue(name, out var value) ? value : fallback;

	private static int PositiveInt(IReadOnlyDictionary<string, string> values, string name, int fallback, int maximum)
	{
		var value = Int(values, name, fallback);
		if (value is < 1 || value > 65535 || value > maximum)
			throw new ArgumentException($"--{name} must be between 1 and {maximum}.");
		return value;
	}

	private static int NonNegativeInt(IReadOnlyDictionary<string, string> values, string name, int fallback, int maximum)
	{
		var value = Int(values, name, fallback);
		if (value < 0 || value > maximum)
			throw new ArgumentException($"--{name} must be between 0 and {maximum}.");
		return value;
	}

	private static int Int(IReadOnlyDictionary<string, string> values, string name, int fallback)
	{
		if (!values.TryGetValue(name, out var raw))
			return fallback;
		if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
			throw new ArgumentException($"--{name} must be an integer, got '{raw}'.");
		return value;
	}

	private static int EnvironmentPort(string name, int fallback)
	{
		var raw = Environment.GetEnvironmentVariable(name);
		if (string.IsNullOrWhiteSpace(raw))
			return fallback;
		if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value is < 1 or > 65535)
			throw new ArgumentException($"{name} must be a TCP port, got '{raw}'.");
		return value;
	}

	private static string ResolveGitSha()
	{
		try
		{
			using var process = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD")
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			});
			if (process == null)
				return "unknown";
			var output = process.StandardOutput.ReadToEnd().Trim();
			process.WaitForExit(2000);
			return process.ExitCode == 0 && output.Length > 0 ? output : "unknown";
		}
		catch
		{
			return "unknown";
		}
	}
}
