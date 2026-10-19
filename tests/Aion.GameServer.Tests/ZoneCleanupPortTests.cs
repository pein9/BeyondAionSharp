using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using Aion.GameServer.Controllers;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine.Bounding;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.GeoEngine.Scene;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.LegionDominion;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Materials;
using Aion.GameServer.Model.Templates.Vortex;
using Aion.GameServer.Model.Templates.World;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;
using Aion.GameServer.World.Zone.Handler;
using Engine = Aion.GameServer.QuestEngine.QuestEngine;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class ZoneCleanupPortTests
{
    [Fact]
    public void StaticNamesAreCaseSensitiveAndDuplicatesAreRejectedAcrossMaps()
    {
        using var scope = new DataScope(Template("MixedCase"), Template("MIXEDCASE"));
        Assert.Equal(2, DataManager.ZONE_DATA.Size());
        Assert.True(DataManager.ZONE_DATA.IsValidZoneName("MixedCase"));
        Assert.False(DataManager.ZONE_DATA.ValidateZoneName("mixedcase"));
        Assert.True(new ZoneInfo(Template("MixedCase")).Matches("MixedCase"));
        Assert.False(new ZoneInfo(Template("MixedCase")).Matches("MIXEDCASE"));
        Assert.Empty(DataManager.ZONE_DATA.GetZones(999));
        var duplicate = new ZoneData { zoneList = new() { Template("same", 1), Template("same", 2) } };
        Assert.Equal("Duplicate zone name: same", Assert.Throws<ArgumentException>(() => duplicate.AfterUnmarshal(null!)).Message);
    }

    [Fact]
    public void WeatherIdsAreOrderedWithinEachMapAndGeometryComesFromTheTemplate()
    {
        var first = Template("weather1"); first.ZoneType = ZoneClassName.WEATHER;
        var second = Template("weather2"); second.ZoneType = ZoneClassName.WEATHER;
        var other = Template("weather3", 2); other.ZoneType = ZoneClassName.WEATHER;
        using var scope = new DataScope(first, Template("sub"), second, other);
        Assert.Equal(1, DataManager.ZONE_DATA.GetWeatherZoneId(first));
        Assert.Equal(2, DataManager.ZONE_DATA.GetWeatherZoneId(second));
        Assert.Equal(1, DataManager.ZONE_DATA.GetWeatherZoneId(other));
        Assert.Null(DataManager.ZONE_DATA.zoneList);
        Assert.True(DataManager.ZONE_DATA.GetZones(1)[0].GetArea().IsInside3D(128, 128, 128));
        Assert.Throws<ArgumentException>(() => new SphereArea(0, 0, 0, 0));
        Assert.Throws<ArgumentException>(() => new SphereArea(0, 0, 0, -1));
        Assert.Throws<InvalidOperationException>(() => Area.Create(new ZoneTemplate
            { AreaType = AreaType.Sphere, Sphere = new Sphere { X = 0, Y = 0, Z = 0 } }));
    }

    [Fact]
    public void MaterialAreasKeepGeometryNamesAndDefaultFlagsAndStayOutOfStaticData()
    {
        using var scope = new DataScope(Template("static"));
        var geometry = new MaterialGeometry("MATERIAL_42");
        var template = new MaterialZoneTemplate(geometry, 1);
        Assert.Equal("MATERIAL_42", template.GetName());
        Assert.Equal(-1, template.GetFlags());
        var service = NewService();
        service.CreateMaterialZoneTemplate(geometry, 1);
        var first = service.CreateZoneInstances(1);
        var second = service.CreateZoneInstances(1);
        var left = Assert.Single(first, z => z.GetZoneTemplate() is MaterialZoneTemplate);
        var right = Assert.Single(second, z => z.GetZoneTemplate() is MaterialZoneTemplate);
        Assert.NotSame(left, right);
        Assert.Same(left.GetZoneTemplate(), right.GetZoneTemplate());
        Assert.Same(Handlers(left).Single(), Handlers(right).Single());
        Assert.Equal(1, DataManager.ZONE_DATA.Size());
        Assert.False(DataManager.ZONE_DATA.IsValidZoneName("MATERIAL_42"));
        Assert.Single(DataManager.ZONE_DATA.GetZones(1));
        Assert.Equal(3, first.Count); // Full map, static zone, material area.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegionsShareZoneTrackingWithinOneInstanceButNotAcrossInstances(bool threeDimensional)
    {
        using var scope = new DataScope(Template("shared"));
        var world = Bare<WorldMap>();
        SetField(world, "worldMapTemplate", DataManager.WORLD_MAPS_DATA.GetTemplate(1));
        WorldMapInstance Make(int id) => threeDimensional
            ? new WorldMap3DInstance(world, id, 10, i => new GeneralInstanceHandler(i))
            : new WorldMap2DInstance(world, id, 0, 10, i => new GeneralInstanceHandler(i));
        var first = Make(1); var second = Make(2);
        var near = Zone(first.GetRegion(10, 10, 10));
        var far = Zone(first.GetRegion(200, 200, 10));
        Assert.Same(near, far);
        var isolated = Zone(second.GetRegion(10, 10, 10));
        Assert.NotSame(near, isolated);
        var npc = Bare<Npc>();
        SetField(typeof(VisibleObject), npc, "_controller", new NpcController());
        Assert.True(near.OnEnter(npc));
        Assert.True(far.IsInsideCreature(npc));
        Assert.False(isolated.IsInsideCreature(npc));

        static ZoneInstance Zone(MapRegion region) => Assert.Single(
            (ZoneInstance[])typeof(MapRegion).GetField("zonesSortedByTypeAndPriority", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(region)!,
            z => z.GetZoneTemplate().GetName() == "shared");
    }

    [Fact]
    public void QuestRegistrationRejectsUnknownNamesAndDispatchesTheActualZone()
    {
        using var scope = new DataScope(Template("quest"));
        var engine = (Engine)Activator.CreateInstance(typeof(Engine), nonPublic: true)!;
        var handler = Bare<QuestProbe>();
        SetField(typeof(AbstractQuestHandler), handler, "questId", 901);
        engine.AddQuestHandler(handler);
        foreach (var register in new Action<string, int>[] { engine.RegisterOnEnterZone, engine.RegisterOnLeaveZone, engine.RegisterOnKillInZone })
        {
            register("quest", 901);
            register("missing", 901);
        }
        var zone = new ZoneInstance(1, new ZoneInfo(Template("quest")));
        var env = new QuestEnv(null!, null!, 0);
        engine.OnEnterZone(env, zone); engine.OnLeaveZone(env, zone); engine.OnKillInZone(env, zone);
        Assert.Equal(3, handler.Calls);
        Assert.Same(zone, handler.LastZone);
        Assert.Equal(901, env.GetQuestId());
        var missing = new ZoneInstance(1, new ZoneInfo(Template("missing")));
        engine.OnEnterZone(env, missing); engine.OnLeaveZone(env, missing); engine.OnKillInZone(env, missing);
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public void VortexMembershipUsesConfiguredZonesAndExactCase()
    {
        using var scope = new DataScope(Template("MixedCase"), Template("MIXEDCASE"));
        var template = new VortexTemplate { id = 9, ZonesRaw = "MixedCase", start = new StartPoint { map = 1 } };
        var vortex = new Aion.GameServer.Model.Vortex.VortexLocation(template);
        DataManager.VORTEX_DATA.GetVortexLocations().Add(9, vortex);
        var zones = NewService().CreateZoneInstances(1);
        var selected = Assert.Single(zones, z => z.GetZoneTemplate().GetName() == "MixedCase");
        Assert.IsType<InvasionZoneInstance>(selected);
        Assert.Contains(vortex, Handlers(selected));
        Assert.IsNotType<InvasionZoneInstance>(Assert.Single(zones, z => z.GetZoneTemplate().GetName() == "MIXEDCASE"));
    }

    [Fact]
    public void NamedHandlersRegisterOnlyExactStaticNames()
    {
        using var scope = new DataScope(Template("MixedCase"));
        var service = NewService();
        service.AddZoneHandlerClass(typeof(NamedHandler));
        Assert.IsType<NamedHandler>(service.GetNewZoneHandler("MixedCase"));
        Assert.Null(service.GetNewZoneHandler("MIXEDCASE"));
        Assert.Null(service.GetNewZoneHandler("missing"));
    }

    [Fact]
    public void ItemAreaPrefixesAreCaseSensitiveAndFortressWildcardIgnoresOtherZoneTypes()
    {
        using var scope = new DataScope(Template("MixedCase_1"));
        var zone = new ZoneInstance(1, new ZoneInfo(Template("MixedCase_1")));
        var creature = Bare<Npc>();
        SetField(typeof(VisibleObject), creature, "_controller", new NpcController());
        zone.OnEnter(creature);
        var region = new MapRegion(1, null!, new[] { zone });
        Assert.True(region.IsInsideItemUseZone("MixedCase", creature));
        Assert.False(region.IsInsideItemUseZone("MIXEDCASE", creature));
        Assert.False(region.IsInsideItemUseZone("_ABYSS_CASTLE_AREA_", creature));
        zone.GetZoneTemplate().ZoneType = ZoneClassName.FORT;
        Assert.True(region.IsInsideItemUseZone("_ABYSS_CASTLE_AREA_", creature));
    }

    [Fact]
    public void VortexZoneListsBindAsExactNamesAndLegionNamesAreNormalizedAtCreation()
    {
        using var input = new StringReader("<Vortex zones=\"MixedCase OTHER MixedCase\" />");
        var template = (VortexTemplate)new XmlSerializer(typeof(VortexTemplate)).Deserialize(input)!;
        Assert.Equal(2, template.GetZones().Count);
        Assert.Contains("MixedCase", template.GetZones());
        Assert.DoesNotContain("MIXEDCASE", template.GetZones());
        var location = new LegionDominionLocation(new LegionDominionLocationTemplate { zone = "MixedCase", worldId = 1 });
        Assert.Equal("MIXEDCASE_1", location.GetZoneName());
    }

    private sealed class QuestProbe : AbstractQuestHandler
    {
        private QuestProbe() : base(901) { }
        public int Calls;
        public ZoneInstance? LastZone;
        public override void Register() { }
        private bool Record(ZoneInstance zone) { Calls++; LastZone = zone; return true; }
        public override bool OnEnterZoneEvent(QuestEnv env, ZoneInstance zone) => Record(zone);
        public override bool OnLeaveZoneEvent(QuestEnv env, ZoneInstance zone) => Record(zone);
        public override bool OnKillInZoneEvent(QuestEnv env, ZoneInstance zone) => Record(zone);
    }

    [ZoneNameAnnotation("MixedCase  MIXEDCASE missing ")]
    public sealed class NamedHandler : IZoneHandler
    {
        public void OnEnterZone(Creature creature, ZoneInstance zone) { }
        public void OnLeaveZone(Creature creature, ZoneInstance zone) { }
    }

    private sealed class MaterialGeometry : Geometry
    {
        public MaterialGeometry(string name) : base(name, new Mesh()) => worldBound = new BoundingBox(new Vector3f(128, 128, 128), 10, 10, 10);
        public override int GetMaterialId() => 42;
    }

    private static ZoneTemplate Template(string name, int map = 1) => new()
        { XmlName = name, Mapid = map, AreaType = AreaType.Sphere, Sphere = new Sphere(128, 128, 128, 300) };
    private static List<IZoneHandler> Handlers(ZoneInstance zone) => (List<IZoneHandler>)typeof(ZoneInstance)
        .GetField("Handlers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(zone)!;
    private static ZoneService NewService() => (ZoneService)Activator.CreateInstance(typeof(ZoneService), nonPublic: true)!;
    private static T Bare<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    private static void SetField(object instance, string name, object value) => SetField(instance.GetType(), instance, name, value);
    private static void SetField(Type type, object instance, string name, object value) =>
        type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);

    internal sealed class DataScope : IDisposable
    {
        private readonly DataManager? previous = DataManager.GetRegisteredInstance();
        public DataScope(params ZoneTemplate[] templates)
        {
            var data = Bare<StaticData>();
            var zones = new ZoneData { zoneList = templates.ToList() }; zones.AfterUnmarshal(null!);
            Set(nameof(StaticData.ZoneInfo), zones);
            var maps = new WorldMapsData();
            maps.SetData(new() { new() { MapId = 1, WorldSize = 256, FlagsRaw = "PVP" }, new() { MapId = 2, WorldSize = 256 } });
            Set(nameof(StaticData.WorldMaps2), maps);
            Set(nameof(StaticData.VortexDataDh), new VortexData());
            var materials = new MaterialData { materialTemplates = new() { new MaterialTemplate { id = 42, skills = new() } } };
            materials.AfterUnmarshal(null!); Set(nameof(StaticData.Materials), materials);
            DataManager.RegisterInstance((DataManager)Activator.CreateInstance(typeof(DataManager), BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null, args: new object[] { data }, culture: null)!);
            void Set(string property, object value) => typeof(StaticData).GetProperty(property)!.SetValue(data, value);
        }
        public void Dispose() => DataManager.RestoreInstance(previous);
    }
}
