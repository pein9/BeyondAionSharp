namespace Aion.Bots.Scenarios;

/// <summary>Join the approved legs using observed incoming state, without restoring a snapshot or changing the character.</summary>
public static class NaturalAltgardContinuation
{
	public static IReadOnlyList<string> Order { get; } = Array.AsReadOnly(new[]
	{
		"l1", "l2", "l3", "l4", "l5", "l6", "l7", "l8", "l9", "l10", "l11", "cg", "l12",
	});

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
