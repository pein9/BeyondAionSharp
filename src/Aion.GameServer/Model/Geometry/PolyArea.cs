using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Model.Geometry;

/// <summary>Java PolyArea: a polygon using non-zero winding and a retained point list.</summary>
public class PolyArea : AbstractArea
{
    private readonly List<Point2D> points;
    // Straight-edge adapter for Path2D.Float with WIND_NON_ZERO, using packed float coordinates.
    private readonly float[] path;
    private readonly double minX, minY, width, height;

    public PolyArea(List<Point2D> points, float zMin, float zMax)
        : base(ValidatePoints(points, zMin), zMax)
    {
        this.points = points;
        path = new float[points.Count * 2];
        for (int i = 0; i < points.Count; i++)
        {
            path[i * 2] = points[i].GetX();
            path[i * 2 + 1] = points[i].GetY();
        }
        // JDK 25 calculates double bounds from the float path, starting with its first vertex.
        double left = path[0], right = left, bottom = path[1], top = bottom;
        for (int i = 2; i < path.Length; i += 2)
        {
            float x = path[i], y = path[i + 1];
            if (x < left) left = x;
            if (x > right) right = x;
            if (y < bottom) bottom = y;
            if (y > top) top = y;
        }
        minX = left; minY = bottom; width = right - left; height = top - bottom;
    }

    private static float ValidatePoints(List<Point2D> points, float zMin)
    {
        if (points.Count < 3) throw new ArgumentException("PolyArea needs at least 3 points but got " + points.Count);
        return zMin;
    }

    public override bool IsInside2D(float x, float y)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y) || !(x >= minX && y >= minY && x < (double)minX + width && y < (double)minY + height))
            return false;
        int winding = 0;
        for (int i = 0, previous = path.Length - 2; i < path.Length; previous = i, i += 2)
            winding += PointCrossing(x, y, path[previous], path[previous + 1], path[i], path[i + 1]);
        return winding != 0;
    }

    public override double GetDistance2D(float x, float y)
    {
        if (IsInside2D(x, y)) return 0;
        Point2D cp = GetClosestPoint(x, y);
        return PositionUtil.GetDistance(cp.GetX(), cp.GetY(), x, y);
    }

    public override double GetDistance3D(float x, float y, float z)
    {
        if (IsInside3D(x, y, z)) return 0;
        if (IsInsideZ(z)) return GetDistance2D(x, y);
        Point3D cp = GetClosestPoint(x, y, z);
        return PositionUtil.GetDistance(cp.GetX(), cp.GetY(), cp.GetZ(), x, y, z);
    }

    public override Point2D GetClosestPoint(float x, float y)
    {
        Point2D? closestPoint = null;
        double closestDistance = 0;
        for (int i = 0; i < points.Count; i++)
        {
            int nextIndex = (i + 1) % points.Count;
            float p1x = points[i].GetX(), p1y = points[i].GetY();
            float p2x = points[nextIndex].GetX(), p2y = points[nextIndex].GetY();
            Point2D point = PositionUtil.GetClosestPointOnSegment(p1x, p1y, p2x, p2y, x, y);
            if (closestPoint == null)
            {
                closestPoint     = point;
                closestDistance  = PositionUtil.GetDistance(closestPoint.GetX(), closestPoint.GetY(), x, y);
            }
            else
            {
                double newDistance = PositionUtil.GetDistance(point.GetX(), point.GetY(), x, y);
                if (newDistance < closestDistance)
                {
                    closestPoint    = point;
                    closestDistance = newDistance;
                }
            }
        }
        return closestPoint!;
    }

    public override bool IntersectsRectangle(RectangleArea area)
    {
        if (area.GetMinZ() > GetMaxZ() || area.GetMaxZ() < GetMinZ()) return false;
        double x = area.GetMinX(), y = area.GetMinY(), w = WorldConfig.WorldRegionSize, h = WorldConfig.WorldRegionSize;
        if (width <= 0 || height <= 0 || w <= 0 || h <= 0 || !(x + w > minX && y + h > minY && x < (double)minX + width && y < (double)minY + height))
            return false;
        if (double.IsNaN(x + w) || double.IsNaN(y + h)) return false;
        int winding = 0;
        for (int i = 0, previous = path.Length - 2; i < path.Length; previous = i, i += 2)
        {
            winding = RectangleCrossings(winding, x, y, x + w, y + h, path[previous], path[previous + 1], path[i], path[i + 1]);
            if (winding == int.MinValue) return true;
        }
        return winding != 0;
    }

    private static int PointCrossing(double x, double y, double x0, double y0, double x1, double y1)
    {
        if (y < y0 && y < y1 || y >= y0 && y >= y1 || x >= x0 && x >= x1) return 0;
        if (x < x0 && x < x1) return y0 < y1 ? 1 : -1;
        if (x >= x0 + (y - y0) * (x1 - x0) / (y1 - y0)) return 0;
        return y0 < y1 ? 1 : -1;
    }

    // Count crossings of the rectangle's right-hand shadow. A segment through its open interior
    // intersects immediately; boundary-only contact does not intersect, matching Path2D.
    private static int RectangleCrossings(int crossings, double left, double bottom, double right, double top,
        double x0, double y0, double x1, double y1)
    {
        if (y0 >= top && y1 >= top || y0 <= bottom && y1 <= bottom || x0 <= left && x1 <= left) return crossings;
        if (x0 >= right && x1 >= right) return ShadowCrossings();
        if (x0 > left && x0 < right && y0 > bottom && y0 < top || x1 > left && x1 < right && y1 > bottom && y1 < top)
            return int.MinValue;
        double clippedX0 = x0, clippedX1 = x1;
        if (y0 < bottom) clippedX0 += (bottom - y0) * (x1 - x0) / (y1 - y0);
        else if (y0 > top) clippedX0 += (top - y0) * (x1 - x0) / (y1 - y0);
        if (y1 < bottom) clippedX1 += (bottom - y1) * (x0 - x1) / (y0 - y1);
        else if (y1 > top) clippedX1 += (top - y1) * (x0 - x1) / (y0 - y1);
        if (clippedX0 <= left && clippedX1 <= left) return crossings;
        return clippedX0 >= right && clippedX1 >= right ? ShadowCrossings() : int.MinValue;

        int ShadowCrossings()
        {
            if (y0 < y1) return crossings + (y0 <= bottom ? 1 : 0) + (y1 >= top ? 1 : 0);
            if (y1 < y0) return crossings - (y1 <= bottom ? 1 : 0) - (y0 >= top ? 1 : 0);
            return crossings;
        }
    }
}
