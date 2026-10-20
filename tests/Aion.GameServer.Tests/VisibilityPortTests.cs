using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Aion.Commons.Nio;
using Aion.GameServer.Controllers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Alliance.Events;
using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Model.Team.Group.Events;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Restrictions;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.SkillEngine.Properties;
using Aion.GameServer.TestKit;
using Aion.GameServer.World.Knownlist;
using SkillProperties = Aion.GameServer.SkillEngine.Properties.Properties;
using PlayerGroupMember = Aion.GameServer.Model.Team.Group.PlayerGroupMember;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class VisibilityPortTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string Sha = "b0a597458330ea36e1de112f21a0ab5c899c869a";

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)]
    public void VisibilityTargetingAndSecondaryEligibilityMatchExactJava(int relation)
    {
        using var s = new Scope(); using var f = Fixture();
        var a = s.A; var b = s.B; Creature observer = a, target = b;
        switch (relation)
        {
            case 1: target = a; break;
            case 2: observer = Summon(a, 501); target = Summon(a, 502); break;
            case 3: target = Summon(a, 502); break;
            case 4: observer = Summon(a, 501); target = a; break;
            case 5: Group(a, b); observer = Summon(a, 501); target = Summon(b, 502); break;
            case 6: Group(a, b); s.Duel(a, b); break;
            case 7: target = Kisk(b.ObjectId); break;
            case 8: Group(a, b); target = Kisk(b.ObjectId); break;
            case 9: target = Kisk(a.ObjectId); break;
            case 10: observer = Summon(a, 501); target = Kisk(a.ObjectId); break;
        }
        int count = 0;
        foreach (var row in f.RootElement.GetProperty("matrix").EnumerateArray().Where(r => r[0].GetInt32() == relation))
        {
            State(observer, "seeState", row[1].GetInt32()); State(target, "visualState", row[2].GetInt32());
            Assert.Equal(row[3].GetBoolean(), observer.CanSee(target));
            Assert.Equal(row[4].GetBoolean(), observer.CanTarget(target));
            Assert.Equal(row[5].GetBoolean(), observer.CanAffect(target)); count++;
        }
        Assert.Equal(140, count);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void RangeLifeAndFirstVersusSecondaryRulesMatchExactJava(bool dead, bool resurrect)
    {
        using var s = new Scope(); using var f = Fixture(); var template = Template();
        if (resurrect)
        {
            template.effects = new Effects(); typeof(Effects).GetField("effectTypes", Fields)!.SetValue(template.effects, new HashSet<EffectType> { EffectType.RESURRECT });
        }
        if (dead) typeof(Aion.GameServer.Model.Stats.Container.CreatureLifeStats).GetField("currentHp", Fields)!.SetValue(s.B.GetLifeStats(), 0);
        var method = typeof(TargetRangeProperty).GetMethod("CheckCommonRequirements", Statics)!; int count = 0;
        foreach (var row in f.RootElement.GetProperty("ranges").EnumerateArray().Where(r => r[3].GetBoolean() == dead && r[4].GetBoolean() == resurrect))
        {
            State(s.A, "seeState", row[0].GetInt32()); State(s.B, "visualState", row[1].GetInt32());
            Assert.Equal(row[5].GetBoolean(), (bool)method.Invoke(null, [s.B, s.A, template, row[2].GetBoolean() ? s.B : s.A])!); count++;
        }
        Assert.Equal(280, count);
    }

    [Fact]
    public void PacketBytesIncludingExplicitRedrawAndCachedStateMatchExactJava()
    {
        using var s = new Scope(); using var f = Fixture(); SetId(s.A, 700001);
        foreach (var row in f.RootElement.GetProperty("packets").EnumerateArray())
        {
            State(s.A, "seeState", row[0].GetInt32()); State(s.A, "visualState", row[1].GetInt32());
            var packet = new SM_PLAYER_STATE(s.A, row[2].GetBoolean());
            State(s.A, "seeState", 0); State(s.A, "visualState", 0);
            Assert.Equal(row[3].GetString(), Hex(packet));
        }
    }

    [Theory]
    [InlineData(0, false, true)] [InlineData(0, true, true)]
    [InlineData(2, false, false)] [InlineData(2, true, true)]
    [InlineData(64, false, false)] [InlineData(64, true, false)]
    [InlineData(128, false, false)] [InlineData(128, true, false)]
    [InlineData(192, false, false)] [InlineData(192, true, false)]
    public void RealSkillUsesHideDetectionOnlyForFirstTarget(int visual, bool secondary, bool accepted)
    {
        using var s = new Scope(); State(s.B, "visualState", visual);
        var skill = new Skill(Template(), s.A, 1, secondary ? s.A : s.B, null!);
        Assert.Equal(accepted, (bool)typeof(Skill).GetMethod("IsValidTarget", Fields)!.Invoke(skill, [s.A, s.B])!);
        if (!secondary) Assert.Equal(accepted, (bool)typeof(Skill).GetMethod("CanTargetFirstTarget", Fields)!.Invoke(skill, null)!);
    }

    [Theory]
    [InlineData(0, false, true)] [InlineData(0, true, true)]
    [InlineData(2, false, false)] [InlineData(2, true, false)]
    [InlineData(64, false, false)] [InlineData(64, true, false)]
    [InlineData(128, false, false)] [InlineData(128, true, false)]
    public void ClickAndAssistApplyTargetabilityToKnownVisibleCreatures(int visual, bool assist, bool accepted)
    {
        using var s = new Scope(); var bridge = s.Common.Player(65);
        s.A.SetKnownlist(new FixedKnownList(s.A, s.B, bridge));
        State(s.B, "visualState", visual);
        if (assist) { bridge.SetTarget(s.B); s.A.SetTarget(bridge); }
        var packet = new CM_TARGET_SELECT(0, new HashSet<AionConnection.State> { AionConnection.State.IN_GAME });
        packet.SetConnection(s.Connection(s.A));
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream); writer.Write(s.B.ObjectId); writer.Write((byte)(assist ? 1 : 0));
        packet.SetBuffer(ByteBuffer.Wrap(stream.ToArray()).Order(ByteOrder.LITTLE_ENDIAN)); Assert.True(packet.Read()); packet.Run();
        Assert.Same(accepted ? s.B : assist ? bridge : null, s.A.GetTarget());
        if (assist && !accepted) AssertMessage(s.Connection(s.A), SM_SYSTEM_MESSAGE.STR_ASSISTKEY_NO_USER());
    }

    [Theory]
    [InlineData(2)] [InlineData(64)] [InlineData(128)]
    public void AttackAndDuelRequestsRejectTargetsTheRequesterCannotTarget(int visual)
    {
        using var s = new Scope(); State(s.B, "visualState", visual);
        s.Connection(s.A).Clear(); Assert.False(PlayerRestrictions.CanAttack(s.A, s.B));
        Assert.Contains(s.Connection(s.A).Packets.OfType<SM_ATTACK_RESPONSE>(), p => Hex(p) == Hex(SM_ATTACK_RESPONSE.STOP_INVALID_TARGET(s.A.GetGameStats().GetAttackCounter())));
        s.Connection(s.A).Clear(); DuelService.GetInstance().OnDuelRequest(s.A, s.B);
        AssertMessage(s.Connection(s.A), SM_SYSTEM_MESSAGE.STR_DUEL_PARTNER_INVALID(s.B.GetName()));
        Assert.Empty(s.Connection(s.B).Packets.OfType<SM_QUESTION_WINDOW>());
    }

    [Fact]
    public void SeeCommandTogglesGmDetectionSeparatelyAndRefreshesKnownVisibility()
    {
        using var s = new Scope(); var known = new FixedKnownList(s.A, s.B); s.A.SetKnownlist(known);
        State(s.B, "visualState", 128); known.Update(); Assert.False(known.Sees(s.B));
        var command = new Aion.GameServer.Handlers.AdminCommands.See(); command.Execute(s.A);
        Assert.True(s.A.IsInSeeState(CreatureSeeState.SearchGmInvisible)); Assert.True(s.A.IsInSeeState(CreatureSeeState.Search20)); Assert.True(known.Sees(s.B));
        Assert.True(s.A.CanTarget(s.B)); Assert.False(s.A.CanAffect(s.B));
        command.Execute(s.A); Assert.Equal(0, s.A.GetSeeState()); Assert.False(known.Sees(s.B));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AdminAndConsoleInvisibilityUseTheGmBitAndRetainExistingHide(bool console)
    {
        using var s = new Scope(); s.A.SetVisualState(CreatureVisualState.HIDE2);
        if (console) new Aion.GameServer.Handlers.ConsoleCommands.Invisible().Execute(s.A);
        else new Aion.GameServer.Handlers.AdminCommands.Invis().Execute(s.A);
        Assert.Equal(130, s.A.GetVisualState()); Assert.False(s.B.CanTarget(s.A));
        if (console) new Aion.GameServer.Handlers.ConsoleCommands.Visible().Execute(s.A);
        else new Aion.GameServer.Handlers.AdminCommands.Invis().Execute(s.A);
        Assert.Equal(2, s.A.GetVisualState());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ProtectionSendsRedrawOnlyToSelfAndDoesNotClearExistingTargets(bool alreadyProtected)
    {
        using var s = new Scope(); s.A.SetKnownlist(new FixedKnownList(s.A, s.B)); s.B.SetKnownlist(new FixedKnownList(s.B, s.A)); s.B.SetTarget(s.A);
        if (alreadyProtected) s.A.SetVisualState(CreatureVisualState.BLINKING);
        s.Connection(s.A).Clear(); s.Connection(s.B).Clear(); s.A.GetController().StartProtectionActiveTask();
        Assert.True(s.A.IsProtectionActive()); Assert.Same(s.A, s.B.GetTarget());
        var self = Assert.Single(s.Connection(s.A).Packets.OfType<SM_PLAYER_STATE>()); Assert.Equal("01", Hex(self)[^2..]);
        var other = s.Connection(s.B).Packets.OfType<SM_PLAYER_STATE>().ToArray(); Assert.Equal(alreadyProtected ? 0 : 1, other.Length);
        if (!alreadyProtected) Assert.Equal("00", Hex(other[0])[^2..]);
        Assert.Single(s.Connection(s.A).Packets.OfType<SM_INVINCIBLE_TIME>());
        s.A.GetController().CancelTask(TaskId.PROTECTION_ACTIVE);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void KiskIsAttackableByTheOwnersDuelOpponentButItsDisplayTypeStaysSupport(bool dueling)
    {
        using var s = new Scope(); var kisk = Kisk(s.B.ObjectId);
        if (dueling) s.Duel(s.A, s.B);
        Assert.Equal(dueling, kisk.IsEnemyFrom(s.A)); Assert.Equal(CreatureType.SUPPORT, kisk.GetTypeValue(s.A));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void GroupAndAllianceMembershipEventsRefreshBothSidesOfHiddenVisibility(bool alliance)
    {
        using var s = new Scope(); var third = s.Common.Player(65); var joiner = s.B;
        State(joiner, "visualState", 128);
        var knownA = new FixedKnownList(s.A, joiner); var knownB = new FixedKnownList(joiner, s.A); var unknown = new FixedKnownList(third);
        s.A.SetKnownlist(knownA); joiner.SetKnownlist(knownB); third.SetKnownlist(unknown); knownA.Update(); Assert.False(knownA.Sees(joiner));
        knownA.Updates = knownB.Updates = unknown.Updates = 0;
        if (alliance)
        {
            var team = new PlayerAlliance(new PlayerAllianceMember(s.A), TeamType.ALLIANCE);
            team.AddMember(new PlayerAllianceMember(s.A)); team.AddMember(new PlayerAllianceMember(third));
            new PlayerAllianceEnteredEvent(team, joiner).HandleEvent();
            Assert.True(knownA.Sees(joiner)); Assert.Equal(1, knownA.Updates); Assert.Equal(1, knownB.Updates); Assert.Equal(0, unknown.Updates);
            knownA.Updates = knownB.Updates = 0;
            new PlayerAllianceLeavedEvent(team, joiner, LeaveReson.DISBAND).HandleEvent();
        }
        else
        {
            var team = Group(s.A, third);
            new PlayerGroupEnteredEvent(team, joiner).HandleEvent();
            Assert.True(knownA.Sees(joiner)); Assert.Equal(1, knownA.Updates); Assert.Equal(1, knownB.Updates); Assert.Equal(0, unknown.Updates);
            knownA.Updates = knownB.Updates = 0;
            new PlayerGroupLeavedEvent(team, joiner, LeaveReson.DISBAND).HandleEvent();
        }
        Assert.False(knownA.Sees(joiner)); Assert.Equal(1, knownA.Updates); Assert.Equal(1, knownB.Updates);
    }

    [Fact]
    public void RemovedHideTimerDoesNotClearATargetReassignedAfterTheNormalVisibilityRefresh()
    {
        using var s = new Scope(); s.A.SetKnownlist(new FixedKnownList(s.A, s.B)); s.B.SetKnownlist(new FixedKnownList(s.B, s.A)); s.B.SetTarget(s.A);
        new HideEffect { state = CreatureVisualState.HIDE2 }.StartEffect(new Effect(s.A, s.A, new SkillTemplate { skillId = 1 }, 1));
        Assert.Null(s.B.GetTarget()); // The unchanged NotSee callback clears the original target immediately.
        s.B.SetTarget(s.A); // An internal reassignment probes the removed delayed callback, not client selection acceptance.
        s.Common.H.Clock.Advance(TimeSpan.FromMilliseconds(501));
        Assert.Same(s.A, s.B.GetTarget()); Assert.False(s.B.CanTarget(s.A));
    }

    [Fact]
    public void SameMapTeleportEstablishesProtectionBeforeTheSpawnCallback()
    {
        using var s = new Scope();
        s.A.SetMotions(new Aion.GameServer.Model.GameObjects.Players.Motion.MotionList(s.A));
        Aion.GameServer.World.World.GetInstance().Despawn(s.A);
        var controller = new SpawnProbeController(); controller.SetOwner(s.A);
        typeof(VisibleObject).GetField("_controller", Fields)!.SetValue(s.A, controller);
        typeof(TeleportService).GetMethod("SpawnOnSameMap", Statics)!.Invoke(null, [s.A]);
        Assert.True(controller.SawProtection); Assert.True(s.A.IsProtectionActive());
        controller.CancelTask(TaskId.PROTECTION_ACTIVE);
    }

    private sealed class SpawnProbeController : PlayerController
    {
        internal bool SawProtection;
        public override void OnBeforeSpawn() { SawProtection = GetOwner().IsProtectionActive(); }
        public override void OnAfterSpawn() { }
    }

    private static SkillTemplate Template() => new() { skillId = 1, properties = new SkillProperties { FirstTargetRaw = "TARGET", TargetTypeRaw = "ONLYONE", TargetRelationRaw = "ALL" } };
    private static PlayerGroup Group(Player a, Player b)
    {
        var g = new PlayerGroup(new PlayerGroupMember(a), TeamType.GROUP, 912346); g.AddMember(new PlayerGroupMember(a)); g.AddMember(new PlayerGroupMember(b)); return g;
    }
    private static Summon Summon(Player owner, int id)
    {
        var s = (Summon)RuntimeHelpers.GetUninitializedObject(typeof(Summon)); SetId(s, id);
        typeof(Summon).GetField("master", Fields)!.SetValue(s, owner);
        typeof(VisibleObject).GetField("_objectTemplate", Fields)!.SetValue(s, new NpcTemplate { npcId = 217182 }); return s;
    }
    private static Kisk Kisk(int creator)
    {
        var k = (Kisk)RuntimeHelpers.GetUninitializedObject(typeof(Kisk)); SetId(k, 503); k.SetCreatorId(creator);
        typeof(Kisk).GetField("ownerRace", Fields)!.SetValue(k, Race.ELYOS); return k;
    }
    private static void State(Creature c, string field, int value) => typeof(Creature).GetField(field, Fields)!.SetValue(c, value);
    private static void SetId(AionObject o, int id) => typeof(AionObject).GetField("_objectId", Fields)!.SetValue(o, id);
    private static JsonDocument Fixture()
    {
        var f = JsonDocument.Parse(File.ReadAllText(Path.Combine(RealStaticData.RepoRoot(), "parity-artifacts/golden/formulas/Visibility.b0a597458.json")));
        Assert.Equal(Sha, f.RootElement.GetProperty("sourceJavaCommit").GetString()); return f;
    }
    private static string Hex(AionServerPacket packet)
    {
        var buf = ByteBuffer.Allocate(8192).Order(ByteOrder.LITTLE_ENDIAN); packet.SetBuf(buf);
        typeof(AionServerPacket).GetMethod("WriteImpl", Fields)!.Invoke(packet, new object?[] { null });
        byte[] bytes = new byte[buf.Position()]; buf.Flip(); buf.Get(bytes); return Convert.ToHexString(bytes);
    }
    private static void AssertMessage(PvpExpPortTests.Connection c, SM_SYSTEM_MESSAGE m) => Assert.Contains(c.Packets.OfType<SM_SYSTEM_MESSAGE>(), p => Hex(p) == Hex(m));
    private sealed class FixedKnownList : KnownList
    {
        internal int Updates;
        internal FixedKnownList(VisibleObject owner, params VisibleObject[] objects) : base(owner)
        {
            foreach (var o in objects) { var ko = new KnownObject(o); typeof(KnownObject).GetMethod("UpdateVisible", Fields)!.Invoke(ko, [true]); KnownObjects[o.ObjectId] = ko; }
        }
        public override void Update() { Updates++; foreach (var o in KnownObjects.Values.ToArray()) UpdateVisibleObject(o.Get()); }
    }
    private sealed class Scope : IDisposable
    {
        internal PvpExpPortTests.Scope Common { get; } = new();
        internal Player A { get; } internal Player B { get; }
        private readonly List<int> duelIds = new();
        internal Scope() { A = Common.Player(65); B = Common.Player(65); A.SetPlayerSettings(new Aion.GameServer.Model.GameObjects.Players.PlayerSettings()); B.SetPlayerSettings(new Aion.GameServer.Model.GameObjects.Players.PlayerSettings()); A.SetKnownlist(new FixedKnownList(A)); B.SetKnownlist(new FixedKnownList(B)); }
        internal PvpExpPortTests.Connection Connection(Player p) => (PvpExpPortTests.Connection)p.GetClientConnection();
        internal void Duel(Player a, Player b)
        {
            var map = (ConcurrentDictionary<int, int>)typeof(DuelService).GetField("duels", Fields)!.GetValue(DuelService.GetInstance())!;
            Assert.True(map.TryAdd(a.ObjectId, b.ObjectId)); duelIds.Add(a.ObjectId); Assert.True(map.TryAdd(b.ObjectId, a.ObjectId)); duelIds.Add(b.ObjectId);
        }
        public void Dispose()
        {
            var map = (ConcurrentDictionary<int, int>)typeof(DuelService).GetField("duels", Fields)!.GetValue(DuelService.GetInstance())!;
            foreach (int id in duelIds) map.TryRemove(id, out _); Common.Dispose();
        }
    }
}
