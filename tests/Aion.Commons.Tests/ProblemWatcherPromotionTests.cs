using System.Diagnostics;
using System.Text.Json;
using Aion.LogWatch;

namespace Aion.Commons.Tests;

public sealed partial class ProblemWatcherTests
{
	[Fact]
	public async Task PassingFullChildNeverPromotesAnAbsentFingerprintWithARealFixTrailer()
	{
		using var git = new PromotionGitFixture();
		using var run = new WatcherRun();
		run.WriteLedger(git.Ledger("tracked"));
		run.WriteProvenance(git.Fixed, 1, "test");
		Assert.Equal(0, await ProblemWatcher.RunAsync(run.Options() with { FullRun = true }));
		Assert.Equal("tracked", KnownProblemLedger.Load(run.LedgerPath).Entries["1234abcd"].Status);
	}

	[Theory]
	[InlineData("tracked", false, true, "fixed")]
	[InlineData("tracked", true, true, "tracked")]
	[InlineData("tracked", false, false, "tracked")]
	[InlineData("new", false, true, "new")]
	public async Task PromotionRequiresTrackedAbsentFingerprintAndExactGitTrailer(string status, bool seen, bool trailer, string expected)
	{
		using var git = new PromotionGitFixture(trailer);
		using var run = new WatcherRun();
		run.WriteLedger(git.Ledger(status));
		var ledger = KnownProblemLedger.Load(run.LedgerPath);
		await ledger.MarkFixedAfterGreenFullRunAsync(seen ? new HashSet<string> { "1234abcd" } : [], git.Fixed, git.DirectoryPath, CancellationToken.None);
		ledger.Save();
		var entry = KnownProblemLedger.Load(run.LedgerPath).Entries["1234abcd"];
		Assert.Equal(expected, entry.Status);
		Assert.Equal(expected == "fixed" ? git.Fixed : null, entry.FixedIn);
		Assert.Equal(1, entry.Count);
	}

	[Fact]
	public async Task PromotionCommandRevalidatesEvidenceBeforeAnyLedgerMutation()
	{
		using var run = new WatcherRun();
		const string contents = """[{"fp":"1234abcd","firstSeenSha":"1111111","lastSeenSha":"1111111","lastSeenRun":"old","count":1,"status":"tracked","tracking":"test"}]""";
		run.WriteLedger(contents);
		string before = File.ReadAllText(run.LedgerPath);
		var repository = new DirectoryInfo(AppContext.BaseDirectory);
		while (repository != null && !File.Exists(Path.Combine(repository.FullName, "scripts", "e2e", "validate-full-promotion.py")))
			repository = repository.Parent;
		Assert.NotNull(repository);
		await Assert.ThrowsAsync<InvalidDataException>(() => FullRunPromotion.RunAsync(run.Options().RunDirectory,
			repository.FullName, run.LedgerPath, CancellationToken.None));
		Assert.Equal(before, File.ReadAllText(run.LedgerPath));
	}

	// Only disposable git objects/empty commits: never touch the source checkout or its ledger.
	private sealed class PromotionGitFixture : IDisposable
	{
		public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "aion-promotion-git-" + Guid.NewGuid().ToString("N"));
		public string Initial { get; }
		public string Fixed { get; }
		public PromotionGitFixture(bool trailer = true)
		{
			Directory.CreateDirectory(DirectoryPath);
			Git("init", "--quiet");
			Commit("Initial fixture");
			Initial = Git("rev-parse", "HEAD");
			Commit(trailer ? "Fixture fix\n\nFixes-Fingerprint: 1234abcd" : "Not a trailer Fixes-Fingerprint: 1234abcd");
			Fixed = Git("rev-parse", "HEAD");
		}
		public string Ledger(string status) => JsonSerializer.Serialize(new[] { new { fp = "1234abcd", firstSeenSha = Initial,
			lastSeenSha = Initial, lastSeenRun = "old", count = 1, status, tracking = status == "new" ? null : "test" } });
		private void Commit(string message) => Git("-c", "user.name=Promotion fixture", "-c", "user.email=fixture@example.invalid",
			"-c", "commit.gpgsign=false", "-c", "core.hooksPath=" + Path.Combine(DirectoryPath, "no-hooks"), "commit", "--allow-empty", "-m", message, "--quiet");
		private string Git(params string[] arguments)
		{
			var start = new ProcessStartInfo("git") { WorkingDirectory = DirectoryPath, UseShellExecute = false, CreateNoWindow = true,
				RedirectStandardOutput = true, RedirectStandardError = true };
			foreach (string argument in arguments) start.ArgumentList.Add(argument);
			using var process = Process.Start(start)!;
			string output = process.StandardOutput.ReadToEnd(), error = process.StandardError.ReadToEnd();
			process.WaitForExit();
			Assert.True(process.ExitCode == 0, error);
			return output.Trim();
		}
		public void Dispose()
		{
			foreach (string file in Directory.EnumerateFiles(DirectoryPath, "*", SearchOption.AllDirectories))
				File.SetAttributes(file, FileAttributes.Normal);
			Directory.Delete(DirectoryPath, recursive: true);
		}
	}
}
