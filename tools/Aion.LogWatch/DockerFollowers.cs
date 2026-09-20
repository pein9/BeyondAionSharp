using System.Diagnostics;
using System.Threading.Channels;

namespace Aion.LogWatch;

internal sealed class DockerFollowers : IAsyncDisposable
{
	private static readonly string[] Services = ["loginserver", "chatserver", "gameserver", "mysql"];
	private readonly List<Process> processes = [];
	private readonly List<Task> readers = [];
	private readonly CancellationTokenSource lifetime = new();
	internal const int BufferedLineLimit = 1024;
	private readonly Channel<DockerLine> lines = CreateBuffer();

	internal static Channel<DockerLine> CreateBuffer() => Channel.CreateBounded<DockerLine>(
		new BoundedChannelOptions(BufferedLineLimit)
		{ SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait });

	public ChannelReader<DockerLine> Lines => lines.Reader;

	public void Start(WatchOptions options)
	{
		foreach (var service in Services)
		{
			var process = StartDocker(options,
				["compose", "-f", options.ComposeFile, "-p", options.ProjectName, "logs", "--follow", "--no-color", "--no-log-prefix", "--timestamps", "--tail", "all", service]);
			processes.Add(process);
			readers.Add(ReadLinesAsync(process.StandardOutput, new DockerLine("log", service, "", false), lifetime.Token));
			readers.Add(ReadLinesAsync(process.StandardError, new DockerLine("log", service, "", true), lifetime.Token));
		}

		var events = StartDocker(options,
			["compose", "-f", options.ComposeFile, "-p", options.ProjectName, "events", "--json", "--since", "1970-01-01T00:00:00Z"]);
		processes.Add(events);
		readers.Add(ReadLinesAsync(events.StandardOutput, new DockerLine("event", "docker", "", false), lifetime.Token));
		readers.Add(ReadLinesAsync(events.StandardError, new DockerLine("event", "docker", "", true), lifetime.Token));
	}

	public async ValueTask DisposeAsync()
	{
		await lifetime.CancelAsync();
		foreach (var process in processes)
		{
			if (!process.HasExited)
			{
				try { process.Kill(entireProcessTree: true); }
				catch (InvalidOperationException) { }
			}
		}
		try { await Task.WhenAll(readers); }
		catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
		foreach (var process in processes)
			process.Dispose();
		lines.Writer.TryComplete();
		lifetime.Dispose();
	}

	private static Process StartDocker(WatchOptions options, IReadOnlyList<string> arguments)
	{
		var start = new ProcessStartInfo("docker")
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};
		foreach (var argument in arguments)
			start.ArgumentList.Add(argument);
		start.Environment["AION_E2E_RUN_DIR"] = options.RunDirectory;
		return Process.Start(start) ?? throw new InvalidOperationException("Could not start docker compose follower.");
	}

	private async Task ReadLinesAsync(StreamReader reader, DockerLine template, CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			var line = await reader.ReadLineAsync(cancellationToken);
			if (line == null)
				break;
			await lines.Writer.WriteAsync(template with { Line = line }, cancellationToken);
		}
	}
}
