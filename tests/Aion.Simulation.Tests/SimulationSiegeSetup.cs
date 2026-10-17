using Aion.GameServer.Model.Siege;
using Aion.GameServer.Services;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.Simulation.Tests;

/// <summary>
/// Director setup for the data sweeps that need a siege location owned by one race, so that the NPCs of that race's
/// peace group stand in the world. It works whether or not the boot ran <c>SiegeService.InitSieges</c>
/// (<c>GameServer.java:141</c>), which places the owner's peace group of every location and starts the endless siege
/// of every standalone artifact.
/// </summary>
internal static class SimulationSiegeSetup
{
	/// <summary>
	/// Gives <paramref name="locationId"/> to <paramref name="race"/>. When the location holds siege NPCs or a siege,
	/// this is the GM capture (<c>//siege capture</c>, <c>SiegeService.CaptureSiege</c>), which also ends and restarts an
	/// artifact's siege, and the location is given back to its owner and legion the same way. Otherwise the race's peace
	/// group is spawned directly and despawned afterwards. The restore is registered before anything changes.
	/// </summary>
	internal static void GiveLocation(GameWorld world, int locationId, SiegeRace race, Action<Action> registerRestore,
		IDictionary<string, string> details, string detailKey)
	{
		SiegeService sieges = SiegeService.GetInstance();
		SiegeLocation location = sieges.GetSiegeLocation(locationId);
		Assert.NotNull(location);
		SiegeRace originalRace = location.GetRace();
		int originalLegionId = location.GetLegionId();
		if (world.GetLocalSiegeNpcs(locationId).Count != 0 || sieges.IsSiegeInProgress(locationId))
		{
			details[detailKey] = "capture";
			registerRestore(() => sieges.CaptureSiege(originalRace, originalLegionId, locationId));
			sieges.CaptureSiege(race, 0, locationId);
			return;
		}
		details[detailKey] = "spawn";
		registerRestore(() => { sieges.DeSpawnNpcs(locationId); location.SetRace(originalRace); });
		location.SetRace(race);
		sieges.SpawnNpcs(locationId, race, SiegeModType.PEACE);
	}
}
