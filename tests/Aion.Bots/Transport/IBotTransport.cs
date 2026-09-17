using Aion.Bots.Protocol;

namespace Aion.Bots.Transport;

/// <summary>
/// Mode-independent boundary between the bot protocol stack and a game-server connection.
/// Implementations arrive separately for real TCP and the in-process simulation host.
/// </summary>
public interface IBotTransport : IAsyncDisposable
{
	/// <summary>
	/// Sends one complete, framed and client-encrypted CM packet. The memory may be reused after the returned
	/// operation completes.
	/// </summary>
	ValueTask SendAsync(ReadOnlyMemory<byte> clientFrame, CancellationToken cancellationToken = default);

	/// <summary>
	/// Streams fully framed, decrypted and decoded server packets in wire order. The stream completes when the
	/// connection closes and propagates protocol or transport failures.
	/// </summary>
	IAsyncEnumerable<DecodedBotServerPacket> ReceiveAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Gracefully closes the transport after the bot has sent any protocol-level quit packet it requires.
	/// Does not synthesize CM_QUIT.
	/// </summary>
	ValueTask CloseAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Abruptly drops the connection without sending CM_QUIT or draining outbound packets, simulating a client
	/// process or network failure.
	/// </summary>
	ValueTask CrashAsync(CancellationToken cancellationToken = default);
}
