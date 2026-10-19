using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>One-time attack boost: level-scaled damage, accuracy and charge parameters.</summary>
[XmlType("OneTimeBoostSkillAttackEffect")]
public class OneTimeBoostSkillAttackEffect : BufEffect
{
    [XmlAttribute] public int count;
    [XmlAttribute("count_delta")] public int countDelta;
    [XmlAttribute("dmg_flat")] public int dmgFlat;
    [XmlAttribute("dmg_flat_delta")] public int dmgFlatDelta;
    [XmlAttribute("acc_flat")] public int accFlat;
    [XmlAttribute("acc_flat_delta")] public int accFlatDelta;
    [XmlAttribute("acc_percent")] public int accPercent;
    [XmlAttribute("acc_percent_delta")] public int accPercentDelta;
    [XmlAttribute] public SkillType type;

    public override void StartEffect(Effect effect)
    {
        base.StartEffect(effect);
        // Missing JAXB type and NONE both mean no boost; the C# enum defaults to NONE.
        if (type == SkillType.NONE)
            return;
        int skillLvl = effect.GetSkillLevel();
        var boost = new OneTimeBoostSkillAttack(effect, type, CalculateBaseValue(effect), unchecked(dmgFlat + dmgFlatDelta * skillLvl),
            unchecked(accPercent + accPercentDelta * skillLvl), unchecked(accFlat + accFlatDelta * skillLvl), unchecked(count + countDelta * skillLvl));
        effect.GetEffected().GetObserveController().SetOneTimeBoostSkillAttack(boost);
    }

    public override void EndEffect(Effect effect)
        => effect.GetEffected().GetObserveController().RemoveOneTimeBoostSkillAttack(effect);

    public static SkillType? GetBoostedSkillType(EffectTemplate template)
    {
        if (template is SkillAttackInstantEffect or SkillAtkDrainInstantEffect or DashEffect or BackDashEffect or MoveBehindEffect or CarveSignetEffect)
            return SkillType.PHYSICAL;
        if (template is SpellAttackInstantEffect or DelayedSpellAttackInstantEffect or SpellAtkDrainInstantEffect)
            return SkillType.MAGICAL;
        return null;
    }

    public static OneTimeBoostSkillAttack? GetActiveBoost(Creature attacker, EffectTemplate template)
    {
        SkillType? attackType = GetBoostedSkillType(template);
        return attackType == null ? null : attacker.GetObserveController().GetOneTimeBoostSkillAttack(attackType.Value);
    }
}
