using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/AbstractOverTimeEffect (kecimis) abstract : EffectTemplate. @XmlAttribute(required=true)→[XmlAttribute] (required dropped); Future&lt;?&gt;→ScheduledTask; scheduleAtFixedRate(lambda)→ScheduleAtFixedRateTask(ct=>{OnPeriodicAction;...}); AbnormalState param nullable. Inherited Value/duration2/Position/OnPeriodicAction + EffectTemplate red-tolerated.</summary>
[XmlType("AbstractOverTimeEffect")]
public abstract class AbstractOverTimeEffect : EffectTemplate
{
    [XmlAttribute]
    public int checktime;
    [XmlAttribute]
    public bool percent;
    [XmlAttribute]
    public bool shared;

    public override int GetValue()
    {
        return Value;
    }

    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void StartEffect(Effect effect)
    {
        this.StartEffect(effect, null);
    }

    public void StartEffect(Effect effect, AbnormalState? abnormal)
    {
        Creature effected = effect.GetEffected();
        if (abnormal != null)
        {
            effect.SetAbnormal(abnormal.Value);
            effected.GetEffectController().SetAbnormal(abnormal.Value);
        }
        // TODO figure out what to do with such cases
        if (checktime == 0)
            return;
        int remainingTicks = effect.GetDuration() > 0 ? GetTickCount(effect.GetDuration()) : int.MaxValue;
        ScheduledTask task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct =>
        {
            // Java AtomicInteger.getAndDecrement returns the old value, including on overflow.
            int previousTicks = unchecked(System.Threading.Interlocked.Decrement(ref remainingTicks) + 1);
            if (previousTicks > 0) OnPeriodicAction(effect);
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(checktime), System.TimeSpan.FromMilliseconds(checktime));
        effect.SetPeriodicTask(task, Position);
    }

    public void EndEffect(Effect effect, AbnormalState? abnormal)
    {
        if (abnormal != null)
            effect.GetEffected().GetEffectController().UnsetAbnormal(abnormal.Value);
    }

    /// <summary>Round after all duration multipliers: whole intervals plus one second.</summary>
    public long RoundDurationToTicks(long duration) => duration <= 0 || checktime <= 0
        ? duration : unchecked(duration + 1000 - duration % checktime);

    public int GetTickCount(int duration) => duration <= 0 || checktime <= 0 ? 0 : (duration - 1) / checktime;
}
