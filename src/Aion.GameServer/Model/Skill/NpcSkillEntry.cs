using System;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Skill;

/// <summary>Java parity: model/skill/NpcSkillEntry. Shared stateless metadata; cooldowns belong to each NPC.</summary>
public abstract class NpcSkillEntry : SkillEntry
{
    protected NpcSkillEntry(int skillId, int skillLevel) : base(skillId, skillLevel)
    {
    }

    public abstract bool IsReady(int hpPercentage, long fightingTimeInMSec);

    public abstract bool ChanceReady();

    public abstract bool HpReady(int hpPercentage);

    public abstract bool TimeReady(long fightingTimeInMSec);

    public abstract bool HasPostSpawnCondition();

    public abstract int GetPriority();

    public abstract bool ConditionReady(Creature creature);

    public abstract NpcSkillConditionTemplate GetConditionTemplate();

    public abstract bool HasCondition();

    public abstract int GetNextSkillTime();

    public abstract bool HasChain();

    public abstract int GetNextChainId();

    public abstract int GetChainId();

    public abstract bool CanUseNextChain(Npc owner);

    public abstract NpcSkillTemplate GetTemplate();

    public abstract void FireOnEndCastEvents(Npc npc);
}
