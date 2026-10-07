using Aion.Bots.World;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>One potion a class buys at a vendor.</summary>
/// <param name="ItemId">What is bought.</param>
/// <param name="AtOrBelow">Buy when the stock is at or below this.</param>
/// <param name="Target">Buy up to this stock.</param>
/// <param name="TradeListId">The vendor's trade list that sells the item.</param>
/// <param name="CountedWith">The items whose owned total is the stock: the bought item and what serves in its place.</param>
public sealed record NaturalRestockLine(int ItemId, int AtOrBelow, int Target, int TradeListId, IReadOnlyList<int> CountedWith);

/// <summary>
/// CP-24: what a class restocks at a vendor, as a pure table (docs/natural-class-profiles.md): potions only. The journey's
/// inventory maintenance asks it whether to go, which line to buy and how many; the walk to the vendor and the trade are
/// the journey's. With the help kit on, the kit's potion is supplied, so a line is bought only when the stock still runs
/// down between two stock checks.
/// </summary>
public sealed record NaturalRestockRules
{
	/// <summary>The Ishalgen vendors' other trade list. It sells bandages, and no class uses one (the operator, CP-Q11): no
	/// table may name it.</summary>
	public const int BandageTradeListId = 264;

	public NaturalRestockRules(IReadOnlyList<NaturalRestockLine> lines, long kinahFloor = 0)
	{
		ArgumentNullException.ThrowIfNull(lines);
		if (kinahFloor < 0) throw new ArgumentOutOfRangeException(nameof(kinahFloor));
		foreach (NaturalRestockLine line in lines)
		{
			if (line.TradeListId == BandageTradeListId)
				throw new ArgumentException($"Restock line {line.ItemId} names trade list {BandageTradeListId}, the bandage list.", nameof(lines));
			if (line.AtOrBelow < 0 || line.Target <= line.AtOrBelow || !line.CountedWith.Contains(line.ItemId))
				throw new ArgumentException($"Restock line {line.ItemId} has no stock to count or nothing to buy.", nameof(lines));
		}
		Lines = lines;
		KinahFloor = kinahFloor;
	}

	public IReadOnlyList<NaturalRestockLine> Lines { get; }

	/// <summary>Kinah the class never spends below on a restock; 0 for none.</summary>
	public long KinahFloor { get; }

	/// <summary>The owned total a line's threshold is compared with.</summary>
	public long Stock(NaturalRestockLine line, IEnumerable<BotInventoryItem> inventory) =>
		inventory.Where(item => line.CountedWith.Contains(item.ItemId)).Sum(item => item.Count);

	/// <summary>The first line of the table whose stock is at or below its threshold, or none.</summary>
	public NaturalRestockLine? Needed(IEnumerable<BotInventoryItem> inventory)
	{
		BotInventoryItem[] owned = inventory.ToArray();
		return Lines.FirstOrDefault(line => Stock(line, owned) <= line.AtOrBelow);
	}

	/// <summary>Kinah a restock may spend.</summary>
	public long Spendable(long kinah) => kinah - KinahFloor;

	/// <summary>How many to buy at the price the vendor shows: up to the target, as far as the spendable Kinah goes.</summary>
	public long PurchaseCount(NaturalRestockLine line, long stock, long kinah, long displayedUnitPrice)
	{
		long spendable = Spendable(kinah);
		if (stock > line.AtOrBelow || displayedUnitPrice <= 0 || spendable < displayedUnitPrice) return 0;
		return Math.Min(line.Target - stock, spendable / displayedUnitPrice);
	}
}
