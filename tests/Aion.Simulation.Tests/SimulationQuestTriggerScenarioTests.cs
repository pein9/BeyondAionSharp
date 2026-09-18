using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunQ3Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(
			fixture, policy, "b01", accountId: 38, "Assimqb", Race.ELYOS);

		session.BeginStep("s01", "login-create-enter-and-finish-prologue");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token);
		await session.EnterWorldAsync(token);
		DecodedBotServerPacket prologueMovie = await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
		await WaitForQuestStatusAsync(session, 1000, 5, token);
		Player player = fixture.World.GetPlayer(session.CharacterId);

		session.BeginStep("s02", "duplicate-movie-end-is-idempotent");
		await session.SendPacketAsync(GameClientPackets.PlayMovieEnd(
			prologueMovie.Get<bool>("isMovie") ? (byte)1 : (byte)0,
			prologueMovie.Get<int>("objectId"),
			prologueMovie.Get<int>("questId"),
			prologueMovie.Get<int>("cutsceneId"),
			prologueMovie.Get<bool>("canSkip")), token);
		await session.DrainServerPacketsAsync(token);
		Assert.Equal(1, player.GetQuestStateList().GetQuestState(1000).GetCompleteCount());

		session.BeginStep("s03", "level-up-starts-1100");
		Assert.True(session.Api.World.Quests.TryGetValue(1100, out BotQuestState? locked1100) && locked1100.Status == 6);
		player.GetCommonData().SetLevel(3);
		Assert.Equal(3, player.GetLevel());
		Assert.Equal(QuestStatus.START, player.GetQuestStateList().GetQuestState(1100).GetStatus());
		await session.DrainServerPacketsAsync(token);
		await session.WaitForPacketAsync(typeof(SM_STATUPDATE_EXP), token);
		Assert.Equal((byte)3, session.Api.World.Quests[1100].Status);

		Npc kalio = FindLivingNpc(player, 203067);
		await MoveBesideAsync(session, kalio, token);
		session.BeginStep("s04", "reject-premature-reward");
		await session.SendPacketAsync(session.Api.TalkTo(kalio.GetObjectId()), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		await session.SendPacketAsync(session.Api.SelectDialogExpectRejection(
			kalio.GetObjectId(), DialogAction.SELECTED_QUEST_REWARD1, questId: 1100), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
			packet => packet.Get<int>("targetObjectId") == kalio.GetObjectId() &&
				packet.Get<ushort>("dialogPageId") == DialogAction.SELECTED_QUEST_REWARD1);
		Assert.Equal(1100, session.Api.QuestDialogEchoes.ConsumeExpectedRejection().QuestId);
		Assert.Equal((byte)3, session.Api.World.Quests[1100].Status);

		session.BeginStep("s05", "cannot-give-up-mission");
		await session.SendPacketAsync(session.Api.DeleteQuest(1100), token);
		await session.DrainServerPacketsAsync(token);
		Assert.Equal((byte)3, session.Api.World.Quests[1100].Status);
		Assert.NotNull(player.GetQuestStateList().GetQuestState(1100));

		player.GetCommonData().SetLevel(7);
		Npc pernos = FindLivingNpc(player, 790001);
		await MoveBesideAsync(session, pernos, token);
		session.BeginStep("s06", "refuse-1123");
		await session.SendPacketAsync(session.Api.TalkTo(pernos.GetObjectId()), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		await session.SendPacketAsync(session.Api.SelectDialog(pernos.GetObjectId(), DialogAction.QUEST_SELECT, questId: 1123), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		await session.SendPacketAsync(session.Api.SelectDialog(pernos.GetObjectId(), DialogAction.ASK_QUEST_ACCEPT, questId: 1123), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
			packet => packet.Get<ushort>("dialogPageId") == 4);
		await session.SendPacketAsync(session.Api.SelectDialog(pernos.GetObjectId(), DialogAction.QUEST_REFUSE_1, questId: 1123), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
			packet => packet.Get<ushort>("dialogPageId") == 1004);
		Assert.False(session.Api.World.Quests.ContainsKey(1123));

		session.BeginStep("s07", "zone-entry-1123");
		await StartQuestAsync(session, pernos.GetObjectId(), 1123, token);
		await session.MoveToPositionAsync(new BotPosition(222.69f, 1900f, 170f, 0), token);
		DecodedBotServerPacket zoneMovie = await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
		Assert.Equal(11, zoneMovie.Get<int>("cutsceneId"));
		await WaitForQuestStatusAsync(session, 1123, 4, token);

		session.BeginStep("s08", "item-starts-1114");
		Assert.Equal(0, ItemService.AddItem(player, 182200214, 1, true));
		await session.DrainServerPacketsAsync(token);
		await session.WaitForPacketAsync(typeof(SM_INVENTORY_ADD_ITEM), token,
			packet => packet.Get<List<IReadOnlyDictionary<string, object?>>>("items")
				.Any(item => Get<int>(item, "itemId") == 182200214));
		Item diary = player.GetInventory().GetFirstItemByItemId(182200214);
		await session.SendPacketAsync(session.Api.UseItem(diary.GetObjectId(), diary.GetItemTemplate()), token);
		await WaitForQuestStatusAsync(session, 1114, 3, token);

		session.BeginStep("s09", "prepare-daeva-and-level-twelve");
		player.GetCommonData().SetLevel(9);
		Assert.True(ClassChangeService.SetClass(player, PlayerClass.GLADIATOR, validate: true, updateDaevaStatus: true));
		player.GetCommonData().SetLevel(12);
		await TeleportForSetupAsync(session, player, 210030000, 1364.77f, 1842.96f, 121.471f, token);

		Npc gano = FindLivingNpc(player, 203123);
		await MoveBesideAsync(session, gano, token);
		session.BeginStep("s10", "timer-expiry-abandons-1146");
		await StartQuestAsync(session, gano.GetObjectId(), 1146, token);
		Assert.Equal(900, session.Api.World.Quests[1146].TimerSeconds);
		Assert.Equal(1, ItemCount(session.Api.World, 182200519));
		await session.AdvanceAsync(TimeSpan.FromSeconds(899), token);
		Assert.True(session.Api.World.Quests.ContainsKey(1146));
		await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
		await session.WaitForPacketAsync(typeof(SM_QUEST_ACTION), token,
			packet => packet.Get<int>("questId") == 1146 && packet.Get<byte>("action") == 3);
		Assert.False(session.Api.World.Quests.ContainsKey(1146));
		Assert.Equal(0, ItemCount(session.Api.World, 182200519));

		session.BeginStep("s11", "level-fourteen-and-start-escort");
		player.GetCommonData().SetLevel(14);
		await TeleportForSetupAsync(session, player, 210030000, 1254.72f, 2223.85f, 144.875f, token);
		Npc cannon = FindLivingNpc(player, 203145);
		await MoveBesideAsync(session, cannon, token);
		await StartQuestAsync(session, cannon.GetObjectId(), 1149, token);

		Npc poppy = FindLivingNpc(player, 203191);
		await MoveBesideAsync(session, poppy, token);
		session.BeginStep("s12", "escort-poppy-to-cannon");
		await session.SendPacketAsync(session.Api.TalkTo(poppy.GetObjectId()), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		await session.SendPacketAsync(session.Api.SelectDialog(poppy.GetObjectId(), DialogAction.QUEST_SELECT, questId: 1149), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
			packet => packet.Get<ushort>("dialogPageId") == 1352);
		await session.SendPacketAsync(session.Api.SelectDialog(poppy.GetObjectId(), DialogAction.SETPRO1, questId: 1149), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
			packet => packet.Get<ushort>("dialogPageId") == 0);
		player.GetPosition().SetXYZH(1254.72f, 2223.85f, 144.875f, 0);
		poppy.GetPosition().SetXYZH(1254.72f, 2223.85f, 144.875f, 0);
		await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
		QuestState escortState = player.GetQuestStateList().GetQuestState(1149);
		Assert.Equal(QuestStatus.REWARD, escortState.GetStatus());
		DecodedBotServerPacket escortMovie = await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
		Assert.Equal(12, escortMovie.Get<int>("cutsceneId"));
		await WaitForQuestStatusAsync(session, 1149, 4, token);
		policy.AssertClean();
	}
}
