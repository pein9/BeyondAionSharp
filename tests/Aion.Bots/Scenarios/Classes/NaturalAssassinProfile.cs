using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// NR-90: the Assassin, the Scout's choice at Ascension that holds two daggers (docs/natural-all-classes-ntc.md). The
/// catalog is generated from the shipped data to level 26, where the accepted line's Abyss-entry leg ends. It fights as
/// the Scout it was, with what it learns from level 10 put into the same table form; what the form cannot say yet is
/// left out with its reason and named in the plan's items after NR-90.
/// </summary>
public static class NaturalAssassinProfile
{
	private const int TopLevel = 26;

	/// <summary>
	/// The Assassin's active skills to level 26, by role, beside the Scout's it keeps. Swift Edge opens the chain Soul
	/// Slash follows, and Rune Slash (level 21) follows that one time in ten: Soul Slash's chain_skill_prob is 10, and the
	/// cast's result says whether the chain is still open. Fang Strike (level 16) opens the chain Beast Kick (level 26)
	/// follows. Rune Carve (level 13) opens a chain nothing follows before level 26. Rune Carve, Rune Slash, Fang Strike
	/// and Beast Kick each carve one rune more into their target, to three or to five. Divine Strike is paid with 2,000
	/// DP. Pain Rune (level 13) and Binding Rune (level 25) burst the runes on their target (NR-90a): a tenth of their
	/// damage with none, all of it with three. Killer's Eye (level 10) adds half to the next physical skill inside 15 s.
	/// Flurry (level 20) quickens the daggers by a fifth for 30 s. Apply Deadly Poison (level 15) lasts 5 min: one swing
	/// in five poisons its target.
	/// </summary>
	private static readonly IReadOnlyDictionary<int, string> Roles = new Dictionary<int, string>
	{
		[3182] = "edge", [3183] = "edge", [3184] = "edge", [3185] = "edge", [3186] = "edge", [3187] = "edge",
		[3223] = "slash", [3224] = "slash", [3225] = "slash", [3226] = "slash",
		[3283] = "runeslash", [3284] = "runeslash",
		[3385] = "carve", [3386] = "carve", [3387] = "carve",
		[3417] = "fang", [3418] = "fang", [3419] = "fang",
		[3427] = "kick",
		[3374] = "pain", [3375] = "pain", [3376] = "pain",
		[3406] = "binding",
		[3519] = "divine", [3520] = "divine", [3521] = "divine", [3522] = "divine",
		[3235] = "devotion",
		[3468] = "eye",
		[3355] = "flurry",
		[3481] = "poison",
		[3195] = "evasion",
		// NR-50a: the two powder skills, cast only in a rest.
		[246] = "herb", [247] = "herb", [251] = "herb", [253] = "herb",
		[249] = "mp-recovery", [250] = "mp-recovery", [252] = "mp-recovery", [254] = "mp-recovery",
	};

	private const string Surprise = "does little from the front, opens its own chain and so resets Swift Edge's; its back damage needs a position " +
		"the bot does not take (CP-Q16).";
	private const string Counter = "the server takes it only within 5 s of a dodge of the Assassin's own (counter_skill DODGE, Java Skill.java " +
		"163-169), which the bot does not observe.";
	private const string Moves = "The server says where only in the cast's result (Java sends no forced move), which the bot does not read, and " +
		"the bot walks checked routes only.";
	/// <summary>Every other active skill and toggle an Assassin learns by itself to level 26, and why it is not cast.</summary>
	private static readonly IReadOnlyDictionary<int, string> Excluded = new Dictionary<int, string>
	{
		[3196] = "Surprise Attack I " + Surprise, [3197] = "Surprise Attack II " + Surprise, [3198] = "Surprise Attack III " + Surprise,
		[3199] = "Surprise Attack IV " + Surprise, [3200] = "Surprise Attack V " + Surprise,
		[3209] = "Counterattack I: " + Counter, [3210] = "Counterattack II: " + Counter, [3211] = "Counterattack III: " + Counter,
		[3212] = "Counterattack IV: " + Counter, [3213] = "Counterattack V: " + Counter,
		[3482] = "Whirlwind Slash: " + Counter,
		[3222] = "Stealth cannot be cast in combat and the journey does not sneak (CP-Q16).",
		[3455] = "Dash Attack I moves the Assassin to its target. " + Moves, [3456] = "Dash Attack II moves the Assassin to its target. " + Moves,
		[3457] = "Dash Attack III moves the Assassin to its target. " + Moves, [3458] = "Dash Attack IV moves the Assassin to its target. " + Moves,
		[3356] = "Ambush puts the Assassin behind its target. " + Moves,
		[3331] = "Flash of Speed throws the Assassin 15 m to a place the server picks; the bot walks checked routes only.",
		[3273] = "Calming Whisper I lowers the enmity of its target; alone, the monster has no one else to turn to.",
		[3274] = "Calming Whisper II lowers the enmity of its target; alone, the monster has no one else to turn to.",
		[3328] = "Aethertwisting resists the next two spells inside 8 s; no rule times a defence against a hit that has not come.",
		[3372] = "Sprinting is a toggle that spends 3% of its mana every 6 s for a fifth more speed; the kit's running scroll is its speed.",
	};

	/// <summary>
	/// NR-90: the Scout's table with what the Assassin adds. With the monster in reach: Devotion when it is ready, then
	/// Divine Strike when 2,000 DP are there, then Swift Edge and Soul Slash, and Rune Slash when Soul Slash left the
	/// chain open, each inside 3 s; then Fang Strike and Beast Kick; then Rune Carve. Killer's Eye and Flurry are cast
	/// when no attack is ready: they fill the time a dagger would swing. An open follow-up is always cast first and the
	/// table holds an open chain, as the Scout's does. The daggers swing whenever no skill is ready. Nothing is cast
	/// from range: it walks in.
	/// <para>
	/// NR-90a: Pain Rune, and Binding Rune when Pain Rune cools down, are cast once the target is seen with three runes,
	/// where the burst does all of its damage and Pain Rune always stuns for 3 s. With fewer they are out of the line.
	/// </para>
	/// <para>
	/// The ladder is the Scout's (CP-Q11): Focused Evasion at or below 70% HP, the shield scroll at 50%, the life potion
	/// at or below 75%. It leaves at three attackers, or at 25% HP with nothing ready. Between fights it rests as the
	/// Templar (NR-50a) and keeps Apply Deadly Poison up.
	/// </para>
	/// </summary>
	private static readonly NaturalRotationRules Rules = new("natural-assassin-v1",
		Adjacent: ["devotion", "divine", "pain", "binding", "edge", "slash", "runeslash", "fang", "kick", "carve", "eye", "flurry"], AtRange: [],
		Upkeep: [],
		Recovery: [new(NaturalRecoveryKind.Skill, 70, "evasion"), new(NaturalRecoveryKind.ShieldScroll, 50), new(NaturalRecoveryKind.LifePotion, 75)],
		SwarmAttackers: 3, FleeHpPercent: 25, AutoAttack: NaturalAutoAttack.Filler, HoldOpenChain: true,
		OnlyWithRunes: new Dictionary<string, int> { ["pain"] = 3, ["binding"] = 3 });

	// NR-Q5, NR-Q6, NR-Q7 and NR-Q13: two daggers, leather first, and the kit of a class that does not cast from mana and
	// rests with the powder.
	private static readonly NaturalGearRules Gear = NaturalClassGearTable.Assassin.Rules(NaturalClassLineContract.LoadDefault(),
		NaturalHelpItemAllowlist.Kit(caster: false, reagent: true));

	public static NaturalClassProfile Create(StaticData data)
	{
		ArgumentNullException.ThrowIfNull(data);
		var excluded = NaturalSkillCatalog.CommonExcluded.Concat(Excluded).ToDictionary(entry => entry.Key, entry => entry.Value);
		NaturalPriestSkill[] skills = NaturalSkillCatalog.Build(data, PlayerClass.ASSASSIN, Roles, excluded);
		// The walk to weapon reach and the swing are the Warrior's, as the Scout's are.
		NaturalFightMovement movement = NaturalWarriorProfile.Movement;
		NaturalProfileValidator.Require(data, PlayerClass.ASSASSIN, TopLevel, skills, excluded, Rules.Lines(skills), Gear);
		return new NaturalClassProfile
		{
			Class = PlayerClass.ASSASSIN,
			Skills = skills,
			Excluded = excluded,
			Combat = new NaturalRotationCombatPolicy(Rules, skills, movement),
			// NR-34, NR-Q13: no mana serum and no Awakening scroll; the powder; Courage in the shared scroll slot.
			HelpItems = NaturalHelpItemRules.ForKinds(caster: false, reagent: true, sharedSlotFamily: "courage"),
			// The poison on its daggers lasts 5 min.
			Upkeep = [new("poison", "buff-apply-deadly-poison")],
			// NR-36: nothing in its table hurts a target from range, so in flight it swings its daggers.
			AirAttackRoles = [],
			// NR-37: every second class holds for a patrol and assesses the fight, by its own table.
			PatrolRule = NaturalPatrolRule.HoldAndAssess,
			Patrol = NaturalPatrolView.From(Rules, skills, PlayerClass.ASSASSIN),
			RangedHold = NaturalRangedHold.Never,
			// NR-50a: the powder first, then as the Scout: the life potion below 90% HP, then sitting.
			Rest = new NaturalRestRules(skills, HealBelowPercent: 90, ManaSitBelowPercent: 25, ManaSitUntilPercent: 50, MaximumQuietSits: 12,
				PotionPlan: new NaturalPotionRestPlan(HpTargetPercent: 90, UsesMana: true), RestSkills: NaturalRestSkills.ReagentOnly(90)),
			// The Scout's distances, thresholds and campaign numbers.
			Ranges = NaturalPriestProfile.PriestLineRanges,
			Readiness = NaturalWarriorProfile.Readiness,
			Movement = movement,
			Campaign = NaturalPriestProfile.PriestLineCampaign,
			Gear = Gear,
			Restock = NaturalWarriorProfile.Restock,
		};
	}
}
