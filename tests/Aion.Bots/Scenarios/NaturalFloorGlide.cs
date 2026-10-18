using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>
/// NL-4: down to a lower floor that no walk joins, the way a player goes there: a glide from the edge of this one. The Sky
/// Temple Interior's tiers (320050000) are islands 23 to 37 m apart, joined by nothing (run/nl/NL1/q24023-research.md,
/// section 3). This is the HM-02 glide of <see cref="NaturalHaramelTravel"/> for any map: a takeoff on the bot's island, a
/// landing on a lower island toward the goal, both legs clear of the geometry, and the FP a glide costs there.
/// </summary>
public sealed class NaturalFloorGlide(INaturalJourneySession session, Func<BotNavigationGeometry> geometry)
{
	/// <summary>Java <c>PlayerLifeStats</c>: a glide outside a FLY zone costs 2 FP a second.</summary>
	public const int GlideFpPerSecond = 2;
	/// <summary>The drops a glide takes: from 12 m (below that a walk-off costs nothing) to 45 m.</summary>
	public const float MinimumDrop = 12, MaximumDrop = 45;
	/// <summary>An island between the bot and the goal is landed on only when it is a floor of its own (the Fire Room's middle
	/// tier), not a scrap of ground: the trial nl4-vs-d1 glided onto an 8 m scrap above Bolcan's tier and stood there.</summary>
	public const int TierPolygons = 40;
	/// <summary>A landing keeps this far from every aggressive spawn spot while one such landing exists: the trial nl4-vs-d2
	/// landed 4 m from two of the Fire Room's elites.</summary>
	public const float KeepAway = 15;
	/// <summary>How many candidate glides are checked against the geometry before giving up.</summary>
	private const int CheckedCandidates = 2000;

	/// <param name="walk">Walks to the takeoff on the bot's island; false when it did not get there.</param>
	/// <param name="settle">At the takeoff, before the glide: fights what is on the bot and rests (a glide is refused in combat).</param>
	/// <param name="monsters">Where the map's aggressive monsters stand (its shipped spawn spots), which a landing keeps away from.</param>
	/// <returns>True when the bot glided and stands on a lower island than before.</returns>
	public async Task<bool> GlideTowardAsync(int map, BotPosition goal, Func<BotPosition, Task<bool>> walk, Func<Task> settle,
		IReadOnlyList<BotPosition> monsters, CancellationToken token)
	{
		BotNavigationGeometry geo = geometry();
		BotNavMesh? mesh = geo.NavMesh?.NavMeshes.Get(map);
		if (mesh == null || session.Api.World.MovementSpeed is not float speed || speed <= 0) return false;
		BotPosition from = session.CurrentPosition;
		int here = mesh.IslandOf(from);
		int target = mesh.IslandOf(goal);
		if (here < 0 || here == target) return false;
		var centres = mesh.Polygons().Select(polygon => (polygon.Island, At: new BotPosition(polygon.Ring.Average(v => v.X),
			polygon.Ring.Average(v => v.Y), polygon.Ring.Average(v => v.Z), 0))).ToArray();
		// Landings: the goal's island first; else a floor between the bot and the goal (the Fire Room's middle tier); never one
		// below the goal (the plane under the whole map is a fall to death).
		Dictionary<int, int> sizes = centres.GroupBy(centre => centre.Island).ToDictionary(group => group.Key, group => group.Count());
		BotPosition[] takeoffs = [.. centres.Where(centre => centre.Island == here).Select(centre => centre.At)];
		var landings = centres.Where(centre => centre.Island != here && centre.At.Z >= goal.Z - 3 && centre.At.Z <= from.Z - MinimumDrop &&
			(centre.Island == target || sizes[centre.Island] >= TierPolygons)).ToArray();
		int fp = session.Api.World.MaxFlightTime;
		float reach = MathF.Max(0, (fp - 5) / (float)GlideFpPerSecond) * speed;
		var candidates = takeoffs.SelectMany(takeoff => landings.Select(landing => (Takeoff: takeoff, Landing: landing.At, OnGoal: landing.Island == target)))
			.Where(pair =>
			{
				float drop = pair.Takeoff.Z - pair.Landing.Z;
				float across = Horizontal(pair.Takeoff, pair.Landing);
				return drop is >= MinimumDrop and <= MaximumDrop && across >= drop && across <= drop * 3 &&
					NaturalFlightPolicy.Distance(pair.Takeoff, pair.Landing) + 6 <= reach;
			})
			// The goal's floor first, then a landing away from the monsters (the trial nl4-vs-d3 spent its checks on safe
			// landings the geometry blocked and found no glide off the Fire Room's middle tier).
			.OrderBy(pair => pair.OnGoal ? 0 : 1)
			.ThenBy(pair => monsters.Any(monster => NaturalFlightPolicy.Distance(monster, pair.Landing) < KeepAway) ? 1 : 0)
			.ThenBy(pair => Horizontal(from, pair.Takeoff) + NaturalFlightPolicy.Distance(pair.Takeoff, pair.Landing) + Horizontal(pair.Landing, goal))
			.Take(CheckedCandidates);
		(BotPosition Takeoff, BotPosition Launch, BotPosition Landing)? chosen = null;
		int checkedCount = 0;
		foreach (var candidate in candidates)
		{
			if (++checkedCount > CheckedCandidates) break;
			float across = Horizontal(candidate.Takeoff, candidate.Landing);
			BotPosition launch = candidate.Takeoff with
			{
				X = candidate.Takeoff.X + (candidate.Landing.X - candidate.Takeoff.X) / across * 6,
				Y = candidate.Takeoff.Y + (candidate.Landing.Y - candidate.Takeoff.Y) / across * 6, Z = candidate.Takeoff.Z + 1.5f,
			};
			BotPosition takeoff = candidate.Takeoff with { Z = candidate.Takeoff.Z + 1 };
			if (!NaturalFlightProtocol.IsClear(geo, map, takeoff, launch) || !NaturalFlightProtocol.IsClear(geo, map, launch, candidate.Landing with { Z = candidate.Landing.Z + 1 }))
				continue;
			chosen = (candidate.Takeoff, launch, candidate.Landing);
			break;
		}
		if (chosen is not { } route)
		{
			session.TraceDiagnostic("floor-glide-none", new Dictionary<string, object?>
			{
				["from"] = from, ["goal"] = goal, ["island"] = here, ["goalIsland"] = target, ["checked"] = checkedCount,
			});
			return false;
		}
		session.TraceDiagnostic("floor-glide-chosen", new Dictionary<string, object?>
		{
			["from"] = from, ["takeoff"] = route.Takeoff, ["landing"] = route.Landing, ["goal"] = goal, ["checked"] = checkedCount,
		});
		if (!await walk(route.Takeoff) || session.Api.World.IsDead || session.Api.World.MapId != map) return false;
		await settle();
		if (session.Api.World.IsDead || session.Api.World.MapId != map || NaturalFlightPolicy.Distance(session.CurrentPosition, route.Takeoff) > 3)
			return false;
		float meters = NaturalFlightPolicy.Distance(route.Takeoff, route.Launch) + NaturalFlightPolicy.Distance(route.Launch, route.Landing);
		int need = checked((int)Math.Ceiling(meters / speed) * GlideFpPerSecond + 5);
		long wait = NaturalFlightPolicy.RestoreMillis(session.Api.World.CurrentFlightTime, need, session.Api.World.MaxFlightTime);
		if (wait > 0) await session.AdvanceAsync(TimeSpan.FromMilliseconds(wait + 100), token);
		var mover = new BotMover(session.Api.World, session.Api.Timing);
		await session.ExecuteMovementAsync(mover.CreateJumpPlan([route.Launch], session.CurrentPosition, speed), token);
		BotMovementPlan glide = mover.CreateGlidePlan([route.Landing], route.Launch, speed);
		// Java CM_MOVE(GLIDE) starts the glide; the final frame without it ends the glide on the landing.
		await session.ExecuteMovementAsync(glide with { Frames = glide.Frames.Skip(1).Take(glide.Frames.Count - 2).ToArray() }, token);
		await session.SynchronizeAsync(token);
		if (session.Api.World.IsDead || session.Api.World.MapId != map) return false;
		await session.ExecuteMovementAsync(glide with { Frames = [glide.Frames[^1]], Duration = TimeSpan.Zero, Distance = 0 }, token);
		await NaturalFlightProtocol.LandAsync(session, token);
		await session.SynchronizeAsync(token);
		int landed = mesh.IslandOf(session.CurrentPosition);
		session.TraceDiagnostic("floor-glide", new Dictionary<string, object?>
		{
			["takeoff"] = route.Takeoff, ["landing"] = route.Landing, ["position"] = session.CurrentPosition, ["island"] = landed,
			["fp"] = session.Api.World.CurrentFlightTime, ["hp"] = session.Api.World.CurrentHp,
		});
		return !session.Api.World.IsDead && landed != here;
	}

	private static float Horizontal(BotPosition a, BotPosition b) => MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2));
}
