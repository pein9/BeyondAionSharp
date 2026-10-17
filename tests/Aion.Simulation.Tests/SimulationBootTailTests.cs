using Aion.Commons.Logging;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Services.Cron;
using Aion.GameServer.Taskmanager.Tasks.Housing;
using Aion.GameServer.Utils;

namespace Aion.Simulation.Tests;

/// <summary>
/// The tail of the production boot against the real database and data (P10-05, D7 approved 2026-10-17). The SIM
/// fixture boots through <c>GameServerBootstrapService.StartAsync</c>, the production code path, so these read what
/// that boot left behind; they replace the env-gated DB full-boot test that GameServerBootstrapTests had. Every test
/// of the collection shares the world, so nothing here changes it, and what the boot did is read from the log lines
/// written before "Game-server bootstrap completed". Java's boot tail: <c>GameServer.java:118-122</c> (housing),
/// 130-134 (faction ratio), 141 (<c>SiegeService.initSieges</c>), 173-175 (<c>startClock</c>, <c>PvpMapService.init</c>).
/// </summary>
[Collection(SimulationWorldCollection.Name)]
public sealed class SimulationBootTailTests(SimulationWorldFixture fixture)
{
	[SkippableFact]
	public void ProductionBootCompletesWithAPopulatedWorld()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		Assert.True(fixture.Bootstrap.IsStarted);
		Assert.True(fixture.World.IsInitialized);
		Assert.True(fixture.World.ObjectCount > 0, $"world empty after the boot (ObjectCount={fixture.World.ObjectCount})");
	}

	/// <summary>
	/// GameServer.java:118-122, before the spawns: HousingService loads the houses and studios, HousingBidService the
	/// bids, then AuctionEndTask, AuctionAutoFillTask and MaintenanceTask each schedule their job in that order, all
	/// before SpawnEngine's first spawn line.
	/// </summary>
	[SkippableFact]
	public void HousingIsInitializedBeforeTheSpawnsInJavaOrder()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		IReadOnlyList<CapturedLogEntry> boot = BootLog();

		int housesLoaded = IndexOfHousesLoaded(boot);
		int bidsLoaded = SingleIndex(boot, IsBidsLoaded, "HousingBidService's \"Loaded bids for N houses\"");
		int[] scheduled = HousingCronTasks.Select(task => SingleIndex(boot, entry =>
			entry.Category == task && entry.Message.StartsWith("Scheduled " + task + " with cron expression: ", StringComparison.Ordinal),
			"\"Scheduled " + task + "\"")).ToArray();
		int firstSpawn = IndexOfFirstSpawn(boot);
		int[] order = [housesLoaded, bidsLoaded, .. scheduled, firstSpawn];
		Assert.True(order.SequenceEqual(order.Order()),
			$"boot order (houses, bids, {string.Join(", ", HousingCronTasks)}, first spawn) was {string.Join(", ", order)}");
	}

	[SkippableFact]
	public void EachHousingCronTaskHasOneCronJob()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		CronService cron = CronService.GetInstance();
		Assert.Single(cron.FindJobs<AuctionEndTask>(withSubTypes: false));
		Assert.Single(cron.FindJobs<AuctionAutoFillTask>(withSubTypes: false));
		Assert.Single(cron.FindJobs<MaintenanceTask>(withSubTypes: false));
	}

	/// <summary>
	/// AuctionEndTask's own clock against its cron job. The job fires in the cron service's zone (GSConfig.TIME_ZONE_ID,
	/// UTC in SIM), and scheduling put that zone on the shared expression, as Java's CronTriggerImpl.setTimeZone does, so
	/// the task's next-run arithmetic agrees with the job from then on. GetNextRun itself is computed before the job is
	/// scheduled (in the constructor, and by a run on start), in the expression's zone of that moment: the default one,
	/// TimeZoneInfo.Local here as the JVM's zone is in Java. On a host whose zone is not UTC it therefore differs from the
	/// job's next fire until the task first runs from its job, exactly as a Java server whose gameserver.timezone is not
	/// its JVM's zone would.
	/// </summary>
	[SkippableFact]
	public void AuctionEndTaskNextRunFollowsItsCronJob()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		AuctionEndTask task = AuctionEndTask.GetInstance();
		DateTimeOffset now = SystemClock.UtcNow();
		DateTimeOffset nextFire = Assert.Single(CronService.GetInstance().FindNextFireTimes<AuctionEndTask>(withSubTypes: false)).Value;

		Assert.Equal(NextIn(GSConfig.TIME_ZONE_ID, now), nextFire);
		Assert.Equal(nextFire, task.GetNextRunAfter(now));
		DateTimeOffset? lastRun = task.GetLastRun();
		if (lastRun is null || lastRun <= fixture.Epoch)
			Assert.Equal(NextIn(TimeZoneInfo.Local, lastRun ?? fixture.Epoch), task.GetNextRun());
		else
			Assert.Equal(nextFire, task.GetNextRun());
	}

	private static DateTimeOffset NextIn(TimeZoneInfo zone, DateTimeOffset after) =>
		new Quartz.CronExpression(HousingConfig.HOUSE_AUCTION_END_TIME.CronExpressionString) { TimeZone = zone }.GetTimeAfter(after)
		?? throw new InvalidOperationException("the auction end expression has no next time");

	private static readonly string[] HousingCronTasks = ["AuctionEndTask", "AuctionAutoFillTask", "MaintenanceTask"];

	// HousingBidService logs to Java's "HOUSE_AUCTION_LOG" category; its constructor ends with this line.
	private static bool IsBidsLoaded(CapturedLogEntry entry) =>
		entry.Category == "HOUSE_AUCTION_LOG" && entry.Message.StartsWith("Loaded bids for ", StringComparison.Ordinal);

	private IReadOnlyList<CapturedLogEntry> BootLog()
	{
		IReadOnlyList<CapturedLogEntry> entries = fixture.LogCapture.Entries;
		int completed = -1;
		for (int i = 0; i < entries.Count && completed < 0; i++)
		{
			if (entries[i].Message.StartsWith("Game-server bootstrap completed", StringComparison.Ordinal))
				completed = i;
		}
		Assert.True(completed >= 0, "the boot's completion line was not captured");
		return entries.Take(completed).ToArray();
	}

	private static int IndexOfHousesLoaded(IReadOnlyList<CapturedLogEntry> boot) => SingleIndex(boot, entry =>
		entry.Category == "HousingService"
		&& entry.Message.StartsWith("Loaded ", StringComparison.Ordinal)
		&& entry.Message.EndsWith(" studios", StringComparison.Ordinal), "HousingService's \"Loaded N houses and M studios\"");

	private static int IndexOfFirstSpawn(IReadOnlyList<CapturedLogEntry> boot)
	{
		for (int i = 0; i < boot.Count; i++)
		{
			if (boot[i].Category == "SpawnEngine" && boot[i].Message.StartsWith("Spawned ", StringComparison.Ordinal))
				return i;
		}
		Assert.Fail("SpawnEngine logged no \"Spawned N objects in ...\" line during the boot.");
		return -1;
	}

	private static int SingleIndex(IReadOnlyList<CapturedLogEntry> boot, Func<CapturedLogEntry, bool> match, string what)
	{
		int[] found = boot.Select((entry, index) => (entry, index)).Where(pair => match(pair.entry)).Select(pair => pair.index).ToArray();
		Assert.True(found.Length == 1, $"expected one {what} line during the boot, found {found.Length}");
		return found[0];
	}
}
