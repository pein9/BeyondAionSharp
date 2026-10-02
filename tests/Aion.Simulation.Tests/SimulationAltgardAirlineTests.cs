using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// AG-00: chained hub flights. From Basfelt, Leg 6's bind at Trader's Berth is two flight transporters away: Hrold's (203683)
	/// to the fortress, then the fortress transporter's (203561) to "Urtumheim", which lands by Trader's Berth's own (203678).
	/// <see cref="NaturalAirlineRoutes.Journey"/> picks each hop, planning again from each landing as the runner does; each
	/// flight is the client's (START_FLYTELEPORT, the route's CM_MOVE_IN_AIR, LAND_FLYTELEPORT), with the fare checked.
	/// GM setup on the probe only: its class and level, the fares' Kinah, and a setup teleport to Hrold's pad.
	/// </summary>
	[SkippableFact]
	public async Task BasfeltToTradersBerthTakesTwoHubFlights()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000;
		using var policy = NewPolicy("AG00", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(fixture, policy, "b01", 68, "Asimairline", Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player Server() => fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(Server(), PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		Server().GetCommonData().SetLevel(20);
		Server().GetInventory().IncreaseKinah(5000);
		IReadOnlyList<NaturalAirlineRoute> routes = NaturalAirlineRoutes.Load(Aion.GameServer.TestKit.RealStaticData.RepoRoot());
		var berthObelisk = new BotPosition(2687f, 1021f, 312f, 0);
		var steps = new NaturalServiceSteps(session);
		var flown = new List<string>();

		// Nokir's square in Basfelt: the journey is two flights.
		await TeleportForSetupAsync(session, Server(), altgard, 1779.88f, 690.477f, 264.309f, token);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
		NaturalAirlineJourney journey = Assert.IsType<NaturalAirlineJourney>(
			NaturalAirlineRoutes.Journey(routes, altgard, session.CurrentPosition, berthObelisk));
		Assert.Equal(2, journey.Flights.Count());

		for (NaturalAirlineJourney? next = journey; next != null; next = NaturalAirlineRoutes.Journey(routes, altgard, session.CurrentPosition, berthObelisk))
		{
			Assert.True(flown.Count < 2, $"a third flight was planned after {string.Join(", ", flown)}");
			NaturalAirlineRoute route = next.Flights.First();
			session.BeginStep($"s-fly-{flown.Count + 1}", $"fly-{route.Route}");
			if (NaturalGuardedTalkPolicy.Distance(session.CurrentPosition, route.Departure) > 3)
			{
				// The walk to the first pad is not what this probe tests.
				await TeleportForSetupAsync(session, Server(), altgard, route.Departure.X, route.Departure.Y, route.Departure.Z + 0.5f, token);
				session.AcceptTeleportPosition();
				await session.SynchronizeAsync(token);
			}
			int transporter = await session.WaitForNpcAsync(route.NpcId, token);
			long before = session.Api.World.Kinah;
			NaturalServiceOutcome outcome = await steps.FlyAsync(transporter, session.Api.World.Objects[transporter].Position, 6, route, token);
			Assert.True(outcome.IsDone, outcome.Reason);
			Assert.True(NaturalGuardedTalkPolicy.Distance(session.CurrentPosition, route.Landing) < 8, $"landed at {session.CurrentPosition}");
			Assert.True(NaturalGuardedTalkPolicy.Distance(new BotPosition(Server().GetX(), Server().GetY(), Server().GetZ(), 0), route.Landing) < 8,
				$"the server has the player at ({Server().GetX()}, {Server().GetY()}, {Server().GetZ()})");
			flown.Add($"{route.Route} ({before - session.Api.World.Kinah} Kinah)");
		}

		Assert.Equal(2, flown.Count);
		Assert.True(NaturalGuardedTalkPolicy.Distance(session.CurrentPosition, berthObelisk) < 15, $"ended at {session.CurrentPosition}");
		Console.WriteLine($"AG-00 {string.Join("; ", flown)}; at {session.CurrentPosition}, {NaturalGuardedTalkPolicy.Distance(session.CurrentPosition, berthObelisk):F1} m " +
			$"from the Trader's Berth obelisk");
		policy.AssertClean();
	}
}
