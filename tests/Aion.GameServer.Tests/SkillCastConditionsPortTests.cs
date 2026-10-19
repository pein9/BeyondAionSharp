using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Aion.GameServer.Controllers;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.SkillEngine.Properties;
using Aion.GameServer.Tests.Ai;
using SkillProperties = Aion.GameServer.SkillEngine.Properties.Properties;
using PlayerGroupMember = Aion.GameServer.Model.Team.Group.PlayerGroupMember;

namespace Aion.GameServer.Tests;

/// <summary>Java 223b56a0d: finish-time targeting and owner/team spawn protection.</summary>
[Collection("GoldenDataManager")]
public sealed class SkillCastConditionsPortTests
{
    [Theory]
    [InlineData("ALL", true)]
    [InlineData("ENEMY", true)]
    [InlineData("FRIEND", false)]
    [InlineData("MYPARTY", false)]
    [InlineData("NONE", false)]
    [InlineData(null, false)]
    public void DuelPartnersAreValidForAllAndEnemySkillsButExcludedFromFriendOnlySkills(string? relation, bool expected)
    {
        // Java 558677569: use the shipped Shield of Compassion template for the affected ALL case.
        using var harness = Harness();
        var caster = harness.SpawnPlayer();
        var target = harness.SpawnPlayer();
        var connection = Attach(caster);
        var skill = relation == "ALL"
            ? new Skill(DataManager.SKILL_DATA.GetSkillTemplate(276), caster, 1, target, null!)
            : NewSkill(caster, target);
        var properties = Assert.IsType<SkillProperties>(skill.GetSkillTemplate().GetProperties());
        if (relation != "ALL") properties.TargetRelationRaw = relation;
        Assert.Equal(relation, properties.GetTargetRelation()?.ToString());
        var duels = (ConcurrentDictionary<int, int>)typeof(DuelService)
            .GetField("duels", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(DuelService.GetInstance())!;
        Assert.True(duels.TryAdd(caster.ObjectId, target.ObjectId));
        Assert.True(duels.TryAdd(target.ObjectId, caster.ObjectId));
        connection.Sent.Clear();
        try
        {
            bool valid = (bool)typeof(Skill).GetMethod("IsValidTarget", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(skill, new object[] { caster, target })!;
            Assert.Equal(expected, valid);
            if (expected)
                Assert.Empty(connection.Sent.OfType<SM_SYSTEM_MESSAGE>());
            else
                Assert.Contains(connection.Sent.OfType<SM_SYSTEM_MESSAGE>(), message =>
                    MessageId(message) == MessageId(SM_SYSTEM_MESSAGE.STR_SKILL_TARGET_IS_NOT_VALID()));
            target.SetVisualState(CreatureVisualState.BLINKING);
            Assert.False((bool)typeof(Skill).GetMethod("IsValidTarget", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(skill, new object[] { caster, target })!);
        }
        finally
        {
            duels.TryRemove(caster.ObjectId, out _);
            duels.TryRemove(target.ObjectId, out _);
        }
    }

    [Theory]
    [InlineData("dead")]
    [InlineData("hidden")]
    [InlineData("protected")]
    [InlineData("despawned")]
    public void AStartedPlayerCastWaitsThenFailsWithTargetLost(string change)
    {
        using var harness = Harness();
        var caster = harness.SpawnPlayer();
        var target = harness.SpawnPlayer();
        var connection = Attach(caster);
        var skill = NewSkill(caster, target);
        caster.SetTarget(target);
        caster.SetCasting(skill);
        Invoke(skill, "StartCast");
        connection.Sent.Clear();

        if (change == "protected")
            target.GetController().StartProtectionActiveTask();
        else if (change == "hidden")
            new HideEffect { state = CreatureVisualState.HIDE20 }
                .StartEffect(new Effect(target, target, new SkillTemplate { skillId = 1 }, 1));
        else
            ChangeTarget(target, change);
        if (change == "dead")
            target.GetObserveController().NotifyDeathObservers(caster);
        Assert.Same(skill, caster.GetCastingSkill());
        Invoke(skill, "EndCast");

        Assert.Null(caster.GetCastingSkill());
        Assert.Single(connection.Sent.OfType<SM_SKILL_CANCEL>());
        Assert.Contains(connection.Sent.OfType<SM_SYSTEM_MESSAGE>(), message =>
            MessageId(message) == MessageId(SM_SYSTEM_MESSAGE.STR_SKILL_TARGET_LOST()));
    }

    [Theory]
    [InlineData("dead")]
    [InlineData("hidden")]
    [InlineData("protected")]
    [InlineData("despawned")]
    public void PlayerAreaValidationDropsOnlyTheUnusableFirstTarget(string change)
    {
        using var harness = Harness();
        var caster = harness.SpawnPlayer();
        var target = harness.SpawnPlayer();
        var other = harness.SpawnPlayer();
        var skill = NewSkill(caster, target, "AREA");
        skill.GetEffectedList().AddRange(new[] { target, other });
        ChangeTarget(target, change);

        Assert.False((bool)Invoke(skill, "CancelOnUnusableFirstTarget")!);
        Assert.True((bool)Invoke(skill, "ValidateEffectedList")!);
        Assert.Equal(new[] { other }, skill.GetEffectedList());
    }

    [Theory]
    [InlineData("dead")]
    [InlineData("hidden")]
    [InlineData("protected")]
    public void NonPlayerCastStillGoesOffWithoutItsUnusableFirstTarget(string change)
    {
        using var harness = Harness();
        var caster = harness.SpawnWithAi(217182, "general");
        var target = harness.SpawnPlayer();
        var skill = NewSkill(caster, target);
        skill.GetEffectedList().Add(target);
        ChangeTarget(target, change);
        Assert.True((bool)Invoke(skill, "ValidateEffectedList")!);
        Assert.Empty(skill.GetEffectedList());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NonPlayerCastEndsQuietlyAndDropsATargetThatDespawnedOrTeleported(bool teleported)
    {
        using var harness = Harness();
        var caster = harness.SpawnWithAi(217182, "general");
        var target = harness.SpawnPlayer(x: teleported ? 900 : 985);
        var witness = Attach(harness.SpawnPlayer());
        var skill = NewSkill(caster, target);
        caster.SetTarget(target);
        caster.SetCasting(skill);
        if (!teleported)
            target.GetPosition().SetIsSpawned(false);
        witness.Sent.Clear();
        Assert.False((bool)Invoke(skill, "ValidateEffectedList")!);
        Invoke(skill, "EndCast");
        Assert.Null(caster.GetCastingSkill());
        Assert.Null(caster.GetTarget());
        Assert.Empty(witness.Sent.OfType<SM_SKILL_CANCEL>());
    }

    [Fact]
    public void GeneralNpcKeepsAStartedCastWhenTheTargetLeavesSight()
    {
        using var harness = Harness();
        var caster = harness.SpawnWithAi(217182, "general");
        var target = harness.SpawnPlayer();
        var skill = NewSkill(caster, target);
        caster.SetTarget(target);
        caster.SetCasting(skill);
        typeof(GeneralNpcAI).GetMethod("HandleCreatureNotSee", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(caster.GetAi(), new object[] { target });
        Assert.Same(skill, caster.GetCastingSkill());
    }

    [Fact]
    public void ChargeReleaseChecksAnUnusableTargetBeforeNormalValidation()
    {
        using var harness = Harness();
        var caster = harness.SpawnPlayer();
        var target = harness.SpawnPlayer();
        var connection = Attach(caster);
        var start = NewSkill(caster, target);
        caster.SetCasting(start);
        ChangeTarget(target, "hidden");
        var charge = new ChargeSkill(start.GetSkillTemplate(), caster, 1, 0, start);
        Assert.False(charge.UseSkill());
        Assert.Null(caster.GetCastingSkill());
        Assert.Contains(connection.Sent.OfType<SM_SYSTEM_MESSAGE>(), message =>
            MessageId(message) == MessageId(SM_SYSTEM_MESSAGE.STR_SKILL_TARGET_LOST()));
    }

    [Fact]
    public void ProtectionAllowsTheOwnerAndTeamButExcludesDuelingTeammatesAndOtherCreatures()
    {
        using var harness = Harness();
        var owner = harness.SpawnPlayer();
        var teammate = harness.SpawnPlayer();
        var outsider = harness.SpawnPlayer();
        Group(owner, teammate);
        var summon = (Summon)RuntimeHelpers.GetUninitializedObject(typeof(Summon));
        typeof(Summon).GetField("master", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(summon, owner);
        summon.SetVisualState(CreatureVisualState.BLINKING);
        owner.SetVisualState(CreatureVisualState.BLINKING);
        Assert.False(owner.IsSpawnProtectedFrom(owner));
        Assert.False(owner.IsSpawnProtectedFrom(teammate));
        Assert.False(summon.IsSpawnProtectedFrom(owner));
        Assert.False(summon.IsSpawnProtectedFrom(teammate));
        Assert.True(summon.IsSpawnProtectedFrom(outsider));
        Assert.True(owner.IsSpawnProtectedFrom(outsider));

        var duels = (ConcurrentDictionary<int, int>)typeof(DuelService)
            .GetField("duels", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(DuelService.GetInstance())!;
        duels[teammate.ObjectId] = owner.ObjectId;
        duels[owner.ObjectId] = teammate.ObjectId;
        try
        {
            Assert.True(owner.IsSpawnProtectedFrom(teammate));
            Assert.True(summon.IsSpawnProtectedFrom(teammate));
        }
        finally
        {
            duels.TryRemove(teammate.ObjectId, out _);
            duels.TryRemove(owner.ObjectId, out _);
        }

        var npc = harness.SpawnWithAi(217182, "general");
        npc.SetVisualState(CreatureVisualState.BLINKING);
        Assert.True(npc.IsProtectionActive());
        Assert.True(npc.IsSpawnProtectedFrom(owner));
        Assert.False(npc.IsSpawnProtectedFrom(npc));
    }

    [Fact]
    public void PartySelectionIncludesProtectedTeammates()
    {
        using var harness = Harness();
        var caster = harness.SpawnPlayer();
        var teammate = harness.SpawnPlayer();
        Attach(caster);
        Attach(teammate);
        Group(caster, teammate);
        teammate.SetVisualState(CreatureVisualState.BLINKING);
        var skill = NewSkill(caster, caster, "PARTY");
        skill.GetSkillTemplate().properties!.effectiveRange = 25;
        var targets = new List<Creature>();
        var result = new SkillProperties.ValidationResult(targets, caster);
        Assert.True(TargetRangeProperty.Set(skill.GetSkillTemplate().properties!, result, caster, skill.GetSkillTemplate(), 0, 0, 0));
        Assert.Contains(teammate, targets);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void FirstTargetLifeStateMatchesWhetherTheSkillResurrects(bool resurrect, bool dead, bool expected)
    {
        using var harness = Harness();
        var caster = harness.SpawnPlayer();
        var target = harness.SpawnPlayer();
        var skill = NewSkill(caster, target);
        if (resurrect)
        {
            var effects = new Effects();
            typeof(Effects).GetField("effectTypes", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(effects, new HashSet<EffectType> { EffectType.RESURRECT });
            skill.GetSkillTemplate().effects = effects;
        }
        if (dead) ChangeTarget(target, "dead");
        Assert.Equal(expected, (bool)Invoke(skill, "CanTargetFirstTarget")!);
    }

    private static BossAiHarness Harness() => BossAiHarness.For().WithAi(typeof(GeneralNpcAI)).Build();

    private static Skill NewSkill(Creature caster, Creature target, string range = "ONLYONE") => new(
        new SkillTemplate { skillId = 1, duration = 1000, properties = new SkillProperties
            { FirstTargetRaw = "TARGET", TargetTypeRaw = range } }, caster, 1, target, null!);

    private static void ChangeTarget(Creature target, string change)
    {
        switch (change)
        {
            case "dead":
                typeof(CreatureLifeStats).GetField("currentHp", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(target.GetLifeStats(), 0);
                break;
            case "hidden": target.SetVisualState(CreatureVisualState.HIDE20); break;
            case "protected": target.SetVisualState(CreatureVisualState.BLINKING); break;
            case "despawned": target.GetPosition().SetIsSpawned(false); break;
        }
    }

    private static void Group(Player first, Player second)
    {
        var group = new PlayerGroup(new PlayerGroupMember(first), TeamType.GROUP, 912345);
        group.AddMember(new PlayerGroupMember(first));
        group.AddMember(new PlayerGroupMember(second));
    }

    private static object? Invoke(Skill skill, string method) => typeof(Skill)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(skill, null);

    private static int MessageId(SM_SYSTEM_MESSAGE message) => (int)typeof(SM_SYSTEM_MESSAGE)
        .GetField("msgId", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(message)!;

    private static Connection Attach(Player player)
    {
        var connection = new Connection();
        connection.SetAccount(player.GetAccount());
        player.SetClientConnection(connection);
        Assert.True(connection.SetActivePlayer(player));
        return connection;
    }

    private sealed class Connection() : AionConnection("127.0.0.1")
    {
        private readonly Queue<AionServerPacket> queue = new();
        public List<AionServerPacket> Sent { get; } = new();
        protected override Queue<AionServerPacket> GetSendMsgQueue() => queue;
        protected override void EnqueuePacket(AionServerPacket packet, bool closing) => Sent.Add(packet);
    }
}
