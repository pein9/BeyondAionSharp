using System.Text;
using System.Text.Json;

namespace Aion.LiveBots;

public sealed class LiveBotProblemWriter : IAsyncDisposable
{
	private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
	private readonly StreamWriter writer;
	private readonly SemaphoreSlim writeLock = new(1, 1);
	private readonly System.Collections.Concurrent.ConcurrentQueue<JsonElement> records = new();
	public JsonElement[] Snapshot() => records.ToArray();

	public LiveBotProblemWriter(string path)
	{
		var fullPath = Path.GetFullPath(path);
		Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
		writer = new StreamWriter(new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), Utf8WithoutBom)
		{
			NewLine = "\n",
		};
	}

	public async Task WriteAsync(
		string run,
		string bot,
		string account,
		string step,
		string kind,
		string message,
		Exception? exception = null)
	{
		var record = new
		{
			ts = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
			run,
			bot,
			account,
			step,
			kind,
			msg = message,
			exType = exception?.GetType().FullName,
			stack = exception?.ToString(),
		};
		var line = JsonSerializer.Serialize(record);
		records.Enqueue(JsonSerializer.SerializeToElement(record));
		await writeLock.WaitAsync();
		try
		{
			await writer.WriteLineAsync(line);
			await writer.FlushAsync();
		}
		finally
		{
			writeLock.Release();
		}
	}

	public async ValueTask DisposeAsync()
	{
		await writer.DisposeAsync();
		writeLock.Dispose();
	}
}
