using Aion.Bots.Protocol;
using Aion.Bots.Reflexes;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Utils;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// D33 (AB-Q6): Q1044 "Testing Flight Skills" and Q2042 "The Last Checkpoint" end the quest timer from their die and
	/// enter-world hooks. Java runs those hooks for every player and calls questTimerEnd unconditionally, and the timer is one
	/// slot per player, so a death, a relog or a respawning teleport ended any other quest's timer (Q2288's 600 s here). The
	/// correction ends it only for a player flying that quest's own ring course (var 2-7), which still fails the course as
	/// Java does: Q2042 goes to var 9, Q1044 to var 10. The quest states and timers are GM setup, not natural play.
	/// </summary>
	[SkippableFact]
	public async Task RingCourseQuestsEndOnlyTheirOwnTimer()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, money = 2288, flight = 1044, checkpoint = 2042;
		using var policy = NewPolicy("D33", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(fixture, policy, "b01", 70, "Asimtimer", Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		bool TimerRunning() => player.GetController().HasTask(TaskId.QUEST_TIMER);
		int ResetsSince(int start) => session.PacketHistory.Skip(start).Count(packet => packet.PacketType == typeof(SM_QUEST_ACTION) &&
			packet.Get<int>("questId") is flight or checkpoint);

		// Another quest's timer survives a death and a respawning teleport (CM_LEVEL_READY runs the enter-world hooks).
		session.BeginStep("s01", "another-quests-timer-survives");
		Assert.True(player.GetQuestStateList().AddQuest(money, new QuestState(money, QuestStatus.START, 1, 0, 0, null, null, null)));
		QuestService.QuestTimerStart(new QuestEnv(null!, player, money), 600);
		Assert.True(TimerRunning());
		int packets = session.PacketHistory.Count;
		Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnDie(new QuestEnv(null!, player, 0));
		Assert.True(TimerRunning(), "A death ended Q2288's timer.");
		await TeleportForSetupAsync(session, player, altgard, 1801.2f, 711.37f, 262.09f, token);
		await session.SynchronizeAsync(token);
		Assert.True(TimerRunning(), "A respawning teleport ended Q2288's timer.");
		Assert.Equal(0, ResetsSince(packets));
		QuestService.QuestTimerEnd(new QuestEnv(null!, player, money));

		// A player on a ring course still fails it on a death or a world entry, as in Java.
		foreach ((int questId, int failedVar, bool die) in new[] { (checkpoint, 9, true), (checkpoint, 9, false), (flight, 10, true) })
		{
			session.BeginStep($"s02-{questId}-{(die ? "die" : "enter")}", "own-course-fails");
			if (player.GetQuestStateList().GetQuestState(questId) is { } again)
			{
				again.SetStatus(QuestStatus.START);
				again.SetQuestVarById(0, 3);
			}
			else
				Assert.True(player.GetQuestStateList().AddQuest(questId, new QuestState(questId, QuestStatus.START, 3, 0, 0, null, null, null)));
			QuestService.QuestTimerStart(new QuestEnv(null!, player, questId), 70);
			Assert.True(TimerRunning());
			if (die)
				Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnDie(new QuestEnv(null!, player, 0));
			else
				Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnEnterWorld(player);
			Assert.False(TimerRunning(), $"Q{questId}: the course timer survived.");
			Assert.Equal(failedVar, player.GetQuestStateList().GetQuestState(questId).GetQuestVarById(0));
		}
		policy.AssertClean();
	}
	/// <summary>
	/// D34: Q2947 "Following Through" fails Garm's timed arena from its timer-end hook. Java's hook asks only for START and
	/// fewer than ten kills, and the engine runs it whenever any quest timer ends, so another quest's timer sent a player who was
	/// anywhere on Q2947 to Garm at var 6 (from var 0 that skipped Kvasir too). The correction answers only the player's own
	/// attempt: START, var 5, fewer than ten kills, inside the arena 320090000. Movie 167's end, which restarts the arena timer,
	/// is held to the same attempt. The quest states and timers are GM setup, not natural play.
	/// </summary>
	[SkippableFact]
	public async Task ArenaQuestIgnoresAnotherQuestsTimer()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int money = 2288, following = 2947;
		using var policy = NewPolicy("D34", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(fixture, policy, "b01", 235, "Asimarenatimer", Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		(int Map, float X, float Y, float Z) Where() => (player.GetWorldId(), player.GetX(), player.GetY(), player.GetZ());
		var start = Where();
		var quest = new QuestState(following, QuestStatus.START, 4, 0, 0, null, null, null);
		Assert.True(player.GetQuestStateList().AddQuest(following, quest));
		int Touched(int since) => session.PacketHistory.Skip(since).Count(packet => packet.PacketType == typeof(SM_PLAYER_SPAWN) ||
			packet.PacketType == typeof(SM_QUEST_ACTION) && packet.Get<int>("questId") == following);

		// Q2288's own timer runs out: its handler abandons Q2288, as in Java. Q2947, at Kvasir's var 4, is left alone.
		session.BeginStep("s01", "another-quests-timer-runs-out");
		Assert.True(player.GetQuestStateList().AddQuest(money, new QuestState(money, QuestStatus.START, 1, 0, 0, null, null, null)));
		QuestService.QuestTimerStart(new QuestEnv(null!, player, money), 5);
		int packets = session.PacketHistory.Count;
		await session.AdvanceAsync(TimeSpan.FromSeconds(6), token);
		await session.SynchronizeAsync(token);
		Assert.Null(player.GetQuestStateList().GetQuestState(money));
		Assert.Equal(4, quest.GetQuestVars().GetQuestVars());
		Assert.Equal(start, Where());
		Assert.Equal(0, Touched(packets));

		// Whatever step Q2947 is at outside the arena, a timer's end does not move it, and movie 167's end starts no arena timer
		// over the one that is running.
		QuestService.QuestTimerStart(new QuestEnv(null!, player, money), 600);
		var engine = Aion.GameServer.QuestEngine.QuestEngine.GetInstance();
		foreach (int var in new[] { 0, 4, 6, 5 })
		{
			session.BeginStep($"s02-var{var}", "timer-end-and-movie-end-outside-the-arena");
			quest.SetQuestVar(var);
			foreach (bool timerEnd in new[] { true, false })
			{
				packets = session.PacketHistory.Count;
				if (timerEnd) engine.OnQuestTimerEnd(new QuestEnv(null!, player, 0));
				else engine.OnMovieEnd(new QuestEnv(null!, player, following), 167);
				await session.SynchronizeAsync(token);
				string hook = $"Q2947 at var {var}, {(timerEnd ? "a timer's end" : "movie 167's end")}";
				Assert.True((QuestStatus.START, var) == (quest.GetStatus(), quest.GetQuestVars().GetQuestVars()), $"{hook}: the quest moved.");
				Assert.True(start == Where(), $"{hook}: the player moved.");
				Assert.True(player.GetController().HasScheduledTask(TaskId.QUEST_TIMER), $"{hook}: the other quest's timer was ended.");
				Assert.True(Touched(packets) == 0, $"{hook}: Q2947 sent a quest update, a timer or a teleport.");
			}
		}
		QuestService.QuestTimerEnd(new QuestEnv(null!, player, money));
		policy.AssertClean();
	}

	/// <summary>
	/// D34: the arena timer runs out on a live player. Java's statements stay: var 6, which clears the kill counter with it, and
	/// the teleport to Garm. The correction then destroys the failed attempt's instance, which the player has just left, so
	/// Garm's next SETPRO3 leads to a new one. Before D34 the entrance returned to the old instance for ten minutes.
	/// </summary>
	[SkippableFact]
	public async Task ArenaTimerRunningOutSendsALivePlayerToGarmAndResetsTheArena()
	{
		await RunCapitalProbeAsync("D34A", 236, "Asimarenaexpire", async (probe, session, token) =>
		{
			Player server = probe.Server;
			await AxArenaStartAsync(probe, session, token);
			(int first, int firstAlive) = await AxEnterArenaAsync(probe, session, again: false, token);
			await AxKillSpiritAsync(session, server, token);
			Assert.Equal((5, 1), (AxArenaVar(server), AxArenaKills(server)));

			await session.AdvanceAsync(TimeSpan.FromSeconds(241), token);
			await AxFollowTeleportAsync(session, token);
			QuestState quest = server.GetQuestStateList().GetQuestState(2947);
			float miss = AxDistanceFromGarmTeleport(server);
			bool gone = !InstanceService.InstanceExists(AxArena, first);
			Assert.Equal((QuestStatus.START, 6), (quest.GetStatus(), quest.GetQuestVars().GetQuestVars()));
			Assert.Equal(AxPandaemonium, server.GetWorldId());
			Assert.True(miss < 0.5f, $"the player is {miss:F1} m from Java's teleport point");
			Assert.False(server.IsDead());
			Assert.False(server.GetController().HasScheduledTask(TaskId.QUEST_TIMER));
			Assert.True(gone, "the failed attempt's instance still exists");
			Assert.Null(InstanceService.GetRegisteredInstance(AxArena, server.GetObjectId()));

			(int second, int secondAlive) = await AxEnterArenaAsync(probe, session, again: true, token);
			int secondKills = AxArenaKills(server);
			await AxLeaveArenaAsync(session, server, token);
			Console.WriteLine($"D34 expiry alive: instance {first} with {firstAlive} spirits, one killed; the 240 s ran out: var 6, {miss:F2} m from " +
				$"(1006.1, 1526, 222.2), instance {first} destroyed {gone}; Garm again: instance {second} with {secondAlive} spirits, {secondKills} counted");
			Assert.Equal(12, firstAlive);
			Assert.NotEqual(first, second);
			Assert.Equal((12, 0), (secondAlive, secondKills));
		});
	}

	/// <summary>
	/// D36: a death fails the attempt at once. 4.8 retail: "Deaths will incur death penalty as normal, and the player must speak
	/// to Garm again to retry." Java has no die hook, so the timer ran on over the corpse and its end revived the player beside
	/// Garm. Now the death itself ends the timer and sets var 6, and nothing more happens at 240 s. The player revives in the
	/// ordinary way: at the bind point here, where the world entry resets the failed arena (D34).
	/// </summary>
	[SkippableFact]
	public async Task ArenaDeathFailsTheAttemptAtOnceAndABindReviveLeadsBackToGarm()
	{
		await RunCapitalProbeAsync("D36A", 237, "Asimarenacorpse", async (probe, session, token) =>
		{
			Player server = probe.Server;
			QuestState Quest() => server.GetQuestStateList().GetQuestState(2947);
			await AxArenaStartAsync(probe, session, token);
			(int first, int firstAlive) = await AxEnterArenaAsync(probe, session, again: false, token);
			await AxKillSpiritAsync(session, server, token);
			Assert.Equal((5, 1), (AxArenaVar(server), AxArenaKills(server)));

			int packets = session.PacketHistory.Count;
			DecodedBotServerPacket prompt = await AxDieAsync(session, server, token);
			Assert.False(prompt.Get<bool>("allowInstanceRevive"));
			bool timerEnded = session.PacketHistory.Skip(packets).Any(packet => packet.PacketType == typeof(SM_QUEST_ACTION) &&
				packet.Fields.TryGetValue("action", out object? action) && action is byte and 4 && packet.Get<int>("questId") == 2947 && packet.Get<int>("timer") == 0);
			Assert.Equal((QuestStatus.START, 6), (Quest().GetStatus(), Quest().GetQuestVars().GetQuestVars()));
			Assert.False(server.GetController().HasScheduledTask(TaskId.QUEST_TIMER), "the death left the arena timer running");
			Assert.True(server.IsDead());
			Assert.Equal((AxArena, first), (server.GetWorldId(), server.GetInstanceId()));

			// The 240 s pass over the corpse: no teleport, no revive, no second failure.
			await session.AdvanceAsync(TimeSpan.FromSeconds(241), token);
			await session.SynchronizeAsync(token);
			Assert.True(server.IsDead());
			Assert.Equal((AxArena, 6), (server.GetWorldId(), Quest().GetQuestVars().GetQuestVars()));

			await session.SendPacketAsync(session.Api.Revive(BotReviveType.Bind), token);
			await AxFollowTeleportAsync(session, token, mapId: null);
			int bindMap = server.GetWorldId();
			bool gone = !InstanceService.InstanceExists(AxArena, first);
			Assert.NotEqual(AxArena, bindMap);
			Assert.False(server.IsDead());
			Assert.Equal(6, Quest().GetQuestVars().GetQuestVars());
			Assert.True(gone, "the failed attempt's instance still exists");
			Assert.Null(InstanceService.GetRegisteredInstance(AxArena, server.GetObjectId()));

			(int second, int secondAlive) = await AxEnterArenaAsync(probe, session, again: true, token);
			int secondKills = AxArenaKills(server);
			await AxLeaveArenaAsync(session, server, token);
			Console.WriteLine($"D36 death: instance {first} with {firstAlive} spirits, one killed; died: var 6 at once, timer-end packet {timerEnded}, no timer; " +
				$"241 s later still dead in the arena; bind revive on map {bindMap}: instance {first} destroyed {gone}; Garm again: instance {second} " +
				$"with {secondAlive} spirits, {secondKills} counted");
			Assert.True(timerEnded, "the client was not told the arena timer ended");
			Assert.NotEqual(first, second);
			Assert.Equal((12, 12, 0), (firstAlive, secondAlive, secondKills));
		});
	}

	/// <summary>
	/// D36: a self-revive does not save the attempt. A Cleric who dies under Hand of Reincarnation (4005, learned at level 22)
	/// and revives in place stands in the arena at var 6: no timer runs and a kill counts for nothing. In Java the same Cleric
	/// was still on the attempt. The way on is the exit and Garm, who sends the player into a new arena.
	/// </summary>
	[SkippableFact]
	public async Task ArenaDeathUnderASelfReviveStillFailsTheAttempt()
	{
		await RunCapitalProbeAsync("D36B", 238, "Asimarenadeath", async (probe, session, token) =>
		{
			Player server = probe.Server;
			QuestState Quest() => server.GetQuestStateList().GetQuestState(2947);
			bool TimerRunning() => server.GetController().HasScheduledTask(TaskId.QUEST_TIMER);
			await AxArenaStartAsync(probe, session, token);
			(int first, _) = await AxEnterArenaAsync(probe, session, again: false, token);
			await AxCastOnSelfAsync(session, 4005, token);
			DecodedBotServerPacket prompt = await AxDieAsync(session, server, token);
			Assert.True(prompt.Get<bool>("allowReviveBySkill"));
			Assert.Equal(6, Quest().GetQuestVars().GetQuestVars());
			Assert.False(TimerRunning(), "the death left the arena timer running");

			await session.SendPacketAsync(session.Api.Revive(BotReviveType.Rebirth), token);
			await session.SynchronizeAsync(token);
			Assert.False(server.IsDead());
			Assert.Equal((6, AxArena, first), (AxArenaVar(server), server.GetWorldId(), server.GetInstanceId()));
			int history = session.PacketHistory.Count;
			await AxKillSpiritAsync(session, server, token);
			await session.AdvanceAsync(TimeSpan.FromSeconds(241), token);
			await session.SynchronizeAsync(token);
			int afterKill = Quest().GetQuestVars().GetQuestVars();
			Assert.Equal((6, AxArena), (afterKill, server.GetWorldId()));
			Assert.False(TimerRunning());
			Assert.DoesNotContain(session.PacketHistory.Skip(history), packet => AxIsTimer(packet, 2947));

			await AxLeaveArenaAsync(session, server, token);
			bool gone = !InstanceService.InstanceExists(AxArena, first);
			Assert.Null(InstanceService.GetRegisteredInstance(AxArena, server.GetObjectId()));
			(int second, int secondAlive) = await AxEnterArenaAsync(probe, session, again: true, token);
			int secondKills = AxArenaKills(server);
			await AxLeaveArenaAsync(session, server, token);
			Console.WriteLine($"D36 self-revive: died in instance {first} under Hand of Reincarnation: var 6 at once, no timer; revived in place, a kill " +
				$"left the vars at {afterKill}, nothing at 240 s; left by the exit: instance {first} destroyed {gone}; Garm again: instance {second} with " +
				$"{secondAlive} spirits, {secondKills} counted");
			Assert.True(gone, "the failed attempt's instance still exists");
			Assert.NotEqual(first, second);
			Assert.Equal((12, 0), (secondAlive, secondKills));
		});
	}

	/// <summary>
	/// D34 leaves the success path as Java has it: the tenth kill ends the timer and plays movie 168, whose end teleports the
	/// player to Garm at var 5 with ten kills. The cleared instance is not reset: it keeps its registration and is destroyed by
	/// the ordinary checker, 600 s after the player left and checked once a minute. Nine counted kills are probe setup.
	/// </summary>
	[SkippableFact]
	public async Task ArenaTenthKillStillTeleportsToGarmAndLeavesTheClearedInstanceItsTenMinutes()
	{
		await RunCapitalProbeAsync("D34D", 239, "Asimarenadone", async (probe, session, token) =>
		{
			Player server = probe.Server;
			await AxArenaStartAsync(probe, session, token);
			(int cleared, int alive) = await AxEnterArenaAsync(probe, session, again: false, token);
			QuestState quest = server.GetQuestStateList().GetQuestState(2947);
			quest.SetQuestVarById(4, 9);
			PacketSendUtility.SendPacket(server, new SM_QUEST_ACTION(SM_QUEST_ACTION.ActionType.UPDATE, quest));
			await session.SynchronizeAsync(token);
			int packets = session.PacketHistory.Count;

			await AxKillSpiritAsync(session, server, token);
			await NaturalMovieGate.FinishAsync(session, token);
			await AxFollowTeleportAsync(session, token);
			float miss = AxDistanceFromGarmTeleport(server);
			bool movie = session.PacketHistory.Skip(packets).Any(packet => packet.PacketType == typeof(SM_PLAY_MOVIE) && packet.Get<int>("cutsceneId") == 168);
			bool kept = InstanceService.InstanceExists(AxArena, cleared);
			Assert.True(movie, "the tenth kill did not play movie 168");
			Assert.Equal((QuestStatus.START, 5, 10), (quest.GetStatus(), quest.GetQuestVarById(0), quest.GetQuestVarById(4)));
			Assert.Equal(AxPandaemonium, server.GetWorldId());
			Assert.True(miss < 0.5f, $"the player is {miss:F1} m from Java's teleport point");
			Assert.False(server.GetController().HasTask(TaskId.QUEST_TIMER));
			Assert.True(kept, "the cleared instance was destroyed");
			Assert.Equal(cleared, InstanceService.GetRegisteredInstance(AxArena, server.GetObjectId())?.GetInstanceId());

			await session.AdvanceAsync(TimeSpan.FromSeconds(599), token);
			await session.SynchronizeAsync(token);
			bool keptAtTen = InstanceService.InstanceExists(AxArena, cleared);
			await session.AdvanceAsync(TimeSpan.FromSeconds(62), token);
			await session.SynchronizeAsync(token);
			bool goneAfter = !InstanceService.InstanceExists(AxArena, cleared);
			Console.WriteLine($"D34 success: instance {cleared} with {alive} spirits, nine kills set up; the tenth: movie 168 {movie}, {miss:F2} m from " +
				$"(1006.1, 1526, 222.2), var 5 with ten kills, instance {cleared} kept {kept}; still there after 599 s {keptAtTen}; destroyed after 661 s {goneAfter}");
			Assert.Equal((QuestStatus.START, 5, 10), (quest.GetStatus(), quest.GetQuestVarById(0), quest.GetQuestVarById(4)));
			Assert.True(keptAtTen, "the cleared instance did not live its 600 s");
			Assert.True(goneAfter, "the ordinary checker did not destroy the cleared instance");
		});
	}

	/// <summary>
	/// D35: Garm sends the player into the arena. Java's Q2947 only sets var 5 at Garm's SETPRO3 and leaves the walk to the
	/// entrance 700368. 4.8 retail's Garm says "I'll send you to the Arena as soon as you're ready", and Java's Elyos twin Q1922
	/// makes a new instance and teleports from the same dialog action. The correction does what the twin does, at var 4 and at
	/// var 6 only: a SETPRO3 from a step whose dialog does not offer it still does nothing. The quest states are GM setup.
	/// </summary>
	[SkippableFact]
	public async Task GarmSendsThePlayerStraightIntoANewArena()
	{
		await RunCapitalProbeAsync("D35", 96, "Asimarenasend", async (probe, session, token) =>
		{
			Player server = probe.Server;
			await AxArenaStartAsync(probe, session, token);

			// Before Kvasir (var 0) Garm's dialog offers no SETPRO3. One sent anyway moves nobody and makes no instance.
			AxSetQuest(server, 2947, QuestStatus.START, 0);
			await session.SynchronizeAsync(token);
			await probe.SetupNearAsync(AxPandaemonium, 204089);
			int garm = await probe.WalkNpcAsync(204089);
			await NaturalDialogProtocol.OpenAsync(session, garm, token);
			await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
			await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialogExpectRejection(garm, DialogAction.SETPRO3, questId: 2947), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(new PendingQuestDialogAction(garm, DialogAction.SETPRO3, 2947), session.Api.QuestDialogEchoes.ConsumeExpectedRejection());
			Assert.Equal((0, AxPandaemonium), (AxArenaVar(server), server.GetWorldId()));
			Assert.Null(InstanceService.GetRegisteredInstance(AxArena, server.GetObjectId()));

			// From var 4: the talk itself ends in the arena. No object is used on the way.
			AxSetQuest(server, 2947, QuestStatus.START, 4);
			await session.SynchronizeAsync(token);
			int packets = session.PacketHistory.Count;
			(int first, int firstAlive) = await AxEnterArenaAsync(probe, session, again: false, token);
			bool usedAnObject = session.PacketHistory.Skip(packets).Any(packet => packet.PacketType == typeof(SM_USE_OBJECT));
			byte heading = server.GetHeading();
			await AxKillSpiritAsync(session, server, token);
			int counted = AxArenaKills(server);

			// The attempt fails at the exit. Garm sends the player back, into another new arena.
			await AxLeaveArenaAsync(session, server, token);
			packets = session.PacketHistory.Count;
			(int second, int secondAlive) = await AxEnterArenaAsync(probe, session, again: true, token);
			bool usedAnObjectAgain = session.PacketHistory.Skip(packets).Any(packet => packet.PacketType == typeof(SM_USE_OBJECT));
			bool firstGone = !InstanceService.InstanceExists(AxArena, first);
			int secondKills = AxArenaKills(server);
			await AxLeaveArenaAsync(session, server, token);

			Console.WriteLine($"D35: SETPRO3 at var 0 did nothing; at var 4 Garm sent the player to instance {first} with {firstAlive} spirits, heading {heading}, " +
				$"object used {usedAnObject}; one kill counted {counted}; left by the exit: var 6; at var 6 Garm sent the player to instance {second} with " +
				$"{secondAlive} spirits and {secondKills} kills, object used {usedAnObjectAgain}; instance {first} destroyed {firstGone}");
			Assert.False(usedAnObject || usedAnObjectAgain, "an attempt used an object on the way into the arena");
			Assert.Equal((12, 1, 90), (firstAlive, counted, heading));
			Assert.NotEqual(first, second);
			Assert.True(firstGone, "the failed attempt's instance still exists");
			Assert.Equal((12, 0), (secondAlive, secondKills));
		});
	}

	/// <summary>An ordinary self cast through the client protocol, as BC-04 casts Hand of Reincarnation.</summary>
	private static async Task AxCastOnSelfAsync(SimulationL0Session session, ushort skillId, CancellationToken token)
	{
		BotSkill skill = session.Api.World.Skills[skillId];
		await session.SendPacketAsync(session.Api.Target(session.CharacterId), token);
		await session.SendPacketAsync(session.Api.Cast(new SpellCastData(skillId, checked((byte)skill.Level), 0)
			{ TargetObjectId = session.CharacterId }), token);
		DecodedBotServerPacket cast = await BotCastProtocol.WaitForStartAsync((predicate, waitToken) => session.WaitForPacketAsync(
			packet => predicate(packet) || BotCastProtocol.IsStartRejection(packet), waitToken), session.CharacterId, skillId, token);
		Assert.Equal(typeof(SM_CASTSPELL), cast.PacketType);
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(cast.Get<ushort>("castDuration") + 1), token);
		DecodedBotServerPacket effect = await BotCastProtocol.WaitForCompletionAsync(session.WaitForPacketAsync, session.CharacterId, skillId, token);
		Assert.Equal(typeof(SM_CASTSPELL_RESULT), effect.PacketType);
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(effect.Get<ushort>("hitTime") + 1), token);
		await session.SynchronizeAsync(token);
		Assert.True(NaturalAltgardQuestSteps.HasEffect(session.Api.World, skillId));
	}
}
