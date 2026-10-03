using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>A copied client observation for Altgard Leg 1; no server oracle enters the decision.</summary>
/// <param name="Bind">AB-08: the obelisk bind point the client last saw (<c>SM_BIND_POINT_INFO</c>).</param>
/// <param name="GameMinutes">AK-08: the client's game clock now (<c>SM_GAME_TIME</c> run on; <see cref="NaturalGameClock"/>).</param>
/// <param name="VisibleNpcIds">AK-08: the template ids of the NPCs in the client's view.</param>
/// <param name="FreeCubeSlots">AK-08: the cube's free slots as the inventory policy counts them (null when not counted).</param>
/// <param name="Kinah">AK-Q4: the kinah the client holds.</param>
/// <param name="CubeNpcExpansions">AK-Q4: the cube's NPC expansion level from SM_CUBE_UPDATE (null before one is seen).</param>
public sealed record NaturalAltgardObservation(bool Synchronized, int? MapId, int Level, bool IsDead,
	IReadOnlyDictionary<int, BotQuestState> Quests, IReadOnlySet<int> CompletedQuestIds, BotPosition Position,
	IReadOnlyDictionary<int, long> ItemCounts, BotBindPoint? Bind = null, long? GameMinutes = null, IReadOnlySet<int>? VisibleNpcIds = null,
	int? FreeCubeSlots = null, long Kinah = 0, int? CubeNpcExpansions = null, bool CanRebirth = false,
	IReadOnlySet<int>? SkillIds = null)
{
	public static NaturalAltgardObservation Observe(BotWorldModel world, BotPosition position, DateTimeOffset? now = null, int? freeCubeSlots = null) =>
		new(world.LoginStateObserved && world.QuestJournalObserved && world.CompletedJournalObserved, world.MapId, world.Level,
			world.IsDead, new Dictionary<int, BotQuestState>(world.Quests), world.CompletedQuestIds.ToHashSet(), position,
			world.Inventory.Values.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count)),
			world.ObeliskBindPoint, now is DateTimeOffset at ? world.GameMinutesAt(at) : world.GameMinutes,
			world.Objects.Values.Where(known => known.Kind == BotKnownObjectKind.Npc && known.TemplateId != null)
				.Select(known => known.TemplateId!.Value).ToHashSet(), freeCubeSlots, world.Kinah, world.CubeExpansion?.Npc,
			world.ReviveOptions?.BySkill == true, world.Skills.Keys.ToHashSet());
}

/// <summary>One independent kill counter in a template quest's compiled plan.</summary>
public sealed record NaturalTemplateKill(int Var, int Count);

/// <summary>What a template quest needs: inventory items or every independent kill counter. Java keeps monster_hunt
/// at START with the counters full until it is turned in.</summary>
public sealed record NaturalTemplateObjective(int QuestId, int? ItemId, int ItemCount, int KillVar, int KillCount, int? ClaimMapId = null,
	NaturalTemplateKill[]? Kills = null)
{
	public static IReadOnlyDictionary<int, NaturalTemplateObjective> From(IReadOnlyDictionary<int, QuestRunPlan> plans) =>
		plans.Values.ToDictionary(plan => plan.Id, plan =>
		{
			QuestRunStep? collect = plan.Steps.FirstOrDefault(step => step.Kind == "collect");
			QuestRunStep? kill = plan.Steps.FirstOrDefault(step => step.Kind == "kill");
			return new NaturalTemplateObjective(plan.Id, collect?.ItemId, collect?.Count ?? 0,
				kill?.Data.GetProperty("var").GetInt32() ?? 0, kill?.Count ?? 0,
				plan.EndNpcs.SelectMany(npc => npc.Positions).FirstOrDefault(position => !position.ConditionalEvent)?.MapId,
				plan.Steps.Where(step => step.Kind == "kill").Select(step =>
					new NaturalTemplateKill(step.Data.GetProperty("var").GetInt32(), step.Count)).ToArray());
		});

	public bool IsDone(BotQuestState? quest, IReadOnlyDictionary<int, long> items) =>
		ItemId is int item ? items.GetValueOrDefault(item) >= ItemCount
			: quest is { } state && (state.Status >= 4 ||
				(Kills ?? [new(KillVar, KillCount)]).All(kill => ((state.StepAndFlags >> (kill.Var * 6)) & 0x3F) >= kill.Count));
}

/// <summary>One next action in Leg 1: a template phase, a contract step (by key), the remedy, the air kills, a hunt for
/// level, the way back to the hub, or a stop.</summary>
public sealed record NaturalAltgardDecision(int Sequence, string Action, string? StepKey, int? QuestId, string Outcome, string Reason,
	NaturalDecisionCheck[] Checks, int? MapId = null);

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
	/// <summary>How far the client's bind point may lie from the contract's obelisk and still be that bind.</summary>
	public const float BindTolerance = 10;

	/// <summary>AK-08: game minutes to walk <paramref name="metres"/> (about 6 m/s, a game minute per 5 s), with a margin for the
	/// road's bends and a fight on the way.</summary>
	public static int TravelGameMinutes(float metres) => (int)MathF.Ceiling(metres / 6f / 5f) + 5;

	/// <summary>AG-07: game minutes to walk to an escort's follower from <paramref name="from"/> and bring him to the goal: the
	/// walk there, then the escort line at half walking speed (the hops and the waits for the follower).</summary>
	public static int EscortGameMinutes(NaturalAltgardContract contract, NaturalAltgardEscort escort, BotPosition from)
	{
		float[] follower = contract.Steps.Single(step => step.Key == escort.RestartStep).Position;
		float there = MathF.Sqrt(MathF.Pow(from.X - follower[0], 2) + MathF.Pow(from.Y - follower[1], 2));
		float line = MathF.Sqrt(MathF.Pow(escort.Goal[0] - follower[0], 2) + MathF.Pow(escort.Goal[1] - follower[1], 2));
		return TravelGameMinutes(there) + TravelGameMinutes(2 * line);
	}

	public static bool BoundAt(NaturalAltgardBind bind, int mapId, BotBindPoint? bound) =>
		bound is { } point && point.MapId == mapId &&
		MathF.Sqrt(MathF.Pow(point.Position.X - bind.Position[0], 2) + MathF.Pow(point.Position.Y - bind.Position[1], 2)) <= BindTolerance;

	/// <param name="only">A diagnostic run limited to these quests (AM-06): the rest of the leg is left alone, and the run is
	/// complete once these are done, wherever the bot stands.</param>
	public static NaturalAltgardDecision Decide(NaturalAltgardContract contract, NaturalAltgardObservation state,
		IReadOnlyDictionary<int, NaturalTemplateObjective> objectives, int sequence, IReadOnlySet<int>? only = null)
	{
		var checks = new List<NaturalDecisionCheck>();
		NaturalAltgardDecision Plan(string action, int? questId, string reason, string? stepKey = null)
		{
			int map = action == "talk" && stepKey != null ? contract.StepMap(contract.Steps.Single(step => step.Key == stepKey))
				: action is "equip-stigma" or "destiny-fight" ? contract.Destiny!.MapId
				: action == "template-claim" && questId is int id ? objectives.GetValueOrDefault(id)?.ClaimMapId ?? contract.Hub.MapId
				: action is "enter-instance" or "leave-instance" ? contract.InstanceTripList.Single(trip => trip.QuestId == questId).MapId
				: action == "revive-in-place" ? state.MapId!.Value
				: action == "use-object" ? contract.ObjectUseList.Single(use => use.Key == stepKey).MapId ?? contract.Hub.MapId
				: action == "hunt" ? contract.HuntList.First(hunt => hunt.QuestId == questId &&
					Var(questId!.Value) >= hunt.FromVar && Var(questId.Value) < hunt.ToVar).MapId ?? contract.Hub.MapId
				: contract.Hub.MapId;
			return new(sequence, state.MapId != map && action is not ("revive-at-bind" or "revive-in-place" or "enter-instance" or "recover-destiny") ? "travel-to-map" : action,
				stepKey, questId, "planned", reason, [.. checks], map);
		}
		NaturalAltgardDecision Stop(string action, string outcome, string reason, int? questId = null) =>
			new(sequence, action, null, questId, outcome, reason, [.. checks]);

		if (!state.Synchronized || state.MapId == null)
			return Stop("refresh-observation", "planned", "Wait for a synchronized client view.");
		if (state.IsDead)
			return contract.Destiny == null && state.CanRebirth && contract.InstanceTripList.Any(trip => trip.MapId == state.MapId)
				? Plan("revive-in-place", null, "Dead in the quest instance: accept the client's learned self-revival option.")
				: Plan("revive-at-bind", null, "Dead: revive at the working hub's obelisk.");
		bool offHub = state.MapId != contract.Hub.MapId;
		if (offHub && !contract.MapTripList.Any(trip => trip.MapId == state.MapId) && !contract.InstanceTripList.Any(trip => trip.MapId == state.MapId) &&
			contract.Destiny?.AllowedMaps.Contains(state.MapId.Value) != true)
			return Stop("wrong-map", "blocked", $"{contract.Leg} is on map {contract.Hub.MapId}; the Cleric is on {state.MapId}.");
		// AB-08, the standing bind policy (AB-Q5): bind at the hub's obelisk first.
		if (!offHub && contract.Bind is { OnArrival: true } bind && !BoundAt(bind, contract.Hub.MapId, state.Bind))
		{
			// AO-00: take the departure hub's message before flying to the next hub to bind (Q2266 at Valurion).
			if (bind.BeforeStep is { } key && contract.Steps.Single(step => step.Key == key) is { } before &&
				!state.CompletedQuestIds.Contains(before.QuestId) &&
				(!state.Quests.TryGetValue(before.QuestId, out BotQuestState? quest) || quest.Status is not (Start or Reward or 5)) &&
				state.Level >= contract.Quest(before.QuestId).MinimumLevel && (only == null || only.Contains(before.QuestId)))
				return Plan("talk", before.QuestId, "Take the departure message before changing the working bind.", key);
			return Plan("bind", null, $"Bind at the {contract.Hub.Key} obelisk ({bind.NpcId}) before working out of it.");
		}

		// AK-08: a held quest (AK-Q2) is done for this leg once it is taken and its objective is met; its hand-in is a later leg's.
		// AK-Q4 (a): the NPC cube expansions come first, bought with the Cleric's own kinah while it has enough.
		if (!offHub && contract.CubeExpansion is { } cube && (state.CubeNpcExpansions ?? 0) < cube.Levels)
		{
			int bought = state.CubeNpcExpansions ?? 0;
			if (state.Kinah >= cube.Prices[bought] + cube.Fare)
				return Plan("cube-expansion", null,
					$"Buy cube expansion {bought + 1} of {cube.Levels} ({cube.Prices[bought]} Kinah) from {cube.ExpanderNpcId} on map {cube.MapId}.");
			checks.Add(new("cube", "wait", $"Expansion {bought + 1} costs {cube.Prices[bought]} Kinah; the Cleric has {state.Kinah}."));
		}

		// AK-08: a cube too full to take a loot (Java refuses it with STR_MSG_DICE_INVEN_ERROR) is emptied at the town's merchant.
		if (!offHub && contract.Town?.VendorNpcId is int vendor && state.FreeCubeSlots is int free &&
			free < (contract.Destiny?.FreeSlotReserve ?? NaturalInventoryPlan.QuestFreeSlotReserve))
			return Plan("town-service", null, $"The cube has {free} free slots: sell the surplus at {vendor} in {contract.Town.Key}.");

		NaturalAltgardQuest[] open = contract.Order.Select(contract.Quest)
			.Where(quest => !Done(quest.Id) && !HeldReady(quest.Id) && (only == null || only.Contains(quest.Id))).ToArray();
		NaturalAltgardQuest[] eligible = open.Where(Eligible).OrderByDescending(quest => offHub && Status(quest.Id) == Start &&
			(contract.StepsFor(quest.Id).Any(step => contract.StepMap(step) == state.MapId) ||
			 contract.InstanceTripList.Any(trip => trip.QuestId == quest.Id && trip.MapId == state.MapId))).ToArray();
		// A hand-in where the leg ends (Leg 2: Q2215 at Manir's Campsite, AM-Q1) waits until everything else is done.
		string? endArea = contract.Endpoint.Anchor is { } end
			? contract.Areas.Where(area => area.Contains(end[0], end[1], end[2]))
				.MinBy(area => (area.Max[0] - area.Min[0]) * (area.Max[1] - area.Min[1]))?.Key : null; // the most specific area
		bool AtTheEnd(NaturalAltgardQuest quest) => endArea != null && quest.Area == endArea;

		// Template quests, hub-style: accept, work, claim.
		foreach (NaturalAltgardQuest quest in eligible.Where(quest => !offHub && quest.IsTemplate && Status(quest.Id) is not (Start or Reward)))
			return Plan("template-accept", quest.Id, $"Q{quest.Id}: accept at the hub with the other hub quests.");
		NaturalCarrierChoice? carrierWait = null;
		(int QuestId, string Key, int Minutes, string Reason)? escortWait = null;
		foreach (NaturalAltgardQuest quest in eligible.Where(quest => !offHub && quest.IsTemplate && Status(quest.Id) == Start && !WorkDone(quest.Id)))
		{
			// AK-08: items that drop only from monsters that exist by the hour (Q2292's ring carriers) are hunted when a carrier
			// can be reached in its window; otherwise the wait goes to the other work (AK-Q3).
			NaturalAltgardTimedSpawn[] carriers = contract.TimedSpawnList.Where(carrier => carrier.QuestId == quest.Id).ToArray();
			if (carriers.Length == 0)
				return Plan("template-work", quest.Id, $"Q{quest.Id}: work the objectives on {quest.Area ?? "its ground"}.");
			if (state.GameMinutes is not long minutes)
				return Stop("no-game-clock", "blocked", $"Q{quest.Id}'s carriers keep hours, and the client has no game time.", quest.Id);
			var rings = carriers.Select(carrier => carrier.ItemId).Where(ring => state.ItemCounts.GetValueOrDefault(ring) < 1).ToHashSet();
			float farthest = carriers.Where(carrier => rings.Contains(carrier.ItemId)).Max(carrier => MathF.Sqrt(
				MathF.Pow(state.Position.X - carrier.Position[0], 2) + MathF.Pow(state.Position.Y - carrier.Position[1], 2)));
			NaturalCarrierChoice choice = NaturalCarrierPolicy.Decide(new NaturalCarrierObservation(rings, minutes,
				state.VisibleNpcIds ?? new HashSet<int>(), TravelGameMinutes(farthest)), carriers);
			if (choice.Action == "hunt")
				return Plan("carrier-hunt", quest.Id, $"Q{quest.Id}: {choice.Reason}", $"{choice.Carrier!.NpcId}");
			checks.Add(new("carrier", "wait", $"Q{quest.Id}: {choice.Reason}"));
			carrierWait ??= choice with { Reason = $"Q{quest.Id}: {choice.Reason}" };
		}
		foreach (NaturalAltgardQuest quest in eligible.Where(quest => quest.IsTemplate && WorkDone(quest.Id) && !Held(quest.Id) && !AtTheEnd(quest) &&
			(!offHub || objectives.GetValueOrDefault(quest.Id)?.ClaimMapId == state.MapId)))
			return Plan("template-claim", quest.Id, $"Q{quest.Id}: objectives done; claim it.");

		// Scripted quests, in the contract's order.
		foreach (NaturalAltgardQuest quest in eligible.Where(quest => !quest.IsTemplate))
		{
			byte? status = Status(quest.Id);
			if (quest.Category == "MISSION" && status is null or Locked)
			{
				checks.Add(new("campaign", "wait", $"Q{quest.Id} is not in the journal as started yet."));
				continue;
			}
			// AC-06: an escort is one action from its offer to the follower's arrival, restarts included; the var the
			// success sets is handed in by its own talk step. AG-07: an escort that starts later (Q2284 at var 1, after Germir's
			// offer and the first disguised Germir) takes its talk steps until then.
			if (contract.EscortList.FirstOrDefault(escort => escort.QuestId == quest.Id) is { } escortEntry &&
				(status is not (Start or Reward) && escortEntry.StartVar is null ||
				status == Start && (Var(quest.Id) == escortEntry.LostVar || Var(quest.Id) == escortEntry.FollowVar)))
			{
				// AG-07 (AG-Q3 (a)): a follower that keeps hours (Q2284's, 04:00-21:00) is fetched only when the escort can end
				// inside them; otherwise the other quests come first, then a wait for the window.
				if (status == Start && Var(quest.Id) == escortEntry.LostVar &&
					escortEntry.FollowerSpawnHour is int opens && escortEntry.FollowerDespawnHour is int closes)
				{
					if (state.GameMinutes is not long minutes)
						return Stop("no-game-clock", "blocked", $"Q{quest.Id}'s follower keeps hours, and the client has no game time.", quest.Id);
					int needed = EscortGameMinutes(contract, escortEntry, state.Position);
					if (!NaturalGameClock.Within(minutes, opens, closes) || NaturalGameClock.MinutesUntilHour(minutes, closes) < needed)
					{
						string reason = $"Q{quest.Id}: {escortEntry.FollowerNpcId} keeps {opens:00}:00-{closes:00}:00, and at " +
							$"{NaturalGameClock.HourOf(minutes):00}:{minutes % 60:00} the escort ({needed} game minutes) cannot end inside it: " +
							"do other work meanwhile.";
						checks.Add(new("escort-hours", "wait", reason));
						escortWait ??= (quest.Id, escortEntry.Key, NaturalGameClock.MinutesUntilHour(minutes, opens), reason);
						continue;
					}
				}
				return Plan("escort", quest.Id, $"Q{quest.Id}: escort ({(status == Start ? $"var {Var(quest.Id)}" : "not taken")}).", escortEntry.Key);
			}
			// AB-08: a timed quest is driven by NaturalTimedQuestPolicy from its offer to its hand-in.
			if (contract.TimerList.FirstOrDefault(timer => timer.QuestId == quest.Id) is { } timed && status != Reward)
				return Plan("timed", quest.Id, $"Q{quest.Id}: a timed quest ({timed.Seconds} s from {timed.StartStep}).");
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
			if (contract.Destiny is { } destiny && quest.Id == destiny.QuestId)
			{
				if (state.MapId == destiny.MapId && var == destiny.ResetVar ||
					state.MapId != destiny.MapId && var is >= 95 and <= 99)
					return Plan("recover-destiny", quest.Id, "Leave the lost attempt through learned Return and observe the outside reset before a new Skuld entry.");
				if (var == 96 && state.ItemCounts.GetValueOrDefault(destiny.StoneItemId) > 0)
					return Plan("recover-destiny", quest.Id, "The tutorial stone is already owned: recover rather than repeating Java's unguarded item-give request.");
				if (var is 97 or 98 && (state.ItemCounts.GetValueOrDefault(destiny.StoneItemId) != 1 ||
					state.SkillIds?.Contains(destiny.StigmaSkillId) != true))
					return Plan("recover-destiny", quest.Id, "The interrupted tutorial lost its actual socketed skill: leave normally before a new entry.");
				if (var == 99)
					return state.ItemCounts.GetValueOrDefault(destiny.StoneItemId) == 1
						? Plan("equip-stigma", quest.Id, "Open Skuld's stigma service and equip the actual tutorial stone once.", "q2900-equip")
						: Plan("recover-destiny", quest.Id, "The equip state needs exactly one observed tutorial stone; recover the attempt.");
				if (var == destiny.FightVar)
					return Plan("destiny-fight", quest.Id, "Fight the current timed Hellion; a lost window requires ordinary outside recovery.");
			}
			if (contract.InstanceTripList.Any(trip => trip.QuestId == quest.Id && var == trip.ResetVar && state.MapId == trip.MapId))
				return Plan("leave-instance", quest.Id, $"Q{quest.Id} reset to var {var}: leave through Dimension Exit before re-entry.");
			if (contract.InstanceTripList.Any(trip => trip.QuestId == quest.Id && var == trip.FromVar && state.MapId != trip.MapId))
				return Plan("enter-instance", quest.Id, $"Q{quest.Id} var {var}: use its ordinary quest portal.");
			if (contract.ItemUse is { } use && quest.Id == use.QuestId && var == use.Var)
				return Plan("use-item", quest.Id, $"Q{quest.Id}: use item {use.ItemId} ({(use.Anywhere ? "anywhere" : $"inside {use.Zone}")}).");
			if (contract.AirKills is { } air && quest.Id == air.QuestId && var >= air.FromVar && var <= air.RewardVar)
				return Plan("air-kills", quest.Id, $"Q{quest.Id}: shoot the Abyss Fungus down (var {var} of {air.RewardVar}).");
			// AB-08: a monster the quest spawns (Q2223's Infernus), and a custom kill counter (Q2289, Q24112, Q24013).
			if (contract.SpawnList.FirstOrDefault(spawn => spawn.QuestId == quest.Id && var == spawn.AtVar) is { } spawned)
				return Plan("spawn-kill", quest.Id, $"Q{quest.Id} var {var}: {spawned.Key}.", spawned.Key);
			if (contract.HuntList.FirstOrDefault(hunt => hunt.QuestId == quest.Id && var >= hunt.FromVar && var < hunt.ToVar) is { } hunt)
				return Plan("hunt", quest.Id, $"Q{quest.Id} var {var}: kill {string.Join("/", hunt.NpcIds)} toward var {hunt.ToVar}.");
			// AM-07: quest objects, a zone to enter, and items that drop only from a var. AB-08: an object that only loots (Q2232's
			// beehives) is done once the collection holds enough of its item.
			if (contract.ObjectUseList.FirstOrDefault(use => use.QuestId == quest.Id && var >= use.FromVar && var < use.ToVar &&
				!LootCollected(use)) is { } objectUse)
				return Plan("use-object", quest.Id, objectUse.LootItemId is int loot
					// AB-08: each use loots one more item at the same var, so the count is the progress the runner sees.
					? $"Q{quest.Id} var {var}: use {objectUse.Key} ({state.ItemCounts.GetValueOrDefault(loot)} of {loot} looted)."
					: $"Q{quest.Id} var {var}: use {objectUse.Key}.", objectUse.Key);
			if (contract.ZoneStepList.FirstOrDefault(zone => zone.QuestId == quest.Id && var == zone.FromVar) is { } zoneStep)
				return Plan("enter-zone", quest.Id, $"Q{quest.Id} var {var}: walk into {zoneStep.Zone}.");
			if (contract.CollectionList.FirstOrDefault(collection => collection.QuestId == quest.Id && var == collection.AtVar) is { } collect &&
				collect.Items.Any(item => state.ItemCounts.GetValueOrDefault(item.ItemId) < item.Count))
				return Plan("collect", quest.Id, $"Q{quest.Id} var {var}: collect " +
					string.Join(", ", collect.Items.Select(item => $"{state.ItemCounts.GetValueOrDefault(item.ItemId)}/{item.Count} of {item.ItemId}")) + ".");
			NaturalAltgardStep? next = contract.StepsFor(quest.Id).SingleOrDefault(step => step.ExpectedStatus == "START" && step.Var == var);
			return next == null
				? Stop("unexpected-var", "blocked", $"Q{quest.Id} var {var} has no contract step.", quest.Id)
				: Plan("talk", quest.Id, $"Q{quest.Id} var {var}: {next.Key}.", next.Key);
		}

		// AK-08: nothing else to do before a carrier's window opens: wait for it (the runner rests at the hub meanwhile).
		if (offHub) return Plan("return-to-hub", null, "The work on this map is done; Return to the hub bind.");
		if (carrierWait != null)
			return Plan("wait-for-carrier", carrierWait.Carrier!.QuestId, $"{carrierWait.Reason} Wait {carrierWait.WaitGameMinutes} game minutes.",
				$"{carrierWait.Carrier.NpcId}");
		// AG-07: the same for an escort's follower.
		if (escortWait is { } hold)
			return Plan("wait-for-escort", hold.QuestId, $"{hold.Reason} Wait {hold.Minutes} game minutes.", hold.Key);

		// The hand-ins held back for the end, once only they are left.
		if (open.Length > 0 && open.All(AtTheEnd))
			foreach (NaturalAltgardQuest quest in eligible.Where(quest => quest.IsTemplate && !Held(quest.Id)))
				return Plan("template-claim", quest.Id, $"Q{quest.Id}: the last hand-in, where the leg ends.");

		// Only gated quests are left: hunt for the level they need, or wait for the campaign to unlock.
		if (open.Length > 0)
		{
			int needed = open.Where(quest => state.Level < quest.MinimumLevel).Select(quest => quest.MinimumLevel).DefaultIfEmpty(0).Min();
			if (needed > state.Level)
				return Plan("hunt-for-level", null, $"Every open quest needs level {needed} or a prerequisite; hunt on the Ice Lake.");
			return Stop("gated", "awaiting-capability", $"Open quests {string.Join(", ", open.Select(quest => quest.Id))} are not available yet.");
		}

		if (only != null)
		{
			checks.Add(new("only", "pass", $"The chosen quests {string.Join(", ", only)} are done."));
			return Stop("only-complete", "complete", "The chosen quests are done (a limited run).");
		}
		if (contract.Destiny is { } finished)
		{
			if (contract.Start.CompletedQuestIds.Any(id => !state.CompletedQuestIds.Contains(id)))
				return Stop("lost-journal", "blocked", "An incoming completed quest is missing at the Destiny endpoint.");
			if (state.ItemCounts.GetValueOrDefault(finished.StoneItemId) != 0 || state.SkillIds?.Contains(finished.StigmaSkillId) == true)
				return Stop("stigma-cleanup", "blocked", "The temporary tutorial stone or skill remains after the instance exit.");
			if (state.ItemCounts.GetValueOrDefault(finished.RewardBundleId) < 1)
				return Stop("missing-reward", "blocked", "Retain the sealed stigma bundle at the endpoint.");
			if (state.ItemCounts.GetValueOrDefault(finished.LegacyRewardId) != 0)
				return Stop("unexpected-legacy-reward", "blocked", "Q2900's inactive legacy class reward must be absent at the endpoint.");
		}

		// The endpoint: every quest of the leg done, alive, where the leg ends (its own anchor, else the hub).
		float[] anchor = contract.Endpoint.Anchor ?? contract.Hub.Anchor;
		float radius = contract.Endpoint.Anchor != null ? contract.Endpoint.Radius : contract.Hub.Radius;
		float away = MathF.Sqrt(MathF.Pow(state.Position.X - anchor[0], 2) + MathF.Pow(state.Position.Y - anchor[1], 2));
		if (MathF.Abs(state.Position.Z - anchor[2]) > 15 || away > radius)
			return Plan(contract.Endpoint.Anchor != null ? "return-to-endpoint" : "return-to-hub", null,
				$"Every {contract.Leg} quest is done; walk to where the leg ends.");
		checks.Add(new("endpoint", "pass", $"All {contract.Endpoint.CompletedQuestIds.Length} quests done, at the endpoint, alive, level {state.Level}."));
		return Stop("leg-complete", "complete", $"The {contract.Leg} endpoint is reached.");

		bool Done(int questId) => state.CompletedQuestIds.Contains(questId);
		bool LootCollected(NaturalAltgardObjectUse use) => use.LootItemId is int loot && contract.CollectionList.Any(collection =>
			collection.QuestId == use.QuestId && collection.Items.Any(item => item.ItemId == loot && state.ItemCounts.GetValueOrDefault(loot) >= item.Count));
		byte? Status(int questId) => state.Quests.TryGetValue(questId, out BotQuestState? quest) ? quest.Status : null;
		int Var(int questId) => state.Quests.TryGetValue(questId, out BotQuestState? quest) ? contract.QuestVar(quest) : 0;
		bool Eligible(NaturalAltgardQuest quest)
		{
			if (state.Level < quest.MinimumLevel)
			{
				checks.Add(new("level", "wait", $"Q{quest.Id} needs level {quest.MinimumLevel}; come back at it."));
				return false;
			}
			foreach (int before in quest.PrerequisiteList)
			{
				if (!Done(before))
				{
					checks.Add(new("prerequisite", "wait", $"Q{quest.Id} needs Q{before} first."));
					return false;
				}
			}
			return true;
		}
		// A carrier quest's work is every carrier's item held (Q2292: one of each ring); its plan lists one collect step per ring.
		bool WorkDone(int questId) => contract.TimedSpawnList.Any(carrier => carrier.QuestId == questId)
			? contract.TimedSpawnList.Where(carrier => carrier.QuestId == questId).All(carrier => state.ItemCounts.GetValueOrDefault(carrier.ItemId) >= 1)
			: objectives.TryGetValue(questId, out NaturalTemplateObjective? objective) &&
				objective.IsDone(state.Quests.GetValueOrDefault(questId), state.ItemCounts);
		bool Held(int questId) => contract.HeldList.Any(held => held.QuestId == questId);
		bool HeldReady(int questId) => Held(questId) && Status(questId) is Start or Reward && WorkDone(questId);
	}
}
