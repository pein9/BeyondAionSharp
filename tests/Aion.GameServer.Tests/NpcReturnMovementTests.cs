using Aion.GameServer.Ai;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class NpcReturnMovementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReturnHomeReplacesAnActivePursuitAfterItsTargetIsCleared(bool pursuing)
    {
        using var harness = BossAiHarness.For().Build();
        var npc = harness.Spawn(210365, 100, 100, 200);
        var target = harness.SpawnPlayer(120, 100, 200);
        long now = SystemClock.CurrentMillis();
        SystemClock.UseSource(() => now);
        harness.World.UpdatePosition(npc, 110, 100, 200, 0, false);
        var movement = npc.GetMoveController();
        if (pursuing)
        {
            npc.SetTarget(target);
            movement.MoveToTargetObject();
            movement.MoveToDestination();
        }

        // LoseAggro clears the target before ReturningEventHandler requests the return.
        npc.SetTarget(null);
        npc.GetAi().SetStateIfNot(AIState.RETURNING);
        movement.ReturnToLastStepOrSpawn();
        now += 60_000;
        movement.MoveToDestination();

        var spawn = npc.GetSpawn();
        Assert.NotNull(spawn);
        Assert.Equal(spawn.GetX(), npc.GetX());
        Assert.Equal(spawn.GetY(), npc.GetY());
        Assert.Equal(spawn.GetZ(), npc.GetZ());
        Assert.True(movement.IsReachedPoint());
    }
}
