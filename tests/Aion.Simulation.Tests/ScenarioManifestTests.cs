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
		ScenarioDefinition l0 = manifest.Get("L0");

		Assert.Equal([ScenarioMode.Sim, ScenarioMode.Live], l0.Modes);
		Assert.Equal(ScenarioTier.Fast, l0.Tier);
		Assert.Equal(ScenarioChannelNeeds.Dedicated, l0.ChannelNeeds);
		Assert.Equal(2, l0.Bots);
		Assert.Equal(["S0", "L0", "M1", "C1", "C2", "C3", "Q1", "Q2"],
			manifest.For(ScenarioMode.Sim, ScenarioTier.Fast).Select(scenario => scenario.Id));
		Assert.Equal(["L0", "M1", "M6", "C1", "Q1", "Q2", "Q3", "connect", "canaries"],
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
