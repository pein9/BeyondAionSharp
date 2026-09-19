using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Collections;

namespace Aion.GameServer.Services.Admin;

/// <summary>Administrative inventory inspection and the explicitly requested refresh operation.</summary>
internal static class AdminInventory
{
    public static InventoryState Refresh(Player player)
    {
        player.SetCubeLimit();
        Storage inventory = player.GetInventory();
        if (inventory.GetKinah() == 0)
            inventory.IncreaseKinah(0);

        List<Item> allItems = new List<Item>();
        var kinahItem = inventory.GetKinahItem();
        if (kinahItem != null)
            allItems.Add(kinahItem);
        var equippedItems = player.GetEquipment().GetEquippedItems();
        allItems.AddRange(equippedItems);
        allItems.AddRange(inventory.GetItems());

        var inventoryItemSplitList = new FixedElementCountSplitList<Item>(allItems, true, 10);
        inventoryItemSplitList.ForEach(part => PacketSendUtility.SendPacket(player, new SM_INVENTORY_INFO(part.IsFirst(), part, player)));
        PacketSendUtility.SendPacket(player, new SM_INVENTORY_INFO(false, new List<Item>(), player));
        PacketSendUtility.SendPacket(player, SM_CUBE_UPDATE.CubeSize(StorageType.CUBE, player));

        return SnapshotInventory(player, inventory, equippedItems.Count, allItems.Count);
    }

    public static InventoryState Read(Player player)
    {
        Storage inventory = player.GetInventory();
        int equippedItemCount = player.GetEquipment().GetEquippedItems().Count;
        int totalPacketItemCount = equippedItemCount + inventory.GetItems().Count + (inventory.GetKinahItem() == null ? 0 : 1);
        return SnapshotInventory(player, inventory, equippedItemCount, totalPacketItemCount);
    }

    private static InventoryState SnapshotInventory(Player player, Storage inventory, int equippedItemCount, int totalPacketItemCount)
    {
        // Read-only: include the same three ownership sets as a full inventory packet, without sending
        // a refresh or creating a zero-kinah item. Scalar copies keep serialization independent of later edits.
        var items = inventory.GetItems().Concat(player.GetEquipment().GetEquippedItems()).ToList();
        if (inventory.GetKinahItem() is { } kinahItem)
            items.Add(kinahItem);
        return new InventoryState
        {
            CubeItemCount = inventory.Size(),
            EquippedItemCount = equippedItemCount,
            TotalPacketItemCount = totalPacketItemCount,
            CubeLimit = inventory.GetLimit(),
            CubeFreeSlots = inventory.GetFreeSlots(),
            Kinah = inventory.GetKinah(),
            Items = items.OrderBy(item => item.GetObjectId()).Select(SnapshotInventoryItem).ToArray()
        };
    }

    private static ItemState SnapshotInventoryItem(Item item) => new(
        item.GetObjectId(), item.GetItemId(), item.GetItemTemplate().GetL10n() ?? string.Empty, item.GetItemCount(),
        unchecked((ushort)item.GetItemMask()), item.GetItemCreator(),
        unchecked((ushort)item.GetEquipmentSlot()), item.GetItemTemplate().IsCloth());

    public sealed class InventoryState
    {
        public int CubeItemCount { get; set; }
        public int EquippedItemCount { get; set; }
        public int TotalPacketItemCount { get; set; }
        public int CubeLimit { get; set; }
        public int CubeFreeSlots { get; set; }
        public long Kinah { get; set; }
        public IReadOnlyList<ItemState> Items { get; set; } = Array.Empty<ItemState>();
    }

    public sealed record ItemState(int ObjectId, int ItemId, string Description, long Count,
        ushort ItemMask, string Creator, ushort EquipmentSlot, bool Cloth);

}
