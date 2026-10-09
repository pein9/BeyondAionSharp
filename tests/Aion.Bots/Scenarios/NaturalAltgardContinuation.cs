using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>Join the approved legs using observed incoming state, without restoring a snapshot or changing the character.</summary>
public static class NaturalAltgardContinuation
{
	public static IReadOnlyList<string> Order { get; } = Array.AsReadOnly(new[]
	{
		"l1", "l2", "l3", "l4", "l5", "l6", "l7", "l8", "l9", "l10", "l11", "cg", "l12",
	});

	/// <summary>Java's Q24014/Q24015 level hooks start these campaigns at level 20 after Q24010.
	/// Their deferred objectives must still have zero flags and no completion credit.</summary>
	public static bool AllowsAutomaticCampaignUnlock(int id, (byte Status, int StepAndFlags) incoming,
		BotQuestState current, int level, IReadOnlySet<int> completed) =>
		id is 24014 or 24015 && incoming == (6, 0) && level >= 20 && completed.Contains(24010) &&
		current is { Status: 3, StepAndFlags: 0, CompleteCount: 0 };

	/// <param name="contractClass">NR-40: the character is of the class the leg's contract was written for. Another class's
	/// coin-gear and Haramel scopes are made from what it holds by the journey (NR-38a, NR-40), not bound here to the
	/// contract's staff and gloves.</param>
	public static NaturalAltgardContract BindIncoming(NaturalAltgardContract leg, IReadOnlySet<int> completed,
		IReadOnlyList<NaturalJourneyItem> inventory, IEnumerable<int>? equippedItemIds = null, bool contractClass = true)
	{
		if (leg.Start.CompletedQuestIds.Any(id => !completed.Contains(id)))
			throw new InvalidDataException($"{leg.Leg} is missing an approved incoming completion.");
		if (!contractClass) return leg with { Start = leg.Start with { CompletedQuestIds = completed.Order().ToArray() } };
		NaturalHaramel? haramel = leg.Haramel;
		if (haramel != null)
		{
			NaturalJourneyItem staff = inventory.SingleOrDefault(i => i.ItemId == haramel.StaffItemId && i.EquipmentSlot == 3)
				?? throw new InvalidDataException("The continuous journey must retain its equipped Altgard Legionary Staff.");
			HashSet<int> owned = inventory.Select(i => i.ItemId).ToHashSet();
			// Revised capital rewards can replace historical accessories before CG. Preserve the actual
			// incoming gear while the staff, approved chain pieces and sealed bundle remain mandatory.
			// NR-19a: the cloth gloves are kept when owned, like the accessories.
			haramel = haramel with
			{
				StaffObjectId = staff.ObjectId,
				ProtectedItemIds = haramel.RequiredIncomingItemIds
					.Concat(haramel.ProtectedItemIds.Where(owned.Contains))
					.Concat((equippedItemIds ?? inventory.Where(i => i.EquipmentSlot is > 0 and not (65535 or 8192 or 16384))
						.Select(i => i.ItemId)).Where(owned.Contains)).Distinct().Order().ToArray(),
			};
		}
		NaturalCoinGear? coinGear = leg.CoinGear;
		if (coinGear != null)
		{
			// NR-19a: the handguards purchase replaces the gloves this character wears, whichever pair that is. That pair
			// is held through the leg and is the one the end check looks for in the bag. None worn, none asked for.
			int worn = inventory.FirstOrDefault(i => i.EquipmentSlot == NaturalCoinGear.GlovesSlot)?.ItemId ?? 0;
			coinGear = coinGear with
			{
				ReplacedGlovesItemId = worn,
				ProtectedItemIds = worn == 0 ? coinGear.ProtectedItemIds : coinGear.ProtectedItemIds.Append(worn).Distinct().ToArray(),
			};
		}
		return leg with
		{
			Start = leg.Start with { CompletedQuestIds = completed.Order().ToArray() }, CoinGear = coinGear, Haramel = haramel,
		};
	}

	/// <summary>Use full item-detail slots so a belt's 65536 mask is not truncated by the legacy ushort field.</summary>
	public static int[] EquippedItemIds(BotWorldModel world) => world.Inventory.Values
		.Where(i => i.Details.EquippedSlot.GetValueOrDefault() is > 0 and not (65535 or 8192 or 16384))
		.Select(i => i.ItemId).ToArray();
}
