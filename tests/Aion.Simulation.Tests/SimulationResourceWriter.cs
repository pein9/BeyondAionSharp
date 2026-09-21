using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

/// <summary>Boundary samples only: no background thread, forced GC or additional virtual timer.</summary>
public sealed class SimulationResourceWriter : IDisposable
{
	public const string Sampling = "run-policy-and-bot-action-boundaries";
	public const string Scope = "post-bootstrap-test-process";
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
	private readonly StreamWriter output;
	private readonly string run;
	private readonly VirtualThreadPool clock;
	private readonly Func<SimulationProcessResources> capture;
	private readonly TimeProvider time;
	private readonly long started;
	private int attempts;
	private int samples;
	private int errors;
	private bool disposed;

	public SimulationResourceWriter(string directory, string run, int seed, string gitSha, string profile,
		VirtualThreadPool clock, Func<SimulationProcessResources>? capture = null, TimeProvider? time = null)
	{
		this.run = run;
		this.clock = clock;
		this.capture = capture ?? CaptureProcess;
		this.time = time ?? TimeProvider.System;
		started = this.time.GetTimestamp();
		Directory.CreateDirectory(directory);
		output = new StreamWriter(new FileStream(Path.Combine(directory, "sim-resources.jsonl"), FileMode.CreateNew,
			FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
		try
		{
			Write(new { schemaVersion = 1, @event = "run-started", run, mode = "SIM", seed, gitSha, profile,
				processId = Environment.ProcessId, startedUtc = this.time.GetUtcNow(), sampling = Sampling, scope = Scope,
				timerSource = "VirtualThreadPool.ArmedTimerCount", heartbeat = "not-applicable" });
			Sample("run-started");
		}
		catch { output.Dispose(); throw; }
	}

	public void Sample(string trigger, string? scenario = null, long? policy = null,
		string? bot = null, string? account = null, string? step = null)
	{
		ObjectDisposedException.ThrowIf(disposed, this);
		long sequence = ++attempts;
		DateTimeOffset timestampUtc = time.GetUtcNow();
		double elapsedSeconds = time.GetElapsedTime(started).TotalSeconds;
		long virtualMillis = clock.NowMillis;
		SimulationProcessResources? metrics = null;
		int? armedTimers = null;
		string? error = null;
		try
		{
			var snapshot = capture();
			if (snapshot.WorkingSetBytes <= 0 || snapshot.ProcessLifetimePeakWorkingSetBytes <= 0 ||
				snapshot.LastGcIndex < 0 || snapshot.LastGcHeapBytes < 0 ||
				(snapshot.LastGcIndex == 0) != (snapshot.LastGcHeapBytes == null))
				throw new InvalidDataException("Invalid SIM process resource counters.");
			armedTimers = clock.ArmedTimerCount;
			metrics = snapshot;
			samples++;
		}
		catch (Exception failure)
		{
			// Preserve a failed observation, never replace missing metrics with zeros.
			error = failure.ToString();
			errors++;
		}
		Write(new { @event = error == null ? "sample" : "sample-error", run, sequence, timestampUtc, elapsedSeconds,
			virtualMillis, trigger, scenario, policy, bot, account, step, armedTimers, metrics, error });
	}

	public static SimulationProcessResources CaptureProcess()
	{
		using var process = Process.GetCurrentProcess();
		process.Refresh();
		// Same last-natural-collection semantics as LIVE; no GetTotalMemory estimate or forced collection.
		var gc = GC.GetGCMemoryInfo();
		return new(process.WorkingSet64, process.PeakWorkingSet64, gc.Index == 0 ? null : gc.HeapSizeBytes, gc.Index);
	}

	private void Write(object value) => output.WriteLine(JsonSerializer.Serialize(value, JsonOptions));

	public void Dispose()
	{
		if (disposed) return;
		try
		{
			Sample("run-completed");
			Write(new { @event = "run-completed", run, attempts, samples, errors });
		}
		finally { disposed = true; output.Dispose(); }
	}
}

public sealed record SimulationProcessResources(long WorkingSetBytes, long ProcessLifetimePeakWorkingSetBytes,
	long? LastGcHeapBytes, long LastGcIndex);
