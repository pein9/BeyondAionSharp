using Aion.Commons.Database;
using Aion.Commons.Logging;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Configs.Schedule;
using Aion.GameServer.Custom.Pvpmap;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Siege;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Spawns.Siegespawns;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Cron;
using Aion.GameServer.Services.Siege;
using Aion.GameServer.Taskmanager.Tasks.Housing;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.Simulation.Tests;

/// <summary>
/// The tail of the production boot against the real database and data (P10-05, D7 approved 2026-10-17). The SIM
/// fixture boots through <c>GameServerBootstrapService.StartAsync</c>, the production code path, so these read what
/// that boot left behind; they replace the env-gated DB full-boot test that GameServerBootstrapTests had. Every test
/// of the collection shares the world, so nothing here changes it (the faction ratio test puts back the rows and the
/// counts it touches), and what the boot did is read from the log lines written before "Game-server bootstrap completed". Java's boot tail: <c>GameServer.java:118-122</c> (housing),
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

	/// <summary>
	/// GameServer.java:141, SiegeService.initSieges, after the spawns: each fortress, outpost and standalone artifact holds
	/// exactly the PEACE group of its owner, every NPC of it a spawned SiegeNpc. With the SIM's new database every owner is
	/// Balaur except the two Silentera outposts (2111 Elyos, 3111 Asmodian, as SiegeLocationData sets them), so Amundi
	/// (278097) stands in Siel's Eastern Fortress (1132) in Reshanta and the five captured Asmodian prisoners (253626) in
	/// Sulfur Fortress (1141).
	/// </summary>
	[SkippableFact]
	public void EverySiegeLocationHoldsItsOwnersPeaceGroup()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		SiegeService sieges = SiegeService.GetInstance();
		IEnumerable<SiegeLocation> owned = sieges.GetFortresses().Values.Cast<SiegeLocation>()
			.Concat(sieges.GetOutposts().Values).Concat(sieges.GetStandaloneArtifacts());
		int total = 0;
		foreach (SiegeLocation location in owned)
		{
			int id = location.GetLocationId();
			int expected = (DataManager.SPAWNS_DATA.GetSiegeSpawnsByLocId(id) ?? []).SelectMany(group => group.GetSpawnTemplates())
				.OfType<SiegeSpawnTemplate>().Count(spawn => spawn.GetSiegeRace() == location.GetRace() && spawn.GetSiegeModType() == SiegeModType.PEACE);
			ICollection<SiegeNpc> npcs = fixture.World.GetLocalSiegeNpcs(id);
			Assert.True(expected == npcs.Count, $"siege location {id} ({location.GetRace()}) holds {npcs.Count} siege NPCs, its PEACE group has {expected}");
			Assert.All(npcs, npc =>
			{
				Assert.True(npc.IsSpawned());
				Assert.Equal(location.GetWorldId(), npc.GetWorldId());
				Assert.Equal(location.GetRace(), npc.GetSpawn().GetSiegeRace());
				Assert.Equal(SiegeModType.PEACE, npc.GetSpawn().GetSiegeModType());
			});
			total += expected;
		}
		Assert.True(total > 0);

		Assert.Equal(SiegeRace.BALAUR, sieges.GetFortress(1132).GetRace());
		SiegeNpc amundi = Assert.Single(fixture.World.GetLocalSiegeNpcs(1132), npc => npc.GetNpcId() == 278097);
		Assert.Equal(400010000, amundi.GetWorldId());
		Assert.Equal(SiegeRace.BALAUR, sieges.GetFortress(1141).GetRace());
		Assert.Equal(5, fixture.World.GetLocalSiegeNpcs(1141).Count(npc => npc.GetNpcId() == 253626));
	}

	/// <summary>
	/// InitSieges schedules a SiegeStartRunnable for each siege time of config/schedule/siege_schedule.xml: the 21 fortress
	/// times at their preparation times and the agent fight's one time, 22 cron jobs, logged in the boot's body after the
	/// first spawn line.
	/// </summary>
	[SkippableFact]
	public void TheSiegeCalendarHasACronJobForEachSiegeTime()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		CronService cron = CronService.GetInstance();
		Assert.Equal(22, cron.FindJobs<SiegeStartRunnable>(withSubTypes: false).Count);
		var schedule = Aion.GameServer.Utils.Xml.JAXBUtil.Deserialize<SiegeSchedules>(new FileInfo(
			Path.Combine(Aion.GameServer.TestKit.RealStaticData.RepoRoot(), "game-server", "config", "schedule", "siege_schedule.xml")));
		var expected = schedule.GetFortresses().Cast<SiegeSchedules.SiegeSchedule>().Concat(schedule.GetAgentFights())
			.ToDictionary(entry => entry.GetId(), entry => entry.GetSiegeTimes().Count);
		var scheduled = cron.FindNextFireTimes<SiegeStartRunnable>(withSubTypes: false).Keys
			.GroupBy(runnable => runnable.GetLocationId()).ToDictionary(group => group.Key, group => group.Count());
		Assert.Equal(expected.OrderBy(pair => pair.Key), scheduled.OrderBy(pair => pair.Key));
		Assert.Equal(8011, SiegeService.GetInstance().GetAgentLocation().GetLocationId());
		Assert.Equal(1, scheduled[8011]);

		IReadOnlyList<CapturedLogEntry> boot = BootLog();
		int[] fortressLines = IndexesOf(boot, entry => entry.Category == "SIEGE_LOG"
			&& entry.Message.StartsWith("Scheduled siege of fortressID ", StringComparison.Ordinal));
		int[] agentLines = IndexesOf(boot, entry => entry.Category == "SIEGE_LOG"
			&& entry.Message.StartsWith("Scheduled agent fight based on cron expression: ", StringComparison.Ordinal));
		Assert.Equal(21, fortressLines.Length);
		Assert.Single(agentLines);
		Assert.True(fortressLines[0] > IndexOfFirstSpawn(boot), "the siege calendar was scheduled before the spawns");
	}

	/// <summary>
	/// InitSieges starts the siege of every standalone artifact, and an artifact's siege is endless: it has its boss, the
	/// one BOSS of the artifact's PEACE group, and no end is scheduled.
	/// </summary>
	[SkippableFact]
	public void EveryStandaloneArtifactIsInItsEndlessSiege()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		SiegeService sieges = SiegeService.GetInstance();
		List<ArtifactLocation> standalone = sieges.GetStandaloneArtifacts();
		Assert.NotEmpty(standalone);
		foreach (ArtifactLocation artifact in standalone)
		{
			int id = artifact.GetLocationId();
			Assert.True(sieges.IsSiegeInProgress(id), $"standalone artifact {id} is not in a siege");
			var siege = Assert.IsType<ArtifactSiege>(sieges.GetSiege(id));
			Assert.True(siege.IsStarted());
			Assert.False(siege.IsFinished());
			Assert.True(siege.IsEndless());
			SiegeNpc boss = siege.GetBoss();
			Assert.NotNull(boss);
			Assert.Contains(boss, fixture.World.GetLocalSiegeNpcs(id));
			Assert.Equal(AbyssNpcType.BOSS, boss.GetObjectTemplate().GetAbyssNpcType());
		}
	}

	/// <summary>
	/// InitSieges ends by setting the next state of every scheduled location from its earliest SiegeStartRunnable: vulnerable
	/// when that run comes at or before the next hourly status update, invulnerable otherwise. The hourly broadcast job does
	/// the same again on every hour, so the rule holds whenever this runs, for a fortress not under siege. (At the SIM
	/// epoch, 08:59 UTC on a Wednesday, no preparation falls within the hour and every scheduled fortress is invulnerable.)
	/// A location the data lacked would have stopped the boot with a NullReferenceException, as in Java.
	/// </summary>
	[SkippableFact]
	public void EveryScheduledFortressHasItsNextState()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		SiegeService sieges = SiegeService.GetInstance();
		var nextUpdate = (DateTimeOffset?)typeof(SiegeService)
			.GetField("nextStateUpdateTime", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
			.GetValue(sieges);
		Assert.NotNull(nextUpdate);
		Assert.InRange(sieges.GetSecondsUntilNextFortressState(), 0, 3600);
		var earliest = CronService.GetInstance().FindNextFireTimes<SiegeStartRunnable>(withSubTypes: true)
			.GroupBy(pair => pair.Key.GetLocationId()).ToDictionary(group => group.Key, group => group.Min(pair => pair.Value));
		Assert.Equal(15, earliest.Count);
		foreach ((int id, DateTimeOffset start) in earliest)
		{
			SiegeLocation location = sieges.GetSiegeLocation(id);
			Assert.NotNull(location);
			if (location is AgentLocation)
				continue; // AgentLocation.getNextState follows its vulnerability, not the stored state
			if (sieges.IsSiegeInProgress(id))
				continue; // its preparation has run since the last update, so its next run is a week later
			Assert.IsType<FortressLocation>(location);
			int expected = start > nextUpdate.Value ? SiegeLocation.STATE_INVULNERABLE : SiegeLocation.STATE_VULNERABLE;
			Assert.True(expected == location.GetNextState(), $"fortress {id}: next state {location.GetNextState()}, its next siege run is {start:O}, the next update {nextUpdate:O}");
		}
	}

	/// <summary>
	/// GameServer.java:173-176: the game clock starts, then PvpMapService.init makes the PvP map, an instance of 301220000
	/// held by a PvpMapHandler and never auto-destroyed, with the map's NPCs in it, then CustomInstanceService runs; the
	/// world container is marked ready after them. With gameserver.pvpmap.enable off, as shipped, nobody is on the map, and
	/// a player who asks to join is told it is disabled (L8C's ".pvp join" step, SimulationPlayerCommandTests).
	/// </summary>
	[SkippableFact]
	public void TheClockStartsAndThenThePvpMapIsMade()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		Assert.False(CustomConfig.PVP_MAP_ENABLED);
		WorldMapInstance pvpMap = Assert.Single(fixture.World.GetWorldMap(301220000), instance => instance.GetInstanceHandler() is PvpMapHandler);
		Assert.Null(pvpMap.GetEmptyInstanceTask());
		Assert.Contains(pvpMap, visible => visible is Npc);
		Assert.Empty(pvpMap.GetPlayersInside());
		Assert.Equal(0, PvpMapService.GetInstance().GetParticipantsSize());

		IReadOnlyList<CapturedLogEntry> boot = BootLog();
		int clock = SingleIndex(boot, entry => entry.Message.StartsWith("Game time started with an update interval of ", StringComparison.Ordinal), "\"GameTime started\"");
		int created = SingleIndex(boot, entry => entry.Category == "InstanceService"
			&& entry.Message.StartsWith("Created new instance: 301220000 [" + pvpMap.GetInstanceId().ToString(System.Globalization.CultureInfo.InvariantCulture) + "]", StringComparison.Ordinal),
			"PvP map \"Created new instance: 301220000\"");
		int ready = SingleIndex(boot, entry => entry.Message == "Initialized world container", "\"Initialized world container\"");
		Assert.True(clock < created && created < ready, $"boot order (clock started, PvP map made, world ready) was {clock}, {created}, {ready}");
	}

	private static int[] IndexesOf(IReadOnlyList<CapturedLogEntry> boot, Func<CapturedLogEntry, bool> match) =>
		boot.Select((entry, index) => (entry, index)).Where(pair => match(pair.entry)).Select(pair => pair.index).ToArray();

	/// <summary>
	/// GameServer.java:130-134 with gameserver.ratio.limitation.enable on. The shipped value is off, so the boot left the
	/// counts alone, and the boot's own block is run here: with the flag off it changes nothing, and with it on the
	/// Asmodian and Elyos counts are the distinct accounts with a character of the race at level 10 or above
	/// (gameserver.ratio.min.required.level). The characters are this test's own rows on accounts no session uses; the
	/// flag, the counts and the table are put back afterwards.
	/// </summary>
	[SkippableFact]
	public void FactionRatioCountsAreTheAccountsWithALevelTenCharacterWhenTheLimitIsOn()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		bool previousFlag = GSConfig.ENABLE_RATIO_LIMITATION;
		int previousAsmodians = Aion.GameServer.GameServer.GetCountFor(Race.ASMODIANS);
		int previousElyos = Aion.GameServer.GameServer.GetCountFor(Race.ELYOS);
		long levelTen = DataManager.PLAYER_EXPERIENCE_TABLE.GetStartExpForLevel(GSConfig.RATIO_MIN_REQUIRED_LEVEL);
		Assert.Equal(10, GSConfig.RATIO_MIN_REQUIRED_LEVEL);
		var inserted = new List<int>();
		try
		{
			GSConfig.ENABLE_RATIO_LIMITATION = false;
			Aion.GameServer.GameServer.SetCharacterCounts(asmodiansCount: -1, elyosCount: -1);
			GameServerBootstrapService.InitFactionRatioCounts();
			Assert.Equal(-1, Aion.GameServer.GameServer.GetCountFor(Race.ASMODIANS));
			Assert.Equal(-1, Aion.GameServer.GameServer.GetCountFor(Race.ELYOS));

			int asmodiansBefore = PlayerDAO.GetCharacterCountForRace(Race.ASMODIANS);
			int elyosBefore = PlayerDAO.GetCharacterCountForRace(Race.ELYOS);
			// Three Asmodian accounts at level 10 or above, one of them with two such characters, an Asmodian account whose
			// character is one point short of level 10, and two Elyos accounts.
			InsertRatioCharacter(inserted, RatioAccount + 1, Race.ASMODIANS, levelTen);
			InsertRatioCharacter(inserted, RatioAccount + 1, Race.ASMODIANS, levelTen * 3);
			InsertRatioCharacter(inserted, RatioAccount + 2, Race.ASMODIANS, levelTen + 1);
			InsertRatioCharacter(inserted, RatioAccount + 3, Race.ASMODIANS, levelTen * 2);
			InsertRatioCharacter(inserted, RatioAccount + 4, Race.ASMODIANS, levelTen - 1);
			InsertRatioCharacter(inserted, RatioAccount + 5, Race.ELYOS, levelTen);
			InsertRatioCharacter(inserted, RatioAccount + 6, Race.ELYOS, levelTen * 2);

			GSConfig.ENABLE_RATIO_LIMITATION = true;
			GameServerBootstrapService.InitFactionRatioCounts();

			Assert.Equal(asmodiansBefore + 3, Aion.GameServer.GameServer.GetCountFor(Race.ASMODIANS));
			Assert.Equal(elyosBefore + 2, Aion.GameServer.GameServer.GetCountFor(Race.ELYOS));
		}
		finally
		{
			GSConfig.ENABLE_RATIO_LIMITATION = previousFlag;
			Aion.GameServer.GameServer.SetCharacterCounts(previousAsmodians, previousElyos);
			if (inserted.Count > 0)
			{
				using var connection = DatabaseFactory.GetConnection();
				connection.Open();
				using var command = connection.CreateCommand();
				command.CommandText = "DELETE FROM players WHERE id IN (" + string.Join(",", inserted) + ")";
				command.ExecuteNonQuery();
			}
		}
	}

	// Far above the session accounts (1-255) and above any object id a SIM process reaches.
	private const int RatioAccount = 1_900_000_000;

	private static void InsertRatioCharacter(List<int> inserted, int accountId, Race race, long exp)
	{
		int id = RatioAccount + 100 + inserted.Count;
		using var connection = DatabaseFactory.GetConnection();
		connection.Open();
		using var command = connection.CreateCommand();
		command.CommandText = "INSERT INTO players (id, name, account_id, account_name, exp, x, y, z, heading, world_id, gender, race, player_class) "
			+ "VALUES (@id, @name, @account, @accountName, @exp, 0, 0, 0, 0, 220010000, 'MALE', @race, 'WARRIOR')";
		command.Parameters.AddWithValue("@id", id);
		command.Parameters.AddWithValue("@name", "Ratiocount" + inserted.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
		command.Parameters.AddWithValue("@account", accountId);
		command.Parameters.AddWithValue("@accountName", "sim-ratio-" + accountId.ToString(System.Globalization.CultureInfo.InvariantCulture));
		command.Parameters.AddWithValue("@exp", exp);
		command.Parameters.AddWithValue("@race", race.ToString());
		inserted.Add(id);
		Assert.Equal(1, command.ExecuteNonQuery());
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
