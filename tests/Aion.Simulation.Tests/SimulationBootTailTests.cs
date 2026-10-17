using Aion.Commons.Logging;

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
	/// Today's state, before GameServer.java:118-122 is wired: HousingService is first built inside SpawnAll, by the
	/// first map's SpawnInstance, after that map's spawn line; HousingBidService and the three housing cron tasks are
	/// not built by the boot at all.
	/// </summary>
	[SkippableFact]
	public void HousingIsNotInitializedBeforeTheSpawns()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		IReadOnlyList<CapturedLogEntry> boot = BootLog();

		int housesLoaded = IndexOfHousesLoaded(boot);
		int firstSpawn = IndexOfFirstSpawn(boot);
		Assert.True(housesLoaded > firstSpawn,
			$"HousingService was built at boot entry {housesLoaded}, before the first spawn line at {firstSpawn}.");
		Assert.DoesNotContain(boot, IsBidsLoaded);
		foreach (string task in HousingCronTasks)
			Assert.DoesNotContain(boot, entry => entry.Category == task);
	}

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
