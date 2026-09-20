using System.Xml;
using System.Xml.Linq;
using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>Independent client-data interpretation of visible AP_BOOST additions, never server stat evaluation.</summary>
public static class PvpApBoostCatalog
{
	private sealed record Definition(int Value, byte Slot);
	private static readonly Lazy<IReadOnlyDictionary<int, Definition?>> Catalog = new(Load);
	private static readonly string[] Slots = ["BUFF", "DEBUFF", "CHANT", "SPEC", "SPEC2", "BOOST", "NOSHOW", "NONE"];

	public static int Percent(IReadOnlyList<BotVisibleEffect>? observed) => Percent(observed, Catalog.Value);

	internal static int Percent(IReadOnlyList<BotVisibleEffect>? observed, XElement templates) => Percent(observed, Parse(templates.Elements("skill_template")));

	private static int Percent(IReadOnlyList<BotVisibleEffect>? observed, IReadOnlyDictionary<int, Definition?> catalog)
	{
		if (observed == null) throw new InvalidDataException("PvP reward has no observed effect snapshot.");
		int percent = 100;
		var ids = new HashSet<int>();
		foreach (var effect in observed)
		{
			if (!catalog.TryGetValue(effect.SkillId, out var definition))
				throw new InvalidDataException($"Unshipped visible effect {effect.SkillId}.");
			if (definition == null) continue;
			if (!ids.Add(effect.SkillId) || effect.SkillLevel == 0 || effect.TargetSlot != definition.Slot)
				throw new InvalidDataException("AP boost snapshot has duplicate or contradictory effect identity.");
			percent = checked(percent + definition.Value);
		}
		return percent;
	}

	private static IReadOnlyDictionary<int, Definition?> Parse(IEnumerable<XElement> templates)
	{
		var result = new Dictionary<int, Definition?>();
		foreach (var skill in templates)
		{
			int id = (int)skill.Attribute("skill_id")!;
			var changes = skill.Descendants("change").Where(value => (string?)value.Attribute("stat") == "AP_BOOST").ToArray();
			Definition? definition = null;
			if (changes.Length > 0)
			{
				int slot = Array.IndexOf(Slots, (string?)skill.Attribute("tslot"));
				if ((string?)skill.Attribute("activation") != "ACTIVE" || slot is < 0 or > 5 ||
					changes.Any(change => change.Parent?.Name.LocalName != "apboost" ||
						(string?)change.Attribute("func") != "ADD" || ((int?)change.Attribute("delta") ?? 0) != 0 ||
						change.HasElements || (int?)change.Attribute("value") is not >= 0))
					throw new InvalidDataException($"Unsupported AP_BOOST definition on skill {id}; extend the oracle explicitly.");
				definition = new(changes.Sum(change => (int)change.Attribute("value")!), checked((byte)slot));
			}
			result.Add(id, definition);
		}
		return result;
	}

	private static IReadOnlyDictionary<int, Definition?> Load()
	{
		string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
		using var reader = XmlReader.Create(Path.Combine(root, "game-server/data/static_data/skills/skill_templates.xml"));
		return Parse(ReadTemplates());
		IEnumerable<XElement> ReadTemplates()
		{
			while (reader.Read())
				if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "skill_template")
				{
					using var subtree = reader.ReadSubtree();
					yield return XElement.Load(subtree);
				}
		}
	}
}
