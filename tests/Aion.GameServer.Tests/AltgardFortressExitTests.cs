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

	// The offline load registers process-wide zone meshes, so a second load in the same process reports duplicates.
	private static readonly Lazy<Task<BotNavigationGeometry>> Geometry = new(async () =>
	{
		string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
		var assets = await BotNavigationAssets.LoadAsync(root, Path.Combine(root, "run", "soak-navigation-test-cache"), CancellationToken.None);
		return assets.NaturalJourneyGeometry(Race.ASMODIANS, 1).WithNavMesh(new BotNavMeshSet(BotNavMeshSet.DefaultDirectory(root)));
	});

	[SkippableFact]
	public async Task RoutesLeaveTheFortressFromBesideTheObeliskButNotFromInsideIt()
	{
		Skip.IfNot(Environment.GetEnvironmentVariable("AION_SOAK_NAV_INTEGRATION") == "1", "Set AION_SOAK_NAV_INTEGRATION=1 for the full offline geometry load.");
		BotNavigationGeometry geometry = await Geometry.Value;
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

	/// <summary>AF-03: Mumu Bon (Q2208) and Noroia (Q2209) stand in the Fortress Dungeon, reached on foot down the ramp
	/// south-west of the obelisk. Mumu Bon stands on his own scrap of mesh, so only interaction routing reaches him.</summary>
	[SkippableFact]
	public async Task DungeonNpcsAreReachedOnFootDownTheRamp()
	{
		Skip.IfNot(Environment.GetEnvironmentVariable("AION_SOAK_NAV_INTEGRATION") == "1", "Set AION_SOAK_NAV_INTEGRATION=1 for the full offline geometry load.");
		BotNavigationGeometry geometry = await Geometry.Value;
		NaturalAltgardContract contract = NaturalAltgardContract.LoadDefault();
		var obelisk = new BotPosition(contract.Hub.Anchor[0], contract.Hub.Anchor[1], contract.Hub.Anchor[2], 0);
		BotPosition beside = geometry.SnapToGround(Altgard, obelisk with { X = obelisk.X - 3, Z = obelisk.Z + 1 })
			?? throw new InvalidDataException("No ground beside the obelisk.");
		NaturalAltgardArea dungeon = contract.Area("fortress-dungeon");
		foreach (NaturalAltgardStep step in contract.Steps.Where(step => step.Area == dungeon.Key))
		{
			var npc = new BotPosition(step.Position[0], step.Position[1], step.Position[2], 0);
			IReadOnlyList<BotPosition> down = geometry.FindInteractionPath(Altgard, beside, npc);
			Assert.True(down.Count > 0, $"{step.Key}: {BotNavMeshRouter.LastOutcome}");
			Assert.True(MathF.Sqrt(MathF.Pow(down[^1].X - npc.X, 2) + MathF.Pow(down[^1].Y - npc.Y, 2)) < step.TalkRange, step.Key);
			Assert.True(dungeon.Contains(down[^1].X, down[^1].Y, down[^1].Z), $"{step.Key} ends at {down[^1]}");
			BotPosition previous = beside;
			foreach (BotPosition point in down)
			{
				Assert.NotNull(geometry.TraceEdge(Altgard, previous, point));
				previous = point;
			}
			Assert.NotEmpty(geometry.FindJourneyPath(Altgard, down[^1], beside));
		}
	}



	/// <summary>AF-05: the floating island over the fortress (z 335-395) blocks the straight climb from the obelisk to
	/// Borender's rock; the flight planner finds a clear column beside it, both ways, inside the FLY zone.</summary>
	[SkippableFact]
	public async Task FlightToBorenderGoesAroundTheFloatingIsland()
	{
		Skip.IfNot(Environment.GetEnvironmentVariable("AION_SOAK_NAV_INTEGRATION") == "1", "Set AION_SOAK_NAV_INTEGRATION=1 for the full offline geometry load.");
		BotNavigationGeometry geometry = await Geometry.Value;
		NaturalAltgardContract contract = NaturalAltgardContract.LoadDefault();
		IReadOnlyList<NaturalFlyZone> zones = NaturalFlyZone.Load(Path.Combine(Path.GetFullPath(Path.Combine(
			Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../..")), "game-server/data/static_data/zones/zones_220030000.xml"));
		var obelisk = new BotPosition(contract.Hub.Anchor[0], contract.Hub.Anchor[1], contract.Hub.Anchor[2], 0);
		BotPosition ground = geometry.SnapToGround(Altgard, obelisk with { X = obelisk.X - 3, Z = obelisk.Z + 1 })
			?? throw new InvalidDataException("No ground beside the obelisk.");
		NaturalAltgardStep borender = contract.Steps.First(step => step.Area == "borender-rock");
		BotPosition rock = geometry.SnapToGround(Altgard, new BotPosition(borender.Position[0] - 2.5f, borender.Position[1], borender.Position[2] + 3, 0))
			?? throw new InvalidDataException("No rock top beside Borender.");
		Assert.False(NaturalFlightProtocol.IsClear(geometry, Altgard, ground, ground with { Z = rock.Z + 8 }));
		foreach ((BotPosition start, BotPosition end) in new[] { (ground, rock), (rock, ground) })
		{
			NaturalFlightRoute route = NaturalFlightProtocol.Plan(geometry, Altgard, start, end, rock.Z + 8);
			Assert.True(route.IsUsable, route.Refusal);
			Assert.Equal(5, route.Waypoints.Count);
			Assert.Equal(end, route.Waypoints[^1]);
			NaturalFlightDecision go = NaturalFlightPolicy.CanFly(NaturalFlightProtocol.ToPlan(route, start, 9), contract.RequiredFlight.MaxFlightTime, zones);
			Assert.True(go.Allowed, go.Reason);
			BotPosition previous = start;
			foreach (BotPosition point in route.Waypoints)
			{
				Assert.True(NaturalFlightProtocol.IsClear(geometry, Altgard, previous, point));
				previous = point;
			}
		}
	}
}
