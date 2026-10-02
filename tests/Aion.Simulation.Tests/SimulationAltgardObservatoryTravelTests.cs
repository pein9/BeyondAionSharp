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
	/// <summary>AO-01: level-21 route plans walked by a level-30 probe, then the three ordinary fortress/Observatory flights.</summary>
	[SkippableFact]
	public async Task ObservatoryLegTravelWalksAllGroundsAndTakesTheHubFlights()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int map = 220030000;
		using var policy = NewPolicy("AO01", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l8");
		await using var session = new SimulationL0Session(fixture, policy, "b01", 207, "Asimobsroute", Race.ASMODIANS);
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
		BotPosition Nearest(int npc) => Ground(Spots(npc).MinBy(at => NaturalGuardedTalkPolicy.Distance(at, session.CurrentPosition)));
		BotPosition fortress = Ground(Spots(700065).Single());
		session.Api.World.BeginWorldReload();
		await TeleportForSetupAsync(session, player, map, fortress.X, fortress.Y, fortress.Z, token);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
		int sequence = 0;
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
			await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing).CreateGroundPlan(route, from,
				session.Api.World.MovementSpeed!.Value), token);
			await session.SynchronizeAsync(token);
			float miss = NaturalGuardedTalkPolicy.Distance(new BotPosition(player.GetX(), player.GetY(), player.GetZ(), 0), to);
			Assert.True(miss <= 5, $"{name}: server ended {miss:F1} m from {to}");
			Assert.False(player.IsDead());
			Console.WriteLine($"AO-01 {name}: {plan?.Length:F0} m, {route.Count} points; miss {miss:F1}; aggressive within 30 m [{string.Join(", ", aggressive)}]");
		}
		async Task FlyAsync(int stone)
		{
			NaturalAirlineJourney journey = Assert.IsType<NaturalAirlineJourney>(NaturalAirlineRoutes.Journey(
				NaturalAirlineRoutes.Load(RealStaticData.RepoRoot()), map, session.CurrentPosition, Ground(Spots(stone).Single())));
			NaturalAirlineRoute flight = Assert.Single(journey.Flights);
			Assert.Equal(stone == 700822 ? 203561 : 205259, flight.NpcId);
			await WalkAsync($"pad-{flight.NpcId}", Ground(flight.Departure));
			int transporter = await session.WaitForNpcAsync(flight.NpcId, token);
			NaturalServiceOutcome result = await new NaturalServiceSteps(session).FlyAsync(transporter,
				session.Api.World.Objects[transporter].Position, 6, flight, token);
			Assert.True(result.IsDone, result.Reason);
			Console.WriteLine($"AO-01 {result.Reason}");
		}
		await WalkAsync("departure-valurion", Nearest(203558));
		await FlyAsync(700822);
		foreach ((int npc, string name) in new[] { (700822, "obelisk"), (203655, "neifenmer"), (203654, "aurtri"),
			(203656, "urnir"), (203657, "sarad"), (210525, "cargo-boxes"), (210526, "peckus-l17"),
			(210527, "peckus-l18"), (210528, "bodyguards"), (210530, "shamans"), (210532, "gattban") })
			await WalkAsync(name, Nearest(npc));
		BotPosition[] orders = Spots(700010);
		Assert.Equal(5, orders.Length);
		for (int spot = 0; spot < orders.Length; spot++) await WalkAsync($"operation-order-{spot + 1}", Ground(orders[spot]));
		await WalkAsync("camp-to-observatory", Nearest(700822));
		await FlyAsync(700065);
		await WalkAsync("letter-suthran", Nearest(203557));
		await FlyAsync(700822);
		await WalkAsync("endpoint-observatory", Nearest(700822));
		Console.WriteLine($"AO-01 {sequence} ground legs, three hub flights; server endpoint ({player.GetX()}, {player.GetY()}, {player.GetZ()})");
		policy.AssertClean();
	}
}
