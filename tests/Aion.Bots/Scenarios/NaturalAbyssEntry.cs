using Aion.Bots.World;

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
	public int[] GraphNpcIds => [Arena.ExitNpcId, CoinArmor.VendorNpcId, .. Arena.Spirits.Select(spirit => spirit.NpcId)];

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
		if (Arena is not { QuestId: 2947, MapId: ArenaMap, ExitNpcId: 730067, EnterVar: 5, FailedVar: 6, KillCounter: 4,
				RequiredKills: 10, Seconds: 240, EnterMovieId: 167, DoneMovieId: 168, MaxAttempts: 3 } ||
			Arena.DoneTeleport.MapId != Pandaemonium || Arena.Arrival.Length != 3 || Arena.ExitPosition.Length != 3 ||
			!new[] { Arena.StartStep, Arena.RestartStep, Arena.DoneStep }.All(steps.Contains) ||
			// D35: Garm's two SETPRO3 talks end inside the arena; his third talk moves nobody.
			new[] { Arena.StartStep, Arena.RestartStep }.Any(key => contract.Steps.First(step => step.Key == key).Teleport is not { MapId: ArenaMap } entry ||
				!entry.Position.SequenceEqual(Arena.Arrival)) ||
			contract.Steps.First(step => step.Key == Arena.DoneStep).Teleport != null ||
			Arena.Spirits.Sum(spirit => spirit.Count) != 12 || Arena.Spirits.Any(spirit => spirit.Hp <= 0 || spirit.Count <= 0) ||
			Arena.Groups.Sum(group => group.Mages + group.Warriors) != 12 || Arena.Groups.Any(group => group.Center.Length != 3))
			throw new InvalidDataException("Garm's arena differs from Java's ten kills in 240 seconds, its twelve spirits (D36), Garm's teleport (D35) or its three tries.");
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

/// <summary>Q2947's only working branch: Garm, who sends the player in (D35), ten of twelve spirits (D36) inside the timer, Garm again. A failed
/// attempt needs no wait: the server destroys its instance, so Garm's next SETPRO3 leads to a new one (D34).</summary>
/// <param name="KillCounter">The quest variable index Java counts the kills in (<c>qs.getQuestVarById(4)</c>).</param>
public sealed record NaturalAbyssArena(int QuestId, int MapId, float[] Arrival,
	int ExitNpcId, float[] ExitPosition, string StartStep, string RestartStep, string DoneStep, int EnterVar, int FailedVar,
	int KillCounter, int RequiredKills, int Seconds, int EnterMovieId, int DoneMovieId, NaturalAscensionTeleport DoneTeleport,
	NaturalAbyssSpirit[] Spirits, NaturalAbyssSpiritGroup[] Groups, int MaxAttempts);

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

public sealed record NaturalAbyssAttemptDecision(string Action, string Reason);

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

	/// <summary>Enter, or stop after the last allowed try. Every try is in a new instance (D34), so none has to wait.</summary>
	public static NaturalAbyssAttemptDecision NextArena(NaturalAbyssArena arena, IReadOnlyList<NaturalAbyssAttempt> attempts)
	{
		NaturalAbyssAttempt[] tries = Tries(attempts, Arena);
		if (tries.Any(attempt => attempt.Outcome == Done)) return new("complete", "The arena is cleared.");
		if (tries.Length >= arena.MaxAttempts)
			return new("stop-finding", $"All {arena.MaxAttempts} arena attempts failed; stop the leg as a finding to fix.");
		return new("enter", tries.Length == 0 ? "First arena attempt." : $"Arena attempt {tries.Length + 1} of {arena.MaxAttempts}, in a new instance.");
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

/// <summary>One quest turn-in of the leg, as the client saw it paid.</summary>
public sealed record NaturalAbyssPayment(int QuestId, long Experience, long Kinah, int Level);

/// <summary>Where the segment stood when it reached the decision rule's frontier: the receipt a contained run keeps beside its
/// trace. Observed state only.</summary>
/// <param name="Frontier">The phase the rule named as not played yet.</param>
/// <param name="StartedQuestIds">The journal's quests at START or REWARD.</param>
/// <param name="LockedQuestIds">The journal's locked quests: campaign quests waiting for a level.</param>
/// <param name="CoinManifests">Every coin armor manifest the leg decided, slot by slot, bought or not.</param>
public sealed record NaturalAbyssEntryProgress(string Leg, string Frontier, int CharacterId, int MapId, Aion.Bots.World.BotPosition Position, int Level,
	long ExperienceGained, long Kinah, long KinahAtStart, long Fares, long BindPaid, NaturalAbyssPayment[] Payments, int BindMapId,
	Aion.Bots.World.BotPosition BindPosition, int StaffItemId, int TorsoItemId, int[] CompletedLegQuestIds, int[] StartedQuestIds,
	int[] LockedQuestIds, int InventoryChecks, long GameMillis, long BronzeCoins = 0, long CoinsSupplied = 0,
	NaturalAbyssCoinManifest[]? CoinManifests = null, NaturalAbyssCoinPurchase[]? CoinPurchases = null);

/// <summary>What the runner counted while the leg ran. Everything else in a receipt is the client's view.</summary>
/// <param name="InventoryChecks">One at the start and one after each turn-in.</param>
/// <param name="OtherInventoryChecks">The checks that wore bought coin armor.</param>
public sealed record NaturalAbyssLedger(long ExperienceGained, long Fares, long BindPaid, IReadOnlyList<NaturalAbyssPayment> Payments,
	int InventoryChecks, int OtherInventoryChecks, IReadOnlyList<NaturalAbyssCoinManifest> CoinManifests,
	IReadOnlyList<NaturalAbyssCoinPurchase> CoinPurchases, long CoinsSupplied);

/// <summary>The leg's incoming contract, checked against what the retained character's login showed.</summary>
public static class NaturalAbyssEntryLeg
{
	public const string StartReceipt = "altgard-ax-start.json";
	public const string StartDiagnostic = "abyss-entry-start-verified";
	public const string ProgressReceipt = "altgard-ax-progress.json";
	public const string ProgressDiagnostic = "abyss-entry-frontier";
	private const ushort NotWorn = ushort.MaxValue;
	private const long Torso = 8;

	/// <summary>AX-05, AX-06: what has to be true at the frontier <paramref name="frontier"/>, from the client's view. Morheim and
	/// the commander: the Cleric stands in Morheim, bound at the fortress obelisk; Q24020 is complete and paid; its hauberk and
	/// the best owned staff are worn; every Kinah that left went to the teleport and the bind; Q2945 has not moved; an
	/// inventory check ran at the start and after each turn-in. The level-21 coin armor: one manifest was decided; every piece
	/// it bought is worn; no piece on offer beats what is worn now; the coins held are the incoming ones, plus the supplied
	/// ones, less the manifest's cost.</summary>
	public static NaturalAbyssEntryProgress VerifyProgress(NaturalAltgardContract leg, NaturalAbyssEntryStart start, NaturalAltgardObservation state,
		string frontier, NaturalAbyssLedger ledger, Func<int, int> staffMagicBoost, Func<int, int> physicalDefence, long gameMillis)
	{
		NaturalAbyssEntry scope = leg.AbyssEntry ?? throw new InvalidDataException($"{leg.Leg} has no Abyss-entry scope.");
		NaturalJourneyItem[] inventory = state.Inventory ?? throw new InvalidDataException("The frontier needs the observed inventory.");
		void Require(bool condition, string what)
		{
			if (!condition) throw new InvalidDataException($"The leg is not at {frontier}: {what}.");
		}
		bool coinArmor = frontier != NaturalAbyssEntryDecisionEngine.CoinArmor21Phase;
		(long experienceGained, long fares, long bindPaid, IReadOnlyList<NaturalAbyssPayment> payments) =
			(ledger.ExperienceGained, ledger.Fares, ledger.BindPaid, ledger.Payments);
		int inventoryChecks = ledger.InventoryChecks;
		NaturalAltgardBind bind = leg.Bind ?? throw new InvalidDataException($"{leg.Leg} has no bind.");
		NaturalAltgardRewardChoice hauberk = leg.RewardChoiceList.Single(choice => choice.QuestId == scope.CommanderQuestId);
		Require(state.Synchronized && !state.IsDead && state.MapId == Aion.Bots.Scenarios.NaturalAbyssEntry.Morheim, $"the Cleric is on map {state.MapId}");
		Require(NaturalAltgardDecisionEngine.BoundAt(bind, leg.Hub.MapId, state.Bind), $"the bind is {state.Bind}, not obelisk {bind.NpcId}");
		Require(state.CompletedQuestIds.Contains(scope.CommanderQuestId), $"Q{scope.CommanderQuestId} is not complete");
		Require(payments.Select(payment => payment.QuestId).SequenceEqual([scope.CommanderQuestId]), "the commander's quest is not the one turn-in so far");
		Require(payments[0].Experience == leg.Quest(scope.CommanderQuestId).RewardExperience,
			$"Q{scope.CommanderQuestId} paid {payments[0].Experience} XP, not {leg.Quest(scope.CommanderQuestId).RewardExperience}");
		Require(experienceGained == payments.Sum(payment => payment.Experience), $"{experienceGained} XP was gained, and the turn-ins paid {payments.Sum(payment => payment.Experience)}");
		Require((leg.Start.StartedQuestIds ?? []).All(id => state.Quests.GetValueOrDefault(id) is { Status: 3, StepAndFlags: 0 }), "an incoming quest moved before its turn");
		Require(state.Kinah == start.Kinah - fares - bindPaid + payments.Sum(payment => payment.Kinah),
			$"{state.Kinah} Kinah is not {start.Kinah} less {fares} in fares and {bindPaid} for the bind, plus the quest's pay");
		Require(fares > 0 && bindPaid == bind.Price, $"the fares were {fares} and the bind {bindPaid}");
		NaturalJourneyItem[] torso = inventory.Where(item => item.EquipmentSlot != NotWorn && (item.EquipmentSlot & Torso) != 0).ToArray();
		Require(torso is [{ } worn] && worn.ItemId == hauberk.ItemId, $"the worn torso is [{string.Join(", ", torso.Select(item => item.ItemId))}], not {hauberk.ItemId}");
		NaturalJourneyItem[] hands = inventory.Where(item => item.EquipmentSlot != NotWorn && (item.EquipmentSlot & 1) != 0).ToArray();
		int best = inventory.Select(item => staffMagicBoost(item.ItemId)).DefaultIfEmpty(0).Max();
		Require(hands is [{ } staff] && staffMagicBoost(staff.ItemId) == best && best > 0, "the worn weapon is not the owned staff with the most magic boost");
		Require(scope.Inventory.KeepSealed.All(id => inventory.Count(item => item.ItemId == id) == 1), "the sealed stigma bundle changed");
		Require(inventoryChecks == 1 + payments.Count, $"{inventoryChecks} inventory checks ran for {payments.Count} turn-ins");
		long coins = state.ItemCounts.GetValueOrDefault(scope.CoinArmor.CoinItemId);
		Require(ledger.CoinPurchases.All(purchase => purchase.KinahAfter == purchase.KinahBefore), "a coin armor piece cost Kinah");
		Require(coins == start.BronzeCoins + ledger.CoinsSupplied - ledger.CoinPurchases.Sum(purchase => purchase.Cost),
			$"{coins} Bronze Coins are not the {start.BronzeCoins} brought, plus {ledger.CoinsSupplied} supplied, less {ledger.CoinPurchases.Sum(purchase => purchase.Cost)} spent");
		if (coinArmor)
		{
			NaturalAbyssCoinTier early = scope.CoinArmor.Tiers.Single(tier => tier.When == "after-commander");
			Require(ledger.CoinManifests.Count(manifest => manifest.Level == early.Level) == 1, $"the {early.Name} manifest was not decided exactly once");
			NaturalAbyssCoinManifest decided = ledger.CoinManifests.Single(manifest => manifest.Level == early.Level);
			Require(decided.Buys.Select(slot => slot.CoinItemId).Order().SequenceEqual(
				ledger.CoinPurchases.Where(purchase => purchase.Level == early.Level).Select(purchase => purchase.ItemId).Order()), "the purchases are not the manifest");
			Require(ledger.CoinsSupplied == decided.CoinsToSupply, $"{ledger.CoinsSupplied} coins were supplied where the manifest was {decided.CoinsToSupply} short");
			Require(ledger.CoinPurchases.All(purchase => inventory.Any(item => item.ObjectId == purchase.ObjectId && item.ItemId == purchase.ItemId &&
				item.EquipmentSlot != NotWorn && (item.EquipmentSlot & purchase.Slot) != 0)), "a bought piece is not worn");
			Require(NaturalAbyssCoinArmorPolicy.Plan(scope.CoinArmor, early, inventory, physicalDefence).Done, $"a {early.Name} piece still beats what is worn");
		}
		else
			Require(ledger.CoinManifests.Count == 0 && ledger.CoinPurchases.Count == 0 && ledger.CoinsSupplied == 0, "coin armor was bought before its turn");
		BotBindPoint bound = state.Bind!;
		return new(leg.Leg, frontier, start.CharacterId, state.MapId!.Value, state.Position, state.Level, experienceGained, state.Kinah, start.Kinah, fares,
			bindPaid, [.. payments], bound.MapId, bound.Position, hands[0].ItemId, torso[0].ItemId,
			leg.Order.Where(state.CompletedQuestIds.Contains).ToArray(),
			state.Quests.Values.Where(quest => quest.Status is 3 or 4).Select(quest => quest.QuestId).Order().ToArray(),
			state.Quests.Values.Where(quest => quest.Status == NaturalAltgardDecisionEngine.Locked).Select(quest => quest.QuestId).Order().ToArray(),
			ledger.InventoryChecks + ledger.OtherInventoryChecks, gameMillis, coins, ledger.CoinsSupplied, [.. ledger.CoinManifests],
			[.. ledger.CoinPurchases]);
	}

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
