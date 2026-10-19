using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml.Serialization;
using Aion.Commons.Nio;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Drop;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Model.Team.League;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Drop;
using Aion.GameServer.Services;
using Aion.GameServer.Taskmanager.Tasks;
using Aion.GameServer.TestKit;
using Aion.GameServer.World;
using Aion.GameServer.World.Knownlist;
using Aion.GameServer.Utils;
using PlayerGroupMember = Aion.GameServer.Model.Team.Group.PlayerGroupMember;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class LootingPortTests
{
    [Fact]
    public void RulesAndBothProductionReadersMatchExactJavaVectors()
    {
        using var s = new Scope(); using var fixture = Fixture();
        foreach (var c in fixture.RootElement.GetProperty("startReads").EnumerateArray())
        {
            var packet = Read<CM_START_LOOT>(s.P, Convert.FromHexString(c.GetProperty("bodyHex").GetString()!));
            Assert.Equal(c.GetProperty("target").GetInt32(), Get(packet, "targetObjectId")); Assert.Equal(c.GetProperty("action").GetString(), Get(packet, "action")?.ToString());
        }
        foreach (var c in fixture.RootElement.GetProperty("distributionReads").EnumerateArray())
        {
            var packet = Read<CM_DISTRIBUTION_SETTINGS>(s.P, Convert.FromHexString(c.GetProperty("bodyHex").GetString()!));
            Assert.Equal(c.GetProperty("rule").GetString(), Get(packet, "lootRules")?.ToString()); Assert.Equal(c.GetProperty("autoJunk").GetBoolean(), Get(packet, "autoDistributeJunk"));
            string[] fields = ["commonItemAbove", "superiorItemAbove", "heroicItemAbove", "fabledItemAbove", "ethernalItemAbove", "mythicItemAbove"];
            Assert.Equal(c.GetProperty("grades").EnumerateArray().Select(v => v.GetInt32()), fields.Select(f => (int)Get(packet, f)!));
        }
        foreach (var c in fixture.RootElement.GetProperty("rules").EnumerateArray())
        {
            var rules = Rules(c.GetProperty("autoJunk").GetBoolean()); var prior = c.GetProperty("previous");
            Assert.Equal(c.GetProperty("message").GetInt32(), rules.GetJunkDistributionChangeMessageId(prior.ValueKind == JsonValueKind.Null ? null : Rules(prior.GetBoolean())));
            Assert.Equal(c.GetProperty("nullAuto").GetBoolean(), rules.IsAutoDistributedJunk(null));
            foreach (var q in c.GetProperty("qualities").EnumerateArray())
            {
                var quality = Enum.Parse<ItemQuality>(q.GetProperty("name").GetString()!);
                Assert.Equal(q.GetProperty("auto").GetBoolean(), rules.IsAutoDistributedJunk(quality)); Assert.Equal(q.GetProperty("qualityRule").GetBoolean(), rules.GetQualityRule(quality));
            }
        }
        Assert.False(new LootGroupRules().IsAutoDistributeJunk()); Assert.Equal(2, LootGroupRules.SEVENTH_GRADE_RULE);
        Assert.Null(LootRuleTypeExtensions.GetById(-1)); Assert.Null(LootRuleTypeExtensions.GetById(3));
    }

    [Fact]
    public void ActualFloatRangeHelperMatchesJavaAtAllBoundariesAndMapExceptions()
    {
        using var s = new Scope(); using var fixture = Fixture(); var npc = s.Corpse().Npc;
        var playerPosition = s.P.GetPosition(); var npcPosition = npc.GetPosition(); var template = npc.GetObjectTemplate(); var oldBounds = template.boundRadius;
        var packet = new CM_START_LOOT(0, new HashSet<AionConnection.State>());
        try
        {
            foreach (var c in fixture.RootElement.GetProperty("geometry").EnumerateArray())
            {
                float[] bits = c.GetProperty("bits").EnumerateArray().Select(v => BitConverter.Int32BitsToSingle(unchecked((int)Convert.ToUInt32(v.GetString()!, 16)))).ToArray();
                s.P.SetPosition(new WorldPosition(c.GetProperty("map").GetInt32(), 0, 0, 0, 0, playerPosition.GetMapRegion()));
                var parent = (WorldMapInstance)RuntimeHelpers.GetUninitializedObject(playerPosition.GetMapRegion()!.GetParent().GetType()); Set(parent, "instanceId", c.GetProperty("instance").GetInt32());
                npc.SetPosition(new WorldPosition(c.GetProperty("npcMap").GetInt32(), bits[0], bits[1], bits[2], 0, new MapRegion(0, parent, [])));
                template.boundRadius = new BoundRadius(bits[3], bits[4], 2);
                bool actual = (bool)Invoke(packet, "IsInLootRange", s.P, npc)!;
                Assert.True(actual == c.GetProperty("expected").GetBoolean(), c.GetProperty("name").GetString());
            }
        }
        finally { template.boundRadius = oldBounds; s.P.SetPosition(playerPosition); npc.SetPosition(npcPosition); }
    }

    [Fact]
    public void ActualCompleteWritersMatchJavaWithMessageFlagsAndNonstandardSortedAllianceGroups()
    {
        using var s = new Scope(); using var fixture = Fixture();
        foreach (var c in fixture.RootElement.GetProperty("packets").EnumerateArray())
        {
            AionServerPacket packet;
            bool flag = c.GetProperty("autoJunk").GetBoolean(); int message = c.GetProperty("message").GetInt32();
            var con = new PvpExpPortTests.Connection(); var active = PvpExpPortTests.BarePlayer(810000, 65); active.SetPosition(new WorldPosition(210010000)); con.SetActivePlayer(active);
            if (c.GetProperty("kind").GetString() == "group")
            {
                packet = (SM_GROUP_INFO)RuntimeHelpers.GetUninitializedObject(typeof(SM_GROUP_INFO));
                Set(packet, "groupId", 4100); Set(packet, "leaderId", 810000); Set(packet, "type", Enum.Parse<TeamType>(c.GetProperty("type").GetString()!));
                Set(packet, "messageId", message); Set(packet, "lootRules", new LootGroupRules(LootRuleType.LEADER, flag, 10, 11, 12, 13, 14, 15));
                if (!c.GetProperty("active").GetBoolean()) con.SetActivePlayer(null!);
            }
            else
            {
                packet = (SM_ALLIANCE_INFO)RuntimeHelpers.GetUninitializedObject(typeof(SM_ALLIANCE_INFO));
                var alliance = (PlayerAlliance)RuntimeHelpers.GetUninitializedObject(typeof(PlayerAlliance)); var groups = new Dictionary<int, PlayerAllianceGroup>();
                foreach (int id in c.GetProperty("groups").EnumerateArray().Select(v => v.GetInt32())) groups.Add(id, new PlayerAllianceGroup(alliance, id));
                Set(alliance, "groups", groups); Set(alliance, "viceCaptainIds", new List<int> { 811001, 811002 }); Set(packet, "alliance", alliance);
                Set(packet, "groupid", 4200); Set(packet, "leaderid", 810000); Set(packet, "type", 63); Set(packet, "subType", 0);
                Set(packet, "lootRules", new LootGroupRules(LootRuleType.ROUNDROBIN, flag, 10, 11, 12, 13, 14, 15)); Set(packet, "messageId", message); Set(packet, "message", "Captainβ");
                var listType = Field(packet.GetType(), "leagueData").FieldType; var list = (IList)Activator.CreateInstance(listType)!; Set(packet, "leagueData", list);
                if (c.GetProperty("league").GetBoolean())
                {
                    Set(packet, "leagueId", 4300); Set(packet, "lootLeagueRules", new LootGroupRules(LootRuleType.FREEFORALL, false, 20, 21, 22, 23, 24, 25));
                    for (int i = 0; i < 2; i++)
                    {
                        var info = Activator.CreateInstance(listType.GetGenericArguments()[0], true)!;
                        Set(info, "alliancePosition", i * 2); Set(info, "allianceObjectId", 4200 + i * 100); Set(info, "memberCount", 6 + i * 6); Set(info, "captainName", i == 0 ? "Alpha" : "βeta"); Set(info, "captainWorldId", 210010000 + i * 10000000); list.Add(info);
                    }
                }
            }
            Assert.Equal(Convert.FromHexString(c.GetProperty("payloadHex").GetString()!), Capture(packet, con));
        }
    }

    [Theory]
    [InlineData("solo", false)] [InlineData("group-member", false)] [InlineData("group-leader", true)]
    [InlineData("alliance-member", false)] [InlineData("alliance-leader", true)]
    [InlineData("league-captain", true)] [InlineData("secondary-captain", false)] [InlineData("league-member", false)]
    public void OnlyTheActualGroupAllianceOrLeagueLeaderCanChangeRules(string mode, bool allowed)
    {
        using var s = new Scope(); object? team = null;
        if (mode.StartsWith("group")) team = s.Group(mode == "group-leader" ? s.P : s.Other, mode == "group-leader" ? s.Other : s.P);
        if (mode.StartsWith("alliance")) team = s.Alliance(mode == "alliance-leader" ? s.P : s.Other, mode == "alliance-leader" ? s.Other : s.P);
        if (mode.Contains("captain") || mode == "league-member")
        {
            var first = mode == "league-captain" ? s.Alliance(s.P, s.Other)
                : mode == "league-member" ? s.Alliance(s.Other, s.P) : s.Alliance(s.Other, s.Third);
            var second = s.Alliance(mode == "secondary-captain" ? s.P : s.Third);
            var lead = new LeagueMember(first, 0); var league = new League(lead); league.AddMember(lead); league.AddMember(new LeagueMember(second, 1)); team = league;
        }
        var before = GetRules(team); s.Clear(); Run(Read<CM_DISTRIBUTION_SETTINGS>(s.P, DistributionBody(2, 1)));
        if (allowed) { Assert.Equal(LootRuleType.LEADER, GetRules(team)!.GetLootRule()); Assert.True(GetRules(team)!.IsAutoDistributeJunk()); }
        else { Assert.Same(before, GetRules(team)); Assert.Empty(s.Packets); }
    }

    [Theory]
    [InlineData(-1)] [InlineData(3)] [InlineData(int.MaxValue)]
    public void InvalidLootRuleNeverFallsBackToFreeForAll(int id)
    {
        using var s = new Scope(); var team = s.Group(s.P, s.Other); var previous = team.GetLootGroupRules(); s.Clear();
        Run(Read<CM_DISTRIBUTION_SETTINGS>(s.P, DistributionBody(id, 1))); Assert.Same(previous, team.GetLootGroupRules()); Assert.Empty(s.Packets);
    }

    [Theory]
    [InlineData("group")] [InlineData("alliance")] [InlineData("league")]
    public void RuleEventsAnnounceOnlyJunkDistributionChangesAndTreatOtherFlagValuesAsFalse(string mode)
    {
        using var s = new Scope(); object team;
        if (mode == "group") team = s.Group(s.P, s.Other);
        else { var alliance = s.Alliance(s.P, s.Other); if (mode == "alliance") team = alliance; else { var lead = new LeagueMember(alliance, 0); var league = new League(lead); league.AddMember(lead); team = league; } }
        foreach (var pair in new[] { (Flag: 1, Message: 1390210), (Flag: 1, Message: 0), (Flag: 2, Message: 1390211) })
        {
            s.Clear(); Run(Read<CM_DISTRIBUTION_SETTINGS>(s.P, DistributionBody(1, pair.Flag)));
            Assert.Equal(pair.Flag == 1, GetRules(team)!.IsAutoDistributeJunk());
            var packet = Assert.Single(s.Packets, p => p is SM_GROUP_INFO or SM_ALLIANCE_INFO); Assert.Equal(pair.Message, Get(packet, "messageId"));
            // Exercise the real constructors as well as the captured standalone writer inputs.
            Assert.NotEmpty(Capture(packet, s.P.GetClientConnection()));
        }
    }

    [Fact]
    public void OpenRequiresKnownNpcInRangeAndTakeRequiresTheSameOpenedList()
    {
        using var s = new Scope(); var first = s.Corpse(); var second = s.Corpse();
        s.P.SetLootingNpcOid(first.Npc.GetObjectId()); Run(Read<CM_LOOT_ITEM>(s.P, Body(second.Npc.GetObjectId(), 0))); Assert.Empty(s.P.GetInventory().GetItems()); Assert.Equal(1, second.Item.GetCount());
        s.P.SetLootingNpcOid(0); s.Known.TryRemove(second.Npc.GetObjectId(), out _); s.Clear();
        Run(Read<CM_START_LOOT>(s.P, Body(second.Npc.GetObjectId(), 0))); Assert.IsType<SM_SYSTEM_MESSAGE>(Assert.Single(s.Packets)); Assert.Null(second.Drop.GetLootingPlayer());
        s.Know(s.Other); s.Clear(); Run(Read<CM_START_LOOT>(s.P, Body(s.Other.GetObjectId(), 0))); Assert.IsType<SM_SYSTEM_MESSAGE>(Assert.Single(s.Packets));
        s.Know(second.Npc); second.Npc.GetPosition().SetXYZH(s.P.GetX() + 100, null, null, null); s.Clear(); Run(Read<CM_START_LOOT>(s.P, Body(second.Npc.GetObjectId(), 0))); Assert.IsType<SM_SYSTEM_MESSAGE>(Assert.Single(s.Packets)); Assert.Null(second.Drop.GetLootingPlayer());
        second.Npc.GetPosition().SetXYZH(s.P.GetX(), null, null, null); Run(Read<CM_START_LOOT>(s.P, Body(second.Npc.GetObjectId(), 0)));
        Assert.Same(s.P, second.Drop.GetLootingPlayer()); Assert.Equal(second.Npc.GetObjectId(), s.P.GetLootingNpcOid()); Assert.True(s.P.IsLooting());
        Run(Read<CM_LOOT_ITEM>(s.P, Body(second.Npc.GetObjectId(), 0))); Assert.Equal(1, Assert.Single(s.P.GetInventory().GetItems()).GetItemCount());
        Run(Read<CM_START_LOOT>(s.P, Body(second.Npc.GetObjectId(), 1))); Assert.False(s.P.IsLooting()); Assert.Equal(0, s.P.GetLootingNpcOid());
    }

    [Theory]
    [InlineData((byte)2)] [InlineData((byte)128)] [InlineData((byte)255)]
    public void UnknownOpenActionCannotClaimOrClearLoot(byte action)
    {
        using var s = new Scope(); var corpse = s.Corpse(); s.Clear(); Run(Read<CM_START_LOOT>(s.P, Body(corpse.Npc.GetObjectId(), action)));
        Assert.Null(corpse.Drop.GetLootingPlayer()); Assert.Equal(0, s.P.GetLootingNpcOid()); Assert.Empty(s.Packets);
    }

    [Fact]
    public void OnlineCorpseOwnerExcludesOtherPlayersAndOnlyItsOwnerCanReleaseIt()
    {
        using var s = new Scope(); var c = s.Corpse(); DropService.GetInstance().RequestDropList(s.P, c.Npc.GetObjectId());
        DropService.GetInstance().RequestDropList(s.Other, c.Npc.GetObjectId()); Assert.Same(s.P, c.Drop.GetLootingPlayer()); Assert.Equal(0, s.Other.GetLootingNpcOid());
        DropService.GetInstance().CloseDropList(s.Other, c.Npc.GetObjectId()); Assert.Same(s.P, c.Drop.GetLootingPlayer());
        DropService.GetInstance().CloseDropList(s.P, c.Npc.GetObjectId()); Assert.Null(c.Drop.GetLootingPlayer());
        DropService.GetInstance().RequestDropList(s.Other, c.Npc.GetObjectId()); Assert.Same(s.Other, c.Drop.GetLootingPlayer());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ClaimAndReleaseWaitForTheCorpseMonitor(bool close)
    {
        using var s = new Scope(); var c = s.Corpse(); if (close) { c.Drop.SetLootingPlayer(s.P); s.P.SetLootingNpcOid(c.Npc.GetObjectId()); }
        using var started = new ManualResetEventSlim(); using var done = new ManualResetEventSlim(); Exception? error = null;
        var worker = new Thread(() => { started.Set(); try { if (close) DropService.GetInstance().CloseDropList(s.P, c.Npc.GetObjectId()); else DropService.GetInstance().RequestDropList(s.P, c.Npc.GetObjectId()); } catch (Exception e) { error = e; } finally { done.Set(); } }) { IsBackground = true };
        Monitor.Enter(c.Drop);
        try
        {
            worker.Start(); Assert.True(started.Wait(TimeSpan.FromSeconds(2)));
            Assert.True(SpinWait.SpinUntil(() => done.IsSet || (worker.ThreadState & ThreadState.WaitSleepJoin) != 0, TimeSpan.FromSeconds(2)));
            Assert.False(done.IsSet); Assert.Same(close ? s.P : null, c.Drop.GetLootingPlayer());
        }
        finally { Monitor.Exit(c.Drop); Assert.True(worker.Join(TimeSpan.FromSeconds(2))); }
        Assert.Null(error); Assert.Same(close ? null : s.P, c.Drop.GetLootingPlayer());
    }

    [Fact]
    public void JunkRecipientRotationAndExistingRollQueueBehaviorRemainIntact()
    {
        using var s = new Scope(); var group = s.Group(s.P, s.Other, s.Third); var rules = Rules(true); group.SetLootGroupRules(rules); var c = s.Corpse();
        c.Drop.SetLootingTeam(group); c.Drop.SetInRangePlayers([s.P, s.Other, s.Third]); s.P.GetInventory().SetLimit(0); s.Other.GetInventory().SetLimit(0); s.Third.GetInventory().SetLimit(0);
        foreach (var expected in new[] { s.P, s.Other, s.Third, s.P })
        {
            var item = s.DropItem(); s.Items[c.Npc.GetObjectId()] = [item]; DropService.GetInstance().RequestDropItem(s.P, c.Npc.GetObjectId(), 0);
            Assert.Same(expected, item.GetWinningPlayer()); Assert.Equal(1, item.GetCount()); Assert.Empty(expected.GetInventory().GetItems());
        }
        Assert.Equal(1, rules.GetJunkRecipientIndex()); rules.SetNrRoundRobin(7); Assert.Equal(7, rules.GetNrRoundRobin());
        var queued = s.DropItem(); rules.AddItemToBeDistributed(queued); Assert.True(rules.ContainDropItem(queued)); rules.RemoveItemToBeDistributed(queued); Assert.Empty(rules.GetItemsToBeDistributed());
    }

    private static LootGroupRules Rules(bool junk) => new(LootRuleType.ROUNDROBIN, junk, 0, 2, 2, 2, 2, 2);
    private static LootGroupRules? GetRules(object? team) => team switch { PlayerGroup g => g.GetLootGroupRules(), PlayerAlliance a => a.GetLootGroupRules(), League l => l.GetLootGroupRules(), _ => null };
    private static byte[] DistributionBody(int rule, int junk) { int[] values = [int.MaxValue, rule, junk, 0, 2, 2, 2, 2, 2, int.MinValue]; byte[] body = new byte[40]; for (int i = 0; i < values.Length; i++) System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(i * 4), values[i]); return body; }
    private static byte[] Body(int target, byte action) { var body = new byte[5]; System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(body, target); body[4] = action; return body; }
    private static T Read<T>(Player player, byte[] body) where T : AionClientPacket { var p = (T)Activator.CreateInstance(typeof(T), 0, new HashSet<AionConnection.State>())!; p.SetConnection(player.GetClientConnection()); p.SetBuffer(ByteBuffer.Wrap(body).Order(ByteOrder.LITTLE_ENDIAN)); Assert.True(p.Read()); Assert.Equal(0, p.GetRemainingBytes()); return p; }
    private static void Run(AionClientPacket p) => Invoke(p, "RunImpl");
    private static byte[] Capture(AionServerPacket p, AionConnection con) { var b = ByteBuffer.Allocate(65536).Order(ByteOrder.LITTLE_ENDIAN); p.SetBuf(b); Invoke(p, "WriteImpl", con); var result = new byte[b.Position()]; b.Flip(); b.Get(result); return result; }
    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(RealStaticData.RepoRoot(), "parity-artifacts/golden/formulas/Looting.54880a691.json")));
    private static object? Get(object obj, string name) => Field(obj.GetType(), name).GetValue(obj);
    private static void Set(object obj, string name, object? value) => Field(obj.GetType(), name).SetValue(obj, value);
    private static FieldInfo Field(Type t, string name) { for (Type? type = t; type != null; type = type.BaseType) { var f = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly); if (f != null) return f; } throw new MissingFieldException(t.Name, name); }
    private static object? Invoke(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(obj, args);

    private sealed class Scope : IDisposable
    {
        internal PvpExpPortTests.Scope Common { get; } = new(); internal Player P { get; } internal Player Other { get; } internal Player Third { get; }
        private readonly object? previousShutdown; private readonly List<(int Id, DropNpc? Previous, HashSet<DropItem>? Items)> records = [];
        private readonly FieldInfo shutdown = typeof(ShutdownHook).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        internal ConcurrentDictionary<int, HashSet<DropItem>> Items => DropRegistrationService.GetInstance().GetCurrentDropMap();
        internal ConcurrentDictionary<int, KnownObject> Known => (ConcurrentDictionary<int, KnownObject>)Get(P.GetKnownList(), "KnownObjects")!;
        internal AionServerPacket[] Packets => ((PvpExpPortTests.Connection)P.GetClientConnection()).Packets;
        internal Scope()
        {
            previousShutdown = shutdown.GetValue(null); shutdown.SetValue(null, null);
            using var xml = new StringReader("<item_templates><item_template id='162000052' name='Junk' level='1' quality='JUNK' max_stack_count='10' mask='6'/><item_template id='182400001' name='Kinah' level='1' quality='COMMON' max_stack_count='2147483647'/></item_templates>"); var data = (ItemData)new XmlSerializer(typeof(ItemData)).Deserialize(xml)!; data.AfterUnmarshal(null!); Set(Common.H.Data.StaticData, "<ItemDataDh>k__BackingField", data);
            P = Common.Player(65); Other = Common.Player(65); Third = Common.Player(65); Clear();
        }
        internal void Clear() { foreach (var p in new[] { P, Other, Third }) ((PvpExpPortTests.Connection)p.GetClientConnection()).Clear(); }
        internal void Know(VisibleObject obj) => Known[obj.GetObjectId()] = new KnownObject(obj);
        internal DropItem DropItem() { var item = new DropItem(new Drop(162000052, 1, 1, 100)); item.SetCount(1); return item; }
        internal (Npc Npc, DropNpc Drop, DropItem Item) Corpse()
        {
            var npc = Common.H.SpawnWithAi(203620, "postbox", P.GetX(), P.GetY(), P.GetZ()); Know(npc); var map = DropRegistrationService.GetInstance().GetDropRegistrationMap(); records.Add((npc.GetObjectId(), map.GetValueOrDefault(npc.GetObjectId()), Items.GetValueOrDefault(npc.GetObjectId())));
            var drop = new DropNpc(npc.GetObjectId()); drop.SetAllowedLooters(new HashSet<int> { P.GetObjectId(), Other.GetObjectId(), Third.GetObjectId() }); var item = DropItem(); map[npc.GetObjectId()] = drop; Items[npc.GetObjectId()] = [item]; Clear(); return (npc, drop, item);
        }
        internal PlayerGroup Group(params Player[] players) { var g = new PlayerGroup(new PlayerGroupMember(players[0]), TeamType.GROUP, 900010); foreach (var p in players) g.AddMember(new PlayerGroupMember(p)); return g; }
        internal PlayerAlliance Alliance(params Player[] players) { var g = new PlayerAlliance(new PlayerAllianceMember(players[0]), TeamType.ALLIANCE); foreach (var p in players) g.AddMember(new PlayerAllianceMember(p)); return g; }
        public void Dispose()
        {
            foreach (var r in records) { var map = DropRegistrationService.GetInstance().GetDropRegistrationMap(); if (r.Previous == null) map.TryRemove(r.Id, out _); else map[r.Id] = r.Previous; if (r.Items == null) Items.TryRemove(r.Id, out _); else Items[r.Id] = r.Items; }
            Common.Dispose(); shutdown.SetValue(null, previousShutdown);
        }
    }
}
