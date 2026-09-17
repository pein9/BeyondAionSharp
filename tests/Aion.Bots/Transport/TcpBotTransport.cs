using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Aion.Bots.Protocol;

namespace Aion.Bots.Transport;

/// <summary>Real-socket game transport with client-side framing, crypt and packet decoding.</summary>
public sealed class TcpBotTransport : IBotTransport
{
	private const int MinimumGameFrameLength = 7;

	private readonly TcpClient client;
	private readonly NetworkStream stream;
	private readonly BotServerPacketDecoder decoder;
	private readonly SemaphoreSlim sendLock = new(1, 1);
	private readonly CancellationTokenSource lifetime = new();
	private int closeMode;
	private int receiveStarted;

	private TcpBotTransport(TcpClient client, GamePacketCodec codec, BotServerPacketDecoder decoder)
	{
		this.client = client;
		stream = client.GetStream();
		Codec = codec;
		this.decoder = decoder;
	}

	public GamePacketCodec Codec { get; }

	public static async Task<TcpBotTransport> ConnectAsync(
		IPEndPoint endpoint,
		CancellationToken cancellationToken = default,
		GamePacketCodec? codec = null,
		BotServerPacketDecoder? decoder = null)
	{
		ArgumentNullException.ThrowIfNull(endpoint);
		var client = new TcpClient(endpoint.AddressFamily) { NoDelay = true };
		try
		{
			await client.ConnectAsync(endpoint.Address, endpoint.Port, cancellationToken);
			return new TcpBotTransport(client, codec ?? new GamePacketCodec(), decoder ?? new BotServerPacketDecoder());
		}
		catch
		{
			client.Dispose();
			throw;
		}
	}

	public async ValueTask SendAsync(ReadOnlyMemory<byte> clientFrame, CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(Volatile.Read(ref closeMode) != 0, this);
		await sendLock.WaitAsync(cancellationToken);
		try
		{
			ObjectDisposedException.ThrowIf(Volatile.Read(ref closeMode) != 0, this);
			await stream.WriteAsync(clientFrame, cancellationToken);
			await stream.FlushAsync(cancellationToken);
		}
		finally
		{
			sendLock.Release();
		}
	}

	public async IAsyncEnumerable<DecodedBotServerPacket> ReceiveAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		if (Interlocked.Exchange(ref receiveStarted, 1) != 0)
			throw new InvalidOperationException("A TCP bot transport supports one receive loop.");

		using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
		while (true)
		{
			byte[]? frame;
			try
			{
				frame = await ReadFrameOrNullAsync(linked.Token);
			}
			catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
			{
				yield break;
			}
			catch (Exception ex) when (Volatile.Read(ref closeMode) != 0 &&
				ex is ObjectDisposedException or IOException or SocketException)
			{
				yield break;
			}

			if (frame == null)
			{
				if (Volatile.Read(ref closeMode) != 0)
					yield break;
				throw new EndOfStreamException("Game server closed the bot connection unexpectedly.");
			}

			var packet = Codec.HasKey
				? Codec.DecodeServerFrame(frame)
				: Codec.RecoverKeyFromSmKeyFrame(frame);
			yield return decoder.DecodeOrRaw(packet);
		}
	}

	public ValueTask CloseAsync(CancellationToken cancellationToken = default)
	{
		Close(graceful: true);
		return ValueTask.CompletedTask;
	}

	public ValueTask CrashAsync(CancellationToken cancellationToken = default)
	{
		Close(graceful: false);
		return ValueTask.CompletedTask;
	}

	public ValueTask DisposeAsync()
	{
		Close(graceful: true);
		sendLock.Dispose();
		lifetime.Dispose();
		return ValueTask.CompletedTask;
	}

	private async Task<byte[]?> ReadFrameOrNullAsync(CancellationToken cancellationToken)
	{
		var header = new byte[sizeof(ushort)];
		var firstRead = await stream.ReadAsync(header, cancellationToken);
		if (firstRead == 0)
			return null;
		if (firstRead == 1)
			await ReadExactAsync(header.AsMemory(1), cancellationToken);

		var frameLength = BinaryPrimitives.ReadUInt16LittleEndian(header);
		if (frameLength < MinimumGameFrameLength)
			throw new InvalidDataException($"Invalid game frame length {frameLength}.");
		var frame = new byte[frameLength];
		header.CopyTo(frame, 0);
		await ReadExactAsync(frame.AsMemory(sizeof(ushort)), cancellationToken);
		return frame;
	}

	private async Task ReadExactAsync(Memory<byte> destination, CancellationToken cancellationToken)
	{
		var offset = 0;
		while (offset < destination.Length)
		{
			var read = await stream.ReadAsync(destination[offset..], cancellationToken);
			if (read == 0)
				throw new EndOfStreamException("Game server closed the socket in the middle of a frame.");
			offset += read;
		}
	}

	private void Close(bool graceful)
	{
		if (Interlocked.CompareExchange(ref closeMode, graceful ? 1 : 2, 0) != 0)
			return;

		try
		{
			if (!graceful)
				client.LingerState = new LingerOption(enable: true, seconds: 0);
			client.Client.Shutdown(SocketShutdown.Both);
		}
		catch (SocketException)
		{
		}
		finally
		{
			lifetime.Cancel();
			client.Dispose();
		}
	}
}
