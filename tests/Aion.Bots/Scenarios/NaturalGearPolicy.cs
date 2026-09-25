using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>What the client knows about an equippable item from its tooltip: the slots it fits (bit mask),
/// the level this character needs to wear it (-1 when its class cannot), its item level, and whether it
/// fits this character's race.</summary>
public sealed record NaturalGearInfo(long ValidSlots, int RequiredLevel, int ItemLevel, bool RaceAllowed);

/// <summary>One equip request: put <see cref="ObjectId"/> into the single slot <see cref="Slot"/>.</summary>
public sealed record NaturalGearUpgrade(int ObjectId, int ItemId, long Slot, int ItemLevel, int? ReplacesItemLevel);

/// <summary>
/// Wear the best gear in the bag, the way the recorded human Priest did at Nalto: a quest reward that sits
/// unused in the cube (a level-8 pair of shoes, a level-7 ring, a level-3 mace) goes on as soon as the
/// character can wear it. For every slot, the highest item-level bag item that fits it, that this class and
/// race may wear at this level, replaces what is worn there when it is higher (or fills an empty slot). The
/// server still checks everything (Java Equipment.equipItem); an item it refuses is simply not worn.
/// </summary>
public static class NaturalGearPolicy
{
	public const long MainHand = 1, SubHand = 2;

	/// <param name="offHandSlots">Slot bits that can never be requested directly (the off-hand swap set).</param>
	/// <param name="refused">Items the server already refused; never asked again.</param>
	public static IReadOnlyList<NaturalGearUpgrade> SelectUpgrades(IEnumerable<BotInventoryItem> inventory, int level,
		Func<int, NaturalGearInfo?> describe, long offHandSlots, IReadOnlySet<int>? refused = null)
	{
		ArgumentNullException.ThrowIfNull(inventory);
		ArgumentNullException.ThrowIfNull(describe);
		BotInventoryItem[] items = inventory.ToArray();
		// What is worn now, by single slot bit.
		var worn = new Dictionary<long, (BotInventoryItem Item, int Level)>();
		foreach (BotInventoryItem item in items)
		{
			long mask = item.Details.EquippedSlot ?? 0;
			if (mask <= 0 || describe(item.ItemId) is not NaturalGearInfo info) continue;
			foreach (long bit in Bits(mask))
				worn[bit] = (item, info.ItemLevel);
		}
		var upgrades = new List<NaturalGearUpgrade>();
		var candidates = items
			.Where(item => (item.Details.EquippedSlot ?? 0) <= 0 && refused?.Contains(item.ObjectId) != true)
			.Select(item => (Item: item, Info: describe(item.ItemId)))
			.Where(c => c.Info is { ValidSlots: > 0, RaceAllowed: true } info && info.RequiredLevel > 0 &&
				info.RequiredLevel <= level)
			.OrderByDescending(c => c.Info!.ItemLevel).ThenBy(c => c.Item.ObjectId);
		foreach (var (item, info) in candidates)
		{
			// A one-handed weapon always lands in the main hand without dual wield (Java moves it there).
			long slots = info!.ValidSlots & ~offHandSlots;
			if ((slots & MainHand) != 0) slots = MainHand;
			long? best = null;
			int bestLevel = int.MaxValue;
			foreach (long bit in Bits(slots))
			{
				int wornLevel = worn.TryGetValue(bit, out var current) ? current.Level : 0;
				if (wornLevel < bestLevel) { bestLevel = wornLevel; best = bit; }
			}
			if (best is not long slot || info.ItemLevel <= bestLevel) continue;
			upgrades.Add(new NaturalGearUpgrade(item.ObjectId, item.ItemId, slot, info.ItemLevel,
				worn.ContainsKey(slot) ? bestLevel : null));
			worn[slot] = (item, info.ItemLevel);
		}
		return upgrades;
	}

	private static IEnumerable<long> Bits(long mask)
	{
		for (int bit = 0; bit < 63; bit++)
			if ((mask & (1L << bit)) != 0) yield return 1L << bit;
	}
}
