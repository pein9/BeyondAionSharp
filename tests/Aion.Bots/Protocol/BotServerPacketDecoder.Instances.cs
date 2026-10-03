using Aion.Bots.World;

namespace Aion.Bots.Protocol;

public sealed partial class BotServerPacketDecoder
{
	// Java SM_INSTANCE_INFO: signed negative entry offset, cooldown IDs (not copy IDs),
	// optional team rows, UTF-16 names and update types 0/1/2.
	private static IReadOnlyDictionary<string, object?> DecodeInstanceInfo(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		byte update = r.ReadByte();
		int cooldownId = r.ReadInt32();
		r.ReadByte();
		var entries = new List<BotInstanceEntry>();
		int players = r.ReadUInt16();
		for (int p = 0; p < players; p++)
		{
			int player = r.ReadInt32(), count = r.ReadUInt16();
			for (int i = 0; i < count; i++)
			{
				int id = r.ReadInt32(); r.ReadInt32();
				int reuseSeconds = r.ReadInt32(), maxEntries = r.ReadInt32(), offset = r.ReadInt32();
				entries.Add(new(player, id, reuseSeconds, maxEntries, checked(-offset), r.ReadByte() != 0));
			}
			r.ReadString();
		}
		if (r.Remaining != 0 || update > 2) throw new InvalidDataException("Invalid instance-info body.");
		return Fields(("updateType", update), ("cooldownId", cooldownId), ("entries", entries));
	}
}
