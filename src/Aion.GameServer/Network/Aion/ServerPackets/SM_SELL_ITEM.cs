using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Tradelist;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Trade;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_SELL_ITEM (orz, Sarynth, Artur, Neon). Opens a vendor trade window (npc type/buy rate/buy-sell tabs/trade tabs). TradeListTemplate.TradeTab nested via alias; ArrayList->new List. Npc/TradeListTemplate/TradeNpcType/PricesService red-tolerated.</summary>
public class SM_SELL_ITEM : AionServerPacket
{
    private readonly int targetObjectId;
    private readonly TradeNpcType tradeNpcType;
    private readonly int buyPriceRate;
    private readonly bool showBuyTab;
    private readonly bool showSellTab;
    private readonly IReadOnlyList<int> tradeTabIds;

    public SM_SELL_ITEM(Npc npc)
    {
        TradeListTemplate tradeList = DataManager.TRADE_LIST_DATA.GetPurchaseTemplate(npc.GetNpcId());
        this.targetObjectId = npc.GetObjectId();
        this.tradeNpcType = tradeList != null ? tradeList.GetTradeNpcType() : TradeNpcType.NORMAL;
        this.buyPriceRate = tradeList != null ? tradeList.GetBuyPriceRate() : PricesService.GetVendorSellModifier();
        this.showBuyTab = npc.CanSell();
        this.showSellTab = npc.CanBuy() || npc.CanPurchase();
        this.tradeTabIds = tradeList != null ? tradeList.GetTradeTabIds() : Array.Empty<int>();
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(targetObjectId);
        WriteC(tradeNpcType.Index());
        WriteD(buyPriceRate); // price * (buyPriceRate / 100) = display price
        WriteC(showBuyTab ? 1 : 0); // npc sells
        WriteC(showSellTab ? 1 : 0); // npc buys
        WriteH(tradeTabIds.Count);
        foreach (int tradeTabId in tradeTabIds)
            WriteD(tradeTabId);
    }
}
