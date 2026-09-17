using Aion.Bots.World;

namespace Aion.Bots.Navigation;

/// <summary>Routes to the latest object positions observed from server packets.</summary>
public sealed class BotWorldNavigator(BotNavigationGraph graph)
{
	public IReadOnlyList<BotPosition> FindPathToNpc(BotWorldModel world, int objectId)
	{
		ArgumentNullException.ThrowIfNull(world);
		if (world.MapId is not int mapId || world.Position is not BotPosition start)
			throw new InvalidOperationException("The bot has not observed its map and position yet.");
		if (!world.Objects.TryGetValue(objectId, out var target) || target.Kind != BotKnownObjectKind.Npc)
			throw new InvalidOperationException($"Object {objectId} is not an observed NPC.");

		// Deliberately use the SM_NPC_INFO/SM_MOVE-derived object position. Spawn XML is only graph
		// topology: retail pattern AI can move an NPC away from its original spawn at any time.
		return graph.FindPath(mapId, start, target.Position);
	}
}
