using Aion.Bots.World;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios;

/// <summary>
/// NA-18: what the Cleric's catalog and rest need beside the shipped skill data. NR-18: the hand-typed Cleric table that
/// stood here is gone; the catalog is generated (<see cref="Classes.NaturalPriestProfile"/>). The server adds each new
/// rank on level-up and keeps the old one (Java SkillLearnService), and <see cref="NaturalPriestSkills.Best"/> takes the
/// highest learned rank of a role, so the bot moves to a new rank the moment it is observed. Every active auto-learned
/// skill without a role is in <see cref="Excluded"/> with its reason; the passives need no casting.
/// </summary>
public static class NaturalClericSkills
{
	/// <summary>Lesser Odella Powder, the reagent of the rank 1 powder skills. From level 25 the next ranks
	/// use Odella Powder 169300004; the catalog entry's rank decides, never the item level.</summary>
	public const int LesserOdellaPowder = 169300003;

	/// <summary>Herb Treatment and MP Recovery share this cooldown group (16 s).</summary>
	public const int PowderCooldownId = 1153;

	/// <summary>NR-12: the Priest line's rest-only skills by kind, with the numbers of <see cref="NaturalPowderRestPolicy"/>.
	/// The Priest has learned none of them and rests by its heal and by sitting.</summary>
	public static Classes.NaturalRestSkills RestSkills { get; } = new(
		ReagentHealth: new("herb", "Herb Treatment"), ReagentMana: new("mp-recovery", "MP Recovery"), HealthForMana: new("penance", "Penance"),
		Heal: new("heal", "Healing Light"), SharedCooldownId: PowderCooldownId,
		HealBelowPercent: NaturalPowderRestPolicy.HealBelowPercent,
		HealthForManaMinimumHpPercent: NaturalPowderRestPolicy.PenanceMinimumHpPercent);

	/// <summary>AC-00, BC-05, ND-05: auto-learned active Cleric skills up to level 24 outside the combat/rest rotation, and why. The
	/// profile validator refuses a profile when skill_tree.xml has one that has neither a role nor a reason.</summary>
	public static readonly IReadOnlyDictionary<int, string> Excluded = new Dictionary<int, string>
	{
		[1699] = "Light of Resurrection revives another player; the bot plays solo.",
		[3935] = "Cleanse I removes dispellable debuffs, but the client's effect list does not say which are; the one lasting debuff met so far, the Okaru poison, cannot be dispelled (req_dispel_level 99).",
		[3936] = "Cleanse II: as Cleanse I.",
		[3937] = "Cleanse III: as Cleanse I.",
		[3878] = "Stability raises a target's enmity, a group tank's tool; solo it changes nothing.",
		[3879] = "Stability II: as Stability I.",
		[4005] = "Hand of Reincarnation is prepared for the Bregirun quest attempt (BC-04/BC-06); it is a recovery buff, outside the combat/rest rotation.",
	};
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

	/// <param name="catalog">The class's catalog.</param>
	/// <param name="restSkills">NR-12: the rest-only skills by kind; the Cleric's when not given.</param>
	public static NaturalPowderRestChoice Decide(NaturalPowderRestObservation state, DateTimeOffset now,
		IEnumerable<NaturalPriestSkill> catalog, Classes.NaturalRestSkills? restSkills = null)
	{
		NaturalPriestSkill[] skills = catalog.ToArray();
		Classes.NaturalRestSkills kinds = restSkills ?? NaturalClericSkills.RestSkills;
		if (state.Engaged)
			return new("defend", null, "A monster is engaged; any hit cancels a 4 s powder cast, so fight first.");
		bool needHp = state.Hp * 100 < state.MaxHp * kinds.HealBelowPercent;
		bool needMp = state.RecoveringMana;
		if (!needHp && !needMp) return new("done", null, "HP and MP are recovered.");
		float hpDeficit = needHp ? (state.MaxHp - state.Hp) / (float)state.MaxHp : 0;
		float mpDeficit = needMp ? (state.MaxMp - state.Mp) / (float)state.MaxMp : 0;
		NaturalPriestSkill? herb = needHp ? NaturalPriestSkills.Best(kinds.ReagentHealth.Role, state.Level, state.Learned, skills) : null;
		NaturalPriestSkill? mana = needMp ? NaturalPriestSkills.Best(kinds.ReagentMana.Role, state.Level, state.Learned, skills) : null;
		// Larger deficit first; with both needed, the one not cast last (they share one cooldown).
		NaturalPriestSkill?[] ordered = hpDeficit >= mpDeficit ? [herb, mana] : [mana, herb];
		if (herb != null && mana != null && state.LastPowderSkillId is ushort last)
			ordered = last == herb.Id ? [mana, herb] : last == mana.Id ? [herb, mana] : ordered;
		NaturalPriestSkill[] usable = ordered.OfType<NaturalPriestSkill>()
			.Where(skill => state.ItemCounts.GetValueOrDefault(skill.ReagentItemId) >= skill.ReagentCount).ToArray();
		// NR-50a: a class with no heal has none to fall back on, and one with no health-for-mana skill has none to start with.
		NaturalPriestSkill? heal = kinds.Heal == null ? null : NaturalPriestSkills.Best(kinds.Heal.Role, state.Level, state.Learned, skills);
		bool healAffordable = heal != null && state.Mp >= heal.ManaCost;
		// AC-00: Penance first when mana is needed: instant, about 1,150 MP over 30 s for about 570 HP, on a 3 min
		// cooldown. Only with HP to spare; the powder and Healing Light put the HP back.
		NaturalPriestSkill? penance = needMp && kinds.HealthForMana != null
			? NaturalPriestSkills.Best(kinds.HealthForMana.Role, state.Level, state.Learned, skills) : null;
		// NR-60a: a skill that gives mana at no cost is cast first when mana is needed and it is ready.
		NaturalPriestSkill? free = needMp && kinds.FreeMana != null
			? NaturalPriestSkills.Best(kinds.FreeMana.Role, state.Level, state.Learned, skills) : null;
		if (free != null && !(state.Cooldowns.TryGetValue(free.CooldownId, out DateTimeOffset freeReadyAt) && freeReadyAt > now))
			return new(free.Role, free, $"MP deficit {mpDeficit:P0}: {kinds.FreeMana!.Name} restores mana at no cost.");
		if (penance != null && state.Hp * 100 >= state.MaxHp * kinds.HealthForManaMinimumHpPercent &&
			!(state.Cooldowns.TryGetValue(penance.CooldownId, out DateTimeOffset penanceReadyAt) && penanceReadyAt > now))
			return new(penance.Role, penance, $"MP deficit {mpDeficit:P0} and HP to spare: {kinds.HealthForMana!.Name} trades HP for mana over 30 s.");
		if (usable.Length > 0)
		{
			if (state.Cooldowns.TryGetValue(kinds.SharedCooldownId, out DateTimeOffset readyAt) && readyAt > now)
			{
				if (!needMp && healAffordable)
					return new("light-heal", heal, $"Only HP is missing and the powder skills are cooling down: {kinds.Heal!.Name}.", readyAt);
				return new("sit", null, "The shared powder cooldown is running and recovery is still needed: sit until it clears.", readyAt);
			}
			NaturalPriestSkill chosen = usable[0];
			return new(chosen.Role, chosen, chosen.Role == kinds.ReagentHealth.Role
				? $"HP deficit {hpDeficit:P0}: {kinds.ReagentHealth.Name} for {chosen.ReagentCount} powder."
				: $"MP deficit {mpDeficit:P0}: {kinds.ReagentMana.Name} for {chosen.ReagentCount} powder.");
		}
		bool powderSkill = herb != null || mana != null;
		string why = powderSkill ? "Out of powder" : "No powder skill is learned";
		if (needMp) return new("sit", null, $"{why}: sit to recover mana.");
		if (kinds.Heal == null) return new("sit", null, $"{why}: the life potion or a sit for HP.");
		return healAffordable
			? new("light-heal", heal, $"{why}: {kinds.Heal.Name} for HP.")
			: new("sit", null, $"{why} and {kinds.Heal.Name} is unaffordable: sit.");
	}
}
