using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public static class NaturalCapitalSteps
{
	/// <summary>The shipped statue dialog, including discarding the old map view before departure.</summary>
	public static async Task PortalAsync(INaturalJourneySession session, NaturalCapitalPortal portal, int npc,
		CancellationToken token)
	{
		if (session.Api.World.MapId != portal.MapId || session.Api.World.Objects[npc].TemplateId != portal.NpcId)
			throw new InvalidDataException("Capital statue is on the wrong map or has the wrong identity.");
		await NaturalDialogProtocol.OpenAsync(session, npc, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, p => p.Get<int>("targetObjectId") == npc);
		session.Api.World.BeginWorldReload();
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc, portal.Action), token);
		await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token);
		await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, p => p.Get<int>("objectId") == session.CharacterId);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
		if (session.Api.World.MapId != portal.DestinationMapId)
			throw new InvalidDataException($"Statue {portal.NpcId} did not enter map {portal.DestinationMapId}.");
		session.TraceDiagnostic("capital-statue", new Dictionary<string, object?>
		{ ["npc"] = portal.NpcId, ["action"] = portal.Action, ["map"] = session.Api.World.MapId });
	}
}
