using System.Buffers.Binary;
using System.Text;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Protocol;

/// <summary>Stateful client-side framing and crypt for the Aion 4.8 game protocol.</summary>
public sealed class GamePacketCodec
{
	public const byte ClientPacketCode = 0x65;
	public const byte ServerPacketCode = 0x44;

	private const int HeaderLength = 5;
	private static readonly byte[] StaticKey =
		Encoding.ASCII.GetBytes("nKO/WctQ0AVLbpzfBkS6NevDYT8ourG5CRlmdjyJ72aswx4EPq1UgZhFMXH?3iI9");

	private byte[]? clientKey;
	private byte[]? serverKey;
	private readonly GamePacketRegistry registry = GamePacketRegistry.Instance;

	public bool HasKey => clientKey != null;

	public int BaseKey { get; private set; }

	/// <summary>Reads the unencrypted SM_KEY frame and initializes both packet directions.</summary>
	public DecodedGamePacket RecoverKeyFromSmKeyFrame(ReadOnlySpan<byte> frame)
	{
		if (HasKey)
			throw new InvalidOperationException("The game crypt key is already initialized.");

		var payload = ReadFramePayload(frame);
		var packet = DecodePlainServerPayload(payload);
		if (packet.Body.Length < sizeof(int))
			throw new InvalidDataException("SM_KEY does not contain its encoded key.");

		var encodedKey = BinaryPrimitives.ReadInt32LittleEndian(packet.Body);
		BaseKey = unchecked((encodedKey - 0x3FF2CCCF) ^ (int)0xCD92E4DF);
		clientKey = CreateKey(BaseKey);
		serverKey = CreateKey(BaseKey);
		return packet;
	}

	/// <summary>Frames and encrypts one client packet, advancing the client key.</summary>
	public byte[] EncodeClientFrame(Type packetType, AionConnection.State state, ReadOnlySpan<byte> body)
	{
		var packet = registry.GetClient(packetType);
		packet.EnsureValid(state);
		var key = clientKey ?? throw new InvalidOperationException("Recover SM_KEY before sending client packets.");
		var payload = new byte[checked(HeaderLength + body.Length)];
		var encodedOpcode = EncodeClientOpcode(packet.Opcode);
		WriteHeader(payload, encodedOpcode, ClientPacketCode);
		body.CopyTo(payload.AsSpan(HeaderLength));
		Encrypt(payload, key);
		return CreateFrame(payload);
	}

	public byte[] EncodeClientFrame(BotClientPacket packet, AionConnection.State state) =>
		EncodeClientFrame(packet.PacketType, state, packet.Body);

	/// <summary>Decrypts and validates one server packet, advancing the server key.</summary>
	public DecodedGamePacket DecodeServerFrame(ReadOnlySpan<byte> frame)
	{
		var key = serverKey ?? throw new InvalidOperationException("Recover SM_KEY before reading encrypted server packets.");
		var payload = ReadFramePayload(frame);
		Decrypt(payload, key);
		return DecodePlainServerPayload(payload);
	}

	public static ushort EncodeClientOpcode(int opcode) =>
		unchecked((ushort)((((opcode + SM_VERSION_CHECK.INTERNAL_VERSION) ^ 0xEF) + 0x0C) ^ 0xEF));

	public static int DecodeClientOpcode(ushort opcode) =>
		((opcode ^ 0xEF) - 0x0C ^ 0xEF) - SM_VERSION_CHECK.INTERNAL_VERSION;

	public static ushort EncodeServerOpcode(int opcode) =>
		unchecked((ushort)((opcode + SM_VERSION_CHECK.INTERNAL_VERSION) ^ 0xDF));

	public static int DecodeServerOpcode(ushort opcode) =>
		(opcode ^ 0xDF) - SM_VERSION_CHECK.INTERNAL_VERSION;

	private DecodedGamePacket DecodePlainServerPayload(ReadOnlySpan<byte> payload)
	{
		var encodedOpcode = ValidateHeader(payload, ServerPacketCode);
		var opcode = DecodeServerOpcode(encodedOpcode);
		var packet = registry.GetServer(opcode);
		return new DecodedGamePacket(opcode, packet.PacketType, payload[HeaderLength..].ToArray());
	}

	private static ushort ValidateHeader(ReadOnlySpan<byte> payload, byte packetCode)
	{
		if (payload.Length < HeaderLength)
			throw new InvalidDataException($"Game packet payload is shorter than the {HeaderLength}-byte header.");

		var encodedOpcode = BinaryPrimitives.ReadUInt16LittleEndian(payload);
		var complement = BinaryPrimitives.ReadUInt16LittleEndian(payload[3..]);
		if (payload[2] != packetCode || complement != unchecked((ushort)~encodedOpcode))
			throw new InvalidDataException("Game packet header validation failed.");

		return encodedOpcode;
	}

	private static void WriteHeader(Span<byte> payload, ushort encodedOpcode, byte packetCode)
	{
		BinaryPrimitives.WriteUInt16LittleEndian(payload, encodedOpcode);
		payload[2] = packetCode;
		BinaryPrimitives.WriteUInt16LittleEndian(payload[3..], unchecked((ushort)~encodedOpcode));
	}

	private static byte[] CreateFrame(ReadOnlySpan<byte> payload)
	{
		var length = checked(payload.Length + sizeof(ushort));
		if (length > ushort.MaxValue)
			throw new ArgumentOutOfRangeException(nameof(payload), "Packet is too large for Aion's u16 frame length.");

		var frame = new byte[length];
		BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)length);
		payload.CopyTo(frame.AsSpan(sizeof(ushort)));
		return frame;
	}

	private static byte[] ReadFramePayload(ReadOnlySpan<byte> frame)
	{
		if (frame.Length < sizeof(ushort) + HeaderLength)
			throw new InvalidDataException("Game frame is too short.");

		var declaredLength = BinaryPrimitives.ReadUInt16LittleEndian(frame);
		if (declaredLength != frame.Length)
			throw new InvalidDataException($"Game frame length mismatch: declared {declaredLength}, actual {frame.Length}.");

		return frame[sizeof(ushort)..].ToArray();
	}

	private static byte[] CreateKey(int baseKey) =>
	[
		(byte)(baseKey & 0xff),
		(byte)((baseKey >> 8) & 0xff),
		(byte)((baseKey >> 16) & 0xff),
		(byte)((baseKey >> 24) & 0xff),
		0xa1,
		0x6c,
		0x54,
		0x87,
	];

	private static void Encrypt(Span<byte> data, byte[] key)
	{
		data[0] ^= key[0];
		var previous = data[0];
		for (var i = 1; i < data.Length; i++)
		{
			data[i] ^= (byte)(StaticKey[i & 63] ^ key[i & 7] ^ previous);
			previous = data[i];
		}
		AdvanceKey(key, data.Length);
	}

	private static void Decrypt(Span<byte> data, byte[] key)
	{
		var previous = data[0];
		data[0] ^= key[0];
		for (var i = 1; i < data.Length; i++)
		{
			var current = data[i];
			data[i] ^= (byte)(StaticKey[i & 63] ^ key[i & 7] ^ previous);
			previous = current;
		}
		AdvanceKey(key, data.Length);
	}

	private static void AdvanceKey(Span<byte> key, int packetLength)
	{
		var value = BinaryPrimitives.ReadUInt64LittleEndian(key);
		BinaryPrimitives.WriteUInt64LittleEndian(key, unchecked(value + (uint)packetLength));
	}
}

public sealed record DecodedGamePacket(int Opcode, Type PacketType, byte[] Body);
