using System.Text;
using System.Text.Json;

namespace Aion.LogWatch;

internal sealed record HangObservation(string Server, DateTimeOffset DetectedUtc, DateTimeOffset? LastHeartbeatUtc,
	string? LastHeartbeatRecord, double InitialThresholdSeconds, double GapThresholdSeconds);

internal sealed record HangDiagnosticResult(string Server, string Status, string? Directory, string? Failure);

/// <summary>One bounded collection per server/run, independent of the watcher polling loop.</summary>
internal sealed class HangDiagnostics(WatchOptions options, IDiagnosticCommand command)
{
	internal static readonly TimeSpan CollectionBudget = TimeSpan.FromSeconds(20);
	private readonly Dictionary<string, Task<HangDiagnosticResult>> pending = new(StringComparer.Ordinal);
	private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	internal void Observe(HangObservation observation)
	{
		if (!options.DockerEnabled || options.Duration == TimeSpan.Zero || pending.ContainsKey(observation.Server)) return;
		if (!options.Servers.Contains(observation.Server, StringComparer.Ordinal)) return;
		pending.Add(observation.Server, Task.Run(() => CollectAsync(observation)));
	}

	internal Task<HangDiagnosticResult[]> CompleteAsync() => Task.WhenAll(pending.Values);

	private async Task<HangDiagnosticResult> CollectAsync(HangObservation observation)
	{
		string server = observation.Server;
		string service = server switch { "gs" => "gameserver", "gs2" => "gameserver2", "ls" => "loginserver", _ => "chatserver" };
		string directory = Path.Combine(options.RunDirectory, "hangs", server);
		using var lifetime = new CancellationTokenSource(CollectionBudget);
		var steps = new Dictionary<string, DiagnosticCommandResult>(StringComparer.Ordinal);
		try
		{
			Directory.CreateDirectory(directory);
			await WriteNewAsync(Path.Combine(directory, "observation.json"), JsonSerializer.Serialize(observation, Json));
			if (options.ProjectName != "aion-bots-" + options.Run ||
				!options.ProjectName.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
				throw new InvalidDataException("Diagnostics require this run's exact isolated aion-bots project.");

			async Task<DiagnosticCommandResult> RunAsync(string name, string[] arguments, double seconds)
			{
				lifetime.Token.ThrowIfCancellationRequested();
				var result = await command.RunAsync(arguments, TimeSpan.FromSeconds(seconds), lifetime.Token).WaitAsync(lifetime.Token);
				steps.Add(name, result);
				return result;
			}
			var selected = await RunAsync("select", ["ps", "-aq", "--no-trunc", "--filter", $"label=com.docker.compose.project={options.ProjectName}",
				"--filter", $"label=com.docker.compose.service={service}"], 3);
			string[] ids = selected.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			if (!selected.Succeeded || ids.Length != 1 || !IsContainerId(ids[0]))
				throw new InvalidDataException("Diagnostic target is absent, ambiguous or invalid.");
			string id = ids[0];
			var inspection = await RunAsync("identity", ["inspect", "--format", InspectFormat, id], 3);
			var identity = ValidateIdentity(inspection, id, options.ProjectName, service);
			await RunAsync("threads", ["top", id, "-eo", "pid,tid,stat,pcpu,pmem,comm"], 3);
			await RunAsync("resources", ["stats", "--no-stream", "--format", "{{json .}}", id], 4);
			// Revalidate immediately before an in-container command. Never retarget a replacement.
			var recheck = await RunAsync("identity-before-stack", ["inspect", "--format", InspectFormat, id], 3);
			var current = ValidateIdentity(recheck, id, options.ProjectName, service);
			if (current.Image != identity.Image || current.StartedAt != identity.StartedAt)
				throw new InvalidDataException("Diagnostic target process/image changed during collection.");
			if (!current.Running || current.Paused || current.Restarting)
				steps.Add("managed-stacks", new(null, "", "", Failure: "Container is not an unpaused running process; managed stack probe not attempted."));
			else
			{
				string assembly = server switch { "gs" or "gs2" => "Aion.GameServer.dll", "ls" => "Aion.LoginServer.dll", _ => "Aion.ChatServer.dll" };
				// Only fixed arguments enter the shell. timeout kills the diagnostic tool, never PID 1.
				await RunAsync("managed-stacks", ["exec", id, "sh", "-c",
					"test \"$(tr '\\000' '\\n' < /proc/1/cmdline | head -n 2 | tail -n 1)\" = \"$1\" || exit 65; " +
					"test -x /opt/aion-diagnostics/dotnet-stack || exit 69; " +
					"exec timeout --signal=KILL 8s env DOTNET_ROLL_FORWARD=Major /opt/aion-diagnostics/dotnet-stack report --process-id 1",
					"aion-hang-probe", assembly], 10);
				var afterStack = await RunAsync("identity-after-stack", ["inspect", "--format", InspectFormat, id], 3);
				var finalIdentity = ValidateIdentity(afterStack, id, options.ProjectName, service);
				if (finalIdentity.Image != identity.Image || finalIdentity.StartedAt != identity.StartedAt)
					throw new InvalidDataException("Diagnostic target process/image changed during stack sampling; do not attribute the sample to the original process.");
			}
			bool complete = steps.Values.All(result => result.Succeeded);
			await WriteNewAsync(Path.Combine(directory, "collection.json"), JsonSerializer.Serialize(new
			{
				status = complete ? "collected" : "partial", containerId = id, steps,
				note = "Suspected hang only; stack sampling adds overhead and does not prove a lock cycle.",
			}, Json));
			return new(server, complete ? "collected" : "partial", directory, null);
		}
		catch (Exception exception) when (exception is not OutOfMemoryException)
		{
			string failure = $"{exception.GetType().Name}: {exception.Message}";
			try { await WriteNewAsync(Path.Combine(directory, "failure.json"), JsonSerializer.Serialize(new { failure, steps }, Json)); }
			catch (Exception writeFailure) when (writeFailure is IOException or UnauthorizedAccessException) { failure += $"; artifact write failed: {writeFailure.Message}"; }
			return new(server, "failed", directory, failure);
		}
	}

	// Select fields explicitly: a complete Docker inspect includes passwords in Config.Env.
	internal const string InspectFormat = "{\"id\":{{json .Id}},\"image\":{{json .Image}},\"running\":{{json .State.Running}}," +
		"\"paused\":{{json .State.Paused}},\"restarting\":{{json .State.Restarting}},\"startedAt\":{{json .State.StartedAt}}," +
		"\"exitCode\":{{json .State.ExitCode}},\"oomKilled\":{{json .State.OOMKilled}}," +
		"\"hostPid\":{{json .State.Pid}}," +
		"\"project\":{{json (index .Config.Labels \"com.docker.compose.project\")}}," +
		"\"service\":{{json (index .Config.Labels \"com.docker.compose.service\")}}," +
		"\"networks\":{{json .NetworkSettings.Networks}}}";

	private sealed record Identity(string Image, string StartedAt, bool Running, bool Paused, bool Restarting);
	private static Identity ValidateIdentity(DiagnosticCommandResult result, string id, string project, string service)
	{
		if (!result.Succeeded) throw new InvalidDataException("Diagnostic target inspection failed.");
		using var json = JsonDocument.Parse(result.StandardOutput);
		var root = json.RootElement;
		if (root.GetProperty("id").GetString() != id || root.GetProperty("project").GetString() != project ||
			root.GetProperty("service").GetString() != service ||
			root.GetProperty("networks").EnumerateObject().Select(n => n.Name).SequenceEqual([project + "_default"]) != true)
			throw new InvalidDataException("Diagnostic target ownership/network does not match the isolated run.");
		return new(root.GetProperty("image").GetString() ?? throw new InvalidDataException("Missing image."),
			root.GetProperty("startedAt").GetString() ?? throw new InvalidDataException("Missing process start."),
			root.GetProperty("running").GetBoolean(), root.GetProperty("paused").GetBoolean(), root.GetProperty("restarting").GetBoolean());
	}

	private static bool IsContainerId(string value) => value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
	private static async Task WriteNewAsync(string path, string value)
	{
		await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
		await stream.WriteAsync(Encoding.UTF8.GetBytes(value));
	}
}
