using System.Text.Json;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed class SimulationResourceWriterTests : IDisposable
{
	private readonly string directory = Path.Combine(Path.GetTempPath(), "aion-sim-resources-" + Guid.NewGuid().ToString("N"));
	private static SimulationProcessResources Valid() => new(100, 200, null, 0);
	private SimulationResourceWriter Writer(VirtualThreadPool clock, Func<SimulationProcessResources>? capture = null, TimeProvider? time = null) =>
		new(directory, "resource-test", 19, "source", "sim-fast", clock, capture ?? Valid, time);
	private JsonElement[] Rows()
	{
		using var stream = new FileStream(Path.Combine(directory, "sim-resources.jsonl"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		using var reader = new StreamReader(stream);
		var rows = new List<JsonElement>();
		while (reader.ReadLine() is { } line) rows.Add(JsonSerializer.Deserialize<JsonElement>(line));
		return rows.ToArray();
	}

	[Fact]
	public async Task SamplesAreFlushedSequencedAndUseWallAndVirtualCoordinatesWithoutAddingTimers()
	{
		await using var clock = new VirtualThreadPool(strict: true);
		var time = new ManualTime();
		int calls = 0;
		clock.Schedule(_ => ValueTask.CompletedTask, TimeSpan.FromSeconds(10));
		using (var writer = Writer(clock, () => { calls++; return Valid(); }, time))
		{
			Assert.Equal(1, clock.ArmedTimerCount);
			Assert.Equal(0, clock.NowMillis);
			time.Ticks = TimeSpan.TicksPerSecond * 2;
			clock.Advance(TimeSpan.FromSeconds(3));
			writer.Sample("policy-started", "S0", 1);
			Assert.Equal(3, Rows().Length);
			Assert.Equal(2, calls);
		}
		Assert.Equal(3, calls);
		Assert.Equal(3000, clock.NowMillis);
		Assert.Equal(1, clock.ArmedTimerCount);
		var rows = Rows();
		Assert.Equal(SimulationResourceWriter.Sampling, rows[0].GetProperty("sampling").GetString());
		Assert.Equal(Environment.ProcessId, rows[0].GetProperty("processId").GetInt32());
		Assert.Equal(2, rows[2].GetProperty("elapsedSeconds").GetDouble());
		Assert.Equal(3000, rows[2].GetProperty("virtualMillis").GetInt64());
		Assert.Equal(1, rows[2].GetProperty("armedTimers").GetInt32());
		Assert.Equal(3, rows[^1].GetProperty("attempts").GetInt32());
		Assert.Equal(0, rows[^1].GetProperty("errors").GetInt32());
		Assert.Equal(new[] { 1, 2, 3 }, rows.Where(r => r.GetProperty("event").GetString() == "sample").Select(r => r.GetProperty("sequence").GetInt32()));
	}

	[Fact]
	public async Task CollectionAvailabilityAndLifetimePeakAreNotConfusedWithCurrentMemory()
	{
		await using var clock = new VirtualThreadPool(strict: true);
		int calls = 0;
		using (var writer = Writer(clock, () => ++calls == 1 ? Valid() : new(150, 900, 73, 5))) { }
		var rows = Rows();
		Assert.Equal(JsonValueKind.Null, rows[1].GetProperty("metrics").GetProperty("lastGcHeapBytes").ValueKind);
		var last = rows[2].GetProperty("metrics");
		Assert.Equal(150, last.GetProperty("workingSetBytes").GetInt64());
		Assert.Equal(900, last.GetProperty("processLifetimePeakWorkingSetBytes").GetInt64());
		Assert.Equal(73, last.GetProperty("lastGcHeapBytes").GetInt64());
		Assert.Equal(5, last.GetProperty("lastGcIndex").GetInt64());
	}

	[Fact]
	public async Task FailedCaptureIsRetainedWithNoFabricatedCountersAndCannotBeErasedByLaterSamples()
	{
		await using var clock = new VirtualThreadPool(strict: true);
		int calls = 0;
		using (var writer = Writer(clock, () => ++calls == 2 ? throw new IOException("counter unavailable") : Valid()))
			writer.Sample("bot-action", "S0", bot: "b01", account: "account", step: "s01");
		var rows = Rows();
		Assert.Equal("sample-error", rows[2].GetProperty("event").GetString());
		Assert.Equal(JsonValueKind.Null, rows[2].GetProperty("metrics").ValueKind);
		Assert.Equal(JsonValueKind.Null, rows[2].GetProperty("armedTimers").ValueKind);
		Assert.Contains("IOException: counter unavailable", rows[2].GetProperty("error").GetString());
		Assert.Equal(2, rows[^1].GetProperty("samples").GetInt32());
		Assert.Equal(1, rows[^1].GetProperty("errors").GetInt32());
	}

	[Theory]
	[InlineData(0, 200, null, 0)]
	[InlineData(100, -1, null, 0)]
	[InlineData(100, 200, null, 2)]
	[InlineData(100, 200, 100L, 0)]
	[InlineData(100, 200, -1L, 1)]
	[InlineData(100, 200, null, -1)]
	public async Task InvalidCountersBecomeFailedSamples(long workingSet, long peak, long? heap, long index)
	{
		await using var clock = new VirtualThreadPool(strict: true);
		using (var writer = Writer(clock, () => new(workingSet, peak, heap, index))) { }
		Assert.Equal(0, Rows()[^1].GetProperty("samples").GetInt32());
		Assert.Equal(2, Rows()[^1].GetProperty("errors").GetInt32());
	}

	[Fact]
	public async Task TimerSamplingExcludesCancellationAndPreservesDueOrder()
	{
		await using var clock = new VirtualThreadPool(strict: true);
		var fired = new List<int>();
		clock.Schedule(_ => { fired.Add(1); return ValueTask.CompletedTask; }, TimeSpan.FromSeconds(1));
		var cancelled = clock.Schedule(_ => { fired.Add(2); return ValueTask.CompletedTask; }, TimeSpan.FromSeconds(1));
		clock.Schedule(_ => { fired.Add(3); return ValueTask.CompletedTask; }, TimeSpan.FromSeconds(1));
		cancelled.Cancel(false);
		using (var writer = Writer(clock)) clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(new[] { 1, 3 }, fired);
		Assert.Equal(2, Rows()[1].GetProperty("armedTimers").GetInt32());
		Assert.Equal(0, Rows()[2].GetProperty("armedTimers").GetInt32());
	}

	[Fact]
	public async Task DisposeIsIdempotentAndExistingExportsAreNeverOverwritten()
	{
		await using var clock = new VirtualThreadPool(strict: true);
		using var writer = Writer(clock);
		writer.Dispose();
		int count = Rows().Length;
		writer.Dispose();
		Assert.Equal(count, Rows().Length);
		Assert.Throws<ObjectDisposedException>(() => writer.Sample("run-completed"));
		Assert.Throws<IOException>(() => Writer(clock));
		Assert.Equal(count, Rows().Length);
	}

	[Fact]
	public void RealProcessCaptureHasPositiveMemoryAndExplicitLastCollectionAvailability()
	{
		var snapshot = SimulationResourceWriter.CaptureProcess();
		Assert.True(snapshot.WorkingSetBytes > 0);
		Assert.True(snapshot.ProcessLifetimePeakWorkingSetBytes > 0);
		Assert.True(snapshot.LastGcIndex >= 0);
		Assert.Equal(snapshot.LastGcIndex == 0, snapshot.LastGcHeapBytes == null);
	}

	private sealed class ManualTime : TimeProvider
	{
		public long Ticks { get; set; }
		public override long TimestampFrequency => TimeSpan.TicksPerSecond;
		public override long GetTimestamp() => Ticks;
		public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-09-21T01:00:00Z").AddTicks(Ticks);
	}

	public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
}
