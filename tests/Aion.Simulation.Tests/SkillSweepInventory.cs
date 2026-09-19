using System.Globalization;
using System.Xml.Linq;
using Aion.GameServer.Model;

namespace Aion.Simulation.Tests;

internal enum SkillSweepAction { Cast, Passive, Gather, Craft }

internal sealed record SkillSweepCase(PlayerClass Class, Race Race, int SkillId, int SkillLevel,
	int MinimumLevel, SkillSweepAction Action, string Activation)
{
	public string Id => $"{Class}:{Race}:{SkillId.ToString(CultureInfo.InvariantCulture)}";
}

/// <summary>Source inventory, not execution evidence. Non-castable skills must not be silently omitted.</summary>
internal static class SkillSweepInventory
{
	public static IReadOnlyList<SkillSweepCase> Load(string staticRoot) => Expand(
		Directory.EnumerateFiles(Path.Combine(staticRoot, "skill_tree"), "*.xml").Order(StringComparer.Ordinal)
			.SelectMany(path => XDocument.Load(path).Root!.Elements("skill")),
		XDocument.Load(Path.Combine(staticRoot, "skills", "skill_templates.xml")).Root!.Elements("skill_template"));

	internal static IReadOnlyList<SkillSweepCase> Expand(IEnumerable<XElement> learnRows, IEnumerable<XElement> skillRows)
	{
		var skills = skillRows.ToDictionary(s => (int)s.Attribute("skill_id")!);
		var result = new SortedDictionary<string, SkillSweepCase>(StringComparer.Ordinal);
		foreach (var learn in learnRows)
		{
			int id = (int)learn.Attribute("skillId")!;
			if (!skills.TryGetValue(id, out var template)) throw new InvalidDataException($"Skill tree references missing skill {id}.");
			string activation = (string?)template.Attribute("activation") ?? throw new InvalidDataException($"Skill {id} has no activation kind.");
			var action = activation switch
			{
				"PASSIVE" => SkillSweepAction.Passive,
				"ACTIVE" or "TOGGLE" or "MAINTAIN" or "CHARGE" => SkillSweepAction.Cast,
				"NONE" when id is 30001 or 30003 => SkillSweepAction.Gather,
				"NONE" when id == 40009 => SkillSweepAction.Craft,
				_ => throw new InvalidDataException($"Skill {id} requires an explicit execution route for activation {activation}.")
			};
			var race = Enum.Parse<Race>((string?)learn.Attribute("race") ?? "PC_ALL");
			// One execution per applicable class; race-specific rows retain their actual race.
			// PC_ALL needs one ordinary subject, not duplicate success claims for the same row.
			if (race == Race.PC_ALL) race = Race.ELYOS;
			if (race is not (Race.ELYOS or Race.ASMODIANS)) throw new InvalidDataException($"Skill {id} has unsupported player race {race}.");
			int minimumLevel = (int)learn.Attribute("minLevel")!;
			var classes = Enum.GetValues<PlayerClass>();
			if (learn.Attribute("classId") is { } classId)
			{
				var declaredClass = Enum.Parse<PlayerClass>(classId.Value);
				if (!Enum.IsDefined(declaredClass)) throw new InvalidDataException($"Skill {id} has unsupported player class {classId.Value}.");
				// LearnNewSkills also learns the starting class's sub-level-10 skills
				// for advanced classes; omitting them would undercount class coverage.
				classes = classes.Where(c => c == declaredClass || minimumLevel < 10
					&& declaredClass.IsStartingClass() && c.GetStartingClass() == declaredClass).ToArray();
			}
			foreach (var playerClass in classes)
			{
				// Missing lvl is the shipped template's zero default, not an invented rank one.
				var row = new SkillSweepCase(playerClass, race, id, (int?)template.Attribute("lvl") ?? 0,
					minimumLevel, action, activation);
				if (!result.TryAdd(row.Id, row)) throw new InvalidDataException($"Duplicate skill sweep case {row.Id}.");
			}
		}
		if (result.Count == 0) throw new InvalidDataException("Skill sweep inventory is empty.");
		return result.Values.ToArray();
	}
}
