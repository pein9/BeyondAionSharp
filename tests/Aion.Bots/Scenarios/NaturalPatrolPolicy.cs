using Aion.Bots.Scenarios.Classes;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios;

/// <summary>
/// NR-37: what the patrol rule asks a class about, from its profile: the recovery it looks at by role, and how many
/// monsters a pull may bring. A role the class does not have is null, and the rule then asks nothing about it.
/// </summary>
/// <param name="HealRole">The class's heal: it must be learned and paid for before a blocked pull is taken.</param>
/// <param name="HealOverTimeRole">A heal kept up while the class is attacked.</param>
/// <param name="RescueRole">A recovery skill paid with DP. Against two monsters one of these two must be ready.</param>
/// <param name="MaximumMembers">The most monsters a pull may bring: one fewer than the class's fight table leaves at.</param>
/// <param name="ClassName">The class as the assessment names it.</param>
/// <param name="HealName">The heal as the assessment names it.</param>
/// <param name="PairNeeds">What two monsters need ready, as the assessment names it.</param>
public sealed record NaturalPatrolView(string? HealRole, string? HealOverTimeRole, string? RescueRole, int MaximumMembers,
	string ClassName, string HealName, string PairNeeds)
{
	/// <summary>The Cleric's, in the words NA-22 gave the rule; a caller that names no view gets it.</summary>
	public static NaturalPatrolView Cleric { get; } = new("heal", "rejuvenation", "salvation", NaturalPatrolPolicy.MaximumMembers,
		"Cleric", "Healing Light", "the heal over time or Salvation");

	/// <summary>
	/// A class's view from its fight table. The heal is the table's reserve role, or else the last skill step of its
	/// recovery ladder that is paid with mana and is not for emergencies only. The heal over time is the upkeep cast
	/// only under attack. The rescue is the first ladder skill paid with DP. The pull limit is one fewer than the
	/// attackers the table leaves at.
	/// </summary>
	public static NaturalPatrolView From(NaturalRotationRules rules, IReadOnlyList<NaturalPriestSkill> catalog, PlayerClass playerClass)
	{
		string[] ladder = [.. rules.Recovery.Where(step => step.Kind == NaturalRecoveryKind.Skill && step.Role != null && !step.EmergencyOnly)
			.Select(step => step.Role!)];
		string? heal = rules.ReserveRole ?? ladder.LastOrDefault(role => catalog.Any(skill => skill.Role == role && skill.DpCost == 0));
		string? overTime = rules.Upkeep.FirstOrDefault(upkeep => upkeep.UnderAttackOnly)?.Role;
		string? rescue = ladder.FirstOrDefault(role => catalog.Any(skill => skill.Role == role && skill.DpCost > 0));
		string className = string.Join(' ', playerClass.ToString().Split('_').Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
		string[] pair = [.. new[] { overTime, rescue }.OfType<string>().Select(role => $"the {role} skill")];
		return new(heal, overTime, rescue, rules.SwarmAttackers - 1, className, heal == null ? "" : $"The {heal} skill", string.Join(" or ", pair));
	}
}

/// <summary>What the client sees when a patrol or its helpers block a planned pull (docs/natural-ascension-altgard.md NA-22).</summary>
/// <param name="CompletedWaits">15 s holds already spent on this blockage.</param>
/// <param name="MemberLevels">Levels of every monster the pull would bring: the target and its expected helpers.</param>
/// <param name="HealReady">Healing Light is learned and affordable.</param>
/// <param name="HotReady">Light of Rejuvenation (the heal over time) is learned and affordable (5 s cooldown).</param>
/// <param name="SalvationReady">Salvation is learned and observed DP pays for it.</param>
/// <param name="BuffsUp">The class buff and the casting-speed scroll are observed (NA-19 ran first).</param>
/// <param name="PotionStock">Owned life potions and elixirs.</param>
/// <param name="RerouteAvailable">Another pull spot or corridor away from the patrol is known.</param>
/// <param name="Cleric">The class holds and assesses (its profile's <see cref="NaturalPatrolRule.HoldAndAssess"/>): the
/// Cleric, and since NR-37 every second class.</param>
/// <param name="View">NR-37: the class's view; the Cleric's when not given. The three recovery flags are those of its roles.</param>
public sealed record NaturalPatrolObservation(bool Cleric, int CompletedWaits, int Level, int Hp, int MaxHp, int Mp, int MaxMp,
	IReadOnlyList<int> MemberLevels, bool HealReady, bool HotReady, bool SalvationReady, bool BuffsUp, long PotionStock,
	bool RerouteAvailable, NaturalPatrolView? View = null);

/// <param name="Action">baseline (the Priest keeps its own rule), wait, fight, reroute or pull-anyway.</param>
/// <param name="WaitMillis">Game time to hold before planning again (wait only).</param>
public sealed record NaturalPatrolDecision(string Action, int WaitMillis, bool Winnable, string Reason, string[] Assessment);

/// <summary>
/// NA-22 (OD-14): a Cleric blocked by a patrol holds outside every aggro circle for 15 s of game time and plans
/// again, at most four times per blockage; then it takes the fight at the planned spot when the assessment says it
/// is winnable, reroutes when another way is known, and otherwise pulls anyway. A death is recorded, not failed
/// (OD-12). The frozen Ishalgen Priest keeps its baseline rule.
/// </summary>
public static class NaturalPatrolPolicy
{
	public const int WaitMillis = 15_000;
	public const int MaximumWaits = 4;
	/// <summary>The Priest line's rule tables leave from one attacker more than this (their SwarmAttackers), so a pull
	/// that brings that many is never taken.</summary>
	public const int MaximumMembers = 2;
	public const int LevelAllowance = 2;

	public static NaturalPatrolDecision Decide(NaturalPatrolObservation state)
	{
		if (!state.Cleric)
			return new("baseline", 0, false, "The Ishalgen Priest keeps its baseline patrol wait.", []);
		(bool winnable, string[] assessment) = Assess(state);
		if (state.CompletedWaits < MaximumWaits)
			return new("wait", WaitMillis, winnable,
				$"Hold 15 s outside every circle for the patrol to move on (wait {state.CompletedWaits + 1} of {MaximumWaits}).", assessment);
		if (winnable)
			return new("fight", 0, true, "The patrol stayed; the assessment says the fight is winnable: pull at the planned spot.", assessment);
		if (state.RerouteAvailable)
			return new("reroute", 0, false, "The patrol stayed and the fight is not winnable: take another way.", assessment);
		return new("pull-anyway", 0, false,
			"The patrol stayed, the fight is not winnable and no other way is known: pull; a death is recorded, not failed (OD-12).", assessment);
	}

	public static (bool Winnable, string[] Assessment) Assess(NaturalPatrolObservation state)
	{
		var failed = new List<string>();
		// NR-37: the class's own pull limit and recovery; a recovery it does not have is not asked for.
		NaturalPatrolView view = state.View ?? NaturalPatrolView.Cleric;
		int members = state.MemberLevels.Count;
		if (members > view.MaximumMembers)
			failed.Add($"{members} monsters would come; the {view.ClassName} leaves from {view.MaximumMembers + 1}.");
		if (state.MemberLevels.Any(level => level > state.Level + LevelAllowance))
			failed.Add($"A member is more than {LevelAllowance} levels above {state.Level}.");
		if (state.Hp * 100 < state.MaxHp * 70) failed.Add("HP is below 70%.");
		if (state.Mp * 100 < state.MaxMp * 50) failed.Add("MP is below 50%.");
		if (view.HealRole != null && !state.HealReady) failed.Add($"{view.HealName} is not ready.");
		if (members >= 2)
		{
			if ((view.HealOverTimeRole != null || view.RescueRole != null) && !state.HotReady && !state.SalvationReady)
				failed.Add($"Two monsters need {view.PairNeeds} ready.");
			if (!state.BuffsUp && state.PotionStock < 3) failed.Add("Two monsters need the buffs up or at least three potions.");
		}
		return failed.Count == 0 ? (true, ["winnable"]) : (false, [.. failed]);
	}
}
