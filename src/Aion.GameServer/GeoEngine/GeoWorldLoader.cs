using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Aion.Commons.Nio;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.GeoEngine.Models;
using Aion.GameServer.GeoEngine.Scene;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.GeoEngine;

/// <summary>Java geoEngine/GeoWorldLoader at ce54b7931. Binary files are big-endian;
/// PNG samples retain their unsigned 16-bit height / byte material representation.</summary>
public static class GeoWorldLoader
{
    private static readonly ILogger log = AionLog.For(nameof(GeoWorldLoader));

    public static void Load(ICollection<GeoMap> maps, IReadOnlySet<int>? mapIds = null) =>
        Load(maps, Path.Combine("data", "geo"), mapIds, waitForCollisionData: false);

    // Directory and synchronous preload are infrastructure seams for deterministic loader tests/measurements.
    internal static void Load(ICollection<GeoMap> maps, string directory, IReadOnlySet<int>? mapIds, bool waitForCollisionData)
    {
        if (mapIds != null && (mapIds.Count == 0 || mapIds.Except(maps.Select(m => m.GetMapId())).Any()))
            throw new ArgumentException("Geo map filter must name existing maps.", nameof(mapIds));
        var selected = maps.Where(m => mapIds == null || mapIds.Contains(m.GetMapId())).ToArray();
        LoadTerrains(selected, directory, mapIds != null);
        var models = LoadMeshes(Path.Combine(directory, "models.mesh"));
        var missing = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        Parallel.ForEach(selected, map => LoadWorld(map, models, missing, directory));
        if (!missing.IsEmpty)
            log.LogWarning("{Count} meshes are missing:\n{Meshes}", missing.Count, string.Join('\n', missing.Keys.Order(StringComparer.Ordinal)));
        int loaded = selected.Count(m => m.GetChildren().Count != 0);
        if (loaded == 0) log.LogWarning("No geo maps loaded.");
        else log.LogInformation("Loaded {Entities} entities on {Maps} maps", selected.Sum(m => (long)m.GetEntityCount()), loaded);
        void Preload() => Parallel.ForEach(selected.SelectMany(m => m.GetGeometries()).Select(g => g.GetMesh()).Distinct(), m => m.CreateCollisionData());
        if (waitForCollisionData) Preload();
        else ThreadPoolManager.GetInstance().ExecuteLongRunning(Preload);
    }

    private static void LoadTerrains(GeoMap[] maps, string directory, bool filtered)
    {
        var terrains = new ConcurrentDictionary<GeoMap, Terrain>();
        Parallel.ForEach(Directory.EnumerateFiles(directory).Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)), path =>
        {
            PngReader.Image? image = null;
            var names = Path.GetFileName(path).Split(',').ToHashSet(StringComparer.Ordinal);
            foreach (var map in maps)
            {
                if (names.Count == 0) break;
                string id = map.GetMapId().ToString(CultureInfo.InvariantCulture);
                if (names.RemoveWhere(n => n.StartsWith(id, StringComparison.Ordinal)) == 0) continue;
                image ??= PngReader.Read(File.ReadAllBytes(path));
                var terrain = terrains.GetOrAdd(map, _ => new Terrain());
                lock (terrain)
                {
                    if (image.Heights != null) terrain.SetHeightmap(image.Heights, image.Width, image.Height);
                    else terrain.SetMaterials(image.Materials!, image.Width, image.Height);
                }
            }
            // A filtered host deliberately does not load or warn about other maps' PNGs.
            if (!filtered)
                foreach (string name in names) log.LogWarning("{Map} of {Path} could not be associated with a map", name, path);
        });
        foreach (var (map, terrain) in terrains)
            if (terrain.HasHeightmap()) map.SetTerrain(terrain);
            else log.LogWarning("Missing terrain heightmap for {Map}", map.GetMapId());
        int count = maps.Count(m => m.HasTerrain());
        if (count == 0) log.LogWarning("No terrains were loaded");
        else log.LogInformation("Loaded terrains for {Maps} maps", count);
        if (!maps.Any(m => m.HasTerrainMaterials())) log.LogWarning("No terrain materials were loaded");
    }

    internal static Dictionary<string, Node> LoadMeshes(string path)
    {
        var models = new Dictionary<string, Node>(StringComparer.Ordinal);
        try
        {
            var data = ByteBuffer.Wrap(File.ReadAllBytes(path));
            while (data.HasRemaining())
            {
                string name = ReadName(data);
                var node = new Node(null);
                sbyte intentions = 0;
                int singleMaterial = 0, count = data.Get();
                for (int c = 0; c < count; c++)
                {
                    var mesh = new Mesh();
                    int vertexBytes = (data.GetShort() & 0xffff) * 3 * 4;
                    mesh.SetVertices(data.Slice(data.Position(), vertexBytes).AsFloatBuffer());
                    data.SetPosition(data.Position() + vertexBytes);
                    int faces = data.GetShort() & 0xffff, indexSize = data.Get();
                    int faceBytes = faces * 3 * indexSize;
                    var indices = data.Slice(data.Position(), faceBytes);
                    switch (indexSize)
                    {
                        case 1: mesh.SetIndices(indices); break;
                        case 2: mesh.SetIndices(indices.AsShortBuffer()); break;
                        default: throw new IOException($"Index size {indexSize} is not supported");
                    }
                    data.SetPosition(data.Position() + faceBytes);
                    mesh.SetMaterialId(unchecked((sbyte)data.Get()));
                    mesh.SetCollisionIntentions(unchecked((sbyte)data.Get()));
                    intentions |= mesh.GetCollisionIntentions();
                    if (node.GetName() == null && (mesh.GetMaterialId() == 11 || DataManager.MATERIAL_DATA.GetTemplate(mesh.GetMaterialId()) != null))
                        node.SetName(name);
                    if (count == 1) singleMaterial = mesh.GetMaterialId();
                    node.AttachChild(new Geometry(name, mesh));
                }
                node.SetCollisionIntentions(intentions);
                node.SetMaterialId(unchecked((sbyte)singleMaterial));
                if (!name.Contains('|')) models[name] = node;
                else foreach (string alias in name.Split('|'))
                {
                    var clone = (Node)node.Clone();
                    if (clone.GetName() != null) clone.SetName(alias);
                    clone.GetChild(name)!.SetName(alias);
                    models[alias] = clone;
                }
            }
        }
        catch (Exception error) { throw new GameServerError("Could not load meshes", error); }
        log.LogInformation("Loaded {Meshes} meshes", models.Count);
        return models;
    }

    private static string ReadName(ByteBuffer data)
    {
        int length = data.GetShort();
        if (length < 0) throw new InvalidDataException("Negative geo name length.");
        var bytes = new byte[length];
        data.Get(bytes);
        return Encoding.UTF8.GetString(bytes);
    }

    private static void LoadWorld(GeoMap map, Dictionary<string, Node> models, ConcurrentDictionary<string, byte> missing, string directory)
    {
        string path = Path.Combine(directory, map.GetMapId().ToString(CultureInfo.InvariantCulture) + ".geo");
        if (!File.Exists(path))
        {
            var template = DataManager.WORLD_MAPS_DATA.GetTemplate(map.GetMapId());
            if (template.GetWorldSize() != 0 && !template.IsPrison()
                && !template.GetName().Equals("IDTest_Dungeon", StringComparison.OrdinalIgnoreCase)
                && !template.GetName().Equals("System_Basic", StringComparison.OrdinalIgnoreCase))
                log.LogWarning("{Path} is missing", path);
            return;
        }
        try
        {
            var data = ByteBuffer.Wrap(File.ReadAllBytes(path));
            while (data.HasRemaining())
            {
                string name = ReadName(data);
                var location = new Vector3f(data.GetFloat(), data.GetFloat(), data.GetFloat());
                var rotation = new Matrix3f();
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++) rotation.Set(i, j, data.GetFloat());
                var scale = new Vector3f(data.GetFloat(), data.GetFloat(), data.GetFloat());
                sbyte type = unchecked((sbyte)data.Get());
                short id = data.GetShort();
                sbyte level = unchecked((sbyte)data.Get());
                if (!models.TryGetValue(name, out var node)) { missing.TryAdd(name, 0); continue; }
                if (type > 0)
                {
                    var despawnable = new DespawnableNode();
                    despawnable.CopyFrom(node);
                    despawnable.type = DespawnableTypes.GetById(type);
                    despawnable.id = id;
                    if (despawnable.type == DespawnableNode.DespawnableType.TOWN_OBJECT)
                    {
                        if (level > 8) throw new ArgumentException($"{level} doesn't fit in bit mask");
                        despawnable.levelBitMask = level < 1 ? (sbyte)0 : unchecked((sbyte)(1 << (level - 1)));
                    }
                    else if (level != 0) throw new ArgumentException("Unexpected value in town level field for non-town entity");
                    node = despawnable;
                }
                var clone = AttachToMapAndCreateZones(map, node, rotation, location, scale);
                if (clone is DespawnableNode town && town.type == DespawnableNode.DespawnableType.TOWN_OBJECT)
                    for (int townLevel = level + 1; townLevel <= 5; townLevel++)
                    {
                        string townName = name.Replace("_01.cgf", "_0" + townLevel + ".cgf", StringComparison.Ordinal);
                        if (!models.TryGetValue(townName, out var model))
                            town.levelBitMask |= unchecked((sbyte)(1 << (townLevel - 1)));
                        else
                        {
                            var next = new DespawnableNode();
                            next.CopyFrom(model);
                            next.type = town.type; next.id = town.id;
                            next.levelBitMask = unchecked((sbyte)(1 << (townLevel - 1)));
                            town = (DespawnableNode)AttachToMapAndCreateZones(map, next, rotation, location, scale);
                        }
                    }
            }
        }
        catch (Exception error) { throw new GameServerError("Could not load " + path, error); }
        map.UpdateModelBound();
    }

    private static Node AttachToMapAndCreateZones(GeoMap map, Node node, Matrix3f rotation, Vector3f location, Vector3f scale)
    {
        var clone = (Node)node.Clone();
        clone.SetTransform(rotation, location, scale);
        clone.UpdateModelBound();
        map.AttachChild(clone);
        var children = clone.GetChildren();
        for (int c = 0; c < children.Count; c++) CreateZone(children[c], map.GetMapId(), children.Count == 1 ? 0 : c + 1);
        return clone;
    }

    private static void CreateZone(Spatial geometry, int worldId, int childNumber)
    {
        if ((geometry.GetCollisionIntentions() & CollisionIntention.MATERIAL.GetId()) == 0) return;
        int region = GetVectorHash(geometry.GetWorldBound()!.GetCenter());
        string path = geometry.GetName()!;
        string name = path[(path.LastIndexOf('/') + 1)..path.LastIndexOf('.')].ToUpperInvariant();
        if (childNumber > 0) name += "_CHILD" + childNumber;
        geometry.SetName(name + "_" + region.ToString(CultureInfo.InvariantCulture));
        var zoneName = ZoneName.CreateOrGet(geometry.GetName() + "_" + worldId.ToString(CultureInfo.InvariantCulture));
        ZoneService.GetInstance().CreateMaterialZoneTemplate(geometry, worldId, zoneName);
    }

    internal static int GetVectorHash(Vector3f location)
    {
        // Java Float.floatToIntBits canonicalizes NaN and sign-extends its int into the long.
        static long Bits(float value) => float.IsNaN(value) ? 0x7fc00000 : BitConverter.SingleToInt32Bits(value);
        return (int)((Bits(location.X) * 73856093 ^ Bits(location.Y) * 19349669 ^ Bits(location.Z) * 83492791) % 700001);
    }
}
