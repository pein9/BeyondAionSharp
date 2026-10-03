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
	public string? IdentityAltgardLegId { get; set; }
	public Action? AfterSynchronize { get; set; }
	public Func<BotPosition, BotPosition>? ResolveForcedLanding { get; set; }
	/// <summary>Sees every game packet after the world model has applied it, on the reading flow.</summary>
	public Action<DecodedBotServerPacket>? PacketObserved { get; set; }
	internal bool InGame => state == Aion.GameServer.Network.Aion.AionConnection.State.IN_GAME;
	internal bool HasGameConnection => transport != null;
	/// <summary>The client-estimated position while walking, else the last server-reported one; null before entry.</summary>
	internal BotPosition? ObservedPosition => currentPosition ?? api.World.Position;

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

	public Task ReloginExistingCharacterAsync(CancellationToken token) => ReloginExistingCharacterAsync(false, token);

	/// <summary>With <paramref name="verifySavedPosition"/>, the selection list must show where the last quit left
	/// the character: the client-visible persistence check when no admin oracle is available.</summary>
	public async Task ReloginExistingCharacterAsync(bool verifySavedPosition, CancellationToken token)
	{
		// Shared recovery owns retry limits and reentry waits. Drop the old transport before logging in.
		await CloseAsync(token);
		var list = await LoginCharacterListAsync(token);
		var character = list.Get<List<IReadOnlyDictionary<string, object?>>>("characters")
			.SingleOrDefault(entry => Get<int>(entry, "objectId") == characterId)
			?? throw new InvalidDataException("The retained natural character is missing; refusing to create a replacement.");
		if (Get<string>(character, "name") != characterName || Get<int>(character, "race") != (int)race ||
			Get<int>(character, "deletionTimeSeconds") != 0)
			throw new InvalidDataException("Retained natural character identity changed.");
		// A Priest, or the Cleric it became at Ascension (NA-07).
		NaturalJourneyIdentityRules.Classify(Get<int>(character, "playerClass"), Get<ushort>(character, "level"),
			Get<int>(character, "mapId"), IdentityAltgardLegId);
		if (verifySavedPosition)
			AssertPersistedPosition(Get<int>(character, "mapId"), Get<float>(character, "x"),
				Get<float>(character, "y"), Get<float>(character, "z"));
		SelectCharacter(characterId, characterName);
		quitExpected = false;
	}
}
