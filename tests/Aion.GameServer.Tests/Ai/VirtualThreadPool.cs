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
	private readonly List<Entry> _entries = new();
	private readonly List<VirtualThreadPoolFault> _faults = new();
	private long _nowMillis;
	private long _sequence;

	public VirtualThreadPool(bool strict = false)
		: base(NullLogger<ThreadPoolManager>.Instance)
	{
		Strict = strict;
	}

	/// <summary>Current virtual time, in milliseconds since the harness started.</summary>
	public long NowMillis => _nowMillis;

	/// <summary>When enabled, disposing the clock fails if any scheduled body faulted.</summary>
	public bool Strict { get; set; }

	public IReadOnlyList<VirtualThreadPoolFault> Faults => _faults;

	public override ScheduledTask Schedule(
		Func<CancellationToken, ValueTask> action,
		TimeSpan delay,
		CancellationToken cancellationToken = default)
	{
		// One-shot: the handle IS the body, so running it flips IsDone() exactly like the real pool.
		long dueMillis = _nowMillis + ToMillis(delay);
		ScheduledTask handle = Deferred(() => Run(action), ToVirtualTime(dueMillis));
		_entries.Add(new Entry(dueMillis, null, handle, action, _sequence++));
		return handle;
	}

	public override ScheduledTask ScheduleAtFixedRateTask(
		Func<CancellationToken, ValueTask> action,
		TimeSpan initialDelay,
		TimeSpan period,
		CancellationToken cancellationToken = default)
	{
		// Repeating: the handle's own body stays unrun forever so IsDone() reports false for the life of the
		// timer (a repeating pool task is never "done"); cancellation is observed through IsCancelled instead.
		long dueMillis = _nowMillis + ToMillis(initialDelay);
		ScheduledTask handle = Deferred(() => { }, ToVirtualTime(dueMillis));
		_entries.Add(new Entry(dueMillis, ToMillis(period), handle, action, _sequence++));
		return handle;
	}

	/// <summary>Runs every timer body due within <paramref name="by"/>, in due order, then parks the clock at the end.</summary>
	public void Advance(TimeSpan by)
	{
		long target = _nowMillis + ToMillis(by);
		int ticks = 0;
		while (true)
		{
			_entries.RemoveAll(e => e.Handle.IsCancelled);
			Entry? next = null;
			foreach (Entry candidate in _entries)
			{
				if (candidate.DueMillis > target)
					continue;
				if (next == null || candidate.DueMillis < next.DueMillis
					|| (candidate.DueMillis == next.DueMillis && candidate.Sequence < next.Sequence))
					next = candidate;
			}

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
				_entries.Remove(next);
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

	/// <summary>Number of timers still armed (one-shots not yet fired plus live repeating timers).</summary>
	public int ArmedTimerCount
	{
		get
		{
			_entries.RemoveAll(e => e.Handle.IsCancelled);
			return _entries.Count;
		}
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
