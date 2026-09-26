using System.Net.Sockets;

namespace Aion.Bots.Scenarios;

/// <summary>Retry a lost connection, never a quest/identity defect. The caller re-observes login state.</summary>
public static class NaturalJourneyReconnect
{
	public static async Task RunAsync(Func<CancellationToken, Task> connect,
		Func<TimeSpan, CancellationToken, Task> wait, Action<int, Exception> recordFailure,
		CancellationToken token, int maximumAttempts = 3)
	{
		if (maximumAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
		for (int attempt = 1; ; attempt++)
		{
			token.ThrowIfCancellationRequested();
			try
			{
				await connect(token);
				return;
			}
			catch (Exception failure) when (IsConnectionLoss(failure) && !token.IsCancellationRequested)
			{
				recordFailure(attempt, failure);
				if (attempt >= maximumAttempts) throw;
				await wait(TimeSpan.FromSeconds(5 * attempt), token);
			}
		}
	}

	public static bool IsConnectionLoss(Exception failure) => failure is EndOfStreamException or SocketException;
}
