using Aion.Bots.Protocol;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

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
}
