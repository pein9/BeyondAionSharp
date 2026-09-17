using System.Buffers.Binary;
using System.Reflection;
using Aion.Bots.Protocol;
using Aion.Commons.Nio;
using Aion.GameServer.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotGamePacketCodecTests
{
	[Fact]
	public void BotCodecInteroperatesWithLiveCryptAcrossRotatingPackets()
	{
		var liveCrypt = new Crypt();
		var encodedKey = liveCrypt.EnableKey();
		var liveKeyPair = GetLiveKeyPair(liveCrypt);
		var baseKey = liveKeyPair.GetBaseKey();
		var keyOpcode = ServerPacketsOpcodes.GetOpcode(typeof(SM_KEY));
		var keyFrame = CreateServerFrame(keyOpcode, Int32Body(encodedKey));
		var keyPayloadBeforeEnable = keyFrame[2..].ToArray();

		EncryptWithLiveCrypt(liveCrypt, keyFrame.AsSpan(2));

		Assert.Equal(keyPayloadBeforeEnable, keyFrame[2..]);
		var botCodec = new GamePacketCodec();
		var keyPacket = botCodec.RecoverKeyFromSmKeyFrame(keyFrame);
		Assert.Equal(keyOpcode, keyPacket.Opcode);
		Assert.Equal(baseKey, botCodec.BaseKey);

		foreach (var (opcode, body) in new[]
		{
			(142, new byte[] { 0, 0 }),
			(99, new byte[] { 1, 2, 3, 4, 5, 6 }),
		})
		{
			var serverFrame = CreateServerFrame(opcode, body);
			EncryptWithLiveCrypt(liveCrypt, serverFrame.AsSpan(2));
			var decoded = botCodec.DecodeServerFrame(serverFrame);
			Assert.Equal(opcode, decoded.Opcode);
			Assert.Equal(body, decoded.Body);
		}

		foreach (var (opcode, body) in new[]
		{
			(37, new byte[] { 0x89, 0x13, 0, 0, 0 }),
			(236, new byte[] { 1, 0xaa, 0xbb }),
		})
		{
			var clientFrame = botCodec.EncodeClientFrame(opcode, body);
			Assert.Equal(clientFrame.Length, BinaryPrimitives.ReadUInt16LittleEndian(clientFrame));
			var decryptedPayload = clientFrame[2..].ToArray();
			Assert.True(DecryptWithLiveCrypt(liveCrypt, decryptedPayload));
			var encodedOpcode = BinaryPrimitives.ReadUInt16LittleEndian(decryptedPayload);
			Assert.Equal(opcode, Crypt.DecodeClientPacketOpcode(encodedOpcode));
			Assert.Equal(GamePacketCodec.ClientPacketCode, decryptedPayload[2]);
			Assert.Equal(unchecked((ushort)~encodedOpcode), BinaryPrimitives.ReadUInt16LittleEndian(decryptedPayload.AsSpan(3)));
			Assert.Equal(body, decryptedPayload[5..]);
		}
	}

	[Theory]
	[InlineData(0)]
	[InlineData(37)]
	[InlineData(236)]
	[InlineData(ushort.MaxValue - 207)]
	public void OpcodeTransformsRoundTrip(int opcode)
	{
		Assert.Equal(opcode, GamePacketCodec.DecodeClientOpcode(GamePacketCodec.EncodeClientOpcode(opcode)));
		Assert.Equal(opcode, GamePacketCodec.DecodeServerOpcode(GamePacketCodec.EncodeServerOpcode(opcode)));
	}

	private static EncryptionKeyPair GetLiveKeyPair(Crypt crypt)
	{
		var field = typeof(Crypt).GetField("packetKey", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingFieldException(typeof(Crypt).FullName, "packetKey");
		return Assert.IsType<EncryptionKeyPair>(field.GetValue(crypt));
	}

	private static byte[] CreateServerFrame(int opcode, ReadOnlySpan<byte> body)
	{
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
}
