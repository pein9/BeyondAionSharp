using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <param name="DpCost">DP the skill needs and spends (Salvation: startconditions/dp).</param>
/// <param name="ReagentItemId">Item the skill consumes (Herb Treatment, MP Recovery: actions/itemuse).</param>
/// <param name="TargetKind">CP-35, filled by <see cref="Classes.NaturalSkillCatalog"/> and left at its default by the frozen
/// hand-typed rows, as every field after it: the template's first target (<c>TARGET</c>, <c>ME</c>, <c>TARGETORME</c>,
/// <c>POINT</c>).</param>
/// <param name="CastMillis">The template's cast time.</param>
/// <param name="RequiredWeaponGroups">The item groups one of which must be held (startconditions/weapon); null for none.</param>
/// <param name="AddWeaponRange">The server adds the main-hand weapon's attack range to <paramref name="Range"/> (awr).</param>
/// <param name="SelfCount">How often the chain step may be cast in a row (chain/selfcount); 0 without a chain.</param>
/// <param name="Activation">The template's activation (<c>ACTIVE</c>, <c>CHARGE</c>, <c>TOGGLE</c>).</param>
/// <param name="CounterStatus">The attack status that must just have happened (<c>DODGE</c>, <c>PARRY</c>, <c>BLOCK</c>).</param>
/// <param name="OutOfCombatOnly">The template refuses the cast in combat (startconditions/combatcheck).</param>
/// <param name="GroundOnly">NR-36: the template refuses the cast while the caster flies (startconditions/noflying, or
/// selfflying with restriction GROUND; Java NoFlyingCondition, SelfFlyingCondition).</param>
/// <param name="TargetFlight">NR-36: what the template asks of the target's flight (startconditions/targetflying:
/// <c>FLY</c> or <c>GROUND</c>); null when it asks nothing.</param>
/// <param name="RequiredOffHand">NR-50b: what the template asks of the left hand (startconditions/lefthandweapon; Java
/// LeftHandCondition): <c>SHIELD</c>, a shield worn, or <c>DUAL</c>, a second weapon or a two-hand weapon held; null when
/// it asks nothing.</param>
public sealed record NaturalPriestSkill(ushort Id, int MinimumLevel, string Role, int ManaCost,
	float Range, int CooldownId, int CooldownDeciseconds, string? ChainCategory = null,
	string? RequiresChainCategory = null, int ChainWindowMillis = 0, int DpCost = 0,
	int ReagentItemId = 0, int ReagentCount = 0,
	string? TargetKind = null, int CastMillis = 0, IReadOnlyList<string>? RequiredWeaponGroups = null,
	bool AddWeaponRange = false, int SelfCount = 0, string? Activation = null, string? CounterStatus = null,
	bool OutOfCombatOnly = false, bool GroundOnly = false, string? TargetFlight = null, string? RequiredOffHand = null);

/// <summary>
/// NR-18: every class's catalog is generated from the shipped skill data (<see cref="Classes.NaturalSkillCatalog"/>); the
/// hand-typed Priest table that stood here is gone. The learned SM_SKILL_LIST is still the authority: a catalog never
/// grants a skill.
/// </summary>
public static class NaturalPriestSkills
{
	/// <summary>The highest learned rank of a role in a catalog, at a level.</summary>
	public static NaturalPriestSkill? Best(string role, int level, IReadOnlyDictionary<int, BotSkill> learned,
		IEnumerable<NaturalPriestSkill> catalog) =>
		catalog.Where(skill => skill.Role == role && skill.MinimumLevel <= level && learned.ContainsKey(skill.Id))
		.OrderByDescending(skill => skill.MinimumLevel).ThenByDescending(skill => skill.Id).FirstOrDefault();
}

/// <param name="TargetAdjacent">The target is in melee reach: within <see cref="Navigation.NaturalCombatGeometry.MeleeReach"/>
/// by the client's estimate, or it hit the bot within the last few seconds (a chasing monster's client
/// position lags; its swings do not).</param>
/// <param name="InEmergency">HP fell to the rule table's emergency percentage and has not recovered to the percentage
/// that ends it yet: sustain only.</param>
/// <param name="HasBlessing">NR-18: no rule reads this or <paramref name="HasRejuvenation"/> any more (the table reads
/// <paramref name="ActiveEffectSkillIds"/>); both stay because every recorded fight decision writes them.</param>
/// <param name="ShieldScrollReady">NA-19: <see cref="NaturalHelpItemPolicy.DecideShield"/> chose an owned, ready
/// Anti-Shock tier (HP at or below 50%, no shield active, not casting).</param>
/// <param name="WeaponAttackRangeMillis">CP-35: the main-hand weapon's attack range, for a skill that adds it to its
/// range.</param>
/// <param name="WeaponAttackSpeedMillis">CP-35: the main-hand weapon's attack speed.</param>
/// <param name="PreviousChainCategory">CP-36: the chain category before <paramref name="OpenChainCategory"/> (Java
/// ChainSkills keeps both).</param>
/// <param name="ChainStepAt">When the current chain step was cast; a follow-up counts its own chain time from it.</param>
/// <param name="OpenChainUseCount">How often the current chain step was cast in a row.</param>
/// <param name="ActiveEffectSkillIds">The skill ids of the effects the client shows on the bot; null when unobserved.</param>
/// <param name="OffHand">NR-50b: what the bot holds for a skill's left-hand condition
/// (<see cref="Classes.NaturalSkillCatalog.OffHandHeld"/>): <c>SHIELD</c>, <c>DUAL</c> or null.</param>
/// <param name="CastThisFight">NR-53b: the skills the bot has cast in this fight; null when the caller does not say.</param>
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
	bool ShieldScrollReady = false, ushort? LastCancelledSkillId = null,
	int? WeaponAttackRangeMillis = null, int? WeaponAttackSpeedMillis = null,
	string? PreviousChainCategory = null, DateTimeOffset? ChainStepAt = null, int? OpenChainUseCount = null,
	IReadOnlySet<int>? ActiveEffectSkillIds = null, string? OffHand = null, IReadOnlySet<ushort>? CastThisFight = null);

public sealed record NaturalCombatChoice(string Action, NaturalPriestSkill? Skill, int? TargetObjectId,
	string Reason, NaturalDecisionCheck[] Checks);

/// <summary>Observed eligibility and baseline disposition; no outcome is assigned to an unchosen action.</summary>
public sealed record NaturalCombatCandidate(string Action, ushort? SkillId, int? TargetObjectId,
	bool Legal, string[] IllegalReasons, string? BaselineRejection, bool NeedsNavigationCheck = false);
