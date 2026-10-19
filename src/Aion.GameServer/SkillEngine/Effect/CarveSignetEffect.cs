using System;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Dataholders;
using System.Xml.Serialization;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: calculate the carve before packet emission; failed or capped carves refresh existing signets.</summary>
[XmlType("CarveSignetEffect")]
public class CarveSignetEffect : DamageEffect
{
    [XmlAttribute("signet_increment")]
    public int signetIncrement = 1;
    [XmlAttribute("signet_cap")]
    public int signetCap;
    [XmlAttribute("signet_id")]
    public int signetId;
    [XmlAttribute]
    public string signet;
    [XmlAttribute]
    public int prob = 100;
    [XmlAttribute("prob_delta")]
    public int probDelta;

    public override void Calculate(Effect effect)
    {
        if (!base.Calculate(effect, null, null))
            return;
        Effect activeSignet = effect.GetEffected().GetEffectController().GetAbnormalEffect(signet);
        int currentLevel = activeSignet == null ? 0 : activeSignet.GetSkillLevel();
        int nextSignetLevel = currentLevel;
        if (currentLevel < signetCap && Rnd.Chance() < CalculateCarveChance(effect, currentLevel))
            nextSignetLevel = Math.Min(currentLevel + signetIncrement, signetCap);
        effect.SetCarvedSignet(nextSignetLevel == currentLevel ? 0 : nextSignetLevel);
    }

    private int CalculateCarveChance(Effect effect, int currentLevel)
    {
        SignetData signetData = DataManager.SIGNET_DATA_TEMPLATES.GetSignetData(Aion.GameServer.Utils.JavaEnum.ValueOf<SignetEnum>(signet), currentLevel + 1);
        return Aion.GameServer.Utils.JavaMath.ToInt(unchecked(prob + probDelta * effect.GetSkillLevel()) / 100f * (signetData == null ? 100 : signetData.GetCarveProb()));
    }

    public override void ApplyEffect(Effect effect)
    {
        base.ApplyEffect(effect);

        Effect activeSignet = effect.GetEffected().GetEffectController().GetAbnormalEffect(signet);
        int currentLevel = activeSignet == null ? 0 : activeSignet.GetSkillLevel();
        int nextSignetLevel = Math.Max(currentLevel, effect.GetCarvedSignet());
        if (nextSignetLevel == 0)
            return;
        if (activeSignet != null)
            activeSignet.EndEffect();
        Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffect(signetId + nextSignetLevel - 1, effect.GetEffector(), effect.GetEffected());
    }
}
