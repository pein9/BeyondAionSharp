using System.Text.Json;
using System.Text.Json.Nodes;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotWarehouseOracleTests
{
	[Fact]
	public void CompleteMatchingStoragePassesWithoutMutatingClientState()
	{
		var (world, response) = Create();
		var regular = world.Warehouses[1].Items.Values.ToArray();
		var account = world.Warehouses[2].Items.Values.ToArray();
		Verify(world, response);
		Assert.Equal(regular, world.Warehouses[1].Items.Values);
		Assert.Equal(account, world.Warehouses[2].Items.Values);
	}

	[Theory]
	[InlineData("item-count")]
	[InlineData("kinah")]
	[InlineData("capacity")]
	[InlineData("row-quantity")]
	[InlineData("gear")]
	[InlineData("wrong-owner")]
	[InlineData("duplicate")]
	public void CorruptedIndependentServerViewFails(string mutation)
	{
		var (world, response) = Create();
		var warehouse = response["warehouse"]!;
		switch (mutation)
		{
			case "item-count": warehouse["characterWarehouseItemCount"] = 2; break;
			case "kinah": warehouse["accountWarehouseKinah"] = 1; break;
			case "capacity": warehouse["characterWarehouseLimit"] = 24; break;
			case "row-quantity": warehouse["characterWarehouseItems"]![0]!["count"] = 1; break;
			case "gear": warehouse["characterWarehouseItems"]![0]!["details"]!["packCount"] = 3; break;
			case "wrong-owner": response["recipientCharacterId"] = 43; break;
			case "duplicate": warehouse["accountWarehouseItems"]!.AsArray().Add(warehouse["accountWarehouseItems"]![0]!.DeepClone()); break;
		}
		Assert.Throws<InvalidDataException>(() => Verify(world, response));
	}

	private static (BotWorldModel, JsonObject) Create()
	{
		var world = new BotWorldModel();
		var item = new BotInventoryItem(10, 152000102, "ore", 5, 4222, "", 0, false) { Details = BotItemDetails.Empty };
		var money = new BotInventoryItem(20, BotWorldModel.KinahItemId, "kinah", 5_000_000_001L, 0, "", ushort.MaxValue, false) { Details = BotItemDetails.Empty };
		foreach (var (type, expansion, rows) in new[] { ((byte)1, (byte)1, new List<BotInventoryItem> { item }), ((byte)2, (byte)0, new List<BotInventoryItem> { money }) })
			world.Apply(new DecodedBotServerPacket(typeof(SM_WAREHOUSE_INFO), new Dictionary<string, object?>
			{
				["warehouseType"] = type, ["firstPacket"] = true, ["expandLevel"] = expansion, ["items"] = rows
			}));
		return (world, JsonSerializer.SerializeToNode(new
		{
			ok = true, online = true, recipientCharacterId = 42,
			inventory = new { kinah = 0, totalPacketItemCount = 0, items = Array.Empty<BotInventoryItem>() },
			warehouse = new
			{
				characterWarehouseItemCount = 1, characterWarehouseLimit = 32, characterWarehouseItems = new[] { item },
				accountWarehouseItemCount = 0, accountWarehouseKinah = money.Count, accountWarehouseItems = new[] { money }
			}
		}, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject());
	}
	private static void Verify(BotWorldModel world, JsonObject response) => BotWarehouseOracle.Verify(42, world, JsonSerializer.SerializeToElement(response));
}
