using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

public sealed record NaturalPriestSkill(ushort Id, int MinimumLevel, string Role, int ManaCost,
	float Range, int CooldownId, int CooldownDeciseconds, string? ChainCategory = null,
	string? RequiresChainCategory = null, int ChainWindowMillis = 0);

/// <summary>
/// Frozen 4.8 Priest level 1-9 active skills. The learned SM_SKILL_LIST is still the authority:
/// this catalog never grants a skill. Java skill_tree.xml and skill_templates.xml at ce54b7931.
/// Cooldowns in the templates are deciseconds (Skill.setCooldowns multiplies by 100).
/// </summary>
public static class NaturalPriestSkills
{
	public static readonly NaturalPriestSkill[] All =
	[
		new(1838, 1, "heal", 13, 23, 1553, 0),
		new(4012, 1, "smite", 25, 25, 1229, 20, "P_CHAINA_1TH_1"),
		new(1614, 3, "hallowed", 11, 1, 1512, 80, "C_CHAINJ_1TH_1"),
		new(1684, 5, "blessing", 41, 15, 1602, 5),
		new(1839, 6, "heal", 23, 23, 1553, 0),
		new(4013, 6, "smite", 32, 25, 1229, 20, "P_CHAINA_1TH_1"),
		new(1814, 7, "infernal", 22, 25, 1549, 240, "C_CHAINB_1TH_1"),
		new(1615, 8, "hallowed", 15, 1, 1512, 80, "C_CHAINJ_1TH_1"),
	];

	public static NaturalPriestSkill? Best(string role, int level, IReadOnlyDictionary<int, BotSkill> learned,
		IEnumerable<NaturalPriestSkill>? catalog = null) =>
		(catalog ?? All).Where(skill => skill.Role == role && skill.MinimumLevel <= level && learned.ContainsKey(skill.Id))
		.OrderByDescending(skill => skill.MinimumLevel).ThenByDescending(skill => skill.Id).FirstOrDefault();
}

public sealed record NaturalCombatObservation(int Level, int Hp, int MaxHp, int Mp, int MaxMp,
	bool Dead, bool Aggro, float? TargetDistance, int? TargetObjectId,
	IReadOnlyDictionary<int, BotSkill> Learned, IReadOnlyDictionary<int, DateTimeOffset> Cooldowns,
	string? OpenChainCategory = null, int? OpenChainTargetId = null, DateTimeOffset? ChainExpiresAt = null,
	bool? HasBlessing = null, bool HasLifePotion = false, bool HasManaPotion = false,
	bool LifePotionReady = false, bool ManaPotionReady = false);

public sealed record NaturalCombatChoice(string Action, NaturalPriestSkill? Skill, int? TargetObjectId,
	string Reason, NaturalDecisionCheck[] Checks);

/// <summary>Pure, deterministic one-step policy. The caller supplies only observed client state.</summary>
public static class NaturalPriestCombatPolicy
{
	public static NaturalCombatChoice Decide(NaturalCombatObservation state, DateTimeOffset now,
		IEnumerable<NaturalPriestSkill>? catalog = null)
	{
		var checks = new List<NaturalDecisionCheck>();
		if (state.Dead) return Choice("revive", null, "Client reported death.");
		if (state.MaxHp <= 0 || state.MaxMp <= 0 || state.Hp < 0 || state.Mp < 0)
			return Choice("blocked", null, "Client life statistics are incomplete.");
		NaturalPriestSkill? heal = NaturalPriestSkills.Best("heal", state.Level, state.Learned, catalog);
		bool urgent = state.Hp * 100 <= state.MaxHp * 55;
		bool critical = state.Hp * 100 <= state.MaxHp * 25;
		if (urgent && heal != null && Eligible(heal, state.TargetObjectId, 0, state, now, reserveHeal: false))
			return Choice("cast-self", heal, "HP is below the healing threshold.");
		if (critical && state.HasLifePotion && state.LifePotionReady)
			return Choice("life-potion", null, "Critical HP and self-heal is unavailable; consume an owned life potion.");
		if (state.Mp < (heal?.ManaCost ?? 0) + 10 && state.HasManaPotion && state.ManaPotionReady)
			return Choice("mana-potion", null, "Mana is below the healing reserve; consume an owned mana potion.");
		if (critical && state.Aggro)
			return Choice("retreat", null, "Critical HP and no legal self-heal; leave the aggressor.");
		NaturalPriestSkill? blessing = NaturalPriestSkills.Best("blessing", state.Level, state.Learned, catalog);
		if (!state.Aggro && state.TargetObjectId == null && state.HasBlessing == false && blessing != null &&
			state.Mp >= blessing.ManaCost + (heal?.ManaCost ?? 0) &&
			Eligible(blessing, null, 0, state, now, reserveHeal: true))
			return Choice("cast-self", blessing, "Learned one-hour protection buff is absent from the observed effects.");
		if (state.TargetObjectId is not int target || state.TargetDistance is not float distance)
		{
			if (state.Aggro) return Choice("defend", null, "Aggression observed without a target; reacquire before pulling.");
			return state.Hp * 100 < state.MaxHp * 90 || state.Mp * 100 < state.MaxMp * 80
				? Choice("rest", null, "No engaged target; recover before the next pull.")
				: Choice("ready", null, "HP and MP exceed the conservative next-pull thresholds.");
		}
		if (distance > 25)
			return Choice("approach", null, "Target is outside Priest spell range.");
		foreach (string role in new[] { "followup", "infernal", "smite", "hallowed" })
		{
			NaturalPriestSkill? skill = NaturalPriestSkills.Best(role, state.Level, state.Learned, catalog);
			if (skill != null && Eligible(skill, target, distance, state, now, reserveHeal: true))
				return Choice("cast-target", skill, $"Learned {role} is in range, ready, and leaves healing mana reserved.");
		}
		if (distance > 3)
			return Choice("approach", null, "No ranged skill is ready; close for ordinary melee.");
		return Choice("attack", null, "No legal skill is ready; use paced ordinary attack.");

		NaturalCombatChoice Choice(string action, NaturalPriestSkill? skill, string reason) =>
			new(action, skill, state.TargetObjectId, reason, checks.ToArray());

		bool Eligible(NaturalPriestSkill skill, int? candidateTarget, float range,
			NaturalCombatObservation observed, DateTimeOffset instant, bool reserveHeal)
		{
			bool inRange = range <= skill.Range;
			bool ready = !observed.Cooldowns.TryGetValue(skill.CooldownId, out var until) || until <= instant;
			int reserve = reserveHeal && heal != null ? heal.ManaCost : 0;
			bool enoughMana = observed.Mp >= skill.ManaCost + reserve;
			bool chainReady = skill.RequiresChainCategory == null ||
				(skill.RequiresChainCategory == observed.OpenChainCategory &&
				 observed.OpenChainTargetId == candidateTarget && observed.ChainExpiresAt > instant);
			checks.Add(new($"skill-{skill.Id}", inRange && ready && enoughMana && chainReady ? "pass" : "skip",
				$"range={inRange}, cooldown={ready}, mana={enoughMana}, prerequisite-chain={chainReady}; " +
				$"cooldown-group={skill.CooldownId}, own-cooldown={skill.CooldownDeciseconds}ds."));
			return inRange && ready && enoughMana && chainReady;
		}
	}
}
