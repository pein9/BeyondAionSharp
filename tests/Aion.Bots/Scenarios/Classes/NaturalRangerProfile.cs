using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// NR-100: the Ranger, the Scout's choice at Ascension that holds a bow (docs/natural-all-classes-ntc.md). The catalog
/// is generated from the shipped data to level 26, where the accepted line's Abyss-entry leg ends. The Scout it was
/// walked in with its daggers; the Ranger shoots from a stand-off, by the table form the Engineer's pistol uses. What the
/// form cannot say is left out with its reason.
/// </summary>
public static class NaturalRangerProfile
{
	private const int TopLevel = 26;

	/// <summary>
	/// The Ranger's active skills to level 26, by role. Every shot needs a bow. Swift Shot (level 10) opens the chain
	/// Arrow Strike (level 13) follows; neither costs mana. Stunning Shot (level 10) reaches 20 m and opens the chain
	/// Rupture Arrow (level 25) follows. Entangling Shot (level 10) slows its target by three fifths for 16 to 20 s.
	/// Deadshot (level 10) is ready every 2 s for 39 MP and more. Transformation: Mau is paid with 2,000 DP: for 2 min a
	/// quarter more attack, a quarter faster shots and two fifths more speed. Sleep Arrow (level 19) puts its target to
	/// sleep for 12 s, until it is hit. Devotion and Focused Evasion are the Scout's and need no weapon.
	/// </summary>
	private static readonly IReadOnlyDictionary<int, string> Roles = new Dictionary<int, string>
	{
		[994] = "swift", [995] = "swift", [996] = "swift", [997] = "swift",
		[939] = "arrow", [940] = "arrow", [941] = "arrow",
		[784] = "stun", [785] = "stun", [786] = "stun", [787] = "stun",
		[1102] = "rupture",
		[950] = "entangle", [951] = "entangle", [952] = "entangle", [953] = "entangle",
		[1010] = "deadshot", [1011] = "deadshot", [1012] = "deadshot", [1013] = "deadshot",
		[872] = "mau",
		[896] = "sleep", [897] = "sleep",
		[3235] = "devotion",
		[3195] = "evasion",
		// NR-50a: the two powder skills, cast only in a rest.
		[246] = "herb", [247] = "herb", [251] = "herb", [253] = "herb",
		[249] = "mp-recovery", [250] = "mp-recovery", [252] = "mp-recovery", [254] = "mp-recovery",
	};

	private const string Melee = "needs a dagger or a sword in hand (startconditions/weapon), and the Ranger holds a bow.";
	private const string Counter = "the server takes it only within 5 s of a dodge of the Ranger's own (counter_skill DODGE, Java Skill.java " +
		"163-169), which the bot does not observe.";
	private const string Trap = "lays a trap at the Ranger's feet for a monster to walk into. The table pulls a monster to a stand-off 18 m " +
		"away and has no rule that lays a trap on its way.";
	private const string Minute = "lasts a minute; the table spends its mana on shots, and Deadshot is 39 MP.";

	/// <summary>Every other active skill a Ranger learns by itself to level 26, and why it is not cast.</summary>
	private static readonly IReadOnlyDictionary<int, string> Excluded = new Dictionary<int, string>
	{
		[3182] = "Swift Edge I " + Melee, [3183] = "Swift Edge II " + Melee, [3184] = "Swift Edge III " + Melee,
		[3185] = "Swift Edge IV " + Melee, [3186] = "Swift Edge V " + Melee, [3187] = "Swift Edge VI " + Melee,
		[3223] = "Soul Slash I " + Melee, [3224] = "Soul Slash II " + Melee, [3225] = "Soul Slash III " + Melee, [3226] = "Soul Slash IV " + Melee,
		[3196] = "Surprise Attack I " + Melee, [3197] = "Surprise Attack II " + Melee, [3198] = "Surprise Attack III " + Melee,
		[3199] = "Surprise Attack IV " + Melee, [3200] = "Surprise Attack V " + Melee,
		[3209] = "Counterattack I " + Melee, [3210] = "Counterattack II " + Melee, [3211] = "Counterattack III " + Melee,
		[3212] = "Counterattack IV " + Melee, [3213] = "Counterattack V " + Melee,
		[1075] = "Seizure Arrow I: " + Counter, [1076] = "Seizure Arrow II: " + Counter, [1077] = "Seizure Arrow III: " + Counter,
		[3222] = "Stealth cannot be cast in combat and the journey does not sneak (CP-Q16).",
		[925] = "Spike Trap I " + Trap, [926] = "Spike Trap II " + Trap, [927] = "Spike Trap III " + Trap,
		[1037] = "Poisoning Trap I " + Trap, [1038] = "Poisoning Trap II " + Trap, [1039] = "Poisoning Trap III " + Trap,
		[1090] = "Retreating Slash throws the Ranger 25 m back from its target, to a place only the cast's result names (Java sends no " +
			"forced move); the bot walks checked routes only.",
		[809] = "Dodging, 200 evasion for 68 MP, " + Minute,
		[1053] = "Aiming, 200 accuracy for 84 MP, " + Minute,
		[796] = "Strong Shots, 5% more attack with a bow for 97 MP, " + Minute,
		[1066] = "Silence Arrow silences its target for 8 to 10 s and hits for little; the table has no rule for a target that casts.",
		[3273] = "Calming Whisper I lowers the enmity of its target; alone, the monster has no one else to turn to.",
		[3274] = "Calming Whisper II lowers the enmity of its target; alone, the monster has no one else to turn to.",
	};

	/// <summary>
	/// NR-100: the shots, in the form of the Engineer's table. From range: Transformation: Mau when 2,000 DP are there
	/// and Devotion when it is ready; then Entangling Shot, which keeps the monster on its way longer; Stunning Shot and
	/// Rupture Arrow; Swift Shot and Arrow Strike; Deadshot whenever nothing else is ready. With the monster on it the
	/// two chains come before Entangling Shot. An open follow-up is always cast first, and the bow shoots whenever no
	/// skill is ready.
	/// <para>
	/// The ladder is the Scout's (CP-Q11): Focused Evasion at or below 70% HP, the shield scroll at 50%, the life potion
	/// at or below 75%. It leaves at two attackers, as the Engineer does, or at 25% HP with nothing ready, and puts its
	/// target to sleep with Sleep Arrow first. Between fights it rests as the Templar (NR-50a).
	/// </para>
	/// </summary>
	private static readonly NaturalRotationRules Rules = new("natural-ranger-v1",
		Adjacent: ["mau", "devotion", "stun", "rupture", "swift", "arrow", "entangle", "deadshot"],
		AtRange: ["mau", "devotion", "entangle", "stun", "rupture", "swift", "arrow", "deadshot"],
		Upkeep: [],
		Recovery: [new(NaturalRecoveryKind.Skill, 70, "evasion"), new(NaturalRecoveryKind.ShieldScroll, 50), new(NaturalRecoveryKind.LifePotion, 75)],
		SwarmAttackers: 2, FleeHpPercent: 25, AutoAttack: NaturalAutoAttack.Filler, ControlRole: "sleep");

	// NR-Q5, NR-Q7 and NR-Q13: a bow, leather first, and the kit of a class that does not cast from mana and rests with
	// the powder.
	private static readonly NaturalGearRules Gear = NaturalClassGearTable.Ranger.Rules(NaturalClassLineContract.LoadDefault(),
		NaturalHelpItemAllowlist.Kit(caster: false, reagent: true));

	public static NaturalClassProfile Create(StaticData data)
	{
		ArgumentNullException.ThrowIfNull(data);
		var excluded = NaturalSkillCatalog.CommonExcluded.Concat(Excluded).ToDictionary(entry => entry.Key, entry => entry.Value);
		NaturalPriestSkill[] skills = NaturalSkillCatalog.Build(data, PlayerClass.RANGER, Roles, excluded);
		// A stand-off at 18 m and never a walk-in: the Engineer's, whose pistol reaches 20 m as Stunning Shot does. The
		// bow itself reaches 25 m.
		NaturalFightMovement movement = NaturalEngineerProfile.Movement;
		NaturalProfileValidator.Require(data, PlayerClass.RANGER, TopLevel, skills, excluded, Rules.Lines(skills), Gear);
		return new NaturalClassProfile
		{
			Class = PlayerClass.RANGER,
			Skills = skills,
			Excluded = excluded,
			Combat = new NaturalRotationCombatPolicy(Rules, skills, movement),
			// NR-34, NR-Q13: no mana serum and no Awakening scroll; the powder; Courage in the shared scroll slot.
			HelpItems = NaturalHelpItemRules.ForKinds(caster: false, reagent: true, sharedSlotFamily: "courage"),
			Upkeep = [],
			// NR-36: in flight it shoots Deadshot, which is ready every 2 s from the bow's reach.
			AirAttackRoles = ["deadshot"],
			// NR-37: every second class holds for a patrol and assesses the fight, by its own table.
			PatrolRule = NaturalPatrolRule.HoldAndAssess,
			Patrol = NaturalPatrolView.From(Rules, skills, PlayerClass.RANGER),
			RangedHold = NaturalRangedHold.RunOption,
			// NR-50a: the powder first, then the life potion below 90% HP, then sitting.
			Rest = new NaturalRestRules(skills, HealBelowPercent: 90, ManaSitBelowPercent: 25, ManaSitUntilPercent: 50, MaximumQuietSits: 12,
				PotionPlan: new NaturalPotionRestPlan(HpTargetPercent: 90, UsesMana: true), RestSkills: NaturalRestSkills.ReagentOnly(90)),
			// The Engineer's distances, thresholds and campaign numbers, all inside 20 m.
			Ranges = NaturalEngineerProfile.Ranges,
			Readiness = NaturalEngineerProfile.Readiness,
			Movement = movement,
			Campaign = NaturalEngineerProfile.Campaign,
			Gear = Gear,
			Restock = NaturalWarriorProfile.Restock,
		};
	}
}
