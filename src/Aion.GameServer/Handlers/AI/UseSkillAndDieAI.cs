using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/UseSkillAndDieAI (Yeats).</summary>
[AIName("useSkillAndDie")]
public class UseSkillAndDieAI : NpcAI
{
    private static readonly ILogger log = AionLog.For(nameof(UseSkillAndDieAI));

    private volatile bool canDie = true;

    public UseSkillAndDieAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ScheduleSkill();
    }

    private void ScheduleSkill()
    {
        NpcSkillEntry skill = GetOwner().GetSkillList().GetSkillOnPosition(0);
        if (skill == null)
        {
            log.LogWarning(GetOwner() + " has no skill list");
            GetOwner().GetController().Delete();
            return;
        }

        NpcSkillConditionTemplate conditionTemplate = skill.GetConditionTemplate();
        if (conditionTemplate != null)
        {
            canDie = conditionTemplate.CanDie();
            ThreadPoolManager.GetInstance().Schedule(
                _ =>
                {
                    if (GetOwner().IsDead() || !GetOwner().IsSpawned())
                        return ValueTask.CompletedTask;
                    if (GetCreatorId() == 0 || (GetKnownList().GetObject(GetCreatorId()) is Creature creator && !creator.IsDead()))
                    {
                        SkillEngine.SkillEngine.GetInstance()
                            .GetSkill(GetOwner(), skill.GetSkillId(), skill.GetSkillLevel(), GetOwner())
                            .UseSkill();
                    }

                    ThreadPoolManager.GetInstance().Schedule(__ => { GetOwner().GetController().Delete(); return ValueTask.CompletedTask; }, (long)conditionTemplate.GetDespawnTime());
                    return ValueTask.CompletedTask;
                },
                (long)conditionTemplate.GetDelay());
        }
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return canDie ? damage : 0;
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        GetOwner().GetController().Delete();
    }
}
