using System;
using System.Threading;
using System.Threading.Tasks;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Controllers.Observer;

/// <summary>Resolved values of the creature's last applied one-time skill attack boost.</summary>
public sealed class OneTimeBoostSkillAttack
{
    private readonly Effect effect;
    private readonly SkillType type;
    private readonly int dmgPercent, dmgFlat, accBonusPercent, accBonusFlat;
    private int remainingCount;

    public OneTimeBoostSkillAttack(Effect effect, SkillType type, int dmgPercent, int dmgFlat, int accBonusPercent, int accBonusFlat, int count)
    {
        this.effect = effect;
        this.type = type;
        this.dmgPercent = dmgPercent;
        this.dmgFlat = dmgFlat;
        this.accBonusPercent = accBonusPercent;
        this.accBonusFlat = accBonusFlat;
        remainingCount = count;
    }

    public Effect GetEffect() => effect;
    public bool Applies(SkillType attackType) => type == SkillType.ALL || type == attackType;
    public float CalculateDamage(float damage) => damage * unchecked(100 + dmgPercent) / 100f + dmgFlat;
    public int CalculatePhysicalAccuracyBonus(Stat2 accuracy) => unchecked(accuracy.GetBase() * accBonusPercent / 100 + accBonusFlat);

    public int CalculateMagicalAccuracy(Stat2 accuracy, int accMod)
    {
        int baseAcc = unchecked(accuracy.GetCurrent() + accMod);
        float boostedAcc = baseAcc + baseAcc * (accuracy.GetFixedBonusRate() + accBonusPercent / 100f)
            + accuracy.GetExactBonus() * accuracy.GetBonusRate() + accBonusFlat;
        // Java narrows float to int with saturation (NaN -> 0), then clamps at zero.
        return float.IsNaN(boostedAcc) || boostedAcc <= 0 ? 0 : boostedAcc >= int.MaxValue ? int.MaxValue : (int)boostedAcc;
    }

    public void ConsumeCharge()
    {
        if (dmgPercent == 0 && dmgFlat == 0)
            return;
        // All targets/effects are calculated in a row; keep the final charge's
        // bonus available to the rest of the attack before ending the boost.
        if (Interlocked.Decrement(ref remainingCount) == 0)
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                effect.EndEffect();
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(10));
    }
}
