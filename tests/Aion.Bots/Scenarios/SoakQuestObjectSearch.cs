using System.Xml.Linq;
using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>Rotating shipped collection-object hints, never object ids or reservations.</summary>
public sealed class SoakQuestObjectSearch
{
	private static readonly Lazy<IReadOnlyList<BotPosition>> sacks = new(() => Load("210010000_Poeta.xml", 210010000, 700105));
	private static readonly Lazy<IReadOnlyList<BotPosition>> baskets = new(() => Load("220010000_Ishalgen.xml", 220010000, 700124));
	private readonly IReadOnlyList<BotPosition> positions;
	private readonly HashSet<BotPosition> rejected = [];
	private int next;

	public SoakQuestObjectSearch(IReadOnlyList<BotPosition> positions, int offset)
	{
		if (positions.Count == 0 || positions.Distinct().Count() != positions.Count || offset < 0)
			throw new ArgumentException("Quest exploration needs distinct positions and a nonnegative offset.");
		this.positions = Array.AsReadOnly(positions.ToArray());
		next = offset % positions.Count;
	}

	public BotPosition? Next()
	{
		for (int checkedCount = 0; checkedCount < positions.Count; checkedCount++)
		{
			var position = positions[next];
			next = (next + 1) % positions.Count;
			if (!rejected.Contains(position)) return position;
		}
		return null;
	}

	public void Reject(BotPosition position)
	{
		if (!positions.Contains(position)) throw new ArgumentException("Unknown quest exploration position.", nameof(position));
		rejected.Add(position);
	}

	public static IReadOnlyList<BotPosition> ShippedPositions(ScenarioRace race, int templateId) => (race, templateId) switch
	{
		(ScenarioRace.Elyos, 700105) => sacks.Value,
		(ScenarioRace.Asmodians, 700124) => baskets.Value,
		_ => throw new ArgumentException("Only the finite starter collection objectives have exploration catalogs."),
	};

	private static IReadOnlyList<BotPosition> Load(string file, int mapId, int templateId)
	{
		string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
		var map = XElement.Load(Path.Combine(root, "game-server/data/static_data/spawns/Npcs", file)).Element("spawn_map")!;
		var spawn = map.Elements("spawn").Single(value => (int?)value.Attribute("npc_id") == templateId);
		if ((int?)map.Attribute("map_id") != mapId || (int?)spawn.Attribute("respawn_time") != 295)
			throw new InvalidDataException("Starter quest collection spawn contract changed.");
		var positions = spawn.Elements("spot").Select(value => new BotPosition(
			(float)value.Attribute("x")!, (float)value.Attribute("y")!, (float)value.Attribute("z")!, 0)).ToArray();
		if (positions.Length == 0 || positions.Distinct().Count() != positions.Length)
			throw new InvalidDataException("Empty or duplicate starter collection positions.");
		return Array.AsReadOnly(positions);
	}
}
