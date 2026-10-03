using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

/// <summary>Haramel's timed outdoor objects and separate movie-end transition, read from Java Q28500.</summary>
public static class NaturalHaramelQuestSteps
{
	public static async Task UseLeadInObjectAsync(INaturalJourneySession session, NaturalAltgardStep step,
		int objectId, CancellationToken token)
	{
		if (step.QuestId != 28500 || step.NpcId is not (730306 or 730307) ||
			NaturalAltgardQuestSteps.State(session.Api.World, 28500) is not (3, int before) || before != step.Var)
			throw new InvalidDataException("The Haramel lead-in object requires its observed START step.");
		int start = session.PacketHistory.Count;
		if (!await NaturalAltgardQuestSteps.UseObjectAsync(session, objectId, null, token))
			throw new InvalidDataException("The Haramel lead-in object's use bar was interrupted.");
		int page = step.Pages.Single();
		if (!session.PacketHistory.Skip(start).Any(p => p.PacketType == typeof(SM_DIALOG_WINDOW) &&
			p.Get<int>("targetObjectId") == objectId && p.Get<ushort>("dialogPageId") == page))
			throw new InvalidDataException($"The Haramel lead-in object did not offer page {page}.");
		foreach (string action in step.Actions.Skip(1))
			await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(objectId,
				checked((ushort)NaturalAscensionContract.DialogActionId(action)), questId: 28500), token);
		await session.SynchronizeAsync(token);
		if (NaturalAltgardQuestSteps.State(session.Api.World, 28500) is not (3 or 4, int after) || after != step.NextVar)
			throw new InvalidDataException("The real Haramel object dialogue did not advance its quest.");
		if (step.MovieId is int movie && !session.PacketHistory.Skip(start).Any(p =>
			p.PacketType == typeof(SM_PLAY_MOVIE) && p.Get<int>("cutsceneId") == movie))
			throw new InvalidDataException("The pile did not start its actual quest movie.");
		await session.SendPacketAsync(session.Api.CloseDialog(objectId), token);
	}

	public static async Task FinishLeadInMovieAsync(INaturalJourneySession session, CancellationToken token)
	{
		if (NaturalAltgardQuestSteps.State(session.Api.World, 28500) is not (3 or 4, 3))
			throw new InvalidDataException("The Haramel movie requires observed quest step 3.");
		await NaturalMovieGate.FinishAsync(session, token);
		await session.SynchronizeAsync(token);
		if (NaturalAltgardQuestSteps.State(session.Api.World, 28500) is not (4, 3))
			throw new InvalidDataException("Only the real movie-end event may put Q28500 at REWARD.");
	}
}
