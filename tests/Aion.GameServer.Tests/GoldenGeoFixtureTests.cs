using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Aion.Commons.Logging;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.GeoEngine.Models;
using Aion.GameServer.TestKit;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class GoldenGeoFixtureTests
{
    [Fact]
    public async Task EveryStarterSpawnAndWalkerHeightAndStratifiedRaysMatchJava()
    {
        string root = RealStaticData.RepoRoot();
        string data = Path.Combine(root, "game-server");
        string fixture = Path.Combine(root, "parity-artifacts/golden/geo");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture, "starter-inputs.json")));
        Assert.Equal("ce54b7931546cddafb970d20c9f71fec6d48c83b", manifest.RootElement.GetProperty("javaSpec").GetString());
        foreach (var input in manifest.RootElement.GetProperty("sha256").EnumerateObject())
        {
            string path = Path.Combine(data, input.Name);
            byte[] bytes = path.EndsWith(".xml", StringComparison.Ordinal) ? Encoding.UTF8.GetBytes(File.ReadAllText(path).Replace("\r\n", "\n")) : File.ReadAllBytes(path);
            Assert.Equal(input.Value.GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
        }
        var points = CollectPoints(data);
        Assert.Equal(manifest.RootElement.GetProperty("points").GetInt32(), points.Count);
        var visited = new Dictionary<string, int>();
        var counts = new Dictionary<string, int>();
        var errors = new List<string>();
        int visible = 0, occluded = 0, finiteHeights = 0, missingHeights = 0;
        var previous = DataManager.GetRegisteredInstance();
        try
        {
            DataManager.RegisterInstance(await RealStaticData.LoadAsync());
            var maps = new[] { new GeoMap(210010000), new GeoMap(220010000) };
            using var logs = new CapturingLoggerProvider();
            using var factory = LoggerFactory.Create(builder => builder.AddProvider(logs));
            using var logOverride = AionLog.OverrideFactory(factory);
            GeoWorldLoader.Load(maps, Path.Combine(data, "data/geo"), new HashSet<int> { 210010000, 220010000 }, true);
            Assert.Equal(9513, maps.Sum(m => m.GetEntityCount()));
            foreach (string line in File.ReadLines(Path.Combine(fixture, "starter-queries.jsonl")))
            {
                using var document = JsonDocument.Parse(line);
                var row = document.RootElement;
                string kind = row.GetProperty("kind").GetString()!;
                int mapId = row.GetProperty("map").GetInt32();
                string key = mapId + ":" + row.GetProperty("source").GetString();
                var origin = Vector(row.GetProperty("origin"));
                Assert.True(points.TryGetValue(key, out var point), "Unknown input point " + key);
                Assert.Equal(point, origin);
                var map = maps[mapId == 210010000 ? 0 : 1];
                var expected = row.GetProperty("expected");
                counts[kind] = counts.GetValueOrDefault(kind) + 1;
                bool match;
                string actual;
                if (kind == "z")
                {
                    visited[key] = visited.GetValueOrDefault(key) + 1;
                    float range = row.GetProperty("range").GetSingle();
                    float z = map.GetZ(origin.X, origin.Y, origin.Z + range, origin.Z - range, 1, row.GetProperty("slope").GetBoolean());
                    if (float.IsNaN(Number(expected))) missingHeights++; else finiteHeights++;
                    match = Same(Number(expected), z); actual = z.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    var target = Vector(row.GetProperty("target"));
                    if (kind == "see")
                    {
                        bool see = map.CanSee(origin.X, origin.Y, origin.Z + 1, target.X, target.Y, target.Z + 1, 1, IgnoreProperties.ANY_RACE);
                        if (expected.GetBoolean()) visible++; else occluded++;
                        match = see == expected.GetBoolean(); actual = see.ToString();
                    }
                    else
                    {
                        var result = kind switch
                        {
                            "closest" => map.GetClosestCollision(origin.X, origin.Y, origin.Z, target.X, target.Y, target.Z, row.GetProperty("nearGround").GetBoolean(), 1, CollisionIntention.DEFAULT_COLLISIONS.GetId(), IgnoreProperties.ANY_RACE),
                            "movement" => map.FindMovementCollision(origin, target.X, target.Y, 1),
                            _ => throw new InvalidDataException("Unknown geo query " + kind)
                        };
                        var want = Vector(expected);
                        match = Same(want.X, result.X) && Same(want.Y, result.Y) && Same(want.Z, result.Z);
                        actual = $"[{result.X:R},{result.Y:R},{result.Z:R}]";
                    }
                }
                if (!match) errors.Add($"{line} ACTUAL {actual}");
            }
            Assert.Equal(points.Count, visited.Count);
            Assert.All(visited.Values, count => Assert.Equal(3, count));
            Assert.Equal(4, counts.Count);
            Assert.True(visible > 100 && occluded > 100 && finiteHeights > 1000 && missingHeights > 0,
                "Geo corpus must exercise both visibility outcomes, terrain hits and missing ground.");
            foreach (var count in manifest.RootElement.GetProperty("counts").EnumerateObject()) Assert.Equal(count.Value.GetInt32(), counts[count.Name]);
            Assert.DoesNotContain(logs.Entries, entry => entry.Level >= LogLevel.Error);
            Assert.All(logs.Entries.Where(entry => entry.Level == LogLevel.Warning), entry => Assert.Equal("No terrain materials were loaded", entry.Message));
            Assert.True(errors.Count == 0, $"{errors.Count} Java/C# geo mismatches (first 20):\n" + string.Join('\n', errors.Take(20)));
        }
        finally { DataManager.RestoreInstance(previous); }
    }

    // Absolute 1mm tolerance for single-precision arithmetic; NaN/hit topology and LOS must match exactly.
    private static bool Same(float expected, float actual) => float.IsNaN(expected) ? float.IsNaN(actual) : expected.Equals(actual) || Math.Abs(expected - actual) <= 0.001f;
    private static float Number(JsonElement value) => value.ValueKind == JsonValueKind.String ? float.Parse(value.GetString()!, CultureInfo.InvariantCulture) : value.GetSingle();
    private static Vector3f Vector(JsonElement value) => new(Number(value[0]), Number(value[1]), Number(value[2]));

    private static Dictionary<string, Vector3f> CollectPoints(string root)
    {
        var result = new Dictionary<string, Vector3f>();
        var routesByMap = new Dictionary<int, HashSet<string>> { [210010000] = [], [220010000] = [] };
        string Relative(string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
        void Add(int map, string id, XElement point) => result.Add(map + ":" + id, new Vector3f(
            float.Parse(point.Attribute("x")!.Value, CultureInfo.InvariantCulture), float.Parse(point.Attribute("y")!.Value, CultureInfo.InvariantCulture), float.Parse(point.Attribute("z")!.Value, CultureInfo.InvariantCulture)));
        foreach (string path in Directory.EnumerateFiles(Path.Combine(root, "data/static_data/spawns"), "*.xml", SearchOption.AllDirectories))
        {
            var maps = XDocument.Load(path).Descendants("spawn_map").ToArray();
            for (int m = 0; m < maps.Length; m++)
            {
                int id = (int)maps[m].Attribute("map_id")!;
                if (!routesByMap.TryGetValue(id, out var routes)) continue;
                var spots = maps[m].Descendants("spot").ToArray();
                for (int s = 0; s < spots.Length; s++)
                {
                    Add(id, Relative(path) + ":map" + m + ":spot" + s, spots[s]);
                    if (spots[s].Attribute("walker_id") is { } route) routes.Add(route.Value);
                }
            }
        }
        var versions = XDocument.Load(Path.Combine(root, "data/static_data/walker_versions.xml")).Descendants("walk_parent").ToDictionary(p => p.Attribute("id")!.Value);
        foreach (var ids in routesByMap.Values)
        {
            bool changed;
            do { changed = false; foreach (string id in ids.ToArray()) if (versions.TryGetValue(id, out var parent)) foreach (var version in parent.Elements("version")) changed |= ids.Add(version.Attribute("id")!.Value); } while (changed);
        }
        var templates = Directory.EnumerateFiles(Path.Combine(root, "data/static_data/npc_walker"), "*.xml", SearchOption.AllDirectories)
            .SelectMany(path => XDocument.Load(path).Descendants("walker_template").Select(element => (path, element)))
            .ToDictionary(t => t.element.Attribute("route_id")!.Value);
        foreach (var (map, ids) in routesByMap) foreach (string id in ids)
        {
            if (!templates.TryGetValue(id, out var route)) { Assert.True(versions.ContainsKey(id), "Missing walker " + id); continue; }
            var steps = route.element.Elements("routestep").ToArray(); Assert.NotEmpty(steps);
            for (int s = 0; s < steps.Length; s++) Add(map, Relative(route.path) + ":route" + id + ":step" + s, steps[s]);
        }
        return result;
    }
}
