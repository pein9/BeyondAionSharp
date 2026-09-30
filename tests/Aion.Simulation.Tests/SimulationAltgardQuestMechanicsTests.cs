using Aion.Bots.Dashboard;
using Aion.Bots.Movement;
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
	/// AF-07 (docs/natural-altgard-leveling.md): the scripted Leg 1 quests played through the contract's steps with
	/// <see cref="NaturalAltgardQuestSteps"/>: Q2209 (Thrud, Tulberg, Borender by flight, Noroia in the dungeon, Thrud),
	/// Q2207 (Emgata, Itu, Suthran, Itu), Q2208 (Itu, the Mau Secret Remedy, Mumu Bon in the dungeon, Itu) and the Q24011
	/// campaign (auto-start at level 11, Valurion, Borender with his movie, the air kills, Valurion's reward). GM setup: a
	/// level 10 Daeva Cleric like the <c>altgard</c> snapshot (Q24010 done, Q24011 LOCKED), and level 11 set after Q2209
	/// instead of hunting for it. Walking and flying use the AF-02..AF-06 code; choosing the next quest is AF-08's.
	/// </summary>
	[SkippableFact]
	public async Task AltgardScriptedQuestsPlayThroughTheContractSteps()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000;
		using var policy = NewPolicy("AF07", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
		CancellationToken token = timeout.Token;
		string root = Aion.GameServer.TestKit.RealStaticData.RepoRoot();
		NaturalAltgardContract contract = NaturalAltgardContract.LoadDefault();
		IReadOnlyList<NaturalFlyZone> zones = NaturalFlyZone.Load(Path.Combine(root, "game-server/data/static_data/zones/zones_220030000.xml"));
		string directory = Path.Combine(root, "run", "af07");
		Directory.CreateDirectory(directory);
		string tracePath = Path.Combine(directory, $"af07-s{fixture.Seed}-{DateTime.UtcNow:yyyyMMddHHmmss}.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(tracePath, "af07", "b01", "sim-player-138",
			virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 138, "Asimaltgard", Race.ASMODIANS, trace, tracePath);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(player, PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		player.GetCommonData().SetLevel(10);
		SkillLearnService.LearnNewSkills(player, 1, 10);
		foreach (int done in contract.Start.CompletedQuestIds.Where(id => player.GetQuestStateList().GetQuestState(id) == null))
			Assert.True(player.GetQuestStateList().AddQuest(done, new QuestState(done, QuestStatus.COMPLETE)));
		foreach (int locked in contract.Start.LockedQuestIds)
			Assert.True(player.GetQuestStateList().AddQuest(locked, new QuestState(locked, QuestStatus.LOCKED)));
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(
			fixture.World.GetWorldMap(altgard).GetMainWorldMapInstance().GetInstanceId(), Race.ASMODIANS);
		BotPosition ground = geometry.SnapToGround(altgard,
			new BotPosition(contract.Hub.Anchor[0] - 3, contract.Hub.Anchor[1], contract.Hub.Anchor[2] + 1, 0))
			?? throw new InvalidDataException("No ground beside the Altgard obelisk.");
		await TeleportForSetupAsync(session, player, altgard, ground.X, ground.Y, ground.Z, token);
		await session.SynchronizeAsync(token);
		NaturalAltgardStep borenderStep = contract.Steps.First(step => step.Area == "borender-rock");
		BotPosition rock = geometry.SnapToGround(altgard, new BotPosition(borenderStep.Position[0] - 2.5f, borenderStep.Position[1], borenderStep.Position[2] + 3, 0))
			?? throw new InvalidDataException("No rock top beside Borender.");
		float cruise = rock.Z + 8;
		var runtime = new NaturalJourneyRuntime(root, "SIM-af07", fixture.Seed, fixture.DataManager.StaticData,
			() => fixture.Clock.NowMillis, fixture.Epoch, () => geometry, _ => Task.FromResult(false), policy.AssertClean,
			() => policy.SnapshotProblems(), trace, new LiveBotDashboardState());
		long? lastTakeoff = null;
		int stepNumber = 0;
		var log = new List<string>();

		async Task PlayAsync(string key)
		{
			NaturalAltgardStep step = contract.Steps.Single(candidate => candidate.Key == key);
			session.BeginStep($"s{++stepNumber:00}", key);
			var at = new BotPosition(step.Position[0], step.Position[1], step.Position[2], 0);
			if (step.Flight)
				await FlyAsync(session.CurrentPosition, rock);
			else
			{
				if (session.CurrentPosition.Z > rock.Z - 20) await FlyAsync(session.CurrentPosition, ground);
				await WalkAsync(at);
			}
			int npc = await session.WaitForNpcAsync(step.NpcId, token);
			// NPCs are talked to where the client sees them, which is not always their spawn point.
			BotPosition seen = session.Api.World.Objects[npc].SettledPosition;
			if (!step.Flight && NaturalFlightPolicy.Distance(session.CurrentPosition, seen) > step.TalkRange - 1.5f)
			{
				Console.WriteLine($"AF-07 {step.NpcId} is seen at {seen}, not at its spawn {at}");
				await WalkAsync(seen);
			}
			// A walking NPC (Tulberg) may have moved on in a long shared SIM world: follow it and talk again, as a player does.
			string change;
			for (int attempt = 1; ; attempt++)
			{
				try { change = await NaturalAltgardQuestSteps.TalkAsync(session, step, npc, token); break; }
				catch (NaturalDialogTooFarException) when (attempt < 3 && !step.Flight)
				{
					await session.SynchronizeAsync(token);
					await WalkAsync(session.Api.World.Objects[npc].Position);
				}
			}
			log.Add(change);
			Console.WriteLine($"AF-07 {change}");
		}

		async Task WalkAsync(BotPosition npc)
		{
			BotPosition from = session.CurrentPosition;
			if (NaturalFlightPolicy.Distance(from, npc) <= 3) return; // already in talk range
			IReadOnlyList<BotPosition> route = geometry.FindInteractionPath(altgard, from, npc);
			// NA-04 finding (a): some NPCs have no interaction approach point; walk to open ground within talk range.
			if (route.Count == 0)
				route = geometry.GroundAround(altgard, npc, [2f, 3f, 4f], sectors: 12)
					.Select(point => geometry.FindJourneyPath(altgard, from, point)).FirstOrDefault(path => path.Count > 0) ?? [];
			Assert.True(route.Count > 0, $"no walk from {from} to {npc}: {Aion.Bots.Navigation.NavMesh.BotNavMeshRouter.LastOutcome}");
			float speed = session.Api.World.MovementSpeed ?? throw new InvalidOperationException("No movement speed.");
			await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing).CreateGroundPlan(route, from, speed), token);
			await session.SynchronizeAsync(token);
			Console.WriteLine($"AF-07 walk {route.Count} waypoints ends {route[^1]}, server ({player.GetX():F1}, {player.GetY():F1}, {player.GetZ():F1}), npc {npc}");
		}

		async Task FlyAsync(BotPosition from, BotPosition to)
		{
			long wait = NaturalFlightPolicy.RestoreMillis(session.Api.World.CurrentFlightTime, session.Api.World.MaxFlightTime, session.Api.World.MaxFlightTime);
			if (lastTakeoff is { } last) wait = Math.Max(wait, last + NaturalFlightPolicy.TakeoffReuseMillis - fixture.Clock.NowMillis);
			if (wait > 0) await session.AdvanceAsync(TimeSpan.FromMilliseconds(wait + 100), token);
			await session.SynchronizeAsync(token);
			NaturalFlightDecision ready = NaturalFlightPolicy.CanTakeOff(new NaturalTakeoffObservation(true, from, false,
				contract.RequiredFlight.WaterLevel, fixture.Clock.NowMillis, lastTakeoff, false, false, false), zones);
			Assert.True(ready.Allowed, ready.Reason);
			NaturalFlightRoute route = NaturalFlightProtocol.Plan(geometry, altgard, from, to, cruise);
			Assert.True(route.IsUsable, route.Refusal);
			lastTakeoff = fixture.Clock.NowMillis;
			float speed = await NaturalFlightProtocol.TakeOffAsync(session, token);
			NaturalFlightDecision go = NaturalFlightPolicy.CanFly(NaturalFlightProtocol.ToPlan(route, from, speed), session.Api.World.CurrentFlightTime, zones);
			Assert.True(go.Allowed, go.Reason);
			await NaturalFlightProtocol.FlyAsync(session, altgard, from, route.Waypoints, speed, token);
			await NaturalFlightProtocol.LandAsync(session, token);
			await session.SynchronizeAsync(token);
		}

		// Q2209 at level 10: Borender is reached by flight, Noroia down the dungeon ramp.
		foreach (string key in new[] { "q2209-offer-thrud", "q2209-v0-tulberg", "q2209-v1-borender", "q2209-v2-noroia", "q2209-v3-thrud" })
			await PlayAsync(key);

		// Level 11 (GM setup in place of the Ice Lake hunting): the campaign follows Q24010 and unlocks now.
		session.BeginStep($"s{++stepNumber:00}", "level-11-unlocks-q24011");
		player.GetCommonData().SetLevel(11);
		await session.SynchronizeAsync(token);
		Assert.Equal(11, session.Api.World.Level);
		Assert.Equal(((byte)3, 0), NaturalAltgardQuestSteps.State(session.Api.World, 24011));

		foreach (string key in new[] { "q2207-offer-emgata", "q2207-v0-itu", "q2207-v1-suthran", "q2207-v2-itu", "q2208-offer-itu" })
			await PlayAsync(key);
		session.BeginStep($"s{++stepNumber:00}", "use-the-mau-secret-remedy");
		await NaturalAltgardQuestSteps.UseQuestItemAsync(session, contract.RequiredItemUse,
			fixture.DataManager.StaticData.ItemDataDh.GetItemTemplate(contract.RequiredItemUse.ItemId), token);
		foreach (string key in new[] { "q2208-v1-mumu-bon", "q2208-reward-itu", "q24011-v0-valurion", "q24011-v1-borender" })
			await PlayAsync(key);

		session.BeginStep($"s{++stepNumber:00}", "q24011-air-kills");
		NaturalAirCombat.Outcome kills = await NaturalAirCombat.RunAsync(session, geometry, altgard, zones, contract.RequiredFlight.WaterLevel,
			new NaturalLandingTarget("platform", rock), cruise, 24011,
			(origin, skill, level, target) => runtime.CreateSpellCast(session.Api.World, origin, skill, level, target),
			() => fixture.Clock.NowMillis, token);
		Console.WriteLine($"AF-07 q24011 air kills: {kills.Kills} in {kills.Sorties} sorties");
		await PlayAsync("q24011-reward-valurion");

		await session.SynchronizeAsync(token);
		foreach (int quest in new[] { 2207, 2208, 2209, 24011 })
			Assert.Contains(quest, session.Api.World.CompletedQuestIds);
		Assert.Contains(session.Api.World.Inventory.Values, item => item.ItemId == contract.RequiredRewardChoice.ItemId);
		Assert.False(player.IsDead());
		Console.WriteLine($"AF-07 done: level {session.Api.World.Level}, {log.Count} talk steps, at ({player.GetX():F1}, {player.GetY():F1}, {player.GetZ():F1})");
		policy.AssertClean();
	}
}
