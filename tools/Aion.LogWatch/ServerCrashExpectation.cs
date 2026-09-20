using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aion.LogWatch;

/// <summary>One deliberately injected SIGKILL, never a general process-error allowance.</summary>
internal sealed class ServerCrashExpectation
{
	internal sealed record Plan
	{
		public required int SchemaVersion { get; init; }
		public required string Run { get; init; }
		public required string Project { get; init; }
		public required string ContainerId { get; init; }
		public required DateTimeOffset ArmedUtc { get; init; }
		public required DateTimeOffset KillDeadlineUtc { get; init; }
		public required DateTimeOffset RecoveryDeadlineUtc { get; init; }
	}

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
	};
	private readonly Plan plan;
	internal string Server { get; }
	private string Service => Server switch { "gs" => "gameserver", "cs" => "chatserver", "ls" => "loginserver", _ => throw new InvalidOperationException("Unknown crash target.") };
	private string Name => Server switch { "gs" => "game server", "cs" => "chat server", "ls" => "login server", _ => throw new InvalidOperationException("Unknown crash target.") };
	private DateTimeOffset? latestHeartbeat;
	internal DateTimeOffset? DiedUtc { get; private set; }
	internal DateTimeOffset? StartedUtc { get; private set; }
	internal DateTimeOffset? RecoveredUtc { get; private set; }
	internal bool Complete => RecoveredUtc != null;

	private ServerCrashExpectation(Plan plan, string server) { this.plan = plan; Server = server; }

	internal static ServerCrashExpectation Load(string json, string run, string project, DateTimeOffset now, string server = "gs")
	{
		if (server is not ("gs" or "cs" or "ls")) throw new ArgumentException("Only an explicitly selected first Game, Chat or Login server may be faulted.", nameof(server));
		var plan = JsonSerializer.Deserialize<Plan>(json, JsonOptions)
			?? throw new InvalidDataException("Crash plan must be an object.");
		if (plan.SchemaVersion != 1 || plan.Run != run || plan.Project != project ||
			project != "aion-bots-" + run || plan.ContainerId is not { Length: 64 } ||
			!plan.ContainerId.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'))
			throw new InvalidDataException("Crash plan must name this isolated run and an exact Docker container id.");
		if (plan.ArmedUtc > now.AddSeconds(1) || plan.ArmedUtc < now.AddSeconds(-5) ||
			plan.KillDeadlineUtc <= plan.ArmedUtc || plan.KillDeadlineUtc > plan.ArmedUtc.AddSeconds(30) ||
			plan.RecoveryDeadlineUtc <= plan.KillDeadlineUtc || plan.RecoveryDeadlineUtc > plan.ArmedUtc.AddSeconds(180))
			throw new InvalidDataException("Crash plan must be fresh, with at most 30 seconds to kill and 180 seconds to recover.");
		return new(plan, server);
	}

	internal bool ObserveDocker(string? project, string? container, string service, string action,
		string? exitCode, DateTimeOffset timestamp)
	{
		if (project != plan.Project || container != plan.ContainerId || service != Service) return false;
		if (action == "die" && exitCode == "137" && DiedUtc == null &&
			timestamp >= plan.ArmedUtc && timestamp <= plan.KillDeadlineUtc)
		{
			DiedUtc = timestamp;
			return true;
		}
		if (action == "start" && DiedUtc is { } died && StartedUtc == null &&
			timestamp >= died && timestamp <= plan.RecoveryDeadlineUtc)
		{
			StartedUtc = timestamp;
			TryRecover();
			return true;
		}
		return false;
	}

	internal void ObserveHeartbeat(string server, DateTimeOffset timestamp)
	{
		if (server != Server) return;
		if (latestHeartbeat == null || timestamp > latestHeartbeat) latestHeartbeat = timestamp;
		TryRecover();
	}

	private void TryRecover()
	{
		// Docker may truncate death/start to whole seconds. A heartbeat from the old
		// process later in that same second cannot prove recovery, regardless of the
		// order in which its log and the Docker stream reach the watcher.
		if (StartedUtc is { } started && latestHeartbeat is { } heartbeat &&
			heartbeat >= DateTimeOffset.FromUnixTimeSeconds(started.ToUnixTimeSeconds() + 1) && heartbeat <= plan.RecoveryDeadlineUtc)
			RecoveredUtc ??= heartbeat;
	}

	internal bool ExpectsHeartbeatGap(string server, DateTimeOffset now) =>
		server == Server && !Complete && DiedUtc is { } died && now >= died && now <= plan.RecoveryDeadlineUtc;

	internal string? Failure(DateTimeOffset now, bool final) =>
		DiedUtc == null && (final || now > plan.KillDeadlineUtc) ? $"The planned {Name} SIGKILL was not observed before its deadline." :
		!Complete && (final || now > plan.RecoveryDeadlineUtc) ? $"The killed {Name} did not restart with a fresh heartbeat before its deadline." : null;
}
