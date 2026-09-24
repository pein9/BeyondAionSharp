using System.Text.Json;

namespace Aion.Bots.Navigation.NavMesh;

/// <summary>The client's playable area for a map: the 16 m sectors of <c>&lt;level&gt;-path.dat</c> that hold
/// ground (see <c>tools/nav/extract_walk_masks.py</c>). The baker only builds tiles near these sectors.</summary>
public sealed class BotNavMask
{
	private readonly HashSet<(int X, int Y)> sectors;

	private BotNavMask(float sectorMetres, HashSet<(int, int)> sectors, string source)
	{
		SectorMetres = sectorMetres;
		this.sectors = sectors;
		Source = source;
	}

	public float SectorMetres { get; }
	public string Source { get; }
	public int Count => sectors.Count;

	public static string PathFor(string repoRoot, int mapId) =>
		Path.Combine(BotNavMeshSet.DefaultDirectory(repoRoot), "masks", mapId + ".mask.json");

	public static BotNavMask? Load(string repoRoot, int mapId)
	{
		string path = PathFor(repoRoot, mapId);
		if (!File.Exists(path)) return null;
		using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
		var set = new HashSet<(int, int)>();
		foreach (JsonElement sector in document.RootElement.GetProperty("sectors").EnumerateArray())
			set.Add((sector[0].GetInt32(), sector[1].GetInt32()));
		return new BotNavMask(document.RootElement.GetProperty("sectorMetres").GetSingle(), set,
			document.RootElement.GetProperty("source").GetString() ?? "");
	}

	public static BotNavMask FromSectors(float sectorMetres, IEnumerable<(int, int)> sectors) => new(sectorMetres, sectors.ToHashSet(), "test");

	/// <summary>True when any playable sector lies within <paramref name="margin"/> metres of the rectangle.</summary>
	public bool Touches(float x0, float y0, float x1, float y1, float margin)
	{
		int sx0 = (int)MathF.Floor((x0 - margin) / SectorMetres), sx1 = (int)MathF.Floor((x1 + margin) / SectorMetres);
		int sy0 = (int)MathF.Floor((y0 - margin) / SectorMetres), sy1 = (int)MathF.Floor((y1 + margin) / SectorMetres);
		for (int sx = sx0; sx <= sx1; sx++)
			for (int sy = sy0; sy <= sy1; sy++)
				if (sectors.Contains((sx, sy))) return true;
		return false;
	}
}
