using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Controllers.Observer;

/// <summary>
/// Java parity: controllers/observer/ActionObserver (ATracer).
/// Default no-op observer; subclasses override the events they care about.
/// </summary>
public class ActionObserver
{
    private readonly ISet<ObserverType> _observerTypes;
    private bool _oneTimeUse;

    public ActionObserver(ObserverType firstType, params ObserverType[] otherTypes)
    {
        _observerTypes = new HashSet<ObserverType>(otherTypes) { firstType };
    }

    public bool Matches(ObserverType observerType)
    {
        return _observerTypes.Contains(observerType);
    }

    public void MakeOneTimeUse()
    {
        _oneTimeUse = true;
    }

    public bool IsOneTimeUse()
    {
        return _oneTimeUse;
    }

    /// <summary>
    /// Called when the observer was removed and no longer receives events
    /// </summary>
    public virtual void OnRemoved()
    {
    }

    public virtual void Moved()
    {
    }

    /// <param name="creature">who effected</param>
    /// <param name="skillId">effector skill id, which called this method</param>
    public virtual void Attacked(Creature creature, int skillId)
    {
    }

    public virtual void Attack(Creature creature, int skillId)
    {
    }

    public virtual void Equip(Item item, Player owner)
    {
    }

    public virtual void Unequip(Item item, Player owner)
    {
    }

    public virtual void StartSkillCast(Skill skill)
    {
    }

    public virtual void EndSkillCast(Skill skill)
    {
    }

    public virtual void BoostSkillCost(Skill skill)
    {
    }

    public virtual void Died(Creature lastAttacker)
    {
    }

    public virtual void Dotattacked(Creature creature, Effect dotEffect)
    {
    }

    public virtual void Itemused(Item item)
    {
    }

    public virtual void Abnormalsetted(AbnormalState state)
    {
    }

    public virtual void Summonrelease()
    {
    }

    public virtual void Sit()
    {
    }

    public virtual void HpChanged(int value)
    {
    }
}
