using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// The accepted line's two profiles. Both are adapters over the static <see cref="NaturalPriestCombatPolicy"/> and its
/// frozen hand-typed skill tables, which stay as they are: the Priest with <see cref="NaturalPriestSkills.All"/>, the
/// Cleric with <see cref="NaturalClericSkills.All"/>.
/// </summary>
public static class NaturalPriestProfile
{
	public static NaturalClassProfile Priest { get; } = new()
	{
		Class = PlayerClass.PRIEST,
		Skills = NaturalPriestSkills.All,
		Excluded = new Dictionary<int, string>(),
		Combat = new StaticPolicy(NaturalPriestSkills.All),
	};

	public static NaturalClassProfile Cleric { get; } = new()
	{
		Class = PlayerClass.CLERIC,
		Skills = NaturalClericSkills.All,
		Excluded = NaturalClericSkills.Excluded,
		Combat = new StaticPolicy(NaturalClericSkills.All),
	};

	/// <summary>Calls the static policy with the class's catalog and the run's parameters, and reports the run's policy id.</summary>
	private sealed class StaticPolicy(NaturalPriestSkill[] catalog) : INaturalCombatPolicy
	{
		public string PolicyVersion(NaturalMauPolicyParameters parameters) => parameters.Id;

		public NaturalCombatChoice Decide(NaturalCombatObservation state, DateTimeOffset now, NaturalMauPolicyParameters parameters) =>
			NaturalPriestCombatPolicy.Decide(state, now, catalog, parameters);

		public NaturalCombatCandidate[] CandidateActions(NaturalCombatObservation state, DateTimeOffset now, NaturalCombatChoice chosen,
			NaturalMauPolicyParameters parameters) => NaturalPriestCombatPolicy.CandidateActions(state, now, chosen, catalog, parameters);
	}
}
