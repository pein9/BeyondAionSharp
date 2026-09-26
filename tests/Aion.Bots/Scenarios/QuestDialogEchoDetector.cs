using Aion.Bots.Protocol;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public sealed record PendingQuestDialogAction(int TargetObjectId, int ActionId, int QuestId);

public sealed class QuestDialogEchoException : InvalidOperationException
{
	public QuestDialogEchoException(PendingQuestDialogAction action)
		: base($"Quest {action.QuestId} control action {DialogAction.NameOf(action.ActionId) ?? "UNKNOWN"} " +
			$"({action.ActionId}) at object {action.TargetObjectId} was echoed as the dialog page; " +
			"DialogService did not find a handler that accepted the action.")
	{
		Action = action;
	}

	public PendingQuestDialogAction Action { get; }
}

/// <summary>
/// Correlates a quest-control CM_DIALOG_SELECT with DialogService's page=action fallback. SELECT page navigation
/// legitimately uses the same numeric value for action and next page, so it is deliberately excluded.
/// </summary>
public sealed class QuestDialogEchoDetector
{
	private PendingQuestDialogAction? pending;
	private bool expectRejection;
	private PendingQuestDialogAction? expectedRejection;

	public void BeginLoginObservation()
	{
		pending = null;
		expectRejection = false;
		expectedRejection = null;
	}

	public PendingQuestDialogAction? Pending => pending;
	public PendingQuestDialogAction? ExpectedRejection => expectedRejection;

	public void Record(int targetObjectId, int actionId, int questId, bool expectEcho = false)
	{
		pending = questId != 0 && IsQuestControlAction(actionId)
			? new PendingQuestDialogAction(targetObjectId, actionId, questId)
			: null;
		expectRejection = pending != null && expectEcho;
		expectedRejection = null;
	}

	public void Observe(DecodedBotServerPacket packet)
	{
		ArgumentNullException.ThrowIfNull(packet);
		if (packet.PacketType != typeof(SM_DIALOG_WINDOW))
			return;
		Observe(
			packet.Get<int>("targetObjectId"),
			packet.Get<ushort>("dialogPageId"),
			packet.Get<int>("questId"));
	}

	public void Observe(int targetObjectId, int pageId, int questId)
	{
		PendingQuestDialogAction? action = pending;
		if (action == null || action.TargetObjectId != targetObjectId || action.QuestId != questId)
			return;

		pending = null;
		bool wasExpected = expectRejection;
		expectRejection = false;
		if (pageId == action.ActionId)
		{
			if (wasExpected)
			{
				expectedRejection = action;
				return;
			}
			throw new QuestDialogEchoException(action);
		}
	}

	public PendingQuestDialogAction ConsumeExpectedRejection()
	{
		PendingQuestDialogAction action = expectedRejection
			?? throw new InvalidOperationException("No expected quest-control rejection was observed.");
		expectedRejection = null;
		return action;
	}

	public static bool IsPageNavigationAction(int actionId) => actionId is >= 1011 and <= 9999;

	public static bool IsQuestControlAction(int actionId)
	{
		if (IsPageNavigationAction(actionId))
			return false;
		return actionId is >= DialogAction.SELECTED_QUEST_REWARD1 and <= DialogAction.SELECTED_QUEST_NOREWARD
			or DialogAction.QUEST_SELECT
			or DialogAction.CHECK_USER_HAS_QUEST_ITEM
			or >= DialogAction.QUEST_ACCEPT_1 and <= DialogAction.SELECT_QUEST_REWARD
			or >= DialogAction.SETPRO1 and <= DialogAction.SET_SUCCEED;
	}
}
