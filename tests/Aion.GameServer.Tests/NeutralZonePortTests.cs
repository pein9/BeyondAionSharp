using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Aion.GameServer.Controllers;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Services;
using Aion.GameServer.SkillEngine.Properties;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Tests;

/// <summary>Behavior from Java 206f9b5db: neutral-zone precedence and ownership-sensitive PvP.</summary>
public sealed class NeutralZonePortTests
{
    [Fact]
    public void OverlappingNeutralZonesRemainDisabledUntilTheLastLeave()
    {
        var creature = BareCreature<Npc>();
        SetField(typeof(VisibleObject), creature, "_controller", new NpcController());
        var first = NeutralZone("PORT_NEUTRAL_FIRST");
        var second = NeutralZone("PORT_NEUTRAL_SECOND");
        creature.SetInsideZoneType(ZoneType.PVP);
        creature.SetInsideZoneType(ZoneType.PVP);

        Assert.True(creature.IsInsidePvPZone());
        Assert.True(first.OnEnter(creature));
        Assert.False(first.OnEnter(creature)); // Duplicate entry must not increment the counter.
        Assert.True(second.OnEnter(creature));
        Assert.False(creature.IsInsidePvPZone());
        Assert.True(first.OnLeave(creature));
        Assert.False(first.OnLeave(creature)); // Duplicate leave must not decrement it.
        Assert.False(creature.IsInsidePvPZone());
        Assert.True(second.OnLeave(creature));
        Assert.True(creature.IsInsidePvPZone());
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    public void NeutralZoneBlocksBothCrossRaceAndSameRaceArenaPvp(bool sameRace, bool firstNeutral, bool secondNeutral, bool expected)
    {
        var first = Player(701, Race.ELYOS);
        var second = Player(702, sameRace ? Race.ELYOS : Race.ASMODIANS);
        first.SetInsideZoneType(ZoneType.PVP);
        second.SetInsideZoneType(ZoneType.PVP);
        SetNeutral(first, firstNeutral);
        SetNeutral(second, secondNeutral);

        Assert.Equal(expected, first.IsEnemyFrom(second));
        Assert.Equal(expected, second.IsEnemyFrom(first));
        Assert.True(first.IsPvPEnemyOf(second)); // Relation stays hostile even where combat is forbidden.
        Assert.False(first.IsPvPEnemyOf(first));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    public void SummonAndServantTargetsUseTheirOwnZoneInsteadOfTheirOwners(bool ownerNeutral, bool targetNeutral, bool expected)
    {
        var owner = Player(703, Race.ELYOS);
        var attacker = Player(704, Race.ASMODIANS);
        var summon = Summon(owner);
        var servant = BareCreature<SummonedObject>();
        SetField(typeof(SummonedObject), servant, "creator", owner);
        SetNeutral(owner, ownerNeutral);
        SetNeutral(summon, targetNeutral);
        SetNeutral(servant, targetNeutral);

        Assert.Equal(expected, summon.IsEnemyFrom(attacker));
        Assert.Equal(expected, servant.IsEnemyFrom(attacker));
        SetNeutral(attacker, true);
        Assert.False(summon.IsEnemyFrom(attacker));
        Assert.False(servant.IsEnemyFrom(attacker));
    }

    [Fact]
    public void SummonAttackUsesItsOwnersZoneEvenWhenTheSummonIsOutsideIt()
    {
        var owner = Player(705, Race.ELYOS);
        var victim = Player(706, Race.ASMODIANS);
        var summon = Summon(owner);
        Assert.True(summon.IsEnemy(victim));
        SetNeutral(owner, true);
        Assert.False(summon.IsEnemy(victim));
    }

    [Fact]
    public void BuffAreaCheckUsesSourceOwnerAndTargetCreatureAndDoesNotRestrictNpcs()
    {
        var owner = Player(707, Race.ELYOS);
        var targetOwner = Player(708, Race.ELYOS);
        var source = Summon(owner);
        var target = Summon(targetOwner);
        var npc = BareCreature<Npc>();
        SetNeutral(owner, true);
        Assert.False(TargetRelationProperty.IsSameAreaType(source, target));
        SetNeutral(target, true);
        Assert.True(TargetRelationProperty.IsSameAreaType(source, target));
        SetNeutral(targetOwner, true);
        SetNeutral(target, false);
        Assert.False(TargetRelationProperty.IsSameAreaType(source, target));
        Assert.True(TargetRelationProperty.IsSameAreaType(npc, owner));
        Assert.True(TargetRelationProperty.IsSameAreaType(owner, npc));
    }

    [Fact]
    public void TrapStillRecognizesAnEnemyWhenTheSkillWouldBeBlockedByANeutralZone()
    {
        var owner = Player(709, Race.ELYOS);
        var victim = Player(710, Race.ASMODIANS);
        SetNeutral(owner, true);
        SetNeutral(victim, true);
        Assert.False(owner.IsEnemy(victim));

        var hostile = typeof(TrapNpcAI).GetMethod("IsHostile", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.True((bool)hostile.Invoke(null, [owner, victim])!);
        Assert.False((bool)hostile.Invoke(null, [owner, owner])!);
    }

    [Fact]
    public void DuelAndEnemyOfAllPlayersExceptionsStillWorkInsideNeutralZones()
    {
        var first = Player(711, Race.ELYOS);
        var second = Player(712, Race.ELYOS);
        SetNeutral(first, true);
        SetNeutral(second, true);
        Assert.False(first.IsEnemyFrom(second));
        var duels = (ConcurrentDictionary<int, int>)typeof(DuelService)
            .GetField("duels", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(DuelService.GetInstance())!;
        try
        {
            duels[first.ObjectId] = second.ObjectId;
            duels[second.ObjectId] = first.ObjectId;
            Assert.True(first.IsEnemyFrom(second));
        }
        finally
        {
            duels.TryRemove(first.ObjectId, out _);
            duels.TryRemove(second.ObjectId, out _);
        }
        first.SetCustomState(CustomPlayerState.ENEMY_OF_ALL_PLAYERS);
        Assert.True(first.IsEnemyFrom(second));
    }

    private static Player Player(int id, Race race)
    {
        var common = new PlayerCommonData(id);
        common.SetName("Neutral" + id);
        common.SetRace(race);
        common.SetPlayerClass(PlayerClass.GLADIATOR);
        var player = new Player(new PlayerAccountData(common, new PlayerAppearance()), new Account(id));
        player.SetPosition(new WorldPosition(110010000));
        return player;
    }

    // Only ownership and zone counters are used; avoid spawning a summon or loading NPC stats/AI.
    private static T BareCreature<T>() where T : Creature
    {
        var creature = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        SetField(typeof(Creature), creature, "zoneTypes", new byte[Enum.GetValues<ZoneType>().Length]);
        return creature;
    }

    private static Summon Summon(Player owner)
    {
        var summon = BareCreature<Summon>();
        SetField(typeof(Summon), summon, "master", owner);
        return summon;
    }

    private static void SetNeutral(Creature creature, bool neutral)
    {
        if (creature.IsInsideZoneType(ZoneType.DISABLE_PVP) == neutral)
            return;
        if (neutral)
            creature.SetInsideZoneType(ZoneType.DISABLE_PVP);
        else
            creature.UnsetInsideZoneType(ZoneType.DISABLE_PVP);
    }

    private static DisablePvPZoneInstance NeutralZone(string name)
    {
        var template = new ZoneTemplate { XmlName = name, ZoneType = ZoneClassName.PVP, Flags = 0,
            AreaType = AreaType.Sphere, Sphere = new Sphere(0, 0, 0, 1) };
        return new DisablePvPZoneInstance(110010000,
            new ZoneInfo(template));
    }

    private static void SetField(Type type, object instance, string name, object value) =>
        type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);
}
