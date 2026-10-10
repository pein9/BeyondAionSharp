using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// NR-140: the Bard, the Artist's one choice at Ascension, which keeps the harp (docs/natural-all-classes-ntc.md). The
/// catalog is generated from the shipped data to level 26, where the accepted line's Abyss-entry leg ends. It fights as
/// the Artist it was, with what it learns from level 10 put into the same table form.
/// </summary>
public static class NaturalBardProfile
{
	private const int TopLevel = 26;

	/// <summary>
	/// The Bard's active skills to level 26, by role, beside the Artist's it keeps. Every one of its own needs a harp in
	/// hand. Song of Ice opens the chain Song of Fire (level 10) and then Song of Earth (level 15) follow, each inside
	/// 3 s; Soothing Melody opens the one Soothing Counterpoint (level 17) follows. Syncopated Echo hits 5 s after its
	/// cast. Minstrel's Flair is paid with 2,000 DP. Resonating Melody gives mana at no cost, once in 30 s. Protective Ode
	/// is a shield for 5 min and Etude (level 22) a tenth more HP for an hour.
	/// </summary>
	private static readonly IReadOnlyDictionary<int, string> Roles = new Dictionary<int, string>
	{
		[4408] = "pulse", [4409] = "pulse", [4410] = "pulse", [4411] = "pulse", [4412] = "pulse", [4413] = "pulse",
		[4221] = "ice", [4222] = "ice", [4223] = "ice", [4224] = "ice", [4225] = "ice",
		[4234] = "fire", [4235] = "fire", [4236] = "fire", [4237] = "fire",
		[4253] = "earth", [4254] = "earth", [4255] = "earth",
		[4285] = "echo", [4286] = "echo", [4287] = "echo", [4288] = "echo",
		[4457] = "flair", [4458] = "flair", [4459] = "flair", [4460] = "flair",
		[4264] = "strike",
		[4339] = "heal", [4340] = "heal", [4341] = "heal", [4342] = "heal", [4343] = "heal",
		[4352] = "counterpoint", [4353] = "counterpoint",
		[4402] = "captivate",
		[4431] = "ode", [4432] = "ode", [4433] = "ode", [4434] = "ode",
		[4444] = "etude",
		// NR-60a: Resonating Melody, cast by the table's mana step and first in a rest for mana.
		[4372] = "resonate", [4373] = "resonate", [4374] = "resonate", [4375] = "resonate",
		// NR-50a: the two powder skills, cast only in a rest.
		[246] = "herb", [247] = "herb", [251] = "herb", [253] = "herb",
		[249] = "mp-recovery", [250] = "mp-recovery", [252] = "mp-recovery", [254] = "mp-recovery",
	};

	private const string Charge = "is a charge skill; the bot holds no charge (CP-Q16).";

	/// <summary>Every other active skill a Bard learns by itself to level 26, and why it is not cast.</summary>
	private static readonly IReadOnlyDictionary<int, string> Excluded = new Dictionary<int, string>
	{
		[4300] = "Fiery Descant I " + Charge, [4303] = "Fiery Descant II " + Charge, [4306] = "Fiery Descant III " + Charge,
		[4309] = "Fiery Descant IV " + Charge,
		[4406] = "Requiem puts up to six monsters within 7 m to sleep for 6 s. The bot pulls one monster at a time, and an area skill " +
			"wakes every other one in reach.",
		[4407] = "Sonicportation throws the Bard to a place the server picks, for 323 MP; the bot walks checked routes only.",
		[4443] = "Purifying Paean removes a physical debuff; no rule reads the bot's own debuffs.",
		[4580] = "Soaring Sonnet gives back flight time to a group in flight for 89 MP; the journey's flights are short.",
	};

	/// <summary>
	/// NR-140: the Artist's table with what the Bard adds. From range: Song of Ice, then Song of Fire and Song of Earth
	/// at once, an open follow-up always first; Syncopated Echo early, since it lands 5 s later; Minstrel's Flair when
	/// 2,000 DP are there; Bright Strike; and Pulse for the pull and everything between. With the monster on it the
	/// instants come first, the ones a hit cannot push back: Syncopated Echo, Bright Strike and Minstrel's Flair, then the
	/// songs and Pulse. Protective Ode goes up before the first hit. Captivate is cast only on the way out, before a
	/// retreat.
	/// <para>
	/// The ladder is the Artist's (CP-Q11), with the heal's follow-up: the shield scroll at 50% HP, the life potion at or
	/// below 75%, and at or below 55% Soothing Counterpoint while Soothing Melody has opened it, else Soothing Melody. The
	/// attacks keep Soothing Melody's mana back. It leaves at two attackers, or at 25% HP with nothing ready; the mana
	/// potion only when the cheapest attack cannot be paid. The harp swings only when no attack can be paid for.
	/// </para>
	/// <para>
	/// NR-60a: Resonating Melody gives 765 MP at once in its fourth rank and 240 every 2 s for 30 s more, at no cost,
	/// once in 30 s. In a fight it is cast at or below half its mana, before any mana potion; in a rest for mana it is
	/// cast first.
	/// </para>
	/// </summary>
	private static readonly NaturalRotationRules Rules = new("natural-bard-v1",
		Adjacent: ["echo", "strike", "flair", "ice", "fire", "earth", "pulse"],
		AtRange: ["ice", "fire", "earth", "echo", "flair", "strike", "pulse"],
		Upkeep: [new("ode")],
		Recovery:
		[
			new(NaturalRecoveryKind.ShieldScroll, 50),
			new(NaturalRecoveryKind.LifePotion, 75),
			new(NaturalRecoveryKind.Skill, 55, "counterpoint"),
			new(NaturalRecoveryKind.Skill, 55, "heal"),
		],
		SwarmAttackers: 2, FleeHpPercent: 25, AutoAttack: NaturalAutoAttack.LastResort, ControlRole: "captivate",
		ReserveRole: "heal", ManaSkill: new("resonate", 50));

	// NR-Q5, NR-Q7 and NR-Q8: the harp, cloth, and the kit of a class that casts from mana and rests with the powder.
	private static readonly NaturalGearRules Gear = NaturalClassGearTable.Bard.Rules(NaturalClassLineContract.LoadDefault(),
		NaturalHelpItemAllowlist.Kit(caster: true, reagent: true));

	public static NaturalClassProfile Create(StaticData data)
	{
		ArgumentNullException.ThrowIfNull(data);
		var excluded = NaturalSkillCatalog.CommonExcluded.Concat(Excluded).ToDictionary(entry => entry.Key, entry => entry.Value);
		NaturalPriestSkill[] skills = NaturalSkillCatalog.Build(data, PlayerClass.BARD, Roles, excluded);
		NaturalFightMovement movement = NaturalPriestProfile.PriestLineMovement;
		NaturalProfileValidator.Require(data, PlayerClass.BARD, TopLevel, skills, excluded, Rules.Lines(skills), Gear);
		return new NaturalClassProfile
		{
			Class = PlayerClass.BARD,
			Skills = skills,
			Excluded = excluded,
			Combat = new NaturalRotationCombatPolicy(Rules, skills, movement),
			// NR-34: it casts from mana and rests with the powder, so it takes both kinds, which is every row.
			HelpItems = NaturalHelpItemRules.ForKinds(caster: true, reagent: true),
			// Protective Ode, which lasts 5 min or until it has taken its share, and Etude, which lasts an hour.
			Upkeep = [new("ode", "buff-protective-ode"), new("etude", "buff-etude")],
			// NR-36: what it shoots with in flight: Pulse, which is ready every 2 s. A follow-up and a skill paid with DP
			// are passed over there.
			AirAttackRoles = ["pulse"],
			// NR-37: every second class holds for a patrol and assesses the fight, by its own table.
			PatrolRule = NaturalPatrolRule.HoldAndAssess,
			Patrol = NaturalPatrolView.From(Rules, skills, PlayerClass.BARD),
			RangedHold = NaturalRangedHold.RunOption,
			// NR-50a: the powder first; Soothing Melody while the powder cools down or is gone, as the Cleric's Healing
			// Light; a sit for mana below 40% until 80%, after Resonating Melody.
			Rest = new NaturalRestRules(skills, HealBelowPercent: 90, ManaSitBelowPercent: 40, ManaSitUntilPercent: 80, MaximumQuietSits: 12,
				PotionPlan: new NaturalPotionRestPlan(HpTargetPercent: 90, UsesMana: true),
				RestSkills: NaturalRestSkills.ReagentOnly(90) with
				{
					Heal = new("heal", "Soothing Melody"), FreeMana = new("resonate", "Resonating Melody"),
				}),
			// A stand-off at 22 m with skills that reach 25 m: the Priest line's distances, thresholds and campaign numbers.
			Ranges = NaturalPriestProfile.PriestLineRanges,
			Readiness = NaturalPriestProfile.PriestLineReadiness,
			Movement = movement,
			Campaign = NaturalPriestProfile.PriestLineCampaign,
			Gear = Gear,
			// The Artist's own purchases: the Minor Life Elixir, and no purchase takes Kinah below 500.
			Restock = NaturalMageProfile.Restock,
		};
	}
}
