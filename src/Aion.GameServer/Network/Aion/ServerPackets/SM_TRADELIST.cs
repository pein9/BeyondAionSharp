using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Limiteditems;
using Aion.GameServer.Model.Templates.Goods;
using Aion.GameServer.Model.Templates.Tradelist;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_TRADELIST (alexa026, ATracer, Sarynth, xTz, Neon). Vendor trade list (npc type/buy modifier/tabs filtered by legion level + limited items). ArrayList->new List; addAll->AddRange; TradeTab aliased. Npc/TradeListTemplate/GoodsList/LimitedItem/LimitedItemTradeService red-tolerated.</summary>
// Java parity (writeImpl audited 1:1 vs game-server/.../SM_TRADELIST.java): 2026-06-17
public class SM_TRADELIST : AionServerPacket
{
    private readonly int targetObjId;
    private readonly int playerObjId;
    private readonly TradeNpcType tradeNpcType;
    private readonly int buyPriceModifier;
    private readonly bool showBuyTab;
    private readonly bool showSellTab;
    private readonly IReadOnlyList<int> tradeTabIds;
    private readonly List<LimitedItem> limitedItems;

    public SM_TRADELIST(Player player, Npc npc, TradeNpcType tradeNpcType, IReadOnlyList<int> tradeTabIds, int buyPriceModifier)
    {
        this.targetObjId = npc.GetObjectId();
        this.playerObjId = player.GetObjectId();
        this.tradeNpcType = tradeNpcType;
        this.buyPriceModifier = buyPriceModifier;
        this.showBuyTab = npc.CanSell();
        this.showSellTab = npc.CanBuy();
        this.tradeTabIds = tradeTabIds;
        LimitedTradeNpc? limitedTradeNpc = LimitedItemTradeService.GetInstance().GetLimitedTradeNpc(npc.GetNpcId());
        this.limitedItems = limitedTradeNpc == null ? new List<LimitedItem>() : limitedTradeNpc.GetLimitedItems();
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(targetObjId);
        WriteC(tradeNpcType.Index()); // reward, abyss or normal
        WriteD(buyPriceModifier); // Vendor Buy Price Modifier
        WriteD(100);// new aion 4.5
        WriteC(showBuyTab ? 1 : 0);
        WriteC(showSellTab ? 1 : 0);
        WriteH(tradeTabIds.Count);
        foreach (int tradeTabId in tradeTabIds)
            WriteD(tradeTabId);
        WriteH(limitedItems.Count);
        foreach (LimitedItem limitedItem in limitedItems)
        {
            WriteD(limitedItem.GetItemId());
            WriteH(limitedItem.GetBuyCount(playerObjId));
            WriteH(limitedItem.GetSellLimit());
        }
    }
}
