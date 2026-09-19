using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.Bots.Protocol;

public static partial class GameClientPackets
{
	public static BotClientPacket BuyTradeIn(int sellerObjectId, byte mask, int itemId, int count,
		params int[] tradeInItemObjectIds) => Create<CM_BUY_TRADE_IN_TRADE>(w =>
	{
		w.D(sellerObjectId); w.C(mask); w.D(itemId); w.D(count); w.UH(tradeInItemObjectIds.Length);
		foreach (int objectId in tradeInItemObjectIds) w.D(objectId);
	});
}
