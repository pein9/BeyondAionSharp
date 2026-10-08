using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// The accepted line's two profiles. Both are still adapters over the static <see cref="NaturalPriestCombatPolicy"/>.
/// NR-13: their catalogs are generated from the shipped skill data, as every other class's is, with the roles below; a
/// generated row equals its hand-typed row of <see cref="NaturalPriestSkills.All"/> and <see cref="NaturalClericSkills.All"/>
/// in every field that row holds.
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

	/// <summary>The Cleric's own active skills from level 10 to level 24, by role, beside the Priest's it keeps: the
	/// powder skills Herb Treatment and MP Recovery, Salvation, Light of Rejuvenation, Flashbolt (the follow-up of Smite),
	/// Slashing Wind, Earth's Wrath, Root, Penance, the Holy Servant, Divine Touch, Healing Grace, Divine Spark and Flash
	/// of Recovery, and the later ranks of the Priest's roles. The ranks of levels 25 and 26 have no role yet.</summary>
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
	};

	/// <summary>The Cleric's catalog reaches this level; the hand-typed table it replaces did.</summary>
	private const int ClericCatalogTopLevel = 24;

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
		NaturalProfileValidator.Require(data, PlayerClass.PRIEST, 9, skills, NaturalSkillCatalog.CommonExcluded, [], NaturalGearRules.Priest);
		return new NaturalClassProfile
		{
			Class = PlayerClass.PRIEST,
			Skills = skills,
			Excluded = NaturalSkillCatalog.CommonExcluded,
			Combat = new StaticPolicy(skills),
			// CP-06: the level 1-9 kit, and nothing from level 10 on.
			HelpItems = new(NaturalHelpItemAllowlist.Starter, NaturalHelpItemAllowlist.StarterMaxLevel),
			Upkeep = [Blessing],
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
		var excluded = NaturalSkillCatalog.CommonExcluded.Concat(NaturalClericSkills.Excluded).ToDictionary(entry => entry.Key, entry => entry.Value);
		NaturalPriestSkill[] skills = NaturalSkillCatalog.Build(data, PlayerClass.CLERIC, ClericRoles, excluded);
		NaturalProfileValidator.Require(data, PlayerClass.CLERIC, ClericCatalogTopLevel, skills, excluded, [], NaturalGearRules.Cleric);
		return new NaturalClassProfile
		{
			Class = PlayerClass.CLERIC,
			Skills = skills,
			Excluded = excluded,
			Combat = new StaticPolicy(skills),
			// OD-13: the approved kit at every level.
			HelpItems = new(NaturalHelpItemAllowlist.AllLevels.ToArray(), null),
			Upkeep = [Blessing],
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
