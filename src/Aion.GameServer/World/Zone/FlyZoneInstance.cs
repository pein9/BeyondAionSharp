using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Zone;

namespace Aion.GameServer.World.Zone;

/// <summary>Java parity: world/zone/FlyZoneInstance (MrPoke) : ZoneInstance. synchronized onEnter/onLeave→lock(this); super→base; instanceof→cast/is. ZoneType.FLY.</summary>
public class FlyZoneInstance : ZoneInstance
{
    public FlyZoneInstance(int mapId, ZoneInfo template) : base(mapId, template)
    {
    }

    public override bool OnEnter(Creature creature)
    {
        lock (this)
        {
            if (!base.OnEnter(creature)) return false;
            bool wasInFlyZone = creature.IsInsideFlyZone();
            creature.SetInsideZoneType(ZoneType.FLY);
            if (!wasInFlyZone && creature is Player player && player.IsInsideFlyZone())
                player.GetController().OnEnterFlyArea();
            return true;
        }
    }

    public override bool OnLeave(Creature creature)
    {
        lock (this)
        {
            if (!base.OnLeave(creature)) return false;
            bool wasInFlyZone = creature.IsInsideFlyZone();
            creature.UnsetInsideZoneType(ZoneType.FLY);
            if (wasInFlyZone && creature is Player player && !player.IsInsideFlyZone())
                player.GetController().OnLeaveFlyArea();
            return true;
        }
    }
}
