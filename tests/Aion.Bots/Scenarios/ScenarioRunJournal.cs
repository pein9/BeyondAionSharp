using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Aion.Bots.Scenarios;

/// <summary>
/// Process-local scenario evidence, not an overall run verdict. Runner cleanup,
/// watcher and coverage gates must still pass after a scenario completes.
/// A start without a terminal record is incomplete, never a pass.
/// </summary>
public sealed class ScenarioRunJournal : IDisposable
{
	public const string FileName = "scenario-results.jsonl";
	private readonly StreamWriter writer;
	private readonly string run;
	private readonly string mode;
	private readonly HashSet<string> recorded = new(StringComparer.Ordinal);
	private readonly object gate = new();
	private bool active;
	private bool disposed;

	public ScenarioRunJournal(string directory, string run, string mode)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(directory);
		ArgumentException.ThrowIfNullOrWhiteSpace(run);
		if (mode is not ("SIM" or "LIVE")) throw new ArgumentException("Expected SIM or LIVE mode.", nameof(mode));
		this.run = run;
		this.mode = mode;
		Directory.CreateDirectory(directory);
		// Never concatenate attempts or overwrite earlier evidence in the same run.
		writer = new StreamWriter(new FileStream(Path.Combine(directory, FileName),
			FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
	}

	public async Task<int> ExecuteAsync(string scenario, Func<Task<int>> execute)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(scenario);
		ArgumentNullException.ThrowIfNull(execute);
		lock (gate)
		{
			ObjectDisposedException.ThrowIf(disposed, this);
			if (active) throw new InvalidOperationException("Scenario journal requires serial execution.");
			if (!recorded.Add(scenario)) throw new InvalidOperationException($"Scenario '{scenario}' was already attempted in this run.");
			active = true;
		}
		try
		{
			DateTimeOffset started = DateTimeOffset.UtcNow;
			long tick = Stopwatch.GetTimestamp();
			Write(scenario, "started", "running", started, null, null, null);
			int exitCode;
			try { exitCode = await execute(); }
			catch (Exception exception)
			{
				Write(scenario, "completed", "failed", started, Stopwatch.GetElapsedTime(tick).TotalSeconds,
					null, exception.ToString());
				throw;
			}
			Write(scenario, "completed", exitCode == 0 ? "passed" : "failed", started,
				Stopwatch.GetElapsedTime(tick).TotalSeconds, exitCode,
				exitCode == 0 ? null : $"Scenario returned exit code {exitCode}; see bot problems and console evidence.");
			return exitCode;
		}
		finally { lock (gate) active = false; }
	}

	private void Write(string scenario, string eventName, string status, DateTimeOffset started,
		double? durationSeconds, int? exitCode, string? error) => writer.WriteLine(JsonSerializer.Serialize(new
	{
		schemaVersion = 1, run, mode, scenario, @event = eventName, status,
		startedUtc = started, timestampUtc = DateTimeOffset.UtcNow, durationSeconds, exitCode, error,
	}));

	public void Dispose()
	{
		lock (gate)
		{
			if (active) throw new InvalidOperationException("Cannot dispose an active scenario journal.");
			if (disposed) return;
			disposed = true;
			writer.Dispose();
		}
	}
}
