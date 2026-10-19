using System.Reflection;
using System.Xml.Serialization;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Templates.Flypath;
using Aion.GameServer.Model.Templates.Siegelocation;
using Aion.GameServer.Model.Templates.Teleport;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Tests;

// Reference: TeleportService.java at 83b9e3322df2bf47c0cdfb28d4115a91ada40c13.
// Exercise the public route entry point, actual siege lookup, inventory and price service.
[Collection("GoldenDataManager")]
public sealed class FortressTeleportPricePortTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    [Theory]
    [InlineData(TeleportType.REGULAR, false, 101)]
    [InlineData(TeleportType.REGULAR, true, 199)]
    [InlineData(TeleportType.FLIGHT, false, 101)]
    [InlineData(TeleportType.FLIGHT, true, 199)]
    public void FortressVulnerabilitySelectsFareForBothRouteTypes(TeleportType type, bool vulnerable, long fare)
    {
        using var s = new Scope(); s.Fortress.SetVulnerable(vulnerable);
        s.Travel(type);
        Assert.Equal(1000 - fare, s.P.GetInventory().GetKinah()); s.AssertStarted(type);
    }

    [Theory]
    [InlineData(49)] // Mapped fortress absent from the siege registry: Java now permits null.
    [InlineData(999)] // Ordinary route without any siege mapping.
    public void RoutesWithoutFortressesKeepNormalFare(int location)
    {
        using var s = new Scope(); s.RemoveFortress(); s.Route.locId = location;
        s.Travel(TeleportType.FLIGHT);
        Assert.Equal(899, s.P.GetInventory().GetKinah()); s.AssertStarted(TeleportType.FLIGHT);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void HiPassStillOverridesBothFares(bool vulnerable)
    {
        using var s = new Scope(); s.Fortress.SetVulnerable(vulnerable); s.HiPass();
        s.Travel(TeleportType.FLIGHT);
        Assert.Equal(999, s.P.GetInventory().GetKinah()); s.AssertStarted(TeleportType.FLIGHT);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EnemyOrDisabledFortressRefusesBeforeChargingEvenWithHiPass(bool enemy)
    {
        using var s = new Scope(); s.Fortress.SetVulnerable(true); s.HiPass();
        if (enemy) s.Fortress.SetRace(s.P.GetRace() == Race.ELYOS ? SiegeRace.ASMODIANS : SiegeRace.ELYOS);
        else s.Fortress.SetCanTeleport(false);
        s.Travel(TeleportType.FLIGHT);
        Assert.Equal(1000, s.P.GetInventory().GetKinah()); Assert.Null(s.P.GetFlightPath());
        Assert.Equal(SM_SYSTEM_MESSAGE.STR_CANNOT_MOVE_TO_AIRPORT_NO_ROUTE().GetId(), Assert.Single(s.Packets.OfType<SM_SYSTEM_MESSAGE>()).GetId());
    }

    [Theory]
    [InlineData(198, false)] [InlineData(199, true)]
    public void VulnerableFortressRequiresTheFullPvpFare(long balance, bool allowed)
    {
        using var s = new Scope(); s.Fortress.SetVulnerable(true); s.P.GetInventory().GetKinahItem().SetItemCount(balance);
        s.Travel(TeleportType.FLIGHT);
        Assert.Equal(allowed ? 0 : balance, s.P.GetInventory().GetKinah());
        Assert.Equal(allowed, s.P.GetFlightPath() != null);
        if (!allowed) Assert.Equal(SM_SYSTEM_MESSAGE.STR_MSG_NOT_ENOUGH_KINA(199).GetId(), Assert.Single(s.Packets.OfType<SM_SYSTEM_MESSAGE>()).GetId());
    }

    [Fact]
    public void PvpFareUsesTheExistingOrderedPriceModifiers()
    {
        using var s = new Scope(); s.Fortress.SetVulnerable(true);
        PricesConfig.DEFAULT_PRICES = 137; PricesConfig.DEFAULT_MODIFIER = 113; PricesConfig.DEFAULT_TAXES = 107;
        s.Travel(TeleportType.FLIGHT);
        // Java: floor(floor(floor(199 * 1.37) * 1.13) * 1.07) = 328.
        Assert.Equal(672, s.P.GetInventory().GetKinah()); s.AssertStarted(TeleportType.FLIGHT);
    }

    [Fact]
    public void ZeroPvpFareDoesNotFallBackToNormalFare()
    {
        using var s = new Scope(); s.Fortress.SetVulnerable(true); s.Route.pricePvp = 0;
        s.Travel(TeleportType.FLIGHT);
        Assert.Equal(1000, s.P.GetInventory().GetKinah()); s.AssertStarted(TeleportType.FLIGHT);
    }

    [Fact]
    public void MissingFlightPathStillRefusesBeforeCharging()
    {
        using var s = new Scope(); s.Fortress.SetVulnerable(true); s.Route.teleportid = 999001;
        s.Travel(TeleportType.FLIGHT);
        Assert.Equal(1000, s.P.GetInventory().GetKinah()); Assert.Null(s.P.GetFlightPath());
        Assert.Equal(SM_SYSTEM_MESSAGE.STR_CANNOT_MOVE_TO_AIRPORT_NO_ROUTE().GetId(), Assert.Single(s.Packets.OfType<SM_SYSTEM_MESSAGE>()).GetId());
    }

    private sealed class Scope : IDisposable
    {
        public PvpExpPortTests.Scope Common { get; } = new();
        public Player P { get; }
        public SiegeLocation Fortress { get; } = new(new SiegeLocationTemplate { id = 1011 });
        public TeleportLocation Route { get; } = new() { locId = 49, teleportid = 1001, price = 101, pricePvp = 199 };
        public AionServerPacket[] Packets => ((PvpExpPortTests.Connection)P.GetClientConnection()).Packets;
        private readonly SiegeLocation? previousFortress;
        private readonly int prices = PricesConfig.DEFAULT_PRICES, modifier = PricesConfig.DEFAULT_MODIFIER, taxes = PricesConfig.DEFAULT_TAXES;
        private readonly float elyos = (float)Field(typeof(Influence), "elyosInfluenceRate").GetValue(Influence.GetInstance())!;
        private readonly float asmo = (float)Field(typeof(Influence), "asmoInfluenceRate").GetValue(Influence.GetInstance())!;
        public Scope()
        {
            P = Common.Player(65);
            Field(typeof(Player), "rideObservers").SetValue(P, new List<Aion.GameServer.Controllers.Observer.ActionObserver>());
            Field(typeof(Player), "flyController").SetValue(P, new Aion.GameServer.Controllers.FlyController(P));
            Fortress.SetRace(P.GetRace() == Race.ELYOS ? SiegeRace.ELYOS : SiegeRace.ASMODIANS);
            previousFortress = SiegeService.GetInstance().GetSiegeLocation(1011);
            SiegeService.GetInstance().GetSiegeLocations()[1011] = Fortress;
            PricesConfig.DEFAULT_PRICES = PricesConfig.DEFAULT_MODIFIER = PricesConfig.DEFAULT_TAXES = 100;
            Field(typeof(Influence), "elyosInfluenceRate").SetValue(Influence.GetInstance(), 0.5f);
            Field(typeof(Influence), "asmoInfluenceRate").SetValue(Influence.GetInstance(), 0.5f);
            var locations = new TeleLocationData { tlist = [new() { locId = 49, mapid = 300100000 }, new() { locId = 999, mapid = 300100000 }] };
            locations.AfterUnmarshal(null!); Holder("TeleLocationDataDh", locations);
            var paths = new FlyPathData { list = [new FlyPathEntry { id = 1, sworld = 300100000, eworld = 300100000, time = 10 }] };
            paths.AfterUnmarshal(null!); Holder("FlyPathDataDh", paths);
            using var xml = new StringReader("<item_templates><item_template id=\"182400001\" name=\"Kinah\" level=\"1\" quality=\"COMMON\" max_stack_count=\"2147483647\"/></item_templates>");
            var items = (ItemData)new XmlSerializer(typeof(ItemData)).Deserialize(xml)!; items.AfterUnmarshal(null!); Holder("ItemDataDh", items);
            var kinah = new Item(992001, items.GetItemTemplate(182400001)); kinah.SetItemCount(1000); P.GetInventory().OnLoadHandler(kinah);
        }
        private static FieldInfo Field(Type type, string name) => type.GetField(name, Fields)!;
        private void Holder(string name, object value) => Field(typeof(StaticData), "<" + name + ">k__BackingField").SetValue(Common.H.Data.StaticData, value);
        public void RemoveFortress() => SiegeService.GetInstance().GetSiegeLocations().Remove(1011);
        public void HiPass()
        {
            var effects = new Effects { effects = [new HiPassEffect()] }; effects.AfterUnmarshal(null!);
            var effect = new Effect(P, P, new SkillTemplate { effects = effects }, 1);
            ((Dictionary<string, Effect>)Field(typeof(EffectController), "abnormalEffectMap").GetValue(P.GetEffectController())!).Add("test-hipass", effect);
        }
        public void Travel(TeleportType type)
        {
            Route.type = type; ((PvpExpPortTests.Connection)P.GetClientConnection()).Clear();
            TeleportService.Teleport(P, Route, TeleportAnimation.FADE_OUT_BEAM);
        }
        public void AssertStarted(TeleportType type)
        {
            if (type == TeleportType.FLIGHT) { Assert.NotNull(P.GetFlightPath()); Assert.Single(Packets.OfType<SM_EMOTION>()); }
            else { Assert.Single(Packets.OfType<SM_TELEPORT_LOC>()); Assert.NotNull(P.GetController().GetAndRemoveTask(TaskId.TELEPORT)); }
        }
        public void Dispose()
        {
            if (previousFortress == null) RemoveFortress(); else SiegeService.GetInstance().GetSiegeLocations()[1011] = previousFortress;
            PricesConfig.DEFAULT_PRICES = prices; PricesConfig.DEFAULT_MODIFIER = modifier; PricesConfig.DEFAULT_TAXES = taxes;
            Field(typeof(Influence), "elyosInfluenceRate").SetValue(Influence.GetInstance(), elyos);
            Field(typeof(Influence), "asmoInfluenceRate").SetValue(Influence.GetInstance(), asmo);
            Common.Dispose();
        }
    }
}
