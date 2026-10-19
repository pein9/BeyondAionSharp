using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;

namespace Aion.GameServer.Services.Items;

/// <summary>Java parity: services/item/ItemRestrictionService (ATracer). Storage move restrictions: isItemRestrictedFrom (legion-warehouse withdrawal rights), isItemRestrictedTo (warehouse/account/legion deposit storability + legion rights), canRemoveItem (quest-item placeholder). switch-on-enum->switch statement; getItemGroup().equals->Equals. StorageType/LegionPermissionsMask/ItemGroup/SM_ red-tolerated.</summary>
public class ItemRestrictionService
{
    /// <summary>
    /// Check if item can be moved from storage by player
    /// </summary>
    public static bool IsItemRestrictedFrom(Player player, Item item, StorageType storageType)
    {
        if (!IsStorageOpen(player, storageType))
            return true;
        if (storageType == StorageType.LEGION_WAREHOUSE)
        {
            if (!LegionConfig.LEGION_WAREHOUSE || !player.IsLegionMember() || !player.GetLegionMember().HasRights(LegionPermissionsMask.WH_WITHDRAWAL))
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_GUILD_WAREHOUSE_NO_RIGHT());
                return true;
            }
            if (!IsLegionWarehouseUser(player))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Check if item can be moved to storage by player
    /// </summary>
    public static bool IsItemRestrictedTo(Player player, Item item, StorageType storageType)
    {
        if (!IsStorageOpen(player, storageType))
            return true;
        if (storageType == StorageType.REGULAR_WAREHOUSE)
        {
            if (!item.IsStorableInWarehouse())
            {
                // You cannot store this in the warehouse.
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_WAREHOUSE_CANT_DEPOSIT_ITEM());
                return true;
            }
        }
        else if (storageType == StorageType.ACCOUNT_WAREHOUSE)
        {
            if (!item.IsStorableInAccWarehouse())
            {
                // You cannot store this item in the account warehouse.
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_WAREHOUSE_CANT_ACCOUNT_DEPOSIT());
                return true;
            }
        }
        else if (storageType == StorageType.LEGION_WAREHOUSE)
        {
            if (!item.IsStorableInLegWarehouse() || !LegionConfig.LEGION_WAREHOUSE)
            {
                // You cannot store this item in the Legion warehouse.
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_WAREHOUSE_CANT_LEGION_DEPOSIT());
                return true;
            }
            else if (!player.IsLegionMember() || (!player.GetLegionMember().HasRights(LegionPermissionsMask.WH_DEPOSIT)
                && !player.GetLegionMember().HasRights(LegionPermissionsMask.WH_WITHDRAWAL)))
            {
                // You do not have the authority to use the Legion warehouse.
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_GUILD_WAREHOUSE_NO_RIGHT());
                return true;
            }
            if (!IsLegionWarehouseUser(player))
                return true;
        }

        return false;
    }

    public static bool IsStorageOpen(Player player, StorageType storageType)
    {
        if (storageType == StorageType.REGULAR_WAREHOUSE || storageType == StorageType.ACCOUNT_WAREHOUSE || storageType == StorageType.LEGION_WAREHOUSE)
            return IsAtStorageManager(player, storageType);
        if (storageType.GetId() >= StorageType.PET_BAG_MIN && storageType.GetId() <= StorageType.PET_BAG_MAX)
            return IsPetBagOpen(player, storageType);
        if (storageType.GetId() >= StorageType.HOUSE_WH_MIN && storageType.GetId() <= StorageType.HOUSE_WH_MAX)
            return IsCabinetOpen(player, storageType);
        return true;
    }

    private static bool IsCabinetOpen(Player player, StorageType storageType)
    {
        var house = player.GetActiveHouse();
        if (house != null)
        {
            int warehouseId = storageType.GetId() - StorageType.HOUSE_WH_MIN + 1;
            foreach (HouseObject obj in house.GetRegistry().GetSpawnedObjects())
                if (obj is StorageObject cabinet && cabinet.GetObjectTemplate().GetWarehouseId() == warehouseId && cabinet.IsOccupiedBy(player)
                    && PositionUtil.IsInTalkRange(player, cabinet))
                    return true;
        }
        AuditLogger.Log(player, "tried to use " + storageType + " without using the cabinet");
        return false;
    }

    private static bool IsPetBagOpen(Player player, StorageType storageType)
    {
        var warehouse = player.GetPet() == null ? null : player.GetPet().GetObjectTemplate().GetWarehouseFunction();
        if (warehouse != null && warehouse.GetPetBagStorageId() == storageType.GetId())
            return true;
        AuditLogger.Log(player, "tried to use " + storageType + " without the pet that carries it");
        return false;
    }

    public static bool IsAtStorageManager(Player player, StorageType storageType)
    {
        if (player.GetStorageManager(storageType) is Npc npc && PositionUtil.IsInTalkRange(player, npc))
            return true;
        AuditLogger.Log(player, "tried to use " + storageType + " away from a storage manager NPC");
        return false;
    }

    public static bool IsLegionWarehouseUser(Player player)
    {
        if (player.IsLegionMember() && player.GetLegion().GetLegionWarehouse().GetCurrentUser() == player.GetObjectId())
            return true;
        AuditLogger.Log(player, "tried to use the legion warehouse without opening it");
        return false;
    }

    /// <summary>Check whether the item can be removed</summary>
    public static bool CanRemoveItem(Player player, Item item)
    {
        ItemTemplate it = item.GetItemTemplate();
        if (it.GetItemGroup().Equals(ItemGroup.QUEST))
        {
            // TODO: not removable, if quest status start and quest can not be abandoned
            // Waiting for quest data reparse
            return true;
        }
        return true;
    }
}
