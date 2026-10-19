using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Templates.Npcskill;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Model.Skill;

/// <summary>Java parity: model/skill/NpcSkillList. Shared stateless entries, stably sorted by descending priority.</summary>
public class NpcSkillList
{
    private static readonly ILogger log = AionLog.For(nameof(NpcSkillList));
    private readonly List<NpcSkillEntry> skillsSortedByPrioDescending;

    public NpcSkillList(List<NpcSkillTemplate> npcSkills)
    {
        var entries = new List<NpcSkillEntry>(npcSkills.Count);
        foreach (NpcSkillTemplate template in npcSkills)
        {
            if (DataManager.SKILL_DATA.GetSkillTemplate(template.GetSkillId()) == null)
            {
                log.LogWarning("Missing skill data for skill {TemplateGetSkillId} in npc skill list", template.GetSkillId());
                continue;
            }
            entries.Add(new NpcSkillTemplateEntry(template));
        }
        // Java List.sort is stable; preserve XML order among equal priorities.
        skillsSortedByPrioDescending = entries.OrderByDescending(entry => entry.GetPriority()).ToList();
    }

    public int Size() => skillsSortedByPrioDescending.Count;
    public bool IsEmpty() => skillsSortedByPrioDescending.Count == 0;
    public NpcSkillEntry GetRandomSkill() => Rnd.Get(skillsSortedByPrioDescending);

    public NpcSkillEntry GetSkillOnPosition(int position)
    {
        if (skillsSortedByPrioDescending.Count == 0)
            return null;
        if (position >= skillsSortedByPrioDescending.Count)
            position = skillsSortedByPrioDescending.Count - 1;
        return skillsSortedByPrioDescending[position];
    }

    public List<NpcSkillEntry> GetPostSpawnSkills() =>
        skillsSortedByPrioDescending.Where(skill => skill.HasPostSpawnCondition()).ToList();

    public List<NpcSkillEntry> GetSkillsSortedByPriority(int priority)
    {
        var result = new List<NpcSkillEntry>();
        foreach (NpcSkillEntry skill in skillsSortedByPrioDescending)
        {
            if (skill.GetPriority() == priority)
                result.Add(skill);
            else if (skill.GetPriority() < priority)
                break;
        }
        return result;
    }

    public int[] GetPriorities() => skillsSortedByPrioDescending.Select(skill => skill.GetPriority()).Distinct().ToArray();

    public List<NpcSkillEntry> GetChainSkillsSortedByPriority(NpcSkillEntry current)
    {
        int id = current.GetNextChainId();
        return id > 0 ? skillsSortedByPrioDescending.Where(skill => skill.GetChainId() == id).ToList() : new List<NpcSkillEntry>();
    }
}
