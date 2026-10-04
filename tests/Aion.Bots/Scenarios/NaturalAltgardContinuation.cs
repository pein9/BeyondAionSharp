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

	public static NaturalAltgardContract BindIncoming(NaturalAltgardContract leg, IReadOnlySet<int> completed,
		IReadOnlyList<NaturalJourneyItem> inventory)
	{
		if (leg.Start.CompletedQuestIds.Any(id => !completed.Contains(id)))
			throw new InvalidDataException($"{leg.Leg} is missing an approved incoming completion.");
		NaturalHaramel? haramel = leg.Haramel;
		if (haramel != null)
		{
			NaturalJourneyItem staff = inventory.SingleOrDefault(i => i.ItemId == haramel.StaffItemId && i.EquipmentSlot == 3)
				?? throw new InvalidDataException("The continuous journey must retain its equipped Altgard Legionary Staff.");
			haramel = haramel with { StaffObjectId = staff.ObjectId };
		}
		return leg with { Start = leg.Start with { CompletedQuestIds = completed.Order().ToArray() }, Haramel = haramel };
	}
}
