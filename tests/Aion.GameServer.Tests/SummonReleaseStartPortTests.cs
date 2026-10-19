using System.Reflection;
using System.Runtime.CompilerServices;
using Aion.GameServer.Ai.Follow;
using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Summons;
using Aion.GameServer.Services.Summons;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class SummonReleaseStartPortTests
{
    [Theory]
    [InlineData(UnsummonType.LOGOUT)]
    [InlineData(UnsummonType.DISTANCE)]
    [InlineData(UnsummonType.COMMAND)]
    [InlineData(UnsummonType.SUMMON_DEATH)]
    [InlineData(UnsummonType.MASTER_DEATH)]
    [InlineData(UnsummonType.UNSPECIFIED)]
    [InlineData(UnsummonType.SKILL_ORDER)]
    [InlineData(UnsummonType.PET_ORDER_UNSUMMON_EFFECT)]
    public void EveryReleaseCauseRunsTheHookAfterRegistrationAndBeforeTheModeChange(UnsummonType cause)
    {
        var summon = Bare<Summon>();
        var controller = new ReleaseProbe();
        controller.SetOwner(summon);
        SetField(typeof(VisibleObject), summon, "_controller", controller);
        SetField(typeof(Summon), summon, "mode", SummonMode.GUARD);
        Assert.Throws<ReleaseObserved>(() => SummonsService.Release(summon, cause));
        Assert.Equal(1, controller.Starts);
        Assert.Equal(1, controller.CastCancels);
        SummonsService.Release(summon, cause); // A release that has started is not registered again.
        Assert.Equal(1, controller.Starts);
        Assert.Equal(1, controller.CastCancels);
    }

    [Fact]
    public void FollowTickDoesNothingAfterDespawnWithoutConsultingTheMasterOrTarget()
    {
        var summon = Bare<Summon>();
        summon.SetPosition(new WorldPosition(1));
        var follow = Bare<FollowSummonTaskAI>();
        SetField(typeof(FollowSummonTaskAI), follow, "summon", summon);
        follow.Run();
        Assert.False(summon.IsBeingReleased());
    }

    [Fact]
    public void SiegeWeaponReleaseStartCancelsTheFollowTaskAndStopsMovement()
    {
        var common = new PlayerCommonData(7901);
        common.SetName("Releaseprobe");
        common.SetRace(Race.ELYOS);
        common.SetPlayerClass(PlayerClass.GLADIATOR);
        var master = new Player(new PlayerAccountData(common, new PlayerAppearance()), new Account(7901));
        var summon = Bare<Summon>();
        SetField(typeof(Summon), summon, "master", master);
        var controller = new SiegeWeaponController();
        controller.SetOwner(summon);
        var movement = new MovementProbe(summon);
        SetField(typeof(Creature), summon, "moveController", movement);
        using var cancellation = new CancellationTokenSource();
        var pending = new TaskCompletionSource();
        var task = (ScheduledTask)Activator.CreateInstance(typeof(ScheduledTask), BindingFlags.Instance | BindingFlags.NonPublic,
            null, new object[] { pending.Task, cancellation, DateTimeOffset.UtcNow }, null)!;
        master.GetController().AddTask(TaskId.SUMMON_FOLLOW, task);
        controller.OnReleaseStart();
        Assert.True(task.IsCancelled);
        Assert.Null(master.GetController().GetAndRemoveTask(TaskId.SUMMON_FOLLOW));
        Assert.Equal(1, movement.Aborts);
        pending.SetResult();
    }

    private sealed class ReleaseObserved : Exception;

    private sealed class ReleaseProbe : SummonController
    {
        public int Starts;
        public int CastCancels;
        public override void CancelCurrentSkill(Creature attacker) => CastCancels++;
        public override void OnReleaseStart()
        {
            Assert.True(GetOwner().IsBeingReleased());
            Assert.Equal(SummonMode.GUARD, GetOwner().GetMode());
            Assert.Equal(1, CastCancels);
            Starts++;
            ((SummonRelease)typeof(Summon).GetField("pendingRelease", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(GetOwner())!).MarkStarted();
            // Observe the lifecycle boundary before unrelated scheduling, persistence and packet paths.
            throw new ReleaseObserved();
        }
    }

    private sealed class MovementProbe(Summon owner) : SummonMoveController(owner)
    {
        public int Aborts;
        public override void AbortMove() => Aborts++;
    }

    private static T Bare<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    private static void SetField(Type type, object instance, string name, object value) =>
        type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(instance, value);
}
