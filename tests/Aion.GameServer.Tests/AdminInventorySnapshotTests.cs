using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Services.Admin;
using Aion.Bots.World;
using Aion.GameServer.Model.Items;

namespace Aion.GameServer.Tests;

public sealed class AdminInventorySnapshotTests
{
    [Fact]
    public void SnapshotIncludesCubeDistinctEquipmentAndKinahAsImmutableScalarRows()
    {
        var (player, storage, equipment) = CreateStorageGraph();
        var cube = Item(20, 162000001, 3, 4);
        var money = Item(10, 182400001, 5_000_000_001, -1);
        var weapon = Item(30, 100000001, 1, 0x10001);
        weapon.SetItemCreator("Crafter");
        storage.OnLoadHandler(cube); storage.OnLoadHandler(money);
        // A two-handed equipment item occupies multiple keys but is sent once.
        equipment[1] = weapon; equipment[2] = weapon;
        var snapshot = Snapshot(player);
        cube.SetItemCount(99); // Serialization must not read later mutations through live Item references.
        var json = JsonSerializer.SerializeToElement(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(1, json.GetProperty("cubeItemCount").GetInt32());
        Assert.Equal(1, json.GetProperty("equippedItemCount").GetInt32());
        Assert.Equal(3, json.GetProperty("totalPacketItemCount").GetInt32());
        Assert.Equal(5_000_000_001, json.GetProperty("kinah").GetInt64());
        var rows = json.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(3, rows.Length);
        Assert.Equal(new[] { 10, 20, 30 }, rows.Select(r => r.GetProperty("objectId").GetInt32()));
        Assert.Equal(3, rows[1].GetProperty("count").GetInt64());
        Assert.Equal(162000001, rows[1].GetProperty("itemId").GetInt32());
        Assert.Equal(cube.GetL10n(), rows[1].GetProperty("description").GetString());
        Assert.Equal(1, rows[1].GetProperty("itemMask").GetUInt16());
        Assert.Equal(65535, rows[0].GetProperty("equipmentSlot").GetUInt16());
        Assert.Equal(1, rows[2].GetProperty("equipmentSlot").GetUInt16());
        Assert.Equal("Crafter", rows[2].GetProperty("creator").GetString());
        Assert.False(rows[2].GetProperty("cloth").GetBoolean());
        Assert.Same(money, storage.GetKinahItem());
        Assert.Equal(2, equipment.Count);
    }

    [Fact]
    public void ZeroDescriptionIdIsReportedAsTheEmptyWireString()
    {
        var (player, storage, _) = CreateStorageGraph();
        var item = Item(20, 162000001, 3, 4);
        item.GetItemTemplate().description = 0;
        Assert.Null(item.GetItemTemplate().GetL10n());
        storage.OnLoadHandler(item);

        var json = JsonSerializer.SerializeToElement(Snapshot(player), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var rows = json.GetProperty("items").EnumerateArray().ToArray();
        Assert.Single(rows);
        Assert.Equal(string.Empty, rows[0].GetProperty("description").GetString());
    }

    [Fact]
    public void GearReadDoesNotInitializeStoneCollectionsAndCopiesClientVisibleState()
    {
        var (player, _, equipment) = CreateStorageGraph();
        var item = Item(40, 100000094, 1, (1L << 40) | 2);
        item.GetItemTemplate().itemGroup = ItemGroup.GREATSWORD;
        item.SetEquipped(true); item.SetSoulBound(true); item.SetEnchantLevel(7);
        item.SetOptionalSockets(3); item.SetEnchantBonus(4); item.SetTuneCount(-1);
        item.SetTempering(5); item.SetAmplified(true); item.SetBuffSkill(1234); item.SetPackCount(2);
        item.SetItemSkinTemplate(new ItemTemplate { itemId = 100000123 });
        typeof(Item).GetField("conditioningInfo", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(item, new ChargeInfo(543210, item));
        equipment[item.GetEquipmentSlot()] = item;
        var snapshot = Snapshot(player);
        item.SetEnchantLevel(15); item.SetTuneCount(2); item.SetPackCount(0);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var row = Assert.Single(JsonSerializer.SerializeToElement(snapshot, options).GetProperty("items").EnumerateArray());
        var details = row.GetProperty("details").Deserialize<BotItemDetails>(options);
        Assert.Equal(new BotItemDetails(new(true, 7, 100000123, 255, 255, BotStoneSlots.Empty, 0, 5, true, 1234),
            new(0, BotStoneSlots.Empty, 0, 0), (1L << 40) | 2, 543210, new(255, 0), 2), details);
        Assert.Null(typeof(Item).GetField("manaStones", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(item));
        Assert.Null(typeof(Item).GetField("fusionStones", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(item));

        row = Assert.Single(JsonSerializer.SerializeToElement(Snapshot(player), options).GetProperty("items").EnumerateArray());
        details = row.GetProperty("details").Deserialize<BotItemDetails>(options);
        Assert.Equal(new BotItemPremium(0, 2), details!.Premium);
        Assert.Equal((byte)3, details.Enchantment!.OptionalSockets);
        Assert.Equal((byte)4, details.Enchantment.EnchantBonus);
        Assert.Equal((byte)0, details.PackCount);
    }

    [Fact]
    public void EmptySnapshotDoesNotCreateKinahOrChangeStorage()
    {
        var (player, storage, equipment) = CreateStorageGraph();
        var json = JsonSerializer.SerializeToElement(Snapshot(player), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Empty(json.GetProperty("items").EnumerateArray());
        Assert.Equal(0, json.GetProperty("kinah").GetInt64());
        Assert.Null(storage.GetKinahItem());
        Assert.Empty(storage.GetItems()); Assert.Empty(equipment);
    }

    [Fact]
    public void WarehouseRowsAreReadOnlyScalarCopiesWithKinahOnlyWhenRequested()
    {
        var (_, storage, _) = CreateStorageGraph();
        var read = typeof(AdminHttpService).Assembly.GetType("Aion.GameServer.Services.Admin.AdminInventory")!.GetMethod("ReadWarehouse")!;
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.Empty(JsonSerializer.SerializeToElement(read.Invoke(null, [storage, true]), options).EnumerateArray());
        Assert.Null(storage.GetKinahItem());
        var item = Item(20, 152000102, 3, 4);
        var money = Item(10, 182400001, 5_000_000_001, -1);
        storage.OnLoadHandler(item); storage.OnLoadHandler(money);
        var regular = read.Invoke(null, [storage, false]);
        var account = read.Invoke(null, [storage, true]);
        item.SetItemCount(99);
        var regularRows = JsonSerializer.SerializeToElement(regular, options).EnumerateArray().ToArray();
        var accountRows = JsonSerializer.SerializeToElement(account, options).EnumerateArray().ToArray();
        Assert.Equal(3, Assert.Single(regularRows).GetProperty("count").GetInt64());
        Assert.Equal(2, accountRows.Length);
        Assert.Equal(5_000_000_001, accountRows[0].GetProperty("count").GetInt64());
        Assert.Equal(3, accountRows[1].GetProperty("count").GetInt64());
        Assert.All(accountRows, row => Assert.False(row.GetProperty("cloth").GetBoolean()));
        Assert.Same(money, storage.GetKinahItem());
        Assert.Single(storage.GetItems());
    }

    private static (Player, PlayerStorage, SortedDictionary<long, Item>) CreateStorageGraph()
    {
        // Only storage is observed; no server boot, connection, timers or stat-dependent equip action needed.
        var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        var storage = new PlayerStorage(player, StorageType.CUBE);
        storage.SetLimit(27);
        var equipment = new Equipment(player);
        typeof(Player).GetField("inventory", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player, storage);
        typeof(Player).GetField("equipment", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player, equipment);
        return (player, storage, (SortedDictionary<long, Item>)typeof(Equipment)
            .GetField("equipment", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(equipment)!);
    }

    private static Item Item(int objectId, int itemId, long count, long slot) => new(objectId,
        new ItemTemplate { itemId = itemId, mask = 0x10001, description = 123, itemGroup = ItemGroup.NONE, maxTuneCount = 0 }, count, false, slot);

    private static object Snapshot(Player player) => typeof(AdminHttpService).Assembly
        .GetType("Aion.GameServer.Services.Admin.AdminInventory")!.GetMethod("Read")!.Invoke(null, [player])!;
}
