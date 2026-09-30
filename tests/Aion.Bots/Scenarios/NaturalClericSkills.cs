using Aion.Bots.World;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios;

/// <summary>
/// NA-18: frozen 4.8 Cleric active skills on top of the Priest's, which carry over at Ascension
/// (docs/natural-ascension-altgard.md Appendix C). Java skill_tree.xml and skill_templates.xml at ce54b7931.
/// The learned SM_SKILL_LIST is still the authority: this catalog never grants a skill.
/// AC-00: every auto-learned Asmodian Cleric rank up to level 20. The server adds each new rank on level-up and keeps
/// the old one (Java SkillLearnService.learnNewSkills), and <see cref="NaturalPriestSkills.Best"/> takes the highest
/// learned rank of a role, so the bot moves to a new rank the moment it is observed. Every active auto-learned skill
/// that is not here is in <see cref="Excluded"/> with its reason; the passives need no casting.
/// </summary>
public static class NaturalClericSkills
{
	/// <summary>Lesser Odella Powder, the reagent of the rank 1 powder skills. From level 25 the next ranks
	/// use Odella Powder 169300004; the catalog entry's rank decides, never the item level.</summary>
	public const int LesserOdellaPowder = 169300003;

	/// <summary>Herb Treatment and MP Recovery share this cooldown group (16 s).</summary>
	public const int PowderCooldownId = 1153;

	public static readonly NaturalPriestSkill[] Cleric =
	[
		new(246, 10, "herb", 0, 1, PowderCooldownId, 160, ReagentItemId: LesserOdellaPowder, ReagentCount: 1),
		new(249, 10, "mp-recovery", 0, 1, PowderCooldownId, 160, ReagentItemId: LesserOdellaPowder, ReagentCount: 2),
		new(3922, 10, "salvation", 0, 1, 1211, 600, DpCost: 2000),
		new(3939, 10, "rejuvenation", 24, 23, 1217, 50),
		new(4025, 10, "followup", 34, 25, 1230, 100, "P_CHAINA_2TH_1", "P_CHAINA_1TH_1", 3000),
		new(4061, 10, "wind", 39, 25, 1234, 160, "C_CHAINC_1TH_1"),
		new(4083, 10, "wrath", 85, 25, 1236, 120, "P_CHAIND_1TH_1"),
		new(4127, 10, "root", 47, 25, 1241, 100),
		// AC-00: the Priest's roles, new ranks (the Priest catalog stays frozen for Ishalgen).
		new(1840, 11, "heal", 42, 23, 1553, 0),
		new(4014, 11, "smite", 38, 25, 1229, 20, "P_CHAINA_1TH_1"),
		new(1815, 12, "infernal", 31, 25, 1549, 240, "C_CHAINB_1TH_1"),
		new(1616, 13, "hallowed", 19, 1, 1512, 80, "C_CHAINJ_1TH_1"),
		new(1841, 16, "heal", 50, 23, 1553, 0),
		new(4015, 16, "smite", 44, 25, 1229, 20, "P_CHAINA_1TH_1"),
		new(1816, 17, "infernal", 40, 25, 1549, 240, "C_CHAINB_1TH_1"),
		new(1617, 18, "hallowed", 23, 1, 1512, 80, "C_CHAINJ_1TH_1"),
		// AC-00: the Cleric's roles, new ranks. The rank 2 and 3 powder skills still use Lesser Odella Powder.
		new(247, 15, "herb", 0, 1, PowderCooldownId, 160, ReagentItemId: LesserOdellaPowder, ReagentCount: 1),
		new(250, 15, "mp-recovery", 0, 1, PowderCooldownId, 160, ReagentItemId: LesserOdellaPowder, ReagentCount: 2),
		new(3940, 15, "rejuvenation", 28, 23, 1217, 50),
		new(4026, 15, "followup", 40, 25, 1230, 100, "P_CHAINA_2TH_1", "P_CHAINA_1TH_1", 3000),
		new(4062, 15, "wind", 46, 25, 1234, 160, "C_CHAINC_1TH_1"),
		new(4084, 15, "wrath", 100, 25, 1236, 120, "P_CHAIND_1TH_1"),
		new(251, 20, "herb", 0, 1, PowderCooldownId, 160, ReagentItemId: LesserOdellaPowder, ReagentCount: 1),
		new(252, 20, "mp-recovery", 0, 1, PowderCooldownId, 160, ReagentItemId: LesserOdellaPowder, ReagentCount: 2),
		new(3941, 20, "rejuvenation", 33, 23, 1217, 50),
		new(4027, 20, "followup", 46, 25, 1230, 100, "P_CHAINA_2TH_1", "P_CHAINA_1TH_1", 3000),
		new(4063, 20, "wind", 53, 25, 1234, 160, "C_CHAINC_1TH_1"),
		new(4085, 20, "wrath", 116, 25, 1236, 120, "P_CHAIND_1TH_1"),
		// AC-00: new skills. Penance (instant): 115 MP every 3 s for 30 s for 57 HP a tick, a rest skill. Summon Holy
		// Servant (Asmodian): a servant attacks the target for 17 s. Divine Touch follows Slashing Wind's chain.
		// Healing Grace: 1,298 HP for 114 MP in a 3 s cast, against Healing Light IV's 374 for 50.
		new(3867, 15, "penance", 0, 1, 1200, 1800),
		new(4106, 15, "servant", 86, 25, 1066, 300),
		new(4073, 17, "touch", 44, 25, 1235, 140, "P_CHAINC_2TH_1", "C_CHAINC_1TH_1", 3000),
		new(4203, 19, "grace", 114, 23, 1257, 60),
		new(3868, 20, "penance", 0, 1, 1200, 1800),
		new(4108, 20, "servant", 100, 25, 1066, 300),
	];

	/// <summary>AC-00: auto-learned active Cleric skills up to level 20 that the bot does not cast, and why. The ratchet
	/// test fails when skill_tree.xml has one that is neither here nor in the catalog.</summary>
	public static readonly IReadOnlyDictionary<int, string> Excluded = new Dictionary<int, string>
	{
		[1699] = "Light of Resurrection revives another player; the bot plays solo.",
		[3935] = "Cleanse I removes dispellable debuffs, but the client's effect list does not say which are; the one lasting debuff met so far, the Okaru poison, cannot be dispelled (req_dispel_level 99).",
		[3936] = "Cleanse II: as Cleanse I.",
		[3878] = "Stability raises a target's enmity, a group tank's tool; solo it changes nothing.",
	};

	public static readonly NaturalPriestSkill[] All = [.. NaturalPriestSkills.All, .. Cleric];

	/// <summary>The observed class chooses the catalog; the Priest keeps its frozen Ishalgen catalog.</summary>
	public static NaturalPriestSkill[] ForClass(byte? classId) =>
		classId == PlayerClass.CLERIC.GetClassId() ? All : NaturalPriestSkills.All;
}

/// <param name="RecoveringMana">The caller's mana-rest hysteresis: set below 50% MP, cleared at 80%.</param>
/// <param name="Engaged">A monster hit the bot since the last observation, or is on it.</param>
/// <param name="LastPowderSkillId">The powder skill cast last in this rest, so the two alternate.</param>
public sealed record NaturalPowderRestObservation(int Level, int Hp, int MaxHp, int Mp, int MaxMp, bool RecoveringMana,
	IReadOnlyDictionary<int, BotSkill> Learned, IReadOnlyDictionary<int, DateTimeOffset> Cooldowns,
	IReadOnlyDictionary<int, long> ItemCounts, bool Engaged = false, ushort? LastPowderSkillId = null);

/// <param name="Action">defend, done, penance, herb, mp-recovery, light-heal or sit.</param>
public sealed record NaturalPowderRestChoice(string Action, NaturalPriestSkill? Skill, string Reason, DateTimeOffset? ReadyAt = null);

/// <summary>
/// NA-18 (OD-9): resting with powder. Herb Treatment (1 powder, 281 HP) and MP Recovery (2 powder, about 299 MP)
/// replace most sitting. Both are 4 s casts that any hit cancels, so they are used only when nothing is engaged;
/// they share cooldown group 1153, so they alternate, starting with the larger deficit. Sitting is the fallback:
/// out of powder, or the shared cooldown running while mana is still needed. Without the powder skills (the
/// Priest) this reduces to the existing rest: Healing Light for HP, sitting for mana.
/// </summary>
public static class NaturalPowderRestPolicy
{
	public const int HealBelowPercent = 90;

	/// <summary>AC-00: Penance costs about 570 HP over its 30 s; it starts only at or above this HP.</summary>
	public const int PenanceMinimumHpPercent = 70;

	public static NaturalPowderRestChoice Decide(NaturalPowderRestObservation state, DateTimeOffset now,
		IEnumerable<NaturalPriestSkill>? catalog = null)
	{
		NaturalPriestSkill[] skills = (catalog ?? NaturalClericSkills.All).ToArray();
		if (state.Engaged)
			return new("defend", null, "A monster is engaged; any hit cancels a 4 s powder cast, so fight first.");
		bool needHp = state.Hp * 100 < state.MaxHp * HealBelowPercent;
		bool needMp = state.RecoveringMana;
		if (!needHp && !needMp) return new("done", null, "HP and MP are recovered.");
		float hpDeficit = needHp ? (state.MaxHp - state.Hp) / (float)state.MaxHp : 0;
		float mpDeficit = needMp ? (state.MaxMp - state.Mp) / (float)state.MaxMp : 0;
		NaturalPriestSkill? herb = needHp ? NaturalPriestSkills.Best("herb", state.Level, state.Learned, skills) : null;
		NaturalPriestSkill? mana = needMp ? NaturalPriestSkills.Best("mp-recovery", state.Level, state.Learned, skills) : null;
		// Larger deficit first; with both needed, the one not cast last (they share one cooldown).
		NaturalPriestSkill?[] ordered = hpDeficit >= mpDeficit ? [herb, mana] : [mana, herb];
		if (herb != null && mana != null && state.LastPowderSkillId is ushort last)
			ordered = last == herb.Id ? [mana, herb] : last == mana.Id ? [herb, mana] : ordered;
		NaturalPriestSkill[] usable = ordered.OfType<NaturalPriestSkill>()
			.Where(skill => state.ItemCounts.GetValueOrDefault(skill.ReagentItemId) >= skill.ReagentCount).ToArray();
		NaturalPriestSkill? heal = NaturalPriestSkills.Best("heal", state.Level, state.Learned, skills);
		bool healAffordable = heal != null && state.Mp >= heal.ManaCost;
		// AC-00: Penance first when mana is needed: instant, about 1,150 MP over 30 s for about 570 HP, on a 3 min
		// cooldown. Only with HP to spare; the powder and Healing Light put the HP back.
		NaturalPriestSkill? penance = needMp ? NaturalPriestSkills.Best("penance", state.Level, state.Learned, skills) : null;
		if (penance != null && state.Hp * 100 >= state.MaxHp * PenanceMinimumHpPercent &&
			!(state.Cooldowns.TryGetValue(penance.CooldownId, out DateTimeOffset penanceReadyAt) && penanceReadyAt > now))
			return new("penance", penance, $"MP deficit {mpDeficit:P0} and HP to spare: Penance trades HP for mana over 30 s.");
		if (usable.Length > 0)
		{
			if (state.Cooldowns.TryGetValue(NaturalClericSkills.PowderCooldownId, out DateTimeOffset readyAt) && readyAt > now)
			{
				if (!needMp && healAffordable)
					return new("light-heal", heal, "Only HP is missing and the powder skills are cooling down: Healing Light.", readyAt);
				return new("sit", null, "The shared powder cooldown is running and recovery is still needed: sit until it clears.", readyAt);
			}
			NaturalPriestSkill chosen = usable[0];
			return new(chosen.Role, chosen, chosen.Role == "herb"
				? $"HP deficit {hpDeficit:P0}: Herb Treatment for {chosen.ReagentCount} powder."
				: $"MP deficit {mpDeficit:P0}: MP Recovery for {chosen.ReagentCount} powder.");
		}
		bool powderSkill = herb != null || mana != null;
		string why = powderSkill ? "Out of powder" : "No powder skill is learned";
		if (needMp) return new("sit", null, $"{why}: sit to recover mana.");
		return healAffordable
			? new("light-heal", heal, $"{why}: Healing Light for HP.")
			: new("sit", null, $"{why} and Healing Light is unaffordable: sit.");
	}
}
