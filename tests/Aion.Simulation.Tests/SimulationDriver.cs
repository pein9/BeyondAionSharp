using Aion.Bots.Transport;
using Aion.Bots.Scenarios;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed class SimulationDriver
{
	public static readonly TimeSpan DefaultWallTimeBudget = TimeSpan.FromSeconds(10);
	public static readonly TimeSpan P003WallTimePerVirtualMinuteBudget = TimeSpan.FromSeconds(2.16);

	private readonly VirtualThreadPool clock;
	private readonly IReadOnlyList<InProcessBotTransport> transports;
	private readonly TimeProvider timeProvider;
	private readonly PriorityQueue<ScheduledBotAction, (long DueMillis, long Sequence)> actions = new();
	private long sequence;

	public SimulationDriver(
		VirtualThreadPool clock,
		IEnumerable<InProcessBotTransport>? transports = null,
		TimeProvider? timeProvider = null)
	{
		this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
		this.transports = transports?.ToArray() ?? [];
		this.timeProvider = timeProvider ?? TimeProvider.System;
	}

	public void ScheduleAction(
		string bot,
		string action,
		TimeSpan delay,
		Func<CancellationToken, ValueTask> callback)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(bot);
		ArgumentException.ThrowIfNullOrWhiteSpace(action);
		ArgumentNullException.ThrowIfNull(callback);
		long delayMillis = checked((long)delay.TotalMilliseconds);
		if (delayMillis < 0)
			throw new ArgumentOutOfRangeException(nameof(delay));
		long dueMillis = checked(clock.NowMillis + delayMillis);
		actions.Enqueue(new ScheduledBotAction(bot, action, dueMillis, callback), (dueMillis, ++sequence));
	}

	/// <summary>Applies the manifest isolation delay before the next scenario starts.</summary>
	public async ValueTask PrepareScenarioAsync(
		ScenarioExecution execution,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(execution);
		if (execution.AdvanceBefore > TimeSpan.Zero)
			await AdvanceAndDrainAsync(checked((long)execution.AdvanceBefore.TotalMilliseconds), cancellationToken);
	}

	public async Task<SimulationRunResult> RunUntilAsync(
		Func<bool> completed,
		TimeSpan virtualTimeout,
		SimulationDriverOptions? options = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(completed);
		long timeoutMillis = checked((long)virtualTimeout.TotalMilliseconds);
		if (timeoutMillis < 0)
			throw new ArgumentOutOfRangeException(nameof(virtualTimeout));
		options ??= new SimulationDriverOptions();
		ValidateOptions(options);

		long virtualStart = clock.NowMillis;
		long virtualDeadline = checked(virtualStart + timeoutMillis);
		clock.ResetTaskTimings(); // Boot belongs to the process fixture, not to this scenario's budget/diagnostics.
		long wallStart = timeProvider.GetTimestamp();
		while (!completed())
		{
			cancellationToken.ThrowIfCancellationRequested();
			ThrowIfWallBudgetExceeded(options, wallStart, virtualStart);

			long? actionDue = actions.TryPeek(out _, out (long DueMillis, long Sequence) priority)
				? priority.DueMillis
				: null;
			long? timerDue = clock.NextDueMillis;
			long nextDue = Math.Min(actionDue ?? long.MaxValue, timerDue ?? long.MaxValue);
			if (nextDue == long.MaxValue || nextDue > virtualDeadline)
				nextDue = virtualDeadline;
			if (nextDue < clock.NowMillis)
				throw new InvalidOperationException($"Simulation event deadline {nextDue}ms is behind virtual time {clock.NowMillis}ms.");

			await AdvanceAndDrainAsync(nextDue - clock.NowMillis, cancellationToken);
			await RunDueBotActionsAsync(cancellationToken);
			if (clock.NowMillis >= virtualDeadline && !completed())
			{
				throw new TimeoutException(
					$"Simulation timed out after {clock.NowMillis - virtualStart} virtual millisecond(s)." +
					FormatTopPeriodicTasks());
			}
		}

		return Complete(options, wallStart, virtualStart);
	}

	private async Task AdvanceAndDrainAsync(long byMillis, CancellationToken cancellationToken)
	{
		clock.Advance(TimeSpan.FromMilliseconds(byMillis));
		foreach (InProcessBotTransport transport in transports)
			await transport.DrainAsync(cancellationToken);
	}

	private async Task RunDueBotActionsAsync(CancellationToken cancellationToken)
	{
		while (actions.TryPeek(out ScheduledBotAction? next, out (long DueMillis, long Sequence) priority) &&
			priority.DueMillis <= clock.NowMillis)
		{
			actions.Dequeue();
			ValueTask pending = next.Callback(cancellationToken);
			if (!pending.IsCompletedSuccessfully)
				await pending;
		}
	}

	private SimulationRunResult Complete(SimulationDriverOptions options, long wallStart, long virtualStart)
	{
		TimeSpan wallElapsed = timeProvider.GetElapsedTime(wallStart);
		TimeSpan virtualElapsed = TimeSpan.FromMilliseconds(clock.NowMillis - virtualStart);
		if (wallElapsed > options.WallTimeBudget)
		{
			throw new SimulationBudgetExceededException(
				$"Simulation exceeded its {options.WallTimeBudget.TotalSeconds:F3}s scenario wall-time budget." +
				FormatTopPeriodicTasks(),
				wallElapsed,
				virtualElapsed,
				clock.GetTopPeriodicTaskTimings());
		}
		if (virtualElapsed > TimeSpan.Zero)
		{
			double virtualMinutes = virtualElapsed.TotalMinutes;
			TimeSpan normalizedCost = TimeSpan.FromSeconds(wallElapsed.TotalSeconds / virtualMinutes);
			if (normalizedCost > options.WallTimePerVirtualMinuteBudget)
			{
				throw new SimulationBudgetExceededException(
					$"Simulation cost {normalizedCost.TotalSeconds:F3}s per virtual minute exceeded the " +
					$"{options.WallTimePerVirtualMinuteBudget.TotalSeconds:F3}s P0-03 budget." + FormatTopPeriodicTasks(),
					wallElapsed,
					virtualElapsed,
					clock.GetTopPeriodicTaskTimings());
			}
		}
		return new SimulationRunResult(wallElapsed, virtualElapsed, clock.GetTopPeriodicTaskTimings());
	}

	private void ThrowIfWallBudgetExceeded(SimulationDriverOptions options, long wallStart, long virtualStart)
	{
		TimeSpan elapsed = timeProvider.GetElapsedTime(wallStart);
		if (elapsed <= options.WallTimeBudget)
			return;
		throw new SimulationBudgetExceededException(
			$"Simulation exceeded its {options.WallTimeBudget.TotalSeconds:F3}s scenario wall-time budget." +
			FormatTopPeriodicTasks(),
			elapsed,
			TimeSpan.FromMilliseconds(clock.NowMillis - virtualStart),
			clock.GetTopPeriodicTaskTimings());
	}

	private string FormatTopPeriodicTasks()
	{
		IReadOnlyList<VirtualTaskTiming> timings = clock.GetTopPeriodicTaskTimings();
		if (timings.Count == 0)
			return " No periodic callbacks have run.";
		return " Top periodic callbacks: " + string.Join(
			"; ",
			timings.Select(timing =>
				$"{timing.Name}={timing.TotalWallTime.TotalMilliseconds:F3}ms/{timing.Invocations} invocation(s)"));
	}

	private static void ValidateOptions(SimulationDriverOptions options)
	{
		if (options.WallTimeBudget <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(options), "Wall-time budget must be positive.");
		if (options.WallTimePerVirtualMinuteBudget <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(options), "Virtual-minute wall-time budget must be positive.");
	}

	private sealed record ScheduledBotAction(
		string Bot,
		string Action,
		long DueMillis,
		Func<CancellationToken, ValueTask> Callback);
}

public sealed record SimulationDriverOptions
{
	public TimeSpan WallTimeBudget { get; init; } = SimulationDriver.DefaultWallTimeBudget;
	public TimeSpan WallTimePerVirtualMinuteBudget { get; init; } = SimulationDriver.P003WallTimePerVirtualMinuteBudget;
}

public sealed record SimulationRunResult(
	TimeSpan WallElapsed,
	TimeSpan VirtualElapsed,
	IReadOnlyList<VirtualTaskTiming> TopPeriodicTasks);

public sealed class SimulationBudgetExceededException : Exception
{
	public SimulationBudgetExceededException(
		string message,
		TimeSpan wallElapsed,
		TimeSpan virtualElapsed,
		IReadOnlyList<VirtualTaskTiming> topPeriodicTasks)
		: base(message)
	{
		WallElapsed = wallElapsed;
		VirtualElapsed = virtualElapsed;
		TopPeriodicTasks = topPeriodicTasks;
	}

	public TimeSpan WallElapsed { get; }
	public TimeSpan VirtualElapsed { get; }
	public IReadOnlyList<VirtualTaskTiming> TopPeriodicTasks { get; }
}
