using System.Reflection;
using Aion.Commons.Configuration;
using Aion.Commons.Configuration.Transformers;
using Aion.Commons.Logging;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine.Models;
using Aion.GameServer.GeoEngine.Scene;
using Aion.GameServer.Services;
using Aion.GameServer.TestKit;
using Aion.GameServer.World.Geo;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class GeoDataModePortTests : IDisposable
{
    private readonly Dictionary<FieldInfo, object?> config = typeof(GeoDataConfig)
        .GetFields(BindingFlags.Public | BindingFlags.Static).ToDictionary(field => field, field => field.GetValue(null));
    public void Dispose()
    {
        foreach (var (field, value) in config) field.SetValue(null, value);
    }

    [Theory]
    [InlineData(GeoDataConfig.Mode.OFF)]
    [InlineData(GeoDataConfig.Mode.MATERIALS_ONLY)]
    [InlineData(GeoDataConfig.Mode.ON)]
    public void StaticAndHostOptionsReadTheSameMode(GeoDataConfig.Mode mode)
    {
        var props = new JavaProperties();
        props.SetProperty("gameserver.geodata.mode", mode.ToString());
        ConfigurableProcessor.Process(props, typeof(GeoDataConfig));
        Assert.Equal(mode, GeoDataConfig.MODE);
        WithConfig(mode.ToString(), root => Assert.Equal(mode, GameServerOptions.LoadFromJavaConfig(root).GeoData.Mode));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("on")]
    [InlineData("ON,OFF")]
    public void HostOptionsRejectValuesThatJavaEnumValueOfRejects(string value) =>
        WithConfig(value, root => Assert.Throws<TransformationException>(() => GameServerOptions.LoadFromJavaConfig(root)));

    [Fact]
    public void ModeDefaultsToOnAndRemovedSwitchesAreNoLongerBound()
    {
        var props = new JavaProperties();
        props.SetProperty("gameserver.geodata.enable", "false");
        props.SetProperty("gameserver.geodata.materials.enable", "false");
        props.SetProperty("gameserver.geodata.shields.enable", "false");
        var unused = ConfigurableProcessor.Process(props, typeof(GeoDataConfig));
        Assert.Equal(GeoDataConfig.Mode.ON, GeoDataConfig.MODE);
        Assert.Equal(3, unused.Count);
    }

    [Fact]
    public async Task OffInitializesEmptyMapsAndNeverTriesToReadGeoFiles()
    {
        var previous = DataManager.GetRegisteredInstance();
        try
        {
            DataManager.RegisterInstance(await RealStaticData.LoadAsync());
            GeoDataConfig.MODE = GeoDataConfig.Mode.OFF;
            var service = new GeoService();
            service.Init(); // The test process has no data/geo directory in its current working directory.
            Assert.All(DataManager.WORLD_MAPS_DATA, template =>
            {
                var map = service.GetMap(template.GetMapId());
                Assert.False(map.HasTerrain());
                Assert.Empty(map.GetChildren());
                Assert.False(service.WorldHasTerrainMaterials(template.GetMapId()));
                Assert.Equal(0, service.GetTerrainMaterialAt(template.GetMapId(), 0, 0, 0, 1));
            });
        }
        finally { DataManager.RestoreInstance(previous); }
    }

    [Theory]
    [InlineData(GeoDataConfig.Mode.ON, true)]
    [InlineData(GeoDataConfig.Mode.MATERIALS_ONLY, false)]
    [InlineData(GeoDataConfig.Mode.OFF, false)]
    public void MissingDoorGeometryWarnsOnlyInFullMode(GeoDataConfig.Mode mode, bool warns)
    {
        GeoDataConfig.MODE = mode;
        using var logs = new CapturingLoggerProvider();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        using var logging = AionLog.OverrideFactory(factory);
        new GeoMap(210010000).SetDoorState(1, 9876, true);
        Assert.Equal(warns, logs.Entries.Any(entry => entry.Message.Contains("No geometry found for door 9876")));
    }

    [Fact]
    public void ShieldRegistrationNeedsNoLegacyTemplateDataAndRetainsTheIgnoredShieldList()
    {
        var previous = DataManager.GetRegisteredInstance();
        try
        {
            DataManager.RestoreInstance(null);
            var service = (ShieldService)Activator.CreateInstance(typeof(ShieldService), nonPublic: true)!;
            var geometry = new Node("PORT_GEO_SHIELD");
            var shield = service.TryRegisterShield(400010000, geometry);
            Assert.Same(geometry, shield.GetGeometry());
            Assert.Null(service.TryRegisterShield(310100000, new Node("BU_AB_CASTLESHIELD_SAMJUNG_03C_TYPE2_487543")));
        }
        finally { DataManager.RestoreInstance(previous); }
    }

    private static void WithConfig(string value, Action<string> check)
    {
        var root = Directory.CreateTempSubdirectory("aion-geo-config-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "game-server", "config", "main"));
            Directory.CreateDirectory(Path.Combine(root, "game-server", "config", "administration"));
            Directory.CreateDirectory(Path.Combine(root, "game-server", "config", "network"));
            File.WriteAllText(Path.Combine(root, "game-server", "config", "mygs.properties"), "");
            File.WriteAllText(Path.Combine(root, "game-server", "config", "main", "geodata.properties"), $"gameserver.geodata.mode = {value}\n");
            check(root);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
