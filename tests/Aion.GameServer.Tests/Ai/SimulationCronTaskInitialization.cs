using Aion.GameServer.Taskmanager.Tasks.Housing;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Tests.Ai;

/// <summary>SIM boot slice for the Java-ordered housing cron singletons, built through the startup-singleton seam.</summary>
internal static class SimulationCronTaskInitialization
{
	public static void InitializeHousingTasks(ThreadPoolManager pool)
	{
		pool.ConstructStartupSingleton(nameof(AuctionEndTask), AuctionEndTask.GetInstance);
		pool.ConstructStartupSingleton(nameof(AuctionAutoFillTask), AuctionAutoFillTask.GetInstance);
		pool.ConstructStartupSingleton(nameof(MaintenanceTask), MaintenanceTask.GetInstance);
	}

	internal static void InitializeSequence(
		ThreadPoolManager pool,
		params (string Name, Func<object> Factory)[] tasks)
	{
		foreach ((string name, Func<object> factory) in tasks)
			pool.ConstructStartupSingleton(name, factory);
	}
}
