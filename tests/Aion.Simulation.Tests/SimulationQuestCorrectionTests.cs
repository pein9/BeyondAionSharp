using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// AF-00 / D26: Q2209 "The Scribbler". Java's handler has a Borender (203572) step at var 1 but never registers him
	/// for the talk event, so a click on him never reaches the quest (docs/upstream-reports/q2209-the-scribbler.md). With
	/// Q24011 completed, nothing else puts him in the quest-selection page, which is the case this pins: the click opens
	/// page 10, the quest page is 1693, and SETPRO2 moves var 1 to 2. The quest states are GM setup, not natural play.
	/// </summary>
	[SkippableFact]
	public async Task Q2209BorenderIsRegisteredAndAdvancesTheScribbler()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, scribbler = 2209, fungus = 24011, borender = 203572;
		using var policy = NewPolicy("AF00", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(fixture, policy, "b01", 92, "Asimscribe", Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		Assert.True(player.GetQuestStateList().AddQuest(fungus, new QuestState(fungus, QuestStatus.COMPLETE, 0, 0, 1, null, null, null)));
		Assert.True(player.GetQuestStateList().AddQuest(scribbler, new QuestState(scribbler, QuestStatus.START, 1, 0, 0, null, null, null)));
		// Borender's floating rock above the fortress (spawn 1620.69, 1806.66, 406).
		await TeleportForSetupAsync(session, player, altgard, 1618.5f, 1806.66f, 406f, token);
		await session.SynchronizeAsync(token);
		int borenderObject = await session.WaitForNpcAsync(borender, token);

		session.BeginStep("s01", "talk-to-borender");
		await NaturalDialogProtocol.OpenAsync(session, borenderObject, token);
		DecodedBotServerPacket opened = await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
			packet => packet.Get<int>("targetObjectId") == borenderObject);
		Assert.Equal((10, 0), (opened.Get<ushort>("dialogPageId"), opened.Get<int>("questId")));
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(borenderObject, DialogAction.QUEST_SELECT, questId: scribbler), token);
		DecodedBotServerPacket page = await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
			packet => packet.Get<int>("targetObjectId") == borenderObject && packet.Get<int>("questId") == scribbler);
		Assert.Equal(1693, page.Get<ushort>("dialogPageId"));

		session.BeginStep("s02", "setpro2-advances-var");
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(borenderObject, DialogAction.SETPRO2, questId: scribbler), token);
		await session.WaitForPacketAsync(typeof(SM_QUEST_ACTION), token, packet => packet.Get<int>("questId") == scribbler);
		QuestState state = player.GetQuestStateList().GetQuestState(scribbler);
		Assert.Equal((QuestStatus.START, 2), (state.GetStatus(), state.GetQuestVarById(0)));
		await session.SendPacketAsync(session.Api.CloseDialog(borenderObject), token);
		policy.AssertClean();
	}
}
