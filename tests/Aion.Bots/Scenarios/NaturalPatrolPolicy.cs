namespace Aion.Bots.Scenarios;

/// <summary>What the client sees when a patrol or its helpers block a planned pull (docs/natural-ascension-altgard.md NA-22).</summary>
/// <param name="CompletedWaits">15 s holds already spent on this blockage.</param>
/// <param name="MemberLevels">Levels of every monster the pull would bring: the target and its expected helpers.</param>
/// <param name="HealReady">Healing Light is learned and affordable.</param>
/// <param name="HotReady">Light of Rejuvenation (the heal over time) is learned and affordable (5 s cooldown).</param>
/// <param name="SalvationReady">Salvation is learned and observed DP pays for it.</param>
/// <param name="BuffsUp">The class buff and the casting-speed scroll are observed (NA-19 ran first).</param>
/// <param name="PotionStock">Owned life potions and elixirs.</param>
/// <param name="RerouteAvailable">Another pull spot or corridor away from the patrol is known.</param>
public sealed record NaturalPatrolObservation(bool Cleric, int CompletedWaits, int Level, int Hp, int MaxHp, int Mp, int MaxMp,
	IReadOnlyList<int> MemberLevels, bool HealReady, bool HotReady, bool SalvationReady, bool BuffsUp, long PotionStock,
	bool RerouteAvailable);

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
	/// <summary>The combat policy leaves from this many attackers, so a pull that brings them is never taken.</summary>
	public const int MaximumMembers = NaturalPriestCombatPolicy.SwarmedAttackers - 1;
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
		int members = state.MemberLevels.Count;
		if (members > MaximumMembers) failed.Add($"{members} monsters would come; the Cleric leaves from {MaximumMembers + 1}.");
		if (state.MemberLevels.Any(level => level > state.Level + LevelAllowance))
			failed.Add($"A member is more than {LevelAllowance} levels above {state.Level}.");
		if (state.Hp * 100 < state.MaxHp * 70) failed.Add("HP is below 70%.");
		if (state.Mp * 100 < state.MaxMp * 50) failed.Add("MP is below 50%.");
		if (!state.HealReady) failed.Add("Healing Light is not ready.");
		if (members >= 2)
		{
			if (!state.HotReady && !state.SalvationReady) failed.Add("Two monsters need the heal over time or Salvation ready.");
			if (!state.BuffsUp && state.PotionStock < 3) failed.Add("Two monsters need the buffs up or at least three potions.");
		}
		return failed.Count == 0 ? (true, ["winnable"]) : (false, [.. failed]);
	}
}
