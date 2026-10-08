using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// CP-48: the Engineer, levels 1-9 (docs/natural-class-profiles.md). Its catalog is generated from the shipped data, so
/// the profile is built from the static data of the run. It fights from the pistol's own range: its attack skills have no
/// range of their own and add the weapon's (20 m), so every distance here is 20 m or less.
/// </summary>
public static class NaturalEngineerProfile
{
	private const int TopLevel = 9;

	/// <summary>The Engineer's seven active skills. Gunshot opens the chain Rapidfire follows, twice, each inside 2 s of
	/// the step before it. Direct Shot, Hot Shot and Bullet Resistance have no chain, so each of them resets an open one.</summary>
	private static readonly IReadOnlyDictionary<int, string> Roles = new Dictionary<int, string>
	{
		[2219] = "direct", [2220] = "direct", [1957] = "gunshot", [1958] = "gunshot", [2142] = "rapid", [1942] = "hot", [2168] = "resist",
	};

	/// <summary>
	/// At every distance: Hot Shot first when it is ready (level 9; it lowers the fire resistance Gunshot and Rapidfire are
	/// measured against), Gunshot, Rapidfire twice, and Direct Shot (2 s cooldown, no mana) for the pull and everything
	/// between; the pistol fires whenever no skill is ready. The table holds an open chain: while Rapidfire only cools down
	/// and clears inside its 2 s, nothing else is cast, so no Direct Shot comes between the steps.
	/// <para>
	/// The ladder, CP-Q11: the shield scroll at 50% HP, the life potion at or below 75%, and Bullet Resistance (level 7,
	/// half of every hit for 10 s) at or below 60%, never inside an open chain. The Engineer leaves at two attackers, or at
	/// 25% HP with nothing ready.
	/// </para>
	/// </summary>
	private static readonly NaturalRotationRules Rules = new("natural-engineer-v1",
		Adjacent: ["hot", "gunshot", "rapid", "direct"], AtRange: ["hot", "gunshot", "rapid", "direct"],
		Upkeep: [],
		Recovery: [new(NaturalRecoveryKind.ShieldScroll, 50), new(NaturalRecoveryKind.LifePotion, 75), new(NaturalRecoveryKind.Skill, 60, "resist")],
		SwarmAttackers: 2, FleeHpPercent: 25, AutoAttack: NaturalAutoAttack.Filler, HoldOpenChain: true);

	/// <summary>The pull opens at 18 m, inside the pistol's 20 m and the planner's bound. Every other distance is the
	/// Priest line's number brought inside 20 m: what the Priest measures from a 25 m spell, the Engineer measures from
	/// its weapon.</summary>
	private static readonly NaturalEngageRanges Ranges = new(
		MeleeReach: Navigation.NaturalCombatGeometry.MeleeReach, SpellRange: 17, PullDistance: 18, FiringRange: 19,
		SpawnApproachRange: 19, SpawnPullScanRange: 20, FightThroughPullRange: 20,
		StandoffSpellRange: 20, StandoffArrivalTolerance: 3, StandoffSafetyMargin: 1, RangedApproachRadius: 16);

	/// <summary>It holds at 18 m and never walks in: an approach is a route to ground in range and sight, and inside 18 m
	/// it stays. After a distance refusal it comes to 12 m, still a pistol shot away; no gun skill is a melee skill. An
	/// obstacle the bot's geometry does not see is answered as the Priest answers it, by closing in: a search for another
	/// sight line is not built.</summary>
	private static readonly NaturalFightMovement Movement = new(NaturalPullStyle.WeaponRangeStandOff,
		MeleeReach: Navigation.NaturalCombatGeometry.MeleeReach, RangedRouteBeyond: 18, RangeRefusalCloseIn: 12, HoldDistance: 18,
		ObstacleAnswer: NaturalObstacleAnswer.CloseToMelee);

	/// <summary>Direct Shot and the pistol cost no mana, so mana only pays for the chain: low thresholds.</summary>
	private static readonly NaturalReadinessThresholds Readiness = new(
		BeforePull: new(80), BeforeUseBar: new(60, 20), BetweenAdds: new(60, 20), BeforeNamedTarget: new(80, 30));

	/// <summary>The Priest line's Ishalgen numbers brought inside the pistol's 20 m.</summary>
	private static readonly NaturalCampaignRules Campaign = new(
		SpriggRouteBeyond: 20, SpriggStandoff: 18, SpriggSelectWithin: 20, FiringEdgeWithin: 20, StalkerSearchRange: 19,
		BlockerReplanBeyond: 20, ReturnCooldownHpFraction: 0.75f, StalkerPull: new(90), BeforeSack: new(80), BeforeCamp: new(80));

	/// <summary>CP-Q11: the Minor Life Elixir of trade list 721, bought at 5 or fewer up to 12, and no purchase takes
	/// Kinah below 500.</summary>
	private static readonly NaturalRestockRules Restock = new(NaturalPriestProfile.PriestLineRestock.Lines, kinahFloor: 500);

	public static NaturalClassProfile Create(StaticData data)
	{
		ArgumentNullException.ThrowIfNull(data);
		IReadOnlyDictionary<int, string> excluded = NaturalSkillCatalog.CommonExcluded;
		NaturalPriestSkill[] skills = NaturalSkillCatalog.Build(data, PlayerClass.ENGINEER, Roles, excluded);
		NaturalGearRules gear = NaturalClassGearTable.Engineer.Rules(NaturalClassLineContract.LoadDefault());
		NaturalProfileValidator.Require(data, PlayerClass.ENGINEER, TopLevel, skills, excluded, Rules.Lines(skills), gear);
		return new NaturalClassProfile
		{
			Class = PlayerClass.ENGINEER,
			Skills = skills,
			Excluded = excluded,
			Combat = new NaturalRotationCombatPolicy(Rules, skills, Movement),
			// CP-05: the level 1-9 kit. Every attack skill is instant, so a casting-speed scroll would do nothing: the shared
			// speed slot holds the attack-speed scroll (Blitzopan, the Courage family), which quickens the pistol.
			HelpItems = new(NaturalHelpItemAllowlist.Starter, NaturalHelpItemAllowlist.StarterMaxLevel, SharedSlotFamily: "courage"),
			Upkeep = [],
			PatrolRule = NaturalPatrolRule.Baseline,
			RangedHold = NaturalRangedHold.RunOption,
			// CP-Q11: a life potion below 90% HP when it is ready and a sit to 90% while it is on its delay. A sit for mana
			// only below 20%, until 50%. No bandage.
			Rest = new NaturalRestRules(skills, HealBelowPercent: 90, ManaSitBelowPercent: 20, ManaSitUntilPercent: 50, MaximumQuietSits: 12,
				PotionPlan: new NaturalPotionRestPlan(HpTargetPercent: 90, UsesMana: true)),
			Ranges = Ranges,
			Readiness = Readiness,
			Movement = Movement,
			Campaign = Campaign,
			Gear = gear,
			Restock = Restock,
		};
	}
}
