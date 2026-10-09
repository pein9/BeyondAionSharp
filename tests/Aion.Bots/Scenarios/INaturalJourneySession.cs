using Aion.Bots.Api;
using Aion.Bots.Movement;
using Aion.Bots.Protocol;
using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>Player protocol and clock boundary for the same SIM/LIVE journey policy.
/// No world lookup, administrative mutation or server actor is available here.</summary>
public interface INaturalJourneySession
{
	BotApi Api { get; }
	int CharacterId { get; }
	int ConnectionGeneration { get; }
	/// <summary>Explicit journey scope for retained-character map validation; ordinary sessions leave it unset.</summary>
	string? IdentityAltgardLegId { get; set; }
	BotPosition CurrentPosition { get; }
	string CurrentStep { get; }
	string CurrentAction { get; }
	string? CombatTracePath { get; }
	IReadOnlyList<DecodedBotServerPacket> PacketHistory { get; }
	Action? BeforeSend { get; set; }
	Action? AfterSynchronize { get; set; }
	Func<BotPosition, BotPosition>? ResolveForcedLanding { get; set; }
	void BeginStep(string step, string action);
	void TraceDiagnostic(string action, IReadOnlyDictionary<string, object?> fields);
	void PublishDashboard(string status = "running", bool force = false);
	void ClearPacketHistory();
	void AcceptTeleportPosition();
	Task SendPacketAsync(BotClientPacket packet, CancellationToken token);
	Task<DecodedBotServerPacket> WaitForPacketAsync(Type type, CancellationToken token);
	Task<DecodedBotServerPacket> WaitForPacketAsync(Type type, CancellationToken token, Func<DecodedBotServerPacket, bool> predicate);
	Task<DecodedBotServerPacket> WaitForPacketAsync(Func<DecodedBotServerPacket, bool> predicate, CancellationToken token);

	/// <summary>
	/// Waits for a packet that may never come, for at most <paramref name="realTime"/> of wall-clock time. Null when it
	/// did not come.
	/// <para>
	/// NR-46b: a session that knows nothing more can arrive answers at once. The simulated session does: its packets
	/// come only when it sends or lets game time pass, so once its queue is empty a wait can only sit out its limit.
	/// This default keeps the limit, for a session on a real connection.
	/// </para>
	/// </summary>
	async Task<DecodedBotServerPacket?> WaitForPacketWithinAsync(Type type, TimeSpan realTime, CancellationToken token,
		Func<DecodedBotServerPacket, bool> predicate)
	{
		using var limit = CancellationTokenSource.CreateLinkedTokenSource(token);
		limit.CancelAfter(realTime);
		try { return await WaitForPacketAsync(type, limit.Token, predicate); }
		catch (OperationCanceledException) when (!token.IsCancellationRequested) { return null; }
	}
	Task<int> WaitForNpcAsync(int templateId, CancellationToken token);
	ValueTask AdvanceAsync(TimeSpan duration, CancellationToken token);
	Task AdvanceOfflineAsync(TimeSpan duration, CancellationToken token);
	Task ExecuteMovementAsync(BotMovementPlan plan, CancellationToken token);
	Task SynchronizeAsync(CancellationToken token);
	Task StartQuestAsync(int npcId, int questId, CancellationToken token);
	Task FinishQuestAsync(int npcId, int questId, CancellationToken token);
	Task CloseSelectionAsync(CancellationToken token);
	Task WaitForReentryAsync(CancellationToken token);
	Task ReloginExistingCharacterAsync(CancellationToken token);
	Task EnterWorldAsync(CancellationToken token);
	Task QuitAsync(CancellationToken token);
}

public sealed class NaturalDialogTooFarException() : InvalidOperationException("NPC dialogue was refused: too far to talk.");
