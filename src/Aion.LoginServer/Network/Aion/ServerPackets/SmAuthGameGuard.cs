using Aion.Commons.Network;

namespace Aion.LoginServer.Network.Aion.ServerPackets;

public sealed class SmAuthGameGuard : AionServerPacket
{
	public SmAuthGameGuard()
		: base(0x0B)
	{
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
	}
}
