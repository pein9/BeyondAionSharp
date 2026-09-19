using Aion.Bots.World;

namespace Aion.Bots.Protocol;

/// <summary>4.8 ItemInfoBlob entries, audited against Java ce54b7931. Partial blobs retain presence information.</summary>
public static class BotItemBlobDecoder
{
	public static BotItemBlob Decode(ReadOnlySpan<byte> blob)
	{
		var r = new PacketBodyReader(blob);
		BotItemGeneralInfo? general = null;
		var details = new BotItemDetails();
		var seen = new HashSet<byte>();
		while (r.Remaining > 0)
		{
			byte entry = r.ReadByte();
			// STAT_BONUSES is emitted once per modifier. Other entries are singletons in full and partial blobs.
			if (entry != 0x0A && !seen.Add(entry))
				throw new InvalidDataException($"Duplicate item blob entry 0x{entry:X2}.");
			switch (entry)
			{
				case 0x00:
					general = new BotItemGeneralInfo(r.ReadUInt16(), r.ReadInt64(), r.ReadString());
					r.Skip(21);
					break;
				case 0x06: details = details with { EquippedSlot = r.ReadInt64() }; break;
				case 0x0B: details = details with { Enchantment = ReadEnchantment(ref r) }; break;
				case 0x0E:
					details = details with { Fusion = new BotItemFusion(r.ReadInt32(), ReadStones(ref r), r.ReadByte(), r.ReadByte()) };
					break;
				case 0x0F: details = details with { ChargePoints = r.ReadInt32() }; break;
				case 0x10:
					details = details with { Premium = new BotItemPremium(r.ReadByte(), r.ReadByte()) };
					r.Skip(1);
					break;
				case 0x12: details = details with { PackCount = r.ReadByte() }; break;
				default:
					r.Skip(entry switch
					{
						0x01 => 16, 0x02 => 20, 0x03 => 20, 0x04 => 16, 0x05 => 8,
						0x07 => 306, 0x08 => 4, 0x0A => 7, 0x0D => 16, 0x11 => 4, 0x13 => 32,
						_ => throw new InvalidDataException($"Unknown item blob entry 0x{entry:X2}."),
					});
					break;
			}
		}
		return new BotItemBlob(general, details);
	}

	internal static BotItemEnchantment ReadEnchantment(ref PacketBodyReader r)
	{
		bool soulBound = r.ReadByte() != 0;
		byte enchantLevel = r.ReadByte();
		int skinId = r.ReadInt32();
		byte optionalSockets = r.ReadByte(), enchantBonus = r.ReadByte();
		var stones = ReadStones(ref r);
		int godstoneId = r.ReadInt32();
		r.Skip(18); // dye and idian fields
		byte tempering = r.ReadByte();
		r.Skip(70); // reserved fields and plume bonus stats
		bool amplified = r.ReadByte() != 0;
		int buffSkill = r.ReadInt32();
		r.Skip(8);
		return new(soulBound, enchantLevel, skinId, optionalSockets, enchantBonus, stones, godstoneId, tempering, amplified, buffSkill);
	}

	private static BotStoneSlots ReadStones(ref PacketBodyReader r) => new(
		r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
}

public sealed record BotItemBlob(BotItemGeneralInfo? General, BotItemDetails Details);
public readonly record struct BotItemGeneralInfo(ushort ItemMask, long ItemCount, string Creator);
