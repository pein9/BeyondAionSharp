using System.Text.Json;
using Aion.Bots.Scenarios;

namespace Aion.GameServer.Tests;

public sealed class ScenarioRunJournalTests : IDisposable
{
	private readonly string directory = Path.Combine(Path.GetTempPath(), "aion-scenario-journal-" + Guid.NewGuid().ToString("N"));

	[Theory]
	[InlineData("SIM", 0, "passed")]
	[InlineData("LIVE", 0, "passed")]
	[InlineData("LIVE", 1, "failed")]
	[InlineData("LIVE", 7, "failed")]
	[InlineData("LIVE", -1, "failed")]
	public async Task OutcomesPreserveExitCodeIdentityAndMonotonicDuration(string mode, int exitCode, string status)
	{
		using var journal = new ScenarioRunJournal(directory, "run-a", mode);
		Assert.Equal(exitCode, await journal.ExecuteAsync("Q1", () => Task.FromResult(exitCode)));
		JsonElement[] rows = Read();
		Assert.Equal(2, rows.Length);
		Assert.All(rows, row =>
		{
			Assert.Equal(1, row.GetProperty("schemaVersion").GetInt32());
			Assert.Equal("run-a", row.GetProperty("run").GetString());
			Assert.Equal(mode, row.GetProperty("mode").GetString());
			Assert.Equal("Q1", row.GetProperty("scenario").GetString());
			Assert.Equal(TimeSpan.Zero, row.GetProperty("timestampUtc").GetDateTimeOffset().Offset);
		});
		Assert.Equal("started", rows[0].GetProperty("event").GetString());
		Assert.Equal("running", rows[0].GetProperty("status").GetString());
		Assert.Equal(JsonValueKind.Null, rows[0].GetProperty("durationSeconds").ValueKind);
		Assert.Equal("completed", rows[1].GetProperty("event").GetString());
		Assert.Equal(status, rows[1].GetProperty("status").GetString());
		Assert.Equal(exitCode, rows[1].GetProperty("exitCode").GetInt32());
		Assert.Equal(rows[0].GetProperty("startedUtc").GetDateTimeOffset(), rows[1].GetProperty("startedUtc").GetDateTimeOffset());
		Assert.True(double.IsFinite(rows[1].GetProperty("durationSeconds").GetDouble()));
		Assert.True(rows[1].GetProperty("durationSeconds").GetDouble() >= 0);
		Assert.Equal(exitCode == 0 ? JsonValueKind.Null : JsonValueKind.String, rows[1].GetProperty("error").ValueKind);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ExceptionsAndCancellationAreRecordedAndRethrownUnchanged(bool cancelled)
	{
		using var journal = new ScenarioRunJournal(directory, "run-a", "SIM");
		Exception expected = cancelled ? new OperationCanceledException("stopped") : new InvalidDataException("outer", new FormatException("inner"));
		Exception? actual = await Record.ExceptionAsync(() => journal.ExecuteAsync("C1", () => Task.FromException<int>(expected)));
		Assert.Same(expected, actual);
		JsonElement terminal = Read()[1];
		Assert.Equal("failed", terminal.GetProperty("status").GetString());
		string captured = terminal.GetProperty("error").GetString()!;
		Assert.StartsWith(expected.GetType().FullName + ": " + expected.Message, captured);
		Assert.Contains(nameof(ScenarioRunJournal.ExecuteAsync), captured);
		if (!cancelled) Assert.Contains("System.FormatException: inner", captured);
		Assert.Equal(JsonValueKind.Null, terminal.GetProperty("exitCode").ValueKind);
	}

	[Fact]
	public async Task StartIsFlushedBeforeAwaitAndCannotBeConfusedWithCompletion()
	{
		using var journal = new ScenarioRunJournal(directory, "run-a", "LIVE");
		var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
		Task<int> running = journal.ExecuteAsync("B4", () => release.Task);
		try
		{
			JsonElement start = Assert.Single(Read());
			Assert.Equal("running", start.GetProperty("status").GetString());
			Assert.False(running.IsCompleted);
			Assert.Throws<InvalidOperationException>(journal.Dispose);
			await Assert.ThrowsAsync<InvalidOperationException>(() => journal.ExecuteAsync("B3", () => Task.FromResult(0)));
			Assert.Single(Read());
		}
		finally { release.SetResult(0); await running; }
		Assert.Equal(2, Read().Length);
	}

	[Fact]
	public async Task SequentialScenariosAreSeparateAndNoAttemptCanBeOverwritten()
	{
		using (var journal = new ScenarioRunJournal(directory, "run-a", "SIM"))
		{
			await journal.ExecuteAsync("Q1", () => Task.FromResult(0));
			await journal.ExecuteAsync("Q2", () => Task.FromResult(1));
			await Assert.ThrowsAsync<InvalidOperationException>(() => journal.ExecuteAsync("Q1", () => Task.FromResult(0)));
		}
		Assert.Throws<IOException>(() => new ScenarioRunJournal(directory, "run-a", "SIM"));
		Assert.Equal(new[] { "Q1", "Q1", "Q2", "Q2" }, Read().Select(row => row.GetProperty("scenario").GetString()));
	}

	[Fact]
	public async Task DisposedOrInvalidInvocationDoesNotExecuteWork()
	{
		using var journal = new ScenarioRunJournal(directory, "run-a", "SIM");
		int calls = 0;
		Task<int> Work() { calls++; return Task.FromResult(0); }
		await Assert.ThrowsAsync<ArgumentException>(() => journal.ExecuteAsync(" ", Work));
		await Assert.ThrowsAsync<ArgumentNullException>(() => journal.ExecuteAsync("Q1", null!));
		journal.Dispose();
		await Assert.ThrowsAsync<ObjectDisposedException>(() => journal.ExecuteAsync("Q1", Work));
		Assert.Equal(0, calls);
		Assert.Empty(Read());
	}

	[Theory]
	[InlineData("", "SIM")]
	[InlineData("run-a", "sim")]
	[InlineData("run-a", "OTHER")]
	public void InvalidIdentityDoesNotCreateEvidence(string run, string mode)
	{
		Assert.Throws<ArgumentException>(() => new ScenarioRunJournal(directory, run, mode));
		Assert.False(Directory.Exists(directory));
	}

	private JsonElement[] Read()
	{
		using var stream = new FileStream(Path.Combine(directory, ScenarioRunJournal.FileName), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		using var reader = new StreamReader(stream);
		return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line =>
		{
			using var document = JsonDocument.Parse(line);
			return document.RootElement.Clone();
		}).ToArray();
	}

	public void Dispose()
	{
		// Only this fixture creates and owns this unique temporary directory.
		if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
	}
}
