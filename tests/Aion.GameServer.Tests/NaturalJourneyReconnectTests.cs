using Aion.Bots.Scenarios;

namespace Aion.GameServer.Tests;

public sealed class NaturalJourneyReconnectTests
{
	[Fact]
	public async Task TemporaryOutageRetainsEachFailureAndBacksOffBeforeSuccess()
	{
		int attempts = 0;
		var failures = new List<int>();
		var waits = new List<TimeSpan>();
		await NaturalJourneyReconnect.RunAsync(_ => ++attempts < 3
			? Task.FromException(new EndOfStreamException("offline")) : Task.CompletedTask,
			(delay, _) => { waits.Add(delay); return Task.CompletedTask; },
			(attempt, _) => failures.Add(attempt), CancellationToken.None);
		Assert.Equal(3, attempts);
		Assert.Equal([1, 2], failures);
		Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)], waits);
	}

	[Fact]
	public async Task PersistentOutageStopsAndPreservesTheLastException()
	{
		int attempts = 0;
		var failure = new EndOfStreamException("offline");
		var actual = await Assert.ThrowsAsync<EndOfStreamException>(() => NaturalJourneyReconnect.RunAsync(
			_ => { attempts++; return Task.FromException(failure); }, (_, _) => Task.CompletedTask,
			(_, _) => { }, CancellationToken.None));
		Assert.Same(failure, actual);
		Assert.Equal(3, attempts);
	}

	[Fact]
	public async Task IdentityAndQuestDefectsAreNotRetried()
	{
		int attempts = 0;
		await Assert.ThrowsAsync<InvalidDataException>(() => NaturalJourneyReconnect.RunAsync(
			_ => { attempts++; throw new InvalidDataException("wrong character"); },
			(_, _) => throw new Exception("must not wait"), (_, _) => throw new Exception("must not retry"),
			CancellationToken.None));
		Assert.Equal(1, attempts);
	}

	[Fact]
	public async Task CancellationDuringBackoffPreventsAnotherConnection()
	{
		using var cancellation = new CancellationTokenSource();
		int attempts = 0;
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => NaturalJourneyReconnect.RunAsync(
			_ => { attempts++; throw new EndOfStreamException(); },
			(_, token) => { cancellation.Cancel(); token.ThrowIfCancellationRequested(); return Task.CompletedTask; },
			(_, _) => { }, cancellation.Token));
		Assert.Equal(1, attempts);
	}
}
