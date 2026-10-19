using System.Collections;
using System.Reflection;
using Aion.GameServer.Ai;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class NpcDequePortTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static NpcSkillEntry Skill(int id) => new NpcSkillTemplateEntry(new QueuedNpcSkillTemplate(id, 1));

    [Fact]
    public void EmptySkillQueueStaysUnallocatedAndFirstEnqueueRetainsFifoAndIdentity()
    {
        using var h = BossAiHarness.For().Build();
        var npc = h.Spawn(210365, 100, 100, 200);
        var field = typeof(Npc).GetField("queuedSkills", PrivateInstance)!;
        var first = Skill(1); var second = Skill(1);
        Assert.Null(npc.GetNextQueuedSkill());
        Assert.False(npc.HasQueuedSkill(_ => throw new InvalidOperationException()));
        npc.RemoveNextQueuedSkill(first); npc.ClearQueuedSkills();
        Assert.Null(field.GetValue(npc));
        npc.QueueSkill(first); npc.QueueSkill(second);
        Assert.Same(first, npc.GetNextQueuedSkill());
        npc.RemoveNextQueuedSkill(second); Assert.Same(first, npc.GetNextQueuedSkill());
        npc.RemoveNextQueuedSkill(first); Assert.Same(second, npc.GetNextQueuedSkill());
        Assert.True(npc.HasQueuedSkill(s => ReferenceEquals(s, second)));
        npc.ClearQueuedSkills(); Assert.Null(npc.GetNextQueuedSkill());
        Assert.NotNull(field.GetValue(npc)); // clear retains allocated storage, like ArrayDeque.
    }

    [Fact]
    public void ArrayDequeNullRejectionDoesNotPoisonTheQueue()
    {
        using var h = BossAiHarness.For().Build();
        var npc = h.Spawn(210365, 100, 100, 200);
        Assert.Throws<ArgumentNullException>(() => npc.QueueSkill(null!));
        Assert.Null(npc.GetNextQueuedSkill());
        var skill = Skill(7); npc.QueueSkill(skill); Assert.Same(skill, npc.GetNextQueuedSkill());
    }

    [Fact]
    public void ConcurrentFirstEnqueuesNeverReplaceOrLoseEntries()
    {
        using var h = BossAiHarness.For().Build();
        var npc = h.Spawn(210365, 100, 100, 200);
        var skills = Enumerable.Range(1, 100).Select(Skill).ToArray();
        Parallel.ForEach(skills, skill => npc.QueueSkill(skill));
        var seen = new HashSet<NpcSkillEntry>();
        while (npc.GetNextQueuedSkill() is { } skill)
        {
            Assert.True(seen.Add(skill)); npc.RemoveNextQueuedSkill(skill);
        }
        Assert.Equal(skills.Length, seen.Count);
        Assert.All(skills, skill => Assert.Contains(skill, seen));
    }

    [Fact]
    public void BackStepHistoryKeepsTenSpacedPointsAndRemovesNewestFirst()
    {
        using var h = BossAiHarness.For().Build();
        var npc = h.Spawn(210365, 100, 100, 200);
        var movement = npc.GetMoveController();
        var store = typeof(NpcMoveController).GetMethod("TryStoreStep", PrivateInstance)!;
        var field = typeof(NpcMoveController).GetField("lastSteps", PrivateInstance)!;
        void Store(float x) => store.Invoke(movement, new object[] {x, 100f, 200f});
        float[] Steps() => ((IEnumerable)field.GetValue(movement)!).Cast<Point3D>().Select(p => p.GetX()).ToArray();
        Store(100); Store(105); Assert.Single(Steps());
        for (int i = 1; i < 12; i++) Store(100 + 11 * i);
        Assert.Equal(Enumerable.Range(2, 10).Select(i => 100f + 11 * i), Steps());
        npc.GetAi().SetStateIfNot(AIState.RETURNING);
        movement.ReturnToLastStepOrSpawn();
        Assert.Equal(Enumerable.Range(2, 9).Select(i => 100f + 11 * i), Steps());
        movement.ClearBackSteps(); Assert.Null(field.GetValue(movement));
    }

    [Fact]
    public void IdentifierHistoryDeduplicatesWithoutRefreshingAndEvictsOldestAfterThree()
    {
        var type = typeof(MultiClientService).GetNestedType("AccountSession", BindingFlags.NonPublic)!;
        var session = Activator.CreateInstance(type, 123)!;
        object? Invoke(string method, params object[] args) => type.GetMethod(method, PrivateInstance)!.Invoke(session, args);
        void Add(string suffix) => Invoke("PutIdentifiers", new IdentifierConnection("ip-" + suffix, "mac-" + suffix));
        bool Has(string suffix) => (bool)Invoke("HasAny", "ip-" + suffix, "unknown")!;
        var previous = SecurityConfig.MULTI_CLIENT_IGNORED_MAC_ADDRESSES;
        try
        {
            SecurityConfig.MULTI_CLIENT_IGNORED_MAC_ADDRESSES = new HashSet<string> {"mac-c"};
            Assert.False((bool)Invoke("IsIgnored")!);
            Add("a"); Add("b"); Add("c"); Add("a");
            Assert.True((bool)Invoke("IsIgnored")!); // duplicate a does not become newest.
            Add("d");
            Assert.False((bool)Invoke("IsIgnored")!);
            Assert.False(Has("a")); Assert.True(Has("b")); Assert.True(Has("c")); Assert.True(Has("d"));
            Assert.True((bool)Invoke("HasAny", "unknown", "mac-b")!);
        }
        finally { SecurityConfig.MULTI_CLIENT_IGNORED_MAC_ADDRESSES = previous; }
    }

    private sealed class IdentifierConnection : AionConnection
    {
        public IdentifierConnection(string ip, string mac) : base(ip) => SetMacAddress(mac);
    }
}
