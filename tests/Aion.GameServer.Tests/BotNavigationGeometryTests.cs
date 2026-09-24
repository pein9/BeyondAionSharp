using Aion.Bots.Navigation;
using Aion.Bots.World;
using Aion.Commons.Nio;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Models;
using Aion.GameServer.GeoEngine.Scene;

namespace Aion.GameServer.Tests;

public sealed class BotNavigationGeometryTests
{
    [Fact]
    public void StationaryGroundSampleDoesNotCastAZeroLengthCollisionRay()
    {
        var terrain = new Terrain(); terrain.SetHeightmap(Enumerable.Repeat((short)320, 2500).ToArray(), 50, 50);
        var map = new RejectDegenerateRayMap(); map.SetTerrain(terrain);
        var geometry = new BotNavigationGeometry(_ => map, 1, IgnoreProperties.ELYOS);
        var point = new BotPosition(10, 10, 10, 0);
        Assert.Equal(point, Assert.Single(geometry.TraceEdge(1, point, point)!));
        Assert.NotNull(geometry.TraceEdge(1, point, point with { X = 12 }));
    }

    private sealed class RejectDegenerateRayMap() : GeoMap(1)
    {
        public override int CollideWith(Collidable other, CollisionResults results)
        {
            if (other is Aion.GameServer.GeoEngine.Math.Ray ray) Assert.True(ray.limit > 0, "A stationary sample has no collision segment.");
            return base.CollideWith(other, results);
        }
    }

    [Fact]
    public void JourneySearchCrossesLongSparseGapsButStillChecksEverySegmentAndBoundsDistance()
    {
        var map = Ground(size: 300); AddWall(map);
        var geometry = new BotNavigationGeometry(_ => map, 1, IgnoreProperties.ANY_RACE);
        BotPosition start = new(10, 10, 10, 0), end = new(510, 10, 10, 0);
        Assert.Empty(geometry.FindLocalPath(1, start, end));
        var path = geometry.FindJourneyPath(1, start, end);
        Assert.NotEmpty(path); Assert.Equal(end, path[^1]);
        Assert.Contains(path, point => point.Y > 25);
        foreach (var point in path)
        {
            Assert.NotNull(geometry.TraceEdge(1, start, point));
            start = point;
        }
        Assert.Empty(geometry.FindJourneyPath(1, end, end with { X = 1511 }));
        Assert.Empty(geometry.FindJourneyPath(1, end, end with { X = float.NaN }));
    }

    [Fact]
    public void BoundedLocalSearchBridgesSparseWaypointsWithoutCrossingTheWall()
    {
        var map = Ground(); AddWall(map);
        var geometry = new BotNavigationGeometry(_ => map, 1, IgnoreProperties.ANY_RACE);
        BotPosition start = new(10, 10, 10, 0), end = new(30, 10, 10, 0);
        var path = geometry.FindLocalPath(1, start, end);
        Assert.NotEmpty(path); Assert.Equal(end, path[^1]);
        Assert.Contains(path, p => p.Y > 25);
        foreach (var point in path)
        {
            Assert.NotNull(geometry.TraceEdge(1, start, point)); start = point;
        }
        Assert.Empty(geometry.FindLocalPath(1, end, end with { X = 1000 }));
        Assert.Null(geometry.TraceEdge(1, end, end with { X = float.MaxValue }));
        Assert.Empty(new BotNavigationGeometry(_ => new GeoMap(1), 1, IgnoreProperties.ANY_RACE)
            .FindLocalPath(1, start, end));
    }

    [Fact]
    public void JourneyRouteAvoidsObservedAggroCircleAndCanEscapeOneAlreadyEntered()
    {
        var map = Ground(size: 80);
        var geometry = new BotNavigationGeometry(_ => map, 1, IgnoreProperties.ANY_RACE);
        BotPosition start = new(10, 20, 10, 0), end = new(50, 20, 10, 0);
        BotNavigationHazard[] hazards = [new(new BotPosition(30, 20, 10, 0), 8)];
        IReadOnlyList<BotPosition> detour = geometry.FindJourneyPathAvoiding(1, start, end, hazards);
        Assert.NotEmpty(detour);
        Assert.Equal(end, detour[^1]);
        Assert.True(BotNavigationGeometry.AvoidsHazards(start, detour, hazards));
        Assert.Contains(detour, point => MathF.Abs(point.Y - 20) >= 8);

        BotPosition trapped = new(26, 20, 10, 0);
        IReadOnlyList<BotPosition> escape = geometry.FindJourneyPathAvoiding(1, trapped,
            new BotPosition(10, 20, 10, 0), hazards);
        Assert.NotEmpty(escape);
        Assert.True(BotNavigationGeometry.AvoidsHazards(trapped, escape, hazards));
        Assert.False(BotNavigationGeometry.AvoidsHazards(start,
            [new BotPosition(30, 20, 10, 0)], hazards));
    }

    [Fact]
    public void LongGroundRouteCanPreferMappedRoadWithoutTreatingItAsWalkability()
    {
        var geometry = new BotNavigationGeometry(_ => Ground(size: 180), 1, IgnoreProperties.ANY_RACE);
        BotPosition start = new(10, 10, 10, 0), end = new(130, 10, 10, 0);
        BotRoadPoint[] road = [new(20, 30), new(120, 30)];
        IReadOnlyList<BotPosition> route = geometry.FindRoadPreferredJourneyPath(1,
            start, end, road, []);
        Assert.NotEmpty(route);
        Assert.Equal(end, route[^1]);
        Assert.Contains(route, point => point.Y >= 25);
        foreach (BotPosition point in route)
        {
            Assert.NotNull(geometry.TraceEdge(1, start, point));
            start = point;
        }
        Assert.Empty(geometry.FindRoadPreferredJourneyPath(1,
            new BotPosition(10, 130, 10, 0), new BotPosition(130, 130, 10, 0), road, []));
    }

    [Fact]
    public void MappedRoadStillAvoidsObservedAggroCircle()
    {
        var geometry = new BotNavigationGeometry(_ => Ground(size: 180), 1, IgnoreProperties.ANY_RACE);
        BotPosition start = new(10, 10, 10, 0), end = new(130, 10, 10, 0);
        BotRoadPoint[] road = [new(20, 30), new(120, 30)];
        BotNavigationHazard[] hazards = [new(new BotPosition(70, 30, 10, 0), 9)];
        IReadOnlyList<BotPosition> route = geometry.FindRoadPreferredJourneyPath(1,
            start, end, road, hazards);
        Assert.NotEmpty(route);
        Assert.True(BotNavigationGeometry.AvoidsHazards(start, route, hazards));
    }

    [Fact]
    public void ClientMapCalibrationPlacesMijouAndMauSacksNearTheRoadHint()
    {
        BotRoadPoint[] road = NaturalIshalgenRoads.MijouToMauFarms;
        Assert.InRange(MathF.Sqrt(MathF.Pow(road[0].X - 946.253f, 2) +
            MathF.Pow(road[0].Y - 1702.775f, 2)), 0, 20);
        Assert.InRange(MathF.Sqrt(MathF.Pow(road[^1].X - 742.801f, 2) +
            MathF.Pow(road[^1].Y - 1515.77f, 2)), 0, 35);
    }

    [Fact]
    public void DestinationInsideNewAggroCircleHasNoHazardFreeRoute()
    {
        var geometry = new BotNavigationGeometry(_ => Ground(size: 80), 1, IgnoreProperties.ANY_RACE);
        BotPosition start = new(10, 20, 10, 0), coveredObject = new(50, 20, 10, 0);
        BotNavigationHazard[] hazards = [new(coveredObject, 8)];
        Assert.Empty(geometry.FindJourneyPathAvoiding(1, start, coveredObject, hazards));
    }

    [Fact]
    public void OverlappingObservedAggroCirclesAllowOnlyNonWorseningPackEscape()
    {
        var map = Ground(size: 80);
        var geometry = new BotNavigationGeometry(_ => map, 1, IgnoreProperties.ANY_RACE);
        BotPosition start = new(30, 30, 10, 0), end = new(30, 50, 10, 0);
        BotNavigationHazard[] hazards =
        [
            new(new BotPosition(27, 30, 10, 0), 8),
            new(new BotPosition(33, 30, 10, 0), 8),
        ];
        IReadOnlyList<BotPosition> escape = geometry.FindJourneyPathAvoiding(1, start, end, hazards);
        Assert.NotEmpty(escape);
        Assert.True(BotNavigationGeometry.AvoidsHazards(start, escape, hazards));
        Assert.True(BotNavigationGeometry.AvoidsHazards(start,
            [new BotPosition(29, 30, 10, 0)], hazards)); // Neutral step in overlapping circles.
        BotNavigationHazard[] asymmetricalPack =
        [.. hazards, new(new BotPosition(30, 33, 10, 0), 8)];
        Assert.False(BotNavigationGeometry.AvoidsHazards(start,
            [new BotPosition(30, 31, 10, 0)], asymmetricalPack));
    }

    [Fact]
    public void ObservedHostileWallCanRequireAWideCheckedDetour()
    {
        var map = Ground(size: 120);
        var geometry = new BotNavigationGeometry(_ => map, 1, IgnoreProperties.ANY_RACE);
        BotPosition start = new(20, 100, 10, 0), end = new(120, 100, 10, 0);
        BotNavigationHazard[] wall = Enumerable.Range(0, 9)
            .Select(index => new BotNavigationHazard(
                new BotPosition(70, 45 + index * 15, 10, 0), 10)).ToArray();
        IReadOnlyList<BotPosition> detour = geometry.FindJourneyPathAvoiding(1, start, end, wall);
        Assert.NotEmpty(detour);
        Assert.True(BotNavigationGeometry.AvoidsHazards(start, detour, wall));
        Assert.Contains(detour, point => point.Y < 40 || point.Y > 160);
    }

    [Fact]
    public void BlockedDirectShortcutAndGraphEdgesUseTheClearDetour()
    {
        var map = Ground();
        AddWall(map);
        var geometry = new BotNavigationGeometry(_ => map, 1, IgnoreProperties.ANY_RACE);
        BotPosition start = new(10, 10, 10, 0), end = new(30, 10, 10, 0);
        var graph = Graph(geometry, start, new(10, 26, 10, 0), new(20, 30, 10, 0), new(30, 26, 10, 0), end);
        Assert.Null(geometry.TraceEdge(1, start, end));
        Assert.DoesNotContain(4, graph.GetMap(1)!.GetNeighbours(0));
        var path = graph.FindPath(1, start, end);
        Assert.NotEmpty(path);
        Assert.Equal(end, path[^1]);
        Assert.Contains(path, point => point.Y > 25);
        var previous = start;
        foreach (var point in path)
        {
            Assert.InRange(MathF.Sqrt(MathF.Pow(point.X - previous.X, 2) + MathF.Pow(point.Y - previous.Y, 2)), 0, 2.001f);
            Assert.NotNull(geometry.TraceEdge(1, previous, point));
            previous = point;
        }
    }

    [Fact]
    public void InteractionApproachStopsOnCheckedGroundNearBlockedNpcCenter()
    {
        var map = Ground(); AddWall(map);
        var geometry = new BotNavigationGeometry(_ => map, 1, IgnoreProperties.ANY_RACE);
        BotPosition start = new(10, 10, 10, 0), npc = new(20, 10, 10, 0);
        Assert.Null(geometry.TraceEdge(1, start, npc));
        IReadOnlyList<BotPosition> path = geometry.FindInteractionPath(1, start, npc);
        Assert.NotEmpty(path);
        BotPosition destination = path[^1];
        Assert.True(MathF.Sqrt(MathF.Pow(destination.X - npc.X, 2) +
            MathF.Pow(destination.Y - npc.Y, 2) + MathF.Pow(destination.Z - npc.Z, 2)) <= 3);
        foreach (BotPosition point in path)
        {
            Assert.NotNull(geometry.TraceEdge(1, start, point));
            start = point;
        }
    }

    [Fact]
    public void TemporaryStartAndDestinationConnectorsCannotCrossWalls()
    {
        var map = Ground(); AddWall(map);
        var geometry = new BotNavigationGeometry(_ => map, 1, IgnoreProperties.ANY_RACE);
        Assert.Empty(Graph(geometry, new(30, 10, 10, 0), new(50, 10, 10, 0))
            .FindPath(1, new(10, 10, 10, 0), new(50, 10, 10, 0)));
        Assert.Empty(Graph(geometry, new BotPosition(10, 10, 10, 0))
            .FindPath(1, new(10, 10, 10, 0), new(30, 10, 10, 0)));
    }

    [Fact]
    public void GroundSamplesFollowTerrainAtTwoMetresAndRejectInteriorGaps()
    {
        var map = Ground((x, _) => (short)((10 + x * 0.5f) * 32));
        var geometry = new BotNavigationGeometry(_ => map, 1, IgnoreProperties.ANY_RACE);
        var samples = geometry.TraceEdge(1, new(10, 10, 12.5f, 0), new(28, 10, 17, 127));
        Assert.NotNull(samples); Assert.Equal(9, samples.Count);
        Assert.All(samples, point => Assert.Equal(10 + point.X * 0.25f, point.Z, 3));
        Assert.Equal((byte)127, samples[^1].Heading);
        var hole = Ground((x, _) => x == 10 ? (short)-1 : (short)320);
        Assert.Equal(10, hole.GetZ(10, 10, 12, 8, 1));
        Assert.Equal(10, hole.GetZ(30, 10, 12, 8, 1));
        Assert.Null(new BotNavigationGeometry(_ => hole, 1, IgnoreProperties.ANY_RACE)
            .TraceEdge(1, new(10, 10, 10, 0), new(30, 10, 10, 0)));
        Assert.Null(new BotNavigationGeometry(_ => new GeoMap(1), 1, IgnoreProperties.ANY_RACE)
            .TraceEdge(1, new(10, 10, 10, 0), new(12, 10, 10, 0)));
    }

    [Fact]
    public void PerInstanceCollisionChangesAreNotCachedAcrossRoutes()
    {
        var map = Ground();
        var door = AddWall(map, despawnable: true)!;
        var geometry = new BotNavigationGeometry(_ => map, 1, IgnoreProperties.ANY_RACE);
        BotPosition start = new(10, 10, 10, 0), end = new(30, 10, 10, 0);
        var graph = Graph(geometry, start, end);
        Assert.NotEmpty(graph.FindPath(1, start, end));
        door.SetActive(1, true);
        Assert.Empty(graph.FindPath(1, start, end));
        Assert.NotNull(new BotNavigationGeometry(_ => map, 2, IgnoreProperties.ANY_RACE).TraceEdge(1, start, end));
        door.SetActive(1, false);
        Assert.NotEmpty(graph.FindPath(1, start, end));
    }

    private static BotNavigationGraph Graph(BotNavigationGeometry geometry, params BotPosition[] points) =>
        new(points.Select((point, id) => new BotWaypoint(id, 1, point, BotWaypointSource.Spawn)), geometry);

    private static GeoMap Ground(Func<int, int, short>? sample = null, int size = 50)
    {
        var height = new short[size * size];
        for (int x = 0; x < size; x++) for (int y = 0; y < size; y++) height[x * size + y] = sample?.Invoke(x, y) ?? 320;
        var terrain = new Terrain(); terrain.SetHeightmap(height, size, size);
        var map = new GeoMap(1); map.SetTerrain(terrain); return map;
    }

    private static DespawnableNode? AddWall(GeoMap map, bool despawnable = false)
    {
        var mesh = new Mesh();
        mesh.SetVertices(FloatBuffer.Wrap([20, 0, 0, 20, 25, 0, 20, 25, 30, 20, 0, 30]));
        mesh.SetIndices(ByteBuffer.Wrap([0, 1, 2, 0, 2, 3]));
        mesh.SetCollisionIntentions(CollisionIntention.PHYSICAL.GetId());
        var wall = new Geometry("wall", mesh); wall.UpdateModelBound();
        DespawnableNode? door = null;
        if (despawnable)
        {
            door = new DespawnableNode { type = DespawnableNode.DespawnableType.PLACEABLE, id = 17 };
            door.SetCollisionIntentions(CollisionIntention.ALL.GetId());
            door.AttachChild(wall); door.UpdateModelBound(); map.AttachChild(door);
        }
        else map.AttachChild(wall);
        map.UpdateModelBound(); mesh.CreateCollisionData(); return door;
    }
}
