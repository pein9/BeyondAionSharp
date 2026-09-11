using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Zone (ATracer).</summary>
public class Zone : AdminCommand
{
    public Zone()
        : base("zone", "Shows zone information.", """
             - Shows info about your target's current zone(s).
            <zone name> - Shows info about your target's current zone(s), filtered by the given zone name.
            refresh - Refreshes your zones.
            """)
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length > 1)
        {
            SendInfo(admin);
            return;
        }
        if (paramsArr.Length == 1 && "refresh".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            admin.RevalidateZones();
            return;
        }
        Creature target = admin.GetTarget() is Creature creature ? creature : admin;
        string zoneNameParam = paramsArr.Length == 0 ? null : paramsArr[0];
        List<ZoneInstance> zones = FindZones(target, zoneNameParam);
        string zoneTypes = string.Join(", ", ((ZoneType[])Enum.GetValues(typeof(ZoneType))).Where(target.IsInsideZoneType).Select(zt => zt.ToString()));
        if (!string.IsNullOrEmpty(zoneTypes))
            SendInfo(admin, Name(target) + "'s zone types: " + zoneTypes);
        if (zones.Count == 0)
        {
            SendInfo(admin, Name(target) + " is not in " + (zoneNameParam == null ? "any zone" : zoneNameParam) + '.');
        }
        else
        {
            SendInfo(admin, Name(target) + "'s " + (zones.Count == 1 ? "zone" : "zones") + ':');
            foreach (ZoneInstance zone in zones)
            {
                SendInfo(admin, zone.GetAreaTemplate().GetZoneName().Name);
                SendInfo(admin, "Fly: " + JavaString.ValueOf(zone.CanFly()) + "; Glide: " + JavaString.ValueOf(zone.CanGlide()));
                SendInfo(admin, "Ride: " + JavaString.ValueOf(zone.CanRide()) + "; Fly-ride: " + JavaString.ValueOf(zone.CanFlyRide()));
                SendInfo(admin, "Kisk: " + JavaString.ValueOf(zone.CanPutKisk()) + "; Recall: " + JavaString.ValueOf(zone.CanRecall()));
                SendInfo(admin, "Same race duels: " + JavaString.ValueOf(zone.IsSameRaceDuelsAllowed()) + "; Other race duels: " + JavaString.ValueOf(zone.IsOtherRaceDuelsAllowed()));
                SendInfo(admin, "PvP: " + JavaString.ValueOf(zone.IsPvpAllowed()));
                SendInfo(admin, "canReturnBattle: " + JavaString.ValueOf(zone.CanReturnToBattle()));
            }
        }
    }

    private List<ZoneInstance> FindZones(Creature creature, string zoneNameFilter)
    {
        List<ZoneInstance> zones = creature.FindZones();
        if (zoneNameFilter != null)
        {
            ZoneName zoneName = ZoneName.Get(zoneNameFilter);
            if (zoneName == ZoneName.NONE)
                throw new ArgumentException("Invalid zone name.");
            zones = zones.Where(zone => zone.GetZoneTemplate().GetName() == zoneName).ToList();
        }
        return zones;
    }
}
