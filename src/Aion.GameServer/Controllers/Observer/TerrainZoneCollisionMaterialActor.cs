using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Materials;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Controllers.Observer;

public class TerrainZoneCollisionMaterialActor : AbstractMaterialSkillActor
{
    public TerrainZoneCollisionMaterialActor(Creature creature)
        : base(creature, null!, (sbyte)CollisionIntention.MATERIAL.GetId(), CheckType.TOUCH, TaskId.TERRAIN_MATERIAL_ACTION, 0, new List<MaterialSkill>()) { }

    public override void OnMoved(CollisionResults collisionResults) { }

    public override void Died(Creature creature)
    {
        base.Died(creature);
        materialId = 0;
    }

    public override void Moved()
    {
        int matId = GeoService.GetInstance().GetTerrainMaterialAt(creature.GetWorldId(), creature.GetX(), creature.GetY(), creature.GetZ(), creature.GetInstanceId());
        if (materialId == matId) return;
        materialId = matId;
        Abort();
        MaterialTemplate? template = matId == 0 ? null : DataManager.MATERIAL_DATA.GetTemplate(matId);
        if (template != null)
        {
            skills = template.GetSkills(creature);
            Act();
        }
    }
}
