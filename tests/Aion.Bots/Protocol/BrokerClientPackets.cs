using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.Bots.Protocol;

public static partial class GameClientPackets
{
	public static BotClientPacket RegisterBrokerItem(int npc, int itemObjectId, long unitPrice, long count, bool splitting) =>
		Create<CM_REGISTER_BROKER_ITEM>(w => { w.D(npc); w.D(itemObjectId); w.Q(unitPrice); w.Q(count); w.C(splitting ? 1 : 0); });
	public static BotClientPacket BuyBrokerItem(int npc, int itemObjectId, long count) =>
		Create<CM_BUY_BROKER_ITEM>(w => { w.D(npc); w.D(itemObjectId); w.Q(count); });
	public static BotClientPacket BrokerList(int npc, byte sort, ushort page, ushort mask) =>
		Create<CM_BROKER_LIST>(w => { w.D(npc); w.C(sort); w.UH(page); w.UH(mask); });
	public static BotClientPacket BrokerSearch(int npc, byte sort, ushort page, ushort mask, params int[] itemIds) =>
		Create<CM_BROKER_SEARCH>(w => { w.D(npc); w.C(sort); w.UH(page); w.UH(mask); w.UH(itemIds.Length); foreach (int id in itemIds) w.D(id); });
	public static BotClientPacket BrokerRegistered(int npc) => Create<CM_BROKER_REGISTERED>(w => w.D(npc));
	public static BotClientPacket CancelBrokerItem(int npc, int itemObjectId) => Create<CM_BROKER_CANCEL_REGISTERED>(w => { w.D(npc); w.D(itemObjectId); });
	public static BotClientPacket BrokerSettlements(int npc, ushort page = 0) => Create<CM_BROKER_SETTLE_LIST>(w => { w.D(npc); w.UH(page); });
	public static BotClientPacket SettleBrokerAccount(int npc) => Create<CM_BROKER_SETTLE_ACCOUNT>(w => w.D(npc));
	public static BotClientPacket BrokerSellWindow(int itemObjectId) => Create<CM_BROKER_SELL_WINDOW>(w => w.D(itemObjectId));
}
