using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>The shipped half-health helpers, including Java's death-before-task boundary.</summary>
[Collection("GoldenDataManager")]
public sealed class HamerunSummonTests
{
	private static BossAiHarness NewHarness() => BossAiHarness.For(300200000).WithWorldSize(2048)
		.WithAi(typeof(SummonerAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();
	private static (Npc Boss, Player Player) AtHalfHealth(BossAiHarness harness)
	{
		Npc boss = harness.Spawn(216922, 400, 400, 145);
		Player player = harness.SpawnPlayer(404, 400, 145);
		harness.Engage(boss, player);
		BossAiHarness.SetExactPercent(boss, 49);
		boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);
		return (boss, player);
	}
	private static int[] Helpers(BossAiHarness harness) => harness.LiveNpcs()
		.Where(n => n.GetNpcId() is 282041 or 282042).Select(n => n.GetNpcId()).Order().ToArray();

	[Fact]
	public void NonlethalThresholdSpawnsBothHelpersAndDeathRemovesThem()
	{
		using BossAiHarness harness = NewHarness();
		var (boss, player) = AtHalfHealth(harness);
		harness.Clock.Advance(TimeSpan.FromMilliseconds(1));
		Assert.Equal(new[] { 282041, 282042 }, Helpers(harness));
		BossAiHarness.Kill(boss, player);
		Assert.Empty(Helpers(harness));
	}

	[Fact]
	public void DeathBeforeQueuedHelpersSuppressesTheirSpawn()
	{
		using BossAiHarness harness = NewHarness();
		var (boss, player) = AtHalfHealth(harness);
		BossAiHarness.Kill(boss, player);
		harness.Clock.Advance(TimeSpan.FromMilliseconds(1));
		Assert.Empty(Helpers(harness));
	}
}
