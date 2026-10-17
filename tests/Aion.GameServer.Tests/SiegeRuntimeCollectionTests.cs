using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Aion.GameServer.Controllers;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Siege;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Spawns.Siegespawns;
using Aion.GameServer.Model.Templates.Stats;
using Aion.GameServer.Services;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;
using IDFactory = Aion.GameServer.Utils.IdFactory.IDFactory;

namespace Aion.GameServer.Tests;

/// <summary>
/// Java parity: World.localSiegeNpcs is a ConcurrentHashMap of CopyOnWriteArrayList and SiegeService.activeSieges a
/// ConcurrentHashMap. A siege walks a location's npcs while it spawns into the same list
/// (FortressSiege.spawnFactionTroopAssault) or deletes from it (AgentSiege/OutpostSiege.despawnSiegeNpcs); a plain
/// List threw "Collection was modified" on the next step of the walk.
/// </summary>
[Collection("GoldenDataManager")]
public sealed class SiegeRuntimeCollectionTests : IDisposable
{
    private const int SiegeId = 1131;
    private const int WorldId = 400010000;

    private readonly DataManager? previousDataManager = DataManager.GetRegisteredInstance();

    public void Dispose()
    {
        DataManager.RestoreInstance(previousDataManager);
    }

    [Fact]
    public void AddingASiegeNpcOfTheSameSiegeWhileIteratingSeesTheSnapshot()
    {
        EnsureInfrastructure();
        var world = new GameWorld(NullLogger<GameWorld>.Instance);
        world.StoreObject(BuildSiegeNpc(SiegeId));
        world.StoreObject(BuildSiegeNpc(SiegeId));

        int seen = 0;
        foreach (SiegeNpc _ in world.GetLocalSiegeNpcs(SiegeId))
        {
            seen++;
            world.StoreObject(BuildSiegeNpc(SiegeId));
        }

        Assert.Equal(2, seen);
        Assert.Equal(4, world.GetLocalSiegeNpcs(SiegeId).Count);
    }

    [Fact]
    public void RemovingSiegeNpcsWhileIteratingIsSafe()
    {
        EnsureInfrastructure();
        var world = new GameWorld(NullLogger<GameWorld>.Instance);
        var npcs = new List<SiegeNpc> { BuildSiegeNpc(SiegeId), BuildSiegeNpc(SiegeId), BuildSiegeNpc(SiegeId) };
        foreach (SiegeNpc npc in npcs)
            world.StoreObject(npc);

        var seen = new List<SiegeNpc>();
        foreach (SiegeNpc npc in world.GetLocalSiegeNpcs(SiegeId))
        {
            seen.Add(npc);
            Assert.True(world.RemoveObject(npc));
        }

        Assert.Equal(npcs, seen);
        Assert.Empty(world.GetLocalSiegeNpcs(SiegeId));
        Assert.All(npcs, npc => Assert.False(world.IsInWorld(npc.GetObjectId())));
    }

    [Fact]
    public void ActiveSiegesIsAConcurrentMapAsInJava()
    {
        FieldInfo field = typeof(SiegeService).GetField("activeSieges", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Assert.Equal(typeof(ConcurrentDictionary<int, global::Aion.GameServer.Services.Siege.Siege>), field.FieldType);
    }

    private static SiegeNpc BuildSiegeNpc(int siegeId)
    {
        var spawn = new SiegeSpawnTemplate(siegeId, SiegeRace.BALAUR, SiegeModType.PEACE,
            new SpawnGroup(WorldId, 920002, 0, null), new SpawnSpotTemplate());
        var template = new NpcTemplate
        {
            npcId = 920002,
            level = 1,
            statsTemplate = new StatsTemplate { MaxHp = 100 }
        };
        return new SiegeNpc(new NpcController(), spawn, template);
    }

    private static void EnsureInfrastructure()
    {
        try
        {
            _ = IDFactory.GetInstance();
        }
        catch (InvalidOperationException)
        {
            IDFactory.RegisterInstance(new IDFactory());
        }

        if (DataManager.GetRegisteredInstance()?.StaticData.NpcSkillDataDh is null)
        {
            var staticData = (StaticData)RuntimeHelpers.GetUninitializedObject(typeof(StaticData));
            typeof(StaticData).GetProperty(nameof(StaticData.NpcSkillDataDh))!
                .SetValue(staticData, new NpcSkillData());
            ConstructorInfo constructor = typeof(DataManager).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                [typeof(StaticData)],
                modifiers: null)!;
            DataManager.RegisterInstance((DataManager)constructor.Invoke([staticData]));
        }
    }
}
