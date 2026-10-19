using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Controllers;

/// <summary>Java parity: controllers/SiegeWeaponController (xTz).</summary>
public class SiegeWeaponController : SummonController
{
    public override void OnReleaseStart()
    {
        GetMaster().GetController().CancelTask(TaskId.SUMMON_FOLLOW);
        GetOwner().GetMoveController().AbortMove();
    }

    public override void OnDespawn()
    {
        GetMaster().GetController().CancelTask(TaskId.SUMMON_FOLLOW);
        base.OnDespawn();
    }

    public override void RestMode()
    {
        GetMaster().GetController().CancelTask(TaskId.SUMMON_FOLLOW);
        base.RestMode();
        GetOwner().GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.StopFollowMe, GetMaster());
    }

    public override void SetUnkMode()
    {
        base.SetUnkMode();
        GetMaster().GetController().CancelTask(TaskId.SUMMON_FOLLOW);
    }

    public sealed override void GuardMode()
    {
        base.GuardMode();
        GetMaster().GetController().CancelTask(TaskId.SUMMON_FOLLOW);
        GetOwner().SetTarget(GetMaster());
        GetOwner().GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.FollowMe, GetMaster());
        GetOwner().GetMoveController().MoveToTargetObject();
        GetMaster().GetController().AddTask(TaskId.SUMMON_FOLLOW, Aion.GameServer.Ai.Follow.FollowStartService.NewFollowingToTargetCheckTask(GetOwner(), GetMaster()));
    }

    public override void AttackMode(int targetObjId)
    {
        Creature target = (Creature)GetOwner().GetKnownList().GetObject(targetObjId);
        if (target == null || !Aion.GameServer.World.Geo.GeoService.GetInstance().CanSee(GetOwner(), target))
        {
            return;
        }
        if (IsValidTarget(target))
        {
            base.AttackMode(targetObjId);
            GetOwner().SetTarget(target);
            GetOwner().GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.FollowMe, target);
            GetOwner().GetMoveController().MoveToTargetObject();
            GetMaster().GetController().AddTask(TaskId.SUMMON_FOLLOW, Aion.GameServer.Ai.Follow.FollowStartService.NewFollowingToTargetCheckTask(GetOwner(), target));
        }
    }

    public bool IsValidTarget(Creature target)
    {
        Aion.GameServer.Model.GameObjects.Players.Player master = GetOwner().GetMaster();
        if (master == null)
        {
            return false;
        }
        Race masterRace = master.GetRace();
        if (!IsBalaurBoss(target))
        {
            if (masterRace == Race.ASMODIANS && target.GetRace() != Race.PC_LIGHT_CASTLE_DOOR && target.GetRace() != Race.DRAGON_CASTLE_DOOR
                    && target.GetRace() != Race.GCHIEF_LIGHT && target.GetRace() != Race.GCHIEF_DRAGON)
            {
                return false;
            }
            else if (masterRace == Race.ELYOS && target.GetRace() != Race.PC_DARK_CASTLE_DOOR && target.GetRace() != Race.DRAGON_CASTLE_DOOR
                    && target.GetRace() != Race.GCHIEF_DARK && target.GetRace() != Race.GCHIEF_DRAGON)
            {
                return false;
            }
        }
        return true;
    }

    private bool IsBalaurBoss(Creature creature)
    {
        return creature.GetRace() == Race.DRAKAN && creature is Aion.GameServer.Model.GameObjects.Siege.SiegeNpc
            && ((Aion.GameServer.Model.GameObjects.Siege.SiegeNpc)creature).GetObjectTemplate().GetRating() == Aion.GameServer.Model.Templates.Npc.NpcRating.LEGENDARY;
    }

}
