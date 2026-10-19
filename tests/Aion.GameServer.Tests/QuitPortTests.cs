using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Aion.Bots.Protocol;
using Aion.Commons.Nio;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Craft;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils;
using TYPE = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.TYPE;
using LOG = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.LOG;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class QuitPortTests
{
    [Fact]
    public void HandlerAndWireMatchAllTwentyThreeOutcomesExecutedFromExactJava()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AionServer.slnx"))) dir = dir.Parent;
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!.FullName, "parity-artifacts/golden/formulas/Quit.6f5feaceb.json")));
        Assert.Equal("6f5feaceb2bda56337f81a8c10c89f6f6b16ff16", fixture.RootElement.GetProperty("sourceJavaCommit").GetString());
        foreach (var row in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            using var h = BossAiHarness.For().Build();
            var player = h.SpawnPlayer(); var con = Attach(player);
            long now = 1_700_000_000_000; SystemClock.UseSource(() => now);
            player.SetQuitCountdownStartMillis(now - row.GetProperty("elapsed").GetInt64());
            now -= 10_000 - row.GetProperty("remaining").GetInt64(); player.BlockQuitting(); now = 1_700_000_000_000;
            player.GetCommonData().SetInEditMode(row.GetProperty("edit").GetBoolean());
            if (row.GetProperty("target").GetBoolean())
            {
                var npc = (EditNpc)RuntimeHelpers.GetUninitializedObject(typeof(EditNpc));
                npc.SetPosition(player.GetPosition());
                typeof(VisibleObject).GetField("_target", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player, npc);
            }
            if (!row.GetProperty("player").GetBoolean()) con.SetActivePlayer(null!);
            int leaves = 0;
            using var capture = PlayerLeaveWorldService.CaptureForCurrentContext((p, registration) =>
            {
                Assert.False(registration); Assert.True(PlayerLeaveWorldService.IsLeavingWorld(p)); leaves++;
                p.SetClientConnection(null!); con.SetActivePlayer(null!);
            }, captureRegistration: false);
            var packet = new CM_QUIT(3, new HashSet<AionConnection.State> { AionConnection.State.AUTHED, AionConnection.State.IN_GAME });
            packet.SetConnection(con); packet.SetBuffer(ByteBuffer.Wrap([row.GetProperty("stay").GetBoolean() ? (byte)1 : (byte)0]));
            Assert.True(packet.Read()); packet.Run();
            Assert.Equal(row.GetProperty("leaves").GetInt32(), leaves);
            Assert.Equal(row.GetProperty("closed").GetBoolean(), con.IsClosed());
            Assert.Equal(row.GetProperty("blockedMillis").GetInt64(), player.GetMillisUntilQuittingAllowed());
            string expected = row.GetProperty("payloadHex").GetString()!;
            if (expected == "none") Assert.Empty(con.Sent);
            else
            {
                var response = Assert.IsType<SM_QUIT_RESPONSE>(Assert.Single(con.Sent));
                byte[] payload = Payload(response); Assert.Equal(expected, Convert.ToHexString(payload));
                var decoded = new BotServerPacketDecoder().Decode(typeof(SM_QUIT_RESPONSE), payload);
                Assert.Equal((int)payload[0], decoded.Get<int>("mode"));
                Assert.Equal(BitConverter.ToInt32(payload, 1), decoded.Get<int>("value"));
                for (int i = 0; i < payload.Length; i++) Assert.Throws<InvalidDataException>(() => new BotServerPacketDecoder().Decode(typeof(SM_QUIT_RESPONSE), payload[..i]));
            }
            Assert.False(PlayerLeaveWorldService.IsLeavingWorld(player));
        }
    }

    [Theory]
    [InlineData(0, 10000)] [InlineData(1, 9999)] [InlineData(9999, 1)] [InlineData(10000, 0)] [InlineData(10001, 0)]
    public void QuitBlockUsesSharedClockAndExpiresAtExactTenSeconds(int elapsed, long expected)
    {
        using var h = BossAiHarness.For().Build(); var p = h.SpawnPlayer();
        Assert.Equal(0, p.GetMillisUntilQuittingAllowed()); p.BlockQuitting();
        h.Clock.Advance(TimeSpan.FromMilliseconds(elapsed)); Assert.Equal(expected, p.GetMillisUntilQuittingAllowed());
        p.BlockQuitting(); Assert.Equal(10000, p.GetMillisUntilQuittingAllowed());
        p.UnblockQuitting(); Assert.Equal(0, p.GetMillisUntilQuittingAllowed());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MayQuitOnlyRecordsTheActivePlayersCountdownAndMayRestartIt(bool active)
    {
        using var h = BossAiHarness.For().Build(); var p = h.SpawnPlayer(); var c = Attach(p);
        if (!active) c.SetActivePlayer(null!);
        var packet = new CM_MAY_QUIT(4, new HashSet<AionConnection.State> { AionConnection.State.IN_GAME, AionConnection.State.AUTHED });
        packet.SetConnection(c); packet.SetBuffer(ByteBuffer.Wrap([])); Assert.True(packet.Read()); packet.Run();
        Assert.Equal(active ? SystemClock.CurrentMillis() : 0, p.GetQuitCountdownStartMillis());
        h.Clock.Advance(TimeSpan.FromSeconds(2)); packet.Run();
        Assert.Equal(active ? SystemClock.CurrentMillis() : 0, p.GetQuitCountdownStartMillis()); Assert.Empty(c.Sent);
        Assert.Equal(typeof(CM_MAY_QUIT), GameClientPackets.MayQuit().PacketType);
        Assert.Empty(GameClientPackets.MayQuit().Body);
    }

    [Fact]
    public void ExplicitQuitRunsPendingLostConnectionTaskImmediatelyOnceAndCancelsItsTimer()
    {
        using var h = BossAiHarness.For().Build(); var p = h.SpawnPlayer(); var c = Attach(p); int saves = 0;
        using var capture = PlayerLeaveWorldService.CaptureForCurrentContext((saving, registration) =>
        {
            Assert.False(registration); Assert.True(PlayerLeaveWorldService.IsLeavingWorld(saving)); saves++;
            saving.SetClientConnection(null!); c.SetActivePlayer(null!);
        }, captureRegistration: false);
        h.Clock.Advance(TimeSpan.FromMinutes(1)); // Idle time no longer shortens lost-connection grace.
        PlayerLeaveWorldService.RegisterLeaveWorld(p); h.Clock.Advance(TimeSpan.FromSeconds(3));
        PlayerLeaveWorldService.RegisterLeaveWorld(p); Assert.Equal(0, saves);
        PlayerLeaveWorldService.LeaveWorldNow(p); Assert.Equal(1, saves); Assert.False(PlayerLeaveWorldService.IsLeavingWorld(p));
        PlayerLeaveWorldService.LeaveWorldNow(p); h.Clock.Advance(TimeSpan.FromSeconds(20));
        Assert.Equal(1, saves); Assert.False(PlayerLeaveWorldService.IsLeavingWorld(p)); Assert.Empty(h.Clock.Faults);
    }

    [Fact]
    public void RegularAttackBlocksBothPlayersBeforeDamageAndDamageRefreshesTheVictim()
    {
        using var h = BossAiHarness.For().Build(); var attacker = h.SpawnPlayer(); var target = h.SpawnPlayer(race: Race.ASMODIANS);
        var controller = new AttackProbe(); controller.SetOwner(attacker); controller.AttackTarget(target, 5000, true);
        Assert.Equal(10000, attacker.GetMillisUntilQuittingAllowed()); Assert.Equal(10000, target.GetMillisUntilQuittingAllowed());
        h.Clock.Advance(TimeSpan.FromSeconds(2)); target.GetController().OnAttack(target, null!, TYPE.DAMAGE, 1, false, LOG.REGULAR, null, null);
        Assert.Equal(8000, attacker.GetMillisUntilQuittingAllowed()); Assert.Equal(10000, target.GetMillisUntilQuittingAllowed());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FallingAndDrowningDoNotStartAQuitBlock(bool drowning)
    {
        using var h = BossAiHarness.For().Build(); var p = h.SpawnPlayer();
        p.GetLifeStats().ReduceHp(drowning ? TYPE.DROWNING : TYPE.FALL_DAMAGE, 1, 0, LOG.REGULAR, p);
        Assert.Equal(0, p.GetMillisUntilQuittingAllowed());
    }

    [Theory]
    [InlineData(SkillSubType.ATTACK, true)] [InlineData(SkillSubType.DEBUFF, true)] [InlineData(SkillSubType.HEAL, false)]
    public void HostileCastBlocksAtCastEndAndRefreshesWhenItsDelayedEffectsLand(SkillSubType type, bool blocked)
    {
        using var h = BossAiHarness.For().Build(); var p = h.SpawnPlayer();
        typeof(StaticData).GetProperty(nameof(StaticData.Motions))!.SetValue(DataManager.GetRegisteredInstance()!.StaticData, new MotionData());
        var skill = new CastProbe(new SkillTemplate { skillId = 999092, lvl = 1, activationAttribute = ActivationAttribute.ACTIVE, subType = type }, p);
        skill.GetEffectedList().Add(p); typeof(Skill).GetField("hitTime", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(skill, 5000);
        p.SetCasting(skill); skill.Finish(); Assert.Equal(blocked ? 10000 : 0, p.GetMillisUntilQuittingAllowed());
        h.Clock.Advance(TimeSpan.FromSeconds(5)); Assert.Equal(blocked ? 10000 : 0, p.GetMillisUntilQuittingAllowed());
        Assert.Empty(h.Clock.Faults);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OnlyAValidatedCraftStartsTheTenSecondBlock(bool valid)
    {
        using var s = new LimitedRecipeMorphPortTests.Scope(); s.Player.GetRecipeList().AddRecipe(9001, 3); s.Materials(5);
        CraftService.StartCrafting(s.Player, 9001, 0, 0, valid ? new() {{199999902, 2}} : new());
        Assert.Equal(valid ? 10000 : 0, s.Player.GetMillisUntilQuittingAllowed());
        if (valid) { s.Player.GetInteractionTask().Abort(); Assert.Equal(10000, s.Player.GetMillisUntilQuittingAllowed()); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RevivalAndTeleportClearAnExistingQuitBlock(bool teleport)
    {
        using var h = BossAiHarness.For().Build(); var p = h.SpawnPlayer(); p.BlockQuitting();
        p.SetMotions(new Aion.GameServer.Model.GameObjects.Players.Motion.MotionList(p));
        if (teleport) TeleportService.TeleportTo(p, p.GetWorldId(), p.GetInstanceId(), p.GetX(), p.GetY(), p.GetZ(), p.GetHeading(), Aion.GameServer.Model.Animations.TeleportAnimation.NONE);
        else PlayerReviveService.Revive(p, 100, 100, false, 0);
        Assert.Equal(0, p.GetMillisUntilQuittingAllowed());
    }

    private static byte[] Payload(AionServerPacket packet)
    {
        var b = ByteBuffer.Allocate(128).Order(ByteOrder.LITTLE_ENDIAN); packet.SetBuf(b);
        typeof(AionServerPacket).GetMethod("WriteImpl", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(packet, new object?[] {null});
        byte[] data = new byte[b.Position()]; b.Flip(); b.Get(data); return data;
    }
    private static Connection Attach(Player p)
    {
        var c = new Connection(); c.SetAccount(p.GetAccount()); p.SetClientConnection(c); Assert.True(c.SetActivePlayer(p)); return c;
    }
    private sealed class Connection() : AionConnection("127.0.0.1")
    {
        public List<AionServerPacket> Sent = new();
        protected override void EnqueuePacket(AionServerPacket packet, bool closing) => Sent.Add(packet);
    }
    // Only the NPC's dialog template and position are read; no AI/controller is needed.
    private sealed class EditNpc() : Npc(null!, null!, null!)
    {
        public override NpcTemplate GetObjectTemplate() => new() { boundRadius = BoundRadius.Default, talkInfo = new TalkInfo { FuncDialogIdsRaw = DialogAction.EDIT_CHARACTER_ALL.ToString() } };
    }
    private sealed class AttackProbe : Aion.GameServer.Controllers.CreatureController<Player> { }
    private sealed class CastProbe(SkillTemplate template, Player player) : Skill(template, player, 1, player, null!)
    {
        public void Finish() => EndCast();
    }
}
