using System.Text.Json;
using System.Text.Json.Serialization;
using Aion.Bots.World;

namespace Aion.Bots.Navigation.NavMesh;

/// <summary>One mapped road centerline in Aion X/Y. Roads are a cost hint baked into the navmesh as
/// <see cref="BotNavAreas.Road"/>; they never make ground walkable that the geometry rejects.</summary>
public sealed record BotNavRoad(string Id, float[][] Points);

public sealed record BotNavRoadFile
{
	public int MapId { get; init; }
	public string Source { get; init; } = "";
	public List<BotNavRoad> Roads { get; init; } = [];
}

/// <summary>Reads <c>game-server/data/nav/roads/&lt;mapId&gt;.roads.json</c> (generated, never hand-edited).</summary>
public static class BotNavRoads
{
	public static readonly JsonSerializerOptions Json = new()
	{
		WriteIndented = false,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.Never,
	};

	public static string PathFor(string repoRoot, int mapId) =>
		Path.Combine(BotNavMeshSet.DefaultDirectory(repoRoot), "roads", mapId + ".roads.json");

	public static BotNavRoadFile? Load(string repoRoot, int mapId)
	{
		string path = PathFor(repoRoot, mapId);
		return File.Exists(path) ? JsonSerializer.Deserialize<BotNavRoadFile>(File.ReadAllText(path), Json) : null;
	}

	/// <summary>Each road segment becomes a convex quad of the given half-width.</summary>
	public static IReadOnlyList<BotNavVolume> LoadVolumes(string repoRoot, int mapId, float halfWidth)
	{
		BotNavRoadFile? file = Load(repoRoot, mapId);
		if (file == null) return [];
		var volumes = new List<BotNavVolume>();
		foreach (BotNavRoad road in file.Roads)
			for (int i = 1; i < road.Points.Length; i++)
			{
				float ax = road.Points[i - 1][0], ay = road.Points[i - 1][1], bx = road.Points[i][0], by = road.Points[i][1];
				float dx = bx - ax, dy = by - ay, length = MathF.Sqrt(dx * dx + dy * dy);
				if (length < 0.01f) continue;
				float nx = -dy / length * halfWidth, ny = dx / length * halfWidth;
				float ex = dx / length * halfWidth * 0.5f, ey = dy / length * halfWidth * 0.5f;
				volumes.Add(new BotNavVolume([ax + nx - ex, ay + ny - ey, ax - nx - ex, ay - ny - ey,
					bx - nx + ex, by - ny + ey, bx + nx + ex, by + ny + ey], -10000, 10000, BotNavAreas.Road, road.Id));
			}
		return volumes;
	}

	/// <summary>Horizontal distance from a point to the nearest mapped road, or infinity without roads.</summary>
	public static float Distance(BotNavRoadFile? file, BotPosition point)
	{
		if (file == null) return float.PositiveInfinity;
		float best = float.PositiveInfinity;
		foreach (BotNavRoad road in file.Roads)
			for (int i = 1; i < road.Points.Length; i++)
			{
				float ax = road.Points[i - 1][0], ay = road.Points[i - 1][1], bx = road.Points[i][0], by = road.Points[i][1];
				float dx = bx - ax, dy = by - ay, lengthSquared = dx * dx + dy * dy;
				float t = lengthSquared <= 0 ? 0 : Math.Clamp(((point.X - ax) * dx + (point.Y - ay) * dy) / lengthSquared, 0, 1);
				float px = ax + t * dx - point.X, py = ay + t * dy - point.Y;
				best = MathF.Min(best, MathF.Sqrt(px * px + py * py));
			}
		return best;
	}
}
