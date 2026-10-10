using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// NR-110: the Spirit Master, the Mage's other choice at Ascension (docs/natural-all-classes-ntc.md). The catalog is
/// generated from the shipped data to level 26, where the accepted line's Abyss-entry leg ends. It fights as the Mage it
/// was, with what it casts itself from level 10 put into the same table form. NR-110a: it keeps a spirit beside it.
/// Every skill that orders, heals or arms the spirit is left out with its reason and named in the plan's items after
/// NR-110: no rule sends the spirit at a target yet.
/// </summary>
public static class NaturalSpiritMasterProfile
{
	private const int TopLevel = 26;

	/// <summary>
	/// The Spirit Master's own spells to level 26, by role, beside the Mage's it keeps. Erosion, which it has in every
	/// rank, hits at once and for 15 s after, and is ready every 3 s. Chain of Earth (level 13) does the same lightly,
	/// halves its target's speed for 15 to 20 s and opens the chain Stone Shock follows, which staggers the target.
	/// Summon Wind Servant (level 10) sends two servants that strike three times each. Vacuum Choke (level 22) is its
	/// hardest spell, a 2 s cast that is ready again after 1 s; Backdraft (level 26) gives back as HP and MP what it
	/// takes. Of Flame Bolt, Blaze, Ice Chain and Frozen Shock it keeps the Mage's first rank only.
	/// </summary>
	private static readonly IReadOnlyDictionary<int, string> Roles = new Dictionary<int, string>
	{
		[1447] = "erosion", [1448] = "erosion", [1449] = "erosion", [1450] = "erosion", [1451] = "erosion",
		[3603] = "earth", [3604] = "earth", [3605] = "earth",
		[3614] = "stone", [3615] = "stone", [3616] = "stone",
		[3810] = "servant", [3812] = "servant", [3814] = "servant", [3816] = "servant",
		[3593] = "choke",
		[3640] = "backdraft",
		[1282] = "bolt", [1403] = "blaze", [1363] = "ice", [1226] = "shock",
		[1328] = "root",
		[1155] = "skin", [1156] = "skin", [1157] = "skin", [1158] = "skin",
		// NR-110a: the spirits it keeps.
		[3645] = "earth-spirit", [3647] = "earth-spirit", [3649] = "earth-spirit",
		[3707] = "fire-spirit", [3709] = "fire-spirit", [3711] = "fire-spirit", [3713] = "fire-spirit",
		// NR-50a: the two powder skills, cast only in a rest.
		[246] = "herb", [247] = "herb", [251] = "herb", [253] = "herb",
		[249] = "mp-recovery", [250] = "mp-recovery", [252] = "mp-recovery", [254] = "mp-recovery",
	};

	private const string Spirit = "is not one of the two spirits the profile keeps, the Earth Spirit and before it the Fire Spirit: the server " +
		"keeps one spirit at a time (Java SummonsService.createSummon 30-33).";
	private const string Order = "is an order to the spirit, and no rule sends the spirit at a target yet (NR-110b).";
	private const string Dispel = "takes a buff off its target and hits for each one taken; the monsters of the route carry none.";

	/// <summary>Every other active skill a Spirit Master learns by itself to level 26, and why it is not cast.</summary>
	private static readonly IReadOnlyDictionary<int, string> Excluded = new Dictionary<int, string>
	{
		[3685] = "Summon: Wind Spirit I " + Spirit, [3687] = "Summon: Wind Spirit II " + Spirit, [3689] = "Summon: Wind Spirit III " + Spirit,
		[3665] = "Summon: Water Spirit I " + Spirit, [3667] = "Summon: Water Spirit II " + Spirit,
		[3837] = "Spirit Disturbance " + Order, [3852] = "Spirit Wrath Position " + Order, [3643] = "Spirit Erosion " + Order,
		[3630] = "Replenish Element I heals the spirit for the Spirit Master's own HP; no rule watches the spirit's HP in a fight yet (NR-110b).",
		[3631] = "Replenish Element II heals the spirit for the Spirit Master's own HP; no rule watches the spirit's HP in a fight yet (NR-110b).",
		[3632] = "Replenish Element III heals the spirit for the Spirit Master's own HP; no rule watches the spirit's HP in a fight yet (NR-110b).",
		[3855] = "Divine Spirit Armor I arms the spirit for 2,000 DP; no rule casts a skill on the spirit yet (NR-110b).",
		[3856] = "Divine Spirit Armor II arms the spirit for 2,000 DP; no rule casts a skill on the spirit yet (NR-110b).",
		[3857] = "Divine Spirit Armor III arms the spirit for 2,000 DP; no rule casts a skill on the spirit yet (NR-110b).",
		[3858] = "Divine Spirit Armor IV arms the spirit for 2,000 DP; no rule casts a skill on the spirit yet (NR-110b).",
		[3780] = "Root of Enervation slows its target's attacks by a fifth for 35 to 45 s for 244 MP, which is the mana of four Erosions.",
		[3571] = "Body Root binds its target for 8 to 10 s: it casts no physical skill and still swings. 108 MP for that buys two Erosions.",
		[3572] = "Sigil of Silence silences its target; the table has no rule for a target that casts.",
		[3570] = "Dispel Magic " + Dispel, [3532] = "Ignite Aether " + Dispel,
		[3742] = "Sandblaster hits up to six monsters around its target. The bot pulls one at a time, and an area skill wakes every other " +
			"one in reach.",
		[3777] = "Summon Group Member calls a member of its group to it; the bot plays alone.",
	};

	/// <summary>
	/// NR-110: the Mage's table with the Spirit Master's own spells. From range: Chain of Earth, whose snare keeps the
	/// monster away longer, and Stone Shock at once; Erosion; Vacuum Choke and Backdraft; Summon Wind Servant; then what
	/// is left of the Mage: Ice Chain and Frozen Shock, Flame Bolt and Blaze. With the monster on it Erosion comes first:
	/// it has no cast time a hit can push back. An open follow-up is always cast first. Stone Skin goes up before the
	/// first hit and is kept up between fights. Root is cast only on the way out, before a retreat.
	/// <para>
	/// The ladder and the rest are the Mage's (CP-Q11, CP-Q12): the shield scroll at 50% HP and the life potion at or
	/// below 75%; it leaves at two attackers, or at 25% HP with nothing ready; the mana potion only when the cheapest
	/// attack cannot be paid. The spellbook swings only when no attack can be paid for.
	/// </para>
	/// </summary>
	private static readonly NaturalRotationRules Rules = new("natural-spirit-master-v1",
		Adjacent: ["erosion", "earth", "stone", "choke", "backdraft", "servant", "ice", "shock", "bolt", "blaze"],
		AtRange: ["earth", "stone", "erosion", "choke", "backdraft", "servant", "ice", "shock", "bolt", "blaze"],
		Upkeep: [new("skin")],
		Recovery: [new(NaturalRecoveryKind.ShieldScroll, 50), new(NaturalRecoveryKind.LifePotion, 75)],
		SwarmAttackers: 2, FleeHpPercent: 25, AutoAttack: NaturalAutoAttack.LastResort, ControlRole: "root");

	/// <summary>NR-110a: the spirits kept, best first: the Earth Spirit, which takes the hits, from level 16, and the Fire
	/// Spirit, its first, before. A cast takes 4.5 s and 145 MP or more.</summary>
	private static readonly NaturalKeptSpirit[] KeptSpirits =
		[new("earth-spirit", "summon-earth-spirit"), new("fire-spirit", "summon-fire-spirit")];

	// NR-Q5, NR-Q7 and NR-Q8: the spellbook, cloth, and the kit of a class that casts from mana and rests with the powder.
	private static readonly NaturalGearRules Gear = NaturalClassGearTable.SpiritMaster.Rules(NaturalClassLineContract.LoadDefault(),
		NaturalHelpItemAllowlist.Kit(caster: true, reagent: true));

	public static NaturalClassProfile Create(StaticData data)
	{
		ArgumentNullException.ThrowIfNull(data);
		var excluded = NaturalSkillCatalog.CommonExcluded.Concat(Excluded).ToDictionary(entry => entry.Key, entry => entry.Value);
		NaturalPriestSkill[] skills = NaturalSkillCatalog.Build(data, PlayerClass.SPIRIT_MASTER, Roles, excluded);
		NaturalFightMovement movement = NaturalPriestProfile.PriestLineMovement;
		NaturalProfileValidator.Require(data, PlayerClass.SPIRIT_MASTER, TopLevel, skills, excluded, Rules.Lines(skills), Gear, spirits: KeptSpirits);
		return new NaturalClassProfile
		{
			Class = PlayerClass.SPIRIT_MASTER,
			Skills = skills,
			Excluded = excluded,
			Combat = new NaturalRotationCombatPolicy(Rules, skills, movement),
			// NR-34: it casts from mana and rests with the powder, so it takes both kinds, which is every row.
			HelpItems = NaturalHelpItemRules.ForKinds(caster: true, reagent: true),
			// Stone Skin as the Mage keeps it.
			Upkeep = [new("skin", "buff-stone-skin")],
			Spirits = KeptSpirits,
			// NR-36: in flight it casts Erosion, which is ready every 3 s from 25 m.
			AirAttackRoles = ["erosion"],
			// NR-37: every second class holds for a patrol and assesses the fight, by its own table.
			PatrolRule = NaturalPatrolRule.HoldAndAssess,
			Patrol = NaturalPatrolView.From(Rules, skills, PlayerClass.SPIRIT_MASTER),
			RangedHold = NaturalRangedHold.RunOption,
			// NR-50a: the powder first, then as the Mage: the life potion below 90% HP, a sit for mana below 40% until 80%.
			Rest = new NaturalRestRules(skills, HealBelowPercent: 90, ManaSitBelowPercent: 40, ManaSitUntilPercent: 80, MaximumQuietSits: 12,
				PotionPlan: new NaturalPotionRestPlan(HpTargetPercent: 90, UsesMana: true), RestSkills: NaturalRestSkills.ReagentOnly(90)),
			// A stand-off at 22 m with skills that reach 25 m: the Priest line's distances, thresholds and campaign numbers.
			Ranges = NaturalPriestProfile.PriestLineRanges,
			Readiness = NaturalPriestProfile.PriestLineReadiness,
			Movement = movement,
			Campaign = NaturalPriestProfile.PriestLineCampaign,
			Gear = Gear,
			Restock = NaturalMageProfile.Restock,
		};
	}
}
