using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// AF-06 (docs/natural-altgard-leveling.md): Q24011's air kills. A level 10 Daeva Cleric with Q24011 at var 2 (as after
	/// Borender's talk; GM setup, the talk itself is AF-07's) takes off beside the obelisk and shoots Abyss Fungus down
	/// with Smite from the air until the quest reaches its reward, landing on Borender's rock to refill flight time
	/// whenever <see cref="NaturalAirCombatPolicy"/> says the next fungus is not affordable.
	/// </summary>
	[SkippableFact]
	public async Task AltgardAirCombatCompletesTheFungusKills()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, fungusQuest = 24011;
		using var policy = NewPolicy("AF06", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
		CancellationToken token = timeout.Token;
		string root = Aion.GameServer.TestKit.RealStaticData.RepoRoot();
		NaturalAltgardContract contract = NaturalAltgardContract.LoadDefault();
		IReadOnlyList<NaturalFlyZone> zones = NaturalFlyZone.Load(Path.Combine(root, "game-server/data/static_data/zones/zones_220030000.xml"));
		string directory = Path.Combine(root, "run", "af06");
		Directory.CreateDirectory(directory);
		string tracePath = Path.Combine(directory, $"af06-s{fixture.Seed}-{DateTime.UtcNow:yyyyMMddHHmmss}.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(tracePath, "af06", "b01", "sim-player-47",
			virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 47, "Asimfungus", Race.ASMODIANS, trace, tracePath);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(player, PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		player.GetCommonData().SetLevel(10);
		SkillLearnService.LearnNewSkills(player, 1, 10);
		var fungusState = new QuestState(fungusQuest, QuestStatus.START, 2, 0, 0, null, null, null);
		Assert.True(player.GetQuestStateList().AddQuest(fungusQuest, fungusState));
		Aion.GameServer.Utils.PacketSendUtility.SendPacket(player,
			new Aion.GameServer.Network.Aion.ServerPackets.SM_QUEST_ACTION(Aion.GameServer.Network.Aion.ServerPackets.SM_QUEST_ACTION.ActionType.ADD, fungusState));
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(
			fixture.World.GetWorldMap(altgard).GetMainWorldMapInstance().GetInstanceId(), Race.ASMODIANS);
		BotPosition ground = geometry.SnapToGround(altgard,
			new BotPosition(contract.Hub.Anchor[0] - 3, contract.Hub.Anchor[1], contract.Hub.Anchor[2] + 1, 0))
			?? throw new InvalidDataException("No ground beside the Altgard obelisk.");
		await TeleportForSetupAsync(session, player, altgard, ground.X, ground.Y, ground.Z, token);
		await session.SynchronizeAsync(token);
		NaturalAltgardStep borender = contract.Steps.First(step => step.Area == "borender-rock");
		BotPosition rock = geometry.SnapToGround(altgard, new BotPosition(borender.Position[0] - 2.5f, borender.Position[1], borender.Position[2] + 3, 0))
			?? throw new InvalidDataException("No rock top beside Borender.");
		var landing = new NaturalLandingTarget("platform", rock);
		var runtime = new NaturalJourneyRuntime(root, "SIM-af06", fixture.Seed, fixture.DataManager.StaticData,
			() => fixture.Clock.NowMillis, fixture.Epoch, () => geometry, _ => Task.FromResult(false), policy.AssertClean,
			() => policy.SnapshotProblems(), trace, new LiveBotDashboardState());
		float cruise = rock.Z + 8;
		int maxFp = session.Api.World.MaxFlightTime;
		bool airborne = false;
		long? lastTakeoff = null;
		float speed = 0;
		int sorties = 0, kills = 0, step = 0;
		var killSeconds = new List<double>();
		var shotDown = new HashSet<int>();
		int missed = 0;

		while (NaturalAirCombat.QuestStatus(session, fungusQuest) == 3)
		{
			Assert.False(player.IsDead(), "died in the air");
			if (!airborne)
			{
				session.BeginStep($"s{++step:00}", "refill-and-take-off");
				long wait = NaturalFlightPolicy.RestoreMillis(player.GetLifeStats().GetCurrentFp(), maxFp, maxFp);
				if (lastTakeoff is { } last) wait = Math.Max(wait, last + NaturalFlightPolicy.TakeoffReuseMillis - fixture.Clock.NowMillis);
				if (wait > 0) await session.AdvanceAsync(TimeSpan.FromMilliseconds(wait + 100), token);
				await session.SynchronizeAsync(token);
				NaturalFlightDecision ready = NaturalFlightPolicy.CanTakeOff(new NaturalTakeoffObservation(true, session.CurrentPosition, false,
					contract.Flight.WaterLevel, fixture.Clock.NowMillis, lastTakeoff, false, false, false), zones);
				Assert.True(ready.Allowed, ready.Reason);
				lastTakeoff = fixture.Clock.NowMillis;
				speed = await NaturalFlightProtocol.TakeOffAsync(session, token);
				airborne = true;
				sorties++;
			}
			await session.SynchronizeAsync(token);
			var observation = new NaturalAirCombatObservation(session.CurrentPosition, player.GetLifeStats().GetCurrentFp(), speed,
				NaturalAirCombat.VisibleFungus(session, shotDown), landing);
			NaturalAirCombatDecision decision = NaturalAirCombatPolicy.Decide(observation);
			session.TraceDiagnostic("af06-decision", new Dictionary<string, object?>
			{
				["action"] = decision.Action, ["target"] = decision.Target, ["reason"] = decision.Reason, ["fp"] = observation.Fp,
				["var"] = NaturalAirCombat.QuestVar(session, fungusQuest),
			});
			if (decision.Action == "attack")
			{
				session.BeginStep($"s{++step:00}", $"shoot-fungus-{decision.Target}");
				BotPosition fungus = observation.Targets.Single(target => target.ObjectId == decision.Target).Position;
				(BotPosition hover, NaturalFlightRoute route) = NaturalAirCombat.FindHover(geometry, altgard, zones, session.CurrentPosition, fungus, cruise)
					?? throw new InvalidDataException($"No hover point in sight of fungus {decision.Target} at {fungus}.");
				long started = fixture.Clock.NowMillis;
				await NaturalFlightProtocol.FlyAsync(session, altgard, session.CurrentPosition, route.Waypoints, speed, token);
				long shooting = fixture.Clock.NowMillis;
				bool killed = await NaturalAirCombat.ShootDownAsync(session, decision.Target!.Value, fungusQuest,
					(origin, skill, level, target) => runtime.CreateSpellCast(session.Api.World, origin, skill, level, target), token);
				shotDown.Add(decision.Target.Value);
				if (!killed)
				{
					Assert.True(++missed <= 2, $"fungus {decision.Target} was not shot down");
					continue;
				}
				kills++;
				killSeconds.Add((fixture.Clock.NowMillis - shooting) / 1000.0);
				Console.WriteLine($"AF-06 kill {kills}: fungus {decision.Target} at ({fungus.X:F0}, {fungus.Y:F0}, {fungus.Z:F0}), flight {(shooting - started) / 1000.0:F1} s, " +
					$"shooting {(fixture.Clock.NowMillis - shooting) / 1000.0:F1} s, FP {player.GetLifeStats().GetCurrentFp()}, var {NaturalAirCombat.QuestVar(session, fungusQuest)}");
			}
			else
			{
				session.BeginStep($"s{++step:00}", $"land-on-the-rock-{decision.Action}");
				NaturalFlightRoute route = NaturalFlightProtocol.Plan(geometry, altgard, session.CurrentPosition, rock, cruise);
				Assert.True(route.IsUsable, route.Refusal);
				await NaturalFlightProtocol.FlyAsync(session, altgard, session.CurrentPosition, route.Waypoints, speed, token);
				await NaturalFlightProtocol.LandAsync(session, token);
				airborne = false;
				Console.WriteLine($"AF-06 landed on the rock to refill ({decision.Reason}) with FP {player.GetLifeStats().GetCurrentFp()}");
				Assert.True(player.GetLifeStats().GetCurrentFp() >= 1, "ran out of flight time before landing");
			}
			Assert.True(sorties <= 6 && kills <= 8, "too many sorties or kills");
		}
		if (airborne)
		{
			NaturalFlightRoute home = NaturalFlightProtocol.Plan(geometry, altgard, session.CurrentPosition, rock, cruise);
			await NaturalFlightProtocol.FlyAsync(session, altgard, session.CurrentPosition, home.Waypoints, speed, token);
			await NaturalFlightProtocol.LandAsync(session, token);
		}
		await session.SynchronizeAsync(token);
		Assert.Equal(QuestStatus.REWARD, player.GetQuestStateList().GetQuestState(fungusQuest).GetStatus());
		Assert.False(player.IsDead());
		Assert.False(player.IsFlying());
		Console.WriteLine($"AF-06 done: {kills} kills in {sorties} sorties; shooting times {string.Join(", ", killSeconds.Select(value => value.ToString("F1")))} s; " +
			$"lands with FP {player.GetLifeStats().GetCurrentFp()} at ({player.GetX():F1}, {player.GetY():F1}, {player.GetZ():F1})");
		policy.AssertClean();
	}
}
