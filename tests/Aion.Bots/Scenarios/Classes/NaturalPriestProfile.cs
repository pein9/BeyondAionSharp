using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// The accepted line's two profiles. NR-13: their catalogs are generated from the shipped skill data, as every other
/// class's is, with the roles below. NR-14: the Priest fights by the rule table <see cref="PriestRules"/>. NR-16: the
/// Cleric fights by <see cref="ClericRules"/>, and its catalog reaches level 26. NR-18: the static rule and the two
/// hand-typed tables these profiles began with are gone.
/// </summary>
public static class NaturalPriestProfile
{
	/// <summary>The Priest's eight active skills of levels 1 to 8: Healing Light, Smite, Hallowed Strike, Blessing of
	/// Guardianship and Infernal Blaze, with their second ranks.</summary>
	internal static readonly IReadOnlyDictionary<int, string> PriestRoles = new Dictionary<int, string>
	{
		[1838] = "heal", [1839] = "heal", [4012] = "smite", [4013] = "smite", [1614] = "hallowed", [1615] = "hallowed",
		[1684] = "blessing", [1814] = "infernal",
	};

	/// <summary>The Cleric's own active skills from level 10 to level 26, by role, beside the Priest's it keeps: the
	/// powder skills Herb Treatment and MP Recovery, Salvation, Light of Rejuvenation, Flashbolt (the follow-up of Smite),
	/// Slashing Wind, Earth's Wrath, Root, Penance, the Holy Servant, Divine Touch, Healing Grace, Divine Spark and Flash
	/// of Recovery, and the later ranks of the Priest's roles. NR-16: the ten ranks of levels 25 and 26 are the last line.</summary>
	internal static readonly IReadOnlyDictionary<int, string> ClericRoles = new Dictionary<int, string>(PriestRoles)
	{
		[246] = "herb", [247] = "herb", [251] = "herb", [249] = "mp-recovery", [250] = "mp-recovery", [252] = "mp-recovery",
		[3922] = "salvation", [3939] = "rejuvenation", [3940] = "rejuvenation", [3941] = "rejuvenation",
		[4025] = "followup", [4026] = "followup", [4027] = "followup", [4061] = "wind", [4062] = "wind", [4063] = "wind",
		[4083] = "wrath", [4084] = "wrath", [4085] = "wrath", [4127] = "root",
		[1840] = "heal", [1841] = "heal", [1842] = "heal", [4014] = "smite", [4015] = "smite", [4016] = "smite",
		[1815] = "infernal", [1816] = "infernal", [1817] = "infernal", [1616] = "hallowed", [1617] = "hallowed", [1618] = "hallowed",
		[3867] = "penance", [3868] = "penance", [4106] = "servant", [4108] = "servant", [4073] = "touch", [4074] = "touch",
		[4203] = "grace", [4204] = "grace", [4037] = "spark", [3951] = "flash-recovery",
		[253] = "herb", [254] = "mp-recovery", [3869] = "penance", [3942] = "rejuvenation", [4028] = "followup", [4064] = "wind",
		[4086] = "wrath", [4110] = "servant", [1843] = "heal", [4017] = "smite",
	};

	/// <summary>NR-16: the two skills of levels 25 and 26 the Cleric does not cast, beside
	/// <see cref="NaturalClericSkills.Excluded"/>.</summary>
	private static readonly IReadOnlyDictionary<int, string> ClericExcludedFromLevel25 = new Dictionary<int, string>
	{
		[4006] = "Splendor of Flight restores flight time over 15 s; the rule table is for fights on the ground.",
		[3880] = "Stability III: as Stability I.",
	};

	/// <summary>
	/// NR-14: the Priest's fight as a table, saying what the static rule said. With the monster on it: Infernal Blaze
	/// (instant, a stun), Hallowed Strike (instant, a 30% attack-speed slow) and Smite, and the mace between skills. From
	/// range: Smite, which is the pull, while the monster comes. Blessing of Guardianship goes up before the first hit.
	/// <para>
	/// The ladder: the Anti-Shock scroll at 50% HP, the life potion at 90%, Healing Light at 55% against one attacker and
	/// at 70% against two or more; the potion's percentage and the two heal percentages are the run's. Once a fight has
	/// had its heal and the target is at or below 15% HP (the run's), Smite finishes it in the heal's place. An emergency
	/// runs from 35% until 45%, and from 55% until 65% against two or more attackers on a Seasoned target. Healing Light's
	/// cost is kept back from every attack; a mana potion is drunk below that cost and 10, or below the cheapest attack.
	/// The Priest leaves at three attackers, or at 30% HP with nothing of the ladder left. Against a target that attacks
	/// from range it holds within 12 m, or with two attackers on it, when the run asks for the hold; otherwise it walks up.
	/// </para>
	/// </summary>
	internal static readonly NaturalRotationRules PriestRules = new("natural-priest-v1",
		Adjacent: ["infernal", "hallowed", "smite"], AtRange: ["smite"],
		Upkeep: [new("blessing")],
		Recovery:
		[
			new(NaturalRecoveryKind.ShieldScroll, 50),
			new(NaturalRecoveryKind.LifePotion, 90, FromRun: NaturalRunPercent.LifePotion),
			new(NaturalRecoveryKind.Skill, 55, "heal", HpPercentMultiple: 70, FinishInstead: true, FromRun: NaturalRunPercent.Heal),
		],
		SwarmAttackers: NaturalPatrolPolicy.MaximumMembers + 1, FleeHpPercent: 30, AutoAttack: NaturalAutoAttack.Filler,
		EmergencyPercent: 35, EmergencyClearPercent: 45, EmergencySeasonedPairPercent: 55,
		Finisher: new("smite", 15, FromRun: true), ReserveRole: "heal", ManaPotionReserveMargin: 10, RangedHoldWithin: 12);

	/// <summary>
	/// NR-16: the Cleric's fight as a table, saying what the static rule said. A follow-up that is open goes first:
	/// Divine Spark after Flashbolt, Flashbolt after Smite, Divine Touch after Slashing Wind, each inside 3 s. Smite is
	/// brought to the front while Flashbolt is off cooldown and both can be paid for beside the heal; at other times it is
	/// the last filler. The Holy Servant is summoned on a target above 50% HP. Then, with the monster on the Cleric:
	/// Infernal Blaze, Hallowed Strike, Slashing Wind, Earth's Wrath (a 1.5 s cast, last where a hit can cancel it); from
	/// range: Earth's Wrath, Slashing Wind. The mace swings between skills. Blessing of Guardianship goes up before the
	/// first hit, and Light of Rejuvenation is kept up while the Cleric is being hit.
	/// <para>
	/// The ladder: the Anti-Shock scroll at 50% HP; Salvation, paid with DP, at 25% or in an emergency; the life potion at
	/// 90%; Flash of Recovery in an emergency only; Healing Grace at 55% against one attacker and at 70% against two or
	/// more, passed over once after it was cancelled; Healing Light at the same percentages. The potion's percentage and
	/// the heal percentages are the run's. Once a fight has had a Healing Light and the target is at or below 15% HP (the
	/// run's), Smite finishes it in a heal's place. An emergency runs from 35% until 45%, and from 55% until 65% against
	/// two or more attackers on a Seasoned target. Healing Light's cost is kept back from every attack; a mana potion is
	/// drunk below that cost and 10. The Cleric leaves at three attackers, or at 30% HP with nothing of the ladder left,
	/// and casts Root on its target first. Against a target that attacks from range it holds within 12 m, or with two
	/// attackers on it, when the run asks for the hold; otherwise it walks up.
	/// </para>
	/// </summary>
	internal static readonly NaturalRotationRules ClericRules = new("natural-cleric-v1",
		Adjacent: ["spark", "followup", "touch", "servant", "infernal", "hallowed", "wind", "wrath", "smite"],
		AtRange: ["spark", "followup", "touch", "servant", "wrath", "wind", "smite"],
		Upkeep: [new("blessing"), new("rejuvenation", DuringFight: true, UnderAttackOnly: true)],
		Recovery:
		[
			new(NaturalRecoveryKind.ShieldScroll, 50),
			new(NaturalRecoveryKind.Skill, 25, "salvation"),
			new(NaturalRecoveryKind.LifePotion, 90, FromRun: NaturalRunPercent.LifePotion),
			new(NaturalRecoveryKind.Skill, 35, "flash-recovery", EmergencyOnly: true),
			new(NaturalRecoveryKind.Skill, 55, "grace", HpPercentMultiple: 70, PassOverWhenCancelled: true, FinishInstead: true,
				FromRun: NaturalRunPercent.Heal),
			new(NaturalRecoveryKind.Skill, 55, "heal", HpPercentMultiple: 70, FinishInstead: true, FromRun: NaturalRunPercent.Heal),
		],
		SwarmAttackers: NaturalPatrolPolicy.MaximumMembers + 1, FleeHpPercent: 30, AutoAttack: NaturalAutoAttack.Filler, ControlRole: "root",
		EmergencyPercent: 35, EmergencyClearPercent: 45, EmergencySeasonedPairPercent: 55,
		Finisher: new("smite", 15, FromRun: true), ReserveRole: "heal", ManaPotionReserveMargin: 10,
		Openers: ["smite"], OnlyWhileTargetAbove: new Dictionary<string, int> { ["servant"] = 50 }, RangedHoldWithin: 12);

	/// <summary>The Cleric's catalog reaches this level: the level the recorded Cleric ends its Abyss-entry leg at.</summary>
	private const int ClericCatalogTopLevel = 26;

	/// <summary>The Priest's catalog, which a Chanter keeps as well.</summary>
	internal static NaturalPriestSkill[] PriestCatalog(StaticData data) =>
		NaturalSkillCatalog.Build(data, PlayerClass.PRIEST, PriestRoles, NaturalSkillCatalog.CommonExcluded);

	/// <summary>Blessing of Guardianship, kept up between fights as the recorded human did.</summary>
	internal static readonly NaturalUpkeepBuff Blessing = new("blessing", "buff-blessing");

	/// <summary>Heal with Healing Light below 90% HP; sit only for mana, from below 50% until 80%, for at most 12 quiet
	/// sits. Sitting solely for missing HP leaves the Priest exposed to respawns and patrols.</summary>
	internal static NaturalRestRules RestWith(NaturalPriestSkill[] skills) => new(skills, HealBelowPercent: 90,
		ManaSitBelowPercent: 50, ManaSitUntilPercent: 80, MaximumQuietSits: 12, RestSkills: NaturalClericSkills.RestSkills);

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

	public static NaturalClassProfile CreatePriest(StaticData data)
	{
		ArgumentNullException.ThrowIfNull(data);
		NaturalPriestSkill[] skills = PriestCatalog(data);
		NaturalProfileValidator.Require(data, PlayerClass.PRIEST, 9, skills, NaturalSkillCatalog.CommonExcluded, PriestRules.Lines(skills),
			NaturalGearRules.Priest);
		return new NaturalClassProfile
		{
			Class = PlayerClass.PRIEST,
			Skills = skills,
			Excluded = NaturalSkillCatalog.CommonExcluded,
			Combat = new NaturalRotationCombatPolicy(PriestRules, skills, PriestLineMovement),
			// CP-06: the level 1-9 kit, and nothing from level 10 on.
			HelpItems = new(NaturalHelpItemAllowlist.Starter, NaturalHelpItemAllowlist.StarterMaxLevel),
			Upkeep = [Blessing],
			// NR-36: Smite is the shot in flight, as the fungus fight has always cast it.
			AirAttackRoles = ["smite"],
			PatrolRule = NaturalPatrolRule.Baseline,
			RangedHold = NaturalRangedHold.RunOption,
			Rest = RestWith(skills),
			Ranges = PriestLineRanges,
			Readiness = PriestLineReadiness,
			Movement = PriestLineMovement,
			Campaign = PriestLineCampaign,
			Gear = NaturalGearRules.Priest,
			Restock = PriestLineRestock,
		};
	}

	public static NaturalClassProfile CreateCleric(StaticData data)
	{
		ArgumentNullException.ThrowIfNull(data);
		var excluded = NaturalSkillCatalog.CommonExcluded.Concat(NaturalClericSkills.Excluded).Concat(ClericExcludedFromLevel25)
			.ToDictionary(entry => entry.Key, entry => entry.Value);
		NaturalPriestSkill[] skills = NaturalSkillCatalog.Build(data, PlayerClass.CLERIC, ClericRoles, excluded);
		NaturalProfileValidator.Require(data, PlayerClass.CLERIC, ClericCatalogTopLevel, skills, excluded, ClericRules.Lines(skills),
			NaturalGearRules.Cleric);
		return new NaturalClassProfile
		{
			Class = PlayerClass.CLERIC,
			Skills = skills,
			Excluded = excluded,
			Combat = new NaturalRotationCombatPolicy(ClericRules, skills, PriestLineMovement),
			// OD-13: the approved kit at every level. NR-34: a Cleric casts from mana and rests with the powder, so it
			// takes both kinds, which is every row.
			HelpItems = NaturalHelpItemRules.ForKinds(caster: true, reagent: true),
			Upkeep = [Blessing],
			// NR-36: Smite is the shot in flight, as the fungus fight has always cast it.
			AirAttackRoles = ["smite"],
			// NA-22 (OD-14).
			PatrolRule = NaturalPatrolRule.HoldAndAssess,
			RangedHold = NaturalRangedHold.RunOption,
			Rest = RestWith(skills),
			Ranges = PriestLineRanges,
			Readiness = PriestLineReadiness,
			Movement = PriestLineMovement,
			Campaign = PriestLineCampaign,
			Gear = NaturalGearRules.Cleric,
			Restock = PriestLineRestock,
		};
	}
}
