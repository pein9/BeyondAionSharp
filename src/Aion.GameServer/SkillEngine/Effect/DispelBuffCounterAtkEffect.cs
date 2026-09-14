using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/DispelBuffCounterAtkEffect : DamageEffect. @XmlAttribute dpower/power/hitvalue/hitdelta; @XmlAttribute(name="dispel_level"); resolveMagicalCritical no-op (never crits); knowledge/boost-spell-attack/one-time-boost off; applyEffect→dispelBuffCounterAtkEffect; calculateDamage: count=base, finalPower, calculateBuffsOrEffectorDebuffsToRemove, valueWithDelta formula, calculateSkillResult; shouldApplyAttackerMovementModifier→false; endEffect→resetDesignatedDispelEffect+super. AttackUtil/Creature red-tolerated.</summary>
[XmlType("DispelBuffCounterAtkEffect")]
public class DispelBuffCounterAtkEffect : DamageEffect
{
    [XmlAttribute]
    public int dpower;
    [XmlAttribute]
    public int power;
    [XmlAttribute]
    public int hitvalue;
    [XmlAttribute]
    public int hitdelta;
    [XmlAttribute("dispel_level")]
    public int dispelLevel;

    protected override void ResolveMagicalCritical(Effect effect)
    {
        // this effect type deals its damage without the magical attack calculation, so it never crits and never decides the critical of other effects
    }

    public override void ApplyEffect(Effect effect)
    {
        base.ApplyEffect(effect);
        effect.GetEffected().GetEffectController().DispelBuffCounterAtkEffect(effect);
    }

    public override void CalculateDamage(Effect effect)
    {
        Creature effected = effect.GetEffected();
        int count = CalculateBaseValue(effect);
        int finalPower = power + dpower * effect.GetSkillLevel();

        int dispelledEffectCount = effected.GetEffectController().CalculateBuffsOrEffectorDebuffsToRemove(effect, count, dispelLevel, finalPower);
        int valueWithDelta = dispelledEffectCount > 0 ? hitvalue + ((hitvalue / 2) * (dispelledEffectCount - 1)) + hitdelta * effect.GetSkillLevel() : 0;
        AttackUtil.CalculateSkillResult(effect, valueWithDelta, this, false);
    }

    public override bool ShouldApplyAttackerMovementModifier()
    {
        return false;
    }

    public override void EndEffect(Effect effect)
    {
        effect.GetEffected().GetEffectController().ResetDesignatedDispelEffect(effect);
        base.EndEffect(effect);
    }

    public override bool ShouldUseKnowledge()
    {
        return false;
    }

    public override bool ShouldUseBoostSpellAttackEffects()
    {
        return false;
    }

    public override bool ShouldUseOneTimeBoostSkillAttack()
    {
        return false;
    }
}
