using System.Collections.Generic;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Scene;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Materials;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Controllers.Observer;

/// <summary>Java parity: controllers/observer/ZoneCollisionMaterialActor (Rolandas) : AbstractMaterialSkillActor. CollisionIntention.MATERIAL.getId()→(sbyte) (base sbyte intentions); collisionResults.size()→Size(); instanceof Player→is Player. CollisionResults/Spatial red-tolerated.</summary>
public class ZoneCollisionMaterialActor : AbstractMaterialSkillActor
{
    private volatile bool isTouched;

    public ZoneCollisionMaterialActor(Creature creature, Spatial geometry, MaterialTemplate template, CheckType checkType, List<MaterialSkill> skills)
        : base(creature, geometry, (sbyte)CollisionIntention.MATERIAL.GetId(), checkType, TaskId.ZONE_MATERIAL_ACTION, template.GetId(), skills)
    {
    }

    public override void Died(Creature creature)
    {
        base.Died(creature);
        isTouched = false;
    }

    public override void OnMoved(CollisionResults collisionResults)
    {
        bool oldTouched = isTouched;
        isTouched = collisionResults.Size() > 0;
        if (oldTouched != isTouched)
        {
            if (isTouched)
                Act();
            else
                Abort();
            if (GeoDataConfig.GEO_MATERIALS_SHOWDETAILS && creature is Player player && player.IsStaff())
            {
                Spatial geom = collisionResults.Size() > 0 ? collisionResults.GetClosestCollision().GetGeometry() : geometry;
                PacketSendUtility.SendMessage(player, (isTouched ? "Touched " : "Untouched ") + geom.GetName());
            }
        }
    }
}
