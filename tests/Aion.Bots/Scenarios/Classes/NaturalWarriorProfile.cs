using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// CP-42, CP-43: the Warrior, levels 1-9 (docs/natural-class-profiles.md). Its catalog is generated from the shipped
/// data, so the profile is built from the static data of the run.
/// </summary>
public static class NaturalWarriorProfile
{
	private const int TopLevel = 9;

	/// <summary>The roles of the Warrior's six active skills. Robust Blow and Rage are both second steps of Ferocious
	/// Strike's chain (precategory W_CHAINA_1TH_1, 3 s); Body Smash opens a chain of its own.</summary>
	private static readonly IReadOnlyDictionary<int, string> Roles = new Dictionary<int, string>
	{
		[2864] = "strike", [2865] = "strike", [2877] = "robust", [2878] = "robust", [2903] = "rage", [2890] = "smash",
	};

	/// <summary>
	/// CP-43: on the target, Ferocious Strike, then Robust Blow inside its 3 s (a follow-up is always cast first), then
	/// Rage when it is hurt, at or below 80% HP (9 physical attack and a 514 HP shield for 10 s; it follows Robust Blow by
	/// the previous chain category), then Body Smash, which is another chain's opener and resets the chain, so it goes
	/// only when no follow-up is ready. The weapon swings whenever no skill is ready; a swing is no skill and resets
	/// nothing. Nothing reaches a target that is not on the bot, so it walks in.
	/// <para>
	/// The ladder, CP-Q11: the shield scroll at 50% HP and the life potion at or below 75%; it leaves at three attackers, or
	/// at 25% HP with nothing ready.
	/// </para>
	/// </summary>
	private static readonly NaturalRotationRules Rules = new("natural-warrior-v1", Adjacent: ["strike", "robust", "rage", "smash"], AtRange: [],
		Upkeep: [],
		Recovery: [new(NaturalRecoveryKind.ShieldScroll, 50), new(NaturalRecoveryKind.LifePotion, 75)],
		SwarmAttackers: 3, FleeHpPercent: 25, AutoAttack: NaturalAutoAttack.Filler,
		OnlyWhenHurt: new Dictionary<string, int> { ["rage"] = 80 });

	/// <summary>It walks to the target and fights at the weapon's reach; after a distance refusal it comes inside reach.</summary>
	internal static readonly NaturalFightMovement Movement = new(NaturalPullStyle.WalkIn,
		MeleeReach: Navigation.NaturalCombatGeometry.MeleeReach, RangedRouteBeyond: 0, RangeRefusalCloseIn: 2);

	/// <summary>HP only: its attacks need no mana worth resting for.</summary>
	internal static readonly NaturalReadinessThresholds Readiness = new(
		BeforePull: new(80), BeforeUseBar: new(60), BetweenAdds: new(60), BeforeNamedTarget: new(80));

	/// <summary>CP-Q11: the Minor Life Elixir of trade list 721, bought at 5 or fewer up to 12, and no purchase takes
	/// Kinah below 500.</summary>
	private static readonly NaturalRestockRules Restock = new(NaturalPriestProfile.PriestLineRestock.Lines, kinahFloor: 500);

	public static NaturalClassProfile Create(StaticData data)
	{
		ArgumentNullException.ThrowIfNull(data);
		IReadOnlyDictionary<int, string> excluded = NaturalSkillCatalog.CommonExcluded;
		NaturalPriestSkill[] skills = NaturalSkillCatalog.Build(data, PlayerClass.WARRIOR, Roles, excluded);
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
