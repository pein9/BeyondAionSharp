using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Model.Geometry;

/// <summary>Java parity: model/geometry/Area — basic interface for all areas in AionEmu.</summary>
public interface Area
{
    bool IsInside2D(Point2D point);
    bool IsInside2D(float x, float y);
    bool IsInside3D(Point3D point);
    bool IsInside3D(float x, float y, float z);
    bool IsInsideZ(Point3D point);
    bool IsInsideZ(float z);

    double GetDistance2D(Point2D point);
    double GetDistance2D(float x, float y);
    double GetDistance3D(Point3D point);
    double GetDistance3D(float x, float y, float z);

    Point2D GetClosestPoint(Point2D point);
    Point2D GetClosestPoint(float x, float y);
    Point3D GetClosestPoint(Point3D point);
    Point3D GetClosestPoint(float x, float y, float z);

    float GetMinZ();
    float GetMaxZ();

    bool IntersectsRectangle(RectangleArea area);

    static Area Create(ZoneTemplate zone) => zone.GetAreaType() switch
    {
        AreaType.Polygon => new PolyArea(zone.GetPoints()!.GetPoints(), zone.GetPoints().GetBottom(), zone.GetPoints().GetTop()),
        AreaType.Cylinder => new CylinderArea(zone.GetCylinder()!.GetX()!.Value, zone.GetCylinder().GetY()!.Value,
            zone.GetCylinder().GetR()!.Value, zone.GetCylinder().GetBottom()!.Value, zone.GetCylinder().GetTop()!.Value),
        AreaType.Sphere => new SphereArea(zone.GetSphere()!.GetX()!.Value, zone.GetSphere().GetY()!.Value,
            zone.GetSphere().GetZ()!.Value, zone.GetSphere().GetR()!.Value),
        AreaType.Semisphere => new SemisphereArea(zone.GetSemisphere()!.GetX()!.Value, zone.GetSemisphere().GetY()!.Value,
            zone.GetSemisphere().GetZ()!.Value, zone.GetSemisphere().GetR()!.Value),
        _ => throw new ArgumentOutOfRangeException(nameof(zone))
    };
}
