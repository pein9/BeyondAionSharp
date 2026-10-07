using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>A copied client observation for the Ascension bridge; no server oracle enters the decision.</summary>
public sealed record NaturalAscensionObservation(
	bool Synchronized, int? MapId, int Level, int ClassId, bool IsDead,
	IReadOnlyDictionary<int, BotQuestState> Quests, IReadOnlySet<int> CompletedQuestIds,
	long Kinah, BotBindPoint? BindPoint, IReadOnlyDictionary<int, long> ItemCounts, bool ShopVisited)
{
	public static NaturalAscensionObservation Observe(BotWorldModel world, bool shopVisited)
	{
		BotKnownObject? self = world.SelfObjectId is int id ? world.Objects.GetValueOrDefault(id) : null;
		return new(world.LoginStateObserved && world.QuestJournalObserved && world.CompletedJournalObserved,
			world.MapId, world.Level, self?.PlayerClass ?? 0, world.IsDead,
			new Dictionary<int, BotQuestState>(world.Quests), world.CompletedQuestIds.ToHashSet(), world.Kinah,
			world.ObeliskBindPoint,
			world.Inventory.Values.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count)),
			shopVisited);
	}
}

/// <summary>One next action on the bridge: a contract step (by key), a named activity, or a stop.</summary>
public sealed record NaturalAscensionDecision(
	int Sequence, string Action, string? StepKey, int? QuestId, string Outcome, string Reason, NaturalDecisionCheck[] Checks);

/// <summary>
/// NA-11: the Ascension bridge's pure decision rule over the reviewed contract (docs/natural-ascension-altgard.md).
/// It picks exactly one next action from the client's view, or stops precisely: <c>blocked</c> when the observed
/// state is outside the bridge, <c>awaiting-capability</c> when only the server can move it on, <c>complete</c> at
/// the endpoint. Static route knowledge guides it like a walkthrough; runtime state only comes from packets.
/// </summary>
public static class NaturalAscensionDecisionEngine
{
	public const byte Start = 3, Reward = 4, Complete = 5;
	private const int Ishalgen = 220010000, Ataxiar = 320020000, Pandaemonium = 120010000, Altgard = 220030000;
	private const int Lyfjaberga = 204083, KarmicStaff = 101500498;

	/// <summary>
	/// NA-17/25: true once a login lands past the Munin stop, so the bridge runner resumes: Q2008 completed, past its
	/// start (START with a var set, or REWARD), or the character on a bridge map. Q2008 shown LOCKED (6) or NONE is
	/// not a start: Java lists it LOCKED in the journal from about level 8, mid-Ishalgen (the NA-25 full run).
	/// </summary>
	public static bool BridgeStarted(IReadOnlySet<int> completedQuestIds, IReadOnlyDictionary<int, BotQuestState> quests, int? mapId)
	{
		if (completedQuestIds.Contains(2008)) return true;
		if (quests.TryGetValue(2008, out BotQuestState? ascension) &&
			(ascension.Status == Reward || ascension.Status == Start && (ascension.StepAndFlags & 0x00FFFFFF) != 0))
			return true;
		return mapId is Ataxiar or Pandaemonium or Altgard;
	}

	public static NaturalAscensionDecision Decide(NaturalAscensionContract contract, NaturalAscensionObservation state, int sequence,
		bool ceremonyOnly = false)
	{
		var checks = new List<NaturalDecisionCheck>();
		NaturalAscensionDecision Plan(string action, string? stepKey, int? questId, string reason) =>
			new(sequence, action, stepKey, questId, "planned", reason, [.. checks]);
		NaturalAscensionDecision Stop(string action, string outcome, string reason, int? questId = null) =>
			new(sequence, action, null, questId, outcome, reason, [.. checks]);
		NaturalAscensionDecision Step(string key, string reason)
		{
			NaturalAscensionStep step = contract.Steps.Single(candidate => candidate.Key == key);
			if (state.MapId != step.MapId)
			{
				checks.Add(new("map", "fail", $"{key} is on map {step.MapId}; observed {state.MapId}."));
				return Stop("wrong-map", "blocked", $"{key} needs map {step.MapId}, observed {state.MapId}.", step.QuestId);
			}
			checks.Add(new("map", "pass", $"{key} on map {step.MapId}."));
			return Plan("talk", key, step.QuestId, reason);
		}

		// CP-26: the class pair, the four class-dependent steps and the dispatch quest are the contract's, so a class line's
		// bridge is decided by the same rule. The reviewed bridge gives the Cleric's steps and reasons as they always read.
		string second = string.Join(' ', contract.ClassChoice.ToClass.Split('_').Select(word => word[..1] + word[1..].ToLowerInvariant()));
		int dispatch = contract.Dispatch.QuestId;

		if (!state.Synchronized || state.MapId == null)
			return Stop("refresh-observation", "planned", "Wait for a synchronized client view.");
		if (state.IsDead)
			return Plan("revive-at-bind", null, null, "Dead: revive at the bound obelisk (a death in Ataxiar resets Q2008 to var 4).");

		NaturalJourneyStage stage;
		try
		{
			stage = NaturalJourneyIdentityRules.Classify(contract.StarterClass, contract.SecondClass, state.ClassId, state.Level, state.MapId);
			bool ascended = state.CompletedQuestIds.Contains(contract.ClassChoice.QuestId);
			NaturalJourneyIdentityRules.RequireJournal(stage, ascended,
				state.Quests.TryGetValue(contract.ClassChoice.QuestId, out BotQuestState? ascension) ? ascension.Status : null, state.MapId);
		}
		catch (InvalidDataException outside)
		{
			checks.Add(new("identity", "fail", outside.Message));
			return Stop("identity", "blocked", outside.Message);
		}
		checks.Add(new("identity", "pass", $"{stage} on map {state.MapId}, level {state.Level}."));

		// Q2008 Ascension.
		if (!Done(2008))
		{
			if (!state.Quests.TryGetValue(2008, out BotQuestState? q2008))
				return Stop("wait-journal", "awaiting-capability", "Q2008 is auto-started at level 9; it is not in the journal.", 2008);
			int v = Var(q2008);
			if (q2008.Status == Reward)
				return Step("q2008-reward-munin", "Class chosen: leave Ataxiar with NOREWARD.");
			return v switch
			{
				0 => Step("q2008-v0-munin", "Munin opens Ascension."),
				1 => Step("q2008-v1-urd", "Urd's card of the past."),
				2 => Step("q2008-v2-verdandi", "Verdandi's card of the present."),
				3 => Step("q2008-v3-skuld", "Skuld's card of the future."),
				4 => Step("q2008-v4-munin", "Munin opens the Ataxiar instance."),
				99 => Step("q2008-v99-hagen", "Hagen's flight to the trial."),
				50 when state.MapId == Ataxiar => Plan("wait-flight", null, 2008, "On Hagen's flight; land when it ends."),
				>= 51 and <= 54 when state.MapId == Ataxiar =>
					Plan("fight-trial", null, 2008, $"Guardian assassins: {54 - v + 1} left (scripted trial, 1 damage)."),
				5 when state.MapId == Ataxiar => Plan("fight-trial", null, 2008, "Brigade General Hellion (scripted trial)."),
				6 => Step(contract.Step(NaturalAscensionStepRole.ClassChoice).Key, $"Choose {second} ({contract.ClassChoice.Action})."),
				_ => Stop("wrong-map", "blocked", $"Q2008 var {v} on map {state.MapId} is not a bridge state.", 2008),
			};
		}

		// Q2009 A Ceremony in Pandaemonium.
		if (!Done(2009))
		{
			if (!state.Quests.TryGetValue(2009, out BotQuestState? q2009))
				return Stop("wait-journal", "awaiting-capability", "Q2009 starts when Q2008 completes; not in the journal yet.", 2009);
			if (q2009.Status == Reward)
			{
				NaturalAscensionStep ceremony = contract.Step(NaturalAscensionStepRole.Ceremony);
				string pick = contract.CeremonyReward.Action.Replace("SELECTED_QUEST_", "", StringComparison.Ordinal);
				return Step(ceremony.Key, ceremony.NpcId == Lyfjaberga && contract.CeremonyReward.ItemId == KarmicStaff
					? $"Lyfjaberga: the Karmic Staff ({pick})."
					: $"Preceptor {ceremony.NpcId}: ceremony item {contract.CeremonyReward.ItemId} ({pick}).");
			}
			return Var(q2009) switch
			{
				0 => Step("q2009-v0-munin", $"Munin sends the {second} to Pandaemonium."),
				1 => Step("q2009-v1-heimdall", "Heimdall's ceremony."),
				2 => Step("q2009-v2-balder", "Balder's ceremony."),
				int other => Stop("unexpected-var", "blocked", $"Q2009 var {other} is not a bridge state.", 2009),
			};
		}

		if (ceremonyOnly)
			return state.Level >= contract.Endpoint.MinimumLevel
				? Stop("ceremony-complete", "complete", "Q2008/Q2009 complete: return to Ishalgen before the Altgard dispatch.")
				: Stop("level", "blocked", "The ceremony did not unlock level 10.", 2009);

		// The dispatch quest to Altgard (Q2904 for the Priest-born classes), travel and the bind.
		if (!Done(dispatch))
		{
			if (!state.Quests.TryGetValue(dispatch, out BotQuestState? q2904))
				return state.Level < contract.Endpoint.MinimumLevel
					? Stop("level", "blocked", $"Q{dispatch} needs level {contract.Endpoint.MinimumLevel}; observed {state.Level}.", dispatch)
					: Stop("wait-journal", "awaiting-capability", $"Q{dispatch} starts when Q2009 completes; not in the journal yet.", dispatch);
			if (Var(q2904) == 0) return Step(contract.Step(NaturalAscensionStepRole.DispatchStart).Key, "Doman takes the dispatch.");
			if (state.MapId == Pandaemonium)
			{
				if (state.Kinah < contract.Teleporter.BasePrice)
					return Stop("not-enough-kinah", "blocked", $"Doman's fare is at least {contract.Teleporter.BasePrice}; {state.Kinah} Kinah.", dispatch);
				return Plan("teleport", null, dispatch, $"Doman's teleporter to Altgard (location {contract.Teleporter.LocationId}).");
			}
			if (state.MapId == Altgard && !BoundHere())
				return BindOrStop();
			if (q2904.Status == Reward || Var(q2904) == 1) return Step(contract.Step(NaturalAscensionStepRole.DispatchReward).Key, "Report to Meiyer.");
		}

		// Q24010 Suthran's Orders: starts on entering Altgard.
		if (!Done(24010))
		{
			if (state.MapId == Altgard && !BoundHere()) return BindOrStop();
			if (!state.Quests.ContainsKey(24010))
				return Stop("wait-journal", "awaiting-capability", "Q24010 starts on entering Altgard; not in the journal yet.", 24010);
			return Step("q24010-reward-suthran", "Suthran's Orders.");
		}

		if (state.MapId != Altgard)
			return Stop("wrong-map", "blocked", $"Every bridge quest is done, but the {second} is on map {state.MapId}, not Altgard.");
		if (!BoundHere()) return BindOrStop();
		if (!state.ShopVisited)
			return Plan("shop", null, null, "Altgard shop stop: equip, sell, buy potions and powder (no gear).");
		checks.Add(new("endpoint", "pass", $"Four quests, {second}, bound in Altgard, shop stop done."));
		return Stop("bridge-complete", "complete", "The Ascension bridge endpoint is reached.");

		bool Done(int quest) => state.CompletedQuestIds.Contains(quest);
		static int Var(BotQuestState quest) => quest.StepAndFlags & 0x00FFFFFF;
		bool BoundHere() => state.BindPoint is { } bound && bound.MapId == contract.Bind.MapId &&
			MathF.Abs(bound.Position.X - contract.Bind.Position[0]) < NaturalServicePolicy.SameObeliskRadius &&
			MathF.Abs(bound.Position.Y - contract.Bind.Position[1]) < NaturalServicePolicy.SameObeliskRadius;
		NaturalAscensionDecision BindOrStop() => state.Kinah < contract.Bind.Price
			? Stop("not-enough-kinah", "blocked", $"The Altgard Fortress bind costs {contract.Bind.Price}; {state.Kinah} Kinah.")
			: Plan("bind", null, null, "Bind at the Altgard Fortress obelisk before anything else in Altgard.");
	}
}
