namespace Aion.Bots.Protocol;

public sealed record BotPassport(int Id, int Stamps, int RewardStatus, int ArriveTime);

public sealed partial class BotServerPacketDecoder
{
	// SM_ATREIAN_PASSPORT at ce54b7931: current stamp count is repeated per row, not in the header.
	private static IReadOnlyDictionary<string, object?> DecodeAtreianPassport(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		ushort year = r.ReadUInt16(), month = r.ReadUInt16(), day = r.ReadUInt16(), count = r.ReadUInt16();
		if (r.Remaining != count * 16) throw new InvalidDataException("Passport row count does not match packet length.");
		if (year is < 1 or > 9999 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
			throw new InvalidDataException("Invalid passport creation date.");
		var passports = new BotPassport[count];
		for (int i = 0; i < count; i++)
		{
			passports[i] = new(r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
			if (passports[i].RewardStatus is < 0 or > 3 || passports[i].Stamps < 0)
				throw new InvalidDataException("Invalid passport status or stamp count.");
		}
		return Fields(("year", year), ("month", month), ("day", day), ("passports", passports));
	}
}
