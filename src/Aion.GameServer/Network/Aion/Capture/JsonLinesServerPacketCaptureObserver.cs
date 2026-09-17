using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Aion.Commons.Nio;

namespace Aion.GameServer.Network.Aion.Capture;

/// <summary>Copies clear server frames synchronously and writes them to a bounded asynchronous JSONL stream.</summary>
public sealed class JsonLinesServerPacketCaptureObserver : ServerPacketCaptureObserver, IAsyncDisposable
{
	private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
	private readonly Channel<CapturedPacket> channel;
	private readonly StreamWriter writer;
	private readonly Task writerTask;
	private readonly string? run;
	private readonly object captureLock = new();
	private long dropped;
	private int disposed;

	public JsonLinesServerPacketCaptureObserver(string path, string? run = null, int capacity = 4096)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
		var fullPath = Path.GetFullPath(path);
		Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
		writer = new StreamWriter(
			new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
			Utf8WithoutBom)
		{
			AutoFlush = true,
			NewLine = "\n",
		};
		this.run = run ?? Environment.GetEnvironmentVariable("AION_RUN_ID");
		channel = Channel.CreateBounded<CapturedPacket>(new BoundedChannelOptions(capacity)
		{
			SingleReader = true,
			SingleWriter = false,
			FullMode = BoundedChannelFullMode.Wait,
		});
		writerTask = WriteAllAsync();
	}

	public bool IsEnabled() => Volatile.Read(ref disposed) == 0;

	public void OnPacketSerialized(AionConnection con, AionServerPacket packet, ByteBuffer clearFrame)
	{
		ArgumentNullException.ThrowIfNull(con);
		ArgumentNullException.ThrowIfNull(packet);
		ArgumentNullException.ThrowIfNull(clearFrame);
		lock (captureLock)
		{
			if (!IsEnabled())
				return;

			// AionServerPacket encrypts the shared backing array immediately after this callback. Copy every byte,
			// including the two-byte frame length, before returning; the background writer sees only this owned array.
			var frame = GC.AllocateUninitializedArray<byte>(clearFrame.Limit());
			for (var index = 0; index < frame.Length; index++)
				frame[index] = clearFrame.Get(index);

			var account = con.GetAccount()?.GetName();
			var player = con.GetActivePlayer()?.GetName();
			var captured = new CapturedPacket(
				DateTimeOffset.UtcNow,
				account,
				player,
				packet.GetType().Name,
				packet.GetOpCode(),
				frame);
			if (!channel.Writer.TryWrite(captured))
				Interlocked.Increment(ref dropped);
		}
	}

	public async ValueTask DisposeAsync()
	{
		lock (captureLock)
		{
			if (Interlocked.Exchange(ref disposed, 1) != 0)
				return;
			channel.Writer.TryComplete();
		}
		await writerTask;
	}

	private async Task WriteAllAsync()
	{
		try
		{
			await foreach (var captured in channel.Reader.ReadAllAsync())
			{
				await WriteDroppedAsync();
				var line = JsonSerializer.Serialize(new
				{
					ts = captured.Timestamp.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture),
					run,
					account = captured.Account,
					player = captured.Player,
					packet = captured.Packet,
					opcode = $"0x{captured.Opcode:X3}",
					length = captured.Frame.Length,
					frameBase64 = Convert.ToBase64String(captured.Frame),
				});
				await writer.WriteLineAsync(line);
			}
			await WriteDroppedAsync();
		}
		finally
		{
			await writer.DisposeAsync();
		}
	}

	private async Task WriteDroppedAsync()
	{
		var count = Interlocked.Exchange(ref dropped, 0);
		if (count == 0)
			return;
		var line = JsonSerializer.Serialize(new
		{
			ts = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture),
			run,
			kind = "dropped",
			count,
		});
		await writer.WriteLineAsync(line);
	}

	private sealed record CapturedPacket(
		DateTimeOffset Timestamp,
		string? Account,
		string? Player,
		string Packet,
		int Opcode,
		byte[] Frame);
}
