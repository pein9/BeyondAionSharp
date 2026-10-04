using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>Ordinary glide into Triniel's lower court, and wings inside its shipped FLY zone before gliding out.</summary>
public sealed class NaturalCapitalTravel(INaturalJourneySession session, string repoRoot, Func<long> nowMillis)
{
	private long? lastTakeoff;
	public async Task<bool> ConnectColiseumAsync(BotNavigationGeometry geometry, BotPosition destination, CancellationToken token)
	{
		const int map = 120010000;
		if (session.Api.World.MapId != map || session.Api.World.Level < 10 || session.Api.World.IsDead) return false;
		BotNavMesh? mesh = geometry.NavMesh?.NavMeshes.Get(map);
		if (mesh == null) return false;
		BotPosition from = session.CurrentPosition;
		int sourceIsland = mesh.IslandOf(from, BotNavQuery.Default with { SnapHorizontal = 2 });
		int targetIsland = mesh.IslandOf(destination, BotNavQuery.Default with { SnapHorizontal = 2 });
		if (sourceIsland < 0 || targetIsland < 0 || sourceIsland == targetIsland) return false;
		IReadOnlyList<NaturalFlyZone> zones = NaturalFlyZone.Load(Path.Combine(repoRoot,
			"game-server/data/static_data/zones/zones_120010000.xml"));
		bool Inside(BotPosition p) => zones.Any(z => !z.Forbids && z.Contains(p.X, p.Y, p.Z));
		BotPosition[] Ground(int island) => mesh.Polygons().Where(p => p.Island == island)
			.SelectMany(p => p.Ring.Append(new(p.Ring.Average(v => v.X), p.Ring.Average(v => v.Y), p.Ring.Average(v => v.Z), 0)))
			.Where(p => p.X is > 880 and < 1040 && p.Y is > 1490 and < 1655)
			.Select(p => geometry.StaticGroundAt(map, p)).OfType<BotPosition>().Distinct().ToArray();
		BotPosition[] landings = Ground(targetIsland).OrderBy(p => NaturalFlightPolicy.Distance(p, destination)).Take(40).ToArray();
		BotPosition[] takeoffs = Ground(sourceIsland).Prepend(from).OrderBy(p => NaturalFlightPolicy.Distance(p, from)).ToArray();
		bool exiting = Inside(from);
		foreach (var candidate in takeoffs.SelectMany(takeoff => landings.Select(landing => (Takeoff: takeoff, Landing: landing)))
			.Where(p => exiting ? Inside(p.Takeoff) && p.Landing.Z > p.Takeoff.Z + 2 : p.Takeoff.Z > p.Landing.Z + 3)
			.OrderBy(p => NaturalFlightPolicy.Distance(from, p.Takeoff) + NaturalFlightPolicy.Distance(p.Takeoff, p.Landing)).Take(200))
		{
			BotPosition takeoff = candidate.Takeoff, landing = candidate.Landing;
			IReadOnlyList<BotPosition> approach = geometry.FindJourneyPath(map, from, takeoff);
			if (approach.Count == 0 && NaturalFlightPolicy.Distance(from, takeoff) > .5f) continue;
			float horizontal = MathF.Sqrt(MathF.Pow(landing.X - takeoff.X, 2) + MathF.Pow(landing.Y - takeoff.Y, 2));
			BotPosition launch = exiting
				? new((landing.X + takeoff.X) / 2, (landing.Y + takeoff.Y) / 2, Math.Max(landing.Z + 4, 239), 0)
				: takeoff with { X = takeoff.X + (landing.X - takeoff.X) / horizontal * 3,
					Y = takeoff.Y + (landing.Y - takeoff.Y) / horizontal * 3, Z = takeoff.Z + 1.2f };
			if (launch.Z - landing.Z > horizontal || !NaturalFlightProtocol.IsClear(geometry, map, launch, landing)) continue;
			NaturalFlightRoute? flight = exiting ? NaturalFlightProtocol.Plan(geometry, map, takeoff, launch, launch.Z) : null;
			if (exiting && (flight?.IsUsable != true || !NaturalFlightPolicy.CanFly(
				NaturalFlightProtocol.ToPlan(flight, takeoff, 6, 0), session.Api.World.MaxFlightTime, zones).Allowed)) continue;
			if (!exiting && !NaturalFlightProtocol.IsClear(geometry, map, takeoff, launch)) continue;
			int need = (int)Math.Ceiling((NaturalFlightPolicy.Distance(takeoff, launch) + NaturalFlightPolicy.Distance(launch, landing)) / 6) * 2
				+ NaturalFlightPolicy.LandingReserveFp;
			if (need > session.Api.World.MaxFlightTime) continue;
			if (approach.Count > 0) await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing)
				.CreateGroundPlan(approach, from, session.Api.World.MovementSpeed!.Value), token);
			await session.SynchronizeAsync(token);
			long rest = NaturalFlightPolicy.RestoreMillis(session.Api.World.CurrentFlightTime, need, session.Api.World.MaxFlightTime);
			if (rest > 0) await session.AdvanceAsync(TimeSpan.FromMilliseconds(rest + 100), token);
			if (lastTakeoff is long previous && nowMillis() < previous + 10000)
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(previous + 10000 - nowMillis()), token);
			float speed = session.Api.World.MovementSpeed!.Value;
			if (exiting)
			{
				NaturalFlightDecision gate = NaturalFlightPolicy.CanTakeOff(new(true, session.CurrentPosition, false, float.MinValue,
					nowMillis(), lastTakeoff, false, false, false), zones);
				if (!gate.Allowed) throw new InvalidDataException(gate.Reason);
				speed = await NaturalFlightProtocol.TakeOffAsync(session, token);
				await NaturalFlightProtocol.FlyAsync(session, map, session.CurrentPosition, flight!.Waypoints, speed, token);
			}
			else await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing).CreateJumpPlan([launch], takeoff, speed), token);
			lastTakeoff = nowMillis();
			BotMovementPlan glide = new BotMover(session.Api.World, session.Api.Timing).CreateGlidePlan([landing], launch, speed);
			await session.ExecuteMovementAsync(glide with { Frames = glide.Frames.Skip(1).Take(glide.Frames.Count - 2).ToArray() }, token);
			await session.SynchronizeAsync(token);
			await session.ExecuteMovementAsync(glide with { Frames = [glide.Frames[^1]], Duration = TimeSpan.Zero, Distance = 0 }, token);
			await NaturalFlightProtocol.LandAsync(session, token);
			await session.SynchronizeAsync(token);
			session.TraceDiagnostic("capital-coliseum-crossing", new Dictionary<string, object?>
			{ ["exiting"] = exiting, ["takeoff"] = takeoff, ["launch"] = launch, ["landing"] = landing, ["flightTime"] = session.Api.World.CurrentFlightTime });
			return true;
		}
		return false;
	}
}
