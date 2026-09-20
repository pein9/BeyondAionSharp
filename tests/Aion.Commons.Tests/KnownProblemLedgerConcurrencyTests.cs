using System.Text.Json;
using System.Diagnostics;
using Aion.LogWatch;

namespace Aion.Commons.Tests;

public sealed class KnownProblemLedgerConcurrencyTests
{
	[Theory]
	[InlineData("none", "fixed")]
	[InlineData("count", "tracked")]
	[InlineData("tracking", "tracked")]
	public void AutoFixDoesNotOverrideAConcurrentObservationOrMaintainerEdit(string change, string expectedStatus)
	{
		using var fixture = new LedgerFixture();
		File.WriteAllText(fixture.Path, """[{"fp":"1234abcd","firstSeenSha":"1111111","lastSeenSha":"1111111","lastSeenRun":"initial","count":1,"status":"tracked","tracking":"original"}]""");
		var stale = KnownProblemLedger.Load(fixture.Path);
		// Simulate the in-memory transition made by MarkFixedAfterGreenFullRunAsync;
		// these cases isolate merge policy from git trailer discovery.
		((IDictionary<string, LedgerEntry>)stale.Entries)["1234abcd"] = stale.Entries["1234abcd"] with { Status = "fixed", FixedIn = "2222222" };
		if (change == "count")
		{
			var other = KnownProblemLedger.Load(fixture.Path);
			other.RecordRun(new Dictionary<string, int> { ["1234abcd"] = 1 }, new("3333333", 1, "test"), "concurrent");
			other.Save();
		}
		if (change == "tracking") File.WriteAllText(fixture.Path, File.ReadAllText(fixture.Path).Replace("original", "maintainer update"));
		stale.Save();
		var saved = KnownProblemLedger.Load(fixture.Path).Entries["1234abcd"];
		Assert.Equal(expectedStatus, saved.Status);
		Assert.Equal(change == "none" ? "2222222" : null, saved.FixedIn);
		Assert.Equal(change == "count" ? 2 : 1, saved.Count);
		Assert.Equal(change == "tracking" ? "maintainer update" : "original", saved.Tracking);
	}

	[Fact]
	public async Task IndependentWatcherProcessesPreserveBothRuns()
	{
		using var fixture = new LedgerFixture();
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		string allowlist = System.IO.Path.Combine(fixture.DirectoryPath, "allowlist.json");
		File.WriteAllText(allowlist, "[]");
		string testAssembly = typeof(KnownProblemLedgerConcurrencyTests).Assembly.Location;
		var children = new List<(Process Process, string Directory, Task<string> Output, Task<string> Error)>();
		try
		{
			for (int i = 0; i < 2; i++)
			{
				string directory = System.IO.Path.Combine(fixture.DirectoryPath, "run" + i);
				Directory.CreateDirectory(System.IO.Path.Combine(directory, "logs", "gs"));
				File.WriteAllText(System.IO.Path.Combine(directory, "logs", "gs", "gs.problems.jsonl"),
					"""{"lvl":"ERROR","ts":"2026-09-20T00:00:00Z","srv":"gs","fp":"1234abcd","tpl":"Synthetic failure","msg":"Synthetic failure","cat":"test","frame":"test","stack":"test"}""" + "\n");
				var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
				{ UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
				foreach (string arg in new[] { "exec", "--runtimeconfig", System.IO.Path.ChangeExtension(testAssembly, ".runtimeconfig.json"),
					"--depsfile", System.IO.Path.ChangeExtension(testAssembly, ".deps.json"), typeof(ProblemWatcher).Assembly.Location,
					"--run", "child" + i, "--run-dir", directory, "--ledger", fixture.Path, "--allowlist", allowlist,
					"--mode", "record", "--no-docker", "true", "--stop-file", System.IO.Path.Combine(directory, "stop"), "--duration-seconds", "30" }) start.ArgumentList.Add(arg);
				var child = Process.Start(start)!;
				children.Add((child, directory, child.StandardOutput.ReadToEndAsync(), child.StandardError.ReadToEndAsync()));
			}
			// Both processes must load their initial ledger and observe their event before either saves.
			foreach (var child in children)
			{
				string digest = System.IO.Path.Combine(child.Directory, "digest.log");
				while (!HasObservedProblem(digest))
				{
					Assert.False(child.Process.HasExited, "Watcher exited before the test barrier: " + (child.Error.IsCompleted ? await child.Error : ""));
					await Task.Delay(20, deadline.Token);
				}
			}
			foreach (var child in children) File.WriteAllText(System.IO.Path.Combine(child.Directory, "stop"), "stop");
			foreach (var child in children)
			{
				await child.Process.WaitForExitAsync(deadline.Token);
				Assert.True(child.Process.ExitCode == 0, await child.Error + await child.Output);
			}
			Assert.Equal(2, KnownProblemLedger.Load(fixture.Path).Entries["1234abcd"].Count);
		}
		finally
		{
			foreach (var child in children)
			{
				if (!child.Process.HasExited) { child.Process.Kill(entireProcessTree: true); await child.Process.WaitForExitAsync(); }
				child.Process.Dispose();
			}
		}
	}

	private static bool HasObservedProblem(string digest)
	{
		if (!File.Exists(digest)) return false;
		using var stream = new FileStream(digest, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		using var reader = new StreamReader(stream);
		return reader.ReadToEnd().Contains("1234abcd", StringComparison.Ordinal);
	}

    [Fact]
    public void StaleGreenWriterPreservesNewCountsAndMaintainerTracking()
    {
        using var fixture = new LedgerFixture();
        var initial = KnownProblemLedger.Load(fixture.Path);
        initial.RecordRun(new Dictionary<string, int> { ["1234abcd"] = 3 }, new("1111111", 1, "test"), "initial");
        initial.Save();
        var stale = KnownProblemLedger.Load(fixture.Path);
        var current = KnownProblemLedger.Load(fixture.Path);
        current.RecordRun(new Dictionary<string, int> { ["1234abcd"] = 2, ["deadbeef"] = 1 }, new("2222222", 1, "test"), "current");
        current.Save();
        var rows = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(File.ReadAllText(fixture.Path))!;
        var changed = rows.Single(row => row["fp"].GetString() == "1234abcd");
        changed["tracking"] = JsonSerializer.SerializeToElement("maintainer update");
        changed["status"] = JsonSerializer.SerializeToElement("tracked");
        File.WriteAllText(fixture.Path, JsonSerializer.Serialize(rows));
        stale.Save();
        var saved = KnownProblemLedger.Load(fixture.Path);
        Assert.Equal(5, saved.Entries["1234abcd"].Count);
        Assert.Equal("maintainer update", saved.Entries["1234abcd"].Tracking);
        Assert.Equal("tracked", saved.Entries["1234abcd"].Status);
        Assert.Equal("current", saved.Entries["1234abcd"].LastSeenRun);
        Assert.Equal(1, saved.Entries["deadbeef"].Count);
    }

    [Fact]
    public async Task StaleConcurrentWritersAddOnlyTheirDeltasAndRepeatedSaveIsIdempotent()
    {
        using var fixture = new LedgerFixture();
        var writers = Enumerable.Range(0, 16).Select(_ => KnownProblemLedger.Load(fixture.Path)).ToArray();
        await Task.WhenAll(writers.Select((writer, i) => Task.Run(() =>
        {
            writer.RecordRun(new Dictionary<string, int> { ["1234abcd"] = i + 1 }, new("1111111", 1, "test"), "run" + i);
            writer.Save(); writer.Save();
        })));
        Assert.Equal(136, KnownProblemLedger.Load(fixture.Path).Entries["1234abcd"].Count);
        writers[0].RecordRun(new Dictionary<string, int> { ["1234abcd"] = 4 }, new("2222222", 1, "test"), "later");
        writers[0].Save();
        Assert.Equal(140, KnownProblemLedger.Load(fixture.Path).Entries["1234abcd"].Count);
    }

    private sealed class LedgerFixture : IDisposable
    {
        private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aion-ledger-" + Guid.NewGuid().ToString("N"));
        public LedgerFixture() => Directory.CreateDirectory(directory);
        public string DirectoryPath => directory;
        public string Path => System.IO.Path.Combine(directory, "ledger.json");
        public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }
}
