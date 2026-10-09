using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

/// <summary>
/// NR-44 (docs/natural-all-classes-ntc.md, Survey C1 part 4): several bots in one simulated world take turns on the
/// world's one virtual clock.
/// <para>
/// A bot alone moves the clock itself whenever it waits. Two bots cannot both do that: the first would play to its end
/// before the second moved. Here a bot's wait becomes "wake me at now + dt" and yields. When the bot whose turn it is
/// waits or ends, the table moves the clock to the earliest wake and lets that one bot play until its next wait. Bots
/// due at the same instant go in the order they were added.
/// </para>
/// <para>
/// Exactly one of them runs at any moment: the table, or the one bot whose turn it is. The table does nothing while a
/// bot plays, and a bot does nothing while it is parked. So what a world plays does not depend on the machine or its
/// load. The table's thread has no synchronization context: the server's own work, which the clock runs and waits for,
/// must be free to finish on any thread.
/// </para>
/// </summary>
internal sealed class SimulationTurnTable(VirtualThreadPool clock)
{
	private sealed class Seat(string bot, int order, long wakeAt, long? stopAt, Func<CancellationToken, Task> play)
	{
		public string Bot { get; } = bot;
		public int Order { get; } = order;
		public long WakeAt { get; set; } = wakeAt;
		public long? StopAt { get; } = stopAt;
		public Func<CancellationToken, Task> Play { get; } = play;
		public Task? Playing { get; set; }
		public TaskCompletionSource? Parked { get; set; }
		public bool Done => Playing is { IsCompleted: true };
	}

	private readonly List<Seat> seats = [];
	private readonly Dictionary<string, Seat> byBot = new(StringComparer.Ordinal);
	// Released once when the bot whose turn it is waits, and once when a bot ends.
	private readonly SemaphoreSlim turnOver = new(0);
	private volatile Seat? current;
	private int running;

	/// <summary>The bot whose turn it is, or null between turns.</summary>
	public string? CurrentBot => current?.Bot;

	/// <summary>
	/// Seats a bot. It starts <paramref name="startAfter"/> of game time after the round begins. With
	/// <paramref name="stopAfter"/> it is stopped on purpose at that game time: its next wait is cancelled.
	/// </summary>
	public void Add(string bot, TimeSpan startAfter, Func<CancellationToken, Task> play, TimeSpan? stopAfter = null)
	{
		if (running != 0) throw new InvalidOperationException("The round has begun; its bots are seated before it.");
		long now = clock.NowMillis;
		var seat = new Seat(bot, seats.Count, now + (long)startAfter.TotalMilliseconds,
			stopAfter is { } stop ? now + (long)stop.TotalMilliseconds : null, play);
		byBot.Add(bot, seat);
		seats.Add(seat);
	}

	/// <summary>
	/// The wait of the bot whose turn it is: it is due again after <paramref name="elapsed"/> of game time. Truncated to
	/// whole milliseconds as VirtualThreadPool.Advance truncates, so a bot's waits move the clock as they do alone.
	/// </summary>
	public ValueTask WaitAsync(string bot, TimeSpan elapsed, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		Seat seat = byBot[bot];
		if (!ReferenceEquals(seat, current))
			throw new InvalidOperationException($"Bot {bot} waited outside its turn (the turn is {current?.Bot ?? "nobody's"}).");
		if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed), "Virtual time cannot move backwards.");
		seat.WakeAt = checked(clock.NowMillis + (long)elapsed.TotalMilliseconds);
		// Continuations run inline: the bot plays on from where the table completes this.
		var parked = new TaskCompletionSource();
		seat.Parked = parked;
		turnOver.Release();
		return new ValueTask(parked.Task);
	}

	/// <summary>
	/// Plays the round until every bot has ended. A bot that throws ends alone; the others go on. Returns each bot's
	/// task, ended, in the order the bots were seated. Call it from a thread with no synchronization context.
	/// </summary>
	public IReadOnlyList<(string Bot, Task Played)> Run(CancellationToken token)
	{
		if (Interlocked.Exchange(ref running, 1) != 0) throw new InvalidOperationException("A turn table plays one round.");
		if (SynchronizationContext.Current != null)
			throw new InvalidOperationException("The round's thread must have no synchronization context.");
		while (true)
		{
			token.ThrowIfCancellationRequested();
			Seat? next = seats.Where(seat => !seat.Done).OrderBy(seat => seat.WakeAt).ThenBy(seat => seat.Order).FirstOrDefault();
			if (next == null) break;
			Exception? worldFault = null;
			long now = clock.NowMillis;
			if (next.WakeAt > now)
			{
				// The world's own work between turns. A fault of it reaches the bot that is due, as it reaches a bot
				// alone through its own wait.
				try { clock.Advance(TimeSpan.FromMilliseconds(next.WakeAt - now)); }
				catch (Exception fault) { worldFault = fault; }
			}
			bool stopped = next.StopAt is long stop && clock.NowMillis >= stop;
			current = next;
			if (next.Playing == null)
			{
				next.Playing = worldFault != null ? Task.FromException(worldFault)
					: stopped ? Task.FromException(new OperationCanceledException($"Bot {next.Bot} was stopped by its round before it began."))
					: Start(next, token);
				next.Playing.ContinueWith(_ => turnOver.Release(), CancellationToken.None,
					TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
			}
			else
			{
				TaskCompletionSource parked = next.Parked ?? throw new InvalidOperationException($"Bot {next.Bot} is due but did not wait.");
				next.Parked = null;
				if (worldFault != null) parked.SetException(worldFault);
				else if (stopped) parked.SetException(new OperationCanceledException($"Bot {next.Bot} was stopped by its round."));
				else parked.SetResult();
			}
			// The bot plays, on this thread or, after a real wait of its own (a file write), on another. The table waits
			// here and touches nothing until that bot waits for its next turn or ends.
			turnOver.Wait(token);
			current = null;
		}
		return seats.Select(seat => (seat.Bot, seat.Playing!)).ToArray();
	}

	private static Task Start(Seat seat, CancellationToken token)
	{
		try { return seat.Play(token); }
		catch (Exception thrownBeforeItsFirstWait) { return Task.FromException(thrownBeforeItsFirstWait); }
	}
}
