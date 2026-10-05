using Aion.Bots.Navigation;
using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>RC-03: a small normal Ishalgen collection return, with actual transport and native drops.</summary>
public sealed class NaturalLaterCapitalBookTravel(INaturalJourneySession session, NaturalJourneyRuntime runtime,
	Func<int, Task<int>> approach, Action enterMap, Func<int, QuestRunOperation, Task<int>> kill,
	Func<int, Task> loot)
{
	private static long Tails(BotWorldModel world) => world.Inventory.Values.Where(i => i.ItemId == 182207011).Sum(i => i.Count);

	public async Task CollectAmphaAndReturnAsync(CancellationToken token)
	{
		if (session.Api.World.CompletedQuestIds.Contains(2919)) return;
		if (NaturalAltgardQuestSteps.State(session.Api.World, 2919) is not (3, 4))
			throw new InvalidDataException("Ampha Tails require accepted Q2919 START/4.");
		var objective = new QuestRunOperation(QuestRunOperationKind.CollectQuestDrop, Count: 2, ItemId: 182207011, MapId: 220010000);
		for (int attempt = 1; Tails(session.Api.World) < 2 && attempt <= 30; attempt++)
		{
			if (session.Api.World.MapId != 220010000)
			{
				await ReturnToCityAsync(token); // Also recovers ordinary bind deaths in Altgard.
				await TeleportAsync(204191, 8, 100, 220010000, token);
				await FlyTowardAsync(210404, token);
			}
			int source = await kill(210404, objective);
			if (source > 0 && session.Api.World.MapId == 220010000 && !session.Api.World.IsDead) await loot(source);
		}
		if (Tails(session.Api.World) < 2) throw new InvalidDataException("The bounded Ampha collection did not obtain both tails.");
		await ReturnToCityAsync(token);
		session.TraceDiagnostic("later-capital-book-ampha", new Dictionary<string, object?>
		{ ["quest"] = 2919, ["status"] = 3, ["var"] = 4, ["item"] = 182207011, ["count"] = Tails(session.Api.World) });
	}

	public async Task ReturnToCityAsync(CancellationToken token)
	{
		int? map = session.Api.World.MapId;
		if (map == 120010000) return;
		int npc = map switch { 220010000 => 203679, 220030000 => 203581,
			_ => throw new InvalidDataException("Book recovery is outside the approved city/Ishalgen/Altgard maps.") };
		await FlyTowardAsync(npc, token);
		await TeleportAsync(npc, 7, map == 220010000 ? 100 : 500, 120010000, token);
	}

	private async Task TeleportAsync(int npcId, int location, long price, int destination, CancellationToken token)
	{
		int npc = await approach(npcId);
		long before = session.Api.World.Kinah;
		NaturalServiceOutcome result = await new NaturalServiceSteps(session).TeleportAsync(npc,
			session.Api.World.Objects[npc].Position, 5, location, price, destination, token);
		if (!result.IsDone) throw new InvalidDataException(result.Reason);
		enterMap();
		session.TraceDiagnostic("later-capital-book-teleport", new Dictionary<string, object?>
		{ ["npc"] = npcId, ["destination"] = destination, ["fare"] = before - session.Api.World.Kinah });
	}

	private async Task FlyTowardAsync(int destinationNpcId, CancellationToken token)
	{
		int map = session.Api.World.MapId!.Value;
		BotPosition at = runtime.Data.SpawnsDh.GetSpawnsByWorldId(map).Where(g => g.GetNpcId() == destinationNpcId)
			.SelectMany(g => g.GetSpawnTemplates()).Select(s => new BotPosition(s.GetX(), s.GetY(), s.GetZ(), s.GetHeading()))
			.OrderBy(p => NaturalFlightPolicy.Distance(session.CurrentPosition, p)).First();
		IReadOnlyList<NaturalAirlineRoute> routes = NaturalAirlineRoutes.Load(runtime.RepoRoot);
		for (int hop = 0; hop < 4; hop++)
		{
			NaturalAirlineRoute? route = NaturalAirlineRoutes.Toward(routes, map, session.CurrentPosition, at);
			session.TraceDiagnostic("later-capital-book-flight-choice", new Dictionary<string, object?>
			{ ["map"] = map, ["from"] = session.CurrentPosition, ["destination"] = at, ["flightNpc"] = route?.NpcId });
			if (route == null) return;
			int npc = await approach(route.NpcId);
			NaturalServiceOutcome flown = await new NaturalServiceSteps(session).FlyAsync(npc,
				session.Api.World.Objects[npc].Position, 4, route, token);
			if (!flown.IsDone) throw new InvalidDataException(flown.Reason);
		}
		throw new InvalidDataException("Book collection exceeded its bounded hub flights.");
	}
}
