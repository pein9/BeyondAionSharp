using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class SoakGatheringRouteTests
{
	[SkippableFact]
	public async Task StarterSoakHubsHaveRoundTripRoutesToMultipleShippedNodes()
	{
		Skip.IfNot(Environment.GetEnvironmentVariable("AION_SOAK_NAV_INTEGRATION") == "1", "Set AION_SOAK_NAV_INTEGRATION=1 for the full offline geometry load.");
		string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
		string cache = Path.Combine(root, "run", "soak-navigation-test-cache");
		var assets = await BotNavigationAssets.LoadAsync(root, cache, CancellationToken.None);
		var spots = SoakGatheringPool.StarterSpots();
		foreach (var (race, map, home) in new[]
		{
			(Race.ELYOS, 210010000, VendorScenario.Position),
			(Race.ELYOS, 210010000, new BotPosition(1212, 1040, 140.756f, 0)),
			(Race.ASMODIANS, 220010000, GatheringTarget.YoungAzpha.Position),
		})
		{
			var nav = assets.StarterRoute(race, 1);
			foreach (int offset in new[] { 0, 1 })
			{
				var start = home with { X = home.X + offset };
				int reachable = 0;
				foreach (var spot in spots.Where(spot => spot.MapId == map && Distance(spot.Position, home) <= 150).OrderBy(spot => Distance(spot.Position, home)))
				{
					var outbound = Path(start, spot.Position);
					if (outbound.Count == 0) continue;
					var inbound = Path(outbound[^1], home);
					if (inbound.Count == 0) continue;
					if (++reachable == 2) break;
				}
				Assert.True(reachable >= 2, $"Hub {map}:{start} has only {reachable} collision-checked round-trip nodes.");
			}
			IReadOnlyList<BotPosition> Path(BotPosition start, BotPosition end)
			{
				var path = nav.Graph.FindPath(map, start, end);
				return path.Count > 0 ? path : nav.Geometry.FindLocalPath(map, start, end);
			}
		}
		static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));
	}
}
