using System.Text.Json;

namespace Aion.LogWatch;

/// <summary>Bounded live attribution cache; retained trace files remain the historical authority.</summary>
internal sealed class BotTraceHistory(string run)
{
	internal const int RecordsPerAccount = 64;
	internal const int CharactersPerAccount = 128 * 1024;
	private readonly Dictionary<string, AccountHistory> accounts = new(StringComparer.Ordinal);
	private readonly HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
	public int RetainedRecords => accounts.Values.Sum(account => account.Recent.Count);
	public int RetainedCharacters => accounts.Values.Sum(account => account.Characters);

	public void Observe(string path, BotStep step)
	{
		paths.Add(path);
		if (!accounts.TryGetValue(step.Account, out var account))
			accounts.Add(step.Account, account = new AccountHistory());
		account.Paths.Add(path);
		account.Recent.Enqueue(step);
		account.Characters += step.RawLine.Length;
		while (account.Recent.Count > RecordsPerAccount || account.Characters > CharactersPerAccount)
		{
			var removed = account.Recent.Dequeue();
			account.Characters -= removed.RawLine.Length;
			if (account.DiscardedThrough == null || removed.Timestamp > account.DiscardedThrough)
				account.DiscardedThrough = removed.Timestamp;
		}
	}

	public BotStep? LatestBefore(string accountName, DateTimeOffset timestamp)
	{
		if (!accounts.TryGetValue(accountName, out var account)) return null;
		var candidate = account.Recent.Where(step => step.Timestamp <= timestamp).MaxBy(step => step.Timestamp);
		// Strictly newer preserves first-record tie behavior. Nonmonotonic/late records also fall back safely.
		if (account.DiscardedThrough == null || candidate != null && candidate.Timestamp > account.DiscardedThrough)
			return candidate;
		return ReadSteps(account.Paths, accountName, timestamp).MaxBy(step => step.Timestamp);
	}

	public IReadOnlyList<BotStep> ContextBefore(string? accountName, DateTimeOffset timestamp, int count = 50)
	{
		if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
		IEnumerable<string> sources = accountName == null ? paths
			: accounts.TryGetValue(accountName, out var account) ? account.Paths : [];
		var retained = new PriorityQueue<BotStep, (DateTimeOffset Timestamp, long Sequence)>();
		long sequence = 0;
		foreach (var step in ReadSteps(sources, accountName, timestamp))
		{
			retained.Enqueue(step, (step.Timestamp, sequence++));
			if (retained.Count > count) retained.Dequeue();
		}
		return retained.UnorderedItems.OrderBy(item => item.Priority).Select(item => item.Element).ToArray();
	}

	private IEnumerable<BotStep> ReadSteps(IEnumerable<string> sources, string? account, DateTimeOffset timestamp)
	{
		foreach (string source in sources)
		{
			foreach (string line in new FileTail(source).ReadNewLines())
			{
				BotStep? step = null;
				try
				{
					using var document = JsonDocument.Parse(line);
					var root = document.RootElement;
					string? recordRun = WatchProblem.OptionalString(root, "run");
					if (recordRun == null || recordRun == run) step = BotStep.FromJson(root, line);
				}
				catch (Exception error) when (error is JsonException or InvalidDataException or InvalidOperationException)
				{
					// The main watcher reports malformed complete records. Context must not erase that failure.
				}
				if (step != null && (account == null || step.Account == account) && step.Timestamp <= timestamp)
					yield return step;
			}
		}
	}

	private sealed class AccountHistory
	{
		public Queue<BotStep> Recent { get; } = new();
		public HashSet<string> Paths { get; } = new(StringComparer.OrdinalIgnoreCase);
		public int Characters { get; set; }
		public DateTimeOffset? DiscardedThrough { get; set; }
	}
}
