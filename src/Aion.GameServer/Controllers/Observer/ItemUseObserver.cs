using Aion.GameServer.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using static Aion.GameServer.Controllers.Observer.ObserverType;

namespace Aion.GameServer.Controllers.Observer;

/// <summary>
/// Java parity: controllers/observer/ItemUseObserver (MrPoke). Aborting removes the observer from the observed player once and then runs
/// <see cref="OnAbort"/>.
/// </summary>
public abstract class ItemUseObserver : ActionObserver
{
    private readonly Player observed;
    private readonly AtomicBoolean aborted = new AtomicBoolean();

    protected ItemUseObserver(Player observed)
        : base(ATTACK, ATTACKED, DEATH, DOT_ATTACKED, EQUIP, UNEQUIP, MOVE, STARTSKILLCAST, ENDSKILLCAST, SIT, ITEMUSE, ABNORMALSETTED, BOOSTSKILLCOST)
    {
        this.observed = observed;
    }

    public sealed override void Attack(Creature creature, int skillId)
    {
        Abort();
    }

    public sealed override void Attacked(Creature creature, int skillId)
    {
        Abort();
    }

    public sealed override void Died(Creature creature)
    {
        Abort();
    }

    public sealed override void Dotattacked(Creature creature, Effect dotEffect)
    {
        Abort();
    }

    public sealed override void Equip(Item item, Player owner)
    {
        Abort();
    }

    public sealed override void Unequip(Item item, Player owner)
    {
        Abort();
    }

    public sealed override void Moved()
    {
        Abort();
    }

    public sealed override void StartSkillCast(Skill skill)
    {
        Abort();
    }

    public sealed override void Sit()
    {
        Abort();
    }

    public override void EndSkillCast(Skill skill)
    {
        Abort();
    }

    public override void Itemused(Item item)
    {
        Abort();
    }

    public override void Abnormalsetted(AbnormalState state)
    {
        if ((state.GetId() & AbnormalState.CANCEL_ITEM_USE.GetId()) != 0)
            Abort();
    }

    public override void BoostSkillCost(Skill skill)
    {
        Abort();
    }

    public void Abort()
    {
        if (aborted.CompareAndSet(false, true))
        {
            observed.GetObserveController().RemoveObserver(this);
            OnAbort();
        }
    }

    protected abstract void OnAbort();
}
