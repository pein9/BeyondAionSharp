using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

/// <summary>The Ishalgen legs the grid search measured in docs/bot-navigation-options.md (up to 303 s, several
/// with no route) must all route through the checked-in navmesh in well under two seconds, every step checked.</summary>
[Collection("GoldenDataManager")]
public sealed class IshalgenNavMeshLegTests
{
	[SkippableFact]
	public async Task QuestNpcLegsRouteQuicklyWithEveryStepChecked()
	{
		Skip.IfNot(Environment.GetEnvironmentVariable("AION_SOAK_NAV_INTEGRATION") == "1", "Set AION_SOAK_NAV_INTEGRATION=1 for the full offline geometry load.");
		string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
		var assets = await BotNavigationAssets.LoadAsync(root, Path.Combine(root, "run", "soak-navigation-test-cache"), CancellationToken.None);
		BotNavigationGeometry geometry = assets.StarterRoute(Race.ASMODIANS, 1).Geometry
			.WithNavMesh(new BotNavMeshSet(BotNavMeshSet.DefaultDirectory(root)));
		string xml = File.ReadAllText(Path.Combine(root, "game-server/data/static_data/spawns/Npcs/220010000_Ishalgen.xml"));
		BotPosition Spot(int npc)
		{
			Match m = Regex.Match(xml, @"<spawn npc_id=""" + npc + @"""[^>]*>\s*<spot x=""([\d.\-]+)"" y=""([\d.\-]+)"" z=""([\d.\-]+)""");
			Assert.True(m.Success, $"spawn {npc}");
			float F(int g) => float.Parse(m.Groups[g].Value, CultureInfo.InvariantCulture);
			return new BotPosition(F(1), F(2), F(3), 0);
		}
		BotPosition ulgorn = Spot(203516), nobekk = Spot(203519), dabi = Spot(203534), derot = Spot(203539),
			mijou = Spot(203540), nalto = Spot(203552), munin = Spot(203550);
		var mauFarm = new BotPosition(819.976f, 1548.345f, 278.999f, 0);
		foreach (var (from, to) in new[] { (ulgorn, nobekk), (mijou, mauFarm), (ulgorn, dabi), (dabi, derot), (derot, nalto),
			(mijou, munin), (ulgorn, mijou), (mijou, ulgorn) })
		{
			var watch = Stopwatch.StartNew();
			IReadOnlyList<BotPosition> route = geometry.FindInteractionPath(220010000, from, to);
			watch.Stop();
			Assert.True(route.Count > 0, $"{from} -> {to}: {BotNavMeshRouter.LastOutcome}");
			Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"{from} -> {to} took {watch.Elapsed}");
			float dx = route[^1].X - to.X, dy = route[^1].Y - to.Y, dz = route[^1].Z - to.Z;
			Assert.True(MathF.Sqrt(dx * dx + dy * dy + dz * dz) <= 3, $"{from} -> {to} ends at {route[^1]}");
			BotPosition previous = from;
			float length = 0;
			foreach (BotPosition point in route)
			{
				Assert.NotNull(geometry.TraceEdge(220010000, previous, point));
				length += MathF.Sqrt((point.X - previous.X) * (point.X - previous.X) + (point.Y - previous.Y) * (point.Y - previous.Y));
				previous = point;
			}
			Assert.True(length / route.Count >= 1.7f, $"{from} -> {to}: mean spacing {length / route.Count:F2} m");
		}
	}
}
