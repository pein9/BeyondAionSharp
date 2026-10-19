using System.Globalization;
using System.Text.Json;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class ZonePolygonParityTests
{
    [Fact]
    public void NonZeroWindingAndRegionIntersectionsMatchTheJavaPathOracle()
    {
        string path = Path.Combine(RealStaticData.RepoRoot(), "parity-artifacts/golden/geometry/zone-polygons-2b097879f.json");
        using var fixture = JsonDocument.Parse(File.ReadAllText(path));
        int previous = WorldConfig.WorldRegionSize;
        try
        {
            WorldConfig.WorldRegionSize = fixture.RootElement.GetProperty("regionSize").GetInt32();
            int queries = 0;
            foreach (var shape in fixture.RootElement.GetProperty("shapes").EnumerateArray())
            {
                var points = shape.GetProperty("points").EnumerateArray().Select(p => new Point2D(Number(p[0]), Number(p[1]))).ToList();
                var area = new PolyArea(points, 0, 100);
                foreach (var query in shape.GetProperty("queries").EnumerateArray())
                {
                    float x = Number(query.GetProperty("x")), y = Number(query.GetProperty("y"));
                    string description = $"{shape.GetProperty("name").GetString()} ({x},{y})";
                    Assert.True(area.IsInside2D(x, y) == query.GetProperty("inside").GetBoolean(), "contains: " + description);
                    var region = new RectangleArea(x, y, x + 128, y + 128, 0, 100);
                    Assert.True(area.IntersectsRectangle(region) == query.GetProperty("intersects").GetBoolean(), "intersects: " + description);
                    queries++;
                }
            }
            Assert.Equal(2532, queries);
        }
        finally { WorldConfig.WorldRegionSize = previous; }
    }

    [Fact]
    public void GeometryKeepsTheOriginalPointListButThePathIsACoordinateSnapshot()
    {
        var points = new List<Point2D> { new(0, 0), new(10, 0), new(10, 10), new(0, 10) };
        var area = new PolyArea(points, 0, 100);
        points[1].X = 20; points[2].X = 20;
        Assert.False(area.IsInside2D(15, 5));
        Assert.Equal(20, area.GetClosestPoint(25, 5).GetX());
        Assert.False(area.IntersectsRectangle(new RectangleArea(0, 0, 128, 128, 101, 200)));
        Assert.Equal("PolyArea needs at least 3 points but got 0", Assert.Throws<ArgumentException>(() => new PolyArea(new(), 2, 1)).Message);
        Assert.Null(new Points().GetPoints());
    }

    private static float Number(JsonElement value) => value.ValueKind == JsonValueKind.String
        ? float.Parse(value.GetString()!, CultureInfo.InvariantCulture) : value.GetSingle();
}
