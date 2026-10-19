using Aion.Commons.Network;

namespace Aion.LoginServer.Network.Aion.ServerPackets;

public sealed class SmAccountKick : AionServerPacket
{
	private readonly AionAuthResponse _response;

	public SmAccountKick(AionAuthResponse response)
		: base(0x08)
	{
		_response = response;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteC((int)_response);
	}
}
