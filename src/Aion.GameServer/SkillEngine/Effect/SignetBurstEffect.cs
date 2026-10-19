using System;
using Aion.GameServer.Commons.Utils;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: consume the capped signet stack and scale damage and sub-effect probability by its data and the casting skill level.</summary>
[XmlType("SignetBurstEffect")]
public class SignetBurstEffect : DamageEffect
{
    [XmlAttribute]
    public int signetlvl;
    [XmlAttribute]
    public string signet;
    [XmlAttribute("add_effect_prob_multi")]
    public int addEffectProbMultiplier = 0;
    [XmlAttribute("add_effect_prob_multi_delta")]
    public float addEffectProbMultiplierDelta;

    public override void CalculateDamage(Effect effect)
    {
        Effect signetEffect = effect.GetEffected().GetEffectController().GetAbnormalEffect(signet);
        int valueWithDelta = CalculateBaseValue(effect);

        int effectProb = 0;
        int signetLvl = Math.Min(signetlvl, signetEffect == null ? 0 : signetEffect.GetSkillLevel());
        SignetData signetData = DataManager.SIGNET_DATA_TEMPLATES.GetSignetData(JavaEnum.ValueOf<SignetEnum>(signet), signetLvl);
        if (signetData != null)
        {
            valueWithDelta = (int)(valueWithDelta * signetData.GetDamageMultiplier());
            effectProb = JavaMath.ToInt((addEffectProbMultiplier + addEffectProbMultiplierDelta * effect.GetSkillLevel()) * signetData.GetAddEffectProb());
        }
        effect.SetSignetBurstedCount(signetLvl);
        AttackUtil.CalculateSkillResult(effect, valueWithDelta, this, false);
        effect.SetLaunchSubEffect(Rnd.Chance() < effectProb);
        if (signetEffect != null)
            signetEffect.EndEffect();
    }

    public override void Calculate(Effect effect)
    {
        Effect signetEffect = effect.GetEffected().GetEffectController().GetAbnormalEffect(signet);
        if (!base.Calculate(effect, null, null))
        {
            if (signetEffect != null)
            {
                signetEffect.EndEffect();
            }
        }
    }

    public string GetSignet()
    {
        return signet;
    }

    public override bool ShouldUseBoostSpellAttackEffects()
    {
        return false;
    }
}
