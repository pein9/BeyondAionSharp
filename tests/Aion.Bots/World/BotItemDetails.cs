namespace Aion.Bots.World;

/// <summary>Value-semantic item fields observed from client-visible blobs, never from server state.</summary>
public sealed record BotItemDetails(BotItemEnchantment? Enchantment = null, BotItemFusion? Fusion = null,
	long? EquippedSlot = null, int? ChargePoints = null, BotItemPremium? Premium = null, byte? PackCount = null)
{
	public static BotItemDetails Empty { get; } = new(PackCount: 0);

	public BotItemDetails Merge(BotItemDetails update) => new(
		update.Enchantment ?? Enchantment, update.Fusion ?? Fusion, update.EquippedSlot ?? EquippedSlot,
		update.ChargePoints ?? ChargePoints, update.Premium ?? Premium, update.PackCount ?? PackCount);
}

public sealed record BotItemEnchantment(bool SoulBound, byte EnchantLevel, int SkinId, byte OptionalSockets,
	byte EnchantBonus, BotStoneSlots Manastones, int GodstoneId, byte Tempering, bool Amplified, int BuffSkill);

public sealed record BotItemFusion(int ItemId, BotStoneSlots Manastones, byte OptionalSockets, byte BonusStatsId);

public sealed record BotItemPremium(byte BonusStatsId, byte TuneCount);

// Fixed fields give record equality actual slot-value semantics (an array would compare references).
public sealed record BotStoneSlots(int Slot0, int Slot1, int Slot2, int Slot3, int Slot4, int Slot5)
{
	public static BotStoneSlots Empty { get; } = new(0, 0, 0, 0, 0, 0);
	public IReadOnlyList<int> ToList() => new[] { Slot0, Slot1, Slot2, Slot3, Slot4, Slot5 };
}
