using Aion.Commons.Network;

namespace Aion.LoginServer.Network.Aion.ServerPackets;

public sealed class SmLoginFail : AionServerPacket
{
	private readonly AionAuthResponse _response;

	public SmLoginFail(AionAuthResponse response)
		: base(0x01)
	{
		_response = response;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteC((int)_response);
	}
}
