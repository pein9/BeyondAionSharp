using System.Diagnostics;
using System.Text.Json;

namespace Aion.LogWatch;

internal static class FullRunPromotion
{
	public static async Task RunAsync(string runDirectory, string repositoryDirectory, string ledgerPath, CancellationToken cancellationToken)
	{
		runDirectory = Path.GetFullPath(runDirectory);
		repositoryDirectory = Path.GetFullPath(repositoryDirectory);
		// Load before validation so the ledger's merge guard also covers observations or
		// maintainer edits made while the complete evidence tree is being revalidated.
		var ledger = KnownProblemLedger.Load(Path.GetFullPath(ledgerPath));
		var start = new ProcessStartInfo("python")
		{
			WorkingDirectory = repositoryDirectory, UseShellExecute = false, CreateNoWindow = true,
			RedirectStandardOutput = true, RedirectStandardError = true,
		};
		foreach (string argument in new[] { Path.Combine(repositoryDirectory, "scripts", "e2e", "validate-full-promotion.py"), runDirectory })
			start.ArgumentList.Add(argument);
		using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start aggregate Full validation.");
		var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
		var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
		try { await process.WaitForExitAsync(cancellationToken); }
		catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
		string output = await outputTask, error = await errorTask;
		if (process.ExitCode != 0) throw new InvalidDataException(error.Trim());
		using var proof = JsonDocument.Parse(output);
		var root = proof.RootElement;
		if (root.GetProperty("schemaVersion").GetInt32() != 1 || root.GetProperty("status").GetString() != "passed")
			throw new InvalidDataException("Aggregate Full validation did not pass.");
		string sha = root.GetProperty("gitSha").GetString() ?? throw new InvalidDataException("Missing validated revision.");
		var seen = root.GetProperty("seenFingerprints").EnumerateArray()
			.Select(value => value.GetString() ?? throw new InvalidDataException("Missing fingerprint.")).ToHashSet(StringComparer.Ordinal);
		await ledger.MarkFixedAfterGreenFullRunAsync(seen, sha, repositoryDirectory, cancellationToken);
		ledger.Save();
		Console.WriteLine($"Full ledger promotion validated for {root.GetProperty("run").GetString()} at {sha}.");
	}
}
