using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Services.Admin;

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
    public void EmptySnapshotDoesNotCreateKinahOrChangeStorage()
    {
        var (player, storage, equipment) = CreateStorageGraph();
        var json = JsonSerializer.SerializeToElement(Snapshot(player), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Empty(json.GetProperty("items").EnumerateArray());
        Assert.Equal(0, json.GetProperty("kinah").GetInt64());
        Assert.Null(storage.GetKinahItem());
        Assert.Empty(storage.GetItems()); Assert.Empty(equipment);
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
