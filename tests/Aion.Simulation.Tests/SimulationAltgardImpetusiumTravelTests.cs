using System.Xml.Linq;
using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Services;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>AH-01: level-21 route plans, upper/lower pillar free flight and four ordinary hub flights on free account 210.</summary>
	[SkippableFact]
	public async Task ImpetusiumLegTravelWalksTheGroundsFliesThePillarAndTakesHubFlights()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int map = 220030000;
		using var policy = NewPolicy("AH01", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l9");
		await using var session = new SimulationL0Session(fixture, policy, "b01", 210, "Asimheartroute", Race.ASMODIANS);
		session.BeginStep("s00", "setup-route-probe");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		var player = fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(player, PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		player.GetCommonData().SetLevel(30);
		player.GetInventory().IncreaseKinah(5000);
		var instance = fixture.World.GetWorldMap(map).GetMainWorldMapInstance();
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(instance.GetInstanceId(), Race.ASMODIANS);
		BotTravelPlanner planner = BotTravelPlanner.For(map, geometry, fixture.DataManager.StaticData)!;
		XElement spawns = XDocument.Load(Path.Combine(RealStaticData.RepoRoot(), "game-server/data/static_data/spawns/Npcs/220030000_Altgard.xml")).Root!;
		BotPosition[] Spots(int npc) => spawns.Descendants("spawn").Where(node => (int?)node.Attribute("npc_id") == npc).Elements("spot")
			.Select(node => new BotPosition((float)node.Attribute("x")!, (float)node.Attribute("y")!, (float)node.Attribute("z")!, 0)).ToArray();
		BotPosition Ground(BotPosition at) => geometry.GroundAround(map, at, [3f, 5f, 8f, 12f]).First(point => point != default);
		BotPosition Nearest(int npc) => npc < 210000 || npc >= 700000
			? Ground(Spots(npc).MinBy(at => NaturalGuardedTalkPolicy.Distance(at, session.CurrentPosition)))
			: Spots(npc).SelectMany(at => geometry.GroundAround(map, at, [3f, 5f, 8f, 12f])
				.Where(point => NaturalFlightPolicy.Distance(point, at) <= 6))
				.Where(at => geometry.OnSameIsland(map, session.CurrentPosition, at))
				.MinBy(at => NaturalGuardedTalkPolicy.Distance(at, session.CurrentPosition));
		BotPosition origin = Ground(Spots(700822).Single());
		session.Api.World.BeginWorldReload();
		await TeleportForSetupAsync(session, player, map, origin.X, origin.Y, origin.Z, token);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
		int sequence = 0, hubFlights = 0, freeFlights = 0;
		long? lastTakeoff = null;
		IReadOnlyList<NaturalFlyZone> zones = NaturalFlyZone.Load(Path.Combine(RealStaticData.RepoRoot(), "game-server/data/static_data/zones/zones_220030000.xml"));
		void Clear(BotPosition[] route)
		{
			foreach (var npc in instance.GetNpcs().Where(npc => !npc.IsDead() &&
				NaturalHostility.IsAggressive(npc.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
				route.Any(at => MathF.Pow(npc.GetX() - at.X, 2) + MathF.Pow(npc.GetY() - at.Y, 2) <= 900)).ToArray())
				fixture.World.Despawn(npc);
		}
		async Task PillarFlightAsync(string name, BotPosition to)
		{
			session.BeginStep($"s{++sequence:00}", name);
			BotPosition from = session.CurrentPosition;
			Clear([from, to]);
			long wait = NaturalFlightPolicy.RestoreMillis(session.Api.World.CurrentFlightTime, session.Api.World.MaxFlightTime, session.Api.World.MaxFlightTime);
			if (lastTakeoff is long last) wait = Math.Max(wait, last + NaturalFlightPolicy.TakeoffReuseMillis - fixture.Clock.NowMillis);
			if (wait > 0) await session.AdvanceAsync(TimeSpan.FromMilliseconds(wait + 100), token);
			await session.SynchronizeAsync(token);
			NaturalFlightDecision ready = NaturalFlightPolicy.CanTakeOff(new NaturalTakeoffObservation(true, from, false, 200,
				fixture.Clock.NowMillis, lastTakeoff, false, false, false), zones);
			Assert.True(ready.Allowed, ready.Reason);
			NaturalFlightRoute route = new([], 0, "No checked pillar route.");
			float cruise = 0;
			foreach (float height in new[] { MathF.Max(from.Z, to.Z) + 12, 360f, 400f, 430f })
			{
				cruise = height;
				route = NaturalFlightProtocol.Plan(geometry, map, from, to, height);
				if (route.IsUsable) break;
				Console.WriteLine($"AH-01 {name} at cruise {height}: {route.Refusal}");
			}
			Assert.True(route.IsUsable, route.Refusal);
			lastTakeoff = fixture.Clock.NowMillis;
			int fp = session.Api.World.CurrentFlightTime;
			float speed = await NaturalFlightProtocol.TakeOffAsync(session, token);
			NaturalFlightDecision go = NaturalFlightPolicy.CanFly(NaturalFlightProtocol.ToPlan(route, from, speed), fp, zones);
			Assert.True(go.Allowed, go.Reason);
			await NaturalFlightProtocol.FlyAsync(session, map, from, route.Waypoints, speed, token);
			await NaturalFlightProtocol.LandAsync(session, token);
			await session.SynchronizeAsync(token);
			Assert.False(player.IsFlying());
			Assert.False(player.IsDead());
			Assert.True(player.GetLifeStats().GetCurrentFp() >= NaturalFlightPolicy.LandingReserveFp);
			Assert.True(NaturalFlightPolicy.Distance(new BotPosition(player.GetX(), player.GetY(), player.GetZ(), 0), to) <= 1);
			freeFlights++;
			Console.WriteLine($"AH-01 {name}: cruise {cruise}, {route.Meters:F1} m, FP {fp} -> {player.GetLifeStats().GetCurrentFp()}, {go.Reason}; endpoint {to}");
		}
		async Task WalkAsync(string name, BotPosition to)
		{
			session.BeginStep($"s{++sequence:00}", name);
			BotPosition from = session.CurrentPosition;
			BotTravelPlan? plan = planner.PlanJourney(map, from, to, 21, []);
			IReadOnlyList<BotPosition> route = plan?.Route ?? geometry.FindJourneyPath(map, from, to);
			Assert.True(route.Count > 0, $"{name}: no route ({BotNavMeshRouter.LastOutcome})");
			var aggressive = instance.GetNpcs().Where(npc => !npc.IsDead() &&
				NaturalHostility.IsAggressive(npc.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
				route.Any(at => MathF.Pow(npc.GetX() - at.X, 2) + MathF.Pow(npc.GetY() - at.Y, 2) <= 900))
				.GroupBy(npc => npc.GetNpcId()).Select(group => $"{group.Key}x{group.Count()}").ToArray();
			Clear(route.ToArray());
			await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing).CreateGroundPlan(route, from,
				session.Api.World.MovementSpeed!.Value), token);
			await session.SynchronizeAsync(token);
			float miss = NaturalGuardedTalkPolicy.Distance(new BotPosition(player.GetX(), player.GetY(), player.GetZ(), 0), to);
			Assert.True(miss <= 5, $"{name}: server ended {miss:F1} m from {to}");
			Assert.False(player.IsDead());
			Console.WriteLine($"AH-01 {name}: {plan?.Length:F0} m, {route.Count} points; miss {miss:F1}; aggressive within 30 m [{string.Join(", ", aggressive)}]");
		}
		async Task HubFlightsAsync(int stone)
		{
			BotPosition destination = Ground(Spots(stone).Single());
			for (int flights = 0; flights < 3; flights++)
			{
				NaturalAirlineJourney? journey = NaturalAirlineRoutes.Journey(NaturalAirlineRoutes.Load(RealStaticData.RepoRoot()), map, session.CurrentPosition, destination);
				if (journey == null) return;
				NaturalAirlineRoute flight = journey.Flights.First();
				await WalkAsync($"pad-{flight.NpcId}", Ground(flight.Departure));
				int transporter = await session.WaitForNpcAsync(flight.NpcId, token);
				NaturalServiceOutcome result = await new NaturalServiceSteps(session).FlyAsync(transporter,
					session.Api.World.Objects[transporter].Position, 6, flight, token);
				Assert.True(result.IsDone, result.Reason);
				hubFlights++;
				Console.WriteLine($"AH-01 {flight.NpcId}: {result.Reason}");
			}
			throw new InvalidDataException("Too many hub flights.");
		}
		await HubFlightsAsync(700067);
		foreach ((int npc, string name) in new[] { (700067, "obelisk"), (203651, "andgar"), (203650, "grak"), (203649, "gulkalla"), (203659, "lateni") })
			await WalkAsync(name, Nearest(npc));
		// Banatisai is underneath the upper structure: land in the debris ground and approach him on foot.
		await PillarFlightAsync("upper-to-debris-ground", Ground(Spots(700144)[0]));
		await WalkAsync("banatisai", Nearest(203673));
		BotPosition[] debris = Spots(700144);
		Assert.Equal(4, debris.Length);
		for (int spot = 0; spot < debris.Length; spot++) await WalkAsync($"debris-{spot + 1}", Ground(debris[spot]));
		foreach ((int npc, string name) in new[] { (210588, "hero-l18"), (210722, "hero-l19"), (210723, "sorcerer-l18"),
			(210724, "sorcerer-l19"), (210575, "mist"), (210577, "splash-l18"), (210522, "splash-l19"), (210547, "wild-tayga") })
			await WalkAsync(name, Nearest(npc));
		await WalkAsync("taygas-to-lower-pillar", Nearest(203673));
		await WalkAsync("lower-pillar-to-takeoff", Ground(Spots(700144)[0]));
		await PillarFlightAsync("lower-to-upper-pillar", Ground(new BotPosition(leg.Hub.Anchor[0], leg.Hub.Anchor[1], leg.Hub.Anchor[2], 0)));
		await HubFlightsAsync(700065);
		foreach ((int npc, string name) in new[] { (203557, "suthran"), (203579, "merchant"), (203581, "city-teleporter") })
			await WalkAsync(name, Nearest(npc));
		await HubFlightsAsync(700067);
		await WalkAsync("endpoint-heart", Nearest(700067));
		Assert.Equal(4, hubFlights);
		Assert.Equal(2, freeFlights);
		Console.WriteLine($"AH-01 {sequence} route legs, {hubFlights} hub flights, {freeFlights} pillar flights; server endpoint ({player.GetX()}, {player.GetY()}, {player.GetZ()})");
		policy.AssertClean();
	}
}
