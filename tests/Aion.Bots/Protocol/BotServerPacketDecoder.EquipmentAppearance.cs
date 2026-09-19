namespace Aion.Bots.Protocol;

public sealed record BotEquipmentAppearance(uint Slot, int SkinId, int GodstoneId, int? Rgb, ushort Enchant);

public sealed partial class BotServerPacketDecoder
{
	private static IReadOnlyDictionary<string, object?> DecodeEquipmentAppearance(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		int playerObjectId = r.ReadInt32(); uint mask = unchecked((uint)r.ReadInt32());
		int count = System.Numerics.BitOperations.PopCount(mask);
		if (r.Remaining != count * 16) throw new InvalidDataException("Appearance slot mask does not match its equipment rows.");
		var items = new List<BotEquipmentAppearance>(count);
		for (int bit = 0; bit < 32; bit++)
		{
			uint slot = 1u << bit;
			if ((mask & slot) == 0) continue;
			int skin = r.ReadInt32(), godstone = r.ReadInt32(); byte dyed = r.ReadByte();
			int rgb = (r.ReadByte() << 16) | (r.ReadByte() << 8) | r.ReadByte();
			ushort enchant = r.ReadUInt16(), reserved = r.ReadUInt16();
			if (dyed > 1 || (dyed == 0 && rgb != 0) || reserved != 0) throw new InvalidDataException("Invalid equipment appearance flags.");
			items.Add(new(slot, skin, godstone, dyed == 0 ? null : rgb, enchant));
		}
		return Fields(("playerObjectId", playerObjectId), ("slotMask", mask), ("equipment", items.ToArray()));
	}
}
