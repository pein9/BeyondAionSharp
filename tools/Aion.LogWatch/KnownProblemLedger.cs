using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace Aion.LogWatch;

internal sealed class KnownProblemLedger
{
	private static readonly HashSet<string> ValidStatuses = ["new", "tracked", "fixed"];
	private readonly string path;
	private readonly Dictionary<string, LedgerEntry> entries;
	private readonly Dictionary<string, LedgerEntry> baseline;

	private KnownProblemLedger(string path, Dictionary<string, LedgerEntry> entries)
	{
		this.path = path;
		this.entries = entries;
		baseline = new(entries, StringComparer.Ordinal);
	}

	public IReadOnlyDictionary<string, LedgerEntry> Entries => entries;

	public static KnownProblemLedger Load(string path)
	{
		using var ledgerLock = new LedgerFileLock(path);
		var entries = new Dictionary<string, LedgerEntry>(StringComparer.Ordinal);
		if (!File.Exists(path))
			return new KnownProblemLedger(path, entries);
		using var document = JsonDocument.Parse(File.ReadAllText(path));
		if (document.RootElement.ValueKind != JsonValueKind.Array)
			throw new InvalidDataException($"Known-problem ledger '{path}' must contain a JSON array.");
		foreach (var root in document.RootElement.EnumerateArray())
		{
			var entry = new LedgerEntry(
				WatchProblem.RequiredString(root, "fp"),
				WatchProblem.RequiredString(root, "firstSeenSha"),
				WatchProblem.RequiredString(root, "lastSeenSha"),
				WatchProblem.RequiredString(root, "lastSeenRun"),
				RequiredPositiveLong(root, "count"),
				WatchProblem.RequiredString(root, "status"),
				WatchProblem.OptionalString(root, "tracking"),
				WatchProblem.OptionalString(root, "fixedIn"));
			Validate(entry, path);
			if (!entries.TryAdd(entry.Fingerprint, entry))
				throw new InvalidDataException($"Known-problem ledger fingerprint '{entry.Fingerprint}' is duplicated.");
		}
		return new KnownProblemLedger(path, entries);
	}

	public bool TryGet(string fingerprint, out LedgerEntry? entry) => entries.TryGetValue(fingerprint, out entry);

	public void RecordRun(
		IReadOnlyDictionary<string, int> problemCounts,
		RunProvenance provenance,
		string run)
	{
		foreach (var (fingerprint, occurrenceCount) in problemCounts)
		{
			if (entries.TryGetValue(fingerprint, out var existing))
			{
				entries[fingerprint] = existing with
				{
					LastSeenSha = provenance.GitSha,
					LastSeenRun = run,
					Count = existing.Count + occurrenceCount,
				};
			}
			else
			{
				entries.Add(fingerprint, new LedgerEntry(
					fingerprint,
					provenance.GitSha,
					provenance.GitSha,
					run,
					occurrenceCount,
					"new",
					null,
					null));
			}
		}
	}

	public async Task MarkFixedAfterGreenFullRunAsync(
		IReadOnlySet<string> seenFingerprints,
		string currentSha,
		string repositoryDirectory,
		CancellationToken cancellationToken)
	{
		if (!IsGitSha(currentSha))
			return;
		foreach (var (fingerprint, entry) in entries.ToArray())
		{
			if (entry.Status != "tracked" || seenFingerprints.Contains(fingerprint) || !IsGitSha(entry.LastSeenSha))
				continue;
			var fixingCommit = await FindFixingCommitAsync(fingerprint, entry.LastSeenSha, currentSha, repositoryDirectory, cancellationToken);
			if (fixingCommit != null)
				entries[fingerprint] = entry with { Status = "fixed", FixedIn = fixingCommit };
		}
	}

	public void Save()
	{
		using var ledgerLock = new LedgerFileLock(path);
		// Merge under the same cross-process lock as reads and replacement. A watcher's startup
		// snapshot is not authority to overwrite another run's counts or later maintainer edits.
		var merged = Load(path).entries;
		foreach (var (fingerprint, local) in entries)
		{
			baseline.TryGetValue(fingerprint, out var original);
			long delta = checked(local.Count - (original?.Count ?? 0));
			if (delta < 0) throw new InvalidDataException("Ledger observations cannot decrease within a watcher.");
			merged.TryGetValue(fingerprint, out var latest);
			if (delta > 0)
				merged[fingerprint] = latest == null ? local with { Count = delta } : latest with
				{
					Count = checked(latest.Count + delta), LastSeenSha = local.LastSeenSha, LastSeenRun = local.LastSeenRun,
				};
			else if (original != null && local != original && latest == original)
				// Auto-fix status is safe only if no other writer has changed this record meanwhile.
				merged[fingerprint] = local;
		}
		var directory = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(directory))
			Directory.CreateDirectory(directory);
		var temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
		try
		{
			var payload = merged.Values
				.OrderBy(entry => entry.Fingerprint, StringComparer.Ordinal)
				.Select(entry => new
				{
					fp = entry.Fingerprint,
					firstSeenSha = entry.FirstSeenSha,
					lastSeenSha = entry.LastSeenSha,
					lastSeenRun = entry.LastSeenRun,
					count = entry.Count,
					status = entry.Status,
					tracking = entry.Tracking,
					fixedIn = entry.FixedIn,
				})
				.ToArray();
			File.WriteAllText(temporaryPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions
			{
				WriteIndented = true,
				DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
			}) + "\n", new UTF8Encoding(false));
			File.Move(temporaryPath, path, overwrite: true);
			entries.Clear(); baseline.Clear();
			foreach (var (fingerprint, entry) in merged)
			{
				entries.Add(fingerprint, entry);
				baseline.Add(fingerprint, entry);
			}
		}
		finally
		{
			if (File.Exists(temporaryPath))
				File.Delete(temporaryPath);
		}
	}

	private sealed class LedgerFileLock : IDisposable
	{
		private readonly Mutex mutex;
		public LedgerFileLock(string path)
		{
			string canonical = Path.GetFullPath(path);
			if (OperatingSystem.IsWindows()) canonical = canonical.ToUpperInvariant();
			string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
			mutex = new Mutex(false, "AionLogWatch-Ledger-" + key);
			try
			{
				try
				{
					if (!mutex.WaitOne(TimeSpan.FromSeconds(30))) throw new TimeoutException("Timed out acquiring known-problem ledger lock.");
				}
				catch (AbandonedMutexException) { /* The crashed owner released ownership; validate disk state through Load. */ }
			}
			catch { mutex.Dispose(); throw; }
		}
		public void Dispose() { mutex.ReleaseMutex(); mutex.Dispose(); }
	}

	private static void Validate(LedgerEntry entry, string sourcePath)
	{
		if (entry.Fingerprint.Length != 8 || !entry.Fingerprint.All(Uri.IsHexDigit))
			throw new InvalidDataException($"Known-problem ledger '{sourcePath}' has invalid fingerprint '{entry.Fingerprint}'.");
		if (!ValidStatuses.Contains(entry.Status))
			throw new InvalidDataException($"Known-problem ledger fingerprint '{entry.Fingerprint}' has invalid status '{entry.Status}'.");
		if (entry.Status is "tracked" or "fixed" && string.IsNullOrWhiteSpace(entry.Tracking))
			throw new InvalidDataException($"Known-problem ledger fingerprint '{entry.Fingerprint}' is {entry.Status} but has no tracking id.");
		if (entry.Status == "fixed" && string.IsNullOrWhiteSpace(entry.FixedIn))
			throw new InvalidDataException($"Known-problem ledger fingerprint '{entry.Fingerprint}' is fixed but has no fixedIn commit.");
	}

	private static long RequiredPositiveLong(JsonElement root, string name)
	{
		if (!root.TryGetProperty(name, out var property) || !property.TryGetInt64(out var value) || value < 1)
			throw new InvalidDataException($"Known-problem ledger record needs positive integer '{name}'.");
		return value;
	}

	private static bool IsGitSha(string value) => value.Length is >= 7 and <= 40 && value.All(Uri.IsHexDigit);

	private static async Task<string?> FindFixingCommitAsync(
		string fingerprint,
		string lastSeenSha,
		string currentSha,
		string repositoryDirectory,
		CancellationToken cancellationToken)
	{
		var startInfo = new ProcessStartInfo("git")
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
			WorkingDirectory = repositoryDirectory,
		};
		startInfo.ArgumentList.Add("log");
		startInfo.ArgumentList.Add("--format=%H%x1f%B%x1e");
		startInfo.ArgumentList.Add($"{lastSeenSha}..{currentSha}");
		using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start git log.");
		var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
		var error = await process.StandardError.ReadToEndAsync(cancellationToken);
		await process.WaitForExitAsync(cancellationToken);
		if (process.ExitCode != 0)
			throw new InvalidOperationException($"git log failed while resolving {fingerprint}: {error.Trim()}");
		return FindFixingCommitInLog(output, fingerprint);
	}

	internal static string? FindFixingCommitInLog(string output, string fingerprint)
	{
		var trailer = $"Fixes-Fingerprint: {fingerprint}";
		foreach (var record in output.Split('\x1e', StringSplitOptions.RemoveEmptyEntries))
		{
			var separator = record.IndexOf('\x1f');
			if (separator < 0)
				continue;
			var message = record[(separator + 1)..];
			if (message.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
				.Any(line => line.Trim().Equals(trailer, StringComparison.Ordinal)))
				return record[..separator].Trim();
		}
		return null;
	}
}
