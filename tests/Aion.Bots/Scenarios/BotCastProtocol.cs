using Aion.Bots.Protocol;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

/// <summary>
/// Java ce54b7931 PlayerController.cancelCurrentSkill sends cancellation instead of a cast result.
/// Read through one packet consumer, filtering by caster and skill; never race two socket readers.
/// Call completion after advancing/waiting the advertised cast duration in either SIM or LIVE.
/// </summary>
public static class BotCastProtocol
{
	public static readonly TimeSpan PacketTimeout = TimeSpan.FromSeconds(10);

	public static Task<DecodedBotServerPacket> WaitForStartAsync(
		Func<Func<DecodedBotServerPacket, bool>, CancellationToken, Task<DecodedBotServerPacket>> wait,
		int caster, ushort skill, CancellationToken token, TimeProvider? deadlines = null) =>
		WaitAsync(wait, packet => packet.PacketType == typeof(SM_CASTSPELL) &&
			packet.Get<int>("objectId") == caster && packet.Get<ushort>("spellId") == skill,
			"cast start", token, deadlines);

	public static bool IsStartRejection(DecodedBotServerPacket packet) =>
		packet.PacketType == typeof(SM_SYSTEM_MESSAGE) &&
		packet.Get<object>("name") is string name &&
		name is "STR_SKILL_CANT_CAST" or "STR_SKILL_NOT_READY" or
			"STR_SKILL_OBSTACLE" or "STR_SKILL_NOT_ENOUGH_DISTANCE" or
			// Java PlayerRestrictions.canUseSkill: stunned, knocked down or otherwise unable to act.
			"STR_SKILL_CAN_NOT_ATTACK_WHILE_IN_ABNORMAL_STATE";

	public static Task<DecodedBotServerPacket> WaitForCompletionAsync(
		Func<Func<DecodedBotServerPacket, bool>, CancellationToken, Task<DecodedBotServerPacket>> wait,
		int caster, ushort skill, CancellationToken token, TimeProvider? deadlines = null) =>
		WaitAsync(wait, packet =>
			(packet.PacketType == typeof(SM_CASTSPELL_RESULT) && packet.Get<int>("effectorId") == caster ||
			 packet.PacketType == typeof(SM_SKILL_CANCEL) && packet.Get<int>("objectId") == caster) &&
			packet.Get<ushort>("skillId") == skill, "cast result or cancellation", token, deadlines);

	public static TimeSpan RecoveryDelay(DecodedBotServerPacket terminal) => terminal.PacketType == typeof(SM_SKILL_CANCEL)
		? TimeSpan.FromSeconds(2) // Keep the same conservative retry cadence; never retry in a tight loop.
		: terminal.PacketType == typeof(SM_CASTSPELL_RESULT)
			? TimeSpan.FromMilliseconds(Math.Max(2000, terminal.Get<ushort>("hitTime") + 1))
			: throw new ArgumentException("Expected cast result or cancellation.", nameof(terminal));

	private static async Task<DecodedBotServerPacket> WaitAsync(
		Func<Func<DecodedBotServerPacket, bool>, CancellationToken, Task<DecodedBotServerPacket>> wait,
		Func<DecodedBotServerPacket, bool> predicate, string expected, CancellationToken token, TimeProvider? deadlines)
	{
		using var deadline = new CancellationTokenSource(PacketTimeout, deadlines ?? TimeProvider.System);
		using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
		try { return await wait(predicate, linked.Token); }
		catch (OperationCanceledException exception) when (deadline.IsCancellationRequested && !token.IsCancellationRequested)
		{
			// A timed-out read ends the scenario; it must not be retried on a still-pending socket reader.
			throw new TimeoutException($"No matching {expected} within {PacketTimeout.TotalSeconds} seconds.", exception);
		}
	}
}
