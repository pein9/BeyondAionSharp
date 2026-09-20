using System.Xml;
using System.Xml.Linq;

namespace Aion.Bots.Scenarios;

/// <summary>Client-data oracle for production-random TASK rewards; never calls the server's RNG or reward service.</summary>
public static class SoakCookingRewards
{
	private static readonly Lazy<(XElement Groups, Dictionary<int, string> Races)> Data = new(LoadData);

	public static IReadOnlyDictionary<int, (long Min, long Max)> For(CookingWorkOrder order) =>
		Select(Data.Value.Groups, Data.Value.Races, order.Master.Race.ToString(), CookingLearnScenario.SkillId, order.SkillLevel);

	public static IReadOnlyDictionary<int, (long Min, long Max)> Select(XElement groups,
		IReadOnlyDictionary<int, string> itemRaces, string race, int skill, int skillPoint)
	{
		var result = new Dictionary<int, (long, long)>();
		foreach (var group in groups.Elements().Where(group => (string?)group.Attribute("bonusType") == "TASK" &&
			((float?)group.Attribute("chance") ?? 100) > 0))
		{
			if (group.Name.LocalName is not ("craft_materials" or "craft_shop" or "craft_recipes" or "craft_bundles"))
				throw new InvalidDataException($"Unknown TASK reward group {group.Name}.");
			bool recipe = group.Name.LocalName is "craft_recipes" or "craft_bundles";
			foreach (var item in group.Elements("item").Where(item => (int?)item.Attribute("skill") == skill))
			{
				int low = (int?)item.Attribute(recipe ? "level" : "minLevel") ?? throw new InvalidDataException("Missing TASK reward level.");
				int high = recipe ? Math.Min(low + 40, low / 100 * 100 + 99) : (int?)item.Attribute("maxLevel") ?? throw new InvalidDataException("Missing TASK reward maximum.");
				if (skillPoint < low || skillPoint > high) continue;
				int id = (int?)item.Attribute("id") ?? throw new InvalidDataException("Missing TASK reward item.");
				if (!itemRaces.TryGetValue(id, out string? itemRace)) throw new InvalidDataException($"Unknown reward item {id}.");
				string entryRace = (string?)item.Attribute("race") ?? "PC_ALL";
				if ((itemRace != "PC_ALL" && itemRace != race) || (entryRace != "PC_ALL" && entryRace != race)) continue;
				result.Add(id, recipe ? (1, 1) : (3, 5));
			}
		}
		return result;
	}

	public static KeyValuePair<int, long>? ValidateDelta(IReadOnlyDictionary<int, long> before,
		IReadOnlyDictionary<int, long> after, IReadOnlyDictionary<int, (long Min, long Max)> allowed)
	{
		var changes = before.Keys.Concat(after.Keys).Distinct()
			.Select(id => new KeyValuePair<int, long>(id, after.GetValueOrDefault(id) - before.GetValueOrDefault(id)))
			.Where(change => change.Value != 0).ToArray();
		if (changes.Length == 0 && allowed.Count == 0) return null;
		if (changes.Length != 1 || !allowed.TryGetValue(changes[0].Key, out var bounds) ||
			changes[0].Value < bounds.Min || changes[0].Value > bounds.Max)
			throw new InvalidDataException("Work-order reward changed inventory outside its race/skill-specific TASK bonus contract.");
		return changes[0];
	}

	private static (XElement, Dictionary<int, string>) LoadData()
	{
		string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "..", "..", "game-server", "data", "static_data", "items"));
		var groups = XElement.Load(Path.Combine(root, "item_groups.xml"));
		var ids = groups.Elements().Where(group => (string?)group.Attribute("bonusType") == "TASK")
			.Elements("item").Select(item => (int)item.Attribute("id")!).ToHashSet();
		var races = new Dictionary<int, string>();
		using var reader = XmlReader.Create(Path.Combine(root, "item_templates.xml"));
		while (reader.Read())
			if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "item_template" &&
				int.TryParse(reader.GetAttribute("id"), out int id) && ids.Contains(id))
				races.Add(id, reader.GetAttribute("race") ?? "PC_ALL");
		return (groups, races);
	}
}
