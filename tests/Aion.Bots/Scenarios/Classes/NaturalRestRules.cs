using Aion.Bots.World;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>What the client observes between two fights, and what the rest has done so far.</summary>
/// <param name="RecoveringMana">The mana sit is on: set below the sit threshold, cleared at the sit target.</param>
/// <param name="QuietSits">Sits of this rest that no monster interrupted.</param>
/// <param name="LastPowderSkillId">The powder skill cast last in this rest, so the two alternate.</param>
public sealed record NaturalRestObservation(int Level, int Hp, int MaxHp, int Mp, int MaxMp, bool RecoveringMana, int QuietSits,
	IReadOnlyDictionary<int, BotSkill> Learned, IReadOnlyDictionary<int, DateTimeOffset> Cooldowns,
	IReadOnlyDictionary<int, long> ItemCounts, ushort? LastPowderSkillId, DateTimeOffset Now);

/// <param name="Action"><see cref="NaturalRestRules.Powder"/>, <see cref="NaturalRestRules.CastHeal"/>,
/// <see cref="NaturalRestRules.SitForMana"/>, <see cref="NaturalRestRules.Done"/> or <see cref="NaturalRestRules.Blocked"/>.</param>
/// <param name="Skill">The powder skill or the heal to cast.</param>
/// <param name="RecoveringMana">The mana sit after this observation.</param>
/// <param name="ManaRecovered">The mana sit ended with this observation: the next one needs a new rest spot.</param>
/// <param name="PowderChoice">What the powder policy said, whenever a powder skill is learned; the caller traces it.</param>
/// <param name="BlockedReason">Why the rest cannot go on.</param>
public sealed record NaturalRestDecision(string Action, NaturalPriestSkill? Skill, bool RecoveringMana, bool ManaRecovered,
	NaturalPowderRestChoice? PowderChoice, string? BlockedReason);

/// <summary>
/// CP-17: how a class recovers between fights, as one pure step (docs/natural-class-profiles.md). The journey's rest
/// loop observes, asks, and carries the answer out: it casts, sits by <see cref="NaturalRestCadence"/>, defends when a
/// rest is interrupted and revives. The Priest line's plan: powder first where its skills are learned
/// (<see cref="NaturalPowderRestPolicy"/>), then the own heal while HP is below <paramref name="HealBelowPercent"/>, and
/// sitting only for mana, from below <paramref name="ManaSitBelowPercent"/> until <paramref name="ManaSitUntilPercent"/>,
/// for at most <paramref name="MaximumQuietSits"/> undisturbed sits.
/// </summary>
/// <param name="Skills">The class's skill catalog.</param>
public sealed record NaturalRestRules(NaturalPriestSkill[] Skills, int HealBelowPercent, int ManaSitBelowPercent, int ManaSitUntilPercent,
	int MaximumQuietSits)
{
	public const string Powder = "powder", CastHeal = "cast-heal", SitForMana = "sit-for-mana", Done = "done", Blocked = "blocked";
	public const string NoSelfHeal = "Priest has mana but no client-observed usable self-heal between fights.";
	public const string NotRecovered = "Priest could not recover HP/MP before the next pull within bounded healing and mana-rest attempts.";

	public NaturalRestDecision Decide(NaturalRestObservation state)
	{
		bool recovering = state.RecoveringMana, recovered = false;
		if (state.Mp * 100 < state.MaxMp * ManaSitBelowPercent) recovering = true;
		if (recovering && state.Mp * 100 >= state.MaxMp * ManaSitUntilPercent)
		{
			recovering = false;
			recovered = true;
		}
		// NA-18 (OD-9): powder first. Sitting and the own heal stay the fallback below.
		NaturalPowderRestChoice? powder = null;
		if (Skills.Any(skill => skill.IsPowderRest && state.Learned.ContainsKey(skill.Id)))
		{
			powder = NaturalPowderRestPolicy.Decide(new NaturalPowderRestObservation(
				state.Level, state.Hp, state.MaxHp, state.Mp, state.MaxMp, recovering, state.Learned,
				state.Cooldowns, state.ItemCounts, LastPowderSkillId: state.LastPowderSkillId), state.Now, Skills);
			if (powder.Skill is { IsRestSkill: true } restSkill) return new(Powder, restSkill, recovering, recovered, powder, null);
		}
		if (!recovering)
		{
			if (state.Hp * 100 >= state.MaxHp * HealBelowPercent) return new(Done, null, recovering, recovered, powder, null);
			NaturalPriestSkill? heal = NaturalPriestSkills.Best("heal", state.Level, state.Learned, Skills);
			return heal == null || state.Mp < heal.ManaCost
				? new(Blocked, null, recovering, recovered, powder, NoSelfHeal)
				: new(CastHeal, heal, recovering, recovered, powder, null);
		}
		return state.QuietSits >= MaximumQuietSits
			? new(Blocked, null, recovering, recovered, powder, NotRecovered)
			: new(SitForMana, null, recovering, recovered, powder, null);
	}
}
