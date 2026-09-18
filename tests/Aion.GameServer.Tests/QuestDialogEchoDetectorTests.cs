using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class QuestDialogEchoDetectorTests
{
	[Theory]
	[InlineData(DialogAction.SELECTED_QUEST_REWARD1)]
	[InlineData(DialogAction.SELECTED_QUEST_NOREWARD)]
	[InlineData(DialogAction.QUEST_SELECT)]
	[InlineData(DialogAction.CHECK_USER_HAS_QUEST_ITEM)]
	[InlineData(DialogAction.QUEST_ACCEPT_1)]
	[InlineData(DialogAction.SELECT_QUEST_REWARD)]
	[InlineData(DialogAction.SETPRO1)]
	[InlineData(DialogAction.SET_SUCCEED)]
	public void QuestControlActionsAreClassified(int actionId)
	{
		Assert.True(QuestDialogEchoDetector.IsQuestControlAction(actionId));
	}

	[Theory]
	[InlineData(1011)]
	[InlineData(4762)]
	[InlineData(9999)]
	public void PageNavigationActionsAreNotQuestControl(int actionId)
	{
		Assert.True(QuestDialogEchoDetector.IsPageNavigationAction(actionId));
		Assert.False(QuestDialogEchoDetector.IsQuestControlAction(actionId));
	}

	[Theory]
	[InlineData(DialogAction.QUEST_ACCEPT)]
	[InlineData(DialogAction.QUEST_REFUSE)]
	[InlineData(DialogAction.QUEST_ACCEPT_SIMPLE)]
	[InlineData(DialogAction.SETPRO_NEXT)]
	public void OtherQuestNamedActionsAreNotInTheFallbackContract(int actionId)
	{
		Assert.False(QuestDialogEchoDetector.IsQuestControlAction(actionId));
	}

	[Fact]
	public void BotApiFailsWhenQuestControlActionIsEchoed()
	{
		var api = new BotApi();
		api.SelectDialog(77, DialogAction.QUEST_SELECT, questId: 1101);

		QuestDialogEchoException error = Assert.Throws<QuestDialogEchoException>(() =>
			api.Observe(Dialog(77, DialogAction.QUEST_SELECT, 1101)));

		Assert.Equal(new PendingQuestDialogAction(77, DialogAction.QUEST_SELECT, 1101), error.Action);
		Assert.Contains("QUEST_SELECT (31)", error.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void NavigationEchoAndZeroQuestControlEchoAreAccepted()
	{
		var api = new BotApi();
		api.SelectDialog(77, 1011, questId: 1101);
		api.Observe(Dialog(77, 1011, 1101));

		api.SelectDialog(77, DialogAction.QUEST_SELECT, questId: 0);
		api.Observe(Dialog(77, DialogAction.QUEST_SELECT, 0));
	}

	[Fact]
	public void DifferentHandlerPageCompletesCorrelationWithoutFailure()
	{
		var detector = new QuestDialogEchoDetector();
		detector.Record(77, DialogAction.QUEST_SELECT, 1101);
		detector.Observe(77, 1011, 1101);

		Assert.Null(detector.Pending);
		detector.Observe(77, DialogAction.QUEST_SELECT, 1101);
	}

	[Fact]
	public void UnrelatedDialogDoesNotConsumePendingControlAction()
	{
		var detector = new QuestDialogEchoDetector();
		detector.Record(77, DialogAction.SETPRO1, 1101);
		detector.Observe(88, DialogAction.SETPRO1, 1101);

		Assert.NotNull(detector.Pending);
		Assert.Throws<QuestDialogEchoException>(() => detector.Observe(77, DialogAction.SETPRO1, 1101));
	}

	private static DecodedBotServerPacket Dialog(int targetObjectId, int pageId, int questId) => new(
		typeof(SM_DIALOG_WINDOW),
		new Dictionary<string, object?>
		{
			["targetObjectId"] = targetObjectId,
			["dialogPageId"] = checked((ushort)pageId),
			["questId"] = questId,
		});
}
