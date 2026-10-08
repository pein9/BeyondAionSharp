using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// CP-49: the Scout, levels 1-9 (docs/natural-class-profiles.md). Its catalog is generated from the shipped data, so the
/// profile is built from the static data of the run. It walks in and fights at the dagger's reach, as the Warrior does
/// with its sword.
/// </summary>
public static class NaturalScoutProfile
{
	private const int TopLevel = 9;

	/// <summary>The Scout's five skills that are cast. Swift Edge opens the chain Soul Slash follows inside 3 s. Devotion
	/// and Focused Evasion have no chain, so either of them between the two loses the follow-up.</summary>
	private static readonly IReadOnlyDictionary<int, string> Roles = new Dictionary<int, string>
	{
		[3182] = "edge", [3183] = "edge", [3223] = "slash", [3235] = "devotion", [3195] = "evasion",
	};

	private static readonly IReadOnlyDictionary<int, string> Excluded = new Dictionary<int, string>(NaturalSkillCatalog.CommonExcluded)
	{
		[3196] = "Surprise Attack does little from the front, opens its own chain and so resets Swift Edge's; its back damage needs a position the bot does not take (CP-Q16).",
		[3197] = "Surprise Attack, rank 2: as rank 1 (CP-Q16).",
		[3209] = "Counterattack needs the bot to observe its own dodge (CP-Q16).",
		[3222] = "Stealth cannot be cast in combat and the journey does not sneak (CP-Q16).",
	};

	/// <summary>
	/// With the monster in reach: Devotion (level 9; 40% more physical attack for 5 s) when it is ready, then Swift Edge,
	/// then Soul Slash at once; an open follow-up is always cast first, so Devotion goes before the opener and never
	/// between the two. The dagger swings whenever no skill is ready. Nothing is cast from range.
	/// <para>
	/// The ladder, CP-Q11: Focused Evasion (5 s in which every hit misses, no mana) at or below 70% HP, the shield scroll
	/// at 50%, the life potion at or below 75%. The table holds an open chain, so Focused Evasion is not cast while Soul
	/// Slash is open, outside an emergency. The Scout leaves at two attackers, or at 25% HP with nothing ready.
	/// </para>
	/// </summary>
	private static readonly NaturalRotationRules Rules = new("natural-scout-v1", Adjacent: ["devotion", "edge", "slash"], AtRange: [],
		Upkeep: [],
		Recovery: [new(NaturalRecoveryKind.Skill, 70, "evasion"), new(NaturalRecoveryKind.ShieldScroll, 50), new(NaturalRecoveryKind.LifePotion, 75)],
		SwarmAttackers: 2, FleeHpPercent: 25, AutoAttack: NaturalAutoAttack.Filler, HoldOpenChain: true);

	/// <summary>CP-Q11: the Minor Life Elixir of trade list 721, bought at 5 or fewer up to 12, and no purchase takes
	/// Kinah below 500.</summary>
	private static readonly NaturalRestockRules Restock = new(NaturalPriestProfile.PriestLineRestock.Lines, kinahFloor: 500);

	public static NaturalClassProfile Create(StaticData data)
	{
		ArgumentNullException.ThrowIfNull(data);
		NaturalPriestSkill[] skills = NaturalSkillCatalog.Build(data, PlayerClass.SCOUT, Roles, Excluded);
		NaturalGearRules gear = NaturalClassGearTable.Scout.Rules(NaturalClassLineContract.LoadDefault());
		// The walk to weapon reach and the swing are the Warrior's, proven in CP-43.
		NaturalFightMovement movement = NaturalWarriorProfile.Movement;
		NaturalProfileValidator.Require(data, PlayerClass.SCOUT, TopLevel, skills, Excluded, Rules.Lines(skills), gear);
		return new NaturalClassProfile
		{
			Class = PlayerClass.SCOUT,
			Skills = skills,
			Excluded = Excluded,
			Combat = new NaturalRotationCombatPolicy(Rules, skills, movement),
			// CP-05: the level 1-9 kit. The shared speed slot holds the attack-speed scroll (Blitzopan, the Courage family).
			HelpItems = new(NaturalHelpItemAllowlist.Starter, NaturalHelpItemAllowlist.StarterMaxLevel, SharedSlotFamily: "courage"),
			Upkeep = [],
			PatrolRule = NaturalPatrolRule.Baseline,
			RangedHold = NaturalRangedHold.Never,
			// CP-Q11: a life potion below 90% HP when it is ready, a sit to 90% while it is on its delay. No mana target:
			// only Devotion costs mana. No bandage.
			Rest = new NaturalRestRules(skills, HealBelowPercent: 90, ManaSitBelowPercent: 50, ManaSitUntilPercent: 80, MaximumQuietSits: 12,
				PotionPlan: new NaturalPotionRestPlan(HpTargetPercent: 90, UsesMana: false)),
			// The Priest line's distances and campaign numbers, as the Warrior starts with (CP-18, CP-21).
			Ranges = NaturalPriestProfile.PriestLineRanges,
			Readiness = NaturalWarriorProfile.Readiness,
			Movement = movement,
			Campaign = NaturalPriestProfile.PriestLineCampaign,
			Gear = gear,
			Restock = Restock,
		};
	}
}
