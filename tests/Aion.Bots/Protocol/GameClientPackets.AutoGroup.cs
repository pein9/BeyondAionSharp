using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.Bots.Protocol;

/// <summary>The auto-group (instance entry queue) client packet, laid out as the server's reader (Java's readImpl) reads it.</summary>
public static partial class GameClientPackets
{
	/// <summary>
	/// CM_AUTO_GROUP: D instance mask, C window, C entry request. The client sends window 100 to register (entry request 0 new
	/// group, 1 quick entry, 2 its group), 101 to cancel the registration, 102 to enter, 103 to decline the entry and 104 when the
	/// player clicks a periodic instance's icon.
	/// </summary>
	public static BotClientPacket AutoGroup(int maskId, byte window, byte entryRequest = 0) =>
		Create<CM_AUTO_GROUP>(w => { w.D(maskId); w.C(window); w.C(entryRequest); });
}
