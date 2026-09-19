using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
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
        unchecked((ushort)item.GetEquipmentSlot()), item.GetItemTemplate().IsCloth(), ReadDetails(item));

    private static DetailsState ReadDetails(Item item)
    {
        var template = item.GetItemTemplate();
        bool equipable = template.GetItemGroup().GetValidEquipmentSlots() != 0;
        bool identified = item.IsIdentified();
        var enchantment = equipable ? new EnchantmentState(item.IsSoulBound(), unchecked((byte)item.GetEnchantLevel()),
            item.GetItemSkinTemplate().GetTemplateId(), unchecked((byte)(identified ? item.GetOptionalSockets() : -1)),
            unchecked((byte)(identified ? item.GetEnchantBonus() : -1)),
            ReadStones(item.HasManaStones() ? item.GetItemStones() : Array.Empty<ManaStone>()),
            item.GetGodStoneId(), unchecked((byte)item.GetTempering()), item.IsAmplified(), item.GetBuffSkill()) : null;
        var fusion = item.HasFusionedItem() || template.IsTwoHandWeapon() ? new FusionState(item.GetFusionedItemId(),
            ReadStones(item.HasFusionStones() ? item.GetFusionStones() : Array.Empty<ManaStone>()),
            unchecked((byte)item.GetFusionedItemOptionalSockets()), unchecked((byte)item.GetFusionedItemBonusStatsId())) : null;
        return new DetailsState(enchantment, fusion, equipable ? (item.IsEquipped() ? item.GetEquipmentSlot() : 0) : null,
            equipable && item.GetConditioningInfo() != null ? item.GetChargePoints() : null,
            equipable ? new PremiumState(unchecked((byte)(identified ? item.GetBonusStatsId() : -1)),
                unchecked((byte)(identified ? item.GetTuneCount() : 0))) : null,
            unchecked((byte)item.GetPackCount()));
    }

    private static StoneSlots ReadStones(IEnumerable<ManaStone> stones)
    {
        // Do not call Item's lazy collection getters for an empty set during read-only inspection.
        var slots = stones.ToDictionary(stone => stone.GetSlot(), stone => stone.GetItemId());
        return new(slots.GetValueOrDefault(0), slots.GetValueOrDefault(1), slots.GetValueOrDefault(2),
            slots.GetValueOrDefault(3), slots.GetValueOrDefault(4), slots.GetValueOrDefault(5));
    }

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
        ushort ItemMask, string Creator, ushort EquipmentSlot, bool Cloth, DetailsState Details);

    public sealed record DetailsState(EnchantmentState? Enchantment, FusionState? Fusion, long? EquippedSlot,
        int? ChargePoints, PremiumState? Premium, byte PackCount);
    public sealed record EnchantmentState(bool SoulBound, byte EnchantLevel, int SkinId, byte OptionalSockets,
        byte EnchantBonus, StoneSlots Manastones, int GodstoneId, byte Tempering, bool Amplified, int BuffSkill);
    public sealed record FusionState(int ItemId, StoneSlots Manastones, byte OptionalSockets, byte BonusStatsId);
    public sealed record PremiumState(byte BonusStatsId, byte TuneCount);
    public sealed record StoneSlots(int Slot0, int Slot1, int Slot2, int Slot3, int Slot4, int Slot5);

}
