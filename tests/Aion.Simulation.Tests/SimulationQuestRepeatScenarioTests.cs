using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.Time;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunQ5Async(ScenarioDefinition scenario, bool includeHistory)
	{
		const int questId = 9600;
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(
			fixture, policy, "b01", accountId: 41, "Aesimqrep");

		session.BeginStep("s01", "login-create-enter");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token);
		await session.EnterWorldAsync(token);
		await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);

		Player player = fixture.World.GetPlayer(session.CharacterId);
		QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(questId);
		Assert.True(template.IsDaily());
		DateTimeOffset now = ServerTime.Now();
		Assert.Equal((8, 59), (now.Hour, now.Minute));
		DateTimeOffset reset = new(now.Year, now.Month, now.Day, 9, 0, 0, now.Offset);

		session.BeginStep("s02", "finish-daily-before-reset");
		var state = new QuestState(questId, QuestStatus.REWARD, 0, 0, 0, null, null, null);
		Assert.True(player.GetQuestStateList().AddQuest(questId, state));
		Assert.True(QuestService.FinishQuest(new QuestEnv(player, player, questId)));
		Assert.Equal(QuestStatus.COMPLETE, state.GetStatus());
		Assert.Equal(1, state.GetCompleteCount());
		Assert.Equal(reset.UtcDateTime, state.GetNextRepeatTime());
		Assert.False(state.CanRepeat());
		Assert.False(QuestService.CheckStartConditions(player, questId, false, 9, false, false, false));

		session.BeginStep("s03", "hold-before-daily-reset");
		TimeSpan untilReset = reset - ServerTime.Now();
		Assert.True(untilReset > TimeSpan.FromMilliseconds(1));
		await session.AdvanceAsync(untilReset - TimeSpan.FromMilliseconds(1), token);
		Assert.Equal(reset.AddMilliseconds(-1), ServerTime.Now());
		Assert.False(state.CanRepeat());

		session.BeginStep("s04", "cross-daily-reset");
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(1), token);
		DecodedBotServerPacket resetMessage = await session.WaitForPacketAsync(typeof(SM_SYSTEM_MESSAGE), token,
			packet => packet.Get<int>("msgId") == 1400854);
		Assert.Equal("STR_MSG_QUEST_LIMIT_RESET_DAILY", resetMessage.Get<string>("name"));
		Assert.Equal(reset, ServerTime.Now());
		Assert.True(state.CanRepeat());
		Assert.True(QuestService.CheckStartConditions(player, questId, false, 9, false, false, false));
		policy.AssertClean();
	}
}
