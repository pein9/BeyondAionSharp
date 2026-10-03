using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>The HM-02 proved client floor transitions, using normal movement and the caller's
/// defended ground routes. No server actor, setup teleport or instance reset is available.</summary>
public sealed class NaturalHaramelTravel(INaturalJourneySession session, NaturalJourneyRuntime runtime,
	Func<BotNavigationGeometry> geometry, Func<BotPosition, float, Task> walk)
{
	public async Task GlideToLowerFloorAsync(CancellationToken token)
	{
		const int map = 300200000;
		BotNavigationGeometry geo = geometry();
		BotPosition from = session.CurrentPosition;
		BotNavMesh mesh = geo.NavMesh!.NavMeshes.Get(map)!;
		int lowerIsland = mesh.IslandOf(new(283.064f, 275.719f, 90.1018f, 0));
		BotPosition[] landings = mesh.Polygons().Where(p => p.Island == lowerIsland).Select(p =>
			new BotPosition(p.Ring.Average(v => v.X), p.Ring.Average(v => v.Y), p.Ring.Average(v => v.Z), 0)).ToArray();
		var candidates = new[] { from }.Concat(geo.GroundAround(map, from, [2f, 4f, 8f, 12f]))
			.SelectMany(takeoff => landings.Select(landing => (Takeoff: takeoff, Landing: landing)))
			.Where(p => p.Takeoff.Z - p.Landing.Z is > 20 and < 60 &&
				MathF.Sqrt(MathF.Pow(p.Takeoff.X - p.Landing.X, 2) + MathF.Pow(p.Takeoff.Y - p.Landing.Y, 2)) >= p.Takeoff.Z - p.Landing.Z)
			.OrderBy(p => NaturalFlightPolicy.Distance(from, p.Takeoff) + NaturalFlightPolicy.Distance(p.Takeoff, p.Landing));
		(BotPosition Takeoff, BotPosition Launch, BotPosition Landing)? chosen = null;
		foreach (var candidate in candidates)
		{
			float horizontal = MathF.Sqrt(MathF.Pow(candidate.Landing.X - candidate.Takeoff.X, 2) + MathF.Pow(candidate.Landing.Y - candidate.Takeoff.Y, 2));
			BotPosition launch = candidate.Takeoff with { X = candidate.Takeoff.X + (candidate.Landing.X - candidate.Takeoff.X) / horizontal * 6,
				Y = candidate.Takeoff.Y + (candidate.Landing.Y - candidate.Takeoff.Y) / horizontal * 6, Z = candidate.Takeoff.Z + 1.5f };
			if (!NaturalFlightProtocol.IsClear(geo, map, candidate.Takeoff, launch) || !NaturalFlightProtocol.IsClear(geo, map, launch, candidate.Landing)) continue;
			if (NaturalFlightPolicy.Distance(from, candidate.Takeoff) > 1 && geo.NavMesh.FindPath(map, from, candidate.Takeoff).Count == 0) continue;
			chosen = (candidate.Takeoff, launch, candidate.Landing);
			break;
		}
		var route = chosen ?? throw new InvalidDataException("No checked Haramel glide to the lower floor.");
		await walk(route.Takeoff, 1);
		if (session.Api.World.IsDead || session.Api.World.MapId != map) return;
		float speed = session.Api.World.MovementSpeed!.Value;
		int need = checked((int)Math.Ceiling(NaturalFlightPolicy.Distance(route.Takeoff, route.Landing) / speed) + 5);
		if (need > session.Api.World.MaxFlightTime) throw new InvalidDataException("Haramel glide exceeds observed flight capacity.");
		long wait = NaturalFlightPolicy.RestoreMillis(session.Api.World.CurrentFlightTime, need, session.Api.World.MaxFlightTime);
		if (wait > 0) await session.AdvanceAsync(TimeSpan.FromMilliseconds(wait + 100), token);
		session.TraceDiagnostic("haramel-checked-glide", new Dictionary<string, object?> { ["takeoff"] = route.Takeoff, ["landing"] = route.Landing });
		var mover = new BotMover(session.Api.World, session.Api.Timing);
		await session.ExecuteMovementAsync(mover.CreateJumpPlan([route.Launch], route.Takeoff, speed), token);
		BotMovementPlan glide = mover.CreateGlidePlan([route.Landing], route.Launch, speed);
		// Java CM_MOVE(GLIDE) starts the glide; the optional START_GLIDE emotion has no handler.
		await session.ExecuteMovementAsync(glide with { Frames = glide.Frames.Skip(1).Take(glide.Frames.Count - 2).ToArray() }, token);
		await session.SynchronizeAsync(token);
		if (session.Api.World.IsDead || session.Api.World.MapId != map) return;
		await session.ExecuteMovementAsync(glide with { Frames = [glide.Frames[^1]], Duration = TimeSpan.Zero, Distance = 0 }, token);
		await NaturalFlightProtocol.LandAsync(session, token);
		await session.SynchronizeAsync(token);
	}

	public async Task RideOfficeElevatorAsync(CancellationToken token)
	{
		NaturalHaramelElevator elevator = NaturalHaramelElevator.Load(runtime.RepoRoot);
		long cycle = 0;
		bool boarded = false;
		for (int attempt = 0; attempt < 3 && !boarded; attempt++)
		{
			await walk(elevator.Bottom with { X = elevator.Bottom.X + 3.5f }, 1);
			if (session.Api.World.IsDead || session.Api.World.MapId != elevator.MapId) return;
			cycle = elevator.NextBottomCycle(runtime.NowMillis);
			if (cycle > runtime.NowMillis) await session.AdvanceAsync(TimeSpan.FromMilliseconds(cycle - runtime.NowMillis), token);
			await walk(elevator.Bottom, .25f);
			if (session.Api.World.IsDead || session.Api.World.MapId != elevator.MapId) return;
			boarded = runtime.NowMillis - cycle < elevator.AscentStarts;
			if (!boarded) session.TraceDiagnostic("haramel-missed-elevator-window", new Dictionary<string, object?> { ["attempt"] = attempt, ["cycle"] = cycle });
		}
		if (!boarded) throw new InvalidDataException("Haramel elevator travel remains incomplete after three recorded boarding windows.");
		BotNavigationGeometry geo = geometry();
		if (!NaturalFlightProtocol.IsClear(geo, elevator.MapId, elevator.Bottom, elevator.Top)) throw new InvalidDataException("Haramel elevator shaft collision.");
		session.TraceDiagnostic("haramel-office-elevator", new Dictionary<string, object?> { ["cycle"] = cycle, ["from"] = elevator.Bottom, ["to"] = elevator.Top });
		await session.ExecuteMovementAsync(elevator.Ascent(cycle, runtime.NowMillis), token);
		await session.SynchronizeAsync(token);
		if (session.Api.World.IsDead || session.Api.World.MapId != elevator.MapId) return;
		BotPosition exit = elevator.Top with { X = elevator.Top.X - 3.75f };
		IReadOnlyList<BotPosition> connector = elevator.Disembark(geo, exit);
		bool hop = connector.Count == 0;
		if (hop) connector = elevator.JumpDisembark(geo, exit);
		if (connector.Count == 0) throw new InvalidDataException("No checked Haramel elevator departure.");
		var mover = new BotMover(session.Api.World, session.Api.Timing);
		await session.ExecuteMovementAsync(hop ? mover.CreateJumpPlan(connector, elevator.Top, session.Api.World.MovementSpeed!.Value)
			: mover.CreateGroundPlan(connector, elevator.Top, session.Api.World.MovementSpeed!.Value), token);
		await session.SynchronizeAsync(token);
	}

	public async Task RideOfficeElevatorDownAsync(CancellationToken token)
	{
		NaturalHaramelElevator elevator = NaturalHaramelElevator.Load(runtime.RepoRoot);
		BotNavigationGeometry geo = geometry();
		BotPosition exit = elevator.Top with { X = elevator.Top.X - 3.75f };
		BotPosition landing = geo.StaticGroundAt(elevator.MapId, exit) ?? throw new InvalidDataException("No checked upper elevator landing.");
		for (int attempt = 0; attempt < 3; attempt++)
		{
			await walk(landing, 1);
			if (session.Api.World.IsDead || session.Api.World.MapId != elevator.MapId) return;
			long cycle = runtime.NowMillis - runtime.NowMillis % elevator.PeriodMillis;
			if (runtime.NowMillis - cycle >= elevator.DescentStarts) cycle += elevator.PeriodMillis;
			long wait = cycle + elevator.AscentEnds - runtime.NowMillis;
			if (wait > 0) await session.AdvanceAsync(TimeSpan.FromMilliseconds(wait), token);
			IReadOnlyList<BotPosition> connector = elevator.Disembark(geo, exit);
			bool hop = connector.Count == 0;
			if (hop) connector = elevator.JumpDisembark(geo, exit);
			if (connector.Count == 0) throw new InvalidDataException("No checked upper elevator boarding connector.");
			BotPosition[] boarding = hop ? [connector[0], elevator.Top] : connector.Reverse().Skip(1).Append(elevator.Top).ToArray();
			BotPosition previous = landing;
			foreach (BotPosition point in boarding)
			{
				if (!NaturalFlightProtocol.IsClear(geo, elevator.MapId, previous, point)) throw new InvalidDataException("Upper elevator boarding collision.");
				previous = point;
			}
			var mover = new BotMover(session.Api.World, session.Api.Timing);
			await session.ExecuteMovementAsync(hop ? mover.CreateJumpPlan(boarding, landing, session.Api.World.MovementSpeed!.Value)
				: mover.CreateGroundPlan(boarding, landing, session.Api.World.MovementSpeed!.Value), token);
			await session.SynchronizeAsync(token);
			if (session.Api.World.IsDead || session.Api.World.MapId != elevator.MapId) return;
			if (runtime.NowMillis - cycle >= elevator.DescentStarts)
			{
				session.TraceDiagnostic("haramel-missed-elevator-window", new Dictionary<string, object?> { ["attempt"] = attempt, ["cycle"] = cycle, ["floor"] = "upper" });
				continue;
			}
			session.TraceDiagnostic("haramel-office-elevator-down", new Dictionary<string, object?> { ["cycle"] = cycle, ["from"] = elevator.Top, ["to"] = elevator.Bottom });
			await session.ExecuteMovementAsync(elevator.Descent(cycle, runtime.NowMillis), token);
			await session.SynchronizeAsync(token);
			if (session.Api.World.IsDead || session.Api.World.MapId != elevator.MapId) return;
			await walk(elevator.Bottom with { X = elevator.Bottom.X + 3.5f }, 1);
			return;
		}
		throw new InvalidDataException("Haramel elevator descent remains incomplete after three recorded boarding windows.");
	}
}
