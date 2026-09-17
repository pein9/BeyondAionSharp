using Aion.GameServer.Network.Aion;

namespace Aion.Bots.Protocol;

public sealed record BotClientPacket(Type PacketType, byte[] Body)
{
	public byte[] Encode(GamePacketCodec codec, AionConnection.State state) =>
		codec.EncodeClientFrame(PacketType, state, Body);
}
