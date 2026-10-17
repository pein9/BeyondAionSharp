using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Tests.Ai;

/// <summary>SIM boot slice for the Java-ordered housing cron singletons, built through the startup-singleton seam.</summary>
internal static class SimulationCronTaskInitialization
{
	// The production boot's own step (GameServer.java:120-122), so this slice runs exactly what StartAsync runs.
	public static void InitializeHousingTasks(ThreadPoolManager pool) =>
		GameServerBootstrapService.ConstructHousingCronTasks(pool);

	internal static void InitializeSequence(
		ThreadPoolManager pool,
		params (string Name, Func<object> Factory)[] tasks)
	{
		foreach ((string name, Func<object> factory) in tasks)
			pool.ConstructStartupSingleton(name, factory);
	}
}
