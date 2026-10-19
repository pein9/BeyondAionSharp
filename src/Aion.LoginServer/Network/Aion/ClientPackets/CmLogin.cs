using Aion.Commons.Network;

namespace Aion.LoginServer.Network.Aion.ClientPackets;

public sealed class CmLogin : AionClientPacket
{
	public CmLogin(byte opCode)
		: base(opCode)
	{
	}

	public byte[] EncryptedLoginData { get; private set; } = Array.Empty<byte>();

	public int SessionId { get; private set; }

	public ushort PackageId { get; private set; }

	public byte OpenClientWindows { get; private set; }

	public byte[] PcInfoMd5 { get; private set; } = Array.Empty<byte>();

	protected override void ReadPayload(PacketBuffer buffer)
	{
		const int fieldsAfterCredentials = 43;
		var encryptedLength = (buffer.Remaining - fieldsAfterCredentials) / 128 * 128;
		EncryptedLoginData = buffer.ReadB(encryptedLength);
		SessionId = buffer.ReadD();
		buffer.ReadB(16); // password zeros or external-token data
		buffer.ReadD(); // 32 in the 4.8 client
		PackageId = buffer.ReadH();
		OpenClientWindows = buffer.ReadC();
		PcInfoMd5 = buffer.ReadB(16);
	}
}
