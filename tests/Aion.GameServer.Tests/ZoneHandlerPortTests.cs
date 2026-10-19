using System.Reflection;
using System.Runtime.CompilerServices;
using Aion.GameServer.Controllers;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.Siege;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;
using Aion.GameServer.World.Zone.Handler;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class ZoneHandlerPortTests
{
    [Fact]
    public void OrdinaryNpcsAreIgnoredButOwnedCreaturesAndSiegeNpcsKeepTheirRequiredZones()
    {
        var zone = new ZoneInstance(1, Info());
        var npc = Bare<Npc>();
        var siegeNpc = Bare<SiegeNpc>();
        Assert.True(zone.IsIgnored(npc));
        Assert.True(zone.IsIgnored(siegeNpc));
        Assert.False(zone.IsIgnored(Bare<Player>()));
        Assert.False(zone.IsIgnored(Bare<Summon>()));
        Assert.False(zone.IsIgnored(Bare<SummonedObject>()));
        var siege = new SiegeZoneInstance(1, Info());
        Assert.False(siege.IsIgnored(siegeNpc));
        Assert.True(siege.IsIgnored(npc));

        zone.AddHandler(new ZoneProbeHandler());
        Assert.True(zone.IsIgnored(npc));
        zone.AddHandler(Bare<MaterialZoneHandler>());
        Assert.False(zone.IsIgnored(npc)); // Material hazards must still affect ordinary NPCs.
    }

    [Fact]
    public void MapRegionSkipsIrrelevantNpcsBeforeEnteringOrLeavingTheirZones()
    {
        var zone = new ObservedZone(Info());
        var region = Bare<MapRegion>();
        SetField(typeof(MapRegion), region, "zonesSortedByTypeAndPriority", new ZoneInstance[] { zone });
        var npc = Bare<Npc>();
        npc.SetPosition(new WorldPosition(1));
        region.RevalidateZones(npc);
        Assert.Equal(0, zone.EnterCalls);
        Assert.Equal(0, zone.LeaveCalls);
    }

    [Fact]
    public void EnterAndLeaveNotifyNonPlayerControllersExactlyOnce()
    {
        var zone = new ZoneInstance(1, Info());
        var controller = new ObservedNpcController();
        var npc = Bare<Npc>();
        SetField(typeof(VisibleObject), npc, "_controller", controller);
        Assert.True(zone.OnEnter(npc));
        Assert.False(zone.OnEnter(npc));
        Assert.Equal(1, controller.Entries);
        Assert.True(zone.OnLeave(npc));
        Assert.False(zone.OnLeave(npc));
        Assert.Equal(1, controller.Leaves);
    }

    [Fact]
    public void UnknownZonesHaveNoDummyHandlerAndRegisteredHandlersStillInstantiate()
    {
        var service = Bare<ZoneService>();
        SetField(typeof(ZoneService), service, "zoneHandlers", new Dictionary<string, Type>());
        using var scope = new ZoneCleanupPortTests.DataScope(Info().GetZoneTemplate());
        var name = "PORT_ZONE_TRACKING";
        Assert.Null(service.GetNewZoneHandler(name));
        service.AddZoneHandlerClass(typeof(ZoneProbeHandler));
        Assert.IsType<ZoneProbeHandler>(service.GetNewZoneHandler(name));
    }

    [ZoneHandlerArea("PORT_ZONE_TRACKING")]
    public sealed class ZoneProbeHandler : IZoneHandler
    {
        public void OnEnterZone(Creature creature, ZoneInstance zone) { }
        public void OnLeaveZone(Creature creature, ZoneInstance zone) { }
    }

    private sealed class ObservedNpcController : NpcController
    {
        public int Entries;
        public int Leaves;
        public override void OnEnterZone(ZoneInstance zone) => Entries++;
        public override void OnLeaveZone(ZoneInstance zone) => Leaves++;
    }

    private sealed class ObservedZone(ZoneInfo info) : ZoneInstance(1, info)
    {
        public int EnterCalls;
        public int LeaveCalls;
        public override bool OnEnter(Creature creature) { EnterCalls++; return false; }
        public override bool OnLeave(Creature creature) { LeaveCalls++; return false; }
    }

    private static ZoneInfo Info()
    {
        var template = new ZoneTemplate { XmlName = "PORT_ZONE_TRACKING", Mapid = 1,
            AreaType = AreaType.Sphere, Sphere = new Sphere(0, 0, 0, 1) };
        return new ZoneInfo(template);
    }

    private static T Bare<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    private static void SetField(Type type, object instance, string name, object value) =>
        type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(instance, value);
}
