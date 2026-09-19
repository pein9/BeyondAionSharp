using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.Bots.Protocol;

public readonly record struct PrivateStoreOffer(int ObjectId, int ItemId, ushort Count, long UnitPrice);

public static partial class GameClientPackets
{
	public static BotClientPacket PrivateStore(params PrivateStoreOffer[] items) => Create<CM_PRIVATE_STORE>(w =>
	{
		w.UH(items.Length);
		foreach (var item in items) { w.D(item.ObjectId); w.D(item.ItemId); w.UH(item.Count); w.Q(item.UnitPrice); }
	});
	public static BotClientPacket PrivateStoreName(string name) => Create<CM_PRIVATE_STORE_NAME>(w => w.S(name));
}
