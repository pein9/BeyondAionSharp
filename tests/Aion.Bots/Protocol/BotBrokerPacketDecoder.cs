using Aion.Bots.World;

namespace Aion.Bots.Protocol;

public sealed partial class BotServerPacketDecoder
{
	private static IReadOnlyDictionary<string, object?> DecodeBroker(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		byte action = r.ReadByte();
		var fields = Fields(("action", action));
		switch (action)
		{
			case 0:
				fields["totalCount"] = r.ReadInt32(); r.Skip(1);
				fields["page"] = r.ReadUInt16();
				fields["items"] = ReadBrokerListings(ref r, r.ReadUInt16(), registered: false);
				break;
			case 1:
				r.Skip(4);
				fields["items"] = ReadBrokerListings(ref r, r.ReadUInt16(), registered: true);
				break;
			case 3:
				byte message = r.ReadByte(); fields["message"] = message;
				if (message == 0)
				{
					fields["position"] = r.ReadByte();
					fields["items"] = ReadBrokerListings(ref r, 1, registered: true);
				}
				else r.Skip(183); // Java's fixed-size registration refusal padding.
				break;
			case 4:
				fields["result"] = r.ReadByte(); fields["objectId"] = r.ReadInt32();
				break;
			case 5:
				fields["settledKinah"] = r.ReadInt64(); fields["totalCount"] = r.ReadInt32();
				fields["page"] = r.ReadUInt16();
				byte clear = r.ReadByte();
				if (clear > 1) throw new InvalidDataException("Invalid broker settlement list flag.");
				fields["iconOnly"] = clear == 1;
				var settled = new BotBrokerSettlement[r.ReadUInt16()];
				if (clear == 1 && settled.Length != 0) throw new InvalidDataException("Broker icon notification contains settlement rows.");
				for (int i = 0; i < settled.Length; i++)
				{
					int itemId = r.ReadInt32(); long proceeds = r.ReadInt64(), count = r.ReadInt64(), repeatedCount = r.ReadInt64();
					int time = r.ReadInt32();
					var enchantment = BotItemBlobDecoder.ReadEnchantment(ref r);
					settled[i] = new(itemId, proceeds, count, repeatedCount, time, enchantment, r.ReadString());
				}
				fields["settlements"] = settled;
				break;
			case 6:
				if (r.ReadByte() != 0) throw new InvalidDataException("Invalid broker icon removal padding.");
				break;
			case 7:
				fields["result"] = r.ReadByte(); fields["objectId"] = r.ReadInt32(); r.Skip(8);
				fields["averageType"] = r.ReadByte(); fields["lowestPrice"] = r.ReadInt64(); fields["highestPrice"] = r.ReadInt64();
				break;
			default: throw new InvalidDataException($"Unknown broker service action {action}.");
		}
		if (r.Remaining != 0) throw new InvalidDataException("Broker service has unexpected trailing bytes.");
		return fields;
	}

	private static BotBrokerListing[] ReadBrokerListings(ref PacketBodyReader r, int count, bool registered)
	{
		var rows = new BotBrokerListing[count];
		for (int i = 0; i < count; i++)
		{
			int objectId = r.ReadInt32(), itemId = r.ReadInt32();
			long totalPrice = r.ReadInt64(), averageOrCount = r.ReadInt64(), itemCount = r.ReadInt64();
			byte? daysLeft = registered ? r.ReadByte() : null;
			var enchantment = BotItemBlobDecoder.ReadEnchantment(ref r);
			string? seller = registered ? null : r.ReadString();
			string creator = r.ReadString();
			r.Skip(3);
			// These two entries are written without their ItemInfoBlob entry IDs in this packet.
			int polish = r.ReadInt32(); byte wraps = r.ReadByte(), splitting = r.ReadByte();
			if (splitting > 1) throw new InvalidDataException("Invalid broker splitting flag.");
			rows[i] = new(objectId, itemId, totalPrice, itemCount, registered ? null : averageOrCount,
				registered ? averageOrCount : null, daysLeft, seller, creator, enchantment, polish, wraps, splitting != 0);
		}
		return rows;
	}
}
