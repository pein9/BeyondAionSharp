using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Tradelist;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_TRADE_IN_LIST (MrPoke). Trade-in (sellback) list (npc type/buy modifier + trade tabs). TradeListTemplate.TradeTab aliased; tradeNpcType.index()->Index(). Npc/TradeListTemplate red-tolerated.</summary>
public class SM_TRADE_IN_LIST : AionServerPacket
{
    private readonly Npc npc;
    private readonly TradeNpcType tradeNpcType;
    private readonly int buyPriceModifier;
    private readonly IReadOnlyList<int> tradeTabIds;

    public SM_TRADE_IN_LIST(Npc npc, TradeListTemplate tlist, int buyPriceModifier)
    {
        this.npc = npc;
        this.tradeNpcType = tlist.GetTradeNpcType();
        this.tradeTabIds = tlist.GetTradeTabIds();
        this.buyPriceModifier = buyPriceModifier;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(npc.GetObjectId());
        WriteC(tradeNpcType.Index());
        WriteD(buyPriceModifier); // Vendor Buy Price Modifier
        WriteD(100);// new aion 4.5
        WriteH(tradeTabIds.Count);
        foreach (int tradeTabId in tradeTabIds)
            WriteD(tradeTabId);
    }
}
