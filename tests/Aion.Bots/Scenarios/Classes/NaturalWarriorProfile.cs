using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// CP-42: the Warrior, levels 1-9 (docs/natural-class-profiles.md). Its catalog is generated from the shipped data, so
/// the profile is built from the static data of the run. This item holds everything but the fight: the table has one
/// line, the weapon swing, and every active skill is excluded until CP-43 adds the rotation.
/// </summary>
public static class NaturalWarriorProfile
{
	/// <summary>The reason every castable Warrior skill carries until its rotation exists.</summary>
	public const string RotationPending = "rotation added by CP-43";

	private const int TopLevel = 9;

	/// <summary>
	/// CP-Q11: in a fight the shield scroll at 50% HP and the life potion at or below 75%; it leaves at three attackers,
	/// or at 25% HP with neither ready. The weapon swings whenever nothing else can be done.
	/// </summary>
	private static readonly NaturalRotationRules Rules = new("natural-warrior-v0", Adjacent: [], AtRange: [], Upkeep: [],
		Recovery: [new(NaturalRecoveryKind.ShieldScroll, 50), new(NaturalRecoveryKind.LifePotion, 75)],
		SwarmAttackers: 3, FleeHpPercent: 25, AutoAttack: NaturalAutoAttack.Filler);

	/// <summary>It walks to the target and fights at the weapon's reach; after a distance refusal it comes inside reach.</summary>
	private static readonly NaturalFightMovement Movement = new(NaturalPullStyle.WalkIn,
		MeleeReach: Navigation.NaturalCombatGeometry.MeleeReach, RangedRouteBeyond: 0, RangeRefusalCloseIn: 2);

	/// <summary>HP only: its attacks need no mana worth resting for.</summary>
	private static readonly NaturalReadinessThresholds Readiness = new(
		BeforePull: new(80), BeforeUseBar: new(60), BetweenAdds: new(60), BeforeNamedTarget: new(80));

	/// <summary>CP-Q11: the Minor Life Elixir of trade list 721, bought at 5 or fewer up to 12, and no purchase takes
	/// Kinah below 500.</summary>
	private static readonly NaturalRestockRules Restock = new(NaturalPriestProfile.PriestLineRestock.Lines, kinahFloor: 500);

	public static NaturalClassProfile Create(StaticData data)
	{
		ArgumentNullException.ThrowIfNull(data);
		var excluded = new Dictionary<int, string>(NaturalSkillCatalog.CommonExcluded);
		foreach (int id in NaturalSkillCatalog.AutoLearnedCastable(data, PlayerClass.WARRIOR, TopLevel).Keys)
			excluded.TryAdd(id, RotationPending);
		NaturalPriestSkill[] skills = NaturalSkillCatalog.Build(data, PlayerClass.WARRIOR, new Dictionary<int, string>(), excluded);
		NaturalGearRules gear = NaturalClassGearTable.Warrior.Rules(NaturalClassLineContract.LoadDefault());
		NaturalProfileValidator.Require(data, PlayerClass.WARRIOR, TopLevel, skills, excluded, Rules.Lines(skills), gear);
		return new NaturalClassProfile
		{
			Class = PlayerClass.WARRIOR,
			Skills = skills,
			Excluded = excluded,
			Combat = new NaturalRotationCombatPolicy(Rules, skills, Movement),
			// CP-05: the level 1-9 kit. The shared speed slot holds the attack-speed scroll (Blitzopan, the Courage family).
			HelpItems = new(NaturalHelpItemAllowlist.Starter, NaturalHelpItemAllowlist.StarterMaxLevel, SharedSlotFamily: "courage"),
			Upkeep = [],
			PatrolRule = NaturalPatrolRule.Baseline,
			RangedHold = NaturalRangedHold.Never,
			// CP-Q11, answered: a life potion below 90% HP when it is ready, a sit to 90% while it is on its delay. No mana
			// target and no bandage.
			Rest = new NaturalRestRules(skills, HealBelowPercent: 90, ManaSitBelowPercent: 50, ManaSitUntilPercent: 80, MaximumQuietSits: 12,
				PotionPlan: new NaturalPotionRestPlan(HpTargetPercent: 90, UsesMana: false)),
			// The engage distances and the campaign numbers start from the Priest line's and are tuned when the Warrior
			// first fights and journeys (CP-43, CP-44).
			Ranges = NaturalPriestProfile.PriestLineRanges,
			Readiness = Readiness,
			Movement = Movement,
			Campaign = NaturalPriestProfile.PriestLineCampaign,
			Gear = gear,
			Restock = Restock,
		};
	}
}
