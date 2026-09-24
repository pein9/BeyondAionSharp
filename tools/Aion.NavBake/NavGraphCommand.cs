using Aion.Bots.Navigation.NavMesh;

namespace Aion.NavBake;

/// <summary>Builds <c>&lt;mapId&gt;.graph.json</c> travel graphs next to the baked navmeshes.</summary>
internal static class NavGraphCommand
{
	public static int Run(BotNavWorld world, int[] maps, string navDir)
	{
		var set = new BotNavMeshSet(navDir);
		foreach (int mapId in maps)
		{
			BotNavMesh? mesh = set.Get(mapId);
			if (mesh == null) { Console.WriteLine($"{mapId}: no navmesh"); continue; }
			var router = new BotNavMeshRouter(set, world.Geometry(BotNavSites.RaceFor(world, mapId)));
			var watch = System.Diagnostics.Stopwatch.StartNew();
			BotTravelGraph graph = BotTravelGraphBuilder.Build(world, mapId, mesh, router, Console.WriteLine);
			string path = Path.Combine(navDir, mapId + ".graph.json");
			graph.Save(path);
			Console.WriteLine($"{mapId}: {graph.Nodes.Count} nodes, {graph.Edges.Count} links, {graph.Exits.Count} exits " +
				$"in {watch.Elapsed.TotalSeconds:F0} s -> {path}");
		}
		return 0;
	}
}
