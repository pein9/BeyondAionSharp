using Aion.LiveBots;

namespace Aion.GameServer.Tests;

public sealed class LiveNavigationWorkQueueTests
{
	[Fact]
	public async Task WaitingSearchesYieldWithoutEnteringGeometryAndCancellationDoesNotConsumeASlot()
	{
		using var queue = new LiveNavigationWorkQueue(2);
		using var release = new ManualResetEventSlim();
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		int active = 0;
		Task<int> Hold() => Task.Factory.StartNew(() => queue.RunAsync(() =>
		{
			if (Interlocked.Increment(ref active) == 2) entered.SetResult();
			release.Wait();
			Interlocked.Decrement(ref active);
			return 7;
		}, CancellationToken.None), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
		Task<int>[] held = [Hold(), Hold()];
		Task<int>? waiting = null;
		try
		{
			await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
			int calls = 0;
			waiting = queue.RunAsync(() => Interlocked.Increment(ref calls), CancellationToken.None);
			using var stop = new CancellationTokenSource();
			var canceled = queue.RunAsync<int>(() => throw new InvalidOperationException("Canceled search must not execute."), stop.Token);
			Assert.False(waiting.IsCompleted);
			Assert.False(canceled.IsCompleted);
			Assert.Equal(0, calls);
			stop.Cancel();
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
			Assert.Equal(2, Volatile.Read(ref active));
		}
		finally
		{
			release.Set();
			Assert.Equal(new[] { 7, 7 }, await Task.WhenAll(held));
			if (waiting != null) Assert.Equal(1, await waiting);
		}
		Assert.Equal(42, await queue.RunAsync(() => 42, CancellationToken.None));
	}

	[Fact]
	public async Task FailureAndInSearchCancellationReleaseSlotsWithoutReturningFallbacks()
	{
		using var queue = new LiveNavigationWorkQueue(1);
		var failure = new InvalidDataException("No checked route");
		Assert.Same(failure, await Assert.ThrowsAsync<InvalidDataException>(() =>
			queue.RunAsync<int>(() => throw failure, CancellationToken.None)));
		using var stop = new CancellationTokenSource();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queue.RunAsync(() =>
		{
			stop.Cancel();
			return 12;
		}, stop.Token));
		Assert.Equal(42, await queue.RunAsync(() => 42, CancellationToken.None));
	}
}
