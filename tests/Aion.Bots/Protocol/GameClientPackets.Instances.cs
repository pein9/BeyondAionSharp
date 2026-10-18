using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.Bots.Protocol;

/// <summary>The instance client packets, laid out as the server's reader (Java's readImpl) reads them.</summary>
public static partial class GameClientPackets
{
	/// <summary>
	/// CM_INSTANCE_LEAVE: no body. The client sends it from its leave-instance button; Java hands it to the instance handler's
	/// leaveInstance, which only some handlers implement.
	/// </summary>
	public static BotClientPacket InstanceLeave() => Empty<CM_INSTANCE_LEAVE>();
}
