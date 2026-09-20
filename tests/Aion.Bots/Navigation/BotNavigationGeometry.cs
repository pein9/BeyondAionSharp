using Aion.Bots.World;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Models;
using Aion.GameServer.Model;
using Aion.GameServer.World.Geo;

namespace Aion.Bots.Navigation;

/// <summary>Ground-only bot routes over the same checked-in geometry used by the server.</summary>
public sealed class BotNavigationGeometry(Func<int, GeoMap> maps, int instanceId, IgnoreProperties ignoreProperties)
{
    public const float SampleSpacing = 2f;
    private const float HeightWindow = 2f;

    public static BotNavigationGeometry ForServerWorld(int instanceId, Race race) =>
        new(GeoService.GetInstance().GetMap, instanceId, IgnoreProperties.Of(race));

    /// <summary>Bounded local ground search when spawn/walker waypoints leave a gap. This is not a navmesh:
    /// it cannot invent jumps, open doors or cross maps, and reports no route when its budget is exhausted.</summary>
    public IReadOnlyList<BotPosition> FindLocalPath(int mapId, BotPosition start, BotPosition destination)
        => FindGroundPath(mapId, start, destination, 200, 8192, 20);

    /// <summary>Bounded longer starter-journey search. Uses the same two-metre ground/collision checks,
    /// not unchecked interpolation across gaps in the sparse spawn graph.</summary>
    public IReadOnlyList<BotPosition> FindJourneyPath(int mapId, BotPosition start, BotPosition destination)
        => FindGroundPath(mapId, start, destination, 1000, 65536, 60);

    private IReadOnlyList<BotPosition> FindGroundPath(int mapId, BotPosition start, BotPosition destination,
        float maximumDistance, int maximumVisited, float padding)
    {
        if (!Finite(start) || !Finite(destination) || Distance(start, destination) > maximumDistance) return [];
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
            var tail = Distance(current, destination) <= 3 ? TraceEdge(mapId, current, destination) : null;
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
                    if (edge == null) return [];
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
                candidate = edge[^1];
                var next = new Cell(x, y, (int)MathF.Round(candidate.Z * 2));
                float cost = costs[cell] + Distance(current, candidate);
                if (closed.Contains(next) || costs.TryGetValue(next, out float old) && old <= cost) continue;
                costs[next] = cost; positions[next] = candidate; parent[next] = cell;
                open.Enqueue(next, (cost + Distance(candidate, destination), order++));
            }
        }
        return [];
    }

    private readonly record struct Cell(int X, int Y, int Z);
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
