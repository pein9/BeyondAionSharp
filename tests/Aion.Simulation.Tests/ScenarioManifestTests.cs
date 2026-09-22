using Aion.Bots.Scenarios;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed class ScenarioManifestTests
{
	public static TheoryData<string> SimScenarioIds
	{
		get
		{
			var data = new TheoryData<string>();
			foreach (ScenarioDefinition scenario in Load().For(ScenarioMode.Sim, ScenarioTier.Soak))
				data.Add(scenario.Id);
			return data;
		}
	}

	[Fact]
	public void CheckedInManifestIsTheSharedModeAndTierSource()
	{
		ScenarioManifest manifest = Load();
		Assert.Null(manifest.Get("C9").ExpectedFail);
		foreach (string id in new[] { "GEO-FEAR", "GEO-KNOCKBACK" })
		{
			Assert.Equal(ScenarioTier.Full, manifest.Get(id).Tier);
			Assert.Equal(2, manifest.Get(id).Bots);
			Assert.Null(manifest.Get(id).ExpectedFail);
		}
		ScenarioDefinition l0 = manifest.Get("L0");

		Assert.Equal([ScenarioMode.Sim, ScenarioMode.Live], l0.Modes);
		Assert.Equal(ScenarioTier.Fast, l0.Tier);
		Assert.Equal(ScenarioChannelNeeds.Dedicated, l0.ChannelNeeds);
		Assert.Equal(2, l0.Bots);
		Assert.Equal(["S0", "L0", "M1", "C1", "C2", "C3", "Q1", "Q2", "E1", "E3", "E6"],
			manifest.For(ScenarioMode.Sim, ScenarioTier.Fast).Select(scenario => scenario.Id));
		Assert.Equal([ScenarioMode.Sim], manifest.Get("Q5").Modes);
		Assert.True(manifest.Get("Q5").ResetEpoch);
		Assert.Equal(["B4", "B3", "B2F", "B2", "O1", "NI-01", "L0", "M1", "M6", "C1", "Q1", "Q2", "Q3", "Q4P", "Q4I", "E1", "E2", "E3", "E4", "E5", "E6", "E7", "CAPITAL", "S1", "S2", "S3", "S4", "S5", "S6", "S7", "G1", "G2", "G3", "G4", "G5", "G6", "E8", "E9", "E10", "E11", "L1", "L2", "L3", "L4", "L5", "L7", "connect", "canaries"],
			manifest.For(ScenarioMode.Live, ScenarioTier.Full).Select(scenario => scenario.Id));
	}

	[Theory]
	[MemberData(nameof(SimScenarioIds))]
	public void SimTheoryEnumerationComesFromTheManifest(string scenarioId)
	{
		ScenarioDefinition scenario = Load().Get(scenarioId);
		Assert.Contains(ScenarioMode.Sim, scenario.Modes);
	}

	[Fact]
	public void PassportDailyResetHasAnIsolatedHistoricalEpoch()
	{
		var scenario = Load().Get("L6");
		Assert.True(scenario.ResetEpoch); Assert.Equal([ScenarioMode.Sim], scenario.Modes);
		Assert.Equal(new DateTimeOffset(2020, 12, 16, 8, 58, 0, TimeSpan.Zero), SimulationWorldFixture.EpochForProcess("reset-L6"));
		Assert.Equal(new DateTimeOffset(2026, 9, 16, 8, 59, 0, TimeSpan.Zero), SimulationWorldFixture.EpochForProcess("shard-00"));
		Assert.Equal(SimulationWorldFixture.EpochForProcess("shard-00"), SimulationWorldFixture.EpochForProcess("reset-Q5"));
	}

	[Fact]
	public void PlayerCommandsHaveAnIsolatedSeasonalProfile()
	{
		var scenario = Load().Get("L8C");
		Assert.True(scenario.ResetEpoch); Assert.Equal([ScenarioMode.Sim], scenario.Modes);
		Assert.Equal(new DateTimeOffset(2026, 12, 16, 12, 0, 0, TimeSpan.Zero), SimulationWorldFixture.EpochForProcess("reset-L8C"));
		Assert.Equal(16, PlayerCommandScenario.Aliases.Count);
		Assert.Equal(PlayerCommandScenario.Aliases.Count, PlayerCommandScenario.Aliases.Distinct().Count());
		Assert.All(PlayerCommandScenario.EnabledCommands, alias => Assert.Contains(alias, PlayerCommandScenario.Aliases));
	}

	[Fact]
	public void SummerEventsUseTheirShippedPeriodInAnIsolatedProcess()
	{
		var scenario = Load().Get("L8");
		Assert.True(scenario.ResetEpoch); Assert.Equal([ScenarioMode.Sim], scenario.Modes);
		Assert.Equal(ScenarioChannelNeeds.Exclusive, scenario.ChannelNeeds);
		Assert.Equal(new DateTimeOffset(2026, 8, 9, 23, 50, 0, TimeSpan.Zero), SimulationWorldFixture.EpochForProcess("reset-L8"));
	}

	[Fact]
	public void GatherSweepIsFullOnlyAndDeclaresTheCompleteSourceInventory()
	{
		var scenario = Load().Get("SWEEP-GATHER");
		Assert.True(scenario.ResetEpoch); Assert.Equal([ScenarioMode.Sim], scenario.Modes);
		Assert.Equal(ScenarioTier.Full, scenario.Tier); Assert.Equal(ScenarioChannelNeeds.Exclusive, scenario.ChannelNeeds);
		Assert.Equal(ScenarioRace.Both, scenario.Race); Assert.Equal(2, scenario.Bots);
		var source = System.Xml.Linq.XDocument.Load(Path.Combine(RealStaticData.RepoRoot(), "game-server/data/static_data/gatherables/gatherable_templates.xml"));
		int[] ids = source.Root!.Elements("gatherable_template").Select(e => (int)e.Attribute("id")!).Order().ToArray();
		Assert.NotEmpty(ids);
		Assert.All(scenario.Consumes, r => Assert.Equal(ScenarioResourceKind.Gatherable, r.Kind));
		Assert.Equal(ids, scenario.Consumes.Select(r => r.Id).Order());
	}

	[Fact]
	public void CraftSweepIsFullOnlyAndIsolatedAcrossBothRaces()
	{
		var scenario = Load().Get("SWEEP-CRAFT");
		Assert.True(scenario.ResetEpoch); Assert.Equal([ScenarioMode.Sim], scenario.Modes);
		Assert.Equal(ScenarioTier.Full, scenario.Tier); Assert.Equal(ScenarioChannelNeeds.Exclusive, scenario.ChannelNeeds);
		Assert.Equal(ScenarioRace.Both, scenario.Race); Assert.Equal(2, scenario.Bots);
		Assert.Contains(ScenarioRequirement.Db, scenario.Requires);
		// Stations are reusable, recipes/materials are private player state; no shared consumable spawn.
		Assert.Empty(scenario.Consumes);
	}

	[Fact]
	public void BindSweepIsFullOnlyAndIsolatedAcrossBothRaces()
	{
		var scenario = Load().Get("SWEEP-BIND");
		Assert.True(scenario.ResetEpoch); Assert.Equal([ScenarioMode.Sim], scenario.Modes);
		Assert.Equal(ScenarioTier.Full, scenario.Tier); Assert.Equal(ScenarioChannelNeeds.Exclusive, scenario.ChannelNeeds);
		Assert.Equal(ScenarioRace.Both, scenario.Race); Assert.Equal(2, scenario.Bots);
		Assert.Contains(ScenarioRequirement.Db, scenario.Requires);
		Assert.Empty(scenario.Consumes); // Binding does not consume the shared obelisk.
	}

	[Fact]
	public void SkillSweepIsFullOnlyWithOrdinaryRaceSubjectsAndCompleteCaseInventory()
	{
		var scenario = Load().Get("SWEEP-SKILL");
		Assert.True(scenario.ResetEpoch); Assert.Equal([ScenarioMode.Sim], scenario.Modes);
		Assert.Equal(ScenarioTier.Full, scenario.Tier); Assert.Equal(ScenarioChannelNeeds.Exclusive, scenario.ChannelNeeds);
		Assert.Equal(ScenarioRace.Both, scenario.Race); Assert.Equal(4, scenario.Bots);
		Assert.Contains(ScenarioRequirement.Db, scenario.Requires);
		Assert.Equal([210119, 210365], scenario.Consumes.Select(c => c.Id).Order());
	}

	[Fact]
	public void TradeSweepIsFullOnlyAndIsolatedAcrossBothRaces()
	{
		var scenario = Load().Get("SWEEP-TRADE");
		Assert.True(scenario.ResetEpoch); Assert.Equal([ScenarioMode.Sim], scenario.Modes);
		Assert.Equal(ScenarioTier.Full, scenario.Tier); Assert.Equal(ScenarioChannelNeeds.Exclusive, scenario.ChannelNeeds);
		Assert.Equal(ScenarioRace.Both, scenario.Race); Assert.Equal(2, scenario.Bots);
		Assert.Contains(ScenarioRequirement.Db, scenario.Requires);
		Assert.Empty(scenario.Consumes); // Isolated process owns dynamic vendor state and stock.
	}

	[Fact]
	public void TeleportSweepIsFullOnlyAndIsolatedAcrossBothRaces()
	{
		var scenario = Load().Get("SWEEP-TELEPORT");
		Assert.True(scenario.ResetEpoch); Assert.Equal([ScenarioMode.Sim], scenario.Modes);
		Assert.Equal(ScenarioTier.Full, scenario.Tier); Assert.Equal(ScenarioChannelNeeds.Exclusive, scenario.ChannelNeeds);
		Assert.Equal(ScenarioRace.Both, scenario.Race); Assert.Equal(2, scenario.Bots);
		Assert.Contains(ScenarioRequirement.Db, scenario.Requires);
		Assert.Empty(scenario.Consumes); // Routes are reusable; setup ownership changes are restored per row.
	}

	[Fact]
	public async Task IsolationPlanAssignsChannelsProcessBoundariesAndRespawnDrain()
	{
		ScenarioDefinition first = Definition("A", ScenarioTier.Fast, ScenarioChannelNeeds.Dedicated, 210010000,
			[new ScenarioConsumedResource { Kind = ScenarioResourceKind.Npc, Id = 10 }]);
		ScenarioDefinition second = Definition("B", ScenarioTier.Full, ScenarioChannelNeeds.Dedicated, 210010000);
		ScenarioDefinition reset = Definition("C", ScenarioTier.Full, ScenarioChannelNeeds.Exclusive, 110010000,
			resetEpoch: true);
		ScenarioDefinition last = Definition("D", ScenarioTier.Full, ScenarioChannelNeeds.None, 210010000);
		IReadOnlyList<ScenarioExecution> plan = ScenarioIsolationPlanner.Plan(
			[first, second, reset, last],
			ScenarioMode.Sim,
			ScenarioTier.Full,
			shardCount: 1,
			resource => resource.Id == 10 ? TimeSpan.FromSeconds(15) : throw new InvalidOperationException());

		Assert.Equal(["A", "B", "C", "D"], plan.Select(execution => execution.Scenario.Id));
		Assert.Equal([1, 2], plan.Take(2).Select(execution => execution.Channel));
		Assert.Equal(TimeSpan.FromMilliseconds(15_001), plan[1].AdvanceBefore);
		Assert.True(plan[2].Exclusive);
		Assert.Equal("reset-C", plan[2].ProcessKey);
		Assert.NotEqual(plan[0].DatabaseShard, plan[2].DatabaseShard);
		Assert.NotEqual(plan[0].CacheDirectoryName, plan[2].CacheDirectoryName);

		await using var clock = new VirtualThreadPool(strict: true);
		var driver = new SimulationDriver(clock);
		bool respawned = false;
		clock.Schedule(_ =>
		{
			respawned = true;
			return ValueTask.CompletedTask;
		}, TimeSpan.FromSeconds(15));
		await driver.PrepareScenarioAsync(plan[1]);
		Assert.True(respawned);
		Assert.Equal(15_001, clock.NowMillis);
	}

	[Fact]
	public void FullTierShardsInFixedManifestOrder()
	{
		ScenarioDefinition[] scenarios = Enumerable.Range(1, 5)
			.Select(index => Definition($"F{index}", ScenarioTier.Full, ScenarioChannelNeeds.None, 210010000))
			.ToArray();
		IReadOnlyList<ScenarioExecution> plan = ScenarioIsolationPlanner.Plan(
			scenarios,
			ScenarioMode.Sim,
			ScenarioTier.Full,
			shardCount: 2,
			_ => TimeSpan.Zero);

		Assert.Equal(["shard-00", "shard-01", "shard-00", "shard-01", "shard-00"],
			plan.Select(execution => execution.ProcessKey));
		Assert.Equal([0, 1, 2, 3, 4], plan.Select(execution => execution.Order));

		ScenarioDefinition[] isolated = Enumerable.Range(1, 5)
			.Select(index => Definition($"D{index}", ScenarioTier.Full, ScenarioChannelNeeds.Dedicated, 210010000))
			.ToArray();
		IReadOnlyList<ScenarioExecution> isolatedPlan = ScenarioIsolationPlanner.Plan(
			isolated,
			ScenarioMode.Sim,
			ScenarioTier.Full,
			shardCount: 2,
			_ => TimeSpan.Zero);
		Assert.Equal([1, 1, 2, 2, 3], isolatedPlan.Select(execution => execution.Channel));
	}

	private static ScenarioManifest Load() => ScenarioManifest.Load(ScenarioManifest.FindDefaultPath());

	private static ScenarioDefinition Definition(
		string id,
		ScenarioTier tier,
		ScenarioChannelNeeds channelNeeds,
		int map,
		ScenarioConsumedResource[]? consumes = null,
		bool resetEpoch = false) => new()
	{
		Id = id,
		Modes = [ScenarioMode.Sim, ScenarioMode.Live],
		Tier = tier,
		Race = ScenarioRace.Elyos,
		Map = map,
		ChannelNeeds = channelNeeds,
		Bots = 1,
		VirtualDuration = TimeSpan.FromMinutes(1),
		Consumes = consumes ?? [],
		Requires = [ScenarioRequirement.Db],
		ExpectedFail = null,
		ResetEpoch = resetEpoch,
	};
}
