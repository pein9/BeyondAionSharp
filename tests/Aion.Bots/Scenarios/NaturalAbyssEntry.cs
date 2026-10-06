namespace Aion.Bots.Scenarios;

/// <summary>
/// AX-03 (docs/natural-abyss-entry.md): the frozen scope of the Morheim arrival and Abyss-entry leg (<c>ax</c>), from the
/// <c>altgard-rc-complete-s1</c> snapshot. The commander's quest, the four missions, Garm's timed arena, the ring course,
/// what the inventory check does with the rewards, the two supplied help items and the Bronze Coin armor tiers. Static
/// walkthrough knowledge from Java and AX-01's measurements; at run time every step is still driven by what the client observes.
/// </summary>
public sealed record NaturalAbyssEntry(int CommanderQuestId, int[] MissionIds, float[] Arrival, NaturalAbyssArena Arena,
	NaturalAbyssRingCourse RingCourse, NaturalAbyssInventory Inventory, int[] ProtectedItemIds, NaturalAbyssSupply[] Supplies,
	NaturalAbyssCoinArmor CoinArmor, NaturalAbyssLevel Level, NaturalAbyssWeapon Weapon)
{
	public const string Leg = "ax";
	public const int Morheim = 220020000, Pandaemonium = 120010000, Altgard = 220030000, ArenaMap = 320090000;

	/// <summary>The objects the leg uses that no dialog step names: the arena's doors, its spirits and the coin vendor.</summary>
	public int[] GraphNpcIds => [Arena.EntranceNpcId, Arena.ExitNpcId, CoinArmor.VendorNpcId, .. Arena.Spirits.Select(spirit => spirit.NpcId)];

	public void Validate(NaturalAltgardContract contract)
	{
		string[] steps = contract.Steps.Select(step => step.Key).ToArray();
		if (contract.Leg != Leg || contract.Hub.MapId != Morheim || contract.Start.Snapshot != "altgard-rc-complete-s1" ||
			contract.Start.MapId != Altgard || contract.Start.Level != 25 || contract.Start.StartedQuestIds is not [2945] ||
			CommanderQuestId != 24020 || !MissionIds.SequenceEqual([2945, 2946, 2947, 2042]) ||
			!contract.Order.SequenceEqual([CommanderQuestId, .. MissionIds]) || Arrival.Length != 3 ||
			contract.Bind is not { NpcId: 700231, OnArrival: true, Price: 2690 } || contract.Endpoint.BindNpcId != 700231 ||
			!contract.MapTripList.Select(trip => (trip.FromMapId ?? 0, trip.MapId, trip.TeleporterNpcId, trip.LocationId, trip.Fare)).SequenceEqual(
				[(Altgard, Morheim, 203581, 10, 1700), (Morheim, Pandaemonium, 204399, 7, 1500), (Pandaemonium, Morheim, 204191, 10, 1500)]))
			throw new InvalidDataException("The Abyss-entry leg differs from its approved start, quests, bind or three teleports.");
		if (Arena is not { QuestId: 2947, MapId: ArenaMap, EntranceNpcId: 700368, ExitNpcId: 730067, EnterVar: 5, FailedVar: 6, KillCounter: 4,
				RequiredKills: 10, Seconds: 240, EnterMovieId: 167, DoneMovieId: 168, MaxAttempts: 3, InstanceLifetimeSeconds: 600,
				InstanceCheckSeconds: 60 } ||
			Arena.DoneTeleport.MapId != Pandaemonium || Arena.EntrancePosition.Length != 3 || Arena.Arrival.Length != 3 || Arena.ExitPosition.Length != 3 ||
			!new[] { Arena.StartStep, Arena.RestartStep, Arena.DoneStep }.All(steps.Contains) ||
			Arena.Spirits.Sum(spirit => spirit.Count) != 11 || Arena.Spirits.Any(spirit => spirit.Hp <= 0 || spirit.Count <= 0) ||
			Arena.Groups.Sum(group => group.Mages + group.Warriors) != 11 || Arena.Groups.Any(group => group.Center.Length != 3))
			throw new InvalidDataException("Garm's arena differs from Java's ten kills in 240 seconds, its eleven spirits or its three tries.");
		if (RingCourse is not { QuestId: 2042, StartMovieId: 89, Seconds: 70, StartVar: 2, DoneVar: 8, FailedVar: 9, RingRadius: 6, BoostSkillId: 265,
				BoostSeconds: 6, BoostFlightPoints: 9, SpeedCap: 16, MaxAttempts: 3, OnExhausted: "ask-operator-recorded-flight" } ||
			!new[] { RingCourse.StartStep, RingCourse.RestartStep, RingCourse.DoneStep }.All(steps.Contains) ||
			RingCourse.Rings.Length != RingCourse.DoneVar - RingCourse.StartVar ||
			RingCourse.Rings.Select((ring, index) => ring.Name == $"MORHEIM_ICE_FORTRESS_220020000_{index + 1}").Contains(false) ||
			// Leaving the FLY zone ends the flight: every ring's centre has to be inside its height band.
			RingCourse.Rings.Any(ring => ring.Center.Length != 3 || ring.Center[2] <= RingCourse.ZoneBottom || ring.Center[2] >= RingCourse.ZoneTop) ||
			contract.Flight?.Zones is not [{ } zone] || zone.Bottom != RingCourse.ZoneBottom || zone.Top != RingCourse.ZoneTop)
			throw new InvalidDataException("The ring course differs from Java's six ordered rings in 70 seconds, the FLY zone or its three tries.");
		if (!Inventory.Open.Order().SequenceEqual([188050873, 188050878, 188051192]) || Inventory.Discard is not [167000465] ||
			Inventory.KeepSealed is not [188053787] || !Inventory.AfterEveryTurnIn ||
			Inventory.Open.Concat(Inventory.Discard).Any(ProtectedItemIds.Contains) || Inventory.KeepSealed.Any(id => !ProtectedItemIds.Contains(id)) ||
			!contract.RewardChoiceList.Select(choice => (choice.QuestId, choice.Action, choice.ItemId)).SequenceEqual(
				[(24020, "SELECTED_QUEST_REWARD4", 110551147), (2947, "SELECTED_QUEST_REWARD2", 101501224)]) ||
			contract.RewardChoiceList.Any(choice => !ProtectedItemIds.Contains(choice.ItemId)) ||
			Weapon is not { Type: "STAFF", Prefer: "magic-boost", Buy: false })
			throw new InvalidDataException("The reward handling differs from the operator's answers AX-Q1, AX-Q4 and AX-Q5.");
		NaturalHelpLegSupply[] approved = NaturalHelpItemAllowlist.LegApproved.Where(supply => supply.Leg == Leg).ToArray();
		if (!Supplies.Select(supply => (supply.ItemId, supply.Family, supply.MaxCount, supply.Decision)).Order().SequenceEqual(
				approved.Select(supply => (supply.ItemId, supply.Family, supply.MaxCount, supply.Decision)).Order()) ||
			Supplies.Any(supply => !ProtectedItemIds.Contains(supply.ItemId)))
			throw new InvalidDataException("The leg's supplied items differ from the approved flight-speed scroll and Bronze Coins.");
		if (CoinArmor is not { CoinItemId: 186000007, IncomingCoins: 7, VendorNpcId: 204425, GoodsListId: 991, Better: "physical-defence", Weapons: false } ||
			CoinArmor.VendorPosition.Length != 3 ||
			!CoinArmor.Tiers.Select(tier => (tier.Level, tier.When, tier.Cost)).SequenceEqual([(21, "after-commander", 11), (26, "endpoint", 44)]) ||
			CoinArmor.Tiers.Any(tier => !tier.Pieces.Select(piece => piece.Slot).Order().SequenceEqual(new ushort[] { 8, 16, 32, 2048, 4096 })) ||
			Supplies.Single(supply => supply.ItemId == CoinArmor.CoinItemId).MaxCount != CoinArmor.Tiers.Max(tier => tier.Cost))
			throw new InvalidDataException("The coin armor differs from the Rank 8 and Elite Rank 7 chain tiers Vebna sells.");
		if (Level is not { Minimum: 26, By: "quests", Fallback: "fortress-quests", Hunting: false, SoulHealing: false } ||
			contract.Endpoint.MinimumLevel != Level.Minimum)
			throw new InvalidDataException("The leg's level is reached by quests: no hunting and no soul healing.");
	}
}

/// <summary>Q2947's only working branch: Garm, the entrance, ten of eleven spirits inside the timer, Garm again.</summary>
/// <param name="KillCounter">The quest variable index Java counts the kills in (<c>qs.getQuestVarById(4)</c>).</param>
/// <param name="InstanceLifetimeSeconds">AX-01: a solo instance lives this long after the player leaves, so an earlier
/// re-entry finds the dead spirits still dead.</param>
public sealed record NaturalAbyssArena(int QuestId, int MapId, int EntranceNpcId, float[] EntrancePosition, float[] Arrival,
	int ExitNpcId, float[] ExitPosition, string StartStep, string RestartStep, string DoneStep, int EnterVar, int FailedVar,
	int KillCounter, int RequiredKills, int Seconds, int EnterMovieId, int DoneMovieId, NaturalAscensionTeleport DoneTeleport,
	NaturalAbyssSpirit[] Spirits, NaturalAbyssSpiritGroup[] Groups, int MaxAttempts, int InstanceLifetimeSeconds, int InstanceCheckSeconds)
{
	/// <summary>How long after leaving a failed attempt the entrance opens a new instance (AX-01 measured 661 s).</summary>
	public long NewInstanceMillis => (InstanceLifetimeSeconds + InstanceCheckSeconds + 1) * 1000L;
}

public sealed record NaturalAbyssSpirit(int NpcId, string Name, int Count, int Hp, int AggroRange, int AttackRange, int Experience);

public sealed record NaturalAbyssSpiritGroup(string Key, float[] Center, int Mages, int Warriors);

/// <summary>Q2042's six rings, passed in order inside the timer. Each ring passed casts <paramref name="BoostSkillId"/>.</summary>
public sealed record NaturalAbyssRingCourse(int QuestId, string StartStep, string RestartStep, string DoneStep, int StartMovieId, int Seconds,
	int StartVar, int DoneVar, int FailedVar, float RingRadius, NaturalAbyssRing[] Rings, int BoostSkillId, int BoostSeconds,
	int BoostFlightPoints, float SpeedCap, float ZoneBottom, float ZoneTop, int MaxAttempts, string OnExhausted);

public sealed record NaturalAbyssRing(string Name, float[] Center);

/// <summary>What the inventory check does with this leg's rewards, after every quest turn-in.</summary>
public sealed record NaturalAbyssInventory(int[] Open, int[] Discard, int[] KeepSealed, bool AfterEveryTurnIn);

/// <summary>A help item the operator approved for this leg only, with the decision that approved it.</summary>
public sealed record NaturalAbyssSupply(int ItemId, string Family, int MaxCount, string Trigger, string Decision);

public sealed record NaturalAbyssCoinArmor(int CoinItemId, int IncomingCoins, int VendorNpcId, float[] VendorPosition, int GoodsListId,
	string Better, bool Weapons, NaturalAbyssCoinTier[] Tiers);

public sealed record NaturalAbyssCoinTier(int Level, string When, string Name, NaturalCoinGearPurchase[] Pieces)
{
	public int Cost => Pieces.Sum(piece => piece.Cost);
}

public sealed record NaturalAbyssLevel(int Minimum, string By, string Fallback, bool Hunting, bool SoulHealing);

public sealed record NaturalAbyssWeapon(string Type, string Prefer, bool Buy);

/// <summary>One try at the arena or the ring course, as the outcome ledger and the trace record it. A failed try is an
/// outcome, not a test failure.</summary>
/// <param name="Progress">Spirits killed, or rings passed.</param>
public sealed record NaturalAbyssAttempt(string Kind, int Number, string Outcome, long StartedMillis, long EndedMillis, int Progress, string Reason);

public sealed record NaturalAbyssAttemptDecision(string Action, string Reason, long WaitMillis = 0);

/// <summary>The bounded tries of the leg's two timed quests (AX-Q3), decided from the attempts already recorded.</summary>
public static class NaturalAbyssAttempts
{
	public const string Arena = "arena", RingCourse = "ring-course", Done = "done";

	/// <summary>Trace rows the outcome ledger collects (<c>scripts/sim/audit-natural-complete.py</c> keeps every "timed" row).</summary>
	public static string Diagnostic(string kind) => kind switch
	{
		Arena => "timed-arena-attempt",
		RingCourse => "timed-ring-course-attempt",
		_ => throw new ArgumentException($"Unknown attempt kind '{kind}'.", nameof(kind)),
	};

	public static Dictionary<string, object?> Row(NaturalAbyssAttempt attempt) => new()
	{
		["kind"] = attempt.Kind, ["number"] = attempt.Number, ["outcome"] = attempt.Outcome, ["startedMillis"] = attempt.StartedMillis,
		["endedMillis"] = attempt.EndedMillis, ["progress"] = attempt.Progress, ["reason"] = attempt.Reason,
	};

	/// <summary>Enter, wait for the failed attempt's instance to be destroyed, or stop after the last allowed try.</summary>
	public static NaturalAbyssAttemptDecision NextArena(NaturalAbyssArena arena, IReadOnlyList<NaturalAbyssAttempt> attempts, long nowMillis)
	{
		NaturalAbyssAttempt[] tries = Tries(attempts, Arena);
		if (tries.Any(attempt => attempt.Outcome == Done)) return new("complete", "The arena is cleared.");
		if (tries.Length >= arena.MaxAttempts)
			return new("stop-finding", $"All {arena.MaxAttempts} arena attempts failed; stop the leg as a finding to fix.");
		if (tries.Length == 0) return new("enter", "First arena attempt.");
		long ready = tries[^1].EndedMillis + arena.NewInstanceMillis;
		return nowMillis < ready
			? new("wait-for-new-instance", "The failed attempt's instance still holds its dead spirits.", ready - nowMillis)
			: new("enter", $"Arena attempt {tries.Length + 1} of {arena.MaxAttempts}, in a new instance.");
	}

	/// <summary>Start the course, or after the last allowed try ask the operator for the recorded flight they offered.</summary>
	public static NaturalAbyssAttemptDecision NextRingCourse(NaturalAbyssRingCourse course, IReadOnlyList<NaturalAbyssAttempt> attempts)
	{
		NaturalAbyssAttempt[] tries = Tries(attempts, RingCourse);
		if (tries.Any(attempt => attempt.Outcome == Done)) return new("complete", "The ring course is flown.");
		return tries.Length >= course.MaxAttempts
			? new(course.OnExhausted, $"All {course.MaxAttempts} ring-course attempts failed; ask the operator to record a flight.")
			: new("start", $"Ring-course attempt {tries.Length + 1} of {course.MaxAttempts}.");
	}

	private static NaturalAbyssAttempt[] Tries(IReadOnlyList<NaturalAbyssAttempt> attempts, string kind)
	{
		NaturalAbyssAttempt[] tries = attempts.Where(attempt => attempt.Kind == kind).ToArray();
		if (tries.Select(attempt => attempt.Number).SequenceEqual(Enumerable.Range(1, tries.Length)) &&
			tries.All(attempt => attempt.EndedMillis >= attempt.StartedMillis)) return tries;
		throw new InvalidDataException($"The recorded {kind} attempts are not numbered in order.");
	}
}

/// <summary>What the client showed when the leg began: the receipt a contained run keeps beside its trace.
/// Observed state only; it is never replayed to set anything up.</summary>
public sealed record NaturalAbyssEntryStart(string Leg, bool Verified, int CharacterId, int MapId, Aion.Bots.World.BotPosition Position, int Level,
	long Kinah, long BronzeCoins, int CompletedQuests, int[] StartedQuestIds, int StaffObjectId, NaturalJourneyItem[] ProtectedItems, long GameMillis);

/// <summary>The leg's incoming contract, checked against what the retained character's login showed.</summary>
public static class NaturalAbyssEntryLeg
{
	public const string StartReceipt = "altgard-ax-start.json";
	public const string StartDiagnostic = "abyss-entry-start-verified";

	/// <summary>The three fares and the bind at their shipped base prices; the price modifier is added on top (AX-01).</summary>
	public static long BaseTravelCost(NaturalAltgardContract leg) => leg.MapTripList.Sum(trip => (long)trip.Fare) + (leg.Bind?.Price ?? 0);

	/// <summary>Refuse anything but the approved start: the level-25 Cleric at the Altgard endpoint, its whole journal, Q2945
	/// untouched, the worn staff, the sealed bundle and the seven Bronze Coins. Nothing is repaired here.</summary>
	public static NaturalAbyssEntryStart VerifyStart(NaturalAltgardContract leg, NaturalAltgardObservation state, int characterId, long gameMillis)
	{
		NaturalAbyssEntry scope = leg.AbyssEntry ?? throw new InvalidDataException($"{leg.Leg} has no Abyss-entry scope.");
		NaturalJourneyItem[] inventory = state.Inventory ?? throw new InvalidDataException("The start needs the observed inventory.");
		void Require(bool condition, string what)
		{
			if (!condition) throw new InvalidDataException($"The Abyss-entry leg cannot start: {what}.");
		}
		Require(state.Synchronized && !state.IsDead, "the login state is incomplete or the character is dead");
		Require(state.MapId == leg.Start.MapId && state.Level == leg.Start.Level, $"expected level {leg.Start.Level} on map {leg.Start.MapId}, saw level {state.Level} on {state.MapId}");
		Require(state.Bind?.MapId == leg.Start.MapId, "the bind is not the Altgard obelisk the snapshot ends at");
		Require(leg.Start.CompletedQuestIds.All(state.CompletedQuestIds.Contains), "an incoming completed quest is missing");
		Require(!leg.Order.Any(state.CompletedQuestIds.Contains), "a quest of this leg is already complete");
		int[] started = state.Quests.Keys.Order().ToArray();
		Require(started.SequenceEqual((leg.Start.StartedQuestIds ?? []).Order()), $"the journal holds [{string.Join(", ", started)}]");
		Require(started.All(id => state.Quests[id] is { Status: 3, StepAndFlags: 0 }), "an incoming quest has already moved");
		NaturalJourneyItem[] staffs = inventory.Where(item => item.EquipmentSlot == 3).ToArray();
		Require(staffs is [{ ItemId: 101501357 }], "the Altgard Dark Legionary Staff is not the worn weapon");
		Require(scope.Inventory.KeepSealed.All(id => inventory.Count(item => item.ItemId == id) == 1), "the sealed stigma bundle is missing");
		Require(state.ItemCounts.GetValueOrDefault(scope.CoinArmor.CoinItemId) == scope.CoinArmor.IncomingCoins,
			$"expected {scope.CoinArmor.IncomingCoins} Bronze Coins, saw {state.ItemCounts.GetValueOrDefault(scope.CoinArmor.CoinItemId)}");
		// What the leg will earn or be supplied is not owned yet, so every later count is this leg's own.
		int[] early = leg.RewardChoiceList.Select(choice => choice.ItemId).Concat(scope.Inventory.Open).Concat(scope.Inventory.Discard)
			.Concat(scope.Supplies.Where(supply => supply.ItemId != scope.CoinArmor.CoinItemId).Select(supply => supply.ItemId))
			.Concat(scope.CoinArmor.Tiers.SelectMany(tier => tier.Pieces.Select(piece => piece.ItemId)))
			.Where(id => state.ItemCounts.GetValueOrDefault(id) > 0).ToArray();
		Require(early.Length == 0, $"items this leg earns are already owned: [{string.Join(", ", early)}]");
		Require(state.Kinah >= BaseTravelCost(leg) * 2, $"{state.Kinah} Kinah does not cover the teleports and the bind");
		return new(leg.Leg, true, characterId, state.MapId!.Value, state.Position, state.Level, state.Kinah,
			state.ItemCounts.GetValueOrDefault(scope.CoinArmor.CoinItemId), state.CompletedQuestIds.Count, started, staffs[0].ObjectId,
			inventory.Where(item => scope.ProtectedItemIds.Contains(item.ItemId)).OrderBy(item => item.ItemId).ThenBy(item => item.ObjectId).ToArray(),
			gameMillis);
	}
}
