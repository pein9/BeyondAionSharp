using Aion.Bots.World;

namespace Aion.Bots.Protocol;

public sealed partial class BotServerPacketDecoder
{
	private static IReadOnlyDictionary<string, object?> DecodePrivateStore(ReadOnlySpan<byte> body)
	{
		// SM_PRIVATE_STORE(null, player) has an intentionally empty body.
		if (body.IsEmpty) return Fields(("hasStore", false));
		var r = new PacketBodyReader(body);
		int seller = r.ReadInt32();
		var rows = new BotPrivateStoreListing[r.ReadUInt16()];
		for (int i = 0; i < rows.Length; i++)
		{
			int objectId = r.ReadInt32(), itemId = r.ReadInt32();
			ushort count = r.ReadUInt16(); long price = r.ReadInt64();
			var blob = BotItemBlobDecoder.Decode(r.ReadLengthPrefixedBlob());
			rows[i] = new(objectId, itemId, count, price,
				blob.General ?? throw new InvalidDataException("Private-store item is missing general information."),
				blob.Details with { PackCount = blob.Details.PackCount ?? 0 });
		}
		if (r.Remaining != 0) throw new InvalidDataException("Trailing private-store bytes.");
		return Fields(("hasStore", true), ("sellerObjectId", seller), ("items", rows));
	}
	private static IReadOnlyDictionary<string, object?> DecodePrivateStoreName(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("sellerObjectId", r.ReadInt32()), ("name", r.ReadString()));
		if (r.Remaining != 0) throw new InvalidDataException("Trailing private-store name bytes.");
		return fields;
	}
}
