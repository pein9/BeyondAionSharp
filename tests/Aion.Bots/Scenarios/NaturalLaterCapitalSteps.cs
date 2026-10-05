using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>PC-08's normal client actions. Route approaches belong to the caller.</summary>
public static class NaturalLaterCapitalSteps
{
	public static readonly NaturalAltgardStep[] HeritagePickup =
	[
		NaturalCapitalSteps.Offer(2917, 203574, item: 182207008) with { MapId = 220030000 },
		NaturalCapitalSteps.Progress(2917, 0, 798029, "SETPRO1", 1352, 10, 1) with { MapId = 220030000 },
	];

	public static async Task PickUpHeritageAsync(INaturalJourneySession session,
		Func<NaturalAltgardStep, Task> talk)
	{
		BotWorldModel world = session.Api.World;
		if (world.CompletedQuestIds.Contains(2917)) return;
		if (world.Level < 10 || world.MapId != 220030000)
			throw new InvalidDataException("Arekedil's Heritage pickup requires level 10 in Altgard.");
		if (NaturalAltgardQuestSteps.State(world, 2917) is null or (1 or 2, _))
			await talk(HeritagePickup[0]);
		if (NaturalAltgardQuestSteps.State(world, 2917) is (3, 0))
			await talk(HeritagePickup[1]);
		if (NaturalAltgardQuestSteps.State(world, 2917) is not (3, 1) ||
			world.Inventory.Values.Where(item => item.ItemId == 182207008).Sum(item => item.Count) != 1)
			throw new InvalidDataException("The heritage pickup must retain START/1 and its supplied item for Lanse.");
		session.TraceDiagnostic("later-capital-heritage-pickup", new Dictionary<string, object?>
		{ ["quest"] = 2917, ["status"] = 3, ["var"] = 1, ["item"] = 182207008 });
	}
}
