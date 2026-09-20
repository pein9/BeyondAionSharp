using System.Text;
using System.Text.Json;
using Aion.LogWatch;

namespace Aion.Commons.Tests;

public sealed class LogWatchBoundedHistoryTests
{
	[Fact]
	public void TailPreservesSplitUtf8PartialLinesTruncationAndEarlyEnumerationStop()
	{
		using var files = new TraceFiles();
		string path = files.PathFor("tail");
		var tail = new FileTail(path);
		Assert.Empty(tail.ReadNewLines());
		byte[] value = Encoding.UTF8.GetBytes("first\r\n" + new string('x', 65530) + "é\nunfinished");
		File.WriteAllBytes(path, value[..^12]); // Split inside the UTF-8 character, before the final newline.
		Assert.Equal(["first"], tail.ReadNewLines());
		using (var output = File.Open(path, FileMode.Append)) output.Write(value.AsSpan(value.Length - 12));
		Assert.Equal([new string('x', 65530) + "é"], tail.ReadNewLines());
		File.AppendAllText(path, "-done\nnext\nlast\n");
		Assert.Equal("unfinished-done", tail.ReadNewLines().First());
		Assert.Equal(["next", "last"], tail.ReadNewLines());
		File.WriteAllText(path, "reset\n");
		Assert.Equal(["reset"], tail.ReadNewLines());
	}

	[Fact]
	public void TailSnapshotDefersConcurrentAppendsUntilTheNextPoll()
	{
		using var files = new TraceFiles();
		string path = files.PathFor("snapshot");
		File.WriteAllText(path, "one\ntwo\n");
		var tail = new FileTail(path);
		using (var snapshot = tail.ReadNewLines().GetEnumerator())
		{
			Assert.True(snapshot.MoveNext());
			Assert.Equal("one", snapshot.Current);
			File.AppendAllText(path, "three\n");
			Assert.True(snapshot.MoveNext());
			Assert.Equal("two", snapshot.Current);
			Assert.False(snapshot.MoveNext());
		}
		Assert.Equal(["three"], tail.ReadNewLines());
	}

	[Fact]
	public void OversizeRecordsEqualTimestampsAndLateRecordsFallBackWithoutChangingAttribution()
	{
		using var files = new TraceFiles();
		var history = new BotTraceHistory("test");
		var start = DateTimeOffset.UnixEpoch;
		BotStep first = files.Add(history, "one", "a", "first", start, new string('x', BotTraceHistory.CharactersPerAccount));
		for (int i = 0; i < 100; i++) files.Add(history, "one", "a", "same-" + i, start);
		files.Add(history, "two", "b", "other-account", start.AddSeconds(1));
		files.Add(history, "one", "a", "newest", start.AddSeconds(100));
		for (int i = 0; i < 100; i++) files.Add(history, "one", "a", "late-" + i, start.AddSeconds(-1));
		Assert.Equal(first, history.LatestBefore("a", start));
		Assert.Equal("newest", history.LatestBefore("a", start.AddSeconds(200))!.Step);
		Assert.Null(history.LatestBefore("missing", start));
		Assert.InRange(history.RetainedRecords, 0, 2 * BotTraceHistory.RecordsPerAccount);
		Assert.InRange(history.RetainedCharacters, 0, 2 * BotTraceHistory.CharactersPerAccount);
		var context = history.ContextBefore("a", start);
		Assert.Equal(50, context.Count);
		Assert.Equal("same-50", context[0].Step);
		Assert.Equal("same-99", context[^1].Step);
		Assert.All(context, step => Assert.Equal("a", step.Account));
		Assert.Equal("newest", history.ContextBefore(null, start.AddSeconds(200))[^1].Step);
	}

	[Fact]
	public async Task DockerLogBufferAppliesBackpressureWithoutDroppingLines()
	{
		var buffer = DockerFollowers.CreateBuffer();
		for (int i = 0; i < DockerFollowers.BufferedLineLimit; i++)
			Assert.True(buffer.Writer.TryWrite(new("log", "gs", i.ToString(), false)));
		var pending = buffer.Writer.WriteAsync(new("log", "gs", "last", false));
		Assert.False(pending.IsCompleted);
		Assert.Equal("0", (await buffer.Reader.ReadAsync()).Line);
		await pending.AsTask().WaitAsync(TimeSpan.FromSeconds(5));
		for (int i = 1; i < DockerFollowers.BufferedLineLimit; i++)
			Assert.Equal(i.ToString(), (await buffer.Reader.ReadAsync()).Line);
		Assert.Equal("last", (await buffer.Reader.ReadAsync()).Line);
		Assert.False(buffer.Reader.TryRead(out _));
	}

	private sealed class TraceFiles : IDisposable
	{
		private readonly string root = Directory.CreateTempSubdirectory("aion-trace-cache-").FullName;
		public string PathFor(string name) => Path.Combine(root, name + ".jsonl");
		public BotStep Add(BotTraceHistory history, string file, string account, string step, DateTimeOffset at, string data = "")
		{
			string line = JsonSerializer.Serialize(new { ts = at, run = "test", bot = file, account, step, dir = ">", packet = "CM_MOVE", fields = new { data } });
			File.AppendAllText(PathFor(file), line + "\n");
			var value = new BotStep(file, account, step, at, ">", "CM_MOVE", line);
			history.Observe(PathFor(file), value);
			return value;
		}
		public void Dispose() => Directory.Delete(root, recursive: true); // Exact temporary directory created above.
	}
}
