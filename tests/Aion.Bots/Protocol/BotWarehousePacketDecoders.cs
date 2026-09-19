using Aion.Bots.World;

namespace Aion.Bots.Protocol;

public sealed partial class BotServerPacketDecoder
{
	private static IReadOnlyDictionary<string, object?> DecodeWarehouseInfo(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		byte storage = r.ReadByte(), first = r.ReadByte(), expansion = r.ReadByte();
		if (first > 1) throw new InvalidDataException("Invalid warehouse first-packet flag.");
		ushort flags = r.ReadUInt16();
		var items = ReadWarehouseItems(ref r, r.ReadUInt16());
		RequireWarehouseEnd(ref r);
		return Fields(("warehouseType", storage), ("firstPacket", first != 0), ("expandLevel", expansion),
			("flags", flags), ("items", items));
	}

	private static IReadOnlyDictionary<string, object?> DecodeWarehouseAdd(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		byte storage = r.ReadByte();
		ushort mask = r.ReadUInt16();
		var items = ReadWarehouseItems(ref r, r.ReadUInt16());
		RequireWarehouseEnd(ref r);
		return Fields(("warehouseType", storage), ("addMask", mask), ("items", items));
	}

	private static IReadOnlyDictionary<string, object?> DecodeWarehouseUpdate(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		int objectId = r.ReadInt32();
		byte storage = r.ReadByte();
		string description = r.ReadString();
		var blob = r.ReadLengthPrefixedBlob();
		var info = BotItemBlobDecoder.Decode(blob);
		var general = info.General ?? throw new InvalidDataException("Warehouse update has no general item information.");
		ushort? mask = r.Remaining == 2 ? r.ReadUInt16() : null;
		RequireWarehouseEnd(ref r);
		// Unlike inventory updates, GENERAL_INFO here is partial: absent gear entries must not clear gear.
		return Fields(("objectId", objectId), ("warehouseType", storage), ("desc", description), ("blob", blob),
			("itemMask", general.ItemMask), ("itemCount", general.ItemCount), ("itemCreator", general.Creator),
			("details", info.Details), ("updateMask", mask));
	}

	private static IReadOnlyDictionary<string, object?> DecodeWarehouseDelete(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("warehouseType", r.ReadByte()), ("itemObjectId", r.ReadInt32()), ("deleteType", r.ReadByte()));
		RequireWarehouseEnd(ref r);
		return fields;
	}

	private static List<BotInventoryItem> ReadWarehouseItems(ref PacketBodyReader r, int count)
	{
		var items = new List<BotInventoryItem>(count);
		for (int i = 0; i < count; i++)
		{
			int objectId = r.ReadInt32(), itemId = r.ReadInt32();
			r.Skip(1); // Warehouse rows have an item-info byte before the description, but no cloth byte.
			string description = r.ReadString();
			var info = BotItemBlobDecoder.Decode(r.ReadLengthPrefixedBlob());
			var general = info.General ?? throw new InvalidDataException("Warehouse item has no general information.");
			items.Add(new(objectId, itemId, description, general.ItemCount, general.ItemMask, general.Creator, r.ReadUInt16(), false)
			{
				Details = info.Details with { PackCount = info.Details.PackCount ?? 0 }
			});
		}
		return items;
	}

	private static void RequireWarehouseEnd(ref PacketBodyReader r)
	{
		if (r.Remaining != 0) throw new InvalidDataException("Warehouse packet has unexpected trailing bytes.");
	}
}
