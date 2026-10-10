using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// CP-32, NR-70: the Chanter, the Priest's other choice at Ascension (docs/natural-all-classes-ntc.md). The line
/// priest-chanter plays the accepted Priest's levels 1-9 and sends SETPRO13 at Munin. The catalog is generated from the
/// shipped data to level 26, where the accepted line's Abyss-entry leg ends. It fights as the Priest it was, from range
/// first and then with the staff, with what it learns from level 10 put into the same table form; what the form cannot
/// say yet is left out with its reason and named in the plan's items after NR-70.
/// </summary>
public static class NaturalChanterProfile
{
	private const int TopLevel = 26;

	/// <summary>
	/// The Chanter's active skills to level 26, by role, beside the Priest's it keeps. Hallowed Strike opens the chain
	/// Booming Strike (level 10) and then Crashing Strike (level 13) follow; Meteor Strike (level 10) opens the one
	/// Incandescent Blow (level 19) follows; Infernal Blaze opens the one Thunderbolt Strike (level 10) follows, both
	/// from 25 m. Protectorate's Prayer (level 10) is the Blessing of Guardianship's next rank. Word of Revival (level 13)
	/// heals at once and for 38 s after. Promise of Earth (level 16) and Rage Spell (level 20) last 30 min. Protective
	/// Ward (level 22) takes three tenths of every hit for 10 s. Winter Circle is paid with 2,000 DP and doubles the
	/// physical attack for 30 s. Binding Word (level 20) halves its target's speed for 5 s and stops its physical skills.
	/// </summary>
	private static readonly IReadOnlyDictionary<int, string> Roles = new Dictionary<int, string>(NaturalPriestProfile.PriestRoles)
	{
		[1840] = "heal", [1841] = "heal", [1842] = "heal", [1843] = "heal",
		[1616] = "hallowed", [1617] = "hallowed", [1618] = "hallowed",
		[1562] = "booming", [1563] = "booming", [1564] = "booming", [1565] = "booming",
		[1703] = "crashing", [1704] = "crashing", [1705] = "crashing",
		[1778] = "meteor", [1779] = "meteor", [1780] = "meteor", [1781] = "meteor",
		[1667] = "incandescent", [1668] = "incandescent",
		[1815] = "infernal", [1816] = "infernal", [1817] = "infernal",
		[1715] = "thunderbolt", [1716] = "thunderbolt", [1717] = "thunderbolt", [1718] = "thunderbolt",
		[1638] = "circle",
		[1685] = "blessing", [1686] = "blessing", [1687] = "blessing", [1688] = "blessing",
		[1735] = "revival", [1736] = "revival", [1737] = "revival",
		[1627] = "promise", [1628] = "promise", [1629] = "promise",
		[1561] = "rage",
		[1690] = "ward",
		[1574] = "binding", [1575] = "binding",
		// The two powder skills, cast only in a rest.
		[246] = "herb", [247] = "herb", [251] = "herb", [253] = "herb",
		[249] = "mp-recovery", [250] = "mp-recovery", [252] = "mp-recovery", [254] = "mp-recovery",
	};

	/// <summary>Every other active skill a Chanter learns by itself to level 26, and why it is not cast.</summary>
	public static readonly IReadOnlyDictionary<int, string> Excluded = new Dictionary<int, string>
	{
		[1699] = "Light of Resurrection revives another player; the bot plays solo.",
		[1587] = "Booming Smash takes Booming Strike's place after Hallowed Strike: a weaker hit with an 8 s snare, after which Crashing " +
			"Strike does not follow. The table keeps the chain of three.",
		[1769] = "Parrying Strike is a counter skill: the server accepts it only within 5 s of a parry of the Chanter's own (counter_skill " +
			"PARRY, Java Skill.java 163-169), which the bot does not observe.",
	};

	/// <summary>
	/// NR-70: the Priest's table with what the Chanter adds. From range: Infernal Blaze and Thunderbolt Strike at once
	/// after it, both instant, then Smite while the monster comes. With the monster on it: Winter Circle when 2,000 DP
	/// are there; then the same pair; Hallowed Strike, Booming Strike and Crashing Strike; Meteor Strike and Incandescent
	/// Blow; Smite last. An open follow-up is always cast first. The staff swings between skills.
	/// Protectorate's Prayer goes up before the first hit, and Word of Revival is kept up while the Chanter is being hit.
	/// <para>
	/// The ladder is the Priest's with one step more: the Anti-Shock scroll at 50% HP; Protective Ward at 60%; the life
	/// potion at 90%; Healing Light at 55% against one attacker and at 70% against two or more. The potion's percentage and
	/// the heal percentages are the run's. Once a fight has had its heal and the target is at or below 15% HP, Smite
	/// finishes it in the heal's place. Healing Light's cost is kept back from every attack. The Chanter leaves at three
	/// attackers, or at 30% HP with nothing of the ladder left, and casts Binding Word on its target first.
	/// </para>
	/// </summary>
	private static readonly NaturalRotationRules Rules = new("natural-chanter-v1",
		Adjacent: ["circle", "infernal", "thunderbolt", "hallowed", "booming", "crashing", "meteor", "incandescent", "smite"],
		AtRange: ["infernal", "thunderbolt", "smite"],
		Upkeep: [new("blessing"), new("revival", DuringFight: true, UnderAttackOnly: true)],
		Recovery:
		[
			new(NaturalRecoveryKind.ShieldScroll, 50),
			new(NaturalRecoveryKind.Skill, 60, "ward"),
			new(NaturalRecoveryKind.LifePotion, 90, FromRun: NaturalRunPercent.LifePotion),
			new(NaturalRecoveryKind.Skill, 55, "heal", HpPercentMultiple: 70, FinishInstead: true, FromRun: NaturalRunPercent.Heal),
		],
		SwarmAttackers: NaturalPatrolPolicy.MaximumMembers + 1, FleeHpPercent: 30, AutoAttack: NaturalAutoAttack.Filler, ControlRole: "binding",
		EmergencyPercent: 35, EmergencyClearPercent: 45, EmergencySeasonedPairPercent: 55,
		Finisher: new("smite", 15, FromRun: true), ReserveRole: "heal", ManaPotionReserveMargin: 10, RangedHoldWithin: 12);

	// CP-29a (CP-Q7, answered 2026-10-07): the Chanter's table is the Cleric's, the staff with the most magic boost and
	// chain first. The supplies are the kit of every level: at the ceremony the level-10 bands are supplied to a Chanter
	// as to a Cleric, because the allowlist is keyed by level and not by class.
	private static readonly NaturalGearRules ChanterGear = NaturalClassGearTable.Chanter.Rules(NaturalClassLineContract.LoadDefault(),
		NaturalHelpItemAllowlist.AllLevels);

	public static NaturalClassProfile Create(StaticData data)
	{
		ArgumentNullException.ThrowIfNull(data);
		var excluded = NaturalSkillCatalog.CommonExcluded.Concat(Excluded).ToDictionary(entry => entry.Key, entry => entry.Value);
		NaturalPriestSkill[] skills = NaturalSkillCatalog.Build(data, PlayerClass.CHANTER, Roles, excluded);
		NaturalProfileValidator.Require(data, PlayerClass.CHANTER, TopLevel, skills, excluded, Rules.Lines(skills), ChanterGear);
		return new NaturalClassProfile
		{
			Class = PlayerClass.CHANTER,
			Skills = skills,
			Excluded = excluded,
			Combat = new NaturalRotationCombatPolicy(Rules, skills, NaturalPriestProfile.PriestLineMovement),
			// NR-34: as the Cleric, both kinds.
			HelpItems = NaturalHelpItemRules.ForKinds(caster: true, reagent: true),
			// The Priest's blessing in its Chanter rank, and the two buffs of 30 min.
			Upkeep = [NaturalPriestProfile.Blessing, new("promise", "buff-promise-of-earth"), new("rage", "buff-rage-spell")],
			// NR-36: what it shoots with in flight.
			AirAttackRoles = Rules.AtRange,
			// NR-37: every second class holds for a patrol and assesses the fight, by its own table.
			PatrolRule = NaturalPatrolRule.HoldAndAssess,
			Patrol = NaturalPatrolView.From(Rules, skills, PlayerClass.CHANTER),
			RangedHold = NaturalRangedHold.RunOption,
			Rest = NaturalPriestProfile.RestWith(skills),
			Ranges = NaturalPriestProfile.PriestLineRanges,
			Readiness = NaturalPriestProfile.PriestLineReadiness,
			Movement = NaturalPriestProfile.PriestLineMovement,
			Campaign = NaturalPriestProfile.PriestLineCampaign,
			Gear = ChanterGear,
			Restock = NaturalPriestProfile.PriestLineRestock,
		};
	}
}
