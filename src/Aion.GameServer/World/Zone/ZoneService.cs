using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Aion.Commons.Scripting;
using Aion.Commons.Scripting.ClassListener;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine.Scene;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Templates.Materials;
using Aion.GameServer.Model.Templates.World;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Model.Vortex;
using Aion.GameServer.Services;
using Aion.GameServer.World.Zone.Handler;

namespace Aion.GameServer.World.Zone;

/// <summary>
/// Java parity: world/zone/ZoneService (ATracer, antness). Builds per-map ZoneInstances and registers zone/material handlers. Twin of
/// InstanceEngine: implements GameEngine via Java-faithful Init() (the reworked async GameEngine contract is red-tolerated, matching
/// InstanceEngine). Class&lt;? extends IZoneHandler&gt; -> Type; getAnnotation -> GetCustomAttribute; newInstance -> Activator.CreateInstance;
/// switch-case on ZoneClassName (PascalCase); synchronized -> lock; SingletonHolder nested. Most zone/data deps red-tolerated.
/// </summary>
public sealed class ZoneService : GameEngine
{

    // GameEngine async lifecycle (infra adapter over the faithful Init()).
    public string Name => GetType().Name;
    public System.Threading.Tasks.ValueTask InitAsync(System.Threading.CancellationToken cancellationToken) { Init(); return System.Threading.Tasks.ValueTask.CompletedTask; }
    public System.Threading.Tasks.ValueTask ShutdownAsync(System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.ValueTask.CompletedTask;
    private static readonly ILogger log = AionLog.For(nameof(ZoneService));
    private readonly Dictionary<string, Type> zoneHandlers = new Dictionary<string, Type>();
    private readonly Dictionary<int, List<MaterialZone>> materialZonesByMapId = new();
    private readonly object sync = new object();

    private ZoneService()
    {
    }

    public void Init()
    {
        ScriptManager scriptManager = new ScriptManager();
        AggregatedClassListener acl = new AggregatedClassListener();
        acl.AddClassListener(new OnClassLoadUnloadListener());
        acl.AddClassListener(new ZoneHandlerClassListener());
        scriptManager.SetGlobalClassListener(acl);
        scriptManager.Load(WorldConfig.ZONE_HANDLER_DIRECTORY);
        log.LogInformation("Loaded {ZoneHandlersSize} zone handlers", zoneHandlers.Count);
    }

    public IZoneHandler? GetNewZoneHandler(string zoneName)
    {
        Type? zoneClass = zoneHandlers.GetValueOrDefault(zoneName);
        if (zoneClass != null)
        {
            try { return (IZoneHandler?)Activator.CreateInstance(zoneClass); }
            catch (Exception ex) { log.LogWarning(ex, "Can't instantiate zone handler {ZoneName}", zoneName); }
        }
        return null;
    }

    public void AddZoneHandlerClass(Type handler)
    {
        var annotation = handler.GetCustomAttribute<ZoneHandlerArea>();
        if (annotation == null) return;
        foreach (string name in annotation.Value)
            if (DataManager.ZONE_DATA.ValidateZoneName(name)) zoneHandlers[name] = handler;
    }

    public List<ZoneInstance> CreateZoneInstances(int mapId)
    {
        List<ZoneInstance> zones = new();
        var vortex = GetAndValidateVortexLocation(mapId);
        foreach (ZoneInfo area in DataManager.ZONE_DATA.GetZones(mapId))
        {
            ZoneInstance instance;
            switch (area.GetZoneTemplate().GetZoneType())
            {
                case ZoneClassName.FLY:
                    instance = new FlyZoneInstance(mapId, area);
                    break;
                case ZoneClassName.NO_FLY:
                    instance = new NoFlyZoneInstance(mapId, area);
                    break;
                case ZoneClassName.FORT:
                    instance = new SiegeZoneInstance(mapId, area);
                    SiegeLocation siege = DataManager.SIEGE_LOCATION_DATA.GetSiegeLocations().GetValueOrDefault(area.GetZoneTemplate().GetSiegeId()[0]);
                    if (siege != null)
                    {
                        siege.AddZone((SiegeZoneInstance)instance);
                        ShieldService.GetInstance().AttachShield(siege);
                    }
                    break;
                case ZoneClassName.ARTIFACT:
                    instance = new SiegeZoneInstance(mapId, area);
                    foreach (int artifactId in area.GetZoneTemplate().GetSiegeId())
                    {
                        SiegeLocation artifact = DataManager.SIEGE_LOCATION_DATA.GetArtifacts().GetValueOrDefault(artifactId);
                        if (artifact == null)
                        {
                            log.LogWarning("Missing siege location data for zone " + area.GetZoneTemplate().GetName());
                        }
                        else
                        {
                            artifact.AddZone((SiegeZoneInstance)instance);
                        }
                    }
                    break;
                case ZoneClassName.PVP:
                    instance = area.GetZoneTemplate().HasZoneAttribute(ZoneAttributes.PvpEnabled) ? new PvPZoneInstance(mapId, area) : new DisablePvPZoneInstance(mapId, area);
                    break;
                default:
                    InvasionZoneInstance? invasionZone = TryRegisterInvasionZone(vortex, area);
                    if (invasionZone != null)
                    {
                        instance = invasionZone;
                    }
                    else
                    {
                        instance = new ZoneInstance(mapId, area);
                    }
                    break;
            }
            IZoneHandler? zoneHandler = GetNewZoneHandler(area.GetZoneTemplate().GetName());
            if (zoneHandler != null)
                instance.AddHandler(zoneHandler);
            zones.Add(instance);
        }
        foreach (var material in materialZonesByMapId.GetValueOrDefault(mapId) ?? new List<MaterialZone>())
            zones.Add(new ZoneInstance(mapId, material.Info, new List<IZoneHandler> { material.Handler }));
        return zones;
    }

    private static VortexLocation? GetAndValidateVortexLocation(int mapId)
    {
        var location = DataManager.VORTEX_DATA.GetVortexLocation(mapId);
        if (location != null)
            foreach (string name in location.GetTemplate().GetZones()) DataManager.ZONE_DATA.ValidateZoneName(name);
        return location;
    }

    private InvasionZoneInstance? TryRegisterInvasionZone(VortexLocation? vortex, ZoneInfo area)
    {
        if (vortex != null && vortex.GetTemplate().GetZones().Contains(area.GetZoneTemplate().GetName()!))
        {
            var instance = new InvasionZoneInstance(area.GetZoneTemplate().GetMapid(), area);
            vortex.AddZone(instance);
            return instance;
        }
        return null;
    }

    public void CreateMaterialZoneTemplate(Spatial geometry, int worldId)
    {
        lock (sync)
        {
            IZoneHandler? handler;
            if (geometry.GetMaterialId() == 11)
            {
                handler = ShieldService.GetInstance().TryRegisterShield(worldId, geometry);
                if (handler == null) return;
            }
            else
            {
                var template = DataManager.MATERIAL_DATA.GetTemplate(geometry.GetMaterialId());
                if (template == null) return;
                handler = new MaterialZoneHandler(geometry, template);
            }
            if (!materialZonesByMapId.TryGetValue(worldId, out var zones))
                materialZonesByMapId[worldId] = zones = new List<MaterialZone>();
            zones.Add(new MaterialZone(new ZoneInfo(new MaterialZoneTemplate(geometry, worldId)), handler));
        }
    }

    public static ZoneService GetInstance()
    {
        return SingletonHolder.instance;
    }

    private static class SingletonHolder
    {
        internal static readonly ZoneService instance = new ZoneService();
    }
    private sealed record MaterialZone(ZoneInfo Info, IZoneHandler Handler);
}
