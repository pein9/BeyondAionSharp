using Aion.GameServer.Model.Geometry;

namespace Aion.GameServer.Model.Templates.Zone;

/// <summary>Java parity: model/templates/zone/ZoneInfo — pairs an Area instance with its template.</summary>
public class ZoneInfo
{
    private readonly Area _area;
    private readonly ZoneTemplate _zoneTemplate;

    public ZoneInfo(ZoneTemplate zoneTemplate)
    {
        _area         = Area.Create(zoneTemplate);
        _zoneTemplate = zoneTemplate;
    }

    public Area         GetArea()         => _area;
    public ZoneTemplate GetZoneTemplate() => _zoneTemplate;

    public bool Matches(string zoneName)
    {
        if (_zoneTemplate.GetName()!.Equals(zoneName)) return true;
        Aion.GameServer.Dataholders.DataManager.ZONE_DATA.ValidateZoneName(zoneName);
        return false;
    }
}
