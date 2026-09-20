using System.Text.Json;
using Aion.LogWatch;

namespace Aion.Commons.Tests;

public sealed partial class ProblemWatcherTests
{
	[Theory]
	[InlineData("normal", "collected", true)]
	[InlineData("ambiguous", "failed", false)]
	[InlineData("invalid-id", "failed", false)]
	[InlineData("wrong-project", "failed", false)]
	[InlineData("wrong-service", "failed", false)]
	[InlineData("wrong-id", "failed", false)]
	[InlineData("shared-network", "failed", false)]
	[InlineData("changed-start", "failed", false)]
	[InlineData("changed-image", "failed", false)]
	[InlineData("changed-during-stack", "failed", true)]
	[InlineData("paused", "partial", false)]
	[InlineData("stopped", "partial", false)]
	[InlineData("restarting", "partial", false)]
	[InlineData("missing-tool", "partial", true)]
	[InlineData("timeout", "partial", true)]
	[InlineData("truncated", "partial", true)]
	[InlineData("command-throws", "failed", false)]
	public async Task HangCollectionIsBoundedToOneOwnedServerAndReportsPartialEvidence(string variant, string status, bool execExpected)
	{
		using var run = new WatcherRun();
		var command = new FakeHangCommand(variant);
		var diagnostics = new HangDiagnostics(run.Options() with { DockerEnabled = true, Duration = null }, command);
		var observation = new HangObservation("gs", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(-30), "last sample", 30, 20);
		diagnostics.Observe(observation);
		diagnostics.Observe(observation); // No repeated probes, even if a second incident follows recovery.
		var result = Assert.Single(await diagnostics.CompleteAsync());
		Assert.Equal(status, result.Status);
		Assert.Equal(execExpected, command.Calls.Any(args => args[0] == "exec"));
		Assert.Single(command.Calls, args => args[0] == "ps");
		Assert.All(command.Calls.Where(args => args[0] is "top" or "stats" or "exec" or "inspect"),
			args => Assert.Contains(FakeHangCommand.ContainerId, args));
		Assert.DoesNotContain(command.Calls, args => args.Contains("restart") || args.Contains("kill") || args.Contains("unpause"));
		Assert.DoesNotContain(".Config.Env", HangDiagnostics.InspectFormat, StringComparison.Ordinal);
		Assert.True(File.Exists(Path.Combine(result.Directory!, "observation.json")));
		if (execExpected)
		{
			var args = Assert.Single(command.Calls, args => args[0] == "exec");
			Assert.Contains("timeout --signal=KILL 8s", args[4], StringComparison.Ordinal);
			Assert.Equal("Aion.GameServer.dll", args[^1]);
		}
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, true)]
	public async Task FileOnlyAndSnapshotWatchingNeverStartDockerDiagnostics(bool docker, bool snapshot)
	{
		using var run = new WatcherRun();
		var command = new FakeHangCommand("normal");
		var diagnostics = new HangDiagnostics(run.Options() with { DockerEnabled = docker, Duration = snapshot ? TimeSpan.Zero : null }, command);
		diagnostics.Observe(new("gs", DateTimeOffset.UtcNow, null, null, 30, 20));
		Assert.Empty(await diagnostics.CompleteAsync());
		Assert.Empty(command.Calls);
	}

	[Fact]
	public async Task NonIsolatedProjectIsRejectedBeforeAnyDockerCommand()
	{
		using var run = new WatcherRun();
		var command = new FakeHangCommand("normal");
		var diagnostics = new HangDiagnostics(run.Options() with { DockerEnabled = true, Duration = null, ProjectName = "aion" }, command);
		diagnostics.Observe(new("gs", DateTimeOffset.UtcNow, null, null, 30, 20));
		Assert.Equal("failed", Assert.Single(await diagnostics.CompleteAsync()).Status);
		Assert.Empty(command.Calls);
	}

	[Fact]
	public async Task CollectionDoesNotBlockWatcherAndCannotEraseTheHeartbeatFailure()
	{
		using var run = new WatcherRun();
		var command = new FakeHangCommand("normal") { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
		var state = new ProblemWatcher.WatcherState(run.Options() with { DockerEnabled = true, Duration = null }, command);
		AppendHeartbeat(run, DateTimeOffset.UtcNow.AddSeconds(-30));
		state.ReadFiles();
		state.CheckHeartbeats(DateTimeOffset.UtcNow);
		// The command's unresolved gate cannot stop unrelated log ingestion on this thread.
		run.WriteProblem("1234abcd");
		state.ReadFiles();
		int problemsDuringCollection = state.FailingProblemCount;
		command.Gate.SetResult();
		await state.WriteSummaryAsync(CancellationToken.None);
		Assert.Equal(2, problemsDuringCollection);
		using var summary = JsonDocument.Parse(File.ReadAllText(run.SummaryPath));
		Assert.True(summary.RootElement.GetProperty("failed").GetBoolean());
		Assert.Equal("collected", Assert.Single(summary.RootElement.GetProperty("hangDiagnostics").EnumerateArray()).GetProperty("status").GetString());
		Assert.Contains("HANG_DIAGNOSTICS gs status=collected", run.ReadDigest(), StringComparison.Ordinal);
	}

	[Fact]
	public async Task ExistingDiagnosticEvidenceIsNeverOverwritten()
	{
		using var run = new WatcherRun();
		string directory = Path.Combine(run.Options().RunDirectory, "hangs", "gs");
		Directory.CreateDirectory(directory);
		string observation = Path.Combine(directory, "observation.json");
		File.WriteAllText(observation, "preserve this evidence");
		var command = new FakeHangCommand("normal");
		var diagnostics = new HangDiagnostics(run.Options() with { DockerEnabled = true, Duration = null }, command);
		diagnostics.Observe(new("gs", DateTimeOffset.UtcNow, null, null, 30, 20));
		Assert.Equal("failed", Assert.Single(await diagnostics.CompleteAsync()).Status);
		Assert.Equal("preserve this evidence", File.ReadAllText(observation));
		Assert.Empty(command.Calls);
	}

	[Fact]
	public async Task FinalSummaryDrainsProblemsWrittenWhileDiagnosticsAreFinishing()
	{
		using var run = new WatcherRun();
		var command = new FakeHangCommand("normal") { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
		var state = new ProblemWatcher.WatcherState(run.Options() with { DockerEnabled = true, Duration = null }, command);
		AppendHeartbeat(run, DateTimeOffset.UtcNow.AddSeconds(-30));
		state.ReadFiles();
		state.CheckHeartbeats(DateTimeOffset.UtcNow);
		var summary = state.WriteSummaryAsync(CancellationToken.None);
		run.WriteProblem("1234abcd");
		command.Gate.SetResult();
		await summary;
		Assert.Equal(2, state.FailingProblemCount);
		Assert.Contains("fp=1234abcd", run.ReadDigest(), StringComparison.Ordinal);
	}

	[Fact]
	public async Task DiagnosticProcessOutputIsBoundedAndTruncationIsNotSuccess()
	{
		var result = await BoundedDiagnosticCommand.RunProcessAsync("dotnet", ["--info"], TimeSpan.FromSeconds(10), CancellationToken.None, outputLimit: 32);
		Assert.True(result.Truncated);
		Assert.False(result.Succeeded);
		Assert.InRange(result.StandardOutput.Length, 1, 32);
	}

	[Fact]
	public async Task DiagnosticProcessLaunchFailureIsRecorded()
	{
		var result = await BoundedDiagnosticCommand.RunProcessAsync("aion-nonexistent-diagnostic-tool", [], TimeSpan.FromSeconds(1), CancellationToken.None);
		Assert.False(result.Succeeded);
		Assert.NotNull(result.Failure);
	}

	[Fact]
	public async Task DiagnosticProcessDeadlineTerminatesOnlyItsOwnedChild()
	{
		string executable = OperatingSystem.IsWindows() ? "pwsh" : "sh";
		string[] args = OperatingSystem.IsWindows() ? ["-NoProfile", "-Command", "Start-Sleep -Seconds 30"] : ["-c", "sleep 30"];
		var result = await BoundedDiagnosticCommand.RunProcessAsync(executable, args, TimeSpan.FromMilliseconds(200), CancellationToken.None);
		Assert.True(result.TimedOut);
		Assert.False(result.Succeeded);
	}

	private sealed class FakeHangCommand(string variant) : IDiagnosticCommand
	{
		internal const string ContainerId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
		internal readonly List<string[]> Calls = [];
		internal TaskCompletionSource? Gate;
		private int inspections;

		public async Task<DiagnosticCommandResult> RunAsync(IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken token)
		{
			Assert.InRange(timeout.TotalSeconds, 1, 10);
			Assert.True(token.CanBeCanceled);
			Calls.Add(arguments.ToArray());
			if (Gate != null) await Gate.Task.WaitAsync(token);
			if (variant == "command-throws") throw new IOException("Synthetic Docker failure.");
			if (arguments[0] == "ps") return new(0, variant switch
			{
				"ambiguous" => ContainerId + "\n" + ContainerId,
				"invalid-id" => "untrusted-target",
				_ => ContainerId,
			}, "");
			if (arguments[0] == "inspect")
			{
				inspections++;
				var networks = new Dictionary<string, object> { ["aion-bots-test_default"] = new { } };
				if (variant == "shared-network") networks.Add("aion_default", new { });
				return new(0, JsonSerializer.Serialize(new
				{
					id = variant == "wrong-id" ? new string('b', 64) : ContainerId,
					project = variant == "wrong-project" ? "aion" : "aion-bots-test",
					service = variant == "wrong-service" ? "mysql" : "gameserver", networks,
					image = variant == "changed-image" && inspections > 1 ? "changed" : "image",
					startedAt = (variant == "changed-start" && inspections > 1) ||
						(variant == "changed-during-stack" && inspections > 2) ? "changed" : "start",
					running = variant != "stopped", paused = variant == "paused", restarting = variant == "restarting",
				}), "");
			}
			if (arguments[0] == "exec") return variant switch
			{
				"missing-tool" => new(69, "", ""),
				"timeout" => new(null, "", "", TimedOut: true),
				"truncated" => new(0, "partial stack", "", Truncated: true),
				_ => new(0, "managed stack", ""),
			};
			return new(0, "sample", "");
		}
	}
}
