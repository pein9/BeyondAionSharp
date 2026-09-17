using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Aion.Bots.Protocol;
using Aion.Commons.Concurrent;
using Aion.Commons.Lang;
using Aion.Commons.Nio;
using Aion.GameServer.Network.Aion;

namespace Aion.Bots.Transport;

/// <summary>
/// Deterministic socketless game transport. Client frames still traverse the faithful decrypt, header validation,
/// packet factory, read and run path; server packets still traverse their faithful serializer and crypt path.
/// </summary>
public sealed class InProcessBotTransport : IBotTransport
{
	private const int MinimumGameFrameLength = 7;

	private readonly InProcessAionConnection connection;
	private readonly BotServerPacketDecoder decoder;
	private readonly Action<TimeSpan>? advanceClock;
	private readonly Channel<DecodedBotServerPacket> received = Channel.CreateUnbounded<DecodedBotServerPacket>(
		new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
	private readonly object gate = new();
	private int closeMode;
	private int receiveStarted;

	public InProcessBotTransport(
		Action<TimeSpan>? advanceClock = null,
		GamePacketCodec? codec = null,
		BotServerPacketDecoder? decoder = null,
		string ip = "127.0.0.1")
	{
		this.advanceClock = advanceClock;
		Codec = codec ?? new GamePacketCodec();
		this.decoder = decoder ?? new BotServerPacketDecoder();
		connection = new InProcessAionConnection(ip);
		connection.Initialize();
		DrainServerPackets();
	}

	public GamePacketCodec Codec { get; }

	internal AionConnection ServerConnection => connection;

	public ValueTask SendAsync(ReadOnlyMemory<byte> clientFrame, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		lock (gate)
		{
			ThrowIfClosed();
			try
			{
				ProcessClientFrame(clientFrame.Span);
				DrainServerPackets();
				CompleteIfDisconnected();
			}
			catch (Exception ex)
			{
				Fail(ex);
				throw;
			}
		}
		return ValueTask.CompletedTask;
	}

	/// <summary>Advances the injected virtual clock and serializes every server packet it made observable.</summary>
	public ValueTask AdvanceAsync(TimeSpan elapsed, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		lock (gate)
		{
			ThrowIfClosed();
			if (advanceClock == null)
				throw new InvalidOperationException("This in-process transport has no virtual-clock advance callback.");
			try
			{
				advanceClock(elapsed);
				DrainServerPackets();
				CompleteIfDisconnected();
			}
			catch (Exception ex)
			{
				Fail(ex);
				throw;
			}
		}
		return ValueTask.CompletedTask;
	}

	public async IAsyncEnumerable<DecodedBotServerPacket> ReceiveAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		if (Interlocked.Exchange(ref receiveStarted, 1) != 0)
			throw new InvalidOperationException("An in-process bot transport supports one receive loop.");

		await foreach (DecodedBotServerPacket packet in received.Reader.ReadAllAsync(cancellationToken))
			yield return packet;
	}

	public ValueTask CloseAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		lock (gate)
		{
			if (closeMode != 0)
				return ValueTask.CompletedTask;
			closeMode = 1;
			try
			{
				DrainServerPackets();
				connection.Close();
			}
			finally
			{
				received.Writer.TryComplete();
			}
		}
		return ValueTask.CompletedTask;
	}

	public ValueTask CrashAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		lock (gate)
		{
			if (closeMode != 0)
				return ValueTask.CompletedTask;
			closeMode = 2;
			try
			{
				connection.Crash();
			}
			finally
			{
				received.Writer.TryComplete();
			}
		}
		return ValueTask.CompletedTask;
	}

	public async ValueTask DisposeAsync()
	{
		await CloseAsync();
	}

	private void ProcessClientFrame(ReadOnlySpan<byte> frame)
	{
		if (frame.Length < MinimumGameFrameLength)
			throw new InvalidDataException($"Invalid game frame length {frame.Length}.");
		int declaredLength = BinaryPrimitives.ReadUInt16LittleEndian(frame);
		if (declaredLength != frame.Length)
			throw new InvalidDataException($"Game frame length mismatch: declared {declaredLength}, actual {frame.Length}.");

		ByteBuffer payload = ByteBuffer.Wrap(frame[sizeof(ushort)..].ToArray()).Order(ByteOrder.LITTLE_ENDIAN);
		if (!connection.ProcessDataInternal(payload))
			throw new InvalidDataException("The game server rejected the client frame.");
		if (payload.Position() == 0 && payload.HasRemaining())
			throw new InvalidDataException("The game server rejected the encrypted client-packet header.");
		if (payload.HasRemaining())
			throw new InvalidDataException($"The game server left {payload.Remaining()} unread client-frame byte(s).");
	}

	private void DrainServerPackets()
	{
		while (true)
		{
			ByteBuffer buffer = connection.writeBuffer;
			buffer.Clear();
			if (!connection.WriteDataInternal(buffer))
			{
				buffer.SetLimit(0);
				return;
			}

			byte[] frame = buffer.Array().AsSpan(buffer.ArrayOffset(), buffer.Limit()).ToArray();
			DecodedGamePacket gamePacket = Codec.HasKey
				? Codec.DecodeServerFrame(frame)
				: Codec.RecoverKeyFromSmKeyFrame(frame);
			DecodedBotServerPacket packet = decoder.DecodeOrRaw(gamePacket);
			if (!received.Writer.TryWrite(packet))
				throw new InvalidOperationException("The in-process receive stream is already closed.");
		}
	}

	private void CompleteIfDisconnected()
	{
		if (!connection.IsClosed())
			return;
		closeMode = 1;
		received.Writer.TryComplete();
	}

	private void Fail(Exception exception)
	{
		closeMode = 3;
		try
		{
			connection.Crash();
		}
		catch
		{
			// Preserve the protocol/serialization failure that caused teardown.
		}
		received.Writer.TryComplete(exception);
	}

	private void ThrowIfClosed() => ObjectDisposedException.ThrowIf(closeMode != 0, this);

	private sealed class InProcessAionConnection(string ip) : AionConnection(ip)
	{
		public void Initialize() => InitializedInternal();

		public void Crash()
		{
			ClearPendingPackets();
			Disconnect(InlineExecutor.Instance);
		}

		protected override void ExecutePacket(AionClientPacket packet) => packet.Run();
	}

	private sealed class InlineExecutor : Executor
	{
		public static InlineExecutor Instance { get; } = new();

		public void Execute(Runnable command) => command.Run();
	}
}
