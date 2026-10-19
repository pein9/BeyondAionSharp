using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Services;

/// <summary>
/// Java parity: services/PeriodicSaveService (author ATracer).
/// Singleton whose ctor schedules the legion-warehouse save task and the server-runtime save task
/// at the PeriodicSaveConfig intervals via ThreadPoolManager.scheduleAtFixedRate.
/// </summary>
public class PeriodicSaveService
{
	private static readonly ILogger Log = AionLog.For(nameof(PeriodicSaveService));

	private readonly List<PeriodicSaveTask> _tasks;

	public static PeriodicSaveService GetInstance()
	{
		return SingletonHolder.Instance;
	}

	private PeriodicSaveService()
	{
		_tasks = new List<PeriodicSaveTask> { new LegionWarehouseSaveTask(), new ServerRunTimeSaveTask() };
	}

	/// <summary>
	/// Save data on shutdown
	/// </summary>
	public void OnShutdown()
	{
		foreach (PeriodicSaveTask task in _tasks)
		{
			task.StoreDataAndCancel();
		}

	}

	private sealed class LegionWarehouseSaveTask : PeriodicSaveTask
	{
		public LegionWarehouseSaveTask()
			: base(PeriodicSaveConfig.LEGION_ITEMS * 1000)
		{
		}

		public override void Run()
		{
			long startTime = System.Environment.TickCount64;
			int legionWhUpdated = 0;
			foreach (Legion legion in LegionService.GetInstance().GetCachedLegions())
			{
				List<Item> allItems = legion.GetLegionWarehouse().GetItemsWithKinah();
				allItems.AddRange(legion.GetLegionWarehouse().GetDeletedItems());
				try
				{
					// 1. save items first
					InventoryDAO.Store(allItems, null, null, legion.GetLegionId());
					// 2. save item stones
					ItemStoneListDAO.Save(allItems);
				}
				catch (Exception ex)
				{
					Log.LogError(ex, "Could not save warehouse of legion {LegionId}", legion.GetLegionId());
				}

				legionWhUpdated++;
			}

			long workTime = System.Environment.TickCount64 - startTime;
			Log.LogInformation("Saved warehouses of {Legions} legions in {ElapsedMs} ms", legionWhUpdated, workTime);
		}
	}

	private sealed class ServerRunTimeSaveTask : PeriodicSaveTask
	{
		public ServerRunTimeSaveTask()
			: base((long)TimeSpan.FromMinutes(2).TotalMilliseconds)
		{
		}

		public override void Run()
		{
			ServerVariablesDAO.Store("serverLastRun", SystemClock.CurrentMillis());
		}
	}

	private abstract class PeriodicSaveTask
	{
		private readonly ScheduledTask _future;

		protected PeriodicSaveTask(long periodMillis)
		{
			_future = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(
				_ => { Run(); return ValueTask.CompletedTask; },
				TimeSpan.FromMilliseconds(periodMillis),
				TimeSpan.FromMilliseconds(periodMillis));
		}

		public abstract void Run();

		public void StoreDataAndCancel()
		{
			_future.Cancel(false);
			Run();
		}
	}

	private static class SingletonHolder
	{
		internal static readonly PeriodicSaveService Instance = new PeriodicSaveService();
	}
}
