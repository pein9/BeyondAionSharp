using Aion.Commons.Network;

namespace Aion.LoginServer.Network.Aion.ServerPackets;

public sealed class SmLoginOk : AionServerPacket
{
	private readonly SessionKey _sessionKey;

	public SmLoginOk(SessionKey sessionKey)
		: base(0x03)
	{
		_sessionKey = sessionKey;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteD(_sessionKey.AccountId);
		buffer.WriteD(_sessionKey.LoginOk);
		buffer.WriteB(new byte[20]);
		buffer.WriteD(0); // penalty flags
		buffer.WriteB(new byte[16]);
		buffer.WriteD(0); // fatigue warning
	}
}
