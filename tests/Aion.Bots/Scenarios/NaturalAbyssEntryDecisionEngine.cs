using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>One next action of the Morheim and Abyss-entry leg.</summary>
/// <param name="Phase">Which part of the operator's order the action belongs to.</param>
/// <param name="Action"><c>travel</c> (the approved teleport to <paramref name="MapId"/>), <c>bind</c>, <c>talk</c> (the contract step
/// <paramref name="StepKey"/>), <c>revive</c>, <c>refresh-observation</c>, <c>frontier</c> (the next phase is not played yet: the
/// segment ends here) or <c>blocked</c>.</param>
public sealed record NaturalAbyssEntryDecision(int Sequence, string Phase, string Action, string? StepKey, int? QuestId, int? MapId, string Reason);

/// <summary>
/// AX-05 (docs/natural-abyss-entry.md): the leg's pure decision rule over its contract and a copied client observation, in the
/// operator's order of 2026-10-06. Morheim and the commander come first: the teleport, the bind at Morheim Ice Fortress on
/// arrival, and the talk with Aegir for Q24020, which the server starts on entering Morheim. Each later phase is added by its
/// own AX item; until then the rule names it as the frontier, and the segment ends there.
/// </summary>
public static class NaturalAbyssEntryDecisionEngine
{
	public const string ObservePhase = "observe", RecoverPhase = "recover", MorheimPhase = "morheim-arrival", CoinArmor21Phase = "coin-armor-21";
	private const byte Start = NaturalAltgardDecisionEngine.Start, Reward = NaturalAltgardDecisionEngine.Reward;

	public static NaturalAbyssEntryDecision Decide(NaturalAltgardContract leg, NaturalAltgardObservation state, int sequence)
	{
		NaturalAbyssEntry scope = leg.AbyssEntry ?? throw new InvalidDataException($"{leg.Leg} is not the Abyss-entry leg.");
		NaturalAbyssEntryDecision Next(string phase, string action, string reason, string? step = null, int? quest = null, int? map = null) =>
			new(sequence, phase, action, step, quest, map, reason);

		if (!state.Synchronized || state.MapId is not int here)
			return Next(ObservePhase, "refresh-observation", "Wait for a synchronized client view.");
		if (state.IsDead)
			return Next(RecoverPhase, "revive", "Dead: revive at the bind point and recover.");

		// 1. Morheim and the commander.
		int commander = scope.CommanderQuestId;
		if (!state.CompletedQuestIds.Contains(commander))
		{
			if (here != NaturalAbyssEntry.Morheim)
				return leg.MapTripList.Any(trip => trip.MapId == NaturalAbyssEntry.Morheim && trip.FromMapId == here)
					? Next(MorheimPhase, "travel", $"Take the teleport from map {here} to Morheim before anything else.", map: NaturalAbyssEntry.Morheim)
					: Next(MorheimPhase, "blocked", $"No approved teleport leads from map {here} to Morheim.");
			NaturalAltgardBind bind = leg.Bind ?? throw new InvalidDataException($"{leg.Leg} has no bind.");
			if (!NaturalAltgardDecisionEngine.BoundAt(bind, NaturalAbyssEntry.Morheim, state.Bind))
				return Next(MorheimPhase, "bind", $"Bind at Morheim Ice Fortress (obelisk {bind.NpcId}) on arrival.");
			return QuestStep(MorheimPhase, commander, "starts on entering Morheim");
		}

		// 2. The level-21 coin armor (AX-06).
		return Next(CoinArmor21Phase, "frontier", "Morheim and the commander are done; the level-21 coin armor is next (AX-06).");

		NaturalAbyssEntryDecision QuestStep(string phase, int questId, string howItStarts)
		{
			if (!state.Quests.TryGetValue(questId, out BotQuestState? quest) || quest.Status is not (Start or Reward))
				return Next(phase, "refresh-observation", $"Q{questId} {howItStarts}; it is not in the journal as started yet.", quest: questId);
			int var = quest.StepAndFlags & 0x3F;
			NaturalAltgardStep? step = leg.StepsFor(questId).FirstOrDefault(candidate => quest.Status == Reward
				? candidate.ExpectedStatus == "REWARD" : candidate.ExpectedStatus == "START" && candidate.Var == var);
			return step == null
				? Next(phase, "blocked", $"Q{questId} is at status {quest.Status}, var {var}; no contract step covers it.", quest: questId)
				: Next(phase, "talk", $"Q{questId} {(quest.Status == Reward ? "reward" : $"var {var}")}: {step.Key}.", step.Key, questId, leg.StepMap(step));
		}
	}
}
