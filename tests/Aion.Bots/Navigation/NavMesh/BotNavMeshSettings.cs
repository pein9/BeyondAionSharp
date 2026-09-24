using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Aion.Bots.Navigation.NavMesh;

/// <summary>Area ids and polygon flags written into every baked bot navmesh.
/// Areas choose traversal cost; flags choose whether a query may use a polygon at all.</summary>
public static class BotNavAreas
{
	public const int Ground = 1;
	public const int Road = 2;
	public const int Water = 3;
	public const int Door = 4;

	public const int FlagWalk = 1;
	public const int FlagSwim = 2;
	public const int FlagDoor = 4;

	public static int FlagsFor(int area) => area switch
	{
		Ground or Road => FlagWalk,
		Water => FlagSwim,
		Door => FlagWalk | FlagDoor,
		_ => 0,
	};

	public static string Name(int area) => area switch
	{
		Ground => "ground",
		Road => "road",
		Water => "water",
		Door => "door",
		_ => "area" + area.ToString(CultureInfo.InvariantCulture),
	};
}

/// <summary>Recast bake parameters. Defaults describe an ordinary player on foot and mirror the
/// ground rules <see cref="BotNavigationGeometry.TraceEdge"/> accepts: 45 degree slopes
/// (Java <c>CollisionResults</c> sloping-surface rule), a 1 m collision ray and 2 m samples.</summary>
public sealed record BotNavMeshSettings
{
	/// <summary>Bump when the extraction or bake logic changes so checked-in meshes are marked stale.</summary>
	public const int BakerVersion = 4;

	public float CellSize { get; init; } = 0.3f;
	public float CellHeight { get; init; } = 0.2f;
	public float AgentHeight { get; init; } = 1.6f;
	public float AgentRadius { get; init; } = 0.5f;
	public float AgentMaxClimb { get; init; } = 0.8f;
	public float AgentMaxSlope { get; init; } = 45f;
	public int TileSizeCells { get; init; } = 128;
	public int RegionMinSize { get; init; } = 8;
	public int RegionMergeSize { get; init; } = 20;
	public float EdgeMaxLength { get; init; } = 12f;
	public float EdgeMaxError { get; init; } = 1.3f;
	public int VertsPerPoly { get; init; } = 6;
	public float DetailSampleDistance { get; init; } = 6f;
	public float DetailSampleMaxError { get; init; } = 1f;
	/// <summary>Voxel height within which a lower walkable surface's flag survives under a steeper top
	/// surface (Recast's flag-merge threshold). The server's ground query takes only the top surface.</summary>
	public int FlagMergeVoxels { get; init; } = 1;
	/// <summary>Walkable ground this far below the map's water level is swimming, not walking.</summary>
	public float SwimDepth { get; init; } = 1.5f;
	/// <summary>Half-width of a mapped road corridor marked as <see cref="BotNavAreas.Road"/>.</summary>
	public float RoadHalfWidth { get; init; } = 3f;

	public string Fingerprint()
	{
		string text = FormattableString.Invariant(
			$"{BakerVersion}|{CellSize}|{CellHeight}|{AgentHeight}|{AgentRadius}|{AgentMaxClimb}|{AgentMaxSlope}|{TileSizeCells}|{RegionMinSize}|{RegionMergeSize}|{EdgeMaxLength}|{EdgeMaxError}|{VertsPerPoly}|{DetailSampleDistance}|{DetailSampleMaxError}|{SwimDepth}|{RoadHalfWidth}|{FlagMergeVoxels}");
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant()[..16];
	}
}
