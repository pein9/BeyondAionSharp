using System.Text.Json;

namespace Aion.Bots.World;

/// <summary>Read-only storage oracle. Comparison never repairs or refreshes the packet-derived client view.</summary>
public static class BotWarehouseOracle
{
	private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
	public static void Verify(int characterId, BotWorldModel world, JsonElement response)
	{
		BotInventoryOracle.Verify(characterId, world.Inventory.Values.ToArray(), world.Kinah, response, world.CubeExpansion);
		var warehouse = response.GetProperty("warehouse");
		foreach (byte storage in new byte[] { 1, 2 })
		{
			string prefix = storage == 1 ? "characterWarehouse" : "accountWarehouse";
			var observed = world.Warehouses[storage];
			var rows = warehouse.GetProperty(prefix + "Items").Deserialize<BotInventoryItem[]>(Options)
				?? throw new InvalidDataException("Missing warehouse oracle rows.");
			Require(rows.Length == rows.Select(item => item.ObjectId).Distinct().Count(), "Warehouse oracle repeated an object ID.");
			Require(rows.Count(item => item.ItemId != BotWorldModel.KinahItemId) == warehouse.GetProperty(prefix + "ItemCount").GetInt32(), "Warehouse row count differs from its summary.");
			Require(observed.Items.Values.OrderBy(item => item.ObjectId).SequenceEqual(rows.OrderBy(item => item.ObjectId)), $"Warehouse {storage} oracle differs from packet-derived contents.");
			if (storage == 1)
				Require(observed.CharacterCapacity == warehouse.GetProperty(prefix + "Limit").GetInt32(), "Character warehouse capacity differs from the oracle.");
			else Require(observed.Kinah == warehouse.GetProperty(prefix + "Kinah").GetInt64(), "Account warehouse kinah differs from the oracle.");
		}
		var identities = world.Inventory.Keys.Concat(world.Warehouses[1].Items.Keys).Concat(world.Warehouses[2].Items.Keys).ToArray();
		Require(identities.Length == identities.Distinct().Count(), "An object ID appears in multiple player storages.");
	}

	private static void Require(bool condition, string message)
	{
		if (!condition) throw new InvalidDataException(message);
	}
}
