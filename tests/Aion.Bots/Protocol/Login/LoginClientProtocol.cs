using System.Buffers.Binary;
using System.Security.Cryptography;
using Aion.Commons.Network;
using Aion.LoginServer.Network;
using Aion.LoginServer.Network.Crypto;

namespace Aion.Bots.Protocol.Login;

public sealed record LoginInitPacket(int SessionId, int ProtocolRevision, byte[] Modulus, byte[] BlowfishKey);

public sealed record LoginHandshakeResult(int AccountId, int LoginOk);

/// <summary>Client-side login protocol state initialized exclusively from the server's SM_INIT frame.</summary>
public sealed class LoginClientProtocol
{
	private static readonly byte[] InitialLoginKey =
	{
		0x6B, 0x60, 0xCB, 0x5B,
		0x82, 0xCE, 0x90, 0xB1,
		0xCC, 0x2B, 0x6C, 0x55,
		0x6C, 0x6C, 0x6C, 0x6C
	};

	private LoginClientProtocol(LoginInitPacket init)
	{
		Init = init;
		PublicParameters = new RSAParameters
		{
			Modulus = init.Modulus.ToArray(),
			Exponent = new byte[] { 0x01, 0x00, 0x01 },
		};
		Crypto = new LoginClientCrypto(init.BlowfishKey);
	}

	public LoginInitPacket Init { get; }

	public RSAParameters PublicParameters { get; }

	public LoginClientCrypto Crypto { get; }

	public static LoginClientProtocol FromInitFrame(ReadOnlySpan<byte> frame)
	{
		if (frame.Length < 2)
			throw new InvalidDataException("SM_INIT frame is missing its length prefix.");
		var declaredLength = BinaryPrimitives.ReadUInt16LittleEndian(frame);
		if (declaredLength != frame.Length)
			throw new InvalidDataException($"SM_INIT frame length mismatch. Declared {declaredLength}, actual {frame.Length}.");

		var payload = DecryptFirstServerPayload(frame[2..]);
		if (payload.Length < 169 || payload[0] != 0x00)
			throw new InvalidDataException("Expected login SM_INIT opcode 0x00 and its complete key material.");

		var revision = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(5, 4));
		if (revision != 0x0000C621)
			throw new InvalidDataException($"Unsupported login protocol revision 0x{revision:X8}.");

		var init = new LoginInitPacket(
			BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(1, 4)),
			revision,
			UnscrambleModulus(payload.AsSpan(9, 128)),
			payload.AsSpan(153, 16).ToArray());
		return new LoginClientProtocol(init);
	}

	public static async Task<LoginClientProtocol> ReadInitAsync(Stream stream, CancellationToken cancellationToken = default)
	{
		return FromInitFrame(await ReadFrameAsync(stream, cancellationToken));
	}

	public async Task<LoginHandshakeResult> LoginAsync(
		Stream stream,
		string username,
		string password,
		CancellationToken cancellationToken = default)
	{
		await stream.WriteAsync(Crypto.CreateAuthGameGuardFrame(Init.SessionId), cancellationToken);
		var gameGuard = Crypto.DecryptServerFrame(await ReadFrameAsync(stream, cancellationToken));
		if (gameGuard[0] != 0x0B || BinaryPrimitives.ReadInt32LittleEndian(gameGuard.AsSpan(1, 4)) != Init.SessionId)
			throw new InvalidDataException("Login server rejected or corrupted the CM_AUTH_GG exchange.");

		await stream.WriteAsync(Crypto.CreateLoginFrame(PublicParameters, Init.SessionId, username, password), cancellationToken);
		var login = Crypto.DecryptServerFrame(await ReadFrameAsync(stream, cancellationToken));
		if (login[0] != 0x03)
			throw new InvalidDataException($"Expected SM_LOGIN_OK (0x03), received opcode 0x{login[0]:X2}.");
		return new LoginHandshakeResult(
			BinaryPrimitives.ReadInt32LittleEndian(login.AsSpan(1, 4)),
			BinaryPrimitives.ReadInt32LittleEndian(login.AsSpan(5, 4)));
	}

	/// <summary>Reverses the login server's four-stage RSA modulus obfuscation.</summary>
	public static byte[] UnscrambleModulus(ReadOnlySpan<byte> scrambledModulus)
	{
		if (scrambledModulus.Length != 128)
			throw new ArgumentException("Login RSA modulus must be 128 bytes.", nameof(scrambledModulus));

		var modulus = scrambledModulus.ToArray();
		for (var i = 0; i < 0x40; i++)
			modulus[0x40 + i] ^= modulus[i];
		for (var i = 0; i < 4; i++)
			modulus[0x0D + i] ^= modulus[0x34 + i];
		for (var i = 0; i < 0x40; i++)
			modulus[i] ^= modulus[0x40 + i];
		for (var i = 0; i < 4; i++)
			(modulus[i], modulus[0x4D + i]) = (modulus[0x4D + i], modulus[i]);
		return modulus;
	}

	public static byte[] DecryptFirstServerPayload(ReadOnlySpan<byte> encryptedPayload)
	{
		var payload = encryptedPayload.ToArray();
		if (payload.Length == 0 || (payload.Length & 7) != 0)
			throw new InvalidDataException("The first login payload must contain complete Blowfish blocks.");
		var cipher = new BlowfishCipher(InitialLoginKey);
		cipher.Decipher(payload);
		UndoFirstServerXorPass(payload);
		return payload;
	}

	public static async Task<byte[]> ReadFrameAsync(Stream stream, CancellationToken cancellationToken = default)
	{
		var header = await ReadExactAsync(stream, 2, cancellationToken);
		var frameLength = BinaryPrimitives.ReadUInt16LittleEndian(header);
		if (frameLength < 3)
			throw new InvalidDataException($"Invalid login frame length {frameLength}.");
		var frame = new byte[frameLength];
		header.CopyTo(frame, 0);
		(await ReadExactAsync(stream, frameLength - 2, cancellationToken)).CopyTo(frame, 2);
		return frame;
	}

	private static void UndoFirstServerXorPass(byte[] payload)
	{
		unchecked
		{
			var stop = payload.Length - 8;
			var ecx = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(stop, 4));
			for (var position = stop - 4; position >= 4; position -= 4)
			{
				var encoded = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(position, 4));
				var plain = encoded ^ ecx;
				BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(position, 4), plain);
				ecx -= plain;
			}
		}
	}

	private static async Task<byte[]> ReadExactAsync(Stream stream, int length, CancellationToken cancellationToken)
	{
		var buffer = new byte[length];
		var offset = 0;
		while (offset < length)
		{
			var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
			if (read == 0)
				throw new EndOfStreamException("Socket closed before the expected login frame was read.");
			offset += read;
		}
		return buffer;
	}
}

/// <summary>Stateful Blowfish codec and login client-packet writers.</summary>
public sealed class LoginClientCrypto
{
	private readonly BlowfishCipher _cipher;

	public LoginClientCrypto(byte[] blowfishKey)
	{
		_cipher = new BlowfishCipher(blowfishKey);
	}

	public byte[] CreateAuthGameGuardFrame(int sessionId)
	{
		using var payload = new PacketBuffer();
		payload.WriteC(0x07);
		payload.WriteD(sessionId);
		payload.WriteD(0);
		payload.WriteD(0);
		payload.WriteD(0);
		payload.WriteD(0);
		payload.WriteB(new byte[0x0B]);
		return EncryptFrame(payload.ToArray());
	}

	public byte[] CreateLoginFrame(RSAParameters publicParameters, int sessionId, string username, string password)
	{
		if (username.Length > 14 || password.Length > 16 || !username.All(char.IsAscii) || !password.All(char.IsAscii))
			throw new ArgumentException("Login credentials must be ASCII and fit the protocol fields.");

		var plainCredentials = new byte[128];
		WriteAscii(plainCredentials, 94, username);
		WriteAscii(plainCredentials, 108, password);
		BinaryPrimitives.WriteInt32LittleEndian(plainCredentials.AsSpan(124, 4), -1);
		var encryptedCredentials = LoginRsaKeyPair.RawEncryptForTesting(plainCredentials, publicParameters);

		using var payload = new PacketBuffer();
		payload.WriteC(0x00);
		payload.WriteB(encryptedCredentials);
		payload.WriteD(sessionId);
		payload.WriteB(new byte[16]);
		payload.WriteB(new byte[] { 0x20, 0, 0, 0, 0, 0, 1 });
		payload.WriteB(new byte[] { 0x9D, 0xDA, 0x47, 0xA7, 0x21, 0xC0, 0xA6, 0xA5, 0x4B, 0xB7, 0x5E, 0xE3, 0xCE, 0xC9, 0x26, 0xAA });
		payload.WriteD(0);
		return EncryptFrame(payload.ToArray());
	}

	public byte[] CreateServerListFrame(int accountId, int loginOk)
	{
		using var payload = new PacketBuffer();
		payload.WriteC(0x05);
		payload.WriteD(accountId);
		payload.WriteD(loginOk);
		payload.WriteC(0);
		payload.WriteB(new byte[6]);
		payload.WriteD(0);
		payload.WriteD(0);
		return EncryptFrame(payload.ToArray());
	}

	public byte[] CreatePlayFrame(int accountId, int loginOk, byte serverId)
	{
		using var payload = new PacketBuffer();
		payload.WriteC(0x02);
		payload.WriteD(accountId);
		payload.WriteD(loginOk);
		payload.WriteC(serverId);
		payload.WriteB(new byte[6]);
		payload.WriteQ(0);
		return EncryptFrame(payload.ToArray());
	}

	public byte[] CreateUpdateSessionFrame(int accountId, int loginOk, int reconnectKey)
	{
		using var payload = new PacketBuffer();
		payload.WriteC(0x08);
		payload.WriteD(accountId);
		payload.WriteD(loginOk);
		payload.WriteD(reconnectKey);
		payload.WriteC(68);
		payload.WriteB(new byte[] { 1, 2, 3, 4, 5, 6 });
		payload.WriteC(4);
		payload.WriteC(68);
		payload.WriteH(0x7788);
		return EncryptFrame(payload.ToArray());
	}

	public byte[] CreateOpcodeOnlyFrame(byte opcode) => EncryptFrame(new[] { opcode });

	public byte[] EncryptFrame(byte[] rawPayload)
	{
		var payload = CreateClientChecksummedPayload(rawPayload);
		_cipher.Cipher(payload);
		return PacketFrameCodec.CreateFrame(payload);
	}

	public byte[] DecryptServerFrame(ReadOnlySpan<byte> frame)
	{
		if (frame.Length < 3 || BinaryPrimitives.ReadUInt16LittleEndian(frame) != frame.Length)
			throw new InvalidDataException("Login server frame has an invalid length prefix.");
		return DecryptServerPayload(frame[2..]);
	}

	public byte[] DecryptServerPayload(ReadOnlySpan<byte> encryptedPayload)
	{
		var payload = encryptedPayload.ToArray();
		_cipher.Decipher(payload);
		if (!VerifyServerChecksum(payload))
			throw new InvalidDataException("Login server packet checksum failed.");
		return payload;
	}

	private static void WriteAscii(byte[] buffer, int offset, string value)
	{
		for (var i = 0; i < value.Length; i++)
			buffer[offset + i] = (byte)value[i];
	}

	private static byte[] CreateClientChecksummedPayload(byte[] rawPayload)
	{
		var length = rawPayload.Length + 8;
		if ((length & 7) != 0)
			length += 8 - (length & 7);

		var payload = new byte[length];
		rawPayload.CopyTo(payload, 0);
		var checksumOffset = length - 8;
		var xor = 0;
		for (var offset = 0; offset < checksumOffset; offset += 4)
			xor ^= BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset, 4));
		BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(checksumOffset, 4), xor);
		return payload;
	}

	private static bool VerifyServerChecksum(byte[] payload)
	{
		if (payload.Length < 8 || (payload.Length & 3) != 0)
			return false;
		var xor = 0;
		var checksumOffset = payload.Length - 4;
		for (var offset = 0; offset < checksumOffset; offset += 4)
			xor ^= BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset, 4));
		return xor == BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(checksumOffset, 4));
	}
}
