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
