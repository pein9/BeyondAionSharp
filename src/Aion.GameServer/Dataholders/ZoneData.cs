using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Zone;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Dataholders;

/// <summary>Java ZoneData: static named areas, duplicate validation and weather-zone numbering.</summary>
[XmlType("")]
[XmlRoot("zones")]
public class ZoneData
{
    [XmlElement("zone")] public List<ZoneTemplate> zoneList = null!;
    [XmlIgnore] private readonly Dictionary<int, List<ZoneInfo>> zoneNameMap = new();
    [XmlIgnore] private readonly HashSet<string> zoneNames = new(StringComparer.Ordinal);
    [XmlIgnore] private readonly Dictionary<ZoneTemplate, int> weatherZoneIds = new();

    // C# streaming-loader seam: concatenate imported rows before the single JAXB-style callback.
    public void MergePending(ZoneData other)
    {
        if (other.zoneList == null) return;
        zoneList ??= new List<ZoneTemplate>();
        zoneList.AddRange(other.zoneList);
    }

    public void AfterUnmarshal(object parent)
    {
        int lastMapId = 0, weatherZoneId = 1;
        foreach (ZoneTemplate zone in zoneList)
        {
            if (!zoneNameMap.TryGetValue(zone.GetMapid(), out var zones))
                zoneNameMap[zone.GetMapid()] = zones = new List<ZoneInfo>();
            if (zone.GetZoneType() == ZoneClassName.WEATHER)
            {
                if (lastMapId != zone.GetMapid())
                {
                    lastMapId = zone.GetMapid();
                    weatherZoneId = 1;
                }
                weatherZoneIds[zone] = weatherZoneId++;
            }
            var info = new ZoneInfo(zone);
            zones.Add(info);
            if (!zoneNames.Add(info.GetZoneTemplate().GetName()!))
                throw new ArgumentException("Duplicate zone name: " + info.GetZoneTemplate().GetName());
        }
        zoneList = null!;
    }

    public IReadOnlyList<ZoneInfo> GetZones(int mapId) =>
        zoneNameMap.TryGetValue(mapId, out var zones) ? zones : Array.Empty<ZoneInfo>();

    public bool ValidateZoneName(string zoneName)
    {
        if (IsValidZoneName(zoneName)) return true;
        AionLog.For(nameof(ZoneData)).LogWarning("Missing data for zone: {ZoneName}", zoneName);
        return false;
    }

    public bool IsValidZoneName(string zoneName) => zoneNames.Contains(zoneName);
    public int Size() => zoneNames.Count;
    public int GetWeatherZoneId(ZoneTemplate template) => weatherZoneIds.GetValueOrDefault(template, 0);
}
