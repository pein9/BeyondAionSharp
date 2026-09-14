using System;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/HealEffectTemplate (Neon) interface w/ default calculateHealValue. C# default interface method delegates to a static helper so overriding classes can invoke the "interface default" (Java's HealEffectTemplate.super.calculateHealValue → CalculateHealValueDefault). Effect/StatEnum red-tolerated.</summary>
public interface HealEffectTemplate
{
    bool IsPercent();
    bool AllowHpHealBoost(Effect effect);
    bool AllowHpHealSkillDeboost(Effect effect);
    int GetCurrentStatValue(Effect effect);
    int GetMaxStatValue(Effect effect);
    int CalculateBaseHealValue(Effect effect);

    int CalculateSnapshotHealValue(Effect effect, HealType type)
    {
        int healValue = IsPercent() ? GetMaxStatValue(effect) * CalculateBaseHealValue(effect) / 100 : CalculateBaseHealValue(effect);
        if (type == HealType.HP && AllowHpHealBoost(effect))
        {
            // caster's heal boost from equipment, titles, etc. (capped at 1000 / 100% boost)
            int healBoost = effect.GetEffector().GetGameStats().GetStat(StatEnum.HEAL_BOOST, 0).GetCurrent();
            // caster's heal related effects (passive boosts, active buffs e.g. blessed shield)
            int healSkillBoost = effect.GetEffector().GetGameStats().GetStat(StatEnum.HEAL_SKILL_BOOST, 1000).GetCurrent() - 1000;
            healValue += (int)(healValue * Math.Clamp(healBoost + healSkillBoost, 0, 2000) / 1000f);
        }
        return healValue;
    }

    int ApplyHealDeboost(Effect effect, int healValue)
    {
        // apply target's heal related effects (e.g. brilliant protection)
        if (AllowHpHealSkillDeboost(effect))
            return Math.Max(0, effect.GetEffected().GetGameStats().GetStat(StatEnum.HEAL_SKILL_DEBOOST, healValue).GetCurrent());
        return healValue;
    }

    int CalculateHealValue(Effect effect, HealType type) => CalculateHealValueDefault(this, effect, type);

    // Java default-method body extracted to a static so AbstractHealEffect can invoke it (Java: HealEffectTemplate.super.calculateHealValue).
    static int CalculateHealValueDefault(HealEffectTemplate self, Effect effect, HealType type)
    {
        int healValue = self.CalculateSnapshotHealValue(effect, type);
        if (type == HealType.HP)
            healValue = self.ApplyHealDeboost(effect, healValue);
        return healValue;
    }
}
