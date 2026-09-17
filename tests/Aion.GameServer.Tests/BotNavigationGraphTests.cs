using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Gather;
using Aion.GameServer.Model.Templates.Portal;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Walker;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

public sealed class BotNavigationGraphTests
{
	[Fact]
	public async Task BuildsIshalgenGraphFromCheckedInServerData()
	{
		var staticData = (await RealStaticData.LoadAsync()).StaticData;
		var graph = BotNavigationGraphFactory.Build([220010000], staticData.SpawnsDh, staticData.WalkerDataDh,
			staticData.GatherableDataDh, staticData.Portal2DataDh, staticData.PortalLocs,
			staticData.BindPointDataDh, [203500]);
		var ishalgen = Assert.IsType<BotMapNavigationGraph>(graph.GetMap(220010000));

		Assert.Contains(ishalgen.Waypoints, node => node.Sources.HasFlag(BotWaypointSource.Spawn));
		Assert.Contains(ishalgen.Waypoints, node => node.Sources.HasFlag(BotWaypointSource.WalkerRoute));
		Assert.Contains(ishalgen.Waypoints, node => node.Sources.HasFlag(BotWaypointSource.Gather));
		Assert.Contains(ishalgen.Waypoints, node => node.Sources.HasFlag(BotWaypointSource.Portal));
		Assert.Contains(ishalgen.Waypoints, node => node.Sources.HasFlag(BotWaypointSource.BindPoint));
		Assert.Contains(ishalgen.Waypoints,
			node => node.TemplateId == 203500 && node.Sources.HasFlag(BotWaypointSource.QuestNpc));
	}

	[Fact]
	public void BuildsPerMapGraphFromEveryStaticSourceAndFindsAStarPath()
	{
		var graph = BuildGraph(
			[
				SpawnMap(1,
					Spawn(100, (0, 0, 0, null)),
					Spawn(200, (10, 0, 1, null)),
					Spawn(300, (20, 0, 2, "route-a"))),
				SpawnMap(2, Spawn(400, (0, 0, 0, null))),
			],
			[new WalkerTemplate("route-a")
			{
				routeStepList = [new RouteStep(30, 0, 3, 0), new RouteStep(40, 0, 4, 0)],
			}],
			gatherIds: [200], portalNpcIds: [300], bindNpcIds: [300], questNpcIds: [100],
			portalLocations: [new PortalLocSummary(1, 9001, 50, 0, 5, 0)]);

		var map = Assert.IsType<BotMapNavigationGraph>(graph.GetMap(1));
		Assert.Single(Assert.IsType<BotMapNavigationGraph>(graph.GetMap(2)).Waypoints);
		var quest = Assert.Single(map.Waypoints, node => node.TemplateId == 100);
		var gather = Assert.Single(map.Waypoints, node => node.TemplateId == 200);
		var bindPortal = Assert.Single(map.Waypoints, node => node.TemplateId == 300);
		var portalDestination = Assert.Single(map.Waypoints, node => node.TemplateId == 9001);
		Assert.Equal(BotWaypointSource.Spawn | BotWaypointSource.QuestNpc, quest.Sources);
		Assert.Equal(BotWaypointSource.Spawn | BotWaypointSource.Gather, gather.Sources);
		Assert.Equal(BotWaypointSource.Spawn | BotWaypointSource.Portal | BotWaypointSource.BindPoint,
			bindPortal.Sources);
		Assert.Equal(2, map.Waypoints.Count(node => node.Sources == BotWaypointSource.WalkerRoute));
		Assert.Equal(BotWaypointSource.Portal, portalDestination.Sources);

		var path = map.FindPath(quest.Id, portalDestination.Id);
		Assert.Equal([1f, 2f, 3f, 4f, 5f], path.Select(node => node.Position.Z));
		Assert.All(path.Zip(path.Skip(1)), pair =>
			Assert.InRange(Distance(pair.First.Position, pair.Second.Position), 0, BotNavigationGraph.MaximumEdgeDistance));
	}

	[Fact]
	public void EdgeLimitIsInclusiveAndMapsNeverConnect()
	{
		var graph = BuildGraph(
			[
				SpawnMap(1, Spawn(1, (0, 0, 0, null)), Spawn(2, (20, 0, 0, null)),
					Spawn(3, (40.01f, 0, 0, null))),
				SpawnMap(2, Spawn(4, (20, 0, 0, null))),
			]);
		var map = graph.GetMap(1)!;
		var first = Assert.Single(map.Waypoints, node => node.TemplateId == 1);
		var boundary = Assert.Single(map.Waypoints, node => node.TemplateId == 2);
		var beyond = Assert.Single(map.Waypoints, node => node.TemplateId == 3);

		Assert.Contains(boundary.Id, map.GetNeighbours(first.Id));
		Assert.DoesNotContain(beyond.Id, map.GetNeighbours(boundary.Id));
		Assert.Empty(map.FindPath(first.Id, beyond.Id));
		Assert.DoesNotContain(graph.GetMap(2)!.Waypoints[0].Id, map.GetNeighbours(boundary.Id));
	}

	[Fact]
	public void NpcDestinationAlwaysComesFromNpcInfoAndMovePackets()
	{
		var graph = BuildGraph([
			SpawnMap(1,
				Spawn(1, (0, 0, 10, null)),
				Spawn(2, (10, 0, 11, null)),
				Spawn(3, (20, 0, 12, null)),
				Spawn(700001, (100, 0, 99, null))),
		]);
		var world = new BotWorldModel();
		world.Apply(Packet<SM_TELEPORT_LOC>(
			("mapId", 1), ("x", 0f), ("y", 0f), ("z", 10f), ("heading", (byte)0)));
		world.Apply(Packet<SM_NPC_INFO>(
			("objectId", 77), ("npcId", 700001), ("visualNpcId", 700001),
			("x", 30f), ("y", 0f), ("z", 13f)));

		var navigator = new BotWorldNavigator(graph);
		var initial = navigator.FindPathToNpc(world, 77);
		Assert.Equal(new BotPosition(30, 0, 13, 0), initial[^1]);
		Assert.DoesNotContain(initial, point => point.X == 100 || point.Z == 99);

		world.Apply(Packet<SM_MOVE>(
			("objectId", 77), ("x", 15f), ("y", 0f), ("z", 21f), ("heading", (byte)9)));
		var moved = navigator.FindPathToNpc(world, 77);
		Assert.Equal(new BotPosition(15, 0, 21, 9), moved[^1]);
	}

	private static BotNavigationGraph BuildGraph(IReadOnlyList<SpawnMap> maps,
		IReadOnlyList<WalkerTemplate>? routes = null, IReadOnlyList<int>? gatherIds = null,
		IReadOnlyList<int>? portalNpcIds = null, IReadOnlyList<int>? bindNpcIds = null,
		IReadOnlyList<int>? questNpcIds = null, IReadOnlyList<PortalLocSummary>? portalLocations = null)
	{
		var spawns = new SpawnsData { Templates = maps.ToList() };
		spawns.Initialize();
		var walkers = new WalkerData { walkerlist = routes?.ToList() ?? [] };
		walkers.AfterUnmarshal(new object());
		var gatherables = new GatherableData
		{
			gatherables = gatherIds?.Select(id => new GatherableTemplate { id = id }).ToList() ?? [],
		};
		gatherables.AfterUnmarshal(new object());
		var portals = new Portal2Data
		{
			portalUse = portalNpcIds?.Select(id => new PortalUse { npcId = id, portalPaths = [] }).ToList() ?? [],
			portalDialog = [],
			portalScroll = [],
		};
		portals.AfterUnmarshal(new object());
		var bindPoints = new BindPointData
		{
			bplist = bindNpcIds?.Select(id => new BindPointTemplate { npcId = id }).ToList() ?? [],
		};
		bindPoints.AfterUnmarshal(new object());

		return BotNavigationGraphFactory.Build(maps.Select(map => map.GetMapId()), spawns, walkers,
			gatherables, portals, new PortalLocTable(portalLocations ?? []), bindPoints, questNpcIds);
	}

	private static SpawnMap SpawnMap(int mapId, params Spawn[] spawns) =>
		new(mapId) { Spawns = spawns.ToList() };

	private static Spawn Spawn(int npcId, params (float X, float Y, float Z, string? WalkerId)[] spots) =>
		new()
		{
			NpcId = npcId,
			RespawnTime = 1,
			SpawnTemplates = spots.Select(spot => new SpawnSpotTemplate(spot.X, spot.Y, spot.Z, 0, 0,
				spot.WalkerId, null)).ToList(),
		};

	private static DecodedBotServerPacket Packet<T>(params (string Name, object? Value)[] fields) =>
		new(typeof(T), fields.ToDictionary(field => field.Name, field => field.Value, StringComparer.Ordinal));

	private static float Distance(BotPosition left, BotPosition right)
	{
		var x = left.X - right.X;
		var y = left.Y - right.Y;
		var z = left.Z - right.Z;
		return MathF.Sqrt(x * x + y * y + z * z);
	}
}
