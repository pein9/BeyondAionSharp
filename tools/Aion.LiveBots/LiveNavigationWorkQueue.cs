namespace Aion.LiveBots;

/// <summary>
/// Limit CPU-heavy offline route searches, not bot actions or game connections.
/// Waiting planners yield so socket/timer continuations can keep using the worker pool.
/// No route cache: every request still performs the ordinary geometry checks.
/// </summary>
internal sealed class LiveNavigationWorkQueue(int parallelism = 4) : IDisposable
{
	internal static LiveNavigationWorkQueue Shared { get; } = new();
	private readonly SemaphoreSlim slots = new(parallelism, parallelism);

	internal async Task<T> RunAsync<T>(Func<T> search, CancellationToken token)
	{
		ArgumentNullException.ThrowIfNull(search);
		await slots.WaitAsync(token).ConfigureAwait(false);
		try
		{
			token.ThrowIfCancellationRequested();
			T result = search();
			token.ThrowIfCancellationRequested();
			return result;
		}
		finally { slots.Release(); }
	}

	public void Dispose() => slots.Dispose();
}
