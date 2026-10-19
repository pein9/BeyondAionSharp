using Aion.Commons.Network;

namespace Aion.LoginServer.Network.Aion;

public abstract class AionClientPacket
{
	protected AionClientPacket(byte opCode)
	{
		OpCode = opCode;
	}

	public byte OpCode { get; }

	public void Read(PacketBuffer buffer)
	{
		ReadPayload(buffer);
		// The checksum trailer was removed by the connection; only 0..7 padding bytes belong here.
		if (buffer.Remaining < 8)
			buffer.ReadB(buffer.Remaining);
	}

	protected abstract void ReadPayload(PacketBuffer buffer);
}
