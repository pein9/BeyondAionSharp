using Aion.Bots.Protocol;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

/// <summary>Java's prologue movie may already have been observed while completing the world-entry handshake.</summary>
public static class BotPrologueProtocol
{
	public static Task<DecodedBotServerPacket> WaitAsync(
		IReadOnlyList<DecodedBotServerPacket> history,
		int entryStart,
		Race race,
		Func<Func<DecodedBotServerPacket, bool>, CancellationToken, Task<DecodedBotServerPacket>> wait,
		CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		ArgumentOutOfRangeException.ThrowIfNegative(entryStart);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(entryStart, history.Count);
		(int quest, int movie) = race switch
		{
			Race.ELYOS => (1000, 1),
			Race.ASMODIANS => (2000, 2),
			_ => throw new ArgumentOutOfRangeException(nameof(race))
		};
		bool Matches(DecodedBotServerPacket packet) => packet.PacketType == typeof(SM_PLAY_MOVIE) &&
			packet.Get<int>("questId") == quest && packet.Get<int>("cutsceneId") == movie;
		DecodedBotServerPacket? observed = history.Skip(entryStart).FirstOrDefault(Matches);
		return observed != null ? Task.FromResult(observed) : wait(Matches, token);
	}
}
