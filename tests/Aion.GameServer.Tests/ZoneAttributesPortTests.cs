using System.Reflection;
using System.Runtime.CompilerServices;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Model.Templates.World;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class ZoneAttributesPortTests
{
    [Fact]
    public void MapAttributesAreASetAndMissingFlagsAreEmpty()
    {
        var map = new WorldMapTemplate();
        Assert.False(map.HasAttribute(ZoneAttributes.Fly));
        map.FlagsRaw = "FLY PVP FLY DUEL_SAME_RACE NO_RETURN_BATTLE";
        Assert.True(map.HasAttribute(ZoneAttributes.Fly));
        Assert.True(map.HasAttribute(ZoneAttributes.PvpEnabled));
        Assert.True(map.HasAttribute(ZoneAttributes.DuelSameRaceEnabled));
        Assert.True(map.HasAttribute(ZoneAttributes.NoReturnBattle));
        Assert.False(map.HasAttribute(ZoneAttributes.Fly | ZoneAttributes.PvpEnabled));
        Assert.Equal("FLY PVP DUEL_SAME_RACE NO_RETURN_BATTLE", map.FlagsRaw);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(8, true)]
    [InlineData(64, false)]
    [InlineData(72, true)]
    public void ZoneAttributesUseExplicitBitsAndNeverTreatMinusOneAsAllFlags(int flags, bool fly)
        => Assert.Equal(fly, new ZoneTemplate { Flags = flags }.HasZoneAttribute(ZoneAttributes.Fly));

    [Theory]
    [InlineData("", false, false)]
    [InlineData("FLY", true, false)]
    [InlineData("PVP", false, true)]
    [InlineData("FLY PVP", true, true)]
    public void MapDefaultsApplyWithoutSyntheticZonesAndExplicitProhibitionsWin(string flags, bool fly, bool pvp)
    {
        var fixture = new Fixture(flags);
        var player = fixture.Player;
        Assert.Equal(fly, player.IsInsideFlyZone());
        Assert.Equal(pvp, player.IsInsidePvPZone());
        player.SetInsideZoneType(ZoneType.NO_FLY);
        player.SetInsideZoneType(ZoneType.DISABLE_PVP);
        Assert.False(player.IsInsideFlyZone());
        Assert.False(player.IsInsidePvPZone());
        player.SetInsideZoneType(ZoneType.FLY);
        player.SetInsideZoneType(ZoneType.PVP);
        Assert.False(player.IsInsideFlyZone());
        Assert.False(player.IsInsidePvPZone());
        player.UnsetInsideZoneType(ZoneType.NO_FLY);
        player.UnsetInsideZoneType(ZoneType.DISABLE_PVP);
        Assert.True(player.IsInsideFlyZone());
        Assert.True(player.IsInsidePvPZone());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FlyCallbacksFollowEffectivePermissionAcrossOverlappingZonesAndMapDefaults(bool mapFly)
    {
        var fly1 = new FlyZoneInstance(1, new ZoneInfo(Template("fly1")));
        var fly2 = new FlyZoneInstance(1, new ZoneInfo(Template("fly2")));
        var noFly1 = new NoFlyZoneInstance(1, new ZoneInfo(Template("noFly1")));
        var noFly2 = new NoFlyZoneInstance(1, new ZoneInfo(Template("noFly2")));
        var fixture = new Fixture(mapFly ? "FLY" : "", fly1, fly2, noFly1, noFly2);
        fly1.OnEnter(fixture.Player); fly2.OnEnter(fixture.Player); fly1.OnLeave(fixture.Player);
        Assert.Equal(mapFly ? 0 : 1, fixture.Controller.FlyEntries);
        Assert.Equal(0, fixture.Controller.FlyLeaves);
        noFly1.OnEnter(fixture.Player); noFly2.OnEnter(fixture.Player);
        Assert.Equal(1, fixture.Controller.FlyLeaves);
        noFly1.OnLeave(fixture.Player);
        Assert.False(fixture.Player.IsInsideFlyZone());
        noFly2.OnLeave(fixture.Player);
        Assert.True(fixture.Player.IsInsideFlyZone());
        Assert.Equal(mapFly ? 1 : 2, fixture.Controller.FlyEntries);
        fly2.OnLeave(fixture.Player);
        Assert.Equal(mapFly ? 1 : 2, fixture.Controller.FlyLeaves);
        Assert.Equal(mapFly, fixture.Player.IsInsideFlyZone());
    }

    [Theory]
    [InlineData("RIDE BIND", -1, true, true)]
    [InlineData("RIDE BIND", 0, true, true)]
    [InlineData("RIDE BIND", 2, false, false)]
    [InlineData("RIDE BIND", 17, true, true)]
    [InlineData("RIDE", 17, true, false)]
    [InlineData("", 17, false, false)]
    public void RideAndKiskChecksRequireTheMapAttributeAndHonorOnlyPositiveZoneRestrictions(string flags, int zoneFlags, bool ride, bool bind)
    {
        var zone = new ZoneInstance(1, new ZoneInfo(Template("limit", zoneFlags)));
        var fixture = new Fixture(flags, zone);
        zone.OnEnter(fixture.Player);
        bool previous = CustomConfig.ENABLE_RIDE_RESTRICTION;
        try
        {
            CustomConfig.ENABLE_RIDE_RESTRICTION = true;
            Assert.Equal(ride, RideAction.IsInRideZone(fixture.Player));
            Assert.Equal(bind, Invoke<bool>(new ToyPetSpawnAction(), "IsPutKiskZone", fixture.Player));
            CustomConfig.ENABLE_RIDE_RESTRICTION = false;
            Assert.True(RideAction.IsInRideZone(fixture.Player));
        }
        finally { CustomConfig.ENABLE_RIDE_RESTRICTION = previous; }
    }

    [Theory]
    [InlineData("DUEL_SAME_RACE", true, 0, ZoneClassName.DUEL, false)]
    [InlineData("DUEL_SAME_RACE", true, -1, ZoneClassName.DUEL, false)]
    [InlineData("DUEL_SAME_RACE", true, 128, ZoneClassName.DUEL, true)]
    [InlineData("DUEL_SAME_RACE", true, 0, ZoneClassName.SUB, true)]
    [InlineData("", true, 128, ZoneClassName.DUEL, false)]
    [InlineData("DUEL_OTHER_RACE", false, 256, ZoneClassName.DUEL, true)]
    [InlineData("DUEL_OTHER_RACE", false, 128, ZoneClassName.DUEL, false)]
    public void DuelPermissionRequiresTheTargetMapAndEveryDuelZone(string flags, bool sameRace, int zoneFlags, ZoneClassName type, bool expected)
    {
        var template = Template("duel", zoneFlags); template.ZoneType = type;
        var zone = new ZoneInstance(1, new ZoneInfo(template));
        var fixture = new Fixture(flags, zone);
        zone.OnEnter(fixture.Player);
        var requester = NewPlayer(2, sameRace ? Race.ELYOS : Race.ASMODIANS);
        Assert.Equal(expected, Invoke<bool>(DuelService.GetInstance(), "IsInDuelZone", requester, fixture.Player));
    }

    [Theory]
    [InlineData(true, 1, 2, true)]
    [InlineData(true, 2, 1, false)]
    [InlineData(false, 1, 2, false)]
    public void RecallUsesTheLowestPriorityLimitAndCannotOverrideTheMap(bool mapAllows, int allowPriority, int denyPriority, bool expected)
    {
        var allow = Template("allow", 2); allow.ZoneType = ZoneClassName.LIMIT; allow.Priority = allowPriority;
        var deny = Template("deny", 0); deny.ZoneType = ZoneClassName.LIMIT; deny.Priority = denyPriority;
        var ignored = Template("no-flags", -1); ignored.ZoneType = ZoneClassName.LIMIT; ignored.Priority = -1;
        var zones = new[] { allow, deny, ignored }.Select(t => new ZoneInstance(1, new ZoneInfo(t))).ToArray();
        var fixture = new Fixture(mapAllows ? "RECALL" : "", zones);
        foreach (var zone in zones) zone.OnEnter(fixture.Player);
        Assert.Equal(expected, RecallService.CanRecallAt(fixture.Player));
    }

    [Fact]
    public void PvpZonesRequireThePvpBitAndNoFullMapZoneIsCreated()
    {
        var unrelated = Template("unrelated", 1); unrelated.ZoneType = ZoneClassName.PVP;
        var pvp = Template("pvp", 64); pvp.ZoneType = ZoneClassName.PVP;
        var noFlags = Template("no-flags", -1); noFlags.ZoneType = ZoneClassName.PVP;
        using var scope = new ZoneCleanupPortTests.DataScope(unrelated, pvp, noFlags);
        var service = (ZoneService)Activator.CreateInstance(typeof(ZoneService), nonPublic: true)!;
        var zones = service.CreateZoneInstances(1);
        Assert.Equal(3, zones.Count);
        Assert.IsType<DisablePvPZoneInstance>(zones.Single(z => z.GetZoneTemplate().GetName() == "unrelated"));
        Assert.IsType<DisablePvPZoneInstance>(zones.Single(z => z.GetZoneTemplate().GetName() == "no-flags"));
        Assert.IsType<PvPZoneInstance>(zones.Single(z => z.GetZoneTemplate().GetName() == "pvp"));
    }

    private static ZoneTemplate Template(string name, int flags = -1) => new()
        { XmlName = name, Mapid = 1, Flags = flags, AreaType = AreaType.Sphere, Sphere = new Sphere(0, 0, 0, 1000) };
    private static Player NewPlayer(int id, Race race)
    {
        var common = new PlayerCommonData(id); common.SetName("Zone" + id); common.SetRace(race); common.SetPlayerClass(PlayerClass.GLADIATOR);
        return new Player(new PlayerAccountData(common, new PlayerAppearance()), new Account(id));
    }
    private static T Invoke<T>(object instance, string name, params object[] args) => (T)instance.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, args)!;
    private static T Bare<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    private static void Set(object instance, string name, object value) => instance.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);

    private sealed class Fixture
    {
        public Player Player { get; } = NewPlayer(1, Race.ELYOS);
        public ProbeController Controller { get; } = new();
        public Fixture(string flags, params ZoneInstance[] zones)
        {
            var map = Bare<WorldMap>();
            Set(map, "worldMapTemplate", new WorldMapTemplate { MapId = 1, WorldSize = 256, FlagsRaw = flags });
            var instance = Bare<WorldMap2DInstance>();
            typeof(WorldMapInstance).GetField("parent", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, map);
            var region = new MapRegion(1, instance, zones);
            Player.SetPosition(new WorldPosition(1, 0, 0, 0, 0, region));
            typeof(VisibleObject).GetField("_controller", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Player, Controller);
            Controller.SetOwner(Player);
        }
    }

    private sealed class ProbeController : PlayerController
    {
        public int FlyEntries, FlyLeaves;
        public override void OnEnterZone(ZoneInstance zone) { }
        public override void OnLeaveZone(ZoneInstance zone) { }
        public override void OnEnterFlyArea() => FlyEntries++;
        public override void OnLeaveFlyArea() => FlyLeaves++;
    }
}
