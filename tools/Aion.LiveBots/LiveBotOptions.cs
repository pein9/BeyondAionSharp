using System.Diagnostics;
using System.Globalization;
using System.Net;

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
	TimeSpan ConnectTimeout,
	TimeSpan StepTimeout,
	int Seed,
	string GitSha,
	string Profile,
	string TimeZone,
	TimeSpan ReentryDelay)
{
	public const string Usage = "Usage: dotnet run --project tools/Aion.LiveBots -- --run <id> --output <run-dir> " +
		"[--scenario connect|L0] [--bots N] [--host 127.0.0.1] [--login-port 12106] " +
		"[--game-port 17777] [--chat-port 11241] [--admin-port 17780] [--admin-token TOKEN] " +
		"[--connect-timeout-seconds 10] [--step-timeout-seconds 15] [--seed N] [--git-sha SHA] " +
		"[--profile deterministic] [--time-zone ID] [--reentry-seconds 10]";

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
		var loginPort = PositiveInt(values, "login-port", EnvironmentPort("AION_BOT_LOGIN_PORT", 12106), 65535);
		var gamePort = PositiveInt(values, "game-port", EnvironmentPort("AION_BOT_GAME_PORT", 17777), 65535);
		var chatPort = PositiveInt(values, "chat-port", EnvironmentPort("AION_BOT_CHAT_PORT", 11241), 65535);
		var adminPort = PositiveInt(values, "admin-port", EnvironmentPort("AION_BOT_ADMIN_PORT", 17780), 65535);
		var bots = PositiveInt(values, "bots", 1, 99);
		var connectSeconds = PositiveInt(values, "connect-timeout-seconds", 10, 3600);
		var stepSeconds = PositiveInt(values, "step-timeout-seconds", 15, 3600);
		var seed = Int(values, "seed", 1);
		var scenarios = Get(values, "scenario", "connect")
			.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
		if (scenarios.Length == 0 || scenarios.Any(scenario => scenario is not ("connect" or "L0")))
			throw new ArgumentException("Supported scenarios are 'connect' and 'L0'.");
		if (scenarios.Contains("L0", StringComparer.Ordinal) && scenarios.Length != 1)
			throw new ArgumentException("L0 is a coordinated two-bot scenario and must be run by itself.");
		if (scenarios.Contains("L0", StringComparer.Ordinal))
			bots = Math.Max(2, bots);
		var reentrySeconds = PositiveInt(values, "reentry-seconds", 10, 3600);

		var known = new HashSet<string>(StringComparer.Ordinal)
		{
			"run", "output", "host", "login-port", "game-port", "chat-port", "admin-port", "admin-token",
			"bots", "scenario", "connect-timeout-seconds", "step-timeout-seconds", "reentry-seconds",
			"seed", "git-sha", "profile", "time-zone",
		};
		var unknown = values.Keys.FirstOrDefault(key => !known.Contains(key));
		if (unknown != null)
			throw new ArgumentException($"Unknown option '--{unknown}'.");

		return new LiveBotOptions(
			run,
			output,
			new IPEndPoint(host, loginPort),
			new IPEndPoint(host, gamePort),
			new IPEndPoint(host, chatPort),
			new Uri($"http://{(host.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? $"[{host}]" : host.ToString())}:{adminPort}/"),
			Get(values, "admin-token", Environment.GetEnvironmentVariable("AION_BOT_ADMIN_TOKEN") ?? "aion-bots-local-token"),
			bots,
			scenarios,
			TimeSpan.FromSeconds(connectSeconds),
			TimeSpan.FromSeconds(stepSeconds),
			seed,
			Get(values, "git-sha", ResolveGitSha()),
			Get(values, "profile", "deterministic"),
			Get(values, "time-zone", TimeZoneInfo.Local.Id),
			TimeSpan.FromSeconds(reentrySeconds));
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
