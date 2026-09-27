using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>Shipped Ishalgen potion knowledge, applied only to client-observed inventory and prices.</summary>
public static class NaturalIshalgenPotionPolicy
{
	public const int StarterLifePotionId = 162000002;
	public const int VendorLifeElixirId = 162000052;
	public const int SharedUseDelayId = 11;
	public const int RestockAtOrBelow = 5;
	public const int RestockTarget = 12;
	public static readonly int[] HealingSkillIds = [9889, 10202];
	public static readonly (int NpcId, BotPosition Position)[] Vendors =
	[
		(798038, new BotPosition(611.017f, 2417.96f, 280.625f, 23)), // Crizpinerk
		(203542, new BotPosition(933.167f, 1685.7f, 261.813f, 6)), // Denma
	];

	public static long Count(IEnumerable<BotInventoryItem> inventory, int itemId) =>
		inventory.Where(item => item.ItemId == itemId).Sum(item => item.Count);
	public static long TotalHealingCount(IEnumerable<BotInventoryItem> inventory) =>
		Count(inventory, StarterLifePotionId) + Count(inventory, VendorLifeElixirId);

	public static BotInventoryItem? SelectOwnedPotion(IEnumerable<BotInventoryItem> inventory) =>
		inventory.Where(item => item.Count > 0 && item.ItemId is StarterLifePotionId or VendorLifeElixirId)
			.OrderBy(item => item.ItemId == StarterLifePotionId ? 0 : 1)
			.ThenBy(item => item.ObjectId).FirstOrDefault();

	public static bool HasActiveHealing(IReadOnlyList<BotVisibleEffect>? effects) =>
		effects?.Any(effect => HealingSkillIds.Contains(effect.SkillId)) == true;

	public static bool NeedsRestock(IEnumerable<BotInventoryItem> inventory) =>
		TotalHealingCount(inventory) <= RestockAtOrBelow;

	public static long AffordablePurchaseCount(long owned, long kinah, long displayedUnitPrice)
	{
		if (owned > RestockAtOrBelow || displayedUnitPrice <= 0 || kinah < displayedUnitPrice) return 0;
		return Math.Min(RestockTarget - owned, kinah / displayedUnitPrice);
	}
}
