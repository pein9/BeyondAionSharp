using Aion.Bots.World;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>What the client observes between two fights, and what the rest has done so far.</summary>
/// <param name="RecoveringMana">The mana sit is on: set below the sit threshold, cleared at the sit target.</param>
/// <param name="QuietSits">Sits of this rest that no monster interrupted.</param>
/// <param name="LastPowderSkillId">The powder skill cast last in this rest, so the two alternate.</param>
/// <param name="LifePotionOwned">CP-37, read by the potion plan only: a life potion is in the bag.</param>
/// <param name="LifePotionReady">Its use delay has run out (the 30 s of use-delay group 11).</param>
/// <param name="LifePotionHealing">A life potion's heal over time is still on the bot.</param>
public sealed record NaturalRestObservation(int Level, int Hp, int MaxHp, int Mp, int MaxMp, bool RecoveringMana, int QuietSits,
	IReadOnlyDictionary<int, BotSkill> Learned, IReadOnlyDictionary<int, DateTimeOffset> Cooldowns,
	IReadOnlyDictionary<int, long> ItemCounts, ushort? LastPowderSkillId, DateTimeOffset Now,
	bool LifePotionOwned = false, bool LifePotionReady = false, bool LifePotionHealing = false);

/// <summary>
/// CP-37: the rest of a class with no heal of its own (the operator, 2026-10-07: "DO not use bandages, just use Potions,
/// rest when potion is on cooldown if needed"). Below the HP target it drinks an owned, ready life potion; while the
/// potion is on its delay, or none is owned, it sits to the target. Sitting restores (level + 3) x 8 x Health / 100 HP
/// every 6 s (Java PlayerGameStats.getHpRegenRate, LifeStatsRestoreService). There is no bandage step.
/// </summary>
/// <param name="HpTargetPercent">The rest goes on while HP is below this.</param>
/// <param name="UsesMana">The class's attacks need mana, so the mana sit of the rules applies as well.</param>
public sealed record NaturalPotionRestPlan(int HpTargetPercent, bool UsesMana);

/// <summary>NR-12: one skill of a class's rest, by its role in the catalog and by the name a trace reason gives it.</summary>
public sealed record NaturalRestSkill(string Role, string Name);

/// <summary>
/// NR-12: the skills a class casts only between fights, by what they do, with the numbers of their use. The Cleric's are
/// Herb Treatment and MP Recovery, which spend a reagent in a long cast that any hit cancels and share one cooldown, and
/// Penance, which trades health for mana.
/// </summary>
/// <param name="ReagentHealth">Restores HP for a reagent.</param>
/// <param name="ReagentMana">Restores MP for a reagent.</param>
/// <param name="HealthForMana">Trades HP for MP over time; null for a class with no such skill (NR-50a).</param>
/// <param name="Heal">The class's own heal, cast while the reagent skills cool down or when there is no reagent; null for a
/// class without a heal (NR-50a), which drinks its life potion and sits then.</param>
/// <param name="SharedCooldownId">The cooldown group the two reagent skills share.</param>
/// <param name="HealBelowPercent">HP below this is restored.</param>
/// <param name="HealthForManaMinimumHpPercent">The health-for-mana skill starts only at or above this HP.</param>
public sealed record NaturalRestSkills(NaturalRestSkill ReagentHealth, NaturalRestSkill ReagentMana, NaturalRestSkill? HealthForMana,
	NaturalRestSkill? Heal, int SharedCooldownId, int HealBelowPercent, int HealthForManaMinimumHpPercent)
{
	/// <summary>NR-60a: a skill that restores mana at no cost on a long cooldown. A rest casts it first when mana is
	/// needed and it is ready; a fight casts it too, by the rule table. Null for a class with no such skill.</summary>
	public NaturalRestSkill? FreeMana { get; init; }

	/// <summary>NR-50a: the two reagent skills every second class learns at level 10, for a class with no heal and no
	/// health-for-mana skill. Herb Treatment is cast below <paramref name="healBelowPercent"/> HP.</summary>
	public static NaturalRestSkills ReagentOnly(int healBelowPercent) => new(
		ReagentHealth: new("herb", "Herb Treatment"), ReagentMana: new("mp-recovery", "MP Recovery"), HealthForMana: null, Heal: null,
		SharedCooldownId: NaturalClericSkills.PowderCooldownId, HealBelowPercent: healBelowPercent, HealthForManaMinimumHpPercent: 0);

	/// <summary>A reagent skill: a long cast that any hit cancels, so only the rest casts it.</summary>
	public bool IsReagent(NaturalPriestSkill skill) => skill.Role == ReagentHealth.Role || skill.Role == ReagentMana.Role;

	/// <summary>A skill only the rest casts: the reagent skills and the one that spends HP for mana.</summary>
	public bool IsRestOnly(NaturalPriestSkill skill) => IsReagent(skill) || HealthForMana != null && skill.Role == HealthForMana.Role;

	/// <summary>NR-60a: a skill the rest casts by the powder policy's choice: a rest-only one, or the free mana skill.</summary>
	public bool IsCastInRest(NaturalPriestSkill skill) => IsRestOnly(skill) || FreeMana != null && skill.Role == FreeMana.Role;
}

/// <param name="Action"><see cref="NaturalRestRules.Powder"/>, <see cref="NaturalRestRules.CastHeal"/>,
/// <see cref="NaturalRestRules.SitForMana"/>, <see cref="NaturalRestRules.DrinkLifePotion"/>,
/// <see cref="NaturalRestRules.SitForHealth"/>, <see cref="NaturalRestRules.Done"/> or <see cref="NaturalRestRules.Blocked"/>.</param>
/// <param name="Skill">The powder skill or the heal to cast.</param>
/// <param name="RecoveringMana">The mana sit after this observation.</param>
/// <param name="ManaRecovered">The mana sit ended with this observation: the next one needs a new rest spot.</param>
/// <param name="PowderChoice">What the powder policy said, whenever a powder skill is learned; the caller traces it.</param>
/// <param name="BlockedReason">Why the rest cannot go on.</param>
public sealed record NaturalRestDecision(string Action, NaturalPriestSkill? Skill, bool RecoveringMana, bool ManaRecovered,
	NaturalPowderRestChoice? PowderChoice, string? BlockedReason);

/// <summary>
/// CP-17: how a class recovers between fights, as one pure step (docs/natural-class-profiles.md). The journey's rest
/// loop observes, asks, and carries the answer out: it casts, sits by <see cref="NaturalRestCadence"/>, defends when a
/// rest is interrupted and revives. The Priest line's plan: powder first where its skills are learned
/// (<see cref="NaturalPowderRestPolicy"/>), then the own heal while HP is below <paramref name="HealBelowPercent"/>, and
/// sitting only for mana, from below <paramref name="ManaSitBelowPercent"/> until <paramref name="ManaSitUntilPercent"/>,
/// for at most <paramref name="MaximumQuietSits"/> undisturbed sits.
/// </summary>
/// <param name="Skills">The class's skill catalog.</param>
/// <param name="PotionPlan">CP-37: the plan of a class with no heal of its own; it applies until a skill of the heal
/// role is observed in the skill list, and from then on the plan above does. Null for the Priest line.</param>
/// <param name="RestSkills">NR-12: the class's rest-only skills, by kind; null for a class that has none. NR-50a: a
/// class on the potion plan that has learned a reagent skill casts it first: the health one below the plan's HP target,
/// the mana one while its mana sit is on, each for the powder it owns; the potion and the sit follow.</param>
/// <param name="HealRole">NR-12: the role of the class's own heal in its catalog.</param>
public sealed record NaturalRestRules(NaturalPriestSkill[] Skills, int HealBelowPercent, int ManaSitBelowPercent, int ManaSitUntilPercent,
	int MaximumQuietSits, NaturalPotionRestPlan? PotionPlan = null, NaturalRestSkills? RestSkills = null, string HealRole = "heal")
{
	public const string Powder = "powder", CastHeal = "cast-heal", SitForMana = "sit-for-mana", Done = "done", Blocked = "blocked";
	public const string DrinkLifePotion = "drink-life-potion", SitForHealth = "sit-for-health";
	public const string NotRecoveredBySitting = "The character could not recover HP/MP before the next pull within the bounded sits.";
	public const string NoSelfHeal = "Priest has mana but no client-observed usable self-heal between fights.";
	public const string NotRecovered = "Priest could not recover HP/MP before the next pull within bounded healing and mana-rest attempts.";

	public NaturalRestDecision Decide(NaturalRestObservation state)
	{
		if (PotionPlan is { } plan && NaturalPriestSkills.Best(HealRole, state.Level, state.Learned, Skills) == null)
			return DecideWithoutHeal(state, plan);
		bool recovering = state.RecoveringMana, recovered = false;
		if (state.Mp * 100 < state.MaxMp * ManaSitBelowPercent) recovering = true;
		if (recovering && state.Mp * 100 >= state.MaxMp * ManaSitUntilPercent)
		{
			recovering = false;
			recovered = true;
		}
		// NA-18 (OD-9): powder first. Sitting and the own heal stay the fallback below.
		NaturalPowderRestChoice? powder = null;
		if (RestSkills is { } restSkills && Skills.Any(skill => restSkills.IsReagent(skill) && state.Learned.ContainsKey(skill.Id)))
		{
			powder = NaturalPowderRestPolicy.Decide(new NaturalPowderRestObservation(
				state.Level, state.Hp, state.MaxHp, state.Mp, state.MaxMp, recovering, state.Learned,
				state.Cooldowns, state.ItemCounts, LastPowderSkillId: state.LastPowderSkillId), state.Now, Skills, restSkills);
			if (powder.Skill is { } restSkill && restSkills.IsCastInRest(restSkill)) return new(Powder, restSkill, recovering, recovered, powder, null);
		}
		if (!recovering)
		{
			if (state.Hp * 100 >= state.MaxHp * HealBelowPercent) return new(Done, null, recovering, recovered, powder, null);
			NaturalPriestSkill? heal = NaturalPriestSkills.Best(HealRole, state.Level, state.Learned, Skills);
			return heal == null || state.Mp < heal.ManaCost
				? new(Blocked, null, recovering, recovered, powder, NoSelfHeal)
				: new(CastHeal, heal, recovering, recovered, powder, null);
		}
		return state.QuietSits >= MaximumQuietSits
			? new(Blocked, null, recovering, recovered, powder, NotRecovered)
			: new(SitForMana, null, recovering, recovered, powder, null);
	}

	/// <summary>CP-37: potion, then sit. A potion is asked for only when one is owned, its delay has run out and no
	/// potion's heal is still running; every sit counts toward <see cref="MaximumQuietSits"/>. NR-50a: a learned reagent
	/// skill is cast before either, as in the Priest line's plan; while the two cool down, the potion and the sit go on.</summary>
	private NaturalRestDecision DecideWithoutHeal(NaturalRestObservation state, NaturalPotionRestPlan plan)
	{
		bool recovering = false, recovered = false;
		if (plan.UsesMana)
		{
			recovering = state.RecoveringMana;
			if (state.Mp * 100 < state.MaxMp * ManaSitBelowPercent) recovering = true;
			if (recovering && state.Mp * 100 >= state.MaxMp * ManaSitUntilPercent)
			{
				recovering = false;
				recovered = true;
			}
		}
		bool needHealth = state.Hp * 100 < state.MaxHp * plan.HpTargetPercent;
		NaturalPowderRestChoice? powder = null;
		if (RestSkills is { } restSkills && Skills.Any(skill => restSkills.IsReagent(skill) && state.Learned.ContainsKey(skill.Id)))
		{
			powder = NaturalPowderRestPolicy.Decide(new NaturalPowderRestObservation(
				state.Level, state.Hp, state.MaxHp, state.Mp, state.MaxMp, recovering, state.Learned,
				state.Cooldowns, state.ItemCounts, LastPowderSkillId: state.LastPowderSkillId), state.Now, Skills, restSkills);
			if (powder.Skill is { } restSkill && restSkills.IsCastInRest(restSkill)) return new(Powder, restSkill, recovering, recovered, powder, null);
		}
		if (needHealth && state.LifePotionOwned && state.LifePotionReady && !state.LifePotionHealing)
			return new(DrinkLifePotion, null, recovering, recovered, powder, null);
		if (!needHealth && !recovering) return new(Done, null, recovering, recovered, powder, null);
		if (state.QuietSits >= MaximumQuietSits) return new(Blocked, null, recovering, recovered, powder, NotRecoveredBySitting);
		return new(needHealth ? SitForHealth : SitForMana, null, recovering, recovered, powder, null);
	}
}
