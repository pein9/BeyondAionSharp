using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml.Serialization;
using Aion.Commons.Logging;
using Aion.Commons.Nio;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine.Bounding;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.GeoEngine.Models;
using Aion.GameServer.GeoEngine.Scene;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.Players.Emotion;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Staticdoor;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Aion.GameServer.World.Geo;
using Aion.GameServer.World.Knownlist;
using Aion.GameServer.World.Zone;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class StaticInteractionRangePortTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private const int Map = 300100000;
    [Theory]
    [InlineData("lookup")] [InlineData("door")] [InlineData("chair")]
    public void MatchesExactJavaDoorMethodsAndChairGuard(string kind)
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(PvpExpPortTests.Root(), "parity-artifacts/golden/formulas/StaticInteractionRange.33aa13cb6.json")));
        using var s = new Scope();
        foreach (var c in fixture.RootElement.GetProperty("cases").EnumerateArray().Where(c => c.GetProperty("kind").GetString() == kind))
        {
            var a = c.GetProperty("args").EnumerateArray().ToArray(); var expected = c.GetProperty("expected").EnumerateArray().Select(v => v.GetString()!).ToArray(); string mode = a[0].GetString()!;
            if (kind != "chair")
            {
                s.Geometry(mode);
                if (kind == "lookup")
                {
                    var bounds = GeoService.GetInstance().GetDoorBounds(Map, 7);
                    Assert.Equal(expected[0], bounds == null ? "null" : ReferenceEquals(bounds, s.Closed) ? "closed" : "open");
                    continue;
                }
                s.P.GetPosition().SetXYZH(F(a[1]), F(a[2]), F(a[3]), 0); int world = a[4].GetInt32(), instance = a[5].GetInt32();
                var region = s.InstanceRegion;
                if (instance != 1)
                {
                    var parent = (WorldMapInstance)RuntimeHelpers.GetUninitializedObject(s.InstanceRegion.GetParent().GetType()); Set(parent, "instanceId", instance);
                    region = (MapRegion)Activator.CreateInstance(typeof(MapRegion), Fields, null, [0, parent, Array.Empty<ZoneInstance>()], null)!;
                }
                s.Door.SetPosition(new WorldPosition(world, 500, 500, 500, 0, region));
                Assert.Equal(bool.Parse(expected[0]), (bool)Call(StaticDoorService.GetInstance(), "IsNearDoor", s.P, s.Door, 7)!);
            }
            else
            {
                s.P.GetPosition().SetXYZH(F(a[1]), F(a[2]), F(a[3]), 0); s.P.SetState(mode == "CHAIR_UP" ? CreatureState.CHAIR : CreatureState.ACTIVE, true);
                s.Clear(); s.Emote(mode == "CHAIR_SIT" ? EmotionType.CHAIR_SIT : mode == "CHAIR_UP" ? EmotionType.CHAIR_UP : EmotionType.JUMP, F(a[4]), F(a[5]), F(a[6]));
                Assert.Equal(bool.Parse(expected[0]), s.Controller.Cancels > 0); Assert.Equal(bool.Parse(expected[1]), s.Audit.Count > 0);
            }
        }
    }

    [Theory]
    [InlineData("none")] [InlineData("00")] [InlineData("n1")]
    public void MissingClosedBoundsUsePositionRatherThanOpenGeometry(string mode)
    {
        using var s = new Scope(); s.Geometry(mode); Assert.Null(s.Geo.GetDoorBounds(7));
        s.P.GetPosition().SetXYZH(519.999f, 500, 500, 0); s.Open(); Assert.True(s.Door.IsOpen()); Assert.Equal(1, s.Handler.Opened);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MissingGeometryRejectsTwentyMetersBeforeConsumingAKeyOrChangingDoor(bool admin)
    {
        using var s = new Scope(key: 162000052); if (admin) s.P.GetAccount().SetAccessLevel(10);
        s.P.GetPosition().SetXYZH(520, 500, 500, 0); s.Clear(); s.Open();
        Assert.False(s.Door.IsOpen()); Assert.True(s.Door.IsLocked()); Assert.Equal(2, s.Key.GetItemCount()); Assert.Equal(0, s.Handler.Opened); Assert.Equal(1, s.Audit.Count);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NearbyDoorsKeepExistingKeyConsumptionAndAdminBypass(bool admin)
    {
        using var s = new Scope(key: 162000052); if (admin) s.P.GetAccount().SetAccessLevel(10);
        s.P.GetPosition().SetXYZH(519.999f, 500, 500, 0); s.Open();
        Assert.True(s.Door.IsOpen()); Assert.Equal(admin ? 2 : 1, s.Key.GetItemCount()); Assert.Equal(admin, s.Door.IsLocked()); Assert.Equal(1, s.Handler.Opened);
        s.Open(); Assert.Equal(1, s.Handler.Opened); Assert.Equal(admin ? 2 : 1, s.Key.GetItemCount());
    }

    [Theory]
    [InlineData(135, 207, 315, true)] [InlineData(135.001f, 200, 300, false)]
    [InlineData(100, 207.001f, 300, false)] [InlineData(100, 200, 315.001f, false)]
    public void GeometryDistanceUsesInclusiveExpandedBoxOnEveryAxis(float x, float y, float z, bool success)
    {
        using var s = new Scope(); s.Geometry("11"); s.P.GetPosition().SetXYZH(x, y, z, 0); s.Clear(); s.Open();
        Assert.Equal(success, s.Door.IsOpen()); Assert.Equal(success ? 1 : 0, s.Handler.Opened); Assert.Equal(success ? 0 : 1, s.Audit.Count);
        // Bounds can be far from the static position; do not replace this with a center-distance check.
        Assert.Equal(success, s.ClosedNode.IsActive(1) == false); // opening deactivates closed-state geometry
    }

    [Theory]
    [InlineData(EmotionType.CHAIR_SIT, 9.999f, true)] [InlineData(EmotionType.CHAIR_SIT, 10, false)]
    [InlineData(EmotionType.CHAIR_UP, 9.999f, true)] [InlineData(EmotionType.CHAIR_UP, 10, false)]
    public void ChairsRefuseAtTenMetersBeforeCancellationStateBroadcastOrProtectionChanges(EmotionType type, float distance, bool success)
    {
        using var s = new Scope(); s.P.GetPosition().SetXYZH(100, 200, 300, 0); s.P.SetState(type == EmotionType.CHAIR_UP ? CreatureState.CHAIR : CreatureState.ACTIVE, true);
        s.P.SetVisualState(CreatureVisualState.BLINKING); int state = s.P.GetState(); s.Clear(); s.Emote(type, 100 + distance, 200, 300);
        Assert.Equal(success ? 1 : 0, s.Controller.Cancels); Assert.Equal(success ? 1 : 0, s.Controller.ItemCancels); Assert.Equal(!success, s.P.IsProtectionActive());
        Assert.Equal(success, s.Packets.OfType<SM_EMOTION>().Any());
        if (success) Assert.True(s.P.IsInState(type == EmotionType.CHAIR_SIT ? CreatureState.CHAIR : CreatureState.ACTIVE)); else Assert.Equal(state, s.P.GetState());
    }

    [Theory]
    [InlineData(EmotionType.CHAIR_SIT)] [InlineData(EmotionType.CHAIR_UP)]
    public void ChairsUseThreeDimensionalDistance(EmotionType type)
    {
        using var s = new Scope(); s.P.GetPosition().SetXYZH(100, 200, 300, 0); s.Clear(); s.Emote(type, 100, 200, 310);
        Assert.Equal(0, s.Controller.Cancels); Assert.Empty(s.Packets.OfType<SM_EMOTION>()); Assert.Equal(1, s.Audit.Count);
    }

    [Fact]
    public void ExistingPrivateStoreGateStillPrecedesChairRangeAudit()
    {
        using var s = new Scope(); s.P.SetState(CreatureState.PRIVATE_SHOP); s.Clear(); s.Emote(EmotionType.CHAIR_SIT, float.PositiveInfinity, 0, 0);
        Assert.Equal(0, s.Audit.Count); Assert.Equal(0, s.Controller.Cancels); Assert.Empty(s.Packets.OfType<SM_EMOTION>());
    }

    private static float F(JsonElement value) => BitConverter.Int32BitsToSingle(unchecked((int)uint.Parse(value.GetString()!, NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
    private static FieldInfo Field(Type type, string name) { for (Type? t = type; t != null; t = t.BaseType) { var f = t.GetField(name, Fields | BindingFlags.DeclaredOnly); if (f != null) return f; } throw new MissingFieldException(type.Name, name); }
    private static object? Get(object obj, string name) => Field(obj.GetType(), name).GetValue(obj);
    private static void Set(object obj, string name, object? value) => Field(obj.GetType(), name).SetValue(obj, value);
    private static object? Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Fields)!.Invoke(obj, args);
    private sealed class Controller : PlayerController
    {
        public int Cancels, ItemCancels;
        public override void CancelCurrentSkill(Creature lastAttacker) => Cancels++;
        public override void CancelUseItem() => ItemCancels++;
    }
    private sealed class Handler(WorldMapInstance instance) : GeneralInstanceHandler(instance) { public int Opened; public override void OnOpenDoor(int door) => Opened++; }
    private sealed class Audit : ILoggerFactory
    {
        public int Count;
        public ILogger CreateLogger(string categoryName) => new Sink(this, categoryName);
        public void AddProvider(ILoggerProvider provider) { }
        public void Dispose() { }
        private sealed class Sink(Audit owner, string name) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? e, Func<TState, Exception?, string> formatter) { if (name == "AUDIT_LOG") owner.Count++; }
        }
    }
    private sealed class Scope : IDisposable
    {
        public PvpExpPortTests.Scope Common { get; } = new(); public Player P { get; }
        public Controller Controller { get; } = new(); public Handler Handler { get; } public Audit Audit { get; } = new();
        public StaticDoor Door { get; } public Item Key { get; }
        public MapRegion InstanceRegion { get; }
        public GeoMap Geo { get; } = new(Map);
        public BoundingBox Closed { get; } = new(new Vector3f(100, 200, 300), 30, 2, 10);
        public BoundingBox OpenBox { get; } = new(new Vector3f(500, 500, 500), 1, 1, 1);
        public DespawnableNode ClosedNode { get; } = new();
        public AionServerPacket[] Packets => ((PvpExpPortTests.Connection)P.GetClientConnection()).Packets;
        private readonly IDictionary maps; private readonly Dictionary<int, object?> oldMaps = new(); private readonly object? oldHandler;
        private readonly IDisposable logs; private readonly bool oldAudit = LoggingConfig.LOG_AUDIT, oldPunish = PunishmentConfig.PUNISHMENT_ENABLE;
        public Scope(int key = 0)
        {
            P = Common.Player(65); InstanceRegion = P.GetPosition().GetMapRegion()!; Controller.SetOwner(P); Set(P, "_controller", Controller);
            Set(P, "emotions", new EmotionList(P)); Handler = new Handler(InstanceRegion.GetParent()); oldHandler = Get(InstanceRegion.GetParent(), "instanceHandler"); Set(InstanceRegion.GetParent(), "instanceHandler", Handler);
            maps = (IDictionary)Get(GeoService.GetInstance(), "geoMaps")!; foreach (int id in new[] { Map, Map + 1 }) { oldMaps[id] = maps.Contains(id) ? maps[id] : null; maps[id] = Geo; }
            using var xml = new StringReader("<item_templates><item_template id='162000052' name='Key' level='1' quality='JUNK' max_stack_count='10' mask='6'/></item_templates>");
            var items = (ItemData)new XmlSerializer(typeof(ItemData)).Deserialize(xml)!; items.AfterUnmarshal(null!); Set(Common.H.Data.StaticData, "<ItemDataDh>k__BackingField", items);
            Key = new Item(800001, items.GetItemTemplate(162000052)); Key.SetItemCount(2); P.GetInventory().OnLoadHandler(Key);
            var spawn = new SpawnTemplate(new SpawnGroup(Map, 0, 0, null), new SpawnSpotTemplate { X = 500, Y = 500, Z = 500, StaticId = 7 });
            Door = new StaticDoor(new StaticObjectController(), spawn, new StaticDoorTemplate { id = 7, keyId = key, state = 10 }, 1);
            Door.SetKnownlist(new KnownList(Door));
            Door.SetPosition(new WorldPosition(Map, 500, 500, 500, 0, InstanceRegion)); InstanceRegion.GetParent().AddObject(Door);
            logs = AionLog.OverrideFactory(Audit); LoggingConfig.LOG_AUDIT = true; PunishmentConfig.PUNISHMENT_ENABLE = false; Clear();
        }
        public void Geometry(string mode)
        {
            var doors = (IDictionary)Get(Geo, "despawnableDoors")!; doors.Clear(); if (mode == "none") return;
            Set(ClosedNode, "worldBound", mode[0] == 'n' ? null : Closed); ClosedNode.SetActive(1, true);
            var open = new DespawnableNode(); Set(open, "worldBound", OpenBox);
            doors[7] = new DespawnableNode?[] { mode[0] == '0' ? null : ClosedNode, mode[1] == '0' ? null : open };
        }
        public void Clear() { ((PvpExpPortTests.Connection)P.GetClientConnection()).Clear(); Controller.Cancels = 0; Controller.ItemCancels = 0; Audit.Count = 0; }
        public void Open() => StaticDoorService.GetInstance().OpenStaticDoor(P, 7);
        public void Emote(EmotionType type, float x, float y, float z)
        {
            using var bytes = new MemoryStream(); using var w = new BinaryWriter(bytes); w.Write((byte)type); if (type is EmotionType.CHAIR_SIT or EmotionType.CHAIR_UP) { w.Write(x); w.Write(y); w.Write(z); w.Write((byte)231); }
            var p = new CM_EMOTION(0, new HashSet<AionConnection.State>()); p.SetConnection(P.GetClientConnection()); p.SetBuffer(ByteBuffer.Wrap(bytes.ToArray()).Order(ByteOrder.LITTLE_ENDIAN)); Assert.True(p.Read()); Call(p, "RunImpl");
        }
        public void Dispose()
        {
            InstanceRegion.GetParent().RemoveObject(Door); Set(InstanceRegion.GetParent(), "instanceHandler", oldHandler);
            foreach (var (id, old) in oldMaps) { if (old == null) maps.Remove(id); else maps[id] = old; }
            LoggingConfig.LOG_AUDIT = oldAudit; PunishmentConfig.PUNISHMENT_ENABLE = oldPunish; logs.Dispose(); Common.Dispose();
        }
    }
}
