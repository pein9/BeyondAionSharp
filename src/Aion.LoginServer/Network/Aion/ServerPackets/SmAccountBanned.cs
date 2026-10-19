using Aion.Commons.Network;

namespace Aion.LoginServer.Network.Aion.ServerPackets;

public sealed class SmAccountBanned : AionServerPacket
{
	private readonly int _reason;
	private readonly int _flags;

	public SmAccountBanned(int reason = 0, int flags = 0)
		: base(0x02)
	{
		_reason = reason;
		_flags = flags;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteD(_reason);
		buffer.WriteD(_flags);
	}
}
