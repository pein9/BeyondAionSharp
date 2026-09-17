using System.Text;
using Aion.Bots.Protocol;
using Aion.Commons.Logging;
using Aion.GameServer.Commons.Network.Packet;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.TestKit;
using Microsoft.Extensions.Logging;

namespace Aion.Simulation.Tests;

public sealed class SimulationLogPolicy : IDisposable
{
	private const int RecentPacketLimit = 12;
	private readonly string run;
	private readonly string scenario;
	private readonly VirtualThreadPool clock;
	private readonly SimulationLogPolicyOptions options;
	private readonly LogProblemAllowlist allowlist;
	private readonly CapturingLoggerProvider capture;
	private readonly ILoggerFactory factory;
	private readonly bool ownsLoggerResources;
	private readonly IDisposable factoryOverride;
	private readonly IDisposable? scenarioScope;
	private readonly int firstCapturedEntry;
	private readonly int firstFault;
	private readonly List<SimulationProblem> syntheticProblems = [];
	private readonly Dictionary<string, Queue<string>> recentPackets = new(StringComparer.Ordinal);
	private int completed;

	public SimulationLogPolicy(
		string run,
		string scenario,
		VirtualThreadPool clock,
		string allowlistPath,
		SimulationLogPolicyOptions? options = null,
		CapturingLoggerProvider? captureProvider = null,
		ILoggerFactory? loggerFactory = null,
		bool includeHistory = false)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(run);
		ArgumentException.ThrowIfNullOrWhiteSpace(scenario);
		this.run = run;
		this.scenario = scenario;
		this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
		this.options = options ?? new SimulationLogPolicyOptions();
		if ((loggerFactory == null) != (captureProvider == null))
			throw new ArgumentException("An external logger factory and capturing provider must be supplied together.");
		BaseClientPacket<AionConnection>.ResetPartiallyReadPacketWarnings();
		allowlist = LogProblemAllowlist.Load(allowlistPath);
		capture = captureProvider ?? new CapturingLoggerProvider();
		ownsLoggerResources = loggerFactory == null;
		factory = loggerFactory ?? LoggerFactory.Create(builder =>
		{
			builder.ClearProviders();
			builder.SetMinimumLevel(LogLevel.Trace);
			builder.AddProvider(capture);
		});
		firstCapturedEntry = includeHistory ? 0 : capture.Entries.Count;
		factoryOverride = AionLog.OverrideFactory(factory);
		scenarioScope = factory.CreateLogger("SIM_SCENARIO").BeginScope(new Dictionary<string, object?>
		{
			["run"] = run,
			["scenario"] = scenario,
			["bot"] = string.Empty,
			["step"] = string.Empty,
		});
		firstFault = clock.Faults.Count;
	}

	public IDisposable? BeginBotStep(string bot, string step)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(bot);
		ArgumentException.ThrowIfNullOrWhiteSpace(step);
		return factory.CreateLogger("SIM_SCENARIO").BeginScope(new Dictionary<string, object?>
		{
			["bot"] = bot,
			["step"] = step,
		});
	}

	public void ObservePacket(string bot, string step, DecodedBotServerPacket packet)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(bot);
		ArgumentException.ThrowIfNullOrWhiteSpace(step);
		ArgumentNullException.ThrowIfNull(packet);
		if (!recentPackets.TryGetValue(bot, out Queue<string>? packets))
		{
			packets = new Queue<string>();
			recentPackets.Add(bot, packets);
		}
		packets.Enqueue($"{step} {packet.PacketType.Name} {FormatFields(packet.Fields)}");
		while (packets.Count > RecentPacketLimit)
			packets.Dequeue();

		if (packet.PacketType != typeof(SM_SYSTEM_MESSAGE) ||
			!packet.Fields.TryGetValue("name", out object? nameValue) ||
			nameValue is not string name ||
			!options.UnexpectedRefusalSystemMessages.Contains(name))
			return;

		string template = $"Unexpected refusal system message {name}";
		LogFingerprintResult fingerprint = LogFingerprint.Create(template, null, template);
		syntheticProblems.Add(new SimulationProblem(
			fingerprint.Value,
			"unexpected-refusal",
			"bots",
			LogLevel.Error,
			template,
			null,
			bot,
			step));
	}

	public void AssertClean()
	{
		if (Interlocked.Exchange(ref completed, 1) != 0)
			throw new InvalidOperationException("Simulation log policy was already completed.");
		var problems = CollectProblems();
		var counts = new Dictionary<string, int>(StringComparer.Ordinal);
		var unallowlisted = new List<SimulationProblem>();
		foreach (SimulationProblem problem in problems)
		{
			LogProblemAllowlistEntry? entry = allowlist.Entries.FirstOrDefault(candidate =>
				candidate.Fingerprint == problem.Fingerprint &&
				candidate.Modes.Contains("SIM", StringComparer.OrdinalIgnoreCase) &&
				candidate.Servers.Contains(problem.Server, StringComparer.OrdinalIgnoreCase));
			int count = counts.GetValueOrDefault(problem.Fingerprint) + 1;
			counts[problem.Fingerprint] = count;
			if (entry == null || count > entry.MaxCount)
				unallowlisted.Add(problem);
		}
		if (unallowlisted.Count != 0)
			throw new SimulationLogPolicyException(FormatFailure(unallowlisted), unallowlisted);
	}

	public void Dispose()
	{
		scenarioScope?.Dispose();
		factoryOverride.Dispose();
		if (ownsLoggerResources)
		{
			factory.Dispose();
			capture.Dispose();
		}
	}

	private IReadOnlyList<SimulationProblem> CollectProblems()
	{
		var problems = new List<SimulationProblem>(syntheticProblems);
		VirtualThreadPoolFault[] faults = clock.Faults.Skip(firstFault).ToArray();
		foreach (CapturedLogEntry entry in capture.Entries.Skip(firstCapturedEntry))
		{
			if (entry.Exception != null && faults.Any(fault => ReferenceEquals(fault.Exception, entry.Exception)))
				continue; // Report scheduler failures once, below, with their virtual due time.
			bool selected = entry.Level >= LogLevel.Error ||
				(options.FailOnWarnings && entry.Level == LogLevel.Warning) ||
				(options.FailOnAuditLog && entry.Category == "AUDIT_LOG");
			if (!selected)
				continue;
			problems.Add(new SimulationProblem(
				entry.Fingerprint.Value,
				entry.Category == "AUDIT_LOG" ? "audit" : "log",
				"gs",
				entry.Level,
				entry.Message,
				entry.Exception?.ToString(),
				entry.Scopes.GetValueOrDefault("bot"),
				entry.Scopes.GetValueOrDefault("step")));
		}

		foreach (VirtualThreadPoolFault fault in faults)
		{
			string template = $"Virtual {fault.Kind} timer failed";
			LogFingerprintResult fingerprint = LogFingerprint.Create(template, fault.Exception, template);
			problems.Add(new SimulationProblem(
				fingerprint.Value,
				"virtual-timer",
				"gs",
				LogLevel.Error,
				$"{template} at {fault.DueMillis}ms",
				fault.Exception.ToString(),
				null,
				null));
		}
		return problems;
	}

	private string FormatFailure(IReadOnlyList<SimulationProblem> problems)
	{
		var output = new StringBuilder();
		output.Append("SIM scenario ").Append(run).Append('/').Append(scenario)
			.Append(" recorded ").Append(problems.Count).AppendLine(" unallowlisted problem(s):");
		foreach (SimulationProblem problem in problems)
		{
			output.Append("- fp=").Append(problem.Fingerprint).Append(' ')
				.Append(problem.Level).Append(' ').Append(problem.Kind);
			if (!string.IsNullOrEmpty(problem.Bot))
				output.Append(" bot=").Append(problem.Bot).Append(" step=").Append(problem.Step);
			output.Append(" | ").AppendLine(problem.Message);
			if (problem.ExceptionText != null)
				output.AppendLine(problem.ExceptionText);
		}
		foreach (var (bot, packets) in recentPackets.OrderBy(entry => entry.Key, StringComparer.Ordinal))
		{
			output.Append("Last packets for ").Append(bot).AppendLine(":");
			foreach (string packet in packets)
				output.Append("- ").AppendLine(packet);
		}
		return output.ToString();
	}

	private static string FormatFields(IReadOnlyDictionary<string, object?> fields) => string.Join(
		", ",
		fields.OrderBy(field => field.Key, StringComparer.Ordinal).Select(field => $"{field.Key}={field.Value}"));
}

public sealed record SimulationLogPolicyOptions
{
	public bool FailOnWarnings { get; init; }
	public bool FailOnAuditLog { get; init; }
	public IReadOnlySet<string> UnexpectedRefusalSystemMessages { get; init; } = new HashSet<string>(StringComparer.Ordinal);
}

public sealed record SimulationProblem(
	string Fingerprint,
	string Kind,
	string Server,
	LogLevel Level,
	string Message,
	string? ExceptionText,
	string? Bot,
	string? Step);

public sealed class SimulationLogPolicyException(
	string message,
	IReadOnlyList<SimulationProblem> problems) : Exception(message)
{
	public IReadOnlyList<SimulationProblem> Problems { get; } = problems;
}
