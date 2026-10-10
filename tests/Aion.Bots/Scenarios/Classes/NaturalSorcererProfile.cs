using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// NR-60: the Sorcerer, the Mage's choice at Ascension that holds a spellbook (docs/natural-all-classes-ntc.md). The
/// catalog is generated from the shipped data to level 26, where the accepted line's Abyss-entry leg ends. It fights as
/// the Mage it was, with what it learns from level 10 put into the same table form; what the form cannot say yet is left
/// out with its reason and named in the plan's items after NR-60.
/// </summary>
public static class NaturalSorcererProfile
{
	private const int TopLevel = 26;

	/// <summary>
	/// The Sorcerer's active skills to level 26, by role, beside the Mage's it keeps. Flame Bolt and Flame Harpoon
	/// (level 13) open the chain Blaze follows; Ice Chain opens the one Frozen Shock follows; each follow-up inside 3 s.
	/// Flame Cage (level 16) takes Erosion's place and its cooldown. Delayed Blast (level 19) hits 4 s after its cast.
	/// Empyrean Fire is paid with 2,000 DP. Wind Spear (level 15) may be cast three times in a row, once in 90 s.
	/// Freezing Wind (level 25) reaches 3 m. Robe of Flame (level 10) gives magic boost and mana regeneration for 30 min.
	/// </summary>
	private static readonly IReadOnlyDictionary<int, string> Roles = new Dictionary<int, string>
	{
		[1282] = "bolt", [1284] = "bolt", [1285] = "bolt", [1286] = "bolt", [1287] = "bolt",
		[1403] = "blaze", [1404] = "blaze", [1405] = "blaze", [1406] = "blaze", [1407] = "blaze",
		[1363] = "ice", [1365] = "ice", [1366] = "ice", [1367] = "ice",
		[1226] = "shock", [1227] = "shock", [1228] = "shock", [1229] = "shock",
		[1447] = "erosion", [1510] = "erosion", [1511] = "erosion", [1512] = "erosion",
		[1271] = "harpoon", [1272] = "harpoon", [1273] = "harpoon",
		[1421] = "blast", [1422] = "blast",
		[1494] = "empyrean", [1495] = "empyrean", [1496] = "empyrean", [1497] = "empyrean",
		[1259] = "spear", [1260] = "spear", [1261] = "spear",
		[1217] = "frost",
		[1328] = "root",
		[1155] = "skin", [1156] = "skin", [1157] = "skin", [1158] = "skin",
		[1296] = "robe", [1297] = "robe", [1298] = "robe", [1299] = "robe",
		// NR-60a: Gain Mana, cast by the table's mana step and first in a rest for mana.
		[1192] = "gainmana", [1193] = "gainmana", [1194] = "gainmana", [1195] = "gainmana",
		// The two powder skills, cast only in a rest (NR-50a).
		[246] = "herb", [247] = "herb", [251] = "herb", [253] = "herb",
		[249] = "mp-recovery", [250] = "mp-recovery", [252] = "mp-recovery", [254] = "mp-recovery",
	};

	private const string WinterBinding = "hits and roots up to eight monsters within 15 m of the Sorcerer. The bot pulls one monster at a " +
		"time, and an area skill wakes every other one in reach.";

	/// <summary>Every other active skill a Sorcerer learns by itself to level 26, and why it is not cast.</summary>
	private static readonly IReadOnlyDictionary<int, string> Excluded = new Dictionary<int, string>
	{
		[1417] = "Curse of Roots holds its target for 20 s after a 1.5 s cast, and a hit ends it. The table names one control, the " +
			"instant Root, cast on the way out before a retreat.",
		[1347] = "Blind Leap throws the Sorcerer 15 m ahead to a place the server picks; the bot walks checked routes only.",
		[1167] = "Winter Binding I " + WinterBinding, [1168] = "Winter Binding II " + WinterBinding, [1169] = "Winter Binding III " + WinterBinding,
		[1343] = "Somnolence puts its target to sleep for 4 s, 268 MP, and a hit ends it; the table names one control, Root.",
		[1549] = "Illusion removes one physical debuff and dodges the next two hits inside 10 s for 269 MP; no rule times a defence " +
			"against a hit that has not come.",
		[1348] = "Summon Rift opens a rift for a group to travel by; the journey's travel casts no skill.",
		[1477] = "Boon of Peace lowers the enmity of the monsters within 5 m; alone, they have no one else to turn to.",
	};

	/// <summary>
	/// NR-60: the Mage's table with what the Sorcerer adds. From range: Ice Chain, whose snare keeps the monster away
	/// longer, then Frozen Shock at once; Empyrean Fire when 2,000 DP are there; Delayed Blast early, since it lands 4 s
	/// later; then Flame Harpoon, Flame Bolt and Blaze, an open follow-up always first; Wind Spear last, the instant that
	/// fills a gap. With the monster on it the instants come first, the ones a hit cannot push back: Freezing Wind,
	/// which reaches 3 m, then Flame Cage or Erosion. Stone Skin goes up before the first hit. Root is cast only on the
	/// way out, before a retreat.
	/// <para>
	/// The ladder and the rest are the Mage's (CP-Q11, CP-Q12): the shield scroll at 50% HP and the life potion at or
	/// below 75%; it leaves at two attackers, or at 25% HP with nothing ready; the mana potion only when the cheapest
	/// attack cannot be paid. The spellbook swings only when no attack can be paid for.
	/// </para>
	/// <para>
	/// NR-60a: Gain Mana (level 10) gives 314 MP at once and 124 a second for 5 s more, at no cost, once in 3 min. In a
	/// fight it is cast at or below half its mana, before any mana potion; in a rest for mana it is cast first.
	/// </para>
	/// </summary>
	private static readonly NaturalRotationRules Rules = new("natural-sorcerer-v1",
		Adjacent: ["frost", "erosion", "ice", "shock", "empyrean", "blast", "harpoon", "bolt", "blaze", "spear"],
		AtRange: ["ice", "shock", "empyrean", "blast", "harpoon", "bolt", "blaze", "spear"],
		Upkeep: [new("skin")],
		Recovery: [new(NaturalRecoveryKind.ShieldScroll, 50), new(NaturalRecoveryKind.LifePotion, 75)],
		SwarmAttackers: 2, FleeHpPercent: 25, AutoAttack: NaturalAutoAttack.LastResort, ControlRole: "root",
		ManaSkill: new("gainmana", 50));

	// NR-Q5, NR-Q7 and NR-Q8: the spellbook, cloth, and the kit of a class that casts from mana and rests with the powder.
	private static readonly NaturalGearRules Gear = NaturalClassGearTable.Sorcerer.Rules(NaturalClassLineContract.LoadDefault(),
		NaturalHelpItemAllowlist.Kit(caster: true, reagent: true));

	public static NaturalClassProfile Create(StaticData data)
	{
		ArgumentNullException.ThrowIfNull(data);
		var excluded = NaturalSkillCatalog.CommonExcluded.Concat(Excluded).ToDictionary(entry => entry.Key, entry => entry.Value);
		NaturalPriestSkill[] skills = NaturalSkillCatalog.Build(data, PlayerClass.SORCERER, Roles, excluded);
		NaturalFightMovement movement = NaturalPriestProfile.PriestLineMovement;
		NaturalProfileValidator.Require(data, PlayerClass.SORCERER, TopLevel, skills, excluded, Rules.Lines(skills), Gear);
		return new NaturalClassProfile
		{
			Class = PlayerClass.SORCERER,
			Skills = skills,
			Excluded = excluded,
			Combat = new NaturalRotationCombatPolicy(Rules, skills, movement),
			// NR-34: it casts from mana and rests with the powder, so it takes both kinds, which is every row.
			HelpItems = NaturalHelpItemRules.ForKinds(caster: true, reagent: true),
			// Stone Skin as the Mage keeps it, and Robe of Flame, which lasts 30 min.
			Upkeep = [new("skin", "buff-stone-skin"), new("robe", "buff-robe-of-flame")],
			// NR-36: what it shoots with in flight.
			AirAttackRoles = Rules.AtRange,
			// NR-37: every second class holds for a patrol and assesses the fight, by its own table.
			PatrolRule = NaturalPatrolRule.HoldAndAssess,
			Patrol = NaturalPatrolView.From(Rules, skills, PlayerClass.SORCERER),
			RangedHold = NaturalRangedHold.RunOption,
			// NR-50a: the powder first, then as the Mage: the life potion below 90% HP, a sit for mana below 40% until 80%.
			Rest = new NaturalRestRules(skills, HealBelowPercent: 90, ManaSitBelowPercent: 40, ManaSitUntilPercent: 80, MaximumQuietSits: 12,
				PotionPlan: new NaturalPotionRestPlan(HpTargetPercent: 90, UsesMana: true),
				RestSkills: NaturalRestSkills.ReagentOnly(90) with { FreeMana = new("gainmana", "Gain Mana") }),
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
