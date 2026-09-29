using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>A copied client observation for Altgard Leg 1; no server oracle enters the decision.</summary>
public sealed record NaturalAltgardObservation(bool Synchronized, int? MapId, int Level, bool IsDead,
	IReadOnlyDictionary<int, BotQuestState> Quests, IReadOnlySet<int> CompletedQuestIds, BotPosition Position,
	IReadOnlyDictionary<int, long> ItemCounts)
{
	public static NaturalAltgardObservation Observe(BotWorldModel world, BotPosition position) =>
		new(world.LoginStateObserved && world.QuestJournalObserved && world.CompletedJournalObserved, world.MapId, world.Level,
			world.IsDead, new Dictionary<int, BotQuestState>(world.Quests), world.CompletedQuestIds.ToHashSet(), position,
			world.Inventory.Values.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count)));
}

/// <summary>What a template quest's objectives need, from its compiled plan: items in the inventory, or a kill counter.
/// Java's monster_hunt keeps the quest at START with the counter full until it is turned in.</summary>
public sealed record NaturalTemplateObjective(int QuestId, int? ItemId, int ItemCount, int KillVar, int KillCount)
{
	public static IReadOnlyDictionary<int, NaturalTemplateObjective> From(IReadOnlyDictionary<int, QuestRunPlan> plans) =>
		plans.Values.ToDictionary(plan => plan.Id, plan =>
		{
			QuestRunStep? collect = plan.Steps.FirstOrDefault(step => step.Kind == "collect");
			QuestRunStep? kill = plan.Steps.FirstOrDefault(step => step.Kind == "kill");
			return new NaturalTemplateObjective(plan.Id, collect?.ItemId, collect?.Count ?? 0,
				kill?.Data.GetProperty("var").GetInt32() ?? 0, kill?.Count ?? 0);
		});

	public bool IsDone(BotQuestState? quest, IReadOnlyDictionary<int, long> items) =>
		ItemId is int item ? items.GetValueOrDefault(item) >= ItemCount
			: quest is { } state && (state.Status >= 4 || ((state.StepAndFlags >> (KillVar * 6)) & 0x3F) >= KillCount);
}

/// <summary>One next action in Leg 1: a template phase, a contract step (by key), the remedy, the air kills, a hunt for
/// level, the way back to the hub, or a stop.</summary>
public sealed record NaturalAltgardDecision(int Sequence, string Action, string? StepKey, int? QuestId, string Outcome, string Reason,
	NaturalDecisionCheck[] Checks);

/// <summary>
/// AF-08 (docs/natural-altgard-leveling.md): Leg 1's pure decision rule over the contract. Template quests are worked
/// hub-style: every eligible one is accepted at the fortress, then their objectives are worked on the Ice Lake, then
/// each is claimed. The scripted quests follow their contract steps. A quest gated by level or by an unfinished
/// prerequisite is left for later; when only gated quests remain, the Cleric hunts for the level. Outcomes as in the
/// bridge: <c>planned</c>, <c>blocked</c>, <c>awaiting-capability</c> and <c>complete</c>.
/// </summary>
public static class NaturalAltgardDecisionEngine
{
	public const byte Start = 3, Reward = 4, Locked = 6;

	public static NaturalAltgardDecision Decide(NaturalAltgardContract contract, NaturalAltgardObservation state,
		IReadOnlyDictionary<int, NaturalTemplateObjective> objectives, int sequence)
	{
		var checks = new List<NaturalDecisionCheck>();
		NaturalAltgardDecision Plan(string action, int? questId, string reason, string? stepKey = null) =>
			new(sequence, action, stepKey, questId, "planned", reason, [.. checks]);
		NaturalAltgardDecision Stop(string action, string outcome, string reason, int? questId = null) =>
			new(sequence, action, null, questId, outcome, reason, [.. checks]);

		if (!state.Synchronized || state.MapId == null)
			return Stop("refresh-observation", "planned", "Wait for a synchronized client view.");
		if (state.IsDead)
			return Plan("revive-at-bind", null, "Dead: revive at the Altgard Fortress obelisk.");
		if (state.MapId != contract.Hub.MapId)
			return Stop("wrong-map", "blocked", $"Leg 1 is on map {contract.Hub.MapId}; the Cleric is on {state.MapId}.");

		NaturalAltgardQuest[] open = contract.Order.Select(contract.Quest).Where(quest => !Done(quest.Id)).ToArray();
		NaturalAltgardQuest[] eligible = open.Where(Eligible).ToArray();

		// Template quests, hub-style: accept, work, claim.
		foreach (NaturalAltgardQuest quest in eligible.Where(quest => quest.IsTemplate && Status(quest.Id) is not (Start or Reward)))
			return Plan("template-accept", quest.Id, $"Q{quest.Id}: accept at the fortress with the other hub quests.");
		foreach (NaturalAltgardQuest quest in eligible.Where(quest => quest.IsTemplate && Status(quest.Id) == Start && !WorkDone(quest.Id)))
			return Plan("template-work", quest.Id, $"Q{quest.Id}: work the objectives on the Ice Lake.");
		foreach (NaturalAltgardQuest quest in eligible.Where(quest => quest.IsTemplate))
			return Plan("template-claim", quest.Id, $"Q{quest.Id}: objectives done; claim at the fortress.");

		// Scripted quests, in the contract's order.
		foreach (NaturalAltgardQuest quest in eligible)
		{
			byte? status = Status(quest.Id);
			if (quest.Category == "MISSION" && status is null or Locked)
			{
				checks.Add(new("campaign", "wait", $"Q{quest.Id} is not in the journal as started yet."));
				continue;
			}
			if (status is null || status is not (Start or Reward))
			{
				NaturalAltgardStep? offer = contract.StepsFor(quest.Id).SingleOrDefault(step => step.ExpectedStatus == "OFFER");
				if (offer == null) return Stop("no-offer", "blocked", $"Q{quest.Id} has no offer step.", quest.Id);
				return Plan("talk", quest.Id, $"Q{quest.Id}: take it from its giver.", offer.Key);
			}
			if (status == Reward)
			{
				NaturalAltgardStep? claim = contract.StepsFor(quest.Id).SingleOrDefault(step => step.ExpectedStatus == "REWARD");
				return claim == null
					? Stop("reward-without-step", "blocked", $"Q{quest.Id} is at REWARD, but its reward is taken in the var step's dialog.", quest.Id)
					: Plan("talk", quest.Id, $"Q{quest.Id}: claim the reward.", claim.Key);
			}
			int var = Var(quest.Id);
			if (quest.Id == contract.ItemUse.QuestId && var == contract.ItemUse.Var)
				return Plan("use-item", quest.Id, $"Q{quest.Id}: use item {contract.ItemUse.ItemId} (anywhere).");
			if (quest.Id == contract.AirKills.QuestId && var >= contract.AirKills.FromVar && var <= contract.AirKills.RewardVar)
				return Plan("air-kills", quest.Id, $"Q{quest.Id}: shoot the Abyss Fungus down (var {var} of {contract.AirKills.RewardVar}).");
			NaturalAltgardStep? next = contract.StepsFor(quest.Id).SingleOrDefault(step => step.ExpectedStatus == "START" && step.Var == var);
			return next == null
				? Stop("unexpected-var", "blocked", $"Q{quest.Id} var {var} has no contract step.", quest.Id)
				: Plan("talk", quest.Id, $"Q{quest.Id} var {var}: {next.Key}.", next.Key);
		}

		// Only gated quests are left: hunt for the level they need, or wait for the campaign to unlock.
		if (open.Length > 0)
		{
			int needed = open.Where(quest => state.Level < quest.MinimumLevel).Select(quest => quest.MinimumLevel).DefaultIfEmpty(0).Min();
			if (needed > state.Level)
				return Plan("hunt-for-level", null, $"Every open quest needs level {needed} or a prerequisite; hunt on the Ice Lake.");
			return Stop("gated", "awaiting-capability", $"Open quests {string.Join(", ", open.Select(quest => quest.Id))} are not available yet.");
		}

		// The endpoint: every Leg 1 quest done, back in the fortress, alive.
		float hub = MathF.Sqrt(MathF.Pow(state.Position.X - contract.Hub.Anchor[0], 2) + MathF.Pow(state.Position.Y - contract.Hub.Anchor[1], 2));
		if (MathF.Abs(state.Position.Z - contract.Hub.Anchor[2]) > 15 || hub > contract.Hub.Radius)
			return Plan("return-to-hub", null, "Every Leg 1 quest is done; walk back into the fortress.");
		checks.Add(new("endpoint", "pass", $"All {contract.Endpoint.CompletedQuestIds.Length} quests done, in the fortress, alive, level {state.Level}."));
		return Stop("leg-complete", "complete", "The Altgard Leg 1 endpoint is reached.");

		bool Done(int questId) => state.CompletedQuestIds.Contains(questId);
		byte? Status(int questId) => state.Quests.TryGetValue(questId, out BotQuestState? quest) ? quest.Status : null;
		int Var(int questId) => state.Quests.TryGetValue(questId, out BotQuestState? quest) ? quest.StepAndFlags & 0x3F : 0;
		bool Eligible(NaturalAltgardQuest quest)
		{
			if (state.Level < quest.MinimumLevel)
			{
				checks.Add(new("level", "wait", $"Q{quest.Id} needs level {quest.MinimumLevel}; come back at it."));
				return false;
			}
			if (quest.Prerequisite is int before && !Done(before))
			{
				checks.Add(new("prerequisite", "wait", $"Q{quest.Id} needs Q{before} first."));
				return false;
			}
			return true;
		}
		bool WorkDone(int questId) => objectives.TryGetValue(questId, out NaturalTemplateObjective? objective) &&
			objective.IsDone(state.Quests.GetValueOrDefault(questId), state.ItemCounts);
	}
}
