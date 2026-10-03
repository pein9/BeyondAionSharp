using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>HM-01: two ordinary fresh visits; every accept, objective and reward is selected
/// from the new client observation. A saved receipt can require a wait, never a quest replay.</summary>
public static class NaturalHaramelDecisionEngine
{
	/// <summary>Java starts Q2945 automatically at level 25; its deferred objective must remain untouched.</summary>
	public static bool PreservesDeferredQuest(int id, (byte Status, int StepAndFlags) incoming, BotQuestState current, int level) =>
		(current.Status, current.StepAndFlags) == incoming ||
		(id == 2945 && incoming == (6, 0) && level >= 25 && current is { Status: 3, StepAndFlags: 0, CompleteCount: 0 });

	public static NaturalAltgardDecision Decide(NaturalAltgardContract leg, NaturalAltgardObservation state,
		IReadOnlyDictionary<int, NaturalTemplateObjective> objectives, int sequence)
	{
		NaturalHaramel rules = leg.Haramel!;
		NaturalHaramelProgress? progress = state.HaramelProgress;
		NaturalAltgardDecision Stop(string action, string reason, string outcome = "blocked") =>
			new(sequence, action, null, null, outcome, reason, []);
		NaturalAltgardDecision Plan(string action, int map, int? id = null, string? key = null, string? reason = null) =>
			new(sequence, action, key, id, "planned", reason ?? $"Haramel: {action}" , [], map);
		bool Done(int id) => state.CompletedQuestIds.Contains(id);
		byte? Status(int id) => state.Quests.GetValueOrDefault(id)?.Status;
		int Var(int id) => state.Quests.TryGetValue(id, out BotQuestState? q) ? leg.QuestVar(q) : -1;
		bool Started(int id) => Status(id) is 3 or 4;
		bool WorkDone(int id) => Done(id) || Status(id) == 4 || objectives.GetValueOrDefault(id)?.IsDone(state.Quests.GetValueOrDefault(id), state.ItemCounts) == true;

		if (!state.Synchronized || state.MapId == null) return Stop("refresh-observation", "Wait for a synchronized client view.", "planned");
		if (state.MapId != rules.MapId && state.MapId != leg.Hub.MapId) return Stop("wrong-map", "Haramel permits only Altgard and its ordinary solo instance.");
		if (leg.Start.CompletedQuestIds.Any(id => !Done(id))) return Stop("lost-journal", "Preserve all 145 incoming completed journals.");
		if (state.CompletedQuestCounts?.GetValueOrDefault(2293) != 1 || leg.Order.Any(id => Done(id) && state.CompletedQuestCounts?.GetValueOrDefault(id) != 1))
			return Stop("quest-repeat-count", "Reconcile first completions; never take another natural coin or Haramel repeat.");
		if (state.ItemCounts.GetValueOrDefault(188053787) != 1 || state.ItemCounts.GetValueOrDefault(140000001) != 0 ||
			state.ItemCounts.GetValueOrDefault(140000098) != 0 || state.SkillIds?.Contains(11504) == true)
			return Stop("stigma-ledger", "Retain the sealed Q2900 bundle without its tutorial stone/skill or legacy reward.");
		if (state.Inventory?.Any(i => i.ObjectId == rules.StaffObjectId && i.ItemId == rules.StaffItemId && i.EquipmentSlot == 3) != true ||
			rules.ProtectedItemIds.Where(id => id is not (186000006 or 186000007)).Any(id => state.ItemCounts.GetValueOrDefault(id) < 1))
			return Stop("retained-gear", "Retain the equipped original staff, purchased armour and old cloth gloves.");
		if (progress == null || state.NowMillis == null) return Stop("haramel-checkpoint", "Initialize or reconcile the explicit Haramel receipt before acting.");
		if (state.NowMillis < progress.LastObservedAtMillis) return Stop("haramel-clock", "Recovery cannot reset the original clock.");
		if (state.IsDead) return progress.Revives >= 20 ? Stop("recovery-budget", "The original twenty-revive journey budget is exhausted.")
			: Plan(state.MapId == rules.MapId && state.CanRebirth ? "revive-in-place" : "revive-at-bind", state.MapId.Value);
		if (state.MapId == rules.MapId && (progress.NeedsInstanceObservation || progress.CurrentVisit == null || progress.CurrentVisit.LeftAtMillis != null))
			return Plan("observe-haramel-entry", rules.MapId, reason: "Reconcile Moorilerk's actual object ID and the self entry-count row after entry/reconnect.");
		if (state.MapId == rules.MapId && (state.InstanceId == null || state.InstanceId != progress.CurrentVisit?.InstanceId))
			return Plan("observe-haramel-entry", rules.MapId, reason: "Reconcile the actual SM_PLAYER_SPAWN instance ID before using this visit's receipts.");
		if (Status(28511) == 3 && state.ItemCounts.GetValueOrDefault(182212023) > 0)
			return Stop("refresh-observation", "Soup is already owned; wait for its reward journal transition instead of giving it twice.", "planned");

		bool allDone = leg.Order.All(Done);
		if (!allDone && !NaturalAltgardDecisionEngine.BoundAt(new(rules.WorkingBindNpcId, rules.WorkingBindPosition, false, 0, 5), leg.Hub.MapId, state.Bind))
			return Stop("working-bind", "Both clears and ordinary recovery retain the Heart bind.");
		if (allDone)
		{
			if (state.CompletedQuestIds.Count != 156 || state.ItemCounts.GetValueOrDefault(rules.IronItemId) != rules.IronCount || state.ItemCounts.GetValueOrDefault(rules.BronzeItemId) != rules.BronzeCount ||
				rules.CleanupItemIds.Any(id => state.ItemCounts.GetValueOrDefault(id) != 0))
				return Stop("haramel-ledger", "Reconcile 19 Iron, seven Bronze and Java quest-item cleanup.");
			if (state.MapId == rules.MapId) return Plan("leave-haramel", rules.MapId);
			if (!NaturalAltgardDecisionEngine.BoundAt(leg.Bind!, leg.Hub.MapId, state.Bind)) return Plan("bind", leg.Hub.MapId);
			if (MathF.Sqrt(MathF.Pow(state.Position.X - leg.Hub.Anchor[0], 2) + MathF.Pow(state.Position.Y - leg.Hub.Anchor[1], 2)) > leg.Hub.Radius)
				return Plan("return-to-hub", leg.Hub.MapId);
			return Stop("leg-complete", "Eleven first completions, 156 retained journals, staff/currency/stigma ledger and fortress bind are observed.", "complete");
		}

		NaturalAltgardDecision? Tasks(IEnumerable<NaturalHaramelTask> tasks)
		{
			foreach (NaturalHaramelTask task in tasks)
			{
				if (task.QuestId is int id && Done(id)) continue;
				bool needed = task.Action switch
				{
					"template-accept" => !Started(task.QuestId!.Value),
					"template-work" => Started(task.QuestId!.Value) && !WorkDone(task.QuestId.Value),
					"template-claim" => Started(task.QuestId!.Value) && WorkDone(task.QuestId.Value),
					"talk" or "haramel-object" or "haramel-soup" or "haramel-movie" => Matches(leg.Steps.Single(s => s.Key == task.StepKey)),
					"haramel-carts" => Status(28510) == 3 && Var(28510) < 3,
					"haramel-ginseng" => Status(28511) == 3 && progress.SoupPayment == null && state.ItemCounts.GetValueOrDefault(182212023) == 0 && state.ItemCounts.GetValueOrDefault(182212022) < 5,
					"haramel-boss" => progress.CurrentVisit?.BossMovieObserved != true,
					"haramel-loot" => progress.CurrentVisit?.ChestResolved != true,
					"leave-haramel" => state.MapId == rules.MapId,
					_ => false,
				};
				if (!needed) continue;
				if (task.QuestId is int gated && !leg.Quest(gated).PrerequisiteList.All(Done))
					return Stop("quest-gate", $"Q{gated} awaits its observed prerequisite completion.");
				if (state.MapId != task.MapId)
				{
					if (task.MapId == rules.MapId && progress.CurrentVisit?.LeftAtMillis != null &&
						progress.FreshEntryAfterMillis is long retryAfter && state.NowMillis < retryAfter)
						return Plan("wait-haramel-expiry", leg.Hub.MapId, reason: $"Preserve remaining counters/items and wait for ordinary cleanup deadline {retryAfter}.");
					return Plan(task.MapId == rules.MapId ? "enter-haramel" : "leave-haramel", rules.MapId, task.QuestId, task.StepKey);
				}
				return Plan(task.Action, task.MapId, task.QuestId, task.StepKey);
			}
			return null;
		}
		bool Matches(NaturalAltgardStep step) => step.ExpectedStatus switch
		{
			"OFFER" => !Started(step.QuestId),
			"REWARD" => Status(step.QuestId) == 4,
			_ => Status(step.QuestId) == 3 && (step.Var == null || Var(step.QuestId) == step.Var),
		};

		// A completed Q28507 is the outside follow-up gate. A lost first copy is entered normally;
		// its remaining quests still use the actual persisted counters/items.
		if (!Done(28507)) return Tasks(rules.FirstVisit) ?? Stop("first-visit-observation", "Reconcile first-clear quest and boss receipts.");
		if (Tasks(rules.BetweenVisits) is { } outside) return outside;
		bool freshSecond = progress.Visits.Any(v => v.PostBossQuests && v.FreshSpawnsObserved);
		if (!freshSecond)
		{
			if (state.MapId == rules.MapId) return Plan("leave-haramel", rules.MapId, reason: "The observed copy is still the first clear; leave it normally.");
			if (progress.FreshEntryAfterMillis is not long deadline) return Stop("expiry-receipt", "Record the ordinary exit and retained cleanup deadline.");
			if (state.NowMillis < deadline) return Plan("wait-haramel-expiry", leg.Hub.MapId, reason: $"Advance normally until retained empty-instance cleanup deadline {deadline}.");
			return Plan("enter-haramel", rules.MapId, reason: "Observe a new Moorilerk object ID and actual entry count; never force an instance reset.");
		}
		if (state.MapId == rules.MapId && (state.InstanceAnchorObjectId is int anchor && anchor != progress.CurrentVisit?.AnchorObjectId ||
			state.InstanceId is int observedInstance && observedInstance != progress.CurrentVisit?.InstanceId))
			return Stop("instance-observation", "Reconcile the actual copy anchor before using saved boss receipts.", "planned");
		if (Tasks(rules.SecondVisit) is { } second) return second;
		return Tasks(rules.Finish) ?? Stop("remaining-objective", "Record missing sources and take another ordinary recovery visit without resetting budgets.", "planned");
	}
}
