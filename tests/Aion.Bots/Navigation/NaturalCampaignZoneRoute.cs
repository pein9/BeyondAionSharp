using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.World;

namespace Aion.Bots.Navigation;

/// <summary>A blocked quest-area approach gives moving patrols one observed 15 s hold before replanning.
/// The caller retains its existing stall and guard-clearing bounds.</summary>
public static class NaturalCampaignZoneRoute
{
	/// <summary>After the required area's normal wait/guard recovery, remembered danger is a preference.
	/// Every current hostile circle and terrain check still constrains this candidate.</summary>
	public static IReadOnlyList<BotPosition> FindMemoryPreferencePath(BotNavigationGeometry geometry,
		int map, BotPosition start, BotPosition destination, IReadOnlyList<BotNavigationHazard> liveHazards)
	{
		IReadOnlyList<BotPosition> route = geometry.FindJourneyPathAvoiding(map, start, destination, liveHazards);
		return BotNavigationGeometry.AvoidsHazards(start, route, liveHazards) ? route : [];
	}

	public static async Task<IReadOnlyList<BotPosition>> FindAsync(
		Func<Task<IReadOnlyList<BotPosition>>> findRoute,
		Func<Task<bool>> waitForPatrol, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		IReadOnlyList<BotPosition> route = await findRoute();
		if (route.Count != 0 || BotNavMeshRouter.LastOutcome != BotNavRouteOutcome.HazardRejected) return route;
		if (!await waitForPatrol()) return [];
		token.ThrowIfCancellationRequested();
		return await findRoute();
	}
}
