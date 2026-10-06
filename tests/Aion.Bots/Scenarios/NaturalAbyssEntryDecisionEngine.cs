using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>One next action of the Morheim and Abyss-entry leg.</summary>
/// <param name="Phase">Which part of the operator's order the action belongs to.</param>
/// <param name="Action"><c>travel</c> (the approved teleport to <paramref name="MapId"/>), <c>bind</c>, <c>talk</c> (the contract step
/// <paramref name="StepKey"/>), <c>coin-armor</c> (buy the tier's better pieces), <c>inventory-check</c>, <c>arena-fight</c> (the
/// next kill of Garm's test), <c>arena-leave</c> (out through the arena's exit), <c>revive</c>, <c>refresh-observation</c>,
/// <c>frontier</c> (the next phase is not played yet: the segment ends here) or <c>blocked</c>.</param>
public sealed record NaturalAbyssEntryDecision(int Sequence, string Phase, string Action, string? StepKey, int? QuestId, int? MapId, string Reason);

/// <summary>
/// AX-05 (docs/natural-abyss-entry.md): the leg's pure decision rule over its contract and a copied client observation, in the
/// operator's order of 2026-10-06. Morheim and the commander come first: the teleport, the bind at Morheim Ice Fortress on
/// arrival, and the talk with Aegir for Q24020, which the server starts on entering Morheim. AX-06: then the level-21 coin
/// armor, for each slot where the tier's piece beats what is worn. AX-07: then the capital missions by their talk steps, with
/// the approved teleport whenever the next step is on another map. A step is chosen from the quest's observed status and var
/// alone, so a resumed run repeats no dialog. AX-08: then Garm's arena. Garm's talk sends the Cleric in (D35); inside, the
/// rule asks for one kill at a time until ten are counted; a failed attempt (var 6: the timer, or a death, D36) goes back to
/// Garm for the next of three tries. Each later phase is added by its own AX item; until then the rule names it as the
/// frontier, and the segment ends there.
/// </summary>
public static class NaturalAbyssEntryDecisionEngine
{
	public const string ObservePhase = "observe", RecoverPhase = "recover", MorheimPhase = "morheim-arrival", CoinArmor21Phase = "coin-armor-21",
		CapitalPhase = "capital-missions", ArenaPhase = "arena", ReturnPhase = "morheim-return", RingCoursePhase = "ring-course",
		EndpointPhase = "endpoint";
	private const byte Start = NaturalAltgardDecisionEngine.Start, Reward = NaturalAltgardDecisionEngine.Reward;

	/// <summary>Q2947's kill counter as the client sees it. Java writes <c>step | flags &lt;&lt; 24</c> (SM_QUEST_ACTION), so the
	/// fifth six-bit var shares bits 24 to 29 with the flag byte; Q2947 sets no flag, and the client shows the same count.</summary>
	public static int ArenaKills(NaturalAbyssArena arena, BotQuestState quest) => (quest.StepAndFlags >> (arena.KillCounter * 6)) & 0x3F;

	/// <param name="physicalDefence">An item's physical defence as its tooltip shows it.</param>
	/// <param name="attempts">The arena and ring-course tries recorded so far in this run.</param>
	public static NaturalAbyssEntryDecision Decide(NaturalAltgardContract leg, NaturalAltgardObservation state, int sequence, Func<int, int> physicalDefence,
		IReadOnlyList<NaturalAbyssAttempt>? attempts = null)
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

		// 2. The level-21 coin armor, at once: only the slots where the tier's piece beats what is worn.
		NaturalAbyssCoinTier early = scope.CoinArmor.Tiers.Single(tier => tier.When == "after-commander");
		NaturalAbyssCoinManifest manifest = NaturalAbyssCoinArmorPolicy.Plan(scope.CoinArmor, early,
			state.Inventory ?? throw new InvalidDataException("The coin armor needs the observed inventory."), physicalDefence);
		if (manifest.Wears.Length > 0)
			return Next(CoinArmor21Phase, "inventory-check", $"Wear the owned {early.Name} pieces: {Pieces(manifest.Wears)}.");
		if (manifest.Buys.Length > 0)
			return here != NaturalAbyssEntry.Morheim
				? Next(CoinArmor21Phase, "blocked", $"The {early.Name} pieces {Pieces(manifest.Buys)} are sold in Morheim; the Cleric is on map {here}.")
				: Next(CoinArmor21Phase, "coin-armor", $"Buy the {early.Name} pieces that beat what is worn: {Pieces(manifest.Buys)} for {manifest.Cost} coins.");

		// 3. The capital missions: Q2945 and Q2946 by their talk steps, then Q2947's start at Kvasir. Each starts when the one
		// before it is turned in.
		foreach (int mission in scope.MissionIds)
		{
			if (state.CompletedQuestIds.Contains(mission)) continue;
			// 4. Garm's arena: everything of Q2947 after Kvasir's var 0.
			if (mission == scope.Arena.QuestId && state.Quests.TryGetValue(mission, out BotQuestState? trial) &&
				(trial.Status == Reward || trial.Status == Start && (trial.StepAndFlags & 0x3F) != 0))
				return ArenaStep(trial);
			if (mission == scope.RingCourse.QuestId)
				return Next(RingCoursePhase, "frontier", $"Q{mission} is next (AX-09, AX-10).", quest: mission);
			return QuestStep(CapitalPhase, mission, "starts when the mission before it is turned in");
		}
		return Next(EndpointPhase, "frontier", "The missions are done; the level check is next (AX-11).");

		static string Pieces(NaturalAbyssCoinSlot[] slots) => string.Join(", ", slots.Select(slot => slot.CoinItemId));

		NaturalAbyssEntryDecision ArenaStep(BotQuestState trial)
		{
			NaturalAbyssArena arena = scope.Arena;
			int questId = arena.QuestId, var = trial.StepAndFlags & 0x3F, kills = ArenaKills(arena, trial);
			bool inside = here == arena.MapId;
			if (trial.Status == Reward)
				return Next(ReturnPhase, "frontier", "The arena is cleared and reported to Garm; Aegir's reward in Morheim is next (AX-09).", quest: questId);
			if (var == arena.EnterVar && kills < arena.RequiredKills)
				return inside
					? Next(ArenaPhase, "arena-fight", $"{kills} of {arena.RequiredKills} spirits are counted: the next kill.", quest: questId)
					: Next(ArenaPhase, "refresh-observation", "Outside the arena on a running attempt; the server fails it on this world entry.", quest: questId);
			// Inside with nothing left to count: the test is passed (movie 168's end teleports out), or the attempt failed at a
			// death the Cleric revived from in place. Either way the exit beside the entry leads back to Garm.
			if (inside)
				return Next(ArenaPhase, "arena-leave", var == arena.EnterVar ? "Ten spirits are counted: leave the arena." : $"Q{questId} is at var {var} inside the arena: leave by the exit.", quest: questId);
			if (var == arena.EnterVar)
				return StepOrTravel(ArenaPhase, questId, leg.Steps.Single(step => step.Key == arena.DoneStep), "ten spirits are counted: report to Garm");
			NaturalAbyssAttemptDecision attempt = NaturalAbyssAttempts.NextArena(arena, attempts ?? []);
			if (attempt.Action == "stop-finding")
				return Next(ArenaPhase, "blocked", attempt.Reason, quest: questId);
			NaturalAltgardStep? garm = leg.StepsFor(questId).FirstOrDefault(step => step.ExpectedStatus == "START" && step.Var == var &&
				(step.Key == arena.StartStep || step.Key == arena.RestartStep));
			return garm == null
				? Next(ArenaPhase, "blocked", $"Q{questId} is at var {var}; no arena step covers it.", quest: questId)
				: StepOrTravel(ArenaPhase, questId, garm, attempt.Reason);
		}

		NaturalAbyssEntryDecision StepOrTravel(string phase, int questId, NaturalAltgardStep step, string why)
		{
			int stepMap = leg.StepMap(step);
			if (stepMap != here)
				return leg.MapTripList.Any(trip => trip.MapId == stepMap && trip.FromMapId == here)
					? Next(phase, "travel", $"Q{questId}'s next step ({step.Key}) is on map {stepMap}: take the teleport from map {here}.", quest: questId, map: stepMap)
					: Next(phase, "blocked", $"Q{questId}'s next step ({step.Key}) is on map {stepMap}, and no approved teleport leads there from map {here}.", quest: questId);
			return Next(phase, "talk", $"Q{questId} {why}: {step.Key}.", step.Key, questId, stepMap);
		}

		NaturalAbyssEntryDecision QuestStep(string phase, int questId, string howItStarts)
		{
			if (!state.Quests.TryGetValue(questId, out BotQuestState? quest) || quest.Status is not (Start or Reward))
				return Next(phase, "refresh-observation", $"Q{questId} {howItStarts}; it is not in the journal as started yet.", quest: questId);
			int var = quest.StepAndFlags & 0x3F;
			NaturalAltgardStep? step = leg.StepsFor(questId).FirstOrDefault(candidate => quest.Status == Reward
				? candidate.ExpectedStatus == "REWARD" : candidate.ExpectedStatus == "START" && candidate.Var == var);
			return step == null
				? Next(phase, "blocked", $"Q{questId} is at status {quest.Status}, var {var}; no contract step covers it.", quest: questId)
				: StepOrTravel(phase, questId, step, quest.Status == Reward ? "reward" : $"var {var}");
		}
	}
}
