namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// One class's one-step fight decision (docs/natural-class-profiles.md, the seam, section 2). Pure: the caller supplies
/// only observed client state and the run's policy parameters, and the policy never sends a packet.
/// </summary>
public interface INaturalCombatPolicy
{
	/// <summary>The version written into every combat-decision record.</summary>
	string PolicyVersion(NaturalMauPolicyParameters parameters);

	/// <summary>The HP percentage at or below which a fight becomes an emergency: sustain only, until
	/// <see cref="EmergencyExitPercent"/>.</summary>
	/// <param name="attackers">Monsters observed on the bot.</param>
	/// <param name="targetSeasoned">The target is of Seasoned rank or better.</param>
	int EmergencyEnterPercent(int attackers, bool targetSeasoned);

	/// <summary>The HP percentage at or above which the emergency is over.</summary>
	int EmergencyExitPercent(int attackers, bool targetSeasoned);

	NaturalCombatChoice Decide(NaturalCombatObservation state, DateTimeOffset now, NaturalMauPolicyParameters parameters);

	/// <summary>Every client-observable candidate action after <see cref="Decide"/>, with the branches it did not visit.</summary>
	NaturalCombatCandidate[] CandidateActions(NaturalCombatObservation state, DateTimeOffset now, NaturalCombatChoice chosen,
		NaturalMauPolicyParameters parameters);
}
