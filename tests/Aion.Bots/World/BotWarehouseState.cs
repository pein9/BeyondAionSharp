using Aion.Bots.Protocol;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.World;

public sealed partial class BotWorldModel
{
	private readonly Dictionary<byte, BotWarehouseState> warehouses = [];
	private readonly HashSet<byte> warehouseSnapshots = [];
	public IReadOnlyDictionary<byte, BotWarehouseState> Warehouses => warehouses;

	private void ApplyWarehouse(DecodedBotServerPacket packet)
	{
		byte storage = packet.Get<byte>("warehouseType");
		if (!warehouses.TryGetValue(storage, out var warehouse))
			warehouses[storage] = warehouse = new BotWarehouseState(storage);
		if (packet.PacketType == typeof(SM_WAREHOUSE_INFO))
		{
			var items = packet.Get<List<BotInventoryItem>>("items");
			bool first = packet.Get<bool>("firstPacket");
			warehouse.ExpansionLevel = packet.Get<byte>("expandLevel");
			if (first)
			{
				warehouse.MutableItems.Clear();
				warehouseSnapshots.Add(storage);
			}
			if (items.Count == 0 && !first)
			{
				// Java WarehouseService sends an empty regular warehouse without a first packet.
				// Its account terminator is also sent during regular-only expansion refreshes, so
				// an account terminator on its own must NOT erase the existing account snapshot.
				if (!warehouseSnapshots.Remove(storage) && storage == 1)
					warehouse.MutableItems.Clear();
			}
			foreach (var item in items) warehouse.MutableItems[item.ObjectId] = item;
		}
		else if (packet.PacketType == typeof(SM_WAREHOUSE_ADD_ITEM))
		{
			foreach (var item in packet.Get<List<BotInventoryItem>>("items")) warehouse.MutableItems[item.ObjectId] = item;
		}
		else if (packet.PacketType == typeof(SM_DELETE_WAREHOUSE_ITEM))
			warehouse.MutableItems.Remove(packet.Get<int>("itemObjectId"));
		else if (warehouse.MutableItems.TryGetValue(packet.Get<int>("objectId"), out var existing))
			warehouse.MutableItems[existing.ObjectId] = existing with
			{
				Description = packet.Get<string>("desc"), Count = packet.Get<long>("itemCount"),
				ItemMask = packet.Get<ushort>("itemMask"), Creator = packet.Get<string>("itemCreator"),
				Details = existing.Details.Merge(packet.Get<BotItemDetails>("details"))
			};
	}
}

/// <summary>Packet-derived warehouse contents. Expansion and capacity apply only to character storage.</summary>
public sealed class BotWarehouseState(byte storageType)
{
	internal Dictionary<int, BotInventoryItem> MutableItems { get; } = [];
	public byte StorageType { get; } = storageType;
	public byte ExpansionLevel { get; internal set; }
	public int? CharacterCapacity => StorageType == 1 ? 24 + 8 * ExpansionLevel : null;
	public IReadOnlyDictionary<int, BotInventoryItem> Items => MutableItems;
	public long Kinah => MutableItems.Values.Where(item => item.ItemId == BotWorldModel.KinahItemId).Sum(item => item.Count);
}
