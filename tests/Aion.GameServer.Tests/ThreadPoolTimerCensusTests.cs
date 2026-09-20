using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class ThreadPoolTimerCensusTests
{
	[Fact]
	public void DefaultMetricsDoNotRetainDetails()
	{
		var metrics = new ThreadPoolMetrics();
		var completion = new TaskCompletionSource();
		metrics.Observe(new(ThreadPoolScheduleKind.Once, TimeSpan.Zero, null, completion.Task));
		Assert.Equal(1, metrics.ArmedTimerCount);
		Assert.Null(metrics.CaptureDetails());
		completion.SetResult();
		Assert.Equal(0, metrics.ArmedTimerCount);
	}

	[Fact]
	public void CensusGroupsActiveTasksAndRemovesEveryTerminalState()
	{
		var metrics = new ThreadPoolMetrics(captureDetails: true);
		var first = new TaskCompletionSource();
		var second = new TaskCompletionSource();
		var third = new TaskCompletionSource();
		var at = DateTimeOffset.UnixEpoch;
		Observe(first.Task, at);
		Observe(second.Task, at.AddSeconds(10));
		Observe(third.Task, at.AddSeconds(20));
		var census = metrics.CaptureDetails()!;
		Assert.Equal(3, census.ActiveCount);
		var group = Assert.Single(census.Groups);
		Assert.Equal(nameof(ThreadPoolScheduleKind.FixedRate), group.Kind);
		Assert.Equal(1000, group.DelayMilliseconds);
		Assert.Equal(2000, group.PeriodMilliseconds);
		Assert.Equal(3, group.Count);
		Assert.Equal(at, group.OldestScheduledUtc);
		Assert.EndsWith("ThreadPoolTimerCensusTests.Callback", group.Callback);
		first.SetResult();
		Assert.Equal(at.AddSeconds(10), Assert.Single(metrics.CaptureDetails()!.Groups).OldestScheduledUtc);
		second.SetCanceled();
		third.SetException(new InvalidOperationException("controlled terminal fault"));
		Assert.NotNull(third.Task.Exception); // Observe the intentional fault.
		Assert.Equal(0, metrics.ArmedTimerCount);
		Assert.Empty(metrics.CaptureDetails()!.Groups);
		Observe(Task.CompletedTask, at); // Already-complete observations must not leak an entry.
		Assert.Equal(0, metrics.CaptureDetails()!.ActiveCount);

		void Observe(Task completion, DateTimeOffset scheduledAt) => metrics.Observe(new(
			ThreadPoolScheduleKind.FixedRate, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), completion,
			(Func<CancellationToken, ValueTask>)Callback, scheduledAt));
	}

	[Fact]
	public void DiagnosticOutputCapsGroupsAndAccountsForOmittedTasks()
	{
		var metrics = new ThreadPoolMetrics(captureDetails: true);
		var completion = new TaskCompletionSource();
		for (int i = 0; i < 200; i++)
			metrics.Observe(new(ThreadPoolScheduleKind.Once, TimeSpan.FromMilliseconds(i), null, completion.Task));
		var census = metrics.CaptureDetails()!;
		Assert.Equal(200, census.ActiveCount);
		Assert.Equal(200, census.GroupCount);
		Assert.Equal(128, census.Groups.Count);
		Assert.Equal(72, census.OmittedActiveCount);
		Assert.Equal(census.ActiveCount, census.Groups.Sum(group => group.Count) + census.OmittedActiveCount);
		Assert.Equal(Enumerable.Range(0, 128).Select(i => (double)i), census.Groups.Select(group => group.DelayMilliseconds));
		completion.SetResult();
		Assert.Equal(0, metrics.CaptureDetails()!.ActiveCount);
	}

	[Fact]
	public async Task SchedulerSuppliesCallbackAndSchedulingTimeWithoutExecutingEarly()
	{
		var observations = new List<ThreadPoolScheduleObservation>();
		await using var pool = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance, observations.Add);
		var task = pool.Schedule(Callback, TimeSpan.FromHours(1));
		var observed = Assert.Single(observations);
		Assert.Equal(nameof(Callback), observed.Callback!.Method.Name);
		Assert.Equal(TimeSpan.FromHours(1), observed.Delay);
		Assert.NotNull(observed.ScheduledAt);
		Assert.False(task.Completion.IsCompleted);
		task.Cancel();
		await task.Completion.WaitAsync(TimeSpan.FromSeconds(5));
	}

	private static ValueTask Callback(CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
