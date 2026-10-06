using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>One next action of the Morheim and Abyss-entry leg.</summary>
/// <param name="Phase">Which part of the operator's order the action belongs to.</param>
/// <param name="Action"><c>travel</c> (the approved teleport to <paramref name="MapId"/>), <c>bind</c>, <c>talk</c> (the contract step
/// <paramref name="StepKey"/>), <c>coin-armor</c> (buy the tier's better pieces), <c>inventory-check</c>, <c>arena-fight</c> (the
/// next kill of Garm's test), <c>arena-leave</c> (out through the arena's exit), <c>ring-course-start</c> (Yornduf's talk
/// <paramref name="StepKey"/>, which starts the clock), <c>ring-course-fly</c> (the rings still to pass, then the landing),
/// <c>revive</c>, <c>refresh-observation</c>, <c>frontier</c> (the next phase is not played yet: the segment ends here) or
/// <c>blocked</c>.</param>
public sealed record NaturalAbyssEntryDecision(int Sequence, string Phase, string Action, string? StepKey, int? QuestId, int? MapId, string Reason);

/// <summary>
/// AX-05 (docs/natural-abyss-entry.md): the leg's pure decision rule over its contract and a copied client observation, in the
/// operator's order of 2026-10-06. Morheim and the commander come first: the teleport, the bind at Morheim Ice Fortress on
/// arrival, and the talk with Aegir for Q24020, which the server starts on entering Morheim. AX-06: then the level-21 coin
/// armor, for each slot where the tier's piece beats what is worn. AX-07: then the capital missions by their talk steps, with
/// the approved teleport whenever the next step is on another map. A step is chosen from the quest's observed status and var
/// alone, so a resumed run repeats no dialog. AX-08: then Garm's arena. Garm's talk sends the Cleric in (D35); inside, the
/// rule asks for one kill at a time until ten are counted; a failed attempt (var 6: the timer, or a death, D36) goes back to
/// Garm for the next of three tries. AX-09: then back to Morheim for Q2947's reward at Aegir and Q2042's first talk with
/// him. AX-10: then Yornduf's ring course. His talk starts the 70 s; the rule asks for the flight while rings are left, for
/// the report once the sixth is passed, and for Yornduf again after a failed try (var 9), up to three tries; after the third
/// it stops and asks for the operator's recorded flight. AX-11: then the level. There are no level goals, only questing
/// goals (AX-Q6): the five quests pay enough for level 26, and a Cleric that is still below it (many deaths) stops the leg
/// as a finding, to be given fortress quests; it is never sent hunting. AX-12: then the level-26 coin armor, again only the
/// slots where the tier's piece beats what is worn. Each later phase is added by its own AX item; until then the rule names
/// it as the frontier, and the segment ends there.
/// </summary>
public static class NaturalAbyssEntryDecisionEngine
{
	public const string ObservePhase = "observe", RecoverPhase = "recover", MorheimPhase = "morheim-arrival", CoinArmor21Phase = "coin-armor-21",
		CapitalPhase = "capital-missions", ArenaPhase = "arena", ReturnPhase = "morheim-return", RingCoursePhase = "ring-course",
		LevelPhase = "level-by-quests", CoinArmor26Phase = "coin-armor-26", EndpointPhase = "endpoint";

	/// <summary>The coin armor tier a coin-armor decision of <paramref name="phase"/> buys from.</summary>
	public static NaturalAbyssCoinTier CoinTier(NaturalAbyssCoinArmor armor, string phase) => phase switch
	{
		CoinArmor21Phase => armor.Tiers.Single(tier => tier.When == "after-commander"),
		CoinArmor26Phase => armor.Tiers.Single(tier => tier.When == "endpoint"),
		_ => throw new ArgumentException($"The phase '{phase}' buys no coin armor.", nameof(phase)),
	};
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
			return Next(RecoverPhase, "revive", here == scope.Arena.MapId
				? "Dead in the arena: take the revive the client offers, inside it (D38), and recover."
				: "Dead: revive at the bind point, soul heal and recover.");

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
		if (CoinArmorStep(CoinArmor21Phase) is { } early) return early;

		// 3. The capital missions: Q2945 and Q2946 by their talk steps, then Q2947's start at Kvasir. Each starts when the one
		// before it is turned in.
		foreach (int mission in scope.MissionIds)
		{
			if (state.CompletedQuestIds.Contains(mission)) continue;
			// 4. Garm's arena: everything of Q2947 after Kvasir's var 0.
			if (mission == scope.Arena.QuestId && state.Quests.TryGetValue(mission, out BotQuestState? trial) &&
				(trial.Status == Reward || trial.Status == Start && (trial.StepAndFlags & 0x3F) != 0))
				return ArenaStep(trial);
			// 5. The ring course: everything of Q2042 after Aegir's var 0, which still belongs to the return.
			if (mission == scope.RingCourse.QuestId)
				return state.Quests.TryGetValue(mission, out BotQuestState? flown) && (flown.Status == Reward || flown.Status == Start && (flown.StepAndFlags & 0x3F) != 0)
					? RingStep(flown)
					: QuestStep(ReturnPhase, mission, "starts when Q2947 is turned in");
			return QuestStep(CapitalPhase, mission, "starts when the mission before it is turned in");
		}
		// 6. The level, by quests alone.
		if (state.Level < scope.Level.Minimum)
			return Next(LevelPhase, "blocked", $"The missions are done and the Cleric is level {state.Level}, below {scope.Level.Minimum}: " +
				"it needs a few fortress quests, which are not listed yet (AX-11). No hunting and no soul healing.");

		// 7. The level-26 coin armor, by the same rule.
		if (CoinArmorStep(CoinArmor26Phase) is { } late) return late;

		// 8. The endpoint (AX-13).
		return Next(EndpointPhase, "frontier", "The missions are done and the coin armor is settled; the endpoint and its relog are next (AX-13).");

		static string Pieces(NaturalAbyssCoinSlot[] slots) => string.Join(", ", slots.Select(slot => slot.CoinItemId));

		// One tier's decision, or null when nothing of the tier beats what is worn.
		NaturalAbyssEntryDecision? CoinArmorStep(string phase)
		{
			NaturalAbyssCoinTier tier = CoinTier(scope.CoinArmor, phase);
			NaturalAbyssCoinManifest manifest = NaturalAbyssCoinArmorPolicy.Plan(scope.CoinArmor, tier,
				state.Inventory ?? throw new InvalidDataException("The coin armor needs the observed inventory."), physicalDefence);
			if (manifest.Wears.Length > 0)
				return Next(phase, "inventory-check", $"Wear the owned {tier.Name} pieces: {Pieces(manifest.Wears)}.");
			if (manifest.Buys.Length == 0) return null;
			return here != NaturalAbyssEntry.Morheim
				? Next(phase, "blocked", $"The {tier.Name} pieces {Pieces(manifest.Buys)} are sold in Morheim; the Cleric is on map {here}.")
				: Next(phase, "coin-armor", $"Buy the {tier.Name} pieces that beat what is worn: {Pieces(manifest.Buys)} for {manifest.Cost} coins.");
		}

		NaturalAbyssEntryDecision ArenaStep(BotQuestState trial)
		{
			NaturalAbyssArena arena = scope.Arena;
			int questId = arena.QuestId, var = trial.StepAndFlags & 0x3F, kills = ArenaKills(arena, trial);
			bool inside = here == arena.MapId;
			if (trial.Status == Reward)
				return QuestStep(ReturnPhase, questId, "is at its reward");
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

		NaturalAbyssEntryDecision RingStep(BotQuestState flown)
		{
			NaturalAbyssRingCourse course = scope.RingCourse;
			int questId = course.QuestId, var = flown.StepAndFlags & 0x3F;
			if (flown.Status == Reward)
				return QuestStep(RingCoursePhase, questId, "is at its reward");
			if (var >= course.StartVar && var < course.DoneVar)
				return Next(RingCoursePhase, "ring-course-fly", $"Ring {var - course.StartVar + 1} of {course.Rings.Length} is next.", quest: questId);
			if (var == course.DoneVar)
				return StepOrTravel(RingCoursePhase, questId, leg.Steps.Single(step => step.Key == course.DoneStep), "all six rings are passed: report to Yornduf");
			// Waiting for Yornduf (var 1), or a failed try (var 9: the timer, a death or a world entry).
			NaturalAbyssAttemptDecision attempt = NaturalAbyssAttempts.NextRingCourse(course, attempts ?? []);
			if (attempt.Action != "start")
				return Next(RingCoursePhase, "blocked", attempt.Reason, quest: questId);
			NaturalAltgardStep? yornduf = leg.StepsFor(questId).FirstOrDefault(step => step.ExpectedStatus == "START" && step.Var == var &&
				(step.Key == course.StartStep || step.Key == course.RestartStep));
			if (yornduf == null)
				return Next(RingCoursePhase, "blocked", $"Q{questId} is at var {var}; no ring-course step covers it.", quest: questId);
			NaturalAbyssEntryDecision reach = StepOrTravel(RingCoursePhase, questId, yornduf, attempt.Reason);
			return reach.Action == "talk" ? reach with { Action = "ring-course-start" } : reach;
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
