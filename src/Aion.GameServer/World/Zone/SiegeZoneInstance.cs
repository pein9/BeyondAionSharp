using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Siege;

namespace Aion.GameServer.World.Zone;

/// <summary>Java parity: world/zone/SiegeZoneInstance.</summary>
public class SiegeZoneInstance : ZoneInstance
{
    public SiegeZoneInstance(int mapId, ZoneInfo template)
        : base(mapId, template)
    {
    }

    public override bool IsIgnored(Creature creature) => base.IsIgnored(creature) && creature is not SiegeNpc;
}
