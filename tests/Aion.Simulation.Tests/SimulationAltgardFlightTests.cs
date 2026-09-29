using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// AF-05 (docs/natural-altgard-leveling.md): free flight in Altgard. A level 10 Cleric (made a Daeva by GM setup, as in
	/// NA-23) takes off from the ground beside the obelisk, flies up to Borender's floating rock, lands on it and talks to
	/// him, then flies back down and lands beside the obelisk. Every flight is checked by <see cref="NaturalFlightPolicy"/>
	/// first and must land with flight time to spare.
	/// </summary>
	[SkippableFact]
	public async Task AltgardFlightToBorendersRockAndBack()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000;
		using var policy = NewPolicy("AF05", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract contract = NaturalAltgardContract.LoadDefault();
		IReadOnlyList<NaturalFlyZone> zones = NaturalFlyZone.Load(Path.Combine(Aion.GameServer.TestKit.RealStaticData.RepoRoot(),
			"game-server/data/static_data/zones/zones_220030000.xml"));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 136, "Asimflyer", Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(player, PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		player.GetCommonData().SetLevel(10);
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(
			fixture.World.GetWorldMap(altgard).GetMainWorldMapInstance().GetInstanceId(), Race.ASMODIANS);
		BotPosition ground = geometry.SnapToGround(altgard,
			new BotPosition(contract.Hub.Anchor[0] - 3, contract.Hub.Anchor[1], contract.Hub.Anchor[2] + 1, 0))
			?? throw new InvalidDataException("No ground beside the Altgard obelisk.");
		await TeleportForSetupAsync(session, player, altgard, ground.X, ground.Y, ground.Z, token);
		await session.SynchronizeAsync(token);
		Assert.True(player.GetCommonData().IsDaeva());
		NaturalAltgardStep borender = contract.Steps.First(step => step.Area == "borender-rock");
		// The rock top beside Borender, found on the server's geometry from just above it.
		BotPosition rock = geometry.SnapToGround(altgard, new BotPosition(borender.Position[0] - 2.5f, borender.Position[1], borender.Position[2] + 3, 0))
			?? throw new InvalidDataException("No rock top beside Borender.");
		Assert.True(contract.Area("borender-rock").Contains(rock.X, rock.Y, rock.Z), $"rock top {rock}");
		int maxFp = session.Api.World.MaxFlightTime;
		Assert.Equal(contract.Flight.MaxFlightTime, maxFp);
		long? lastTakeoff = null;

		int leg = 0;
		foreach ((string name, BotPosition from, BotPosition to) in new[] { ("up-to-borender", ground, rock), ("down-to-the-obelisk", rock, ground) })
		{
			session.BeginStep($"s{++leg:00}", $"fly-{name}");
			// Takeoff reuse and flight time on the ground: wait them out as a player would.
			long now = fixture.Clock.NowMillis;
			var takeoff = new NaturalTakeoffObservation(true, from, OnWaterArea: false, contract.Flight.WaterLevel, now, lastTakeoff,
				NoFlyEffect: false, Transformed: false, PrivateStore: false);
			if (lastTakeoff is { } last && now < last + NaturalFlightPolicy.TakeoffReuseMillis)
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(last + NaturalFlightPolicy.TakeoffReuseMillis - now), token);
			takeoff = takeoff with { NowMillis = fixture.Clock.NowMillis };
			NaturalFlightDecision ready = NaturalFlightPolicy.CanTakeOff(takeoff, zones);
			Assert.True(ready.Allowed, ready.Reason);
			NaturalFlightRoute route = NaturalFlightProtocol.Plan(geometry, altgard, from, to, cruiseZ: rock.Z + 8);
			Assert.True(route.IsUsable, route.Refusal);

			int fpBefore = session.Api.World.CurrentFlightTime;
			lastTakeoff = fixture.Clock.NowMillis;
			float speed = await NaturalFlightProtocol.TakeOffAsync(session, token);
			NaturalFlightDecision go = NaturalFlightPolicy.CanFly(NaturalFlightProtocol.ToPlan(route, from, speed), fpBefore, zones);
			Assert.True(go.Allowed, go.Reason);
			Assert.True(player.IsFlying());
			await NaturalFlightProtocol.FlyAsync(session, altgard, from, route.Waypoints, speed, token);
			await NaturalFlightProtocol.LandAsync(session, token);
			await session.SynchronizeAsync(token);
			int fpLanded = player.GetLifeStats().GetCurrentFp();
			Console.WriteLine($"AF-05 {name}: speed {speed} m/s, {route.Meters:F0} m, FP {fpBefore} -> {fpLanded} (client sees {session.Api.World.CurrentFlightTime}), {go.Reason}; lands at ({player.GetX():F1}, {player.GetY():F1}, {player.GetZ():F1})");
			Assert.False(player.IsFlying());
			Assert.False(player.IsDead());
			Assert.True(fpLanded >= NaturalFlightPolicy.LandingReserveFp, $"landed with {fpLanded} FP");
			Assert.True(fpLanded < fpBefore, "flight time was not spent");
			Assert.True(NaturalFlightPolicy.Distance(new BotPosition(player.GetX(), player.GetY(), player.GetZ(), 0), to) <= 1, $"{name} lands off target");
			session.TraceDiagnostic("af05-flight", new Dictionary<string, object?>
			{
				["leg"] = name, ["speed"] = speed, ["meters"] = route.Meters, ["fpBefore"] = fpBefore, ["fpLanded"] = fpLanded,
				["x"] = player.GetX(), ["y"] = player.GetY(), ["z"] = player.GetZ(),
			});

			if (name == "up-to-borender")
			{
				session.BeginStep($"s{++leg:00}", "talk-to-borender-on-the-rock");
				int borenderObject = await session.WaitForNpcAsync(borender.NpcId, token);
				await NaturalDialogProtocol.OpenAsync(session, borenderObject, token);
				DecodedBotServerPacket opened = await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
					packet => packet.Get<int>("targetObjectId") == borenderObject);
				Console.WriteLine($"AF-05 Borender answers with page {opened.Get<ushort>("dialogPageId")}");
				await session.SendPacketAsync(session.Api.CloseDialog(borenderObject), token);
				// Flight time comes back on the rock: 3 FP every 6 s after a 3 s delay.
				long wait = NaturalFlightPolicy.RestoreMillis(fpLanded, fpLanded + 3, maxFp);
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(wait + 100), token);
				await session.SynchronizeAsync(token);
				Assert.True(player.GetLifeStats().GetCurrentFp() >= fpLanded + 3, $"FP did not restore: {player.GetLifeStats().GetCurrentFp()}");
			}
		}
		policy.AssertClean();
	}
}
