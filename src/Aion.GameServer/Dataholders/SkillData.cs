using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/SkillData (ATracer, Neon). @XmlRootElement(skill_data); LinkedHashMap→Dictionary; computeIfAbsent→TryGetValue+init; afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("skill_data")]
public class SkillData
{
    private static readonly ILogger log = AionLog.For(nameof(SkillData));

    // Public so XmlSerializer can populate it (JAXB read the private field via @XmlAccessorType(FIELD)).
    [XmlElement("skill_template")] public List<SkillTemplate> skillTemplates;

    [XmlIgnore] private readonly Dictionary<int, SkillTemplate> skillTemplateById = new();
    [XmlIgnore] private readonly Dictionary<string, List<SkillTemplate>> skillTemplatesByGroup = new();
    [XmlIgnore] private readonly Dictionary<string, List<SkillTemplate>> skillTemplatesByStack = new();

    [XmlIgnore] private readonly Dictionary<ItemGroup, ISet<int>> masterySkillsByWeapon = new();
    [XmlIgnore] private readonly Dictionary<ItemSubType, ISet<int>> masterySkillsByArmor = new();
    [XmlIgnore] private readonly ISet<int> shieldMasterySkills = new HashSet<int>();

    private static readonly ISet<int> NoSkills = ImmutableHashSet<int>.Empty;

    public void AfterUnmarshal(object parent)
    {
        skillTemplateById.Clear();
        skillTemplatesByGroup.Clear();
        skillTemplatesByStack.Clear();
        masterySkillsByWeapon.Clear();
        masterySkillsByArmor.Clear();
        shieldMasterySkills.Clear();
        foreach (SkillTemplate skillTemplate in skillTemplates)
        {
            // Java parity: JAXB fires Effects.afterUnmarshal (building the effectTypes set) per <effects>
            // element before the parent holder's afterUnmarshal; XmlSerializer does not invoke JAXB callbacks,
            // so fire it here, children-first, before indexing the template.
            skillTemplate.GetEffects()?.AfterUnmarshal(this);
            int skillId = skillTemplate.GetSkillId();
            skillTemplateById[skillId] = skillTemplate;
            if (skillTemplate.GetGroup() != null)
            {
                if (!skillTemplatesByGroup.TryGetValue(skillTemplate.GetGroup(), out var groupList))
                {
                    groupList = new List<SkillTemplate>();
                    skillTemplatesByGroup[skillTemplate.GetGroup()] = groupList;
                }
                groupList.Add(skillTemplate);
            }
            if (skillTemplate.GetStack() != null)
            {
                if (!skillTemplatesByStack.TryGetValue(skillTemplate.GetStack(), out var stackList))
                {
                    stackList = new List<SkillTemplate>();
                    skillTemplatesByStack[skillTemplate.GetStack()] = stackList;
                }
                stackList.Add(skillTemplate);
            }
            if (skillTemplate.GetEffects()?.GetEffects() != null)
                IndexMasterySkills(skillId, skillTemplate.GetEffects().GetEffects());
        }
        skillTemplates = null;
    }

    private void IndexMasterySkills(int skillId, List<EffectTemplate> effects)
    {
        foreach (EffectTemplate effect in effects)
        {
            switch (effect)
            {
                case WeaponMasteryEffect e:
                    if (e.GetItemGroup() == null)
                        throw new ArgumentException("Weapon mastery effect of skill " + skillId + " has no weapon attribute");
                    AddMasterySkill(masterySkillsByWeapon, e.GetItemGroup()!.Value, skillId);
                    break;
                case ArmorMasteryEffect e:
                    if (e.GetArmorType() == null)
                        throw new ArgumentException("Armor mastery effect of skill " + skillId + " has no armor attribute");
                    AddMasterySkill(masterySkillsByArmor, e.GetArmorType()!.Value, skillId);
                    break;
                case ShieldMasteryEffect:
                    shieldMasterySkills.Add(skillId);
                    break;
            }
        }
    }

    private static void AddMasterySkill<TKey>(Dictionary<TKey, ISet<int>> skillsByKey, TKey key, int skillId) where TKey : notnull
    {
        if (!skillsByKey.TryGetValue(key, out ISet<int>? skills))
            skillsByKey[key] = skills = new HashSet<int>();
        skills.Add(skillId);
    }

    /// <summary>The skills that allow equipping items of this group, empty if the group needs no mastery skill.</summary>
    public ISet<int> GetMasterySkills(ItemGroup? itemGroup)
    {
        if (itemGroup == null || !itemGroup.Value.RequiresMastery())
            return NoSkills;
        if (itemGroup == ItemGroup.SHIELD)
            return shieldMasterySkills;
        if (itemGroup.Value.GetEquipType() == EquipType.WEAPON)
            return masterySkillsByWeapon.TryGetValue(itemGroup.Value, out ISet<int>? weaponSkills) ? weaponSkills : NoSkills;
        return masterySkillsByArmor.TryGetValue(itemGroup.Value.GetItemSubType(), out ISet<int>? armorSkills) ? armorSkills : NoSkills;
    }

    public SkillTemplate GetSkillTemplate(int skillId)
    {
        return skillTemplateById.TryGetValue(skillId, out var v) ? v : null;
    }

    /// <summary>All skill templates of this group. A group is less precise and may be null.</summary>
    public List<SkillTemplate> GetSkillTemplatesByGroup(string skillGroup)
    {
        return skillTemplatesByGroup.TryGetValue(skillGroup, out var v) ? v : null;
    }

    /// <summary>All skill templates of this stack. A stack is more precise than a group.</summary>
    public List<SkillTemplate> GetSkillTemplatesByStack(string skillStack)
    {
        return skillTemplatesByStack.TryGetValue(skillStack, out var v) ? v : null;
    }

    public int Size()
    {
        return skillTemplateById.Count;
    }

    public ICollection<SkillTemplate> GetSkillTemplates()
    {
        return skillTemplateById.Values;
    }

    public void ValidateMotions()
    {
        StringBuilder missing = new StringBuilder();
        HashSet<string> motionNames = new HashSet<string>();
        foreach (SkillTemplate t in GetSkillTemplates())
        {
            Motion m = t.GetMotion();
            if (m == null || m.GetName() == null)
                continue;
            if (motionNames.Add(m.GetName()))
            {
                MotionTime mt = DataManager.MOTION_DATA.GetMotionTime(m.GetName());
                if (mt == null)
                    missing.Append('"').Append(m.GetName()).Append("\" (skill id ").Append(t.GetSkillId()).Append("), ");
            }
        }
        if (missing.Length > 0)
            log.LogWarning("Missing motion times for these motion names: {Missing}", missing.ToString().Substring(0, missing.Length - 2));
    }
}
