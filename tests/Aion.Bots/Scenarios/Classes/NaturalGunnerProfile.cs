using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// NR-120: the Gunner, the Engineer's one choice at Ascension that keeps the pistol (docs/natural-all-classes-ntc.md).
/// The catalog is generated from the shipped data to level 26, where the accepted line's Abyss-entry leg ends. It
/// fights as the Engineer it was, from the pistol's own range, with a second pistol in the off hand from level 10. What
/// the table form cannot say yet is left out with its reason and named in the plan's items after NR-120.
/// </summary>
public static class NaturalGunnerProfile
{
	private const int TopLevel = 26;

	/// <summary>
	/// The Gunner's active skills to level 26, by role. Every shot needs a pistol, and from level 10 most of what is new
	/// needs two (startconditions/lefthandweapon DUAL). Gunshot opens the chain Rapidfire follows, twice. Crosstrigger
	/// (level 22) opens the chain Canted Shot follows, twice; neither costs mana, and each takes mana from its target.
	/// Gunshot and Crosstrigger share one cooldown, 16 s after the first and 24 s after the second, so one of the two
	/// chains is opened at a time. Direct Shot is ready every 2 s and costs no mana before level 21. Hot Shot lowers its
	/// target's fire resistance and slows its attacks for 10 s. Green Grenade (level 10) roots its target for 4 s. Wing
	/// Clip (level 25) is a hard shot once a minute. Spend Success is paid with 2,000 DP: for 2 min 150 more magic boost
	/// and shots 15% faster. Bullet Resistance halves every hit for 10 s; Bulletproof (level 16) dodges every hit for 8 s.
	/// </summary>
	private static readonly IReadOnlyDictionary<int, string> Roles = new Dictionary<int, string>
	{
		[2219] = "direct", [2220] = "direct", [2221] = "direct", [2222] = "direct", [2223] = "direct", [2224] = "direct",
		[1957] = "gunshot", [1958] = "gunshot", [1959] = "gunshot", [1960] = "gunshot", [1961] = "gunshot",
		[2142] = "rapid", [2143] = "rapid", [2144] = "rapid", [2145] = "rapid", [2146] = "rapid",
		[2171] = "cross",
		[2186] = "canted",
		[1942] = "hot", [1943] = "hot", [1944] = "hot", [1945] = "hot",
		[2155] = "grenade", [2156] = "grenade", [2157] = "grenade", [2158] = "grenade",
		[1995] = "clip",
		[2007] = "success",
		[2168] = "resist", [2169] = "resist", [2170] = "resist",
		[1923] = "proof",
		// NR-50a: the two powder skills, cast only in a rest.
		[246] = "herb", [247] = "herb", [251] = "herb", [253] = "herb",
		[249] = "mp-recovery", [250] = "mp-recovery", [252] = "mp-recovery", [254] = "mp-recovery",
	};

	private const string Twice = "follows Rapidfire only after Rapidfire was cast twice (chain/precount 2; Java ChainCondition), and the " +
		"table's reading of a chain does not count the step before yet (NR-120a).";
	private const string Stagger = "staggers its target 2 m back. The server tells a client where a monster lands only inside the cast " +
		"result, which the bot does not read to its end (NR-110g, blocked by NR-Q17); a rooted monster would stay where the bot does not " +
		"see it.";
	private const string AfterTrunk = "follows Trunk Shot only, which is left out.";
	private const string Cannon = "needs an aethercannon in hand (startconditions/weapon), and the Gunner holds pistols: every other skill " +
		"of its table needs those.";
	private const string BackDash = "throws the Gunner 15 m back from its target, to a place only the cast's result names (Java sends no " +
		"forced move); the bot walks checked routes only.";

	/// <summary>Every other active skill a Gunner learns by itself to level 26, and why it is not cast.</summary>
	private static readonly IReadOnlyDictionary<int, string> Excluded = new Dictionary<int, string>
	{
		[2132] = "Automatic Fire I " + Twice, [2133] = "Automatic Fire II " + Twice, [2134] = "Automatic Fire III " + Twice,
		[2053] = "Reload makes the cooldown that Gunshot, Trunk Shot and Crosstrigger share end at once. The fight loop keeps a cooldown " +
			"from its own cast and does not read that it was ended (NR-120b).",
		[2055] = "Trunk Shot I " + Stagger, [2056] = "Trunk Shot II " + Stagger, [2057] = "Trunk Shot III " + Stagger,
		[1912] = "Volley I " + AfterTrunk, [1913] = "Volley II " + AfterTrunk, [1914] = "Volley III " + AfterTrunk,
		[2350] = "Parting Shot I " + BackDash, [2351] = "Parting Shot II " + BackDash,
		[2210] = "Harassing Fire " + Cannon,
		[2322] = "Incendiary Shell " + Cannon,
	};

	/// <summary>
	/// NR-120: the Engineer's table with the Gunner's own shots. Spend Success when 2,000 DP are there. From range Green
	/// Grenade first: a rooted monster stands 4 s in the pistol's reach. Then Hot Shot, Gunshot and Rapidfire twice;
	/// Crosstrigger and Canted Shot, which the shared cooldown leaves for the time Gunshot cannot be paid for; Wing Clip;
	/// and Direct Shot for everything between. With the monster on it the grenade comes last before Direct Shot. The
	/// table holds an open chain, as the Engineer's does, and the pistols fire whenever no skill is ready.
	/// <para>
	/// The ladder is the Engineer's (CP-Q11) with Bulletproof under it: the shield scroll at 50% HP, the life potion at
	/// or below 75%, Bullet Resistance at or below 60%, Bulletproof at or below 45%. It leaves at two attackers, or at
	/// 25% HP with nothing ready, and roots its target with Green Grenade first. Between fights it rests as the Templar
	/// (NR-50a).
	/// </para>
	/// </summary>
	private static readonly NaturalRotationRules Rules = new("natural-gunner-v1",
		Adjacent: ["success", "hot", "gunshot", "rapid", "cross", "canted", "clip", "grenade", "direct"],
		AtRange: ["success", "grenade", "hot", "gunshot", "rapid", "cross", "canted", "clip", "direct"],
		Upkeep: [],
		Recovery:
		[
			new(NaturalRecoveryKind.ShieldScroll, 50), new(NaturalRecoveryKind.LifePotion, 75), new(NaturalRecoveryKind.Skill, 60, "resist"),
			new(NaturalRecoveryKind.Skill, 45, "proof"),
		],
		SwarmAttackers: 2, FleeHpPercent: 25, AutoAttack: NaturalAutoAttack.Filler, ControlRole: "grenade", HoldOpenChain: true);

	// NR-Q5, NR-Q7 and NR-Q13: two pistols, leather first, and the kit of a class that does not cast from mana and rests
	// with the powder.
	private static readonly NaturalGearRules Gear = NaturalClassGearTable.Gunner.Rules(NaturalClassLineContract.LoadDefault(),
		NaturalHelpItemAllowlist.Kit(caster: false, reagent: true));

	public static NaturalClassProfile Create(StaticData data)
	{
		ArgumentNullException.ThrowIfNull(data);
		var excluded = NaturalSkillCatalog.CommonExcluded.Concat(Excluded).ToDictionary(entry => entry.Key, entry => entry.Value);
		NaturalPriestSkill[] skills = NaturalSkillCatalog.Build(data, PlayerClass.GUNNER, Roles, excluded);
		// A stand-off at 18 m and never a walk-in: the Engineer's own.
		NaturalFightMovement movement = NaturalEngineerProfile.Movement;
		NaturalProfileValidator.Require(data, PlayerClass.GUNNER, TopLevel, skills, excluded, Rules.Lines(skills), Gear);
		return new NaturalClassProfile
		{
			Class = PlayerClass.GUNNER,
			Skills = skills,
			Excluded = excluded,
			Combat = new NaturalRotationCombatPolicy(Rules, skills, movement),
			// NR-34, NR-Q13: no mana serum and no Awakening scroll; the powder; Courage in the shared scroll slot, as the
			// Engineer keeps it.
			HelpItems = NaturalHelpItemRules.ForKinds(caster: false, reagent: true, sharedSlotFamily: "courage"),
			Upkeep = [],
			// NR-36: in flight it fires Direct Shot, which is ready every 2 s from the pistol's reach.
			AirAttackRoles = ["direct"],
			// NR-37: every second class holds for a patrol and assesses the fight, by its own table.
			PatrolRule = NaturalPatrolRule.HoldAndAssess,
			Patrol = NaturalPatrolView.From(Rules, skills, PlayerClass.GUNNER),
			RangedHold = NaturalRangedHold.RunOption,
			// NR-50a: the powder first, then the life potion below 90% HP, then sitting.
			Rest = new NaturalRestRules(skills, HealBelowPercent: 90, ManaSitBelowPercent: 25, ManaSitUntilPercent: 50, MaximumQuietSits: 12,
				PotionPlan: new NaturalPotionRestPlan(HpTargetPercent: 90, UsesMana: true), RestSkills: NaturalRestSkills.ReagentOnly(90)),
			// The Engineer's distances, thresholds and campaign numbers, all inside the pistol's 20 m.
			Ranges = NaturalEngineerProfile.Ranges,
			Readiness = NaturalEngineerProfile.Readiness,
			Movement = movement,
			Campaign = NaturalEngineerProfile.Campaign,
			Gear = Gear,
			Restock = NaturalWarriorProfile.Restock,
		};
	}
}
