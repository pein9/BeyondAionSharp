using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// CP-35: what every new class profile must pass before its first run (docs/natural-class-profiles.md). The rules are
/// about the shipped data and the server's chain rule, not about how well the class plays.
/// </summary>
public static class NaturalProfileValidator
{
	/// <summary>
	/// Every problem of the profile, each named; empty for a valid one.
	/// <list type="number">
	/// <item>Each auto-learned ACTIVE, CHARGE or TOGGLE skill up to <paramref name="maximumLevel"/> has one role or an
	/// exclusion with a reason, and not both.</item>
	/// <item>A follow-up has its opener in the catalog, learned no later.</item>
	/// <item>A counter, a charge and an out-of-combat skill are in no rotation.</item>
	/// <item>No rotation line casts a non-chain skill, or another chain's first step, between an opener and its follow-up:
	/// either resets the chain (Java Skill.canUseSkill and ChainCondition.shouldReset).</item>
	/// <item>The gear groups lie inside the class's masteries.</item>
	/// <item>NR-70a: a toggle is in no rotation. A kept toggle is a toggle and no stance, which any skill cast ends (Java
	/// StanceObserver), and the kept ones are no more than the server lets the class keep at once: a further one ends the
	/// first (Java EffectController.addEffect 76-86), and the two would be cast in turn without end.</item>
	/// <item>NR-110a: a skill that summons a spirit is in no rotation, and a kept spirit is such a skill.</item>
	/// <item>NR-110d: a skill cast on the spirit is in no rotation, which is a list of attacks; and a profile that gives
	/// a role to such a skill, or to an order, keeps a spirit.</item>
	/// </list>
	/// </summary>
	/// <param name="skills">The profile's catalog.</param>
	/// <param name="excluded">Skill id to the reason the profile does not cast it.</param>
	/// <param name="rotations">The profile's rotation lines, each a list of skill ids in cast order.</param>
	/// <param name="toggles">The toggles the profile keeps on; null for none.</param>
	/// <param name="spirits">The spirits the profile keeps; null for none.</param>
	public static IReadOnlyList<string> Problems(StaticData data, PlayerClass playerClass, int maximumLevel,
		IReadOnlyList<NaturalPriestSkill> skills, IReadOnlyDictionary<int, string> excluded,
		IEnumerable<IReadOnlyList<int>> rotations, NaturalGearRules gear, IReadOnlyList<NaturalKeptToggle>? toggles = null,
		IReadOnlyList<NaturalKeptSpirit>? spirits = null)
	{
		ArgumentNullException.ThrowIfNull(skills);
		ArgumentNullException.ThrowIfNull(excluded);
		ArgumentNullException.ThrowIfNull(rotations);
		ArgumentNullException.ThrowIfNull(gear);
		var problems = new List<string>();
		foreach (var twice in skills.GroupBy(skill => skill.Id).Where(group => group.Count() > 1))
			problems.Add($"Skill {twice.Key} has {twice.Count()} rows; a skill has one role.");
		Dictionary<int, NaturalPriestSkill> byId = skills.GroupBy(skill => (int)skill.Id).ToDictionary(group => group.Key, group => group.First());
		foreach ((int id, int level) in NaturalSkillCatalog.AutoLearnedCastable(data, playerClass, maximumLevel)
			.Concat(NaturalSkillCatalog.AutoLearnedToggles(data, playerClass, maximumLevel)).OrderBy(entry => entry.Key))
		{
			bool cast = byId.ContainsKey(id), left = excluded.TryGetValue(id, out string? reason);
			if (cast && left) problems.Add($"Skill {id} has a role and an exclusion.");
			else if (!cast && !left) problems.Add($"Skill {id}, auto-learned at level {level}, has neither a role nor an exclusion.");
			else if (left && string.IsNullOrWhiteSpace(reason)) problems.Add($"Skill {id} is excluded without a reason.");
		}
		foreach (NaturalPriestSkill followUp in skills.Where(skill => skill.RequiresChainCategory != null))
			if (!skills.Any(opener => opener.ChainCategory == followUp.RequiresChainCategory && opener.MinimumLevel <= followUp.MinimumLevel))
				problems.Add($"Follow-up {followUp.Id} needs {followUp.RequiresChainCategory}, which no skill learned by level {followUp.MinimumLevel} opens.");
		int line = 0;
		foreach (IReadOnlyList<int> rotation in rotations)
		{
			line++;
			foreach (int id in rotation)
			{
				if (!byId.TryGetValue(id, out NaturalPriestSkill? skill)) problems.Add($"Rotation {line} casts skill {id}, which is not in the catalog.");
				else if (skill.CounterStatus != null) problems.Add($"Rotation {line} casts counter skill {id} ({skill.CounterStatus}).");
				else if (skill.Activation == "CHARGE") problems.Add($"Rotation {line} casts charge skill {id}.");
				else if (skill.Activation == "TOGGLE") problems.Add($"Rotation {line} casts toggle {id}.");
				else if (skill.SummonsNpcId != 0) problems.Add($"Rotation {line} casts skill {id}, which summons a spirit.");
				else if (skill.OutOfCombatOnly) problems.Add($"Rotation {line} casts skill {id}, which cannot be cast in combat.");
				else if (skill.TargetKind == "MYPET") problems.Add($"Rotation {line} casts skill {id}, which is cast on the spirit.");
			}
			for (int first = 0; first < rotation.Count; first++)
			{
				if (!byId.TryGetValue(rotation[first], out NaturalPriestSkill? opener) || opener.ChainCategory == null) continue;
				// The line's next skill that needs this opener's category; what stands between the two is checked.
				int followUp = -1;
				for (int next = first + 1; next < rotation.Count && followUp < 0; next++)
					if (byId.TryGetValue(rotation[next], out NaturalPriestSkill? later) && later.RequiresChainCategory == opener.ChainCategory)
						followUp = next;
				for (int between = first + 1; between < followUp; between++)
				{
					if (!byId.TryGetValue(rotation[between], out NaturalPriestSkill? cast)) continue;
					if (cast.ChainCategory == null)
						problems.Add($"Rotation {line} casts non-chain skill {cast.Id} between {opener.Id} and its follow-up.");
					else if (NaturalSkillCatalog.IsChainOpener(cast) && cast.ChainCategory != opener.ChainCategory)
						problems.Add($"Rotation {line} casts the opener {cast.Id} of another chain between {opener.Id} and its follow-up.");
				}
			}
		}
		foreach (string group in gear.GearGroups.Where(group => !gear.Wears(group)).Order())
			problems.Add($"Gear group {group} is outside the masteries of {playerClass}.");
		int chants = 0, others = 0;
		foreach (NaturalKeptToggle kept in toggles ?? [])
		{
			NaturalPriestSkill[] ranks = skills.Where(skill => skill.Role == kept.Role).ToArray();
			if (ranks.Length == 0) problems.Add($"Kept toggle '{kept.Role}' has no skill in the catalog.");
			foreach (NaturalPriestSkill rank in ranks)
			{
				SkillTemplate? template = data.SkillDataDh.GetSkillTemplate(rank.Id);
				if (template?.IsToggle() != true) problems.Add($"Kept toggle '{kept.Role}' holds skill {rank.Id}, which is no toggle.");
				else if (template.IsStance()) problems.Add($"Kept toggle '{kept.Role}' holds the stance {rank.Id}, which any skill cast ends.");
			}
			// The limit is on toggles of the slot NOSHOW (Java EffectController.isNoShowToggle 600-602).
			if (ranks.Length == 0 || data.SkillDataDh.GetSkillTemplate(ranks[0].Id) is not { } first ||
				first.GetTargetSlot() != SkillTargetSlot.NOSHOW) continue;
			if (first.GetSubType() == SkillSubType.CHANT) chants++;
			else others++;
		}
		if (spirits is not { Count: > 0 })
			foreach (NaturalPriestSkill skill in skills.Where(skill => skill.TargetKind == "MYPET" || skill.OrdersSpirit))
				problems.Add($"Skill {skill.Id} is for a spirit, and the profile keeps none.");
		foreach (NaturalKeptSpirit kept in spirits ?? [])
		{
			NaturalPriestSkill[] ranks = skills.Where(skill => skill.Role == kept.Role).ToArray();
			if (ranks.Length == 0) problems.Add($"Kept spirit '{kept.Role}' has no skill in the catalog.");
			foreach (NaturalPriestSkill rank in ranks.Where(rank => rank.SummonsNpcId == 0))
				problems.Add($"Kept spirit '{kept.Role}' holds skill {rank.Id}, which summons no spirit.");
		}
		// Java EffectController.addEffect 76-86: three of the sub type CHANT; of the others one, and two for a Ranger or a Rider.
		int otherLimit = playerClass is PlayerClass.RANGER or PlayerClass.RIDER ? 2 : 1;
		if (chants > 3) problems.Add($"{chants} mantras are kept on; the server keeps three.");
		if (others > otherLimit) problems.Add($"{others} toggles are kept on; the server keeps {otherLimit} for a {playerClass}.");
		return problems;
	}

	/// <summary>Throws with every problem of the profile.</summary>
	public static void Require(StaticData data, PlayerClass playerClass, int maximumLevel, IReadOnlyList<NaturalPriestSkill> skills,
		IReadOnlyDictionary<int, string> excluded, IEnumerable<IReadOnlyList<int>> rotations, NaturalGearRules gear,
		IReadOnlyList<NaturalKeptToggle>? toggles = null, IReadOnlyList<NaturalKeptSpirit>? spirits = null)
	{
		IReadOnlyList<string> problems = Problems(data, playerClass, maximumLevel, skills, excluded, rotations, gear, toggles, spirits);
		if (problems.Count > 0)
			throw new InvalidDataException($"The {playerClass} profile is not valid: " + string.Join(" ", problems));
	}
}
