namespace Aion.Bots.Protocol;

public sealed partial class BotServerPacketDecoder
{
	// Java ce54b7931 SM_BIND_POINT_INFO.writeImpl: C C D F F F D.
	private static IReadOnlyDictionary<string, object?> DecodeBindPoint(ReadOnlySpan<byte> body)
	{
		if (body.Length != 22) throw new InvalidDataException("Bind point must contain exactly 22 bytes.");
		var r = new PacketBodyReader(body);
		byte type = r.ReadByte(), reserved = r.ReadByte();
		if (type is not (0 or 4) || reserved != 1) throw new InvalidDataException("Unknown bind point header.");
		return Fields(("bindPointType", type), ("mapId", r.ReadInt32()), ("x", r.ReadSingle()),
			("y", r.ReadSingle()), ("z", r.ReadSingle()), ("kiskObjectId", r.ReadInt32()));
	}

	// Java ce54b7931 SM_KISK_UPDATE.writeImpl: eight D fields, lifetime in seconds.
	private static IReadOnlyDictionary<string, object?> DecodeKiskUpdate(ReadOnlySpan<byte> body)
	{
		if (body.Length != 32) throw new InvalidDataException("Kisk update must contain exactly eight int32 fields.");
		var r = new PacketBodyReader(body);
		return Fields(("objectId", r.ReadInt32()), ("creatorId", r.ReadInt32()), ("useMask", r.ReadInt32()),
			("currentMembers", r.ReadInt32()), ("maxMembers", r.ReadInt32()), ("remainingResurrects", r.ReadInt32()),
			("maxResurrects", r.ReadInt32()), ("remainingLifetimeSeconds", r.ReadInt32()));
	}
}
