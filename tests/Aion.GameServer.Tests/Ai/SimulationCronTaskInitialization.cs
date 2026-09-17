using Aion.GameServer.Taskmanager.Tasks.Housing;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests.Ai;

/// <summary>SIM boot slice for the Java-ordered housing cron singletons.</summary>
internal static class SimulationCronTaskInitialization
{
	public static async Task InitializeHousingTasksAsync(VirtualThreadPool pool, TimeSpan wallTimeTimeout)
	{
		await pool.InitializeAndDrainAsync(nameof(AuctionEndTask), AuctionEndTask.GetInstance, wallTimeTimeout);
		await pool.InitializeAndDrainAsync(nameof(AuctionAutoFillTask), AuctionAutoFillTask.GetInstance, wallTimeTimeout);
		await pool.InitializeAndDrainAsync(nameof(MaintenanceTask), MaintenanceTask.GetInstance, wallTimeTimeout);
	}

	internal static async Task InitializeSequenceAsync(
		VirtualThreadPool pool,
		TimeSpan wallTimeTimeout,
		params (string Name, Func<object> Factory)[] tasks)
	{
		foreach ((string name, Func<object> factory) in tasks)
			await pool.InitializeAndDrainAsync(name, factory, wallTimeTimeout);
	}
}
