using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// NR-80: the Gladiator, the Warrior's choice at Ascension that holds a two-hand weapon
/// (docs/natural-all-classes-ntc.md). The catalog is generated from the shipped data to level 26, where the accepted
/// line's Abyss-entry leg ends. It fights as the Warrior it was, with what it learns from level 10 put into the same
/// table form; what the form cannot say yet is left out with its reason and named in the plan's items after NR-80.
/// </summary>
public static class NaturalGladiatorProfile
{
	private const int TopLevel = 26;

	/// <summary>
	/// The Gladiator's active skills to level 26, by role, beside the Warrior's it keeps. Ferocious Strike opens the first
	/// chain; Robust Blow and Rage are its second steps, and Wrathful Strike (level 13) and Rupture (level 16) both follow
	/// Robust Blow. Body Smash opens a chain of its own. Explosion of Rage is paid with 2,000 DP: it cannot miss and
	/// throws its target down for 2 s. Cleave (level 19) is thrown from 15 m and may halve its target's speed. Taunt
	/// (level 10) sets a monster 15 m away on the Gladiator. Second Wind (level 20) heals 35% of its HP and raises the
	/// most it has by as much for a minute. Aerial Lockdown (level 22) lifts its target for 2 s, and Crashing Blow
	/// (level 25) hits a target that is in the air (NR-80a). Slaughter (level 13) is a toggle: 15% more physical attack
	/// while it is on.
	/// </summary>
	private static readonly IReadOnlyDictionary<int, string> Roles = new Dictionary<int, string>
	{
		[2864] = "strike", [2865] = "strike", [2866] = "strike", [2867] = "strike", [2868] = "strike", [2869] = "strike",
		[2877] = "robust", [2878] = "robust", [2879] = "robust", [2880] = "robust", [2881] = "robust",
		[2903] = "rage", [2904] = "rage", [2905] = "rage", [2906] = "rage",
		[2890] = "smash", [2891] = "smash", [2892] = "smash", [2893] = "smash", [2894] = "smash",
		[624] = "wrathful", [625] = "wrathful", [626] = "wrathful",
		[739] = "rupture", [740] = "rupture", [741] = "rupture",
		[519] = "explosion", [520] = "explosion", [521] = "explosion", [522] = "explosion",
		[710] = "cleave", [711] = "cleave",
		[2981] = "taunt", [2982] = "taunt", [2983] = "taunt", [2984] = "taunt",
		[648] = "wind",
		[545] = "aerial",
		[508] = "crashing",
		[697] = "slaughter",
		// NR-50a: the two powder skills, cast only in a rest.
		[246] = "herb", [247] = "herb", [251] = "herb", [253] = "herb",
		[249] = "mp-recovery", [250] = "mp-recovery", [252] = "mp-recovery", [254] = "mp-recovery",
	};

	private const string Area = "hits up to three monsters within 7 m of the Gladiator. The bot pulls one at a time, and an area skill wakes " +
		"every other one in reach.";

	/// <summary>Every other active skill and toggle a Gladiator learns by itself to level 26, and why it is not cast.</summary>
	private static readonly IReadOnlyDictionary<int, string> Excluded = new Dictionary<int, string>
	{
		[769] = "Absorbing Fury I " + Area, [770] = "Absorbing Fury II " + Area, [771] = "Absorbing Fury III " + Area,
		[772] = "Absorbing Fury IV " + Area,
		[758] = "Roiling Hack follows Absorbing Fury and " + Area,
		[3124] = "Charge raises run speed for 13 s; the journey's travel casts no skill, and the kit's running scroll is its speed.",
		[619] = "Defense Preparation is a toggle of Slaughter's kind, and the server keeps one of them on (Java EffectController.addEffect " +
			"76-86). It raises parry, block and enmity; Slaughter's 15% of attack is kept.",
	};

	/// <summary>
	/// NR-80: the Warrior's table with what the Gladiator adds. On the target, Explosion of Rage first when 2,000 DP are
	/// there; then Ferocious Strike and Robust Blow, Rage when it is hurt, at or below 80% HP, and after Robust Blow
	/// Wrathful Strike when it is ready (20 s; seven times in ten it throws the target down for 2 s) and then Rupture
	/// (8 s, the harder hit, no mana). Both follow Robust Blow, Rupture by the step before the current one (Java
	/// ChainCondition.validate), and Wrathful Strike keeps the chain open: a skill without chain_skill_prob has 100.
	/// After Rupture the chain is over nine times in ten (chain_skill_prob 10); the cast's result says which. Then Body
	/// Smash, Aerial Lockdown and Cleave. An open follow-up is always cast first, and the weapon swings whenever no
	/// skill is ready. NR-80a: Crashing Blow is cast before anything else while the target is seen in the air, which it
	/// is for 2 s after an Aerial Lockdown it did not resist.
	/// <para>
	/// It pulls as the Templar does (NR-53b). From range: Cleave from level 19, which hits and may slow the monster on
	/// its way, and otherwise Taunt (free, 10 s); then it holds where it stands until the monster is on it. Taunt is a
	/// pull and no attack: once the fight is on it is not cast again.
	/// </para>
	/// <para>
	/// The ladder: the shield scroll at 50% HP, the life potion at or below 75%, and Second Wind at or below 45%, whose
	/// 298 MP are kept back from every attack. It leaves at three attackers, or at 25% HP with nothing ready. Between
	/// fights it rests as the Templar (NR-50a) and keeps Slaughter on (NR-70a).
	/// </para>
	/// </summary>
	private static readonly NaturalRotationRules Rules = new("natural-gladiator-v1",
		Adjacent: ["explosion", "strike", "robust", "rage", "wrathful", "rupture", "smash", "aerial", "crashing", "cleave"],
		AtRange: ["cleave", "taunt"],
		Upkeep: [],
		Recovery:
		[
			new(NaturalRecoveryKind.ShieldScroll, 50),
			new(NaturalRecoveryKind.LifePotion, 75),
			new(NaturalRecoveryKind.Skill, 45, "wind"),
		],
		SwarmAttackers: 3, FleeHpPercent: 25, AutoAttack: NaturalAutoAttack.Filler,
		OnlyWhenHurt: new Dictionary<string, int> { ["rage"] = 80 }, PullRoles: ["taunt"]);

	/// <summary>NR-70a: the one toggle kept on. The server keeps one of its kind.</summary>
	private static readonly NaturalKeptToggle[] KeptToggles = [new("slaughter", "toggle-slaughter")];

	// NR-Q5, NR-Q7 and NR-Q13: a two-hand weapon, the greatsword before the polearm, plate first, and the kit of a class
	// that does not cast from mana and rests with the powder.
	private static readonly NaturalGearRules Gear = NaturalClassGearTable.Gladiator.Rules(NaturalClassLineContract.LoadDefault(),
		NaturalHelpItemAllowlist.Kit(caster: false, reagent: true));

	public static NaturalClassProfile Create(StaticData data)
	{
		ArgumentNullException.ThrowIfNull(data);
		var excluded = NaturalSkillCatalog.CommonExcluded.Concat(Excluded).ToDictionary(entry => entry.Key, entry => entry.Value);
		NaturalPriestSkill[] skills = NaturalSkillCatalog.Build(data, PlayerClass.GLADIATOR, Roles, excluded);
		NaturalProfileValidator.Require(data, PlayerClass.GLADIATOR, TopLevel, skills, excluded, Rules.Lines(skills), Gear, KeptToggles);
		return new NaturalClassProfile
		{
			Class = PlayerClass.GLADIATOR,
			Skills = skills,
			Excluded = excluded,
			Combat = new NaturalRotationCombatPolicy(Rules, skills, NaturalTemplarProfile.Movement),
			// NR-34, NR-Q13: no mana serum and no Awakening scroll; the powder; Courage in the shared scroll slot.
			HelpItems = NaturalHelpItemRules.ForKinds(caster: false, reagent: true, sharedSlotFamily: "courage"),
			Upkeep = [],
			Toggles = KeptToggles,
			// NR-36: Cleave reaches 15 m from level 19; before it, in flight it swings its weapon.
			AirAttackRoles = ["cleave"],
			// NR-37: every second class holds for a patrol and assesses the fight, by its own table.
			PatrolRule = NaturalPatrolRule.HoldAndAssess,
			Patrol = NaturalPatrolView.From(Rules, skills, PlayerClass.GLADIATOR),
			RangedHold = NaturalRangedHold.Never,
			// NR-50a: the powder first, then as the Warrior: the life potion below 90% HP, then sitting.
			Rest = new NaturalRestRules(skills, HealBelowPercent: 90, ManaSitBelowPercent: 25, ManaSitUntilPercent: 50, MaximumQuietSits: 12,
				PotionPlan: new NaturalPotionRestPlan(HpTargetPercent: 90, UsesMana: true), RestSkills: NaturalRestSkills.ReagentOnly(90)),
			// NR-53b: the distances and the movement of a pull with a 15 m skill, the Templar's.
			Ranges = NaturalTemplarProfile.Ranges,
			Readiness = NaturalWarriorProfile.Readiness,
			Movement = NaturalTemplarProfile.Movement,
			Campaign = NaturalPriestProfile.PriestLineCampaign,
			Gear = Gear,
			Restock = NaturalWarriorProfile.Restock,
		};
	}
}
