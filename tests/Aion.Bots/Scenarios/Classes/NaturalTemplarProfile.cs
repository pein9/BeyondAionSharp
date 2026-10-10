using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// NR-50: the Templar, the Warrior's choice at Ascension that holds a one-hand weapon and a shield
/// (docs/natural-all-classes-ntc.md). The catalog is generated from the shipped data to level 26, where the accepted
/// line's Abyss-entry leg ends. It fights as the Warrior it was, with what it learns from level 10 put into the same
/// table form; what the form cannot say yet is left out with its reason and named in the plan's items after NR-50.
/// </summary>
public static class NaturalTemplarProfile
{
	private const int TopLevel = 26;

	/// <summary>
	/// The Templar's active skills to level 26, by role, beside the Warrior's it keeps. Ferocious Strike opens the first
	/// chain; Robust Blow and Rage are its second steps and Wrath Strike (level 19) follows Robust Blow. Body Smash opens
	/// a chain of its own. Dazing Severe Blow (level 10) opens a third and Divine Blow (level 11) follows it. Empyrean
	/// Chastisement is paid with 2,000 DP. Empyrean Armor (level 13) heals a quarter of its HP and raises the most it
	/// has by half for three minutes. Shield Bash (level 10) stuns for 2 s and needs a shield worn (NR-50b). NR-53b: Taunt
	/// (level 10) sets a monster 15 m away on the Templar.
	/// </summary>
	private static readonly IReadOnlyDictionary<int, string> Roles = new Dictionary<int, string>
	{
		[2864] = "strike", [2865] = "strike", [2866] = "strike", [2867] = "strike", [2868] = "strike", [2869] = "strike",
		[2877] = "robust", [2878] = "robust", [2879] = "robust", [2880] = "robust", [2881] = "robust",
		[2903] = "rage", [2904] = "rage", [2905] = "rage", [2906] = "rage",
		[2890] = "smash", [2891] = "smash", [2892] = "smash", [2893] = "smash", [2894] = "smash",
		[3055] = "dazing", [3056] = "dazing", [3057] = "dazing", [3058] = "dazing",
		[3131] = "divine", [3132] = "divine", [3133] = "divine", [3134] = "divine",
		[3038] = "wrath", [3039] = "wrath",
		[3019] = "chastise", [3020] = "chastise", [3021] = "chastise", [3022] = "chastise",
		[3129] = "armor",
		[3072] = "bash", [3073] = "bash", [3074] = "bash", [3075] = "bash",
		// NR-50a: the two powder skills, cast only in a rest.
		[2981] = "taunt", [2982] = "taunt", [2983] = "taunt", [2984] = "taunt",
		[246] = "herb", [247] = "herb", [251] = "herb", [253] = "herb",
		[249] = "mp-recovery", [250] = "mp-recovery", [252] = "mp-recovery", [254] = "mp-recovery",
	};

	private const string Counter = "the client offers it only after a block or a resist (counter_skill BLOCK,RESIST), which the bot does not observe. " +
		"Java reads a counter of two statuses as none and would accept it at any time; the bot does not send what a client could not.";

	// NR-50d, NR-70a: the Templar's one toggle.
	private const string Stance = "a toggle that is a stance too: the first skill cast ends it (Java StanceObserver), and the table casts one " +
		"every second or two of a fight.";

	/// <summary>Every other active skill and every toggle a Templar learns by itself to level 26, and why it is not cast.</summary>
	private static readonly IReadOnlyDictionary<int, string> Excluded = new Dictionary<int, string>
	{
		[3000] = "Stubborn Spirit I: " + Stance, [3001] = "Stubborn Spirit II: " + Stance, [3002] = "Stubborn Spirit III: " + Stance,
		[3003] = "Stubborn Spirit IV: " + Stance,
		[3010] = "Provoking Roar raises the enmity of what is already around the Templar and deals no damage; alone they are on it anyway.",
		[3094] = "Shield Counter I: " + Counter, [3095] = "Shield Counter II: " + Counter, [3096] = "Shield Counter III: " + Counter,
		[3097] = "Shield Counter IV: " + Counter,
		[3048] = "Courageous Shield: " + Counter,
		[3085] = "Avenging Blow: " + Counter,
		[3123] = "Aether Leash drags its target to the Templar. The server says where only in the cast's result (Java PulledEffect sends " +
			"no forced move for a monster), which the bot does not read: it took the monster to be where it was and walked there (NR-53b).",
		[3124] = "Charge raises run speed for 13 s; the journey's travel casts no skill, and the kit's running scroll is its speed.",
	};

	/// <summary>
	/// NR-50: on the target, an open follow-up first: Divine Blow after Dazing Severe Blow, Robust Blow after Ferocious
	/// Strike, then Rage when it is hurt, at or below 80% HP, then Wrath Strike, each inside 3 s. Rage stands before Wrath
	/// Strike because it follows Ferocious Strike or Robust Blow and not Wrath Strike, and Wrath Strike still follows
	/// Robust Blow after it (Java ChainCondition.validate: the current or the previous chain step). Of the openers,
	/// Dazing Severe Blow goes first (it slows the target's attacks and lowers its defence for 12 s), then Shield Bash
	/// while a shield is worn (a 2 s stun once a minute, 30 to 49 MP), then Ferocious Strike, then Body Smash. Empyrean
	/// Chastisement is cast only at or below 70% HP, and then before every other opener: its 2,000 DP also buy a shield
	/// that takes half of every hit for 15 s, which is worth most at the start of what is left of the fight. NR-51: it
	/// stood last at first, and a monster of the route was dead before its turn came. The weapon swings whenever no
	/// skill is ready.
	/// <para>
	/// NR-53b: it pulls, as a tank does. From range: Taunt (free, 10 s), and then it holds where it stands until the
	/// monster is on it. Taunt is a pull and no attack: once the fight is on it is not cast again, and a monster that
	/// does not come, because it attacks from range or runs at its last HP, is walked to. It walked in at first, as the Warrior, and in
	/// Altgard that put it inside every pack: in leg 4 it left thirteen fights at three attackers and was refused 22
	/// walks. A target that attacks from range does not come to a Taunt; the Templar walks to that one.
	/// </para>
	/// <para>
	/// The ladder: the shield scroll at 50% HP, the life potion at or below 75%, and Empyrean Armor in an emergency only
	/// (35% until 45%), whose 113 MP are kept back from Rage. It leaves at three attackers, or at 25% HP with nothing ready.
	/// </para>
	/// <para>
	/// NR-50a, between fights: Herb Treatment below 90% HP and MP Recovery from below 25% MP until 50%, each for the powder
	/// it owns and while their shared 16 s are not running; then the life potion below 90% HP, then sitting. A quarter of
	/// its mana pays for the armor and several Rages at every level from 13 (1,050 MP) on.
	/// </para>
	/// </summary>
	private static readonly NaturalRotationRules Rules = new("natural-templar-v1",
		Adjacent: ["chastise", "dazing", "divine", "bash", "strike", "robust", "rage", "wrath", "smash"], AtRange: ["taunt"],
		Upkeep: [],
		Recovery:
		[
			new(NaturalRecoveryKind.ShieldScroll, 50),
			new(NaturalRecoveryKind.LifePotion, 75),
			new(NaturalRecoveryKind.Skill, 35, "armor", EmergencyOnly: true),
		],
		SwarmAttackers: 3, FleeHpPercent: 25, AutoAttack: NaturalAutoAttack.Filler,
		OnlyWhenHurt: new Dictionary<string, int> { ["rage"] = 80, ["chastise"] = 70 }, PullRoles: ["taunt"]);

	/// <summary>NR-53b: the distances of a pull with a 15 m skill, as the Priest line's are those of 25 m: the planned
	/// pull opens at 13 m, a far target is approached to 10 m with sight of it, and the stand-off is held at 11 m (15 m
	/// less the arrival tolerance and the margin). The approach must end inside the stand-off, or no point of its route
	/// is one to cast from. NR-54c: the fight-through's firing range is not the pull's: it says which of a route's
	/// blockers is taken, from the route's last point outside its circle, and the pull is then planned from there. It is
	/// the one every line that has played uses; at 14 m a patrol's blocker on the road was never taken.</summary>
	internal static readonly NaturalEngageRanges Ranges = new(
		MeleeReach: Navigation.NaturalCombatGeometry.MeleeReach, SpellRange: 14, PullDistance: 13,
		FiringRange: Navigation.NaturalFightThrough.FiringRange,
		SpawnApproachRange: 13, SpawnPullScanRange: 20, FightThroughPullRange: 20,
		StandoffSpellRange: 15, StandoffArrivalTolerance: 3, StandoffSafetyMargin: 1, RangedApproachRadius: 10);

	/// <summary>NR-53b: a stand-off: a ranged route while the target is farther than 15 m, then up to it; after a distance
	/// refusal come to 10 m, or inside melee reach for a melee skill. NR-53c: when it walks up, it walks to the monster it
	/// fights and to no other of its kind.</summary>
	internal static readonly NaturalFightMovement Movement = new(NaturalPullStyle.StandOff,
		MeleeReach: Navigation.NaturalCombatGeometry.MeleeReach, RangedRouteBeyond: 15, RangeRefusalCloseIn: 10, WalksToItsTarget: true);

	// NR-Q5, NR-Q7 and NR-Q13: a one-hand weapon and a shield, plate first, and the kit of a class that does not cast
	// from mana and rests with the powder.
	private static readonly NaturalGearRules Gear = NaturalClassGearTable.Templar.Rules(NaturalClassLineContract.LoadDefault(),
		NaturalHelpItemAllowlist.Kit(caster: false, reagent: true));

	public static NaturalClassProfile Create(StaticData data)
	{
		ArgumentNullException.ThrowIfNull(data);
		var excluded = NaturalSkillCatalog.CommonExcluded.Concat(Excluded).ToDictionary(entry => entry.Key, entry => entry.Value);
		NaturalPriestSkill[] skills = NaturalSkillCatalog.Build(data, PlayerClass.TEMPLAR, Roles, excluded);
		NaturalProfileValidator.Require(data, PlayerClass.TEMPLAR, TopLevel, skills, excluded, Rules.Lines(skills), Gear);
		return new NaturalClassProfile
		{
			Class = PlayerClass.TEMPLAR,
			Skills = skills,
			Excluded = excluded,
			Combat = new NaturalRotationCombatPolicy(Rules, skills, Movement),
			// NR-34, NR-Q13: no mana serum and no Awakening scroll; the powder; Courage in the shared scroll slot.
			HelpItems = NaturalHelpItemRules.ForKinds(caster: false, reagent: true, sharedSlotFamily: "courage"),
			Upkeep = [],
			// NR-36: nothing it learns by level 12 hurts a target from range, so in flight it swings its weapon.
			AirAttackRoles = [],
			// NR-37: every second class holds for a patrol and assesses the fight, by its own table.
			PatrolRule = NaturalPatrolRule.HoldAndAssess,
			Patrol = NaturalPatrolView.From(Rules, skills, PlayerClass.TEMPLAR),
			RangedHold = NaturalRangedHold.Never,
			// NR-50a: the powder first, then as the Warrior: the life potion below 90% HP, then sitting.
			Rest = new NaturalRestRules(skills, HealBelowPercent: 90, ManaSitBelowPercent: 25, ManaSitUntilPercent: 50, MaximumQuietSits: 12,
				PotionPlan: new NaturalPotionRestPlan(HpTargetPercent: 90, UsesMana: true), RestSkills: NaturalRestSkills.ReagentOnly(90)),
			Ranges = Ranges,
			Readiness = NaturalWarriorProfile.Readiness,
			Movement = Movement,
			Campaign = NaturalPriestProfile.PriestLineCampaign,
			Gear = Gear,
			Restock = NaturalWarriorProfile.Restock,
		};
	}
}
