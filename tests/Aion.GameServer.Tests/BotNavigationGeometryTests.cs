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
