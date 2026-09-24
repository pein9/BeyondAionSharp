using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.World;
using Aion.Commons.Nio;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Models;
using Aion.GameServer.GeoEngine.Scene;

namespace Aion.GameServer.Tests;

/// <summary>Baked-navmesh routing on a synthetic 100 m map: flat 10 m ground with a 25 m wall at x = 20.</summary>
public sealed class BotNavMeshTests
{
	private const int MapId = 1;
	private static readonly Lazy<(GeoMap Map, BotNavMeshSet Set, string Directory)> World = new(Build);

	private static (GeoMap, BotNavMeshSet, string) Build()
	{
		var map = Ground();
		AddWall(map);
		var input = BotNavMeshInput.Create(map, Heights(), 50, 100, new HashSet<int>());
		BotNavMeshBakeResult result = BotNavMeshBaker.Bake(input, new BotNavMeshSettings(), waterLevel: 0);
		string directory = Path.Combine(Path.GetTempPath(), "bot-navmesh-tests-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		byte[] bytes = BotNavMesh.Write(result.NavMesh);
		File.WriteAllBytes(BotNavMesh.PathFor(directory, MapId), bytes);
		File.WriteAllText(BotNavMesh.ManifestPathFor(directory, MapId), System.Text.Json.JsonSerializer.Serialize(
			new BotNavMeshManifest { MapId = MapId, Settings = new BotNavMeshSettings(), Polygons = result.Polygons }, BotNavMeshManifest.Json));
		return (map, new BotNavMeshSet(directory), directory);
	}

	private static BotNavigationGeometry Geometry() =>
		new BotNavigationGeometry(_ => World.Value.Map, 1, IgnoreProperties.ANY_RACE).WithNavMesh(World.Value.Set);

	[Fact]
	public void BakeIsDeterministicAndSerializationRoundTrips()
	{
		var map = Ground();
		AddWall(map);
		var input = BotNavMeshInput.Create(map, Heights(), 50, 100, new HashSet<int>());
		byte[] first = BotNavMesh.Write(BotNavMeshBaker.Bake(input, new BotNavMeshSettings(), 0).NavMesh);
		byte[] second = BotNavMesh.Write(BotNavMeshBaker.Bake(input, new BotNavMeshSettings(), 0, threads: 1).NavMesh);
		Assert.Equal(first, second);
		BotNavMesh loaded = World.Value.Set.Get(MapId)!;
		Assert.NotEmpty(loaded.Polygons());
		Assert.Null(World.Value.Set.Get(2));
	}

	[Fact]
	public void RouteGoesAroundTheWallAndEveryStepPassesTheServerGroundCheck()
	{
		BotNavigationGeometry geometry = Geometry();
		BotPosition start = new(10, 10, 10, 0), end = new(30, 10, 10, 0);
		IReadOnlyList<BotPosition> path = geometry.FindJourneyPath(MapId, start, end);
		Assert.Equal(BotNavRouteOutcome.Routed, BotNavMeshRouter.LastOutcome);
		Assert.NotEmpty(path);
		Assert.Equal(end, path[^1]);
		Assert.Contains(path, p => p.Y > 25);
		BotPosition previous = start;
		foreach (BotPosition point in path)
		{
			Assert.True(MathF.Sqrt((point.X - previous.X) * (point.X - previous.X) + (point.Y - previous.Y) * (point.Y - previous.Y)) <= 2.01f);
			Assert.NotNull(geometry.TraceEdge(MapId, previous, point));
			previous = point;
		}
		// Callers move a fixed number of points per turn, so points keep the grid search's ~2 m spacing.
		float length = 0;
		previous = start;
		foreach (BotPosition point in path)
		{
			length += MathF.Sqrt((point.X - previous.X) * (point.X - previous.X) + (point.Y - previous.Y) * (point.Y - previous.Y));
			previous = point;
		}
		Assert.True(length / path.Count >= 1.6f, $"mean spacing {length / path.Count:F2} m");
		// The navmesh route is much shorter than the grid budget allows and never crosses the wall.
		Assert.DoesNotContain(path, p => p.Y < 25 && MathF.Abs(p.X - 20) < 0.3f);
	}

	[Fact]
	public void ObservedHazardsAreHardRulesAndStaticDangerOnlyBendsTheRoute()
	{
		BotNavigationGeometry geometry = Geometry();
		BotNavMeshRouter router = geometry.NavMesh!;
		BotPosition start = new(10, 10, 10, 0), end = new(30, 10, 10, 0);
		// A hazard covering the only way around the wall's end (the wall spans y 0..25 at x = 20).
		var blocking = new BotNavigationHazard(new BotPosition(20, 62, 10, 0), 40);
		Assert.Empty(geometry.FindJourneyPathAvoiding(MapId, start, end, [blocking]));
		// A hazard off to the side is avoided while the route still arrives.
		var aside = new BotNavigationHazard(new BotPosition(10, 20, 10, 0), 4);
		IReadOnlyList<BotPosition> avoiding = geometry.FindJourneyPathAvoiding(MapId, start, end, [aside]);
		Assert.NotEmpty(avoiding);
		Assert.True(BotNavigationGeometry.AvoidsHazards(start, avoiding, [aside]));
		// A destination inside an observed hazard is refused outright.
		Assert.Empty(geometry.FindJourneyPathAvoiding(MapId, start, end, [new BotNavigationHazard(end, 5)]));
		// Soft danger is only a cost: routes still arrive, and a heavily weighted zone is entered less.
		BotPosition west = new(40, 50, 10, 0), east = new(90, 50, 10, 0);
		var zone = new BotNavDanger(65, 50, 12, 50);
		IReadOnlyList<BotPosition> straight = router.FindPath(MapId, west, east);
		IReadOnlyList<BotPosition> bent = router.FindPath(MapId, west, east, BotNavQuery.Default with { Danger = [zone] });
		Assert.NotEmpty(straight);
		Assert.NotEmpty(bent);
		Assert.Equal(east, bent[^1]);
		static int Inside(IEnumerable<BotPosition> route, BotNavDanger zone) =>
			route.Count(p => (p.X - zone.X) * (p.X - zone.X) + (p.Y - zone.Y) * (p.Y - zone.Y) < zone.Radius * zone.Radius);
		Assert.True(Inside(bent, zone) < Inside(straight, zone), $"danger samples {Inside(bent, zone)} vs {Inside(straight, zone)}");
	}

	[Fact]
	public void InteractionAndRangedApproachesStopShortOfTheTarget()
	{
		BotNavigationGeometry geometry = Geometry();
		BotPosition start = new(10, 10, 10, 0);
		// A target standing inside the wall: any ground within three metres is enough.
		var target = new BotPosition(20, 10, 10, 0);
		IReadOnlyList<BotPosition> interaction = geometry.FindInteractionPath(MapId, start, target);
		Assert.NotEmpty(interaction);
		Assert.True(Distance(interaction[^1], target) <= 3);
		// Ranged: within 20 m with line of sight; the far side of the wall needs a way around.
		var ranged = geometry.FindRangedApproachPath(MapId, start, new BotPosition(40, 5, 10, 0), []);
		Assert.NotEmpty(ranged);
		Assert.True(Distance(ranged[^1], new BotPosition(40, 5, 10, 0)) <= 20);
		Assert.True(geometry.HasLineOfSight(MapId, ranged[^1], new BotPosition(40, 5, 10, 0)));
	}

	[Fact]
	public void MapsWithoutANavMeshKeepTheGridSearch()
	{
		var other = Ground();
		AddWall(other);
		var geometry = new BotNavigationGeometry(_ => other, 1, IgnoreProperties.ANY_RACE)
			.WithNavMesh(new BotNavMeshSet(Path.Combine(Path.GetTempPath(), "no-navmesh-" + Guid.NewGuid().ToString("N"))));
		Assert.False(geometry.NavMesh!.Covers(MapId));
		IReadOnlyList<BotPosition> path = geometry.FindLocalPath(MapId, new BotPosition(10, 10, 10, 0), new BotPosition(30, 10, 10, 0));
		Assert.NotEmpty(path);
		Assert.Equal(path, geometry.GridLocalPath(MapId, new BotPosition(10, 10, 10, 0), new BotPosition(30, 10, 10, 0)));
		Assert.Null(new BotNavigationGeometry(_ => other, 1, IgnoreProperties.ANY_RACE).WithNavMesh(null).NavMesh);
	}

	[Fact]
	public void RoadsBecomeConvexQuadsAndPreferredRoutesFollowThem()
	{
		string root = Path.Combine(Path.GetTempPath(), "bot-roads-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.GetDirectoryName(BotNavRoads.PathFor(root, MapId))!);
		File.WriteAllText(BotNavRoads.PathFor(root, MapId), """{"mapId":1,"source":"test","roads":[{"id":"r0","points":[[70,10],[70,90]]}]}""");
		IReadOnlyList<BotNavVolume> volumes = BotNavRoads.LoadVolumes(root, MapId, 3);
		BotNavVolume quad = Assert.Single(volumes);
		Assert.Equal(8, quad.Footprint.Length);
		Assert.Equal(BotNavAreas.Road, quad.Area);
		Assert.Equal(0, BotNavRoads.Distance(BotNavRoads.Load(root, MapId), new BotPosition(70, 50, 10, 0)), 3);

		var map = Ground();
		var input = BotNavMeshInput.Create(map, Heights(), 50, 100, new HashSet<int>());
		BotNavMeshBakeResult result = BotNavMeshBaker.Bake(input, new BotNavMeshSettings(), 0, volumes);
		Assert.True(result.PolygonsByArea.GetValueOrDefault(BotNavAreas.Road) > 0);
		var mesh = new BotNavMesh(result.NavMesh, new BotNavMeshManifest { MapId = MapId });
		// With a strong road preference, a trip between two points beside the road bends onto it.
		IReadOnlyList<BotPosition> corners = mesh.FindCorners(new BotPosition(60, 15, 10, 0), new BotPosition(60, 85, 10, 0),
			BotNavQuery.Default with { GroundCost = 3f });
		Assert.Contains(corners, p => MathF.Abs(p.X - 70) < 3.5f);
		IReadOnlyList<BotPosition> direct = mesh.FindCorners(new BotPosition(60, 15, 10, 0), new BotPosition(60, 85, 10, 0),
			BotNavQuery.Default with { GroundCost = 1f });
		Assert.DoesNotContain(direct, p => MathF.Abs(p.X - 70) < 3.5f);
	}

	[Fact]
	public void TravelPlannerWeighsDangerByLevelAndFallsBackToTheDirectRoute()
	{
		Assert.Equal(1f, BotTravelPlanner.Weight(3, 9));
		Assert.Equal(3f, BotTravelPlanner.Weight(9, 9));
		Assert.Equal(40f, BotTravelPlanner.Weight(15, 9));
		var safe = new BotTravelEdge(0, 1, 100, 1, []);
		var risky = new BotTravelEdge(0, 1, 100, 0, [new BotTravelDanger(1, 12, 20)]);
		Assert.True(BotTravelPlanner.EdgeCost(safe, 9) < BotTravelPlanner.EdgeCost(risky, 9));
		Assert.Equal(120, BotTravelPlanner.EdgeCost(risky, 20), 3);

		// Start attaches only to a, the goal only to b. The direct link crosses a level-15 camp.
		BotPosition P(float x, float y) => new(x, y, 10, 0);
		var graph = new BotTravelGraph
		{
			MapId = MapId,
			Nodes =
			[
				new BotTravelNode(0, BotTravelNodeKind.Road, "a", 0, P(0, 0)),
				new BotTravelNode(1, BotTravelNodeKind.Road, "b", 0, P(1000, 0)),
				new BotTravelNode(2, BotTravelNodeKind.Road, "c", 0, P(500, 400)),
			],
			Edges =
			[
				new BotTravelEdge(0, 1, 1000, 0, [new BotTravelDanger(7, 15, 100)]),
				new BotTravelEdge(0, 2, 640, 1, []),
				new BotTravelEdge(1, 2, 640, 1, []),
			],
		};
		var planner = new BotTravelPlanner(graph, Geometry().NavMesh!, []);
		Assert.Equal([0, 2, 1], planner.GraphRoute(P(5, 0), P(995, 0), 5).Select(n => n.Id));
		Assert.Equal([0, 1], planner.GraphRoute(P(5, 0), P(995, 0), 30).Select(n => n.Id));
		Assert.Empty(planner.GraphRoute(P(5, 0), P(3000, 3000), 5));
		// On the synthetic map (no nearby graph nodes) the plan is the checked direct navmesh route.
		planner = new BotTravelPlanner(new BotTravelGraph { MapId = MapId }, Geometry().NavMesh!, []);
		BotTravelPlan plan = planner.Plan(new BotPosition(10, 10, 10, 0), new BotPosition(30, 10, 10, 0), 5, interaction: false);
		Assert.NotEmpty(plan.Route);
		Assert.Equal(new BotPosition(30, 10, 10, 0), plan.Route[^1]);
	}

	private static float Distance(BotPosition a, BotPosition b) =>
		MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));

	private static short[] Heights() => Enumerable.Repeat((short)320, 50 * 50).ToArray();

	private static GeoMap Ground()
	{
		var terrain = new Terrain();
		terrain.SetHeightmap(Heights(), 50, 50);
		var map = new GeoMap(MapId);
		map.SetTerrain(terrain);
		return map;
	}

	private static void AddWall(GeoMap map)
	{
		var mesh = new Mesh();
		mesh.SetVertices(FloatBuffer.Wrap([20, 0, 0, 20, 25, 0, 20, 25, 30, 20, 0, 30]));
		mesh.SetIndices(ByteBuffer.Wrap([0, 1, 2, 0, 2, 3]));
		mesh.SetCollisionIntentions(CollisionIntention.PHYSICAL.GetId());
		var wall = new Geometry("wall", mesh);
		wall.UpdateModelBound();
		map.AttachChild(wall);
		map.UpdateModelBound();
		mesh.CreateCollisionData();
	}
}
