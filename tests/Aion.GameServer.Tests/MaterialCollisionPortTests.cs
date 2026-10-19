using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.GeoEngine.Models;
using Aion.GameServer.GeoEngine.Scene;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class MaterialCollisionPortTests
{
    [Theory]
    [InlineData(8f, 7)]
    [InlineData(8.1f, 7)]
    [InlineData(10f, 7)]
    [InlineData(11.9f, 7)]
    [InlineData(12f, 7)]
    [InlineData(7.9f, 0)]
    [InlineData(12.1f, 0)]
    public void TerrainMaterialsReachTwoMetersAboveAndBelowTheFeet(float feetZ, int expected)
    {
        var map = Map();
        Assert.Equal(expected, map.GetTerrainMaterialAt(3, 3, feetZ, 1));
    }

    [Theory]
    [InlineData(10f, 10.5f, 7)] // Hanging above the feet does not mask terrain.
    [InlineData(10f, 10.05f, 0)] // Surface at the feet masks terrain, including the 0.1m tolerance.
    [InlineData(11.5f, 10.75f, 0)]
    [InlineData(10f, 9f, 7)]
    public void OnlyAMeshBetweenTheTerrainAndFeetMasksTheMaterial(float feetZ, float meshZ, int expected)
    {
        var map = Map(meshZ);
        Assert.Equal(expected, map.GetTerrainMaterialAt(3, 3, feetZ, 1));
    }

    [Fact]
    public void TouchObserversCastTwoMetersEitherSideOfTheFeet()
    {
        using var harness = BossAiHarness.For().Build();
        var player = harness.SpawnPlayer(z: 242);
        var geometry = new Plane(240.5f);
        var observer = new TouchProbe(player, geometry);
        observer.Moved();
        harness.Clock.Advance(TimeSpan.Zero);
        Assert.Equal(1, observer.Hits);
        Assert.Equal(240f, geometry.LastBottom);
        Assert.True(geometry.LastTop >= 244f);
    }

    private static SurfaceMap Map(params float[] surfaces)
    {
        var terrain = new Terrain();
        terrain.SetHeightmap(Enumerable.Repeat((short)320, 9).ToArray(), 3, 3); // Flat height 10m.
        terrain.SetMaterials(Enumerable.Repeat((byte)7, 9).ToArray(), 3, 3);
        var map = new SurfaceMap(surfaces); map.SetTerrain(terrain); return map;
    }

    private sealed class SurfaceMap(float[] surfaces) : GeoMap(1)
    {
        private readonly Geometry mesh = new("platform", new Mesh());
        public override int CollideWith(Collidable other, CollisionResults results)
        {
            var ray = (Ray)other;
            int added = 0;
            foreach (float z in surfaces)
            {
                float distance = (z - ray.origin.Z) / ray.direction.Z;
                if (distance < 0 || distance > ray.GetLimit()) continue;
                var collision = new CollisionResult(new Vector3f(ray.origin.X, ray.origin.Y, z), distance);
                collision.SetGeometry(mesh); results.AddCollision(collision); added++;
            }
            return added;
        }
    }

    private sealed class Plane(float z) : Geometry("material", new Mesh())
    {
        public float LastTop, LastBottom;
        public override int CollideWith(Collidable other, CollisionResults results)
        {
            var ray = (Ray)other;
            LastTop = ray.origin.Z; LastBottom = ray.origin.Z + ray.direction.Z * ray.GetLimit();
            float distance = (z - ray.origin.Z) / ray.direction.Z;
            if (distance < 0 || distance > ray.GetLimit()) return 0;
            var collision = new CollisionResult(new Vector3f(ray.origin.X, ray.origin.Y, z), distance);
            collision.SetGeometry(this); results.AddCollision(collision); return 1;
        }
    }

    private sealed class TouchProbe(Creature creature, Geometry geometry)
        : AbstractCollisionObserver(creature, geometry, 0, CheckType.TOUCH)
    {
        public int Hits;
        public override void OnMoved(CollisionResults results) => Hits = results.Size();
    }
}
