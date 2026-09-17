using Aion.Commons.Logging;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// A <see cref="ThreadPoolManager"/> whose clock is driven by the test rather than by wall time.
/// </summary>
/// <remarks>
/// Boss AIs express their fights as battle timers (<c>Schedule</c> / <c>ScheduleAtFixedRateTask</c>) with
/// 10–40 second periods. Verifying a rotation against the real pool would mean sleeping through the fight,
/// which is not viable in a test suite. This subclass instead queues every scheduled body on a virtual
/// timeline and runs it synchronously on the calling thread when <see cref="Advance"/> passes its due time,
/// so a two-minute encounter is exercised in microseconds and in a fully deterministic order.
/// <para>
/// The returned <see cref="ScheduledTask"/> handles are real ones, produced by the production
/// <see cref="ThreadPoolManager.Deferred"/> factory, so the cancellation contract AI classes rely on
/// (<c>IsDone()</c> before <c>Cancel(true)</c>) behaves exactly as it does against the real pool:
/// a one-shot handle completes once its body has run; a fixed-rate handle never completes until cancelled.
/// </para>
/// </remarks>
public sealed class VirtualThreadPool : ThreadPoolManager
{
	private const int MaxTicksPerAdvance = 100_000;
	private static readonly ILogger Log = AionLog.For(nameof(VirtualThreadPool));
	private readonly PriorityQueue<Entry, (long DueMillis, long Sequence)> _entries = new();
	private readonly object _queueGate = new();
	private readonly List<VirtualThreadPoolFault> _faults = new();
	private long _nowMillis;
	private long _sequence;
	private int _advanceOwnerThreadId;

	public VirtualThreadPool(bool strict = false)
		: base(NullLogger<ThreadPoolManager>.Instance)
	{
		Strict = strict;
	}

	/// <summary>Current virtual time, in milliseconds since the harness started.</summary>
	public long NowMillis => _nowMillis;

	public override bool IsDeterministic => true;

	/// <summary>When enabled, disposing the clock fails if any scheduled body faulted.</summary>
	public bool Strict { get; set; }

	public IReadOnlyList<VirtualThreadPoolFault> Faults => _faults;

	/// <summary>
	/// Runs one eager singleton constructor and all work it queued for virtual time zero. Both phases use wall
	/// time bounds because an <c>AbstractCronTask</c> constructor can wait forever on Java's shared semaphore,
	/// while a startup body can block inside the synchronous virtual drain.
	/// </summary>
	public async Task<T> InitializeAndDrainAsync<T>(string name, Func<T> factory, TimeSpan wallTimeTimeout)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(factory);
		if (wallTimeTimeout <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(wallTimeTimeout));

		int firstNewFault = _faults.Count;
		T value = await RunBoundedAsync(factory, name + " construction", wallTimeTimeout);
		await RunBoundedAsync(
			() =>
			{
				Advance(TimeSpan.Zero);
				return true;
			},
			name + " zero-delay drain",
			wallTimeTimeout);

		VirtualThreadPoolFault[] newFaults = _faults.Skip(firstNewFault).ToArray();
		if (newFaults.Length > 0)
		{
			throw new AggregateException(
				$"{name} initialization recorded {newFaults.Length} virtual scheduled-task fault(s).",
				newFaults.Select(fault => fault.Exception));
		}
		return value;
	}

	public override ScheduledTask Schedule(
		Func<CancellationToken, ValueTask> action,
		TimeSpan delay,
		CancellationToken cancellationToken = default)
	{
		lock (_queueGate)
		{
			AssertOwnerThreadWhileAdvancing();
			// One-shot: the handle IS the body, so running it flips IsDone() exactly like the real pool.
			long dueMillis = _nowMillis + ToMillis(delay);
			ScheduledTask handle = Deferred(() => Run(action), ToVirtualTime(dueMillis));
			EnqueueLocked(new Entry(dueMillis, null, handle, action, ++_sequence));
			return handle;
		}
	}

	public override ScheduledTask ScheduleAtFixedRateTask(
		Func<CancellationToken, ValueTask> action,
		TimeSpan initialDelay,
		TimeSpan period,
		CancellationToken cancellationToken = default)
	{
		lock (_queueGate)
		{
			AssertOwnerThreadWhileAdvancing();
			// Repeating: the handle's own body stays unrun forever so IsDone() reports false for the life of the
			// timer (a repeating pool task is never "done"); cancellation is observed through IsCancelled instead.
			long dueMillis = _nowMillis + ToMillis(initialDelay);
			ScheduledTask handle = Deferred(() => { }, ToVirtualTime(dueMillis));
			EnqueueLocked(new Entry(dueMillis, ToMillis(period), handle, action, ++_sequence));
			return handle;
		}
	}

	/// <summary>Runs every timer body due within <paramref name="by"/>, in due order, then parks the clock at the end.</summary>
	public void Advance(TimeSpan by)
	{
		long advanceMillis = ToMillis(by);
		if (advanceMillis < 0)
			throw new ArgumentOutOfRangeException(nameof(by), "Virtual time cannot move backwards.");

		int currentThreadId = Environment.CurrentManagedThreadId;
		lock (_queueGate)
		{
			int ownerThreadId = _advanceOwnerThreadId;
			if (ownerThreadId != 0)
			{
				string reason = ownerThreadId == currentThreadId ? "re-entrant" : $"owned by thread {ownerThreadId}";
				throw new InvalidOperationException($"VirtualThreadPool.Advance is {reason}; thread {currentThreadId} cannot advance it.");
			}
			_advanceOwnerThreadId = currentThreadId;
		}

		try
		{
			long target = checked(_nowMillis + advanceMillis);
			int ticks = 0;
			while (true)
			{
				Entry? next = DequeueDue(target);
				if (next == null)
				{
					_nowMillis = target;
					return;
				}
				if (ticks++ >= MaxTicksPerAdvance)
					throw new InvalidOperationException($"VirtualThreadPool exceeded {MaxTicksPerAdvance} timer ticks while advancing to {target}ms; clock remains at {_nowMillis}ms.");

				_nowMillis = next.DueMillis;
				if (next.PeriodMillis == null)
				{
					next.Handle.Run();
					try
					{
						next.Handle.Get();
					}
					catch (Exception exception)
					{
						RecordFault(ThreadPoolScheduleKind.Once, next.DueMillis, exception);
					}
				}
				else
				{
					next.DueMillis += next.PeriodMillis.Value <= 0 ? 1 : next.PeriodMillis.Value;
					next.Handle.SetDueTime(ToVirtualTime(next.DueMillis));
					Enqueue(next);
					try
					{
						Run(next.Action);
					}
					catch (Exception exception)
					{
						RecordFault(ThreadPoolScheduleKind.FixedRate, _nowMillis, exception);
					}
				}
			}
		}
		finally
		{
			lock (_queueGate)
				_advanceOwnerThreadId = 0;
		}
	}

	/// <summary>Number of timers still armed (one-shots not yet fired plus live repeating timers).</summary>
	public int ArmedTimerCount
	{
		get
		{
			lock (_queueGate)
			{
				Entry[] live = _entries.UnorderedItems.Select(item => item.Element).Where(entry => !entry.Handle.IsCancelled).ToArray();
				if (live.Length != _entries.Count)
				{
					_entries.Clear();
					foreach (Entry entry in live)
						_entries.Enqueue(entry, (entry.DueMillis, entry.Sequence));
				}
				return live.Length;
			}
		}
	}

	private void Enqueue(Entry entry)
	{
		lock (_queueGate)
			EnqueueLocked(entry);
	}

	private void EnqueueLocked(Entry entry) => _entries.Enqueue(entry, (entry.DueMillis, entry.Sequence));

	private Entry? DequeueDue(long target)
	{
		lock (_queueGate)
		{
			while (_entries.TryPeek(out Entry? candidate, out _))
			{
				if (candidate.Handle.IsCancelled)
				{
					_entries.Dequeue();
					continue;
				}
				if (candidate.DueMillis > target)
					return null;
				return _entries.Dequeue();
			}
			return null;
		}
	}

	private void AssertOwnerThreadWhileAdvancing()
	{
		int ownerThreadId = Volatile.Read(ref _advanceOwnerThreadId);
		int currentThreadId = Environment.CurrentManagedThreadId;
		if (ownerThreadId != 0 && ownerThreadId != currentThreadId)
			throw new InvalidOperationException($"VirtualThreadPool is advancing on owner thread {ownerThreadId}; thread {currentThreadId} cannot schedule work.");
	}

	private void Run(Func<CancellationToken, ValueTask> action)
	{
		// AI timer bodies are synchronous (`_ => { Something(); return ValueTask.CompletedTask; }`), so this
		// never actually blocks. Exceptions propagate to the test instead of being swallowed into a log line.
		ValueTask pending = action(CancellationToken.None);
		if (!pending.IsCompleted)
			pending.AsTask().GetAwaiter().GetResult();
		else
			pending.GetAwaiter().GetResult();
	}

	private void RecordFault(ThreadPoolScheduleKind kind, long dueMillis, Exception exception)
	{
		_faults.Add(new VirtualThreadPoolFault(kind, dueMillis, exception));
		Log.LogError(
			exception,
			"Virtual {TimerKind} timer failed at {DueMillis}ms",
			kind == ThreadPoolScheduleKind.FixedRate ? "fixed-rate" : "one-shot",
			dueMillis);
	}

	private static async Task<T> RunBoundedAsync<T>(Func<T> action, string operation, TimeSpan wallTimeTimeout)
	{
		try
		{
			return await Task.Run(action).WaitAsync(wallTimeTimeout);
		}
		catch (TimeoutException exception)
		{
			throw new TimeoutException($"{operation} exceeded the {wallTimeTimeout} wall-time limit.", exception);
		}
	}

	public override async ValueTask DisposeAsync()
	{
		await base.DisposeAsync();
		if (Strict && _faults.Count > 0)
		{
			throw new AggregateException(
				$"VirtualThreadPool recorded {_faults.Count} scheduled task fault(s).",
				_faults.Select(fault => fault.Exception));
		}
	}

	private static long ToMillis(TimeSpan span) => (long)span.TotalMilliseconds;

	private DateTimeOffset ToVirtualTime(long millis) =>
		SystemClock.UtcNow().AddMilliseconds(millis - _nowMillis);

	private sealed class Entry
	{
		public Entry(long dueMillis, long? periodMillis, ScheduledTask handle, Func<CancellationToken, ValueTask> action, long sequence)
		{
			DueMillis = dueMillis;
			PeriodMillis = periodMillis;
			Handle = handle;
			Action = action;
			Sequence = sequence;
		}

		public long DueMillis { get; set; }

		public long? PeriodMillis { get; }

		public ScheduledTask Handle { get; }

		public Func<CancellationToken, ValueTask> Action { get; }

		public long Sequence { get; }
	}
}

public sealed record VirtualThreadPoolFault(
	ThreadPoolScheduleKind Kind,
	long DueMillis,
	Exception Exception);
