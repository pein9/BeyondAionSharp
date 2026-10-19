using System.Reflection;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Tests.Ai;

[Collection("GoldenDataManager")]
public sealed class NpcSkillCachePortTests
{
    private const int SkillId = 17424;

    [Fact]
    public void SharedTemplateCachesOneStableSortedListWithoutMutatingSourceOrExposingEntries()
    {
        using var harness = BossAiHarness.For().WithAi(typeof(AggressiveNpcAI)).Build();
        var templates = new List<NpcSkillTemplate>
        {
            new() { id = SkillId, lv = 1, prio = 0, chainId = 7 },
            new() { id = SkillId, lv = 2, prio = 5, chainId = 7 },
            new() { id = SkillId, lv = 3, prio = 5, chainId = 7 },
            new() { id = int.MaxValue, lv = 4 },
        };
        var data = Holder("801 802", templates);
        var list = data.GetOrCreateNpcSkillList(801);
        Assert.Same(list, data.GetOrCreateNpcSkillList(802));
        Parallel.For(0, 16, _ => Assert.Same(list, data.GetOrCreateNpcSkillList(801)));
        Assert.Equal(4, templates.Count); // Missing skills are skipped, never removed from shared XML data.
        Assert.Equal(3, list.Size());
        Assert.Equal(new[] { 5, 0 }, list.GetPriorities());
        Assert.Equal(new[] { 2, 3, 1 }, Enumerable.Range(0, list.Size()).Select(i => list.GetSkillOnPosition(i).GetSkillLevel()));
        var chain = list.GetChainSkillsSortedByPriority(new NpcSkillTemplateEntry(new() { id = SkillId, nextChainId = 7 }));
        Assert.Equal(new[] { 2, 3, 1 }, chain.Select(entry => entry.GetSkillLevel()));
        list.GetSkillsSortedByPriority(5).Clear();
        chain.Clear();
        Assert.Equal(3, list.Size());
    }

    [Fact]
    public void ReloadClearsTemplateCacheAndMissingListsShareTheEmptyValue()
    {
        using var harness = BossAiHarness.For().WithAi(typeof(AggressiveNpcAI)).Build();
        var data = Holder("803 804", new() { new() { id = SkillId, lv = 1 } });
        var old = data.GetOrCreateNpcSkillList(803);
        Assert.Same(data.GetOrCreateNpcSkillList(-1), data.GetOrCreateNpcSkillList(-2));
        Assert.True(data.GetOrCreateNpcSkillList(-1).IsEmpty());
        data.SetSkillData(new() { Holder("803 804", new() { new() { id = SkillId, lv = 9 } }) });
        var fresh = data.GetOrCreateNpcSkillList(803);
        Assert.NotSame(old, fresh);
        Assert.Same(fresh, data.GetOrCreateNpcSkillList(804));
        Assert.Equal(9, fresh.GetSkillOnPosition(0).GetSkillLevel());
        Assert.Equal(1, old.GetSkillOnPosition(0).GetSkillLevel());
    }

    [Fact]
    public void RetailDataLevelSelectionKeepsXmlOrderWhenAutomaticSkillsArePrioritySorted()
    {
        using var harness = BossAiHarness.For().WithAi(typeof(AggressiveNpcAI)).Build();
        var npc = harness.SpawnWithAi(201010, "aggressive");
        var data = Holder("201010", new()
        {
            new() { id = SkillId, lv = 1, prio = 0 },
            new() { id = SkillId, lv = 9, prio = 10 },
        });
        var slot = typeof(StaticData).GetProperty(nameof(StaticData.NpcSkillDataDh))!;
        var previous = harness.Data.StaticData.NpcSkillDataDh;
        try
        {
            slot.SetValue(harness.Data.StaticData, data);
            Assert.Equal(9, data.GetOrCreateNpcSkillList(201010).GetSkillOnPosition(0).GetSkillLevel());
            Assert.Equal(1, NpcSkillCasting.LevelOf(npc, SkillId));
        }
        finally
        {
            slot.SetValue(harness.Data.StaticData, previous);
        }
    }

    [Fact]
    public void ReadinessCooldownBelongsToEachNpcEvenWhenBothShareTheSameEntry()
    {
        using var harness = BossAiHarness.For().WithAi(typeof(AggressiveNpcAI)).Build();
        var first = harness.SpawnWithAi(201010, "aggressive");
        var second = harness.SpawnWithAi(201010, "aggressive");
        Assert.Same(first.GetSkillList(), second.GetSkillList());
        var entry = new NpcSkillTemplateEntry(new() { id = SkillId, lv = 1, prob = 100 });
        var ready = typeof(SkillAttackManager).GetMethod("IsReady", BindingFlags.Static | BindingFlags.NonPublic)!;
        first.SetSkillCoolDown(SkillId, SystemClock.CurrentMillis() + 10_000);
        Assert.False((bool)ready.Invoke(null, new object[] { first, entry })!);
        Assert.True((bool)ready.Invoke(null, new object[] { second, entry })!);
        first.SetSkillCoolDown(SkillId, SystemClock.CurrentMillis());
        Assert.True((bool)ready.Invoke(null, new object[] { first, entry })!); // Java's deadline comparison is strict >.
    }

    private static NpcSkillData Holder(string ids, List<NpcSkillTemplate> skills)
    {
        var data = new NpcSkillData { npcSkills = new() { new() { NpcIdsXml = ids, npcSkills = skills } } };
        data.AfterUnmarshal(null!);
        return data;
    }
}
