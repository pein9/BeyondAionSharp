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
	/// <summary>AE-02: level-21 route plans walked by a level-30 probe, then the two ordinary hub flights.</summary>
	[SkippableFact]
	public async Task AltgardLeg7TravelWalksTheGateSwampAndBothSpiritSpotsAndFliesToTheBerth()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int map = 220030000;
		using var policy = NewPolicy("AE02", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l7");
		await using var session = new SimulationL0Session(fixture, policy, "b01", 199, "Asimfortroute", Race.ASMODIANS);
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
			Console.WriteLine($"AE-02 {name}: {plan?.Length:F0} m, {route.Count} points; miss {miss:F1}; aggressive within 30 m [{string.Join(", ", aggressive)}]");
		}
		foreach ((int npc, string name) in new[] { (203557, "suthran"), (203559, "meiyer"), (203590, "emgata"),
			(203560, "morn"), (203558, "valurion"), (203579, "donabe"), (798031, "chagarinerk"), (700065, "fortress") })
			await WalkAsync(name, Nearest(npc));
		foreach ((int npc, string name) in new[] { (798036, "mabrunerk"), (203664, "eggther"), (203665, "dellalont"),
			(210754, "arachnas-l16"), (210447, "arachnas-l17"), (210444, "malodors-l17"), (210500, "malodors-other"),
			(210459, "warriors-l16"), (210460, "warriors-l17"), (210502, "sleekpaws"), (203669, "taora") })
			await WalkAsync(name, Nearest(npc));
		BotPosition[] spirit = Spots(203682);
		Assert.Equal(2, spirit.Length);
		for (int spot = 0; spot < spirit.Length; spot++) await WalkAsync($"spirit-{spot + 1}", Ground(spirit[spot]));
		await WalkAsync("swamp-to-fortress", fortress);

		IReadOnlyList<NaturalAirlineRoute> airlines = NaturalAirlineRoutes.Load(RealStaticData.RepoRoot());
		foreach (int npc in new[] { 203561, 203678 })
		{
			BotPosition destination = npc == 203561 ? Ground(Spots(700821).Single()) : fortress;
			NaturalAirlineJourney journey = Assert.IsType<NaturalAirlineJourney>(NaturalAirlineRoutes.Journey(airlines, map, session.CurrentPosition, destination));
			NaturalAirlineRoute flight = Assert.Single(journey.Flights);
			Assert.Equal(npc, flight.NpcId);
			await WalkAsync($"pad-{npc}", Ground(flight.Departure));
			int transporter = await session.WaitForNpcAsync(npc, token);
			NaturalServiceOutcome result = await new NaturalServiceSteps(session).FlyAsync(transporter,
				session.Api.World.Objects[transporter].Position, 6, flight, token);
			Assert.True(result.IsDone, result.Reason);
			Console.WriteLine($"AE-02 {result.Reason}");
			if (npc == 203561)
			{
				await WalkAsync("berth-kagorinerk", Nearest(798035));
				await WalkAsync("berth-pad", Nearest(203678));
			}
		}
		await WalkAsync("landing-to-fortress", fortress);
		Console.WriteLine($"AE-02 {sequence} ground legs, two hub flights; server endpoint ({player.GetX()}, {player.GetY()}, {player.GetZ()})");
		policy.AssertClean();
	}
}
