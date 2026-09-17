using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Aion.Bots.Protocol;
using Aion.Bots.Transport;
using Aion.Commons.Nio;
using Aion.GameServer.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class TcpBotTransportTests
{
	[Fact]
	public async Task Transport_StreamsKeyAndRawKnownPacket_AndSendsEncryptedClientFrame()
	{
		using var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var endpoint = (IPEndPoint)listener.LocalEndpoint;
		var accept = listener.AcceptTcpClientAsync();
		await using var transport = await TcpBotTransport.ConnectAsync(endpoint);
		using var server = await accept;
		var serverStream = server.GetStream();
		var liveCrypt = new Crypt();
		var encodedKey = liveCrypt.EnableKey();

		var keyFrame = CreateServerFrame(typeof(SM_KEY), Int32Body(encodedKey));
		EncryptWithLiveCrypt(liveCrypt, keyFrame.AsSpan(2)); // SM_KEY remains plain but enables the live server crypt.
		await serverStream.WriteAsync(keyFrame.AsMemory(0, 1));
		await serverStream.WriteAsync(keyFrame.AsMemory(1));

		await using var packets = transport.ReceiveAsync().GetAsyncEnumerator();
		Assert.True(await packets.MoveNextAsync());
		Assert.Equal(typeof(SM_KEY), packets.Current.PacketType);
		Assert.Equal(encodedKey, packets.Current.Get<int>("encodedKey"));

		var pongFrame = CreateServerFrame(typeof(SM_PONG), new byte[] { 0, 0 });
		EncryptWithLiveCrypt(liveCrypt, pongFrame.AsSpan(2));
		await serverStream.WriteAsync(pongFrame);
		Assert.True(await packets.MoveNextAsync());
		Assert.Equal(typeof(SM_PONG), packets.Current.PacketType);
		Assert.Equal("0000", packets.Current.Get<string>("bodyHex"));

		var ping = GameClientPackets.Ping();
		var clientFrame = transport.Codec.EncodeClientFrame(ping, AionConnection.State.IN_GAME);
		await transport.SendAsync(clientFrame);
		var receivedFrame = await ReadFrameAsync(serverStream);
		var payload = receivedFrame[2..];
		Assert.True(DecryptWithLiveCrypt(liveCrypt, payload));
		Assert.Equal(
			GamePacketRegistry.Instance.GetClient(typeof(CM_PING)).Opcode,
			Crypt.DecodeClientPacketOpcode(BinaryPrimitives.ReadUInt16LittleEndian(payload)));

		await transport.CloseAsync();
		Assert.False(await packets.MoveNextAsync());
	}

	[Fact]
	public async Task Transport_ReportsRemoteEofAsUnexpectedDisconnect()
	{
		using var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var endpoint = (IPEndPoint)listener.LocalEndpoint;
		var accept = listener.AcceptTcpClientAsync();
		await using var transport = await TcpBotTransport.ConnectAsync(endpoint);
		using var server = await accept;
		await using var packets = transport.ReceiveAsync().GetAsyncEnumerator();

		server.Dispose();

		await Assert.ThrowsAsync<EndOfStreamException>(async () => await packets.MoveNextAsync());
	}

	private static byte[] CreateServerFrame(Type packetType, ReadOnlySpan<byte> body)
	{
		var opcode = GamePacketRegistry.Instance.GetServer(packetType).Opcode;
		var frame = new byte[2 + 5 + body.Length];
		BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)frame.Length);
		var encodedOpcode = unchecked((ushort)Crypt.EncodeServerPacketOpcode(opcode));
		BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), encodedOpcode);
		frame[4] = Crypt.staticServerPacketCode;
		BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(5), unchecked((ushort)~encodedOpcode));
		body.CopyTo(frame.AsSpan(7));
		return frame;
	}

	private static byte[] Int32Body(int value)
	{
		var body = new byte[sizeof(int)];
		BinaryPrimitives.WriteInt32LittleEndian(body, value);
		return body;
	}

	private static void EncryptWithLiveCrypt(Crypt crypt, Span<byte> payload)
	{
		var data = payload.ToArray();
		var buffer = ByteBuffer.Wrap(data).Order(ByteOrder.LITTLE_ENDIAN);
		crypt.Encrypt(buffer);
		data.CopyTo(payload);
	}

	private static bool DecryptWithLiveCrypt(Crypt crypt, byte[] payload)
	{
		var buffer = ByteBuffer.Wrap(payload).Order(ByteOrder.LITTLE_ENDIAN);
		return crypt.Decrypt(buffer);
	}

	private static async Task<byte[]> ReadFrameAsync(NetworkStream stream)
	{
		var header = await ReadExactAsync(stream, sizeof(ushort));
		var length = BinaryPrimitives.ReadUInt16LittleEndian(header);
		var frame = new byte[length];
		header.CopyTo(frame, 0);
		(await ReadExactAsync(stream, length - sizeof(ushort))).CopyTo(frame, sizeof(ushort));
		return frame;
	}

	private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length)
	{
		var bytes = new byte[length];
		var offset = 0;
		while (offset < length)
		{
			var read = await stream.ReadAsync(bytes.AsMemory(offset));
			if (read == 0)
				throw new EndOfStreamException();
			offset += read;
		}
		return bytes;
	}
}
