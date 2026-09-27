using Aion.Bots.Protocol;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios;

/// <summary>Client-visible NPC interaction order and reaction pacing from the recorded 4.8 client.</summary>
public static class NaturalDialogProtocol
{
	public static readonly TimeSpan TargetReaction = TimeSpan.FromMilliseconds(250);
	public static readonly TimeSpan PageReaction = TimeSpan.FromMilliseconds(750);

	public static async Task OpenAsync(INaturalJourneySession session, int targetObjectId, CancellationToken token)
	{
		await session.SendPacketAsync(GameClientPackets.Emotion((byte)EmotionType.SELECT_TARGET), token);
		await session.SendPacketAsync(session.Api.Target(targetObjectId), token);
		await session.AdvanceAsync(TargetReaction, token);
		await session.SendPacketAsync(session.Api.TalkTo(targetObjectId), token);
	}

	public static async Task SelectAsync(INaturalJourneySession session, BotClientPacket choice, CancellationToken token)
	{
		await session.AdvanceAsync(PageReaction, token);
		await session.SendPacketAsync(choice, token);
	}
}
