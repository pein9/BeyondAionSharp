using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

/// <summary>
/// AF-02: NA-23 reported the way out of Altgard Fortress as GeometryRejected "probably at the gate". It is not the gate:
/// a route that starts on the obelisk's own spot starts inside its collision (NA-04 finding b), while the same route
/// from the ground beside it leaves the fortress. The SIM probe <c>AltgardFortressExitWalksToTheIceLakeTargetsAndBack</c>
/// walks every Leg 1 target on the live server.
/// </summary>
[Collection("GoldenDataManager")]
public sealed class AltgardFortressExitTests
{
	private const int Altgard = 220030000;

	[SkippableFact]
	public async Task RoutesLeaveTheFortressFromBesideTheObeliskButNotFromInsideIt()
	{
		Skip.IfNot(Environment.GetEnvironmentVariable("AION_SOAK_NAV_INTEGRATION") == "1", "Set AION_SOAK_NAV_INTEGRATION=1 for the full offline geometry load.");
		string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
		var assets = await BotNavigationAssets.LoadAsync(root, Path.Combine(root, "run", "soak-navigation-test-cache"), CancellationToken.None);
		BotNavigationGeometry geometry = assets.NaturalJourneyGeometry(Race.ASMODIANS, 1)
			.WithNavMesh(new BotNavMeshSet(BotNavMeshSet.DefaultDirectory(root)));
		NaturalAltgardContract contract = NaturalAltgardContract.LoadDefault();
		var inside = new BotPosition(contract.Hub.Anchor[0], contract.Hub.Anchor[1], contract.Hub.Anchor[2], 0);
		BotPosition beside = geometry.SnapToGround(Altgard, inside with { X = inside.X - 3, Z = inside.Z + 1 })
			?? throw new InvalidDataException("No ground beside the obelisk.");
		// NA-23's stage (open ground south-west of the fortress) and a crasaur spawn on the western Ice Lake.
		foreach (var open in new[] { new BotPosition(1622.13f, 1947.95f, 259f, 0), new BotPosition(1459.08f, 1882.34f, 248f, 0) })
		{
			BotPosition target = geometry.SnapToGround(Altgard, open) ?? throw new InvalidDataException($"No ground at {open}.");
			Assert.Empty(geometry.FindJourneyPath(Altgard, inside, target));
			Assert.NotEqual(BotNavRouteOutcome.Routed, BotNavMeshRouter.LastOutcome);
			Assert.NotEmpty(geometry.FindJourneyPath(Altgard, beside, target));
			Assert.NotEmpty(geometry.FindJourneyPath(Altgard, target, beside));
		}
	}
}
