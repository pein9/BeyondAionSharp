using System.Text.Json;
using System.Threading.Channels;
using Aion.LogWatch;

namespace Aion.Commons.Tests;

public sealed partial class ProblemWatcherTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void SecondGameServerMustBeExplicitlyEnabled(bool enabled)
	{
		using var run = new WatcherRun();
		var options = WatchOptions.Parse(["--run", "test", "--run-dir", run.Options().RunDirectory,
			"--second-game-server", enabled.ToString()]);
		Assert.Equal(enabled, options.SecondGameServer);
		Assert.Equal(enabled, options.Servers.Contains("gs2"));
		Assert.Equal(enabled, options.Servers.Contains("cs2"));
		Assert.Equal(enabled, options.DockerServices.Contains("chatserver2"));
		Assert.Equal(enabled, options.DockerServices.Contains("gameserver2"));
		Assert.False(run.Options().SecondGameServer);
	}

	[Theory]
	[InlineData("gs", "gs2")]
	[InlineData("gs2", "gs")]
	[InlineData("cs", "cs2")]
	[InlineData("cs2", "cs")]
	public async Task OneGameServerHeartbeatCannotHideTheOtherMissingProducer(string healthy, string missing)
	{
		using var run = new WatcherRun();
		var options = run.Options() with { SecondGameServer = true, Duration = null };
		var state = new ProblemWatcher.WatcherState(options);
		var now = DateTimeOffset.UtcNow.AddSeconds(31);
		foreach (string server in options.Servers.Where(server => server != missing))
			WriteInstanceHeartbeat(run, server, now);
		state.ReadFiles();
		state.CheckHeartbeats(now);
		await state.WriteSummaryAsync(CancellationToken.None);
		Assert.Contains($"NEW HEARTBEAT {missing}", run.ReadDigest(), StringComparison.Ordinal);
		using var summary = JsonDocument.Parse(File.ReadAllText(run.SummaryPath));
		Assert.Equal(1, summary.RootElement.GetProperty("total").GetInt32());
		Assert.Equal(5, summary.RootElement.GetProperty("servers").GetArrayLength());
		Assert.DoesNotContain($"NEW HEARTBEAT {healthy}\n", run.ReadDigest(), StringComparison.Ordinal);
	}

	[Fact]
	public async Task SecondServerErrorsKeepTheirIdentityAndOwnContext()
	{
		using var run = new WatcherRun();
		run.WriteProblem("1234abcd");
		string second = Path.Combine(run.Options().RunDirectory, "logs", "gs2");
		Directory.CreateDirectory(second);
		File.Move(Path.Combine(run.Options().RunDirectory, "logs", "gs", "gs.problems.jsonl"),
			Path.Combine(second, "gs.problems.jsonl"));
		File.WriteAllText(Path.Combine(second, "server_console.log"), "second server context\n");
		Assert.Equal(1, await ProblemWatcher.RunAsync(run.Options() with { SecondGameServer = true }));
		Assert.Contains("NEW ERROR gs2", run.ReadDigest(), StringComparison.Ordinal);
		Assert.Contains("second server context", File.ReadAllText(Path.Combine(run.ProblemDirectory("1234abcd"), "server-context.log")), StringComparison.Ordinal);
		using var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(run.ProblemDirectory("1234abcd"), "metadata.json")));
		Assert.Equal("gs2", metadata.RootElement.GetProperty("server").GetString());
	}

	[Fact]
	public async Task MisplacedProducerCannotRefreshAnotherServersLiveness()
	{
		using var run = new WatcherRun();
		WriteInstanceHeartbeat(run, "gs2", DateTimeOffset.UtcNow, producer: "ls");
		Assert.Equal(1, await ProblemWatcher.RunAsync(run.Options() with { SecondGameServer = true }));
		Assert.Contains("Log producer does not match the gs2 source directory", run.ReadDigest(), StringComparison.Ordinal);
	}

	[Fact]
	public async Task FirstServerAllowanceDoesNotSuppressSecondServerErrors()
	{
		using var run = new WatcherRun();
		run.WriteAllowlist("""
			[{"fp":"1234abcd","reason":"one instance only","owner":"tests","tracking":"test",
			"modes":["LIVE"],"servers":["gs"],"maxCount":1,"expires":"2099-01-01"}]
			""");
		run.WriteProblem("1234abcd");
		string second = Path.Combine(run.Options().RunDirectory, "logs", "gs2");
		Directory.CreateDirectory(second);
		File.Copy(Path.Combine(run.Options().RunDirectory, "logs", "gs", "gs.problems.jsonl"),
			Path.Combine(second, "gs.problems.jsonl"));
		Assert.Equal(1, await ProblemWatcher.RunAsync(run.Options() with { SecondGameServer = true }));
		using var summary = JsonDocument.Parse(File.ReadAllText(run.SummaryPath));
		Assert.Equal(1, summary.RootElement.GetProperty("suppressed").GetInt32());
		Assert.Equal(1, summary.RootElement.GetProperty("new").GetInt32());
		Assert.Contains("NEW ERROR gs2", run.ReadDigest(), StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("log", "gameserver2", "gs2")]
	[InlineData("event", "gameserver2", "gs2")]
	[InlineData("log", "chatserver2", "cs2")]
	[InlineData("event", "chatserver2", "cs2")]
	public async Task SecondContainerFailureIsAttributedToSecondGameServer(string source, string service, string identity)
	{
		using var run = new WatcherRun();
		var state = new ProblemWatcher.WatcherState(run.Options() with { SecondGameServer = true });
		var lines = Channel.CreateUnbounded<DockerLine>();
		lines.Writer.TryWrite(source == "log"
			? new DockerLine("log", service, "Unhandled exception. second server", false)
			: new DockerLine("event", "docker", JsonSerializer.Serialize(new
				{ service, action = "die", time = DateTimeOffset.UtcNow.ToUnixTimeSeconds() }), false));
		state.ReadDocker(lines.Reader);
		await state.WriteSummaryAsync(CancellationToken.None);
		Assert.Contains(identity, run.ReadDigest(), StringComparison.Ordinal);
		Assert.True(state.FailingProblemCount > 0);
	}

	[Theory]
	[InlineData(false, "gameserver2", "gs2", "Aion.GameServer.dll")]
	[InlineData(true, "gameserver2", "gs2", "Aion.GameServer.dll")]
	[InlineData(false, "chatserver2", "cs2", "Aion.ChatServer.dll")]
	[InlineData(true, "chatserver2", "cs2", "Aion.ChatServer.dll")]
	public async Task SecondServerHangCollectorTargetsOnlyItsExplicitComposeService(bool enabled, string service, string identity, string assembly)
	{
		using var run = new WatcherRun();
		var command = new FakeHangCommand("normal", service);
		var diagnostics = new HangDiagnostics(run.Options() with
			{ DockerEnabled = true, Duration = null, SecondGameServer = enabled }, command);
		diagnostics.Observe(new(identity, DateTimeOffset.UtcNow, null, null, 30, 20));
		var results = await diagnostics.CompleteAsync();
		if (!enabled)
		{
			Assert.Empty(results);
			Assert.Empty(command.Calls);
			return;
		}
		var result = Assert.Single(results);
		Assert.Equal("collected", result.Status);
		Assert.EndsWith(Path.Combine("hangs", identity), result.Directory, StringComparison.Ordinal);
		Assert.Contains($"label=com.docker.compose.service={service}", Assert.Single(command.Calls, args => args[0] == "ps"));
		Assert.Equal(assembly, Assert.Single(command.Calls, args => args[0] == "exec")[^1]);
	}

	private static void WriteInstanceHeartbeat(WatcherRun run, string server, DateTimeOffset at, string? producer = null)
	{
		string name = server switch { "gs2" => "gs", "cs2" => "cs", _ => server };
		string directory = Path.Combine(run.Options().RunDirectory, "logs", server);
		Directory.CreateDirectory(directory);
		File.AppendAllText(Path.Combine(directory, $"{name}.events.jsonl"), JsonSerializer.Serialize(new
			{ ts = at, srv = producer ?? name, run = "test", tpl = "Server heartbeat", msg = "heartbeat" }) + "\n");
	}
}
