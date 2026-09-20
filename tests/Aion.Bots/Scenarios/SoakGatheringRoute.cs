using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>Gathering must not leave its pair rendezvous on a one-way checked route.</summary>
public static class SoakGatheringRoute
{
	public static IReadOnlyList<BotPosition> FindReturnablePath(BotPosition start, BotPosition destination,
		BotPosition home, Func<BotPosition, BotPosition, IReadOnlyList<BotPosition>> findPath)
	{
		var outbound = findPath(start, destination);
		// Ground sampling can change Z. Probe from the actual planned endpoint, not the
		// shipped node's nominal position, and always return to the original pair hub.
		return outbound.Count != 0 && findPath(outbound[^1], home).Count != 0 ? outbound : [];
	}
}
