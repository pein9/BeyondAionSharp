using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Aion.Commons.Logging;
using Aion.Commons.Nio;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Model.Team.League;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World.Knownlist;
using Aion.GameServer.World.Zone;
using Microsoft.Extensions.Logging;
using PlayerGroupMember = Aion.GameServer.Model.Team.Group.PlayerGroupMember;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class ClientBroadcastPortTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private const int Map = 300100000;

    [Theory]
    [InlineData("read")] [InlineData("writer")] [InlineData("run")]
    public void CompleteJavaClassesMatchReadersWritersAndDeclaredRecipientCases(string kind)
    {
        using var fixture = Fixture();
        foreach (var c in fixture.RootElement.GetProperty("cases").EnumerateArray().Where(c => c.GetProperty("kind").GetString() == kind))
        {
            var a = c.GetProperty("args").EnumerateArray().ToArray();
            string[] expected = c.GetProperty("expected").EnumerateArray().Select(v => v.GetString()!).ToArray();
            if (kind == "writer")
            {
                var writer = a[0].GetInt32() == 0 ? new SM_GROUP_DATA_EXCHANGE(a[1].GetInt32(), Data(a[2].GetInt32())) : new SM_GROUP_DATA_EXCHANGE(Data(a[2].GetInt32()));
                Assert.Equal(expected[0], Convert.ToHexString(Payload(writer))); continue;
            }
            using var s = new Scope();
            var packet = s.Read(Body(a[0].GetInt32(), a[1].GetInt32(), a[2].GetInt32(), a[3].GetInt32(), a[4].GetInt32()));
            if (kind == "read")
            {
                var data = (byte[]?)Get(packet, "data");
                string[] actual = [Id(Get(packet, "type")), Id(Get(packet, "team")), Get(packet, "tag")!.ToString()!, Get(packet, "dataSize")!.ToString()!, data == null ? "null" : Convert.ToHexString(SHA256.HashData(data)), packet.GetRemainingBytes().ToString(CultureInfo.InvariantCulture)];
                Assert.Equal(expected, actual);
            }
            else
            {
                string mode = a[5].GetString()!;
                // This oracle declares membership snapshots, including simultaneous group/alliance lists.
                // Separate tests below use ordinary seven-person alliances and two-alliance leagues.
                if (mode != "none")
                {
                    s.Group(s.Players[0], s.Players[1]); var first = s.Alliance(s.Players[0], s.Players[1], s.Players[2]);
                    if (mode == "league") s.League(first, s.Alliance(s.Players[3]));
                }
                s.Houses(a[6].GetString()!); s.Clear();
                if (a[7].GetInt32() == 0) s.Connection(s.P).SetActivePlayer(null!);
                Run(packet);
                int[] ids = JsonSerializer.Deserialize<int[]>(expected[0])!;
                s.AssertRecipients(ids, expected[1]);
                Assert.Equal(expected[2], string.Join("", s.Log.AuditMessages));
            }
        }
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void HouseTypesReadOnlySizeAndDataAndCanonicalizeOutput(int type)
    {
        using var s = new Scope(); s.Houses("HOUSE_A"); s.Clear(); var packet = s.Read(Body(type, 0, 231, 3, 3));
        Assert.Equal(0, packet.GetRemainingBytes()); Assert.Null(Get(packet, "team")); Assert.Equal(0, Get(packet, "tag")); Run(packet);
        s.AssertRecipients([1, 2], Convert.ToHexString(Payload(new SM_GROUP_DATA_EXCHANGE(Data(3)))));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void HouseDataOutsideALandIsRefusedEvenForUncheckedType(int type)
    {
        using var s = new Scope(); s.Houses(""); s.Clear(); Run(s.Read(Body(type, 0, 231, 3, 3))); s.AssertRecipients([], "");
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void TeamSelectionSendsOnlyToRequestedOnlineTeamAndNeverEchoes(int team)
    {
        using var s = new Scope();
        if (team == 0) s.Group(s.Players[0], s.Players[1]);
        else { var first = s.Alliance(s.Players[0], s.Players[1], s.Players[2]); if (team == 2) s.League(first, s.Alliance(s.Players[3])); }
        s.Clear(); Run(s.Read(Body(0, team, 231, 3, 3)));
        s.AssertRecipients(team == 0 ? [2] : team == 1 ? [2, 3] : [2, 3, 4], Convert.ToHexString(Payload(new SM_GROUP_DATA_EXCHANGE(231, Data(3)))));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AllianceAndLeagueBroadcastsReachOtherSubgroupsAndOtherAlliancesWithoutKnownListVisibility(bool league)
    {
        using var s = new Scope(count: 10); var first = s.Alliance(s.Players.Take(7).ToArray());
        Assert.NotSame(s.Players[0].GetPlayerAllianceGroup(), s.Players[6].GetPlayerAllianceGroup());
        if (league) s.League(first, s.Alliance(s.Players[7], s.Players[8]));
        s.Clear(); Run(s.Read(Body(0, league ? 2 : 1, 255, 3, 3)));
        s.AssertRecipients(Enumerable.Range(2, league ? 8 : 6).ToArray(), Convert.ToHexString(Payload(new SM_GROUP_DATA_EXCHANGE(255, Data(3)))));
    }

    [Fact]
    public void OfflineTeamMembersAndUnrequestedTeamTypesReceiveNothing()
    {
        using var s = new Scope(); s.Group(s.Players[0], s.Players[1], s.Players[2]); s.Players[2].SetClientConnection(null!);
        s.Clear(); Run(s.Read(Body(0, 0, 231, 3, 3))); Assert.Single(s.Connection(s.Players[1]).Packets.OfType<SM_GROUP_DATA_EXCHANGE>()); Assert.Empty(s.Connection(s.P).Packets.OfType<SM_GROUP_DATA_EXCHANGE>());
        s.Clear(); Run(s.Read(Body(0, 1, 231, 3, 3))); s.AssertRecipients([], "");
        s.Clear(); Run(s.Read(Body(0, 255, 231, 3, 3))); s.AssertRecipients([], "");
    }

    [Theory]
    [InlineData(-1, 0)] [InlineData(6145, 8)] [InlineData(int.MaxValue, 8)] [InlineData(3, 2)]
    public void InvalidOrUnavailableSizesAreNotAllocatedOrRelayedAndHaveBoundedAudit(int size, int available)
    {
        using var s = new Scope(); s.Group(s.Players[0], s.Players[1]); s.Clear(); var packet = s.Read(Body(0, 0, 231, size, available));
        Assert.Null(Get(packet, "data")); Assert.Equal(available, packet.GetRemainingBytes()); Run(packet); s.AssertRecipients([], "");
        Assert.Equal("sent client broadcast data of " + size + " bytes (type TEAM)", Assert.Single(s.Log.AuditMessages));
        Assert.DoesNotContain(s.Log.Entries, e => e.Category == nameof(CM_GROUP_DATA_EXCHANGE) && e.Level == LogLevel.Error);
        // BaseClientPacket's separate once-per-opcode leftover diagnostic remains native and is outside this packet's audit contract.
    }

    [Theory]
    [InlineData(0)] [InlineData(6144)]
    public void EmptyDataStaysSilentAndThe6144ByteLimitIsInclusive(int size)
    {
        using var s = new Scope(); s.Group(s.Players[0], s.Players[1]); s.Clear(); var packet = s.Read(Body(0, 0, 231, size, size));
        Assert.Equal(size, ((byte[])Get(packet, "data")!).Length); Assert.Equal(0, packet.GetRemainingBytes()); Run(packet);
        s.AssertRecipients(size == 0 ? [] : [2], size == 0 ? "" : Convert.ToHexString(Payload(new SM_GROUP_DATA_EXCHANGE(231, Data(size))))); Assert.Empty(s.Log.AuditMessages);
    }

    [Fact]
    public void UnknownTypesAndMissingPlayersStaySilent()
    {
        using var s = new Scope(); s.Clear(); Run(s.Read(Body(255, 0, 231, 3, 3))); s.AssertRecipients([], ""); Assert.Empty(s.Log.AuditMessages);
        var malformed = s.Read(Body(0, 0, 231, -1, 0)); s.Connection(s.P).SetActivePlayer(null!); Run(malformed); Assert.Empty(s.Log.AuditMessages);
    }

    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(PvpExpPortTests.Root(), "parity-artifacts/golden/formulas/ClientBroadcast.6dd3aa507.json")));
    private static byte[] Data(int size) => Enumerable.Range(0, size).Select(i => unchecked((byte)(i * 37 + 11))).ToArray();
    private static byte[] Body(int type, int team, int tag, int size, int available) { using var b = new MemoryStream(); using var w = new BinaryWriter(b); w.Write((byte)type); if (type == 0) { w.Write((byte)team); w.Write((byte)tag); } w.Write(size); w.Write(Data(available)); return b.ToArray(); }
    private static string Id(object? value) => value == null ? "null" : Convert.ToInt32(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
    private static FieldInfo Field(Type type, string name) { for (Type? t = type; t != null; t = t.BaseType) { var f = t.GetField(name, Fields | BindingFlags.DeclaredOnly); if (f != null) return f; } throw new MissingFieldException(type.Name, name); }
    private static object? Get(object obj, string name) => Field(obj.GetType(), name).GetValue(obj);
    private static void Set(object obj, string name, object? value) => Field(obj.GetType(), name).SetValue(obj, value);
    private static void Run(AionClientPacket packet) => packet.GetType().GetMethod("RunImpl", Fields)!.Invoke(packet, null);
    private static byte[] Payload(AionServerPacket packet) { var b = ByteBuffer.Allocate(20000).Order(ByteOrder.LITTLE_ENDIAN); packet.SetBuf(b); packet.GetType().GetMethod("WriteImpl", Fields)!.Invoke(packet, [null]); var bytes = new byte[b.Position()]; b.Flip(); b.Get(bytes); return bytes; }
    private sealed class FixedKnownList(VisibleObject owner, IEnumerable<Player> players) : KnownList(owner)
    {
        public void Declare() { foreach (var p in players) KnownObjects[p.ObjectId] = new KnownObject(p); }
        public override void Update() { }
    }
    private sealed class LogCapture : ILoggerFactory
    {
        public List<(string Category, LogLevel Level, object State)> Entries { get; } = [];
        public IEnumerable<string> AuditMessages => Entries.Where(e => e.Category == "AUDIT_LOG").Select(e => ((IEnumerable<KeyValuePair<string, object?>>)e.State).Single(v => v.Key == "Message").Value!.ToString()!);
        public ILogger CreateLogger(string categoryName) => new Sink(this, categoryName);
        public void AddProvider(ILoggerProvider provider) { }
        public void Dispose() { }
        private sealed class Sink(LogCapture owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter) => owner.Entries.Add((category, level, state!));
        }
    }
    private sealed class Scope : IDisposable
    {
        public PvpExpPortTests.Scope Common { get; } = new();
        public Player[] Players { get; }
        public Player P => Players[0];
        public LogCapture Log { get; } = new();
        private readonly Dictionary<int, PvpExpPortTests.Connection> connections = new();
        private readonly bool oldAudit = LoggingConfig.LOG_AUDIT, oldPunish = PunishmentConfig.PUNISHMENT_ENABLE;
        private readonly IDisposable logging;
        public Scope(int count = 5)
        {
            Players = Enumerable.Range(0, count).Select(_ => Common.Player(65)).ToArray();
            foreach (var p in Players) connections[p.ObjectId] = (PvpExpPortTests.Connection)p.GetClientConnection();
            var known = new FixedKnownList(P, [Players[1], Players[2], Players[4]]); known.Declare(); P.SetKnownlist(known);
            logging = AionLog.OverrideFactory(Log); LoggingConfig.LOG_AUDIT = true; PunishmentConfig.PUNISHMENT_ENABLE = false; Clear();
        }
        public PvpExpPortTests.Connection Connection(Player p) => connections[p.ObjectId];
        public void Clear() { foreach (var c in connections.Values) c.Clear(); Log.Entries.Clear(); }
        public CM_GROUP_DATA_EXCHANGE Read(byte[] bytes) { var p = new CM_GROUP_DATA_EXCHANGE(0, new HashSet<AionConnection.State>()); p.SetConnection(Connection(P)); p.SetBuffer(ByteBuffer.Wrap(bytes).Order(ByteOrder.LITTLE_ENDIAN)); Assert.True(p.Read()); return p; }
        public PlayerGroup Group(params Player[] members) { var team = new PlayerGroup(new PlayerGroupMember(members[0]), TeamType.GROUP, 900010); foreach (var p in members) team.AddMember(new PlayerGroupMember(p)); return team; }
        public PlayerAlliance Alliance(params Player[] members) { var team = new PlayerAlliance(new PlayerAllianceMember(members[0]), TeamType.ALLIANCE); foreach (var p in members) team.AddMember(new PlayerAllianceMember(p)); return team; }
        public void League(params PlayerAlliance[] alliances) { var leader = new LeagueMember(alliances[0], 0); var team = new League(leader); foreach (var (alliance, i) in alliances.Select((a, i) => (a, i))) team.AddMember(i == 0 ? leader : new LeagueMember(alliance, i)); }
        public void Houses(string orderedLands)
        {
            var names = orderedLands.Split(',', StringSplitOptions.RemoveEmptyEntries).Concat(new[] { "SUB", "HOUSE_A", "HOUSE_B", "house_A" }).Distinct().ToArray();
            var data = new ZoneData { zoneList = names.Select(name => new ZoneTemplate { XmlName = name, Mapid = Map, AreaType = AreaType.Sphere, Sphere = new Sphere(500, 500, 300, 1000) }).ToList() }; data.AfterUnmarshal(null!);
            Set(Common.H.Data.StaticData, "<ZoneInfo>k__BackingField", data);
            var zones = data.GetZones(Map).ToDictionary(info => info.GetZoneTemplate().GetName()!, info => new ZoneInstance(Map, info));
            foreach (string land in orderedLands.Split(',', StringSplitOptions.RemoveEmptyEntries)) Declare(zones[land], P);
            Declare(zones["HOUSE_A"], Players[1]); Declare(zones["HOUSE_B"], Players[2]); Declare(zones["HOUSE_A"], Players[3]);
            var ordered = orderedLands.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(n => zones[n]).Concat(zones.Where(k => !orderedLands.Split(',').Contains(k.Key)).Select(k => k.Value)).ToArray();
            Set(P.GetPosition().GetMapRegion()!, "zonesSortedByTypeAndPriority", ordered);
        }
        private static void Declare(ZoneInstance zone, Player player) => ((IDictionary)Get(zone, "Creatures")!)[player.ObjectId] = player;
        public void AssertRecipients(int[] ids, string payload)
        {
            foreach (var (player, index) in Players.Select((p, i) => (p, i + 1)))
            {
                var packets = Connection(player).Packets.OfType<SM_GROUP_DATA_EXCHANGE>().ToArray();
                if (ids.Contains(index)) Assert.Equal(payload, Convert.ToHexString(Payload(Assert.Single(packets)))); else Assert.Empty(packets);
            }
        }
        public void Dispose() { LoggingConfig.LOG_AUDIT = oldAudit; PunishmentConfig.PUNISHMENT_ENABLE = oldPunish; logging.Dispose(); Common.Dispose(); }
    }
}
