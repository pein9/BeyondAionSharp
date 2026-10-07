using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// The accepted line's two profiles. Both are adapters over the static <see cref="NaturalPriestCombatPolicy"/> and its
/// frozen hand-typed skill tables, which stay as they are: the Priest with <see cref="NaturalPriestSkills.All"/>, the
/// Cleric with <see cref="NaturalClericSkills.All"/>.
/// </summary>
public static class NaturalPriestProfile
{
	/// <summary>Blessing of Guardianship, kept up between fights as the recorded human did.</summary>
	private static readonly NaturalUpkeepBuff Blessing = new("blessing", "buff-blessing");

	public static NaturalClassProfile Priest { get; } = new()
	{
		Class = PlayerClass.PRIEST,
		Skills = NaturalPriestSkills.All,
		Excluded = new Dictionary<int, string>(),
		Combat = new StaticPolicy(NaturalPriestSkills.All),
		// CP-06: the level 1-9 kit, and nothing from level 10 on.
		HelpItems = new(NaturalHelpItemAllowlist.Starter, NaturalHelpItemAllowlist.StarterMaxLevel),
		Upkeep = [Blessing],
		PatrolRule = NaturalPatrolRule.Baseline,
		RangedHold = NaturalRangedHold.RunOption,
	};

	public static NaturalClassProfile Cleric { get; } = new()
	{
		Class = PlayerClass.CLERIC,
		Skills = NaturalClericSkills.All,
		Excluded = NaturalClericSkills.Excluded,
		Combat = new StaticPolicy(NaturalClericSkills.All),
		// OD-13: the approved kit at every level.
		HelpItems = new(NaturalHelpItemAllowlist.AllLevels.ToArray(), null),
		Upkeep = [Blessing],
		// NA-22 (OD-14).
		PatrolRule = NaturalPatrolRule.HoldAndAssess,
		RangedHold = NaturalRangedHold.RunOption,
	};

	/// <summary>Calls the static policy with the class's catalog and the run's parameters, and reports the run's policy id.</summary>
	private sealed class StaticPolicy(NaturalPriestSkill[] catalog) : INaturalCombatPolicy
	{
		public string PolicyVersion(NaturalMauPolicyParameters parameters) => parameters.Id;

		public int EmergencyEnterPercent(int attackers, bool targetSeasoned) =>
			NaturalPriestCombatPolicy.EmergencyEnterPercent(attackers, targetSeasoned);

		public int EmergencyExitPercent(int attackers, bool targetSeasoned) =>
			NaturalPriestCombatPolicy.EmergencyExitPercent(attackers, targetSeasoned);

		public NaturalCombatChoice Decide(NaturalCombatObservation state, DateTimeOffset now, NaturalMauPolicyParameters parameters) =>
			NaturalPriestCombatPolicy.Decide(state, now, catalog, parameters);

		public NaturalCombatCandidate[] CandidateActions(NaturalCombatObservation state, DateTimeOffset now, NaturalCombatChoice chosen,
			NaturalMauPolicyParameters parameters) => NaturalPriestCombatPolicy.CandidateActions(state, now, chosen, catalog, parameters);
	}
}
