using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.GeoEngine.Scene;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Materials;
using Aion.GameServer.Model.Templates.World;
using Aion.GameServer.Services;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Time.Gametime;

namespace Aion.GameServer.Controllers.Observer;

/// <summary>Java material actor: all eligible skills use per-creature periods and conjunctive conditions.</summary>
public abstract class AbstractMaterialSkillActor : AbstractCollisionObserver
{
    private ScheduledTask? task;
    private readonly TaskId taskId;
    protected volatile List<MaterialSkill> skills;
    protected volatile int materialId;

    public AbstractMaterialSkillActor(Creature creature, Spatial geometry, sbyte intentions, CheckType checkType, TaskId taskId, int materialId, List<MaterialSkill> skills)
        : base(creature, geometry, intentions, checkType, ObserverType.DEATH)
    {
        this.taskId = taskId;
        this.materialId = materialId;
        this.skills = skills;
    }

    public void Act()
    {
        if (skills.Count != 0 && !creature.GetController().HasTask(taskId))
        {
            MaterialSkillTask msTask = new MaterialSkillTask(this);
            ScheduledTask t = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct => { msTask.Run(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(1000));
            if (Interlocked.CompareExchange(ref task, t, null) == null)
                creature.GetController().AddTask(taskId, task!);
            else // should not happen
                t.Cancel(false);
        }
    }

    public void Abort()
    {
        ScheduledTask? t = Interlocked.Exchange(ref task, null);
        if (t != null)
            creature.GetController().CancelTaskIfPresent(taskId, t);
    }

    public override void Died(Creature creature)
    {
        Abort();
    }

    private class MaterialSkillTask
    {
        private readonly AbstractMaterialSkillActor outer;

        public MaterialSkillTask(AbstractMaterialSkillActor outer)
        {
            this.outer = outer;
        }

        public void Run()
        {
            if (!outer.creature.IsSpawned() || outer.creature.IsDead()) return;
            if (outer.creature is Player player && (player.IsInFlyingState() || player.IsUsingFlightTransporterOrWindstream())) return;
            int materialId = outer.materialId;
            List<MaterialSkill> skills = outer.skills;
            MaterialSkillUsage usage = outer.creature.GetController().GetOrCreateMaterialSkillUsage();
            for (int slot = 0; slot < skills.Count; slot++)
            {
                MaterialSkill skill = skills[slot];
                if (!WeatherService.GetInstance().MatchesMaterialActConditions(outer.creature, skill.GetConditions()) || !usage.TryUse(materialId, slot, skill.GetFrequency()))
                    continue;
                if (GeoDataConfig.GEO_MATERIALS_SHOWDETAILS && outer.creature is Player staff && staff.IsStaff())
                    PacketSendUtility.SendMessage(staff, outer.GetType().Name + " use skill=" + skill.GetId());
                Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(skill.GetId(), skill.GetSkillLevel(), outer.creature, outer.creature, null, Effect.ForceType.MATERIAL_SKILL);
            }
        }
    }
}
