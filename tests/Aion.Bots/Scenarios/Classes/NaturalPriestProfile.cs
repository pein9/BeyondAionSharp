using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// The accepted line's two profiles. Both are adapters over the static <see cref="NaturalPriestCombatPolicy"/> and its
/// frozen hand-typed skill tables, which stay as they are: the Priest with <see cref="NaturalPriestSkills.All"/>, the
/// Cleric with <see cref="NaturalClericSkills.All"/>.
/// </summary>
public static class NaturalPriestProfile
{
	/// <summary>Blessing of Guardianship, kept up between fights as the recorded human did.</summary>
	internal static readonly NaturalUpkeepBuff Blessing = new("blessing", "buff-blessing");

	/// <summary>Heal with Healing Light below 90% HP; sit only for mana, from below 50% until 80%, for at most 12 quiet
	/// sits. Sitting solely for missing HP leaves the Priest exposed to respawns and patrols.</summary>
	internal static NaturalRestRules RestWith(NaturalPriestSkill[] skills) => new(skills, HealBelowPercent: 90,
		ManaSitBelowPercent: 50, ManaSitUntilPercent: 80, MaximumQuietSits: 12);

	/// <summary>The Priest casts from range and lets the monster come. The pull distance stays the run's parameter.</summary>
	internal static readonly NaturalEngageRanges PriestLineRanges = new(
		MeleeReach: Navigation.NaturalCombatGeometry.MeleeReach, SpellRange: Navigation.NaturalPullPlanner.SpellRange, PullDistance: null,
		FiringRange: Navigation.NaturalFightThrough.FiringRange, SpawnApproachRange: 23, SpawnPullScanRange: 30, FightThroughPullRange: 30,
		StandoffSpellRange: 25, StandoffArrivalTolerance: 3, StandoffSafetyMargin: 1, RangedApproachRadius: 20);

	internal static readonly NaturalReadinessThresholds PriestLineReadiness = new(
		BeforePull: new(80), BeforeUseBar: new(60, 40), BetweenAdds: new(60, 40), BeforeNamedTarget: new(80, 60));

	/// <summary>A stand-off: a ranged route while the target is farther than 25 m, then up to it; after a distance refusal
	/// come to 10 m, or inside melee reach for a melee skill; an obstacle is answered by closing to melee.</summary>
	internal static readonly NaturalFightMovement PriestLineMovement = new(NaturalPullStyle.StandOff,
		MeleeReach: Navigation.NaturalCombatGeometry.MeleeReach, RangedRouteBeyond: 25, RangeRefusalCloseIn: 10);

	/// <summary>The Sprigg hunt and Q2005's firing edge are spell-range work: 22, 23 and 25 m, each where it stood.</summary>
	internal static readonly NaturalCampaignRules PriestLineCampaign = new(
		SpriggRouteBeyond: 25, SpriggStandoff: 22, SpriggSelectWithin: 25, FiringEdgeWithin: 25, StalkerSearchRange: 23,
		BlockerReplanBeyond: 25, ReturnCooldownHpFraction: 0.75f, StalkerPull: new(90), BeforeSack: new(80), BeforeCamp: new(80));

	/// <summary>The Minor Life Elixir of trade list 721, bought when every owned life potion and elixir together is 5 or
	/// fewer, up to 12, with no Kinah floor.</summary>
	internal static readonly NaturalRestockRules PriestLineRestock = new(
	[
		new(NaturalIshalgenPotionPolicy.VendorLifeElixirId, AtOrBelow: 5, Target: 12, TradeListId: 721, CountedWith:
		[
			NaturalIshalgenPotionPolicy.StarterLifePotionId, NaturalIshalgenPotionPolicy.VendorLifeElixirId,
			NaturalIshalgenPotionPolicy.LesserLifeElixirId, NaturalIshalgenPotionPolicy.LesserLifePotionId,
			NaturalIshalgenPotionPolicy.LifePotionId, NaturalIshalgenPotionPolicy.MajorLifePotionId,
		]),
	]);

	public static NaturalClassProfile Priest { get; } = new()
	{
		Class = PlayerClass.PRIEST,
		Skills = NaturalPriestSkills.All,
		Excluded = new Dictionary<int, string>(),
		Combat = new StaticPolicy(NaturalPriestSkills.All),
		// CP-06: the level 1-9 kit, and nothing from level 10 on.
		HelpItems = new(NaturalHelpItemAllowlist.Starter, NaturalHelpItemAllowlist.StarterMaxLevel),
		Upkeep = [Blessing],
		PatrolRule = NaturalPatrolRule.Baseline,
		RangedHold = NaturalRangedHold.RunOption,
		Rest = RestWith(NaturalPriestSkills.All),
		Ranges = PriestLineRanges,
		Readiness = PriestLineReadiness,
		Movement = PriestLineMovement,
		Campaign = PriestLineCampaign,
		Gear = NaturalGearRules.Priest,
		Restock = PriestLineRestock,
	};

	public static NaturalClassProfile Cleric { get; } = new()
	{
		Class = PlayerClass.CLERIC,
		Skills = NaturalClericSkills.All,
		Excluded = NaturalClericSkills.Excluded,
		Combat = new StaticPolicy(NaturalClericSkills.All),
		// OD-13: the approved kit at every level.
		HelpItems = new(NaturalHelpItemAllowlist.AllLevels.ToArray(), null),
		Upkeep = [Blessing],
		// NA-22 (OD-14).
		PatrolRule = NaturalPatrolRule.HoldAndAssess,
		RangedHold = NaturalRangedHold.RunOption,
		Rest = RestWith(NaturalClericSkills.All),
		Ranges = PriestLineRanges,
		Readiness = PriestLineReadiness,
		Movement = PriestLineMovement,
		Campaign = PriestLineCampaign,
		Gear = NaturalGearRules.Cleric,
		Restock = PriestLineRestock,
	};

	/// <summary>Calls the static policy with the class's catalog and the run's parameters, and reports the run's policy id.</summary>
	internal sealed class StaticPolicy(NaturalPriestSkill[] catalog) : INaturalCombatPolicy
	{
		public string PolicyVersion(NaturalMauPolicyParameters parameters) => parameters.Id;

		public int EmergencyEnterPercent(int attackers, bool targetSeasoned) =>
			NaturalPriestCombatPolicy.EmergencyEnterPercent(attackers, targetSeasoned);

		public int EmergencyExitPercent(int attackers, bool targetSeasoned) =>
			NaturalPriestCombatPolicy.EmergencyExitPercent(attackers, targetSeasoned);

		public NaturalCombatChoice Decide(NaturalCombatObservation state, DateTimeOffset now, NaturalMauPolicyParameters parameters) =>
			NaturalPriestCombatPolicy.Decide(state, now, catalog, parameters);

		public NaturalCombatCandidate[] CandidateActions(NaturalCombatObservation state, DateTimeOffset now, NaturalCombatChoice chosen,
			NaturalMauPolicyParameters parameters) => NaturalPriestCombatPolicy.CandidateActions(state, now, chosen, catalog, parameters);
	}
}
