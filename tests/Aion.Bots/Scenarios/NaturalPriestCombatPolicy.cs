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

	/// <summary>Skill ids by role, for recognising them in packets and effect lists.</summary>
	public static IReadOnlySet<int> Ids(string role) => All.Where(skill => skill.Role == role).Select(skill => (int)skill.Id).ToHashSet();

	public static NaturalPriestSkill? Best(string role, int level, IReadOnlyDictionary<int, BotSkill> learned,
		IEnumerable<NaturalPriestSkill>? catalog = null) =>
		(catalog ?? All).Where(skill => skill.Role == role && skill.MinimumLevel <= level && learned.ContainsKey(skill.Id))
		.OrderByDescending(skill => skill.MinimumLevel).ThenByDescending(skill => skill.Id).FirstOrDefault();
}

/// <param name="TargetAdjacent">The target is in melee reach: within <see cref="NaturalPriestCombatPolicy.MeleeReach"/>
/// by the client's estimate, or it hit the bot within the last few seconds (a chasing monster's client
/// position lags; its swings do not).</param>
/// <param name="InEmergency">HP fell to <see cref="NaturalPriestCombatPolicy.EmergencyPercent"/> and has not
/// recovered to <see cref="NaturalPriestCombatPolicy.EmergencyClearPercent"/> yet: sustain only.</param>
public sealed record NaturalCombatObservation(int Level, int Hp, int MaxHp, int Mp, int MaxMp,
	bool Dead, bool Aggro, float? TargetDistance, int? TargetObjectId,
	IReadOnlyDictionary<int, BotSkill> Learned, IReadOnlyDictionary<int, DateTimeOffset> Cooldowns,
	string? OpenChainCategory = null, int? OpenChainTargetId = null, DateTimeOffset? ChainExpiresAt = null,
	bool? HasBlessing = null, bool HasLifePotion = false, bool HasManaPotion = false,
	bool LifePotionReady = false, bool ManaPotionReady = false, int NearbyAggressors = 0,
	int? TargetHpPercent = null, bool HasHealedThisFight = false,
	bool HasHotPotion = false, bool HotPotionReady = false, bool HotPotionActive = false,
	bool Cornered = false, bool TargetAdjacent = false, bool InEmergency = false);

public sealed record NaturalCombatChoice(string Action, NaturalPriestSkill? Skill, int? TargetObjectId,
	string Reason, NaturalDecisionCheck[] Checks);

/// <summary>
/// Pure, deterministic one-step policy. The caller supplies only observed client state.
///
/// The Priest is a melee class with a few ranged spells, and this plays it the way the recorded human did
/// (docs/session-recording.md, 2026-09-24): open with Smite from range and let the monster come; once it is
/// adjacent, the instants first because nothing can interrupt them (Infernal Blaze with its stun, Hallowed
/// Strike with its 30% attack-speed slow, every 8 s), Smite as the filler, and the mace between skills.
/// Never walk into melee: the monster closes, and walking pulls the bot into its neighbours' circles.
/// Heal at 70% against one attacker and at 55% against two or more (damage ends a two-on-one; healing
/// alone does not), potion at 80%, and below 35% sustain only until 45% again.
/// </summary>
public static class NaturalPriestCombatPolicy
{
	/// <summary>Attackers at which low HP means leave rather than heal through it.</summary>
	public const int SwarmedAttackers = 3;

	/// <summary>Client distance at which a monster is on the bot (its bound radius plus the swing reach).</summary>
	public const float MeleeReach = 3f;

	public const int HealPercentSingle = 70, HealPercentMultiple = 55, EmergencyPercent = 35, EmergencyClearPercent = 45;

	public static NaturalCombatChoice Decide(NaturalCombatObservation state, DateTimeOffset now,
		IEnumerable<NaturalPriestSkill>? catalog = null)
	{
		var checks = new List<NaturalDecisionCheck>();
		if (state.Dead) return Choice("revive", null, "Client reported death.");
		if (state.MaxHp <= 0 || state.MaxMp <= 0 || state.Hp < 0 || state.Mp < 0)
			return Choice("blocked", null, "Client life statistics are incomplete.");
		NaturalPriestSkill? heal = NaturalPriestSkills.Best("heal", state.Level, state.Learned, catalog);
		bool fighting = state.Aggro || state.TargetObjectId != null || state.NearbyAggressors > 0;
		bool adjacent = state.TargetAdjacent || state.TargetDistance is float near && near <= MeleeReach;
		// Low HP: heal through it while heals and potions last, as a player does against two monsters (the
		// recorded human run kept chaining Healing Light down to 24% and won). Retreat only when swarmed (three
		// or more attackers outdamage the heal) or when nothing is left to heal with. Cornered: no checked
		// escape leads away from the pack, so fight it out regardless.
		if (!state.Cornered && fighting && state.Hp * 100 <= state.MaxHp * 30)
		{
			bool swarmed = state.NearbyAggressors >= SwarmedAttackers;
			bool canHeal = heal != null && Eligible(heal, state.TargetObjectId, 0, state, now, reserveHeal: false);
			bool canPotion = state.HasLifePotion && state.LifePotionReady ||
				state.HasHotPotion && state.HotPotionReady && !state.HotPotionActive;
			if (swarmed)
				return Choice("retreat", null, $"HP is at or below 30% with {state.NearbyAggressors} client-observed attackers.");
			if (!canHeal && !canPotion)
				return Choice("retreat", null, "HP is at or below 30% and no self-heal or potion is available.");
		}
		int healPercent = state.InEmergency ? 100 : state.NearbyAggressors >= 2 ? HealPercentMultiple : HealPercentSingle;
		bool urgent = fighting && state.Hp * 100 <= state.MaxHp * healPercent;
		bool critical = state.Hp * 100 <= state.MaxHp * 25;
		if (fighting && state.Hp * 100 <= state.MaxHp * 80 &&
			state.HasHotPotion && state.HotPotionReady && !state.HotPotionActive)
			return Choice("hot-potion", null,
				"HP is at or below 80% in a fight; apply owned timed healing before the self-heal threshold.");
		if (urgent && !state.InEmergency && state.HasHealedThisFight && state.TargetHpPercent is > 0 and <= 15 &&
			state.TargetObjectId is int finishingTarget && state.TargetDistance is float finishingDistance)
		{
			NaturalPriestSkill? finisher = NaturalPriestSkills.Best("smite", state.Level, state.Learned, catalog);
			if (finisher != null && Eligible(finisher, finishingTarget, finishingDistance,
				state, now, reserveHeal: true))
				return Choice("cast-target", finisher,
					"Client-observed target is at or below 15% HP after this fight already received a self-heal.");
		}
		if (urgent && heal != null && Eligible(heal, state.TargetObjectId, 0, state, now, reserveHeal: false))
			return Choice("cast-self", heal, state.InEmergency
				? $"Emergency: HP fell to {EmergencyPercent}% and has not recovered to {EmergencyClearPercent}%."
				: $"HP is at or below {healPercent}% during a client-observed fight with {state.NearbyAggressors} attackers.");
		if (critical && state.HasLifePotion && state.LifePotionReady)
			return Choice("life-potion", null, "Critical HP and self-heal is unavailable; consume an owned life potion.");
		if (state.Mp < (heal?.ManaCost ?? 0) + 10 && state.HasManaPotion && state.ManaPotionReady)
			return Choice("mana-potion", null, "Mana is below the healing reserve; consume an owned mana potion.");
		if (critical && state.Aggro && !state.Cornered)
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
		if (distance > 25 && !adjacent)
			return Choice("approach", null, "Target is outside Priest spell range.");
		// The rotation. Adjacent: instants first (they cannot be interrupted and each buys time: the stun, the
		// slow), then Smite. At range: Smite is the pull and the filler while the monster closes.
		string[] rotation = adjacent ? ["followup", "infernal", "hallowed", "smite"] : ["followup", "smite"];
		foreach (string role in rotation)
		{
			NaturalPriestSkill? skill = NaturalPriestSkills.Best(role, state.Level, state.Learned, catalog);
			if (skill != null && Eligible(skill, target, distance, state, now, reserveHeal: true))
				return Choice("cast-target", skill, adjacent
					? $"Learned {role} is ready at melee and leaves healing mana reserved."
					: $"Learned {role} is in range, ready, and leaves healing mana reserved.");
		}
		if (!adjacent)
			return Choice("wait", null, "Nothing ready at range; the pulled monster is closing, so hold position.");
		return Choice("attack", null, "No skill is ready at melee; swing the mace.");

		NaturalCombatChoice Choice(string action, NaturalPriestSkill? skill, string reason) =>
			new(action, skill, state.TargetObjectId, reason, checks.ToArray());

		bool Eligible(NaturalPriestSkill skill, int? candidateTarget, float range,
			NaturalCombatObservation observed, DateTimeOffset instant, bool reserveHeal)
		{
			// A melee skill's template range (1 m) is measured by the server from bound radius to bound radius;
			// a monster that is on the bot is in reach whatever the client's lagging distance says.
			bool inRange = range <= skill.Range || skill.Range <= MeleeReach && adjacent;
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
