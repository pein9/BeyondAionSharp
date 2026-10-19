using System.Xml.Serialization;
using Aion.GameServer.Model.Geometry;

namespace Aion.GameServer.Model.Templates.Flypath;

/// <summary>Java parity: model/templates/flypath/FlyPathEntry.</summary>
[XmlRoot("flypath_location")]
public class FlyPathEntry
{
    [XmlAttribute("id")] public int id;

    [XmlAttribute("sx")] public float startX;
    [XmlAttribute("sy")] public float startY;
    [XmlAttribute("sz")] public float startZ;
    [XmlAttribute("sworld")] public int sworld;

    [XmlAttribute("ex")] public float endX;
    [XmlAttribute("ey")] public float endY;
    [XmlAttribute("ez")] public float endZ;
    [XmlAttribute("eworld")] public int eworld;

    [XmlAttribute("time")] public float time;

    // An array preserves JAXB's null when no windstream_point elements are present.
    [XmlElement("windstream_point")] public WindstreamPoint[] windstreamPoints = null!;

    public int GetId()
    {
        return id;
    }

    public float GetStartX()
    {
        return startX;
    }

    public float GetStartY()
    {
        return startY;
    }

    public float GetStartZ()
    {
        return startZ;
    }

    public float GetEndX()
    {
        return endX;
    }

    public float GetEndY()
    {
        return endY;
    }

    public float GetEndZ()
    {
        return endZ;
    }

    public int GetStartWorldId()
    {
        return sworld;
    }

    public int GetEndWorldId()
    {
        return eworld;
    }

    public bool IsWindstream() => windstreamPoints != null;

    public Point3D GetWindstreamPosition(int progress)
    {
        int index = Math.Max(0, progress) / 1000;
        if (index >= windstreamPoints.Length - 1)
        {
            WindstreamPoint end = windstreamPoints[^1];
            return new Point3D(end.GetX(), end.GetY(), end.GetZ());
        }
        float ratio = (Math.Max(0, progress) % 1000) / 1000f;
        WindstreamPoint from = windstreamPoints[index], to = windstreamPoints[index + 1];
        return new Point3D(from.GetX() + (to.GetX() - from.GetX()) * ratio,
            from.GetY() + (to.GetY() - from.GetY()) * ratio, from.GetZ() + (to.GetZ() - from.GetZ()) * ratio);
    }

    public bool CanEnterWindstream(int progress)
    {
        int index = Math.Max(0, progress) / 1000;
        return index + 1 >= windstreamPoints.Length || windstreamPoints[index].CanEnter() || windstreamPoints[index + 1].CanEnter();
    }

    public bool CanLeaveWindstream(int progress)
    {
        int index = Math.Max(0, progress) / 1000;
        return index + 1 >= windstreamPoints.Length || windstreamPoints[index].CanExit() || windstreamPoints[index + 1].CanExit();
    }

    public int GetTimeInMs()
    {
        return (int)(time * 1000);
    }
}
