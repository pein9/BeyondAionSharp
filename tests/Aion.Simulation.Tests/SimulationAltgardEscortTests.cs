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
	/// AC-04 (docs/natural-altgard-leveling.md): the Q2290 escort, Groken to his boat, on the live SIM server. A level 15 probe
	/// Cleric (GM setup: Q2221 set COMPLETE, the robbers on the line and at the dock despawned; this probe is the escort, not
	/// the fights) proves the three ways an escort ends:
	/// <list type="number">
	/// <item>a logout while Groken follows puts the quest back to var 0;</item>
	/// <item>a GM teleport of the probe to the goal stand, 111 m off, loses him past the 50 m leash: var 0, Groken deleted, respawned 295 s later;</item>
	/// <item>then <see cref="NaturalEscortProtocol"/> waits for him, restarts at var 0 and walks him to the boat: var 3 and
	/// movie 69, Groken gone; Manir's hand-in completes the quest.</item>
	/// </list>
	/// </summary>
	[SkippableFact]
	public async Task AltgardGrokensEscapeEscortEndsInLogoutLossDistanceLossAndSuccess()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, level = 15;
		string root = Aion.GameServer.TestKit.RealStaticData.RepoRoot();
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? $"ac04-escort-s{fixture.Seed}";
		string directory = Path.Combine(root, "run", "ac04", run);
		Directory.CreateDirectory(directory);
		string tracePath = Path.Combine(directory, $"{run}.trace.jsonl");
		if (File.Exists(tracePath)) File.Delete(tracePath);
		using var trace = BotActionTraceWriter.Open(tracePath, run, "b01", "sim-player-144",
			virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		using var policy = NewPolicy("AC04", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l3");
		NaturalAltgardEscort escort = leg.EscortList.Single();
		NaturalAltgardStep Step(string key) => leg.Steps.Single(step => step.Key == key);
		await using var session = new SimulationL0Session(fixture, policy, "b01", 144, "Asimescort", Race.ASMODIANS, trace, tracePath);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player Server() => fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(Server(), PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		Server().GetCommonData().SetLevel(level);
		SkillLearnService.LearnNewSkills(Server(), 1, level);
		Assert.True(Server().GetQuestStateList().AddQuest(2221, new QuestState(2221, QuestStatus.COMPLETE)));
		var instance = fixture.World.GetWorldMap(altgard).GetMainWorldMapInstance();
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(instance.GetInstanceId(), Race.ASMODIANS);
		BotPosition Ground(float[] at, string what) => geometry.GroundAround(altgard, new BotPosition(at[0], at[1], at[2], 0), [2f, 3f, 5f])
			.FirstOrDefault() is { } ground && ground != default ? ground : throw new InvalidDataException($"No ground near {what}.");
		BotPosition grokenSpawn = Ground(Step(escort.RestartStep).Position, "Groken");
		BotPosition standPoint = NaturalEscortPolicy.GoalStand(escort.Goal, grokenSpawn);
		BotPosition stand = Ground([standPoint.X, standPoint.Y, standPoint.Z], "the goal stand");
		IReadOnlyList<BotPosition> route = geometry.FindJourneyPath(altgard, grokenSpawn, stand);
		Assert.NotEmpty(route);
		bool Aggressive(Aion.GameServer.Model.GameObjects.Npc npc) =>
			NaturalHostility.IsAggressive(npc.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
			escort.ClearAreas.Any(area => leg.Area(area).Contains(npc.GetX(), npc.GetY(), npc.GetZ()));
		int Clear()
		{
			var robbers = instance.GetNpcs().Where(Aggressive).ToArray();
			foreach (var robber in robbers) fixture.World.Despawn(robber);
			return robbers.Length;
		}
		Console.WriteLine($"AC-04 despawned {Clear()} robbers on the line and at the dock");
		await TeleportForSetupAsync(session, Server(), altgard, grokenSpawn.X + 2, grokenSpawn.Y, grokenSpawn.Z, token);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
		(byte Status, int Var)? State() => NaturalAltgardQuestSteps.State(session.Api.World, escort.QuestId);
		BotKnownObject? Groken() => session.Api.World.Objects.Values.FirstOrDefault(known =>
			known.Kind == BotKnownObjectKind.Npc && known.TemplateId == escort.FollowerNpcId);
		async Task WalkAsync(BotPosition to, CancellationToken walkToken)
		{
			IReadOnlyList<BotPosition> path = geometry.FindJourneyPath(altgard, session.CurrentPosition, to);
			if (path.Count == 0) path = [to];
			float speed = session.Api.World.MovementSpeed ?? throw new InvalidOperationException("No movement speed.");
			await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing).CreateGroundPlan(path, session.CurrentPosition, speed), walkToken);
			await session.SynchronizeAsync(walkToken);
		}

		// 1. The offer (QUEST_SELECT, QUEST_ACCEPT_1, SELECT1_1) starts the follow at var 1; a logout puts it back to var 0.
		session.BeginStep("s01", "offer-then-logout");
		int groken = await session.WaitForNpcAsync(escort.FollowerNpcId, token);
		Console.WriteLine($"AC-04 {await NaturalAltgardQuestSteps.TalkAsync(session, Step(escort.StartStep), groken, token)}");
		Assert.Equal(((byte)3, escort.FollowVar), State());
		await WalkAsync(route[Math.Min(4, route.Count - 1)], token);
		await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
		await session.SynchronizeAsync(token);
		float followedGap = NaturalEscortPolicy.Distance3(session.CurrentPosition, Groken()!.Position);
		Console.WriteLine($"AC-04 Groken follows: {followedGap:F1} m behind after an 8 m walk, speed {Groken()!.MovementSpeed}");
		Assert.True(followedGap < 8, $"Groken did not follow ({followedGap:F1} m).");
		await session.QuitAsync(token);
		await session.WaitForReentryAsync(token);
		await session.ReloginExistingCharacterAsync(token);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Assert.Equal(((byte)3, escort.LostVar), State());
		Assert.Equal(escort.LostVar, Server().GetQuestStateList().GetQuestState(escort.QuestId).GetQuestVarById(0));
		// Java: the logout clears Groken's target (CreatureController.notSee), and the next CREATURE_MOVED near him takes
		// FollowingNpcAI's no-target branch, stopFollow: he is deleted and respawns 295 s later, as after a distance loss.
		bool GrokenOnServer() => instance.GetNpcs().Any(npc => npc.GetNpcId() == escort.FollowerNpcId);
		bool presentAfterLogout = GrokenOnServer();
		long respawnWait = fixture.Clock.NowMillis;
		while (!GrokenOnServer() && fixture.Clock.NowMillis - respawnWait < (escort.FollowerRespawnSeconds + 10) * 1000L)
			await session.AdvanceAsync(TimeSpan.FromSeconds(5), token);
		await session.SynchronizeAsync(token);
		Console.WriteLine($"AC-04 after the relog: var {State()?.Var}; Groken on the server {presentAfterLogout}; " +
			$"back after {(fixture.Clock.NowMillis - respawnWait) / 1000} game s");
		Assert.True(GrokenOnServer());

		// 2. The restart (QUEST_SELECT at var 0) follows again; the probe 111 m away loses him: var 0 and Groken deleted.
		session.BeginStep("s02", "restart-then-run-past-the-leash");
		groken = await session.WaitForNpcAsync(escort.FollowerNpcId, token);
		BotPosition near = Groken()!.Position;
		if (NaturalEscortPolicy.Distance3(session.CurrentPosition, near) > 4) await WalkAsync(near, token);
		Console.WriteLine($"AC-04 {await NaturalAltgardQuestSteps.TalkAsync(session, Step(escort.RestartStep), groken, token)}");
		Assert.Equal(((byte)3, escort.FollowVar), State());
		BotPosition away = stand; // 111 m from Groken, past the 50 m leash
		await TeleportForSetupAsync(session, Server(), altgard, away.X, away.Y, away.Z, token);
		session.AcceptTeleportPosition();
		await session.AdvanceAsync(TimeSpan.FromSeconds(3), token);
		await session.SynchronizeAsync(token);
		long lostAt = fixture.Clock.NowMillis;
		Assert.Equal(((byte)3, escort.LostVar), State());
		Assert.DoesNotContain(instance.GetNpcs(), npc => npc.GetNpcId() == escort.FollowerNpcId);
		Console.WriteLine($"AC-04 lost past the leash: var {State()?.Var}, Groken deleted on the server");
		await TeleportForSetupAsync(session, Server(), altgard, grokenSpawn.X + 2, grokenSpawn.Y, grokenSpawn.Z, token);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);

		// 3. The protocol: wait out the 295 s respawn, restart, walk him to the boat.
		session.BeginStep("s03", "escort-protocol");
		var protocol = new NaturalEscortProtocol(session, leg, escort, () => fixture.Clock.NowMillis)
		{
			WalkToAsync = WalkAsync,
			Route = () => route,
			ClearAreasHaveAggressors = () => instance.GetNpcs().Any(Aggressive),
			ClearAsync = _ => { Console.WriteLine($"AC-04 cleared {Clear()} respawned robbers"); return Task.FromResult<long?>(null); },
			PriorAttempts = 2,
			FollowerGoneAtMillis = lostAt,
			// The GM teleport away and back never refreshed the client's view, so it missed the delete of this Groken.
			EndedFollowerObjectIds = [groken],
		};
		long protocolStart = fixture.Clock.NowMillis;
		NaturalEscortResult result = await protocol.RunAsync(token);
		Console.WriteLine($"AC-04 protocol: {result.Outcome} after {(fixture.Clock.NowMillis - protocolStart) / 1000} game s, attempts {result.Attempts}, " +
			$"movie {result.MovieSeen}, Groken speed {result.FollowerSpeed}; " +
			string.Join("; ", result.Log.Select(a => $"#{a.Number} {a.Outcome} {(a.EndMillis - a.StartMillis) / 1000.0:F0} s, gap max {a.LongestGap:F1} m, {a.Hops} hops")));
		Assert.Equal("done", result.Outcome);
		Assert.True(result.MovieSeen);
		Assert.Equal(((byte)3, escort.SuccessVar), State());
		NaturalEscortAttempt success = Assert.Single(result.Log);
		Assert.True(success.LongestGap < escort.Leash / 2, $"gap {success.LongestGap}");
		Assert.True(fixture.Clock.NowMillis - lostAt >= escort.FollowerRespawnSeconds * 1000L);
		await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
		await session.SynchronizeAsync(token);
		Assert.DoesNotContain(instance.GetNpcs(), npc => npc.GetNpcId() == escort.FollowerNpcId);

		// 4. Manir's hand-in (the travel back is AC-02's).
		session.BeginStep("s04", "hand-in-at-manir");
		NaturalAltgardStep handIn = Step("q2290-v3-manir");
		BotPosition manir = Ground(handIn.Position, "Manir");
		await TeleportForSetupAsync(session, Server(), altgard, manir.X, manir.Y, manir.Z, token);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
		Console.WriteLine($"AC-04 {await NaturalAltgardQuestSteps.TalkAsync(session, handIn, await session.WaitForNpcAsync(handIn.NpcId, token), token)}");
		Assert.Contains(escort.QuestId, session.Api.World.CompletedQuestIds);
		Assert.Equal(QuestStatus.COMPLETE, Server().GetQuestStateList().GetQuestState(escort.QuestId).GetStatus());
		policy.AssertClean();
	}
}
