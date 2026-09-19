using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Aion.Commons.Logging;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.GeoEngine.Models;
using Aion.GameServer.GeoEngine.Scene;
using Aion.GameServer.TestKit;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class GeoWorldLoaderTests
{
    [Fact]
    public async Task BinaryPlacementAliasesTownLevelsAndMapFilteringRetainSourceSemantics()
    {
        var previous = DataManager.GetRegisteredInstance();
        string root = Directory.CreateTempSubdirectory("aion-geo-loader-").FullName;
        try
        {
            DataManager.RegisterInstance(await RealStaticData.LoadAsync());
            using var meshes = new MemoryStream();
            MeshRecord(meshes, "world/a.cgf|world/b.cgf", 1);
            MeshRecord(meshes, "town_01.cgf", 2);
            MeshRecord(meshes, "town_03.cgf", 1);
            File.WriteAllBytes(Path.Combine(root, "models.mesh"), meshes.ToArray());
            using var placements = new MemoryStream();
            Placement(placements, "world/b.cgf", 0, 0, 0);
            Placement(placements, "town_01.cgf", 5, 123, 1);
            Placement(placements, "world/a.cgf", 1, -123, 0);
            File.WriteAllBytes(Path.Combine(root, "1.geo"), placements.ToArray());
            File.WriteAllBytes(Path.Combine(root, "1,2.png"), PngReaderTests.Encode(3, 3, Enumerable.Repeat((ushort)320, 9).ToArray(), 16, 0, [0]));
            File.WriteAllBytes(Path.Combine(root, "1.materials.png"), PngReaderTests.Encode(3, 3, Enumerable.Repeat((ushort)7, 9).ToArray(), 8, 3, [4]));
            File.WriteAllText(Path.Combine(root, "2.bad.png"), "must not decode excluded maps");
            var map = new GeoMap(1); var excluded = new GeoMap(2);
            GeoWorldLoader.Load([map, excluded], root, new HashSet<int> { 1 }, waitForCollisionData: true);
            Assert.True(map.HasTerrain()); Assert.True(map.HasTerrainMaterials());
            Assert.False(excluded.HasTerrain()); Assert.Empty(excluded.GetChildren());
            Assert.Equal(4, map.GetEntityCount());
            Assert.Equal(10f, map.GetZ(3, 3, 20, 0, 1));
            var town = map.DescendantMatches<DespawnableNode>().Where(n => n.type == DespawnableNode.DespawnableType.TOWN_OBJECT).ToArray();
            Assert.Equal(new sbyte[] { 3, 28 }, town.Select(n => n.levelBitMask));
            Assert.All(town, n => Assert.Equal(123, n.id));
            Assert.Equal(-123, Assert.Single(map.DescendantMatches<DespawnableNode>(), n => n.type == DespawnableNode.DespawnableType.EVENT).id);
            var alias = Assert.Single(map.GetGeometries(), g => g.GetName() == "world/b.cgf");
            Assert.Equal(200, alias.GetMaterialId());
            var a = new Vector3f(); var b = new Vector3f(); var c = new Vector3f();
            alias.GetMesh().GetTriangle(0, a, b, c);
            Assert.Equal(1f, b.X); Assert.Equal(1f, c.Y);
            Assert.InRange(alias.GetWorldBound()!.GetCenter().X, 8.49f, 8.51f);
            Assert.InRange(alias.GetWorldBound()!.GetCenter().Y, 20.99f, 21.01f);
            Assert.Equal(30f, alias.GetWorldBound()!.GetCenter().Z);
            Assert.Throws<ArgumentException>(() => GeoWorldLoader.Load([map], root, new HashSet<int> { 999 }, true));
            var loaded = GeoWorldLoader.LoadMeshes(Path.Combine(root, "models.mesh"));
            Assert.Same(((Geometry)loaded["world/a.cgf"].GetChild(0)).GetMesh(), ((Geometry)loaded["world/b.cgf"].GetChild(0)).GetMesh());
            foreach (var (type, level) in new (byte, byte)[] { (5, 9), (1, 1) })
            {
                using var invalid = new MemoryStream();
                Placement(invalid, "world/a.cgf", type, 1, level);
                File.WriteAllBytes(Path.Combine(root, "1.geo"), invalid.ToArray());
                var error = Assert.Throws<AggregateException>(() => GeoWorldLoader.Load([new GeoMap(1)], root, new HashSet<int> { 1 }, true));
                Assert.IsType<ArgumentException>(Assert.IsType<Aion.GameServer.GameServerError>(Assert.Single(error.InnerExceptions)).InnerException);
            }
        }
        finally { DataManager.RestoreInstance(previous); Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void InvalidMeshIndexWidthFailsWithItsUnderlyingCause()
    {
        string path = Path.GetTempFileName();
        try
        {
            using var mesh = new MemoryStream();
            MeshRecord(mesh, "invalid.cgf", 0);
            File.WriteAllBytes(path, mesh.ToArray());
            var error = Assert.Throws<Aion.GameServer.GameServerError>(() => GeoWorldLoader.LoadMeshes(path));
            Assert.Contains("Index size 0", Assert.IsType<IOException>(error.InnerException).Message);
        }
        finally { File.Delete(path); }
    }

    [SkippableFact]
    public async Task ShippedGeoLoadMeasurementIncludesCollisionPreload()
    {
        string? profile = Environment.GetEnvironmentVariable("AION_GEO_PROFILE");
        Skip.If(profile is not ("all" or "starter"), "Run in a dedicated process with AION_GEO_PROFILE=all or starter.");
        var previous = DataManager.GetRegisteredInstance();
        try
        {
            DataManager.RegisterInstance(await RealStaticData.LoadAsync());
            var maps = DataManager.WORLD_MAPS_DATA.Select(m => new GeoMap(m.GetMapId())).ToArray();
            using var logs = new CapturingLoggerProvider();
            using var factory = LoggerFactory.Create(builder => builder.AddProvider(logs));
            using var logOverride = AionLog.OverrideFactory(factory);
            HashSet<int>? filter = profile == "starter" ? [210010000, 220010000] : null;
            long before = GC.GetTotalMemory(forceFullCollection: true);
            var process = Process.GetCurrentProcess(); process.Refresh(); long workingBefore = process.WorkingSet64;
            var watch = Stopwatch.StartNew();
            GeoWorldLoader.Load(maps, Path.Combine(RealStaticData.RepoRoot(), "game-server/data/geo"), filter, true);
            watch.Stop();
            long after = GC.GetTotalMemory(forceFullCollection: true);
            process.Refresh();
            var selected = maps.Where(m => filter == null || filter.Contains(m.GetMapId())).ToArray();
            Assert.All(selected.Where(m => m.GetMapId() is 210010000 or 220010000), m =>
            {
                Assert.True(m.HasTerrain()); Assert.True(m.GetEntityCount() > 0);
                var start = DataManager.PLAYER_INITIAL_DATA.GetSpawnLocation(m.GetMapId() == 210010000 ? Aion.GameServer.Model.Race.ELYOS : Aion.GameServer.Model.Race.ASMODIANS);
                Assert.True(float.IsFinite(m.GetZ(start.GetX(), start.GetY(), start.GetZ() + 5, start.GetZ() - 5, 1)));
            });
            string receipt = JsonSerializer.Serialize(new { profile, elapsedMilliseconds = watch.ElapsedMilliseconds,
                managedBefore = before, managedAfter = after, managedDelta = after - before,
                workingBefore, workingAfter = process.WorkingSet64, peakWorkingSet = process.PeakWorkingSet64,
                maps = selected.Length, terrainMaps = selected.Count(m => m.HasTerrain()), materialMaps = selected.Count(m => m.HasTerrainMaterials()),
                entities = selected.Sum(m => (long)m.GetEntityCount()), geometries = selected.Sum(m => (long)m.GetGeometries().Count()),
                meshes = selected.SelectMany(m => m.GetGeometries()).Select(g => g.GetMesh()).Distinct().Count(),
                logs = logs.Entries.Select(entry => new { entry.Category, Level = entry.Level.ToString(), entry.Message, entry.Fingerprint }) });
            Console.WriteLine("GEO-MEASUREMENT " + receipt);
            if (Environment.GetEnvironmentVariable("AION_GEO_RECEIPT") is { Length: > 0 } path) File.WriteAllText(path, receipt + "\n");
            Assert.DoesNotContain(logs.Entries, entry => entry.Level >= LogLevel.Error);
            // Neither starter zone ships a material PNG. This exact inherited warning is expected;
            // all-map loading must be entirely warning-free (no missing files or mesh aliases).
            Assert.All(logs.Entries.Where(entry => entry.Level == LogLevel.Warning), entry =>
            {
                Assert.Equal("starter", profile);
                Assert.Equal("GeoWorldLoader", entry.Category);
                Assert.Equal("No terrain materials were loaded", entry.Message);
            });
        }
        finally { DataManager.RestoreInstance(previous); }
    }

    private static void MeshRecord(Stream stream, string name, int indexSize)
    {
        Name(stream, name); stream.WriteByte(1); Short(stream, 3);
        foreach (float value in new float[] { 0, 0, 0, 1, 0, 0, 0, 1, 0 }) Float(stream, value);
        Short(stream, 1); stream.WriteByte((byte)indexSize);
        for (short i = 0; i < 3; i++) if (indexSize == 1) stream.WriteByte((byte)i); else Short(stream, i);
        stream.WriteByte(200); stream.WriteByte(129);
    }

    private static void Placement(Stream stream, string name, byte type, short id, byte level)
    {
        Name(stream, name);
        foreach (float value in new float[] { 10, 20, 30, 0, -1, 0, 1, 0, 0, 0, 0, 1, 2, 3, 4 }) Float(stream, value);
        stream.WriteByte(type); Short(stream, id); stream.WriteByte(level);
    }

    private static void Name(Stream stream, string name) { byte[] bytes = Encoding.UTF8.GetBytes(name); Short(stream, (short)bytes.Length); stream.Write(bytes); }
    private static void Short(Stream stream, short value) { Span<byte> bytes = stackalloc byte[2]; BinaryPrimitives.WriteInt16BigEndian(bytes, value); stream.Write(bytes); }
    private static void Float(Stream stream, float value) { Span<byte> bytes = stackalloc byte[4]; BinaryPrimitives.WriteSingleBigEndian(bytes, value); stream.Write(bytes); }
}
