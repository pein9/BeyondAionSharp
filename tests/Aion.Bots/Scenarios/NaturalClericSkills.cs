using Aion.Bots.World;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios;

/// <summary>
/// NA-18: frozen 4.8 Cleric level 10 active skills on top of the Priest's, which carry over at Ascension
/// (docs/natural-ascension-altgard.md Appendix C). Java skill_tree.xml and skill_templates.xml at ce54b7931.
/// The learned SM_SKILL_LIST is still the authority: this catalog never grants a skill.
/// Light of Resurrection (solo play), the passives and Winged Recovery are left out on purpose.
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
	];

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

/// <param name="Action">defend, done, herb, mp-recovery, light-heal or sit.</param>
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
