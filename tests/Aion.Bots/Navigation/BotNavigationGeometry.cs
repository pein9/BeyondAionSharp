using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.World;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Models;
using Aion.GameServer.Model;
using Aion.GameServer.World.Geo;

namespace Aion.Bots.Navigation;

public readonly record struct BotNavigationHazard(BotPosition Position, float Radius);

/// <summary>Ground-only bot routes over the same checked-in geometry used by the server.</summary>
public sealed class BotNavigationGeometry(Func<int, GeoMap> maps, int instanceId, IgnoreProperties ignoreProperties)
{
    public const float SampleSpacing = 2f;
    private const float HeightWindow = 2f;

    public static BotNavigationGeometry ForServerWorld(int instanceId, Race race) =>
        new(GeoService.GetInstance().GetMap, instanceId, IgnoreProperties.Of(race));

    /// <summary>Read-only terrain/obstacle sight check for selecting a ranged firing point.
    /// The 1.25 m eye height matches the ordinary player offset used by GeoService.</summary>
    public bool HasLineOfSight(int mapId, BotPosition observer, BotPosition target) =>
        maps(mapId).CanSee(observer.X, observer.Y, observer.Z + 1.25f,
            target.X, target.Y, target.Z + 1.25f, instanceId, ignoreProperties);

    /// <summary>Straight-line distance within which a failed navmesh request still gets the bounded grid
    /// search (NPCs standing in collision, doors, gaps the voxel bake cannot express). Longer requests trust
    /// the navmesh's answer instead of spending minutes on a grid search that cannot finish.</summary>
    public const float GridFallbackDistance = 60f;

    private BotNavMeshRouter? navMesh;
    private bool navMeshResolved;

    /// <summary>The baked-navmesh router used first by every path method below, or null when disabled
    /// (<c>AION_BOT_NAVMESH=0</c>) or when no navmeshes are checked in. Maps without a baked navmesh
    /// keep using the grid search.</summary>
    public BotNavMeshRouter? NavMesh
    {
        get
        {
            if (!navMeshResolved)
            {
                navMesh = BotNavMeshSet.Default is { } set ? new BotNavMeshRouter(set, this) : null;
                navMeshResolved = true;
            }
            return navMesh;
        }
    }

    /// <summary>Uses the given navmeshes (or none) instead of the process default.</summary>
    public BotNavigationGeometry WithNavMesh(BotNavMeshSet? set)
    {
        navMesh = set == null ? null : new BotNavMeshRouter(set, this);
        navMeshResolved = true;
        return this;
    }

    /// <summary>Checked route to <paramref name="destination"/> for short hops.</summary>
    public IReadOnlyList<BotPosition> FindLocalPath(int mapId, BotPosition start, BotPosition destination)
        => ViaNavMesh(mapId, start, destination, router => router.FindPath(mapId, start, destination, BotNavQuery.Default with { GroundCost = 1f }))
            ?? GridLocalPath(mapId, start, destination);

    /// <summary>Checked route for longer journeys, preferring mapped roads.</summary>
    public IReadOnlyList<BotPosition> FindJourneyPath(int mapId, BotPosition start, BotPosition destination)
        => ViaNavMesh(mapId, start, destination, router => router.FindPath(mapId, start, destination))
            ?? GridJourneyPath(mapId, start, destination);

    /// <summary>Checked ground route that stays outside client-observed aggro circles.
    /// If already inside one, only outward steps are allowed until clear.</summary>
    public IReadOnlyList<BotPosition> FindJourneyPathAvoiding(int mapId, BotPosition start,
        BotPosition destination, IReadOnlyList<BotNavigationHazard> hazards)
        => ViaNavMesh(mapId, start, destination, router => router.FindPath(mapId, start, destination, BotNavQuery.Default with { Hazards = hazards }))
            ?? GridJourneyPathAvoiding(mapId, start, destination, hazards);

    /// <summary>Find checked ground in Priest spell range with sight to the observed
    /// target. Other observed aggro circles remain forbidden; reaching the mob's
    /// occupied ground position is neither required nor treated as safe.</summary>
    public IReadOnlyList<BotPosition> FindRangedApproachPath(int mapId, BotPosition start,
        BotPosition target, IReadOnlyList<BotNavigationHazard> otherHazards)
        => ViaNavMesh(mapId, start, target, router => router.FindRangedApproachPath(mapId, start, target, BotNavQuery.Default with { Hazards = otherHazards }))
            ?? GridRangedApproachPath(mapId, start, target, otherHazards);

    /// <summary>Road-preferring journey. With a navmesh, the roads baked into it (extracted from the
    /// client map art) replace the hand-transcribed <paramref name="road"/> polyline; either way roads
    /// change cost, never walkability.</summary>
    public IReadOnlyList<BotPosition> FindRoadPreferredJourneyPath(int mapId, BotPosition start,
        BotPosition destination, IReadOnlyList<BotRoadPoint> road,
        IReadOnlyList<BotNavigationHazard> hazards)
    {
        ArgumentNullException.ThrowIfNull(road);
        ArgumentNullException.ThrowIfNull(hazards);
        return ViaNavMesh(mapId, start, destination, router => router.FindPath(mapId, start, destination,
                BotNavQuery.Default with { Hazards = hazards, GroundCost = 1.5f }))
            ?? GridRoadPreferredJourneyPath(mapId, start, destination, road, hazards);
    }

    /// <summary>Find checked ground inside the ordinary three-metre interaction radius when an
    /// observed NPC's exact spawn point is occupied or otherwise not walkable. Never returns an
    /// approach outside that radius, and does not assume that a nearby point can be teleported to.</summary>
    public IReadOnlyList<BotPosition> FindInteractionPath(int mapId, BotPosition start, BotPosition target)
        => ViaNavMesh(mapId, start, target, router => router.FindInteractionPath(mapId, start, target))
            ?? GridInteractionPath(mapId, start, target);

    /// <summary>Terrain-checked route to within interaction range of <paramref name="destination"/> that treats
    /// the observed aggro circles as costs rather than walls: it skirts a circle when a reasonable detour
    /// exists and otherwise goes through, so the caller can fight its way in along it
    /// (<see cref="NaturalFightThrough"/>). Every step still passes the ground/collision check; hostiles are
    /// deliberately not a hard rule here, so walk only its hostile-free prefix.</summary>
    public IReadOnlyList<BotPosition> FindFightThroughPath(int mapId, BotPosition start, BotPosition destination,
        IReadOnlyList<BotNavigationHazard> hazards)
    {
        ArgumentNullException.ThrowIfNull(hazards);
        BotNavMeshRouter? router = NavMesh;
        if (router != null && router.Covers(mapId))
        {
            var danger = hazards.Where(h => HorizontalDistance(start, h.Position) >= h.Radius)
                .Select(h => new BotNavDanger(h.Position.X, h.Position.Y, h.Radius + 1, 12)).ToArray();
            IReadOnlyList<BotPosition> route = router.FindInteractionPath(mapId, start, destination,
                BotNavQuery.Default with { Danger = danger });
            if (route.Count > 0 || BotNavMeshRouter.LastOutcome == BotNavRouteOutcome.Routed) return route;
        }
        return Distance(start, destination) <= 3 ? [] : GridInteractionPath(mapId, start, destination);
    }

    /// <summary>Navmesh answer, or null when the caller should use the grid search: no navmesh for this
    /// map, or a failed request short enough for the bounded grid search to settle.</summary>
    private IReadOnlyList<BotPosition>? ViaNavMesh(int mapId, BotPosition start, BotPosition destination,
        Func<BotNavMeshRouter, IReadOnlyList<BotPosition>> query)
    {
        BotNavMeshRouter? router = NavMesh;
        if (router == null || !router.Covers(mapId)) return null;
        IReadOnlyList<BotPosition> route = query(router);
        if (route.Count > 0 || BotNavMeshRouter.LastOutcome == BotNavRouteOutcome.Routed) return route;
        // Observed packs: the navmesh's local detours are bounded, so keep the grid's wider avoidance search
        // as a backstop. Routing around hostiles is never worse than before the navmesh.
        if (BotNavMeshRouter.LastOutcome == BotNavRouteOutcome.HazardRejected) return null;
        return Distance(start, destination) <= GridFallbackDistance ? null : route;
    }

    /// <summary>Bounded local grid search (no navmesh). It cannot invent jumps, open doors or cross
    /// maps, and reports no route when its budget is exhausted.</summary>
    public IReadOnlyList<BotPosition> GridLocalPath(int mapId, BotPosition start, BotPosition destination)
        => FindGroundPath(mapId, start, destination, 200, 8192, 20);

    /// <summary>Bounded longer grid search (no navmesh), with the same two-metre ground/collision checks.</summary>
    public IReadOnlyList<BotPosition> GridJourneyPath(int mapId, BotPosition start, BotPosition destination)
        => FindGroundPath(mapId, start, destination, 1000, 65536, 60);

    /// <summary>Grid search (no navmesh) that stays outside client-observed aggro circles.</summary>
    public IReadOnlyList<BotPosition> GridJourneyPathAvoiding(int mapId, BotPosition start,
        BotPosition destination, IReadOnlyList<BotNavigationHazard> hazards)
        => FindGroundPath(mapId, start, destination, 1000, 131072, 180, hazards);

    /// <summary>Grid search (no navmesh) for a ranged firing point with sight to the target.</summary>
    public IReadOnlyList<BotPosition> GridRangedApproachPath(int mapId, BotPosition start,
        BotPosition target, IReadOnlyList<BotNavigationHazard> otherHazards)
        => FindGroundPath(mapId, start, target, 200, 32768, 30, otherHazards,
            arrivalRadius: 20, arrivalPredicate: point => HasLineOfSight(mapId, point, target));

    /// <summary>Grid search (no navmesh) preferring a mapped road on longer journeys, only when both
    /// endpoints can reasonably join it.</summary>
    public IReadOnlyList<BotPosition> GridRoadPreferredJourneyPath(int mapId, BotPosition start,
        BotPosition destination, IReadOnlyList<BotRoadPoint> road,
        IReadOnlyList<BotNavigationHazard> hazards)
    {
        ArgumentNullException.ThrowIfNull(road);
        ArgumentNullException.ThrowIfNull(hazards);
        if (road.Count < 2 || Distance(start, destination) < 100 ||
            RoadDistance(start.X, start.Y, road) > 90 ||
            RoadDistance(destination.X, destination.Y, road) > 90)
            return [];
        return FindGroundPath(mapId, start, destination, 1000, 131072, 120, hazards,
            preferredRoad: road);
    }

    public static bool AvoidsHazards(BotPosition start, IReadOnlyList<BotPosition> route,
        IReadOnlyList<BotNavigationHazard> hazards)
    {
        BotPosition previous = start;
        foreach (BotPosition point in route)
        {
            if (!SafeStep(start, previous, point, hazards)) return false;
            previous = point;
        }
        return true;
    }

    /// <summary>Grid search (no navmesh) for ground within the three-metre interaction radius.</summary>
    public IReadOnlyList<BotPosition> GridInteractionPath(int mapId, BotPosition start, BotPosition target)
    {
		IReadOnlyList<BotPosition> nearbyGround = FindGroundPath(mapId, start, target,
			1000, 65536, 60, arrivalRadius: 3);
		if (nearbyGround.Count != 0) return nearbyGround;
        for (int sector = 0; sector < 16; sector++)
        {
            float angle = sector * MathF.PI / 8f;
            var candidate = new BotPosition(target.X + 2 * MathF.Cos(angle),
                target.Y + 2 * MathF.Sin(angle), target.Z, target.Heading);
            IReadOnlyList<BotPosition> path = GridJourneyPath(mapId, start, candidate);
            if (path.Count != 0 && Distance(path[^1], target) <= 3) return path;
        }
        return [];
    }

    private IReadOnlyList<BotPosition> FindGroundPath(int mapId, BotPosition start, BotPosition destination,
        float maximumDistance, int maximumVisited, float padding,
        IReadOnlyList<BotNavigationHazard>? hazards = null, float arrivalRadius = 0,
        IReadOnlyList<BotRoadPoint>? preferredRoad = null,
        Func<BotPosition, bool>? arrivalPredicate = null)
    {
        if (!Finite(start) || !Finite(destination) || Distance(start, destination) > maximumDistance) return [];
        // A path cannot enter an observed aggro circle that does not already
        // contain its origin. Reject this impossible goal before exploring a
        // large ground grid around it.
        if (hazards != null && hazards.Any(hazard =>
            HorizontalDistance(start, hazard.Position) >= hazard.Radius &&
            HorizontalDistance(destination, hazard.Position) < hazard.Radius - arrivalRadius))
            return [];
        var initial = TraceEdge(mapId, start, start);
        if (initial == null) return [];
        var first = new Cell(0, 0, (int)MathF.Round(initial[0].Z * 2));
        var positions = new Dictionary<Cell, BotPosition> { [first] = initial[0] };
        var costs = new Dictionary<Cell, float> { [first] = 0 };
        var parent = new Dictionary<Cell, Cell>();
        var open = new PriorityQueue<Cell, (float Cost, int Order)>();
        int order = 0;
        open.Enqueue(first, (Distance(start, destination), order++));
        var closed = new HashSet<Cell>();
        while (open.TryDequeue(out var cell, out _) && closed.Count < maximumVisited)
        {
            if (!closed.Add(cell)) continue;
            BotPosition current = positions[cell];
			if (arrivalRadius > 0 && Distance(current, destination) <= arrivalRadius &&
				(arrivalPredicate == null || arrivalPredicate(current)))
			{
				var reverse = new List<BotPosition> { current };
				Cell parentCell = cell;
				while (parent.TryGetValue(parentCell, out parentCell)) reverse.Add(positions[parentCell]);
				reverse.Reverse();
				var checkedPath = new List<BotPosition>();
				BotPosition previous = start;
				foreach (BotPosition point in reverse.Skip(1))
				{
					IReadOnlyList<BotPosition>? edge = TraceEdge(mapId, previous, point);
					if (edge == null || hazards != null && !SafeEdge(start, previous, edge, hazards)) return [];
					checkedPath.AddRange(edge);
					previous = edge[^1];
				}
				return checkedPath;
			}
            var tail = arrivalPredicate == null && Distance(current, destination) <= 3
                ? TraceEdge(mapId, current, destination) : null;
            if (tail != null && hazards != null && !SafeEdge(start, current, tail, hazards)) tail = null;
            if (tail != null)
            {
                var reverse = new List<BotPosition> { current };
                while (parent.TryGetValue(cell, out cell)) reverse.Add(positions[cell]);
                reverse.Reverse();
                var result = new List<BotPosition>();
                BotPosition previous = start;
                foreach (var point in reverse.Skip(1).Append(destination))
                {
                    var edge = TraceEdge(mapId, previous, point);
                    if (edge == null || hazards != null && !SafeEdge(start, previous, edge, hazards)) return [];
                    result.AddRange(edge); previous = edge[^1];
                }
                return result;
            }
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int x = cell.X + dx, y = cell.Y + dy;
                var candidate = new BotPosition(start.X + x * SampleSpacing, start.Y + y * SampleSpacing, current.Z, destination.Heading);
                if (candidate.X < MathF.Min(start.X, destination.X) - padding || candidate.X > MathF.Max(start.X, destination.X) + padding
                    || candidate.Y < MathF.Min(start.Y, destination.Y) - padding || candidate.Y > MathF.Max(start.Y, destination.Y) + padding) continue;
                var edge = TraceEdge(mapId, current, candidate);
                if (edge == null) continue;
                if (hazards != null && !SafeEdge(start, current, edge, hazards)) continue;
                candidate = edge[^1];
                var next = new Cell(x, y, (int)MathF.Round(candidate.Z * 2));
                float stepCost = Distance(current, candidate);
                if (preferredRoad != null)
                {
                    float offRoad = MathF.Min(1, RoadDistance(candidate.X, candidate.Y, preferredRoad) / 30);
                    stepCost *= 1 + 1.5f * offRoad;
                }
                float cost = costs[cell] + stepCost;
                if (closed.Contains(next) || costs.TryGetValue(next, out float old) && old <= cost) continue;
                costs[next] = cost; positions[next] = candidate; parent[next] = cell;
                open.Enqueue(next, (cost + Distance(candidate, destination), order++));
            }
        }
        return [];
    }

    private readonly record struct Cell(int X, int Y, int Z);

    private static float RoadDistance(float x, float y, IReadOnlyList<BotRoadPoint> road)
    {
        float nearest = float.PositiveInfinity;
        for (int index = 1; index < road.Count; index++)
        {
            BotRoadPoint a = road[index - 1], b = road[index];
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float lengthSquared = dx * dx + dy * dy;
            float fraction = lengthSquared <= 0 ? 0 : Math.Clamp(
                ((x - a.X) * dx + (y - a.Y) * dy) / lengthSquared, 0, 1);
            float px = a.X + fraction * dx, py = a.Y + fraction * dy;
            nearest = MathF.Min(nearest, MathF.Sqrt((x - px) * (x - px) + (y - py) * (y - py)));
        }
        return nearest;
    }

    private static bool SafeEdge(BotPosition start, BotPosition from,
        IReadOnlyList<BotPosition> samples, IReadOnlyList<BotNavigationHazard> hazards)
    {
        BotPosition previous = from;
        foreach (BotPosition sample in samples)
        {
            if (!SafeStep(start, previous, sample, hazards)) return false;
            previous = sample;
        }
        return true;
    }

    private static bool SafeStep(BotPosition start, BotPosition previous,
        BotPosition next, IReadOnlyList<BotNavigationHazard> hazards)
    {
        float previousExposure = 0, nextExposure = 0;
        foreach (BotNavigationHazard hazard in hazards)
        {
            float nextDistance = HorizontalDistance(next, hazard.Position);
            if (nextDistance >= hazard.Radius) continue;
            float previousDistance = HorizontalDistance(previous, hazard.Position);
            if (HorizontalDistance(start, hazard.Position) >= hazard.Radius ||
                previousDistance >= hazard.Radius) return false; // Do not enter or re-enter a hazard.
        }
        foreach (BotNavigationHazard hazard in hazards)
        {
            if (HorizontalDistance(start, hazard.Position) >= hazard.Radius) continue;
            previousExposure += MathF.Max(0, hazard.Radius - HorizontalDistance(previous, hazard.Position));
            nextExposure += MathF.Max(0, hazard.Radius - HorizontalDistance(next, hazard.Position));
        }
        // Overlapping circles can make moving outward from each individual mob
        // geometrically impossible. Permit a checked escape only when total
        // exposure to the already-observed pack does not increase.
        return nextExposure <= previousExposure + 0.01f;
    }

    private static float HorizontalDistance(BotPosition a, BotPosition b) =>
        MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2));
    private static float Distance(BotPosition a, BotPosition b) =>
        MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));

    /// <summary>Returns ground samples excluding the origin, or null for a blocked/ungrounded edge.
    /// Nothing is cached across routes: doors and other per-instance collision state can change.</summary>
    public IReadOnlyList<BotPosition>? TraceEdge(int mapId, BotPosition start, BotPosition destination)
    {
        if (!Finite(start) || !Finite(destination)) return null;
        GeoMap map = maps(mapId);
        float dx = destination.X - start.X, dy = destination.Y - start.Y;
        float distance = MathF.Sqrt(dx * dx + dy * dy);
        if (!float.IsFinite(distance) || distance > 200) return null;
        int steps = Math.Max(1, (int)MathF.Ceiling(distance / SampleSpacing));
        float startZ = map.GetZ(start.X, start.Y, start.Z + HeightWindow, start.Z - HeightWindow, instanceId, true);
        if (!float.IsFinite(startZ)) return null;
        var previous = start with { Z = startZ };
        var samples = new List<BotPosition>(steps);
        for (int step = 1; step <= steps; step++)
        {
            float t = (float)step / steps;
            float x = start.X + dx * t, y = start.Y + dy * t;
            float approximateZ = start.Z + (destination.Z - start.Z) * t;
            float z = map.GetZ(x, y, approximateZ + HeightWindow, approximateZ - HeightWindow, instanceId, true);
            if (!float.IsFinite(z)) return null;
            float horizontalStep = MathF.Sqrt(MathF.Pow(x - previous.X, 2) + MathF.Pow(y - previous.Y, 2));
            // Do not connect different floors or climb a cliff between otherwise valid endpoint samples.
            if (MathF.Abs(z - previous.Z) > horizontalStep + 0.05f) return null;
            // Ground normalization may make this a stationary sample. A zero-direction ray has no
            // segment to test and can visit unrelated scene bounds. Keep the ground lookup above,
            // and continue checking every nonzero segment with the ordinary race collision rules.
            if ((x != previous.X || y != previous.Y || z != previous.Z) &&
                map.GetCollisions(previous.X, previous.Y, previous.Z + GeoMap.COLLISION_CHECK_Z_OFFSET,
                x, y, z + GeoMap.COLLISION_CHECK_Z_OFFSET, instanceId,
                CollisionIntention.DEFAULT_COLLISIONS.GetId(), ignoreProperties).GetClosestCollision() != null) return null;
            previous = new BotPosition(x, y, z, destination.Heading);
            samples.Add(previous);
        }
        return samples;
    }

    private static bool Finite(BotPosition p) => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);
}
