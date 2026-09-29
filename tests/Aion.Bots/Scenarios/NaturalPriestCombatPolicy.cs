using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <param name="DpCost">DP the skill needs and spends (Salvation: startconditions/dp).</param>
/// <param name="ReagentItemId">Item the skill consumes (Herb Treatment, MP Recovery: actions/itemuse).</param>
public sealed record NaturalPriestSkill(ushort Id, int MinimumLevel, string Role, int ManaCost,
	float Range, int CooldownId, int CooldownDeciseconds, string? ChainCategory = null,
	string? RequiresChainCategory = null, int ChainWindowMillis = 0, int DpCost = 0,
	int ReagentItemId = 0, int ReagentCount = 0)
{
	/// <summary>Skills cast on the bot itself; every other role targets the monster.</summary>
	public bool TargetsSelf => Role is "heal" or "blessing" or "rejuvenation" or "salvation" or "herb" or "mp-recovery";

	/// <summary>Powder rest skills (4 s cast, cancelled by any hit): only the rest policy casts them.</summary>
	public bool IsPowderRest => Role is "herb" or "mp-recovery";
}

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
/// <param name="ShieldScrollReady">NA-19: <see cref="NaturalHelpItemPolicy.DecideShield"/> chose an owned, ready
/// Anti-Shock tier (HP at or below 50%, no shield active, not casting).</param>
public sealed record NaturalCombatObservation(int Level, int Hp, int MaxHp, int Mp, int MaxMp,
	bool Dead, bool Aggro, float? TargetDistance, int? TargetObjectId,
	IReadOnlyDictionary<int, BotSkill> Learned, IReadOnlyDictionary<int, DateTimeOffset> Cooldowns,
	string? OpenChainCategory = null, int? OpenChainTargetId = null, DateTimeOffset? ChainExpiresAt = null,
	bool? HasBlessing = null, bool HasLifePotion = false, bool HasManaPotion = false,
	bool LifePotionReady = false, bool ManaPotionReady = false, int NearbyAggressors = 0,
	int? TargetHpPercent = null, bool HasHealedThisFight = false,
	bool HasHotPotion = false, bool HotPotionReady = false, bool HotPotionActive = false,
	bool Cornered = false, bool TargetAdjacent = false, bool InEmergency = false, bool TargetSeasoned = false,
	bool TargetRanged = false, bool ConservativeRangedHold = false, int Dp = 0, bool? HasRejuvenation = null,
	bool ShieldScrollReady = false);

public sealed record NaturalCombatChoice(string Action, NaturalPriestSkill? Skill, int? TargetObjectId,
	string Reason, NaturalDecisionCheck[] Checks);

/// <summary>Observed eligibility and baseline disposition; no outcome is assigned to an unchosen action.</summary>
public sealed record NaturalCombatCandidate(string Action, ushort? SkillId, int? TargetObjectId,
	bool Legal, string[] IllegalReasons, string? BaselineRejection, bool NeedsNavigationCheck = false);

/// <summary>
/// Pure, deterministic one-step policy. The caller supplies only observed client state.
///
/// The Priest is a melee class with a few ranged spells, and this plays it the way the recorded human did
/// (docs/session-recording.md, 2026-09-24): open with Smite from range and let the monster come; once it is
/// adjacent, the instants first because nothing can interrupt them (Infernal Blaze with its stun, Hallowed
/// Strike with its 30% attack-speed slow, every 8 s), Smite as the filler, and the mace between skills.
/// Never walk into melee: the monster closes, and walking pulls the bot into its neighbours' circles.
/// Heal at 55% against one attacker and at 70% against two; flee from three or more attackers
/// regardless of HP. Use an owned timed-healing potion at 90%, and below 35% sustain only until 45% again.
/// </summary>
public static class NaturalPriestCombatPolicy
{
	public const string PolicyVersion = "natural-priest-baseline-v1";
	/// <summary>Attackers at which the Priest leaves regardless of HP.</summary>
	public const int SwarmedAttackers = 3;

	/// <summary>Client distance at which a monster is on the bot (its bound radius plus the swing reach).</summary>
	public const float MeleeReach = 3f;

	public const int HealPercentSingle = 55, HealPercentMultiple = 70, EmergencyPercent = 35, EmergencyClearPercent = 45;

	/// <summary>The HP percentage at which a fight becomes an emergency (heal chain and potions until
	/// <see cref="EmergencyExitPercent"/>). Earlier against a Seasoned or better target with a second attacker on
	/// the bot: two swings a round and interrupted heals made 35% too late against Hatata the Torturer
	/// (1,821 HP, stuns), where every death in the Ishalgen batches happened.</summary>
	public static int EmergencyEnterPercent(int attackers, bool targetSeasoned) =>
		attackers >= 2 && targetSeasoned ? 55 : EmergencyPercent;

	public static int EmergencyExitPercent(int attackers, bool targetSeasoned) =>
		EmergencyEnterPercent(attackers, targetSeasoned) + (EmergencyClearPercent - EmergencyPercent);

	/// <summary>Audit all client-observable candidate actions after Decide, including later branches it did not visit.</summary>
	public static NaturalCombatCandidate[] CandidateActions(NaturalCombatObservation state,
		DateTimeOffset now, NaturalCombatChoice chosen, IEnumerable<NaturalPriestSkill>? catalog = null,
		NaturalMauPolicyParameters? parameters = null)
	{
		NaturalMauPolicyParameters policy = parameters ?? NaturalMauPolicyParameters.Baseline;
		policy.Validate();
		NaturalPriestSkill[] skills = (catalog ?? NaturalPriestSkills.All).ToArray();
		NaturalPriestSkill? heal = NaturalPriestSkills.Best("heal", state.Level, state.Learned, skills);
		bool adjacent = state.TargetAdjacent || state.TargetDistance is float near && near <= MeleeReach;
		bool fighting = state.Aggro || state.TargetObjectId != null || state.NearbyAggressors > 0;
		var candidates = new List<NaturalCombatCandidate>();
		void Add(string action, ushort? skillId, int? target, bool legal, string[] reasons,
			bool needsNavigationCheck = false)
		{
			bool selected = chosen.Action == action && chosen.Skill?.Id == skillId &&
				(action != "cast-target" || chosen.TargetObjectId == target);
			string? rejected = selected ? null : !legal ? string.Join("; ", reasons) :
				$"Baseline selected {chosen.Action}/{chosen.Skill?.Id}: {chosen.Reason}";
			candidates.Add(new(action, skillId, target, legal, reasons, rejected, needsNavigationCheck));
		}
		void Simple(string action, bool legal, string reason, bool navigation = false) =>
			Add(action, null, state.TargetObjectId, legal, legal ? [] : [reason], navigation);

		Simple("revive", state.Dead, "Client did not report death.");
		Simple("blocked", !state.Dead && (state.MaxHp <= 0 || state.MaxMp <= 0 || state.Hp < 0 || state.Mp < 0),
			"Client life statistics are complete.");
		Simple("rest", !state.Dead && !fighting, "A fight or target is active.");
		Simple("retreat", !state.Dead && fighting && !state.Cornered,
			"No active fight or no checked escape from this corner.", navigation: true);
		Simple("shield-scroll", !state.Dead && state.ShieldScrollReady,
			"No owned, ready Anti-Shock tier at or below 50% HP, or a shield is active.");
		Simple("hot-potion", !state.Dead && state.HasHotPotion && state.HotPotionReady && !state.HotPotionActive,
			"Timed healing potion is absent, cooling down, or already active.");
		Simple("life-potion", !state.Dead && state.HasLifePotion && state.LifePotionReady,
			"Life potion is absent or cooling down.");
		Simple("mana-potion", !state.Dead && state.HasManaPotion && state.ManaPotionReady,
			"Mana potion is absent or cooling down.");
		Simple("attack", !state.Dead && state.TargetObjectId != null && adjacent,
			"No adjacent client-observed target.");
		Simple("approach", !state.Dead && state.TargetObjectId != null,
			"No client-observed target.", navigation: true);
		Simple("wait", !state.Dead, "Client reported death.");
		Simple("defend", !state.Dead && state.Aggro, "No client-observed aggression.");
		Simple("ready", !state.Dead && !fighting, "A fight or target is active.");

		foreach (NaturalPriestSkill skill in skills.OrderBy(skill => skill.Id))
		{
			string action = skill.TargetsSelf ? "cast-self" : "cast-target";
			int? target = action == "cast-target" ? state.TargetObjectId : null;
			var reasons = new List<string>();
			if (state.Dead) reasons.Add("Client reported death.");
			if (skill.IsPowderRest) reasons.Add("Powder rest skill: any hit cancels its 4 s cast, so only the rest policy casts it.");
			if (state.Dp < skill.DpCost) reasons.Add("Observed DP is below the skill's cost.");
			if (skill.Role == "rejuvenation" && state.HasRejuvenation != false)
				reasons.Add("The heal over time is already observed, or effects are unobserved.");
			if (state.Level < skill.MinimumLevel || !state.Learned.ContainsKey(skill.Id))
				reasons.Add("Skill is not in the client-observed learned list at this level.");
			if (action == "cast-target" && (target == null || state.TargetDistance == null))
				reasons.Add("No client-observed target position.");
			float distance = action == "cast-target" ? state.TargetDistance ?? float.MaxValue : 0;
			bool inRange = distance <= skill.Range || skill.Range <= MeleeReach && adjacent;
			if (!inRange) reasons.Add("Target is outside the skill's observed range.");
			if (state.Cooldowns.TryGetValue(skill.CooldownId, out DateTimeOffset until) && until > now)
				reasons.Add("Client-observed cooldown is active.");
			int reserve = (action == "cast-target" || skill.Role == "blessing") && heal != null
				? heal.ManaCost + policy.ManaReserveExtra : 0;
			if (state.Mp < skill.ManaCost + reserve) reasons.Add("Insufficient observed mana after healing reserve.");
			if (skill.RequiresChainCategory != null && !(skill.RequiresChainCategory == state.OpenChainCategory &&
				state.OpenChainTargetId == target && state.ChainExpiresAt > now))
				reasons.Add("Required client-observed chain is not open.");
			if (skill.Role == "blessing" && state.HasBlessing == true)
				reasons.Add("Protection buff is already observed.");
			Add(action, skill.Id, target, reasons.Count == 0, reasons.ToArray());
			if (reasons.Count == 0 && NaturalPriestSkills.Best(skill.Role, state.Level, state.Learned, skills)?.Id != skill.Id)
				candidates[^1] = candidates[^1] with { BaselineRejection = "Baseline prefers a higher learned rank for this role." };
		}
		return candidates.ToArray();
	}

	public static NaturalCombatChoice Decide(NaturalCombatObservation state, DateTimeOffset now,
		IEnumerable<NaturalPriestSkill>? catalog = null, NaturalMauPolicyParameters? parameters = null)
	{
		NaturalMauPolicyParameters policy = parameters ?? NaturalMauPolicyParameters.Baseline;
		policy.Validate();
		var checks = new List<NaturalDecisionCheck>();
		if (state.Dead) return Choice("revive", null, "Client reported death.");
		if (state.MaxHp <= 0 || state.MaxMp <= 0 || state.Hp < 0 || state.Mp < 0)
			return Choice("blocked", null, "Client life statistics are incomplete.");
		NaturalPriestSkill? heal = NaturalPriestSkills.Best("heal", state.Level, state.Learned, catalog);
		bool fighting = state.Aggro || state.TargetObjectId != null || state.NearbyAggressors > 0;
		bool adjacent = state.TargetAdjacent || state.TargetDistance is float near && near <= MeleeReach;
		if (!state.Cornered && state.NearbyAggressors >= SwarmedAttackers)
			return Retreat($"{state.NearbyAggressors} client-observed attackers; disengage from the whole pack.");
		if (!fighting && state.Mp * 100 < state.MaxMp * 50 &&
			!(state.Hp * 100 <= state.MaxHp * 25 && state.HasLifePotion && state.LifePotionReady) &&
			!(state.Mp < (heal?.ManaCost ?? 0) + 10 && state.HasManaPotion && state.ManaPotionReady))
			return Choice("rest", null, "Between fights, mana is below 50%; recover before spending more on healing.");
		// Low HP: heal through it while heals and potions last, as a player does against two monsters (the
		// recorded human run kept chaining Healing Light down to 24% and won). Retreat when nothing is left
		// to heal with. Cornered: no checked
		// escape leads away from the pack, so fight it out regardless.
		// NA-18: the Cleric's Salvation (instant, 50% MP then 50% HP) is the emergency heal whenever observed DP
		// pays for it; it goes before potions and Healing Light.
		NaturalPriestSkill? salvation = NaturalPriestSkills.Best("salvation", state.Level, state.Learned, catalog);
		bool salvationReady = salvation != null && fighting && state.Dp >= salvation.DpCost &&
			Eligible(salvation, null, 0, state, now, reserveHeal: false);
		if (!state.Cornered && fighting && state.Hp * 100 <= state.MaxHp * 30)
		{
			bool canHeal = heal != null && Eligible(heal, state.TargetObjectId, 0, state, now, reserveHeal: false) || salvationReady;
			bool canPotion = state.HasLifePotion && state.LifePotionReady ||
				state.HasHotPotion && state.HotPotionReady && !state.HotPotionActive;
			if (!canHeal && !canPotion)
				return Retreat("HP is at or below 30% and no self-heal or potion is available.");
		}
		int healPercent = state.InEmergency ? 100 : state.NearbyAggressors >= 2
			? policy.HealMultiplePercent : policy.HealSinglePercent;
		bool urgent = fighting && state.Hp * 100 <= state.MaxHp * healPercent;
		bool critical = state.Hp * 100 <= state.MaxHp * 25;
		// NA-19: the Anti-Shock shield at 50% HP comes after the 30% retreat rule and before Salvation (which spends
		// DP) and the potions; it is an item, so it never interrupts a cast (the caller only offers it between casts).
		if (fighting && state.ShieldScrollReady)
			return Choice("shield-scroll", null, "HP is at or below 50% in a fight: use the owned Anti-Shock damage shield.");
		if (salvationReady && (state.InEmergency || critical))
			return Choice("cast-self", salvation, $"Emergency with {state.Dp} observed DP: Salvation restores half of MP and HP at once.");
		// A selected target can make this a "fight" before any monster has attacked.
		// Earlier-than-baseline potion tuning applies only after an attacker is observed.
		int hotPotionPercent = state.NearbyAggressors > 0 ? policy.HotPotionPercent
			: Math.Min(policy.HotPotionPercent, NaturalMauPolicyParameters.Baseline.HotPotionPercent);
		// At a healthy Priest and one attacker, a target already below 60% HP may
		// finish before extra early healing matters. Keep the baseline timing there.
		if (hotPotionPercent > NaturalMauPolicyParameters.Baseline.HotPotionPercent &&
			state.NearbyAggressors == 1 && state.TargetHpPercent is < 60)
			hotPotionPercent = NaturalMauPolicyParameters.Baseline.HotPotionPercent;
		// Infernal Blaze is the learned instant stun. At HP above the baseline potion
		// threshold, cast that ready control skill before spending the tuned early potion.
		if (hotPotionPercent > NaturalMauPolicyParameters.Baseline.HotPotionPercent &&
			state.Hp * 100 > state.MaxHp * NaturalMauPolicyParameters.Baseline.HotPotionPercent &&
			adjacent && state.TargetObjectId is int potionTarget && state.TargetDistance is float potionDistance)
		{
			NaturalPriestSkill? infernal = NaturalPriestSkills.Best("infernal", state.Level, state.Learned, catalog);
			if (infernal != null && Eligible(infernal, potionTarget, potionDistance,
				state, now, reserveHeal: true))
				hotPotionPercent = NaturalMauPolicyParameters.Baseline.HotPotionPercent;
		}
		if (fighting && state.Hp * 100 <= state.MaxHp * hotPotionPercent &&
			state.HasHotPotion && state.HotPotionReady && !state.HotPotionActive)
			return Choice("hot-potion", null,
				$"HP is at or below {hotPotionPercent}% in a fight; apply owned timed healing before the self-heal threshold.");
		if (urgent && !state.InEmergency && state.HasHealedThisFight &&
			state.TargetHpPercent is > 0 && state.TargetHpPercent <= policy.FinishTargetHpPercent &&
			state.TargetObjectId is int finishingTarget && state.TargetDistance is float finishingDistance)
		{
			NaturalPriestSkill? finisher = NaturalPriestSkills.Best("smite", state.Level, state.Learned, catalog);
			if (finisher != null && Eligible(finisher, finishingTarget, finishingDistance,
				state, now, reserveHeal: true))
				return Choice("cast-target", finisher,
					$"Client-observed target is at or below {policy.FinishTargetHpPercent}% HP after this fight already received a self-heal.");
		}
		if (urgent && heal != null && Eligible(heal, state.TargetObjectId, 0, state, now, reserveHeal: false))
			return Choice("cast-self", heal, state.InEmergency
				? $"Emergency: HP fell to {EmergencyEnterPercent(state.NearbyAggressors, state.TargetSeasoned)}% and has not recovered to {EmergencyExitPercent(state.NearbyAggressors, state.TargetSeasoned)}%."
				: $"HP is at or below {healPercent}% during a client-observed fight with {state.NearbyAggressors} attackers.");
		if (critical && state.HasLifePotion && state.LifePotionReady)
			return Choice("life-potion", null, "Critical HP and self-heal is unavailable; consume an owned life potion.");
		if (state.Mp < (heal?.ManaCost ?? 0) + 10 && state.HasManaPotion && state.ManaPotionReady)
			return Choice("mana-potion", null, "Mana is below the healing reserve; consume an owned mana potion.");
		if (critical && state.Aggro && !state.Cornered)
			return Retreat("Critical HP and no legal self-heal; leave the aggressor.");
		// NA-18: keep the Cleric's heal over time (Light of Rejuvenation, 450 HP over 30 s) up while being hit.
		NaturalPriestSkill? rejuvenation = NaturalPriestSkills.Best("rejuvenation", state.Level, state.Learned, catalog);
		if (state.Aggro && state.HasRejuvenation == false && rejuvenation != null &&
			Eligible(rejuvenation, null, 0, state, now, reserveHeal: true))
			return Choice("cast-self", rejuvenation, "Under attack without the observed heal over time; keep it up.");
		NaturalPriestSkill? blessing = NaturalPriestSkills.Best("blessing", state.Level, state.Learned, catalog);
		if (!state.Aggro && state.TargetObjectId == null && state.HasBlessing == false && blessing != null &&
			state.Mp >= blessing.ManaCost + (heal?.ManaCost ?? 0) &&
			Eligible(blessing, null, 0, state, now, reserveHeal: true))
			return Choice("cast-self", blessing, "Learned one-hour protection buff is absent from the observed effects.");
		if (state.TargetObjectId is not int target || state.TargetDistance is not float distance)
		{
			if (state.Aggro) return Choice("defend", null, "Aggression observed without a target; reacquire before pulling.");
			if (state.Hp * 100 < state.MaxHp * 90 && heal != null &&
				Eligible(heal, null, 0, state, now, reserveHeal: false))
				return Choice("cast-self", heal, "Between fights, heal HP while mana remains above 50%.");
			return Choice("ready", null, "Mana is above 50%; no sit is needed between fights.");
		}
		if (distance > 25 && !adjacent)
			return Choice("approach", null, "Target is outside Priest spell range.");
		// The rotation. Adjacent: instants first (they cannot be interrupted and each buys time: the stun, the
		// slow), then Smite. At range: Smite is the pull and the filler while the monster closes.
		// NA-18, the Cleric: every other _1TH opener resets an open Smite chain (Java ChainCondition.shouldReset), so
		// while Flashbolt is ready Smite opens first and Flashbolt follows at once; then Slashing Wind and Earth's
		// Wrath (a 1.5 s cast, last at melee where a hit can cancel it). The Priest catalog has none of these roles.
		var rotation = new List<string> { "followup" };
		NaturalPriestSkill? followup = NaturalPriestSkills.Best("followup", state.Level, state.Learned, catalog);
		NaturalPriestSkill? opener = NaturalPriestSkills.Best("smite", state.Level, state.Learned, catalog);
		if (followup != null && opener != null && followup.RequiresChainCategory == opener.ChainCategory &&
			!(state.Cooldowns.TryGetValue(followup.CooldownId, out DateTimeOffset followupReadyAt) && followupReadyAt > now) &&
			state.Mp >= opener.ManaCost + followup.ManaCost + (heal?.ManaCost ?? 0) + policy.ManaReserveExtra)
			rotation.Add("smite");
		rotation.AddRange(adjacent ? ["infernal", "hallowed", "wind", "wrath", "smite"] : ["wrath", "wind", "smite"]);
		foreach (string role in rotation)
		{
			NaturalPriestSkill? skill = NaturalPriestSkills.Best(role, state.Level, state.Learned, catalog);
			if (skill != null && Eligible(skill, target, distance, state, now, reserveHeal: true))
				return Choice("cast-target", skill, adjacent
					? $"Learned {role} is ready at melee and leaves healing mana reserved."
					: $"Learned {role} is in range, ready, and leaves healing mana reserved.");
		}
		if (!adjacent && state.TargetRanged && state.ConservativeRangedHold &&
			(distance <= 12 || state.NearbyAggressors >= 2))
			return Choice("wait", null,
				"Ranged target is in spell range or multiple attackers are present; hold checked ground until Smite is ready.");
		if (!adjacent && state.TargetRanged)
			return Choice("approach", null, "Nothing ready at range and the target attacks from range, so it will not close: walk up to it.");
		if (!adjacent)
			return Choice("wait", null, "Nothing ready at range; the pulled monster is closing, so hold position.");
		return Choice("attack", null, "No skill is ready at melee; swing the mace.");

		NaturalCombatChoice Choice(string action, NaturalPriestSkill? skill, string reason) =>
			new(action, skill, state.TargetObjectId, reason, checks.ToArray());

		// NA-18: a Cleric roots the monster it is leaving (instant, about 10 s) before retreating; Root is on its
		// own cooldown afterwards, so the next decision retreats.
		NaturalCombatChoice Retreat(string reason)
		{
			NaturalPriestSkill? root = NaturalPriestSkills.Best("root", state.Level, state.Learned, catalog);
			if (root != null && state.TargetObjectId is int rootTarget && state.TargetDistance is float rootDistance &&
				Eligible(root, rootTarget, rootDistance, state, now, reserveHeal: true))
				return Choice("cast-target", root, "Root the target before retreating: " + reason);
			return Choice("retreat", null, reason);
		}

		bool Eligible(NaturalPriestSkill skill, int? candidateTarget, float range,
			NaturalCombatObservation observed, DateTimeOffset instant, bool reserveHeal)
		{
			// A melee skill's template range (1 m) is measured by the server from bound radius to bound radius;
			// a monster that is on the bot is in reach whatever the client's lagging distance says.
			bool inRange = range <= skill.Range || skill.Range <= MeleeReach && adjacent;
			bool ready = !observed.Cooldowns.TryGetValue(skill.CooldownId, out var until) || until <= instant;
			int reserve = reserveHeal && heal != null ? heal.ManaCost + policy.ManaReserveExtra : 0;
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
