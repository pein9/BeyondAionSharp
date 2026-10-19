using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Stats.Container;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>
/// Java parity: model/gameobjects/SummonedObject&lt;T extends VisibleObject&gt; extends Npc.
/// Non-generic base + generic shim (no C# wildcard generics; StatFunctions uses `is SummonedObject`).
/// </summary>
public class SummonedObject : Npc
{
    private readonly sbyte level;
    private readonly VisibleObject creator;

    public SummonedObject(Aion.GameServer.Controllers.NpcController controller, Aion.GameServer.Model.Templates.Spawns.SpawnTemplate spawnTemplate, sbyte level, VisibleObject creator)
        : base(controller, spawnTemplate, DataManager.NPC_DATA.GetNpcTemplate(spawnTemplate.GetNpcId()))
    {
        this.level = level;
        this.creator = creator;
    }

    protected override void SetupStatContainers()
    {
        SetGameStats(new SummonedObjectGameStats(this));
        SetLifeStats(new NpcLifeStats(this));
    }

    public override sbyte GetLevel()
    {
        return this.level;
    }

    public override VisibleObject GetCreator()
    {
        return creator;
    }

    public override string GetMasterName()
    {
        return base.GetMasterName() == null && creator != null ? creator.GetName() : base.GetMasterName();
    }

    public override int GetCreatorId()
    {
        return base.GetCreatorId() == 0 && creator != null ? creator.GetObjectId() : base.GetCreatorId();
    }

    public sealed override Creature GetMaster()
    {
        return creator is Creature owner ? owner : this;
    }

    public override CreatureType GetTypeValue(Creature creature)
    {
        return creature.IsEnemy(GetMaster()) ? CreatureType.ATTACKABLE : CreatureType.SUPPORT;
    }

    public override bool IsEnemy(Creature creature)
    {
        if (creator is Creature owner)
            return owner.IsEnemy(creature);
        return base.IsEnemy(creature);
    }

    public override bool IsEnemyFrom(Npc npc)
    {
        if (creator is Creature owner)
            return owner.IsEnemyFrom(npc);
        return base.IsEnemyFrom(npc);
    }

    public override bool IsEnemyFrom(Player player)
    {
        if (creator is Player owner)
            return owner.IsEnemyFrom(player, this);
        if (creator is Creature creatureOwner)
            return creatureOwner.IsEnemyFrom(player);
        return base.IsEnemyFrom(player);
    }

    public override Race GetRace()
    {
        return creator is Creature owner ? owner.GetRace() : base.GetRace();
    }

    public override bool IsPvpTarget(Creature creature)
    {
        return creator is Player && creature.GetMaster() is Player;
    }
}

/// <summary>
/// Java parity: generic typing of <see cref="SummonedObject"/> (Java <c>SummonedObject&lt;T extends VisibleObject&gt;</c>).
/// </summary>
public class SummonedObject<T> : SummonedObject where T : VisibleObject
{
    public SummonedObject(Aion.GameServer.Controllers.NpcController controller, Aion.GameServer.Model.Templates.Spawns.SpawnTemplate spawnTemplate, sbyte level, T creator)
        : base(controller, spawnTemplate, level, creator)
    {
    }

    public new T GetCreator() => (T)base.GetCreator();
}
