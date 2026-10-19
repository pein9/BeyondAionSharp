using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Limiteditems;
using Aion.GameServer.Model.Templates.Goods;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Tradelist;
using Aion.GameServer.Model.Trade;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Restrictions;
using Aion.GameServer.Services.Abyss;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Services.Trade;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/TradeService (ATracer, Rama, Wakizashi, xTz, Neon). NPC buy/sell/tradein. switch enum (TradeNpcType) bare-case→enum-qualified; enum.equals→==; enum.name()→ToString(); Map<Integer,Long>→Dictionary<int,long>; Set.addAll→HashSet.UnionWith; instanceof Npc→is Npc; Math.round(float)→(int)Math.Floor(+0.5f); nested ItemService.ItemUpdatePredicate/ItemPacketService.* preserved. Templates/services/SM_* red-tolerated.</summary>
public class TradeService
{
    private static readonly ILogger log = AionLog.For(nameof(TradeService));
    private static readonly TradeListData tradeListData = DataManager.TRADE_LIST_DATA;
    private static readonly GoodsListData goodsListData = DataManager.GOODSLIST_DATA;

    private static bool CanBuyLimitItem(Npc npc, Player player, int itemId, long count)
    {
        LimitedItem item = LimitedItemTradeService.GetInstance().GetLimitedItem(itemId, npc.GetNpcId());
        if (item != null)
        {
            if (item.GetDefaultSellLimit() > 0 && item.GetSellLimit() - count < 0)
                return false;
            if (item.GetBuyLimit() > 0 && item.GetBuyCount(player.GetObjectId()) + count > item.GetBuyLimit())
                return false;
        }
        return true;
    }

    private static bool ContainsItem(TradeListTemplate template, Func<int, GoodsList> goodsListById, int itemId)
    {
        foreach (var tab in template.GetTradeTablist())
            if (goodsListById(tab.GetId()).GetItemIdList().Contains(itemId))
                return true;
        return false;
    }

    private readonly record struct Sale(Item Item, long Count, long SellReward);

    public static bool PerformBuyFromShop(Npc npc, Player player, TradeList tradeList)
    {
        TradeNpcType npcType = tradeListData.GetTradeListTemplate(npc.GetNpcId()).GetTradeNpcType();
        switch (npcType)
        {
            case TradeNpcType.NORMAL:
            case TradeNpcType.ABYSS_KINAH:
                return PerformBuyTransaction(npc, player, tradeList, true); // trade including kinah
            case TradeNpcType.ABYSS:
            case TradeNpcType.REWARD:
                return PerformBuyTransaction(npc, player, tradeList, false); // trade without kinah
            default:
                log.LogWarning("Unhandled TradeNpcType:{NpcTypeName}", npcType.ToString());
                break;
        }
        return false;
    }

    /// <summary>General Trade with NPC method. Handles buy items for AP and/or tokens (coins etc.) and/or kinah.</summary>
    public static bool PerformBuyTransaction(Npc npc, Player player, TradeList tradeList, bool useKinah)
    {
        if (!PlayerRestrictions.CanTrade(player))
        {
            return false;
        }

        if (!ValidateBuyItems(npc, tradeList, player))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_BUY_SELL_USER_BUY_FAILED());
            return false;
        }

        Storage inventory = player.GetInventory();
        int freeSlots = inventory.GetFreeSlots();

        // strange new attributes for new trader type
        TradeListTemplate template = tradeListData.GetTradeListTemplate(npc.GetNpcId());
        int sellModifier = template.GetTradeNpcType() == TradeNpcType.ABYSS_KINAH ? template.GetSellPriceRate2() : template.GetSellPriceRate();
        int apSellModifier = template.GetTradeNpcType() == TradeNpcType.ABYSS_KINAH ? template.GetApSellPriceRate2() : template.GetSellPriceRate();

        // 1. If useKinah, check for required Kinah
        if (useKinah && !tradeList.CalculateBuyListPrice(player, sellModifier))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_NOT_ENOUGH_MONEY());
            return false;
        }

        // 2. check required AP + select required items
        if (!tradeList.CalculateAbyssRewardBuyList(player, apSellModifier))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_NOT_ENOUGH_ABYSSPOINT());
            return false;
        }

        // 3. check exploit
        if (tradeList.GetRequiredAp() < 0)
        {
            AuditLogger.Log(player, "possibly used packet hack: tradeList.getRequiredAp() < 0");
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_NOT_ENOUGH_ABYSSPOINT());
            return false;
        }

        // 4. check free slots
        long requiredSlots = 0;
        foreach (TradeItem tradeItem in tradeList.GetTradeItems())
        {
            long maxStackCount = tradeItem.GetItemTemplate().GetMaxStackCount();
            requiredSlots += (tradeItem.GetCount() + maxStackCount - 1) / maxStackCount;
        }
        if (freeSlots < requiredSlots)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_FULL_INVENTORY());
            return false;
        }

        // 5. check sell limits
        var countByItemId = new Dictionary<int, long>();
        foreach (TradeItem tradeItem in tradeList.GetTradeItems())
            countByItemId[tradeItem.GetItemId()] = countByItemId.GetValueOrDefault(tradeItem.GetItemId()) + tradeItem.GetCount();
        foreach (var entry in countByItemId)
        {
            if (!CanBuyLimitItem(npc, player, entry.Key, entry.Value))
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_LIMITED_BUYING_CANT_SELECT_NO_ITEMS());
                return false;
            }
        }

        // 6. subtract all costs
        long tradeListPrice = tradeList.GetRequiredKinah();
        if (tradeList.GetRequiredAp() > 0)
            AbyssPointsService.AddAp(player, unchecked((int)-tradeList.GetRequiredAp()));

        if (useKinah && tradeListPrice > 0)
            if (!inventory.TryDecreaseKinah(tradeListPrice))
                return false;

        Dictionary<int, long> requiredItems = tradeList.GetRequiredItems();
        foreach (int itemId in requiredItems.Keys)
        {
            if (!player.GetInventory().DecreaseByItemId(itemId, requiredItems[itemId]))
            {
                AuditLogger.Log(player, "tried to sell item " + itemId + " for AP, which could not be removed");
                return false;
            }
        }

        // 7. finally add items and update sell limits
        foreach (TradeItem tradeItem in tradeList.GetTradeItems())
        {
            // allow inventory overflow because player can get deranked during purchase, possibly reducing the number of free inventory slots
            ItemService.AddItem(player, tradeItem.GetItemId(), tradeItem.GetCount(), true,
                new ItemService.ItemUpdatePredicate(ItemPacketService.ItemAddType.BUY, ItemPacketService.ItemUpdateType.INC_ITEM_BUY));

            LimitedItem item = LimitedItemTradeService.GetInstance().GetLimitedItem(tradeItem.GetItemId(), npc.GetNpcId());
            if (item != null)
            {
                if (item.GetBuyLimit() > 0)
                    item.SetBuyCount(player.GetObjectId(), item.GetBuyCount(player.GetObjectId()) + (int)tradeItem.GetCount());
                if (item.GetDefaultSellLimit() > 0)
                    item.SetSellLimit(item.GetSellLimit() - (int)tradeItem.GetCount());
            }
        }

        return true;
    }

    private static bool ValidateBuyItems(Npc npc, TradeList tradeList, Player player)
    {
        TradeListTemplate tradeListTemplate = tradeListData.GetTradeListTemplate(npc.GetObjectTemplate().GetTemplateId());

        HashSet<int> allowedItems = new HashSet<int>();
        foreach (TradeListTemplate.TradeTab tradeTab in tradeListTemplate.GetTradeTablist())
        {
            GoodsList goodsList = goodsListData.GetGoodsListById(tradeTab.GetId());
            if (goodsList != null && goodsList.GetItemIdList() != null)
                allowedItems.UnionWith(goodsList.GetItemIdList());
        }

        foreach (TradeItem tradeItem in tradeList.GetTradeItems())
            if (tradeItem.GetCount() < 1 || !allowedItems.Contains(tradeItem.GetItemId()))
                return false;

        return true;
    }

    public static bool PerformSellToShop(Player player, TradeList tradeList, TradeListTemplate purchaseTemplate)
    {
        return PerformSellToShop(player, tradeList, purchaseTemplate, Aion.GameServer.Services.Trade.PricesService.GetVendorSellModifier());
    }

    public static bool PerformSellToShop(Player player, TradeList tradeList, TradeListTemplate purchaseTemplate, int sellModifier)
    {
        if (!PlayerRestrictions.CanTrade(player))
            return false;

        Storage inventory = player.GetInventory();
        // Validate every row before any inventory or daily-limit changes.
        var sales = new List<Sale>();
        foreach (TradeItem tradeItem in tradeList.GetTradeItems())
        {
            long count = tradeItem.GetCount();
            Item item = inventory.GetItemByObjId(tradeItem.GetItemId());
            if (item == null) // don't allow to sell fake items;
                return false;

            long sellReward;

            if (purchaseTemplate != null)
            {
                if (!ContainsItem(purchaseTemplate, goodsListData.GetGoodsPurchaseListById, item.GetItemId()))
                    return false;
                sellReward = (long)(item.GetItemTemplate().GetPrice() * purchaseTemplate.GetBuyPriceRate() / 100D);
            }
            else
            {
                if (!item.IsSellable())
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_BUY_SELL_ITEM_CAN_NOT_BE_SELLED_TO_NPC(item.GetL10n()));
                    return false;
                }
                sellReward = PricesService.GetSellReward(item.GetItemTemplate().GetPrice(), sellModifier);
            }

            if (count > item.GetItemCount())
            {
                AuditLogger.Log(player, "tried to sell more items to npc than he has");
                return false;
            }
            sales.Add(new Sale(item, count, sellReward));
        }

        long kinahReward = 0;
        var items = new List<Item>();
        foreach (var sale in sales)
        {
            Item item = sale.Item;
            long count = PlayerLimitService.UpdateSellLimit(player, sale.SellReward, sale.Count);
            if (count == 0)
                break;

            long realReward = sale.SellReward * count;
            Item repurchaseItem;
            if (item.GetItemCount() == count)
            {
                inventory.Delete(item, ItemPacketService.ItemDeleteType.SELL);
                repurchaseItem = item;
            }
            else
            {
                repurchaseItem = ItemFactory.NewItem(item.GetItemId(), count);
                repurchaseItem.SetExpireTime(item.GetExpireTime());
                inventory.DecreaseItemCount(item, count);
            }

            kinahReward += realReward;
            repurchaseItem.SetRepurchasePrice(realReward);
            items.Add(repurchaseItem);
        }
        RepurchaseService.GetInstance().AddRepurchaseItems(player, items);
        inventory.IncreaseKinah(kinahReward, ItemPacketService.ItemUpdateType.INC_KINAH_SELL);

        return true;
    }

    public static bool PerformSellForAPToShop(Player player, TradeList tradeList, TradeListTemplate purchaseTemplate)
    {
        if (!CustomConfig.SELLING_APITEMS_ENABLED)
        {
            PacketSendUtility.SendMessage(player, "This feature is disabled");
            return false;
        }

        if (!PlayerRestrictions.CanTrade(player))
            return false;

        Storage inventory = player.GetInventory();
        foreach (TradeItem tradeItem in tradeList.GetTradeItems())
        {
            int itemObjectId = tradeItem.GetItemId();
            long count = tradeItem.GetCount();
            Item item = inventory.GetItemByObjId(itemObjectId);
            if (item == null)
                return false;

            Acquisition acquisition = item.GetItemTemplate().GetAcquisition();
            if (acquisition == null)
                return false;
            if (!ContainsItem(purchaseTemplate, goodsListData.GetGoodsPurchaseListById, item.GetItemId()))
                return false;
            if (inventory.DecreaseByObjectId(itemObjectId, count))
            {
                int requiredAp = acquisition.GetRequiredAp();
                int apToAdd = (int)Math.Floor((requiredAp * purchaseTemplate.GetBuyPriceRate()) / 100F + 0.5f);
                AbyssPointsService.AddAp(player, apToAdd * (int)count);
            }
        }
        return true;
    }

    public static bool PerformBuyFromTradeInTrade(Player player, int npcObjectId, int itemId, int count, List<int> tradeInItemObjectIds)
    {
        if (!PlayerRestrictions.CanTrade(player))
        {
            return false;
        }
        if (player.GetInventory().IsFull())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_FULL_INVENTORY());
            return false;
        }

        if (!(player.GetTarget() is Npc))
            return false;

        Npc npc = (Npc)player.GetTarget();
        if (!npc.CanTradeIn() || npc.GetObjectId() != npcObjectId || PositionUtil.GetDistance(npc, player) > 10)
            return false;

        TradeListTemplate tradeInList = tradeListData.GetTradeInListTemplate(npc.GetNpcId());
        if (!ContainsItem(tradeInList, goodsListData.GetGoodsInListById, itemId))
            return false;

        ItemTemplate itemTemplate = DataManager.ITEM_DATA.GetItemTemplate(itemId);
        if (itemTemplate.GetMaxStackCount() < count)
            return false;

        List<TradeinItem> requiredTradeInItems = itemTemplate.GetTradeinList().GetTradeinItem();

        HashSet<int> tradeInItemIds = new HashSet<int>();
        foreach (int tradeInItemObjectId in tradeInItemObjectIds)
        {
            Item checkItem = player.GetInventory().GetItemByObjId(tradeInItemObjectId);
            if (checkItem == null)
            {
                AuditLogger.Log(player,
                    "possibly used TradeIn packet hack on " + npc + ": Player does not have the submitted item with object ID " + tradeInItemObjectId);
                return false;
            }
            tradeInItemIds.Add(checkItem.GetItemId());
        }

        if (tradeInItemIds.Count != requiredTradeInItems.Count)
        {
            AuditLogger.Log(player, "possibly used TradeIn packet hack on " + npc
                + ": The tradein list count differs from the servers templates.\nRequired: " + requiredTradeInItems + "\nSubmitted:" + tradeInItemIds);
            return false;
        }

        foreach (TradeinItem requiredTradeInItem in requiredTradeInItems)
        {
            bool validated = false;
            foreach (int tradeInItemId in tradeInItemIds)
            {
                if (requiredTradeInItem.GetId() == tradeInItemId)
                {
                    validated = true;
                    break;
                }
            }
            if (!validated)
            {
                AuditLogger.Log(player,
                    "possibly used TradeIn packet hack on " + npc + ": Did not receive all required items (expected " + requiredTradeInItem.GetId() + ").");
                return false;
            }
        }

        foreach (TradeinItem requiredTradeInItem in requiredTradeInItems)
        {
            if (player.GetInventory().GetItemCountByItemId(requiredTradeInItem.GetId()) < requiredTradeInItem.GetPrice() * count)
                return false;
        }

        Acquisition aquisition = itemTemplate.GetAcquisition();
        if (aquisition != null && (aquisition.GetType_() == AcquisitionType.ABYSS || aquisition.GetType_() == AcquisitionType.AP))
        {
            int requiredAp = (int)((aquisition.GetRequiredAp() * count * tradeInList.GetSellPriceRate() / 100.0D) * Aion.GameServer.Services.Trade.PricesService.GetVendorBuyModifier())
                / 100;
            int diferenceAp = 0;
            foreach (TradeinItem treadInList in requiredTradeInItems)
            {
                ItemTemplate itemReq = DataManager.ITEM_DATA.GetItemTemplate(treadInList.GetId());
                if (itemReq != null)
                {
                    diferenceAp += (int)((itemReq.GetAcquisition().GetRequiredAp() * count * tradeInList.GetSellPriceRate() / 100.0D)
                        * Aion.GameServer.Services.Trade.PricesService.GetVendorBuyModifier()) / 100;
                }
            }
            if ((requiredAp - diferenceAp) > 0)
            {
                if (player.GetAbyssRank().GetAp() < (requiredAp - diferenceAp))
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_NOT_ENOUGH_ABYSSPOINT());
                    return false;
                }
                AbyssPointsService.AddAp(player, -(requiredAp - diferenceAp));
            }
        }

        foreach (TradeinItem requiredTradeInItem in requiredTradeInItems)
        {
            if (!player.GetInventory().DecreaseByItemId(requiredTradeInItem.GetId(), requiredTradeInItem.GetPrice() * count))
                return false;
        }

        ItemService.AddItem(player, itemId, count);
        return true;
    }
}
