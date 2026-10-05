using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>Q2919's native Leg 5 drops, after ordinary acceptance and before the city finish.</summary>
public static class NaturalLaterCapitalFieldCollection
{
	public static async Task CollectAsync(INaturalJourneySession session,
		Func<int, QuestRunOperation, Task<int>> kill, Func<int, int, Task> loot, Func<Task> rest)
	{
		BotWorldModel world = session.Api.World;
		if (world.CompletedQuestIds.Contains(2919)) return;
		if (world.MapId != 220030000 || NaturalAltgardQuestSteps.State(world, 2919) is not (3, 4))
			throw new InvalidDataException("Book field collection requires Q2919 START/4 in Altgard.");
		if (Owned(world, 182207011) < 2)
			throw new InvalidDataException("Both accepted Ampha Tails must be carried into Leg 5.");
		foreach (var (item, count, sources) in new[]
		{
			(182207010, 3, new[] { 210443, 210444 }),
			(182207012, 1, new[] { 210435 }),
		})
		{
			var objective = new QuestRunOperation(QuestRunOperationKind.CollectQuestDrop,
				Count: count, ItemId: item, MapId: 220030000);
			for (int attempt = 0; Owned(world, item) < count && attempt < 30; attempt++)
			{
				int source = await kill(sources[attempt % sources.Length], objective);
				if (!world.IsDead) await loot(source, item);
				await rest();
			}
			if (Owned(world, item) < count) throw new InvalidDataException($"Book collection did not obtain item {item}.");
		}
		session.TraceDiagnostic("later-capital-book-field-materials", new Dictionary<string, object?>
		{
			["quest"] = 2919, ["status"] = 3, ["var"] = 4,
			["items"] = world.Inventory.Values.Where(i => i.ItemId is 182207010 or 182207011 or 182207012)
				.Select(i => new { i.ObjectId, i.ItemId, i.Count }).ToArray(),
		});
	}

	private static long Owned(BotWorldModel world, int item) => world.Inventory.Values.Where(i => i.ItemId == item).Sum(i => i.Count);
}
