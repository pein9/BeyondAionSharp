namespace Aion.Bots.Scenarios.Classes;

/// <summary>What a step of the recovery ladder uses.</summary>
public enum NaturalRecoveryKind
{
	/// <summary>The Anti-Shock scroll the help-item policy chose (<see cref="NaturalCombatObservation.ShieldScrollReady"/>).</summary>
	ShieldScroll,
	/// <summary>The owned life potion, when it is ready and its healing is not already running.</summary>
	LifePotion,
	/// <summary>The best learned skill of the step's role, cast on the bot.</summary>
	Skill,
}

/// <summary>One step of the recovery ladder: used in a fight when HP is at or below <paramref name="HpPercent"/>.</summary>
/// <param name="Role">The skill role of a <see cref="NaturalRecoveryKind.Skill"/> step.</param>
public sealed record NaturalRecoveryStep(NaturalRecoveryKind Kind, int HpPercent, string? Role = null);

/// <summary>A buff the rotation keeps up: the best learned skill of the role, cast when the client's effect list is
/// observed without it.</summary>
/// <param name="DuringFight">Also cast in a fight; otherwise only before a target is hit.</param>
public sealed record NaturalRotationUpkeep(string Role, bool DuringFight = false);

/// <summary>When the weapon swings.</summary>
public enum NaturalAutoAttack
{
	/// <summary>Whenever no skill is ready and the target is in the weapon's reach.</summary>
	Filler,
	/// <summary>Only when no attack skill can be paid for; while one is merely cooling down the class waits.</summary>
	LastResort,
}

/// <summary>
/// CP-36: one class's fight as a table (docs/natural-class-profiles.md). Every role names skills of the profile's catalog.
/// </summary>
/// <param name="Id">The table's version, written into every combat-decision record.</param>
/// <param name="Adjacent">The attack roles in cast order while the target is on the bot.</param>
/// <param name="AtRange">The attack roles in cast order while it is not.</param>
/// <param name="Upkeep">The buffs kept up, in the order they are checked.</param>
/// <param name="Recovery">The recovery ladder, in the order its steps are tried; empty for a class with nothing to recover with.</param>
/// <param name="SwarmAttackers">Attackers at which the class leaves regardless of HP.</param>
/// <param name="FleeHpPercent">HP at or below which the class leaves when no ladder step is available.</param>
/// <param name="ControlRole">A skill cast on the target before a retreat; null for none.</param>
/// <param name="EmergencyPercent">HP at or below which the fight is an emergency: recover only, every ladder step at once.</param>
/// <param name="EmergencyClearPercent">HP at or above which the emergency is over.</param>
/// <param name="OnlyWhenHurt">CP-43: attack roles that are cast only while HP is at or below the percentage given, in
/// their place in the list (the Warrior's Rage, a chain step that shields it). Null for none.</param>
/// <param name="HoldOpenChain">CP-48: while a follow-up of the open chain only cools down and clears inside its chain time,
/// nothing else is cast or swung, and outside an emergency no recovery skill is cast while a follow-up is ready or so
/// awaited (the Engineer's Rapidfire, twice inside 2 s each: a Direct Shot or a Bullet Resistance between resets it).</param>
public sealed record NaturalRotationRules(string Id, IReadOnlyList<string> Adjacent, IReadOnlyList<string> AtRange,
	IReadOnlyList<NaturalRotationUpkeep> Upkeep, IReadOnlyList<NaturalRecoveryStep> Recovery, int SwarmAttackers, int FleeHpPercent,
	NaturalAutoAttack AutoAttack, string? ControlRole = null, int EmergencyPercent = 35, int EmergencyClearPercent = 45,
	IReadOnlyDictionary<string, int>? OnlyWhenHurt = null, bool HoldOpenChain = false)
{
	/// <summary>The table's two attack lists as lines of skill ids, every rank of a role in level order, for
	/// <see cref="NaturalProfileValidator"/>.</summary>
	public IEnumerable<IReadOnlyList<int>> Lines(IReadOnlyList<NaturalPriestSkill> catalog) => new[] { Adjacent, AtRange }
		.Select(list => (IReadOnlyList<int>)list.SelectMany(role => catalog.Where(skill => skill.Role == role)
			.OrderBy(skill => skill.MinimumLevel).ThenBy(skill => skill.Id).Select(skill => (int)skill.Id)).ToArray());
}

/// <summary>
/// CP-36: the fight decision of a class whose profile is a rule table. Pure and deterministic, like the Priest's policy,
/// and read by the same fight loop. The Priest, the Cleric and the Chanter do not use it.
/// <para>
/// Order of one decision: death and incomplete life statistics; the swarm limit; the recovery ladder; the flee limit;
/// the mana potion; with no target, upkeep or ready; with one, a ready follow-up before anything else (any non-chain
/// cast, and another chain's first step, resets the open chain: Java Skill.canUseSkill, ChainCondition.shouldReset), the
/// held chain's wait (<see cref="NaturalRotationRules.HoldOpenChain"/>), the in-fight upkeep, the attack list for the
/// target's place, the weapon, a wait for a listed attack that is in reach and only cooling down, and last a movement
/// answer by the pull style.
/// A follow-up is legal when its required category is the current or the previous chain category (Java
/// ChainCondition.validate accepts either) on the same target, inside its own chain time (CP-Q17).
/// </para>
/// </summary>
public sealed class NaturalRotationCombatPolicy : INaturalCombatPolicy
{
	private const string CoolingDown = "Client-observed cooldown is active.";

	private readonly NaturalRotationRules rules;
	private readonly NaturalPriestSkill[] catalog;
	private readonly NaturalFightMovement movement;

	public NaturalRotationCombatPolicy(NaturalRotationRules rules, IEnumerable<NaturalPriestSkill> catalog, NaturalFightMovement movement)
	{
		ArgumentNullException.ThrowIfNull(rules);
		ArgumentNullException.ThrowIfNull(catalog);
		ArgumentNullException.ThrowIfNull(movement);
		this.rules = rules;
		this.catalog = catalog.ToArray();
		this.movement = movement;
		IEnumerable<string> named = rules.Adjacent.Concat(rules.AtRange).Concat(rules.Upkeep.Select(upkeep => upkeep.Role))
			.Concat(rules.OnlyWhenHurt?.Keys ?? [])
			.Concat(rules.Recovery.Where(step => step.Kind == NaturalRecoveryKind.Skill).Select(step => step.Role ?? ""))
			.Concat(rules.ControlRole == null ? [] : [rules.ControlRole]);
		foreach (string role in named.Distinct())
			if (!this.catalog.Any(skill => skill.Role == role))
				throw new InvalidDataException($"Rotation table {rules.Id} names the role '{role}', which its catalog does not hold.");
		if (rules.EmergencyClearPercent < rules.EmergencyPercent || rules.SwarmAttackers < 1)
			throw new InvalidDataException($"Rotation table {rules.Id} has an emergency or swarm limit that cannot be met.");
	}

	public string PolicyVersion(NaturalMauPolicyParameters parameters) => $"{rules.Id}:{parameters.Id}";

	/// <summary>CP-56a: attackers at which the class leaves; a walk-in approach takes on no pack that reaches it.</summary>
	public int SwarmAttackers => rules.SwarmAttackers;

	public int EmergencyEnterPercent(int attackers, bool targetSeasoned) => rules.EmergencyPercent;

	public int EmergencyExitPercent(int attackers, bool targetSeasoned) => rules.EmergencyClearPercent;

	public NaturalCombatChoice Decide(NaturalCombatObservation state, DateTimeOffset now, NaturalMauPolicyParameters parameters)
	{
		ArgumentNullException.ThrowIfNull(state);
		parameters.Validate();
		var checks = new List<NaturalDecisionCheck>();
		NaturalCombatChoice Choice(string action, NaturalPriestSkill? skill, string reason) =>
			new(action, skill, state.TargetObjectId, reason, checks.ToArray());
		bool Ready(NaturalPriestSkill skill)
		{
			string[] reasons = Refusals(skill, state, now, parameters);
			checks.Add(new($"skill-{skill.Id}", reasons.Length == 0 ? "pass" : "skip", reasons.Length == 0 ? "Legal." : string.Join(" ", reasons)));
			return reasons.Length == 0;
		}
		NaturalCombatChoice Cast(NaturalPriestSkill skill, string reason) => Choice(Self(skill) ? "cast-self" : "cast-target", skill, reason);
		NaturalCombatChoice Retreat(string reason)
		{
			if (rules.ControlRole != null && Best(rules.ControlRole, state) is { } control && state.TargetObjectId != null && Ready(control))
				return Cast(control, "Hold the target before retreating: " + reason);
			return Choice("retreat", null, reason);
		}

		// CP-48: a follow-up of the open chain is ready, or only cools down and clears inside its chain time.
		bool Pending(NaturalPriestSkill skill)
		{
			if (skill.RequiresChainCategory == null) return false;
			string[] reasons = Refusals(skill, state, now, parameters);
			return reasons.Length == 0 || reasons is [CoolingDown] && ClearsInsideChain(skill, state);
		}
		bool ChainHeld() => rules.HoldOpenChain &&
			catalog.Any(skill => skill.RequiresChainCategory != null && Best(skill.Role, state)?.Id == skill.Id && Pending(skill));

		if (state.Dead) return Choice("revive", null, "Client reported death.");
		if (state.MaxHp <= 0 || state.MaxMp <= 0 || state.Hp < 0 || state.Mp < 0)
			return Choice("blocked", null, "Client life statistics are incomplete.");
		bool fighting = Fighting(state);
		bool HpAtOrBelow(int percent) => state.Hp * 100 <= state.MaxHp * percent;
		if (!state.Cornered && state.NearbyAggressors >= rules.SwarmAttackers)
			return Retreat($"{state.NearbyAggressors} client-observed attackers; disengage from the whole pack.");
		if (fighting)
		{
			foreach (NaturalRecoveryStep step in rules.Recovery)
			{
				if (!state.InEmergency && !HpAtOrBelow(step.HpPercent)) continue;
				string at = state.InEmergency ? "Emergency" : $"HP is at or below {step.HpPercent}%";
				switch (step.Kind)
				{
					case NaturalRecoveryKind.ShieldScroll when state.ShieldScrollReady:
						return Choice("shield-scroll", null, $"{at}: use the owned Anti-Shock damage shield.");
					case NaturalRecoveryKind.LifePotion when LifePotionUsable(state):
						return Choice("hot-potion", null, $"{at}: drink the owned life potion.");
					case NaturalRecoveryKind.Skill when (state.InEmergency || !ChainHeld()) && Best(step.Role!, state) is { } recovery && Ready(recovery):
						return Cast(recovery, $"{at}: {step.Role}.");
				}
			}
			// Nothing above was available at its threshold. At the flee limit the class leaves; cornered, it fights on.
			if (!state.Cornered && HpAtOrBelow(rules.FleeHpPercent))
				return Retreat($"HP is at or below {rules.FleeHpPercent}% and nothing is left to recover with.");
		}
		if (ManaShort(state) && state.HasManaPotion && state.ManaPotionReady)
			return Choice("mana-potion", null, "Mana is below the cheapest attack; consume an owned mana potion.");
		if (state.TargetObjectId is not int || state.TargetDistance is not float distance)
		{
			if (state.Aggro || state.NearbyAggressors > 0) return Choice("defend", null, "Aggression observed without a target; reacquire before pulling.");
			if (Upkeep(duringFight: false) is { } buff) return Cast(buff, $"The {buff.Role} buff is absent from the observed effects.");
			return Choice("ready", null, "No target; nothing to keep up.");
		}
		bool adjacent = Adjacent(state);
		IReadOnlyList<string> list = adjacent ? rules.Adjacent : rules.AtRange;
		// A role that is cast only when hurt is left out of the line while HP is above its percentage.
		NaturalPriestSkill[] line = list
			.Where(role => rules.OnlyWhenHurt == null || !rules.OnlyWhenHurt.TryGetValue(role, out int percent) || HpAtOrBelow(percent))
			.Select(role => Best(role, state)).OfType<NaturalPriestSkill>().ToArray();
		foreach (NaturalPriestSkill followUp in line.Where(skill => skill.RequiresChainCategory != null))
			if (Ready(followUp)) return Cast(followUp, $"The {followUp.Role} follow-up is open; cast it before the chain resets.");
		if (rules.HoldOpenChain && line.Any(Pending))
			return Choice("wait", null, "An open follow-up only cools down and clears inside its chain time; hold the chain for it.");
		if (!state.Aggro && Upkeep(duringFight: false) is { } before) return Cast(before, $"The {before.Role} buff goes up before the first hit.");
		if (Upkeep(duringFight: true) is { } during) return Cast(during, $"The {during.Role} buff is absent from the observed effects.");
		foreach (NaturalPriestSkill attack in line.Where(skill => skill.RequiresChainCategory == null))
			if (Ready(attack)) return Cast(attack, adjacent ? $"Learned {attack.Role} is ready at melee." : $"Learned {attack.Role} is in reach and ready.");
		bool swingLegal = InWeaponReach(state, distance, adjacent);
		// A last-resort swing waits while a listed attack only cools down and could be paid for.
		bool swing = swingLegal && (rules.AutoAttack == NaturalAutoAttack.Filler ||
			!line.Any(skill => skill.RequiresChainCategory == null && state.Mp >= skill.ManaCost + Reserve(state, parameters)));
		if (swing) return Choice("attack", null, "No skill is ready; swing the weapon.");
		// CP-47: a listed attack that reaches the target and only cools down is waited for where the class stands (the
		// Artist's Pulse, 2 s). Going to the target is for an attack that cannot be cast from here at all.
		if (line.Any(skill => skill.RequiresChainCategory == null && Refusals(skill, state, now, parameters) is [CoolingDown]))
			return Choice("wait", null, "A listed attack is in reach and only cools down; hold position.");
		// Nothing can be cast or swung from here. An unpulled target never closes by itself, so the class goes to it.
		if (!state.Aggro) return Choice("approach", null, "Nothing reaches the unpulled target from here: go to it.");
		bool holds = movement.Style switch
		{
			NaturalPullStyle.WalkIn => adjacent,
			NaturalPullStyle.WeaponRangeStandOff => swingLegal,
			_ => !state.TargetRanged,
		};
		return holds
			? Choice("wait", null, "Nothing is ready; hold position until a cooldown clears.")
			: Choice("approach", null, "Nothing is ready and the target is out of reach: close in.");

		NaturalPriestSkill? Upkeep(bool duringFight)
		{
			if (state.ActiveEffectSkillIds is not { } active) return null;
			foreach (NaturalRotationUpkeep upkeep in rules.Upkeep.Where(upkeep => upkeep.DuringFight == duringFight))
			{
				if (catalog.Any(skill => skill.Role == upkeep.Role && active.Contains(skill.Id))) continue;
				if (Best(upkeep.Role, state) is { } buff && Ready(buff)) return buff;
			}
			return null;
		}
	}

	public NaturalCombatCandidate[] CandidateActions(NaturalCombatObservation state, DateTimeOffset now, NaturalCombatChoice chosen,
		NaturalMauPolicyParameters parameters)
	{
		ArgumentNullException.ThrowIfNull(state);
		ArgumentNullException.ThrowIfNull(chosen);
		parameters.Validate();
		bool fighting = Fighting(state);
		bool adjacent = Adjacent(state);
		var candidates = new List<NaturalCombatCandidate>();
		void Add(string action, ushort? skillId, int? target, bool legal, string[] reasons, bool needsNavigationCheck = false)
		{
			bool selected = chosen.Action == action && chosen.Skill?.Id == skillId && (action != "cast-target" || chosen.TargetObjectId == target);
			string? rejected = selected ? null : !legal ? string.Join("; ", reasons)
				: $"Table {rules.Id} selected {chosen.Action}/{chosen.Skill?.Id}: {chosen.Reason}";
			candidates.Add(new(action, skillId, target, legal, reasons, rejected, needsNavigationCheck));
		}
		void Simple(string action, bool legal, string reason, bool navigation = false) =>
			Add(action, null, state.TargetObjectId, legal, legal ? [] : [reason], navigation);

		Simple("revive", state.Dead, "Client did not report death.");
		Simple("blocked", !state.Dead && (state.MaxHp <= 0 || state.MaxMp <= 0 || state.Hp < 0 || state.Mp < 0), "Client life statistics are complete.");
		Simple("retreat", !state.Dead && fighting && !state.Cornered, "No active fight or no checked escape from this corner.", navigation: true);
		Simple("shield-scroll", !state.Dead && state.ShieldScrollReady, "No owned, ready Anti-Shock tier, or a shield is active.");
		Simple("hot-potion", !state.Dead && LifePotionUsable(state), "Life potion is absent, cooling down, or already healing.");
		Simple("mana-potion", !state.Dead && state.HasManaPotion && state.ManaPotionReady, "Mana potion is absent or cooling down.");
		Simple("attack", !state.Dead && state.TargetObjectId != null && state.TargetDistance is float swingDistance &&
			InWeaponReach(state, swingDistance, adjacent), "No client-observed target in the weapon's reach.");
		Simple("approach", !state.Dead && state.TargetObjectId != null, "No client-observed target.", navigation: true);
		Simple("wait", !state.Dead, "Client reported death.");
		Simple("defend", !state.Dead && (state.Aggro || state.NearbyAggressors > 0), "No client-observed aggression.");
		Simple("ready", !state.Dead && !fighting, "A fight or target is active.");
		foreach (NaturalPriestSkill skill in catalog.OrderBy(skill => skill.Id))
		{
			string action = Self(skill) ? "cast-self" : "cast-target";
			string[] reasons = Refusals(skill, state, now, parameters);
			Add(action, skill.Id, action == "cast-target" ? state.TargetObjectId : null, reasons.Length == 0, reasons);
			if (reasons.Length == 0 && Best(skill.Role, state)?.Id != skill.Id)
				candidates[^1] = candidates[^1] with { BaselineRejection = "The table prefers a higher learned rank for this role." };
		}
		return candidates.ToArray();
	}

	/// <summary>Why the skill cannot be cast now; empty when it can. Decide and CandidateActions both read this.</summary>
	private string[] Refusals(NaturalPriestSkill skill, NaturalCombatObservation state, DateTimeOffset now, NaturalMauPolicyParameters parameters)
	{
		var reasons = new List<string>();
		bool self = Self(skill);
		int? target = self ? null : state.TargetObjectId;
		if (state.Dead) reasons.Add("Client reported death.");
		if (state.Level < skill.MinimumLevel || !state.Learned.ContainsKey(skill.Id))
			reasons.Add("Skill is not in the client-observed learned list at this level.");
		if (skill.CounterStatus != null) reasons.Add($"Counter skill: it needs a {skill.CounterStatus} the bot does not observe.");
		if (skill.Activation == "CHARGE") reasons.Add("Charge skill: the fight loop holds no charge.");
		if (skill.OutOfCombatOnly && Fighting(state)) reasons.Add("The skill cannot be cast in combat.");
		if (!self)
		{
			if (target == null || state.TargetDistance is not float distance) reasons.Add("No client-observed target position.");
			// A melee skill's reach is measured by the server from bound radius to bound radius; a monster that is on the
			// bot is in reach whatever the client's lagging distance says.
			else if (!(distance <= NaturalSkillCatalog.Reach(skill, state) ||
				NaturalSkillCatalog.Reach(skill, state) <= movement.MeleeReach && Adjacent(state)))
				reasons.Add("Target is outside the skill's reach.");
		}
		if (state.Cooldowns.TryGetValue(skill.CooldownId, out DateTimeOffset until) && until > now) reasons.Add(CoolingDown);
		if (state.Dp < skill.DpCost) reasons.Add("Observed DP is below the skill's cost.");
		// The reserve is kept for a recovery skill; a recovery skill itself spends it.
		int reserve = rules.Recovery.Any(step => step.Role == skill.Role) ? 0 : Reserve(state, parameters);
		if (state.Mp < skill.ManaCost + reserve) reasons.Add(reserve > 0 ? "Insufficient observed mana after the recovery reserve." : "Insufficient observed mana.");
		if (skill.RequiresChainCategory != null && !ChainOpen(skill, self, state, now)) reasons.Add("Required client-observed chain is not open.");
		return reasons.ToArray();
	}

	/// <summary>
	/// The follow-up's step is open: its required category is the current chain category or the one before it, inside the
	/// follow-up's own chain time counted from the step before it; one cast on a target follows only on the target the
	/// chain was opened on, and one cast on the bot follows on any (the Warrior's Rage). A follow-up that is itself the
	/// current step may repeat only while its self count allows.
	/// </summary>
	private static bool ChainOpen(NaturalPriestSkill skill, bool self, NaturalCombatObservation state, DateTimeOffset now)
	{
		if (state.OpenChainCategory == null || !self && state.OpenChainTargetId != state.TargetObjectId) return false;
		bool repeat = skill.ChainCategory == state.OpenChainCategory;
		if (repeat && (state.OpenChainUseCount ?? 1) >= Math.Max(1, skill.SelfCount)) return false;
		bool category = repeat ? skill.RequiresChainCategory == state.PreviousChainCategory
			: skill.RequiresChainCategory == state.OpenChainCategory || skill.RequiresChainCategory == state.PreviousChainCategory;
		if (!category) return false;
		if (skill.ChainWindowMillis <= 0) return state.ChainExpiresAt is not DateTimeOffset expires || expires > now;
		return state.ChainStepAt is DateTimeOffset at ? now <= at.AddMilliseconds(skill.ChainWindowMillis) : state.ChainExpiresAt > now;
	}

	/// <summary>The skill's cooldown ends no later than its step of the open chain does: the follow-up's own chain time
	/// counted from the step before it, or the observed expiry when the step's time is not known.</summary>
	private static bool ClearsInsideChain(NaturalPriestSkill skill, NaturalCombatObservation state)
	{
		DateTimeOffset? deadline = skill.ChainWindowMillis > 0 && state.ChainStepAt is DateTimeOffset at
			? at.AddMilliseconds(skill.ChainWindowMillis) : state.ChainExpiresAt;
		return deadline is DateTimeOffset end && state.Cooldowns.TryGetValue(skill.CooldownId, out DateTimeOffset until) && until <= end;
	}

	/// <summary>The mana kept back from attacks: the best learned recovery skill's cost and the run's extra. A class with no
	/// learned recovery skill keeps nothing back.</summary>
	private int Reserve(NaturalCombatObservation state, NaturalMauPolicyParameters parameters)
	{
		NaturalPriestSkill? recovery = rules.Recovery.Where(step => step.Kind == NaturalRecoveryKind.Skill)
			.Select(step => Best(step.Role!, state)).OfType<NaturalPriestSkill>().FirstOrDefault();
		return recovery == null ? 0 : recovery.ManaCost + parameters.ManaReserveExtra;
	}

	/// <summary>The class has attacks that cost mana and cannot pay for the cheapest learned one.</summary>
	private bool ManaShort(NaturalCombatObservation state)
	{
		int[] costs = rules.Adjacent.Concat(rules.AtRange).Distinct().Select(role => Best(role, state)).OfType<NaturalPriestSkill>()
			.Where(skill => skill.ManaCost > 0).Select(skill => skill.ManaCost).ToArray();
		return costs.Length > 0 && state.Mp < costs.Min();
	}

	private NaturalPriestSkill? Best(string role, NaturalCombatObservation state) =>
		NaturalPriestSkills.Best(role, state.Level, state.Learned, catalog);

	private bool Adjacent(NaturalCombatObservation state) =>
		state.TargetAdjacent || state.TargetDistance is float near && near <= movement.MeleeReach;

	/// <summary>The weapon swings at a target on the bot, or within the main-hand weapon's observed attack range.</summary>
	private static bool InWeaponReach(NaturalCombatObservation state, float distance, bool adjacent) =>
		adjacent || state.WeaponAttackRangeMillis is int range && distance <= range / 1000f;

	private static bool Fighting(NaturalCombatObservation state) => state.Aggro || state.TargetObjectId != null || state.NearbyAggressors > 0;

	private static bool LifePotionUsable(NaturalCombatObservation state) => state.HasHotPotion && state.HotPotionReady && !state.HotPotionActive;

	/// <summary>The skill is cast on the bot: the template's first target is the caster, or a friend or the caster; a
	/// hand-typed row says it by its role.</summary>
	private static bool Self(NaturalPriestSkill skill) => skill.TargetKind == null ? skill.TargetsSelf : skill.TargetKind is "ME" or "TARGETORME";
}
