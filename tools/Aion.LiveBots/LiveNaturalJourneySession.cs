using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;

namespace Aion.LiveBots;

/// <summary>Real sockets and wall-clock pacing for the shared natural journey.</summary>
internal sealed partial class LiveBotSession : INaturalJourneySession
{
	private bool naturalJourney;
	public string CurrentStep => currentStep;
	public string CurrentAction => currentAction;
	public string? CombatTracePath => Path.Combine(options.OutputDirectory, "bots", $"{bot}.trace.jsonl");
	public Action? BeforeSend { get; set; }
	public Action? AfterSynchronize { get; set; }
	public Func<BotPosition, BotPosition>? ResolveForcedLanding { get; set; }

	public void EnableNaturalJourney() => naturalJourney = true;
	public void ClearPacketHistory() => packetHistory.Clear();
	public void TraceDiagnostic(string action, IReadOnlyDictionary<string, object?> fields) =>
		trace.WriteAction(currentStep, action, fields);
	void INaturalJourneySession.PublishDashboard(string status, bool force)
	{
		actionStatus = status;
		PublishDashboard();
	}

	Task<DecodedBotServerPacket> INaturalJourneySession.WaitForPacketAsync(Type type, CancellationToken token) =>
		WaitForGamePacketAsync(type, token);
	public Task<DecodedBotServerPacket> WaitForPacketAsync(Func<DecodedBotServerPacket, bool> predicate, CancellationToken token) =>
		WaitForAnyPacketAsync(predicate, token);
	public ValueTask AdvanceAsync(TimeSpan duration, CancellationToken token) => new(Task.Delay(duration, token));
	public Task AdvanceOfflineAsync(TimeSpan duration, CancellationToken token) => Task.Delay(duration, token);
	public Task CloseSelectionAsync(CancellationToken token) => CloseAsync(token);

	public void AcceptTeleportPosition()
	{
		BotPosition position = api.World.Position
			?? throw new InvalidOperationException("The teleport response did not provide a destination.");
		currentPosition = position;
		if (api.World.MapId is int mapId)
			expectedPosition = new PersistedPosition(mapId, position.X, position.Y, position.Z);
	}

	public async Task ReloginExistingCharacterAsync(CancellationToken token)
	{
		// Shared recovery owns retry limits and reentry waits. Drop the old transport before logging in.
		await CloseAsync(token);
		var list = await LoginCharacterListAsync(token);
		var character = list.Get<List<IReadOnlyDictionary<string, object?>>>("characters")
			.SingleOrDefault(entry => Get<int>(entry, "objectId") == characterId)
			?? throw new InvalidDataException("The retained natural character is missing; refusing to create a replacement.");
		if (Get<string>(character, "name") != characterName || Get<int>(character, "race") != (int)race ||
			Get<int>(character, "playerClass") != (int)PlayerClass.PRIEST || Get<int>(character, "deletionTimeSeconds") != 0)
			throw new InvalidDataException("Retained natural character identity changed.");
		SelectCharacter(characterId, characterName);
		quitExpected = false;
	}
}
