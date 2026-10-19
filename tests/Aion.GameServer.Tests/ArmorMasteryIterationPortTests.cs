using System.Collections.Concurrent;
using System.Reflection;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class ArmorMasteryIterationPortTests
{
    [Theory]
    [InlineData(StatEnum.PHYSICAL_DEFENSE)]
    [InlineData(StatEnum.MAGICAL_DEFEND)]
    public void MasteryIterationSharesEachListsWriterLockAndRefreshesDirectAndProxiedFunctions(StatEnum lockedStat)
    {
        using var h = BossAiHarness.For().Build();
        var stats = h.SpawnPlayer().GetGameStats();
        var torso = new Item(100, new ItemTemplate { itemGroup = ItemGroup.PL_TORSO }, 1, true, (long)ItemSlot.TORSO);
        var pants = new Item(101, new ItemTemplate { itemGroup = ItemGroup.PL_PANTS }, 1, true, (long)ItemSlot.PANTS);
        var mismatched = new Item(102, new ItemTemplate { itemGroup = ItemGroup.CL_TORSO }, 1, true, (long)ItemSlot.TORSO);
        var functions = new List<StatArmorMasteryFunction>();
        var functionOwner = new FunctionOwner();
        foreach (var stat in new[] { StatEnum.PHYSICAL_DEFENSE, StatEnum.MAGICAL_DEFEND })
        {
            var direct = new StatArmorMasteryFunction(ItemSubType.PLATE, stat, 20, true, 10, new List<Item> { torso });
            var proxied = new StatArmorMasteryFunction(ItemSubType.PLATE, stat, 40, true, 20, new List<Item> { torso });
            stats.AddEffectOnly(null!, new[] { direct });
            stats.AddEffectOnly(functionOwner, new[] { proxied });
            functions.Add(direct); functions.Add(proxied);
            Assert.Contains(direct, stats.GetStatsSorted(stat));
            Assert.Contains(stats.GetStatsSorted(stat), f => f is StatFunctionProxy proxy && ReferenceEquals(proxy.GetProxiedFunction(), proxied));
        }
        Assert.Equal(new[] { 6, 12, 6, 12 }, functions.Select(f => f.GetValue()));
        var dictionary = (ConcurrentDictionary<StatEnum, List<IStatFunction>>)typeof(CreatureGameStats)
            .GetField("stats", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(stats)!;
        var statFunctions = dictionary[lockedStat];
        using var entered = new ManualResetEventSlim();
        using var finished = new ManualResetEventSlim();
        Exception? error = null;
        var worker = new Thread(() =>
        {
            entered.Set();
            try { stats.UpdateArmorMasteryStats(new List<Item> { pants, mismatched }); }
            catch (Exception e) { error = e; }
            finally { finished.Set(); }
        }) { IsBackground = true };
        var ordinary = new StatFunction(lockedStat, 7, true);
        try
        {
            lock (statFunctions)
            {
                worker.Start();
                Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "Updater thread did not start.");
                Assert.True(SpinWait.SpinUntil(() => finished.IsSet || (worker.ThreadState & ThreadState.WaitSleepJoin) != 0,
                    TimeSpan.FromSeconds(5)), "Updater neither blocked on the list nor completed.");
                Assert.False(finished.IsSet, "Armor mastery iteration bypassed the stat-function writer lock.");
                // The existing writer uses the same monitor. It may mutate the list before the updater proceeds.
                stats.AddEffectOnly(null!, new[] { ordinary });
            }
        }
        finally { Assert.True(worker.Join(TimeSpan.FromSeconds(5)), "Updater did not finish after releasing the list."); }
        Assert.Null(error);
        Assert.Equal(new[] { 5, 10, 5, 10 }, functions.Select(f => f.GetValue()));
        Assert.Equal(7, ordinary.GetValue());
        stats.UpdateArmorMasteryStats(new List<Item>());
        Assert.All(functions, f => Assert.Equal(0, f.GetValue()));
    }

    private sealed class FunctionOwner : IStatOwner { }
}
