using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Zone;

namespace Aion.GameServer.World.Zone;

/// <summary>Java parity: world/zone/DisablePvPZoneInstance. Neutral zones override PvP regardless of other overlapping zones.</summary>
public class DisablePvPZoneInstance : ZoneInstance
{
    public DisablePvPZoneInstance(int mapId, ZoneInfo template) : base(mapId, template)
    {
    }

    public override bool OnEnter(Creature creature)
    {
        lock (this)
        {
            if (base.OnEnter(creature))
            {
                creature.SetInsideZoneType(ZoneType.DISABLE_PVP);
                return true;
            }
            return false;
        }
    }

    public override bool OnLeave(Creature creature)
    {
        lock (this)
        {
            if (base.OnLeave(creature))
            {
                creature.UnsetInsideZoneType(ZoneType.DISABLE_PVP);
                return true;
            }
            return false;
        }
    }
}
