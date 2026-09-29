using Aion.Bots.Scenarios;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

/// <summary>AF-04: the pure flight policy against the shipped Altgard FLY zones and the Java flight rules.</summary>
public sealed class NaturalFlightPolicyTests
{
	private static readonly Lazy<IReadOnlyList<NaturalFlyZone>> Zones = new(() => NaturalFlyZone.Load(Path.Combine(
		Aion.GameServer.TestKit.RealStaticData.RepoRoot(), "game-server/data/static_data/zones/zones_220030000.xml")));

	private static readonly NaturalAltgardContract Contract = NaturalAltgardContract.LoadDefault();
	private static readonly BotPosition BesideObelisk = new(1655.44f, 1815.3f, 255.1f, 0);
	private static readonly BotPosition BorenderRock = new(1618.5f, 1806.66f, 406f, 0);

	private static NaturalTakeoffObservation Ready(BotPosition? at = null) =>
		new(Daeva: true, at ?? BesideObelisk, OnWaterArea: false, WaterLevel: Contract.Flight.WaterLevel, NowMillis: 100_000,
			LastTakeoffMillis: null, NoFlyEffect: false, Transformed: false, PrivateStore: false);

	[Fact]
	public void AltgardHasExactlyTheContractsTwoFlyZonesAndNoNoFlyZone()
	{
		Assert.Equal(Contract.Flight.Zones.Select(zone => zone.Name).Order(), Zones.Value.Select(zone => zone.Name).Order());
		Assert.All(Zones.Value, zone => Assert.False(zone.Forbids));
		NaturalFlyZone fortress = Zones.Value.Single(zone => zone.Name == Contract.Flight.Zones[0].Name);
		Assert.True(fortress.Contains(BesideObelisk.X, BesideObelisk.Y, BesideObelisk.Z));
		Assert.True(fortress.Contains(BorenderRock.X, BorenderRock.Y, BorenderRock.Z));
		Assert.False(fortress.Contains(BesideObelisk.X, BesideObelisk.Y, 441), "above the ceiling");
		Assert.False(fortress.Contains(1703.96f, 1792.58f, 205.09f), "the Fortress Dungeon lies below the floor");
		Assert.False(fortress.Contains(1625, 1451, 260), "Moslan Crossroad lies outside the polygon");
	}

	[Fact]
	public void TakeoffNeedsADaevaInsideAFlyZoneOffWaterWithNoBlockerAndTheReuseSpent()
	{
		Assert.True(NaturalFlightPolicy.CanTakeOff(Ready(), Zones.Value).Allowed);
		Assert.False(NaturalFlightPolicy.CanTakeOff(Ready() with { Daeva = false }, Zones.Value).Allowed);
		Assert.False(NaturalFlightPolicy.CanTakeOff(Ready(new BotPosition(1625, 1451, 260, 0)), Zones.Value).Allowed);
		Assert.False(NaturalFlightPolicy.CanTakeOff(Ready() with { NoFlyEffect = true }, Zones.Value).Allowed);
		Assert.False(NaturalFlightPolicy.CanTakeOff(Ready() with { Transformed = true }, Zones.Value).Allowed);
		Assert.False(NaturalFlightPolicy.CanTakeOff(Ready() with { PrivateStore = true }, Zones.Value).Allowed);

		// Water is the client's rule: a navmesh water area, or standing below the map's water level, refuses takeoff.
		NaturalFlightDecision wet = NaturalFlightPolicy.CanTakeOff(Ready() with { OnWaterArea = true }, Zones.Value);
		Assert.False(wet.Allowed);
		Assert.Contains("water", wet.Reason, StringComparison.Ordinal);
		var lakeBed = new BotPosition(1430, 1740, Contract.Flight.WaterLevel + 41, 0);
		Assert.True(NaturalFlightPolicy.CanTakeOff(Ready(lakeBed), Zones.Value).Allowed);
		var fortressZone = new NaturalFlyZone("low", false, [(0, 0), (0, 100), (100, 100), (100, 0)], 0, 440);
		Assert.False(NaturalFlightPolicy.CanTakeOff(Ready(new BotPosition(50, 50, 199, 0)), [fortressZone]).Allowed);

		// Java: the next takeoff is allowed FLY_REUSE_TIME - 100 ms after the last.
		Assert.Equal(NaturalFlightPolicy.TakeoffReuseMillis, Contract.Flight.ReuseMillis - 100);
		Assert.False(NaturalFlightPolicy.CanTakeOff(Ready() with { LastTakeoffMillis = 100_000 - 9_899 }, Zones.Value).Allowed);
		Assert.True(NaturalFlightPolicy.CanTakeOff(Ready() with { LastTakeoffMillis = 100_000 - 9_900 }, Zones.Value).Allowed);
	}

	[Fact]
	public void AFlightMustStayInsideTheZoneAndLandWithTheReserve()
	{
		const float speed = 9;
		float up = NaturalFlightPolicy.Distance(BesideObelisk, BorenderRock);
		var toRock = new NaturalFlightPlan([BesideObelisk with { Z = 300 }, BorenderRock], [up], speed, 0);
		Assert.Equal((int)MathF.Ceiling(up / speed), NaturalFlightPolicy.FlightCost(toRock));
		Assert.True(NaturalFlightPolicy.CanFly(toRock, Contract.Flight.MaxFlightTime, Zones.Value).Allowed);

		// The FP reserve: with only the cost plus 9 FP the flight is refused; with the cost plus 10 it is taken.
		int cost = NaturalFlightPolicy.FlightCost(toRock);
		Assert.False(NaturalFlightPolicy.CanFly(toRock, cost + NaturalFlightPolicy.LandingReserveFp - 1, Zones.Value).Allowed);
		Assert.True(NaturalFlightPolicy.CanFly(toRock, cost + NaturalFlightPolicy.LandingReserveFp, Zones.Value).Allowed);

		// An air fight's time counts; so does every waypoint staying in the zone.
		Assert.Equal(cost + 20, NaturalFlightPolicy.FlightCost(toRock with { AirborneWorkSeconds = 20 }));
		var outside = toRock with { Waypoints = [BesideObelisk with { Z = 300 }, new BotPosition(1625, 1451, 300, 0)] };
		Assert.Contains("leaves the FLY zone", NaturalFlightPolicy.CanFly(outside, 60, Zones.Value).Reason, StringComparison.Ordinal);
		var tooHigh = toRock with { Waypoints = [BorenderRock with { Z = 445 }] };
		Assert.False(NaturalFlightPolicy.CanFly(tooHigh, 60, Zones.Value).Allowed);
	}

	[Fact]
	public void AirborneTheBotLandsBeforeTheReserveAndChoosesTheNearestReachableLanding()
	{
		Assert.False(NaturalFlightPolicy.MustLand(40, 90, 9).Allowed);
		Assert.True(NaturalFlightPolicy.MustLand(19, 90, 9).Allowed);
		Assert.False(NaturalFlightPolicy.MustLand(20, 90, 9).Allowed);

		NaturalLandingTarget ground = new("ground", BesideObelisk);
		NaturalLandingTarget rock = new("platform", BorenderRock);
		var overRock = BorenderRock with { Z = 415 };
		Assert.Equal(rock, NaturalFlightPolicy.ChooseLanding(overRock, [ground, rock], 30, 9));
		// The rock is out of reach with 0 FP; nothing is.
		Assert.Null(NaturalFlightPolicy.ChooseLanding(overRock with { X = overRock.X + 40 }, [ground, rock], 0, 9));
	}

	[Fact]
	public void FlightTimeComesBackThreeEverySixSecondsAfterAThreeSecondDelay()
	{
		Assert.Equal(0, NaturalFlightPolicy.RestoreMillis(60, 60, 60));
		Assert.Equal(3_000, NaturalFlightPolicy.RestoreMillis(57, 60, 60));
		Assert.Equal(9_000, NaturalFlightPolicy.RestoreMillis(55, 60, 60));
		// From empty to full: 20 ticks, the first at 3 s: 117 s on the ground.
		Assert.Equal(117_000, NaturalFlightPolicy.RestoreMillis(0, 60, 60));
		Assert.Equal(117_000, NaturalFlightPolicy.RestoreMillis(0, 90, 60));
		Assert.Equal(60, Contract.Flight.MaxFlightTime);
	}
}
