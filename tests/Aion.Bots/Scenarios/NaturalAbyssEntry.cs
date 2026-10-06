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
	NaturalAbyssCoinArmor CoinArmor, NaturalAbyssLevel Level, NaturalAbyssWeapon Weapon, NaturalAbyssSoulHealer SoulHealer)
{
	public const string Leg = "ax";
	public const int Morheim = 220020000, Pandaemonium = 120010000, Altgard = 220030000, ArenaMap = 320090000;

	/// <summary>The objects the leg uses that no dialog step names: the arena's doors, its spirits and the coin vendor.</summary>
	public int[] GraphNpcIds => [Arena.ExitNpcId, CoinArmor.VendorNpcId, SoulHealer.NpcId, .. Arena.Spirits.Select(spirit => spirit.NpcId)];

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
			Arena.Groups.Sum(group => group.Mages + group.Warriors) != 12 || Arena.Groups.Any(group => group.Center.Length != 3) ||
			!Arena.Groups.Select(group => group.DoorId).Order().SequenceEqual([1, 2, 10]) ||
			Arena.Groups.Any(group => group.DoorPosition.Length != 3 || group.DoorStand.Length != 3))
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
			Weapon is not { Type: "STAFF", Prefer: "magic-boost", Buy: true })
			throw new InvalidDataException("The reward handling differs from the operator's answers AX-Q1, AX-Q4 and AX-Q5.");
		NaturalHelpLegSupply[] approved = NaturalHelpItemAllowlist.LegApproved.Where(supply => supply.Leg == Leg).ToArray();
		if (!Supplies.Select(supply => (supply.ItemId, supply.Family, supply.MaxCount, supply.Decision)).Order().SequenceEqual(
				approved.Select(supply => (supply.ItemId, supply.Family, supply.MaxCount, supply.Decision)).Order()) ||
			Supplies.Any(supply => !ProtectedItemIds.Contains(supply.ItemId)))
			throw new InvalidDataException("The leg's supplied items differ from the approved flight-speed scroll and Bronze Coins.");
		// AX-12c (operator, 2026-10-06): the best coin staff of a tier is bought when it has more magic boost than the worn one.
		if (CoinArmor is not { CoinItemId: 186000007, IncomingCoins: 7, VendorNpcId: 204425, GoodsListId: 991, Better: "physical-defence", Weapons: true,
				StaffGoodsListId: 989, StaffBetter: "magic-boost" } ||
			!CoinArmor.Tiers.Select(tier => (tier.Staff?.ItemId, tier.Staff?.Cost, tier.Staff?.Slot)).SequenceEqual(
				[(101500811, 4, (ushort)3), (101500818, 19, (ushort)3)]) ||
			CoinArmor.VendorPosition.Length != 3 ||
			!CoinArmor.Tiers.Select(tier => (tier.Level, tier.When, tier.Cost)).SequenceEqual([(21, "after-commander", 11), (26, "endpoint", 44)]) ||
			CoinArmor.Tiers.Any(tier => !tier.Pieces.Select(piece => piece.Slot).Order().SequenceEqual(new ushort[] { 8, 16, 32, 2048, 4096 })) ||
			Supplies.Single(supply => supply.ItemId == CoinArmor.CoinItemId).MaxCount != CoinArmor.Tiers.Max(tier => tier.Cost))
			throw new InvalidDataException("The coin armor differs from the Rank 8 and Elite Rank 7 chain tiers Vebna sells.");
		if (Level is not { Minimum: 26, By: "quests", Fallback: "fortress-quests", Hunting: false, SoulHealing: false } ||
			contract.Endpoint.MinimumLevel != Level.Minimum)
			throw new InvalidDataException("The leg's level is reached by quests: no hunting and no soul healing.");
		if (SoulHealer is not { NpcId: 204318, TitleId: NaturalServicePolicy.SoulHealerTitleId, After: "obelisk-revive",
				DialogAction: NaturalServicePolicy.SoulHealDialogAction, QuestionId: NaturalServicePolicy.SoulHealQuestionId } ||
			SoulHealer.Position.Length != 3)
			throw new InvalidDataException("The soul healing differs from the operator's rule: Golenthor, after every obelisk revive.");
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

/// <summary>One of the arena's three rooms. Its spirits stand behind a closed door the player clicks open (the shipped
/// static door has state 6: clickable and closeable, not opened; NCSoft's world file agrees).</summary>
/// <param name="DoorStand">Hall ground in front of the door, from where it is clicked.</param>
public sealed record NaturalAbyssSpiritGroup(string Key, float[] Center, int Mages, int Warriors, int DoorId, float[] DoorPosition, float[] DoorStand);

/// <summary>Q2042's six rings, passed in order inside the timer. Each ring passed casts <paramref name="BoostSkillId"/>.</summary>
public sealed record NaturalAbyssRingCourse(int QuestId, string StartStep, string RestartStep, string DoneStep, int StartMovieId, int Seconds,
	int StartVar, int DoneVar, int FailedVar, float RingRadius, NaturalAbyssRing[] Rings, int BoostSkillId, int BoostSeconds,
	int BoostFlightPoints, float SpeedCap, float ZoneBottom, float ZoneTop, int MaxAttempts, string OnExhausted);

public sealed record NaturalAbyssRing(string Name, float[] Center);

/// <summary>What the inventory check does with this leg's rewards, after every quest turn-in.</summary>
public sealed record NaturalAbyssInventory(int[] Open, int[] Discard, int[] KeepSealed, bool AfterEveryTurnIn);

/// <summary>A help item the operator approved for this leg only, with the decision that approved it.</summary>
public sealed record NaturalAbyssSupply(int ItemId, string Family, int MaxCount, string Trigger, string Decision);

/// <param name="StaffGoodsListId">AX-12c: the vendor's weapon tab, where each tier's staff is sold.</param>
public sealed record NaturalAbyssCoinArmor(int CoinItemId, int IncomingCoins, int VendorNpcId, float[] VendorPosition, int GoodsListId,
	string Better, bool Weapons, NaturalAbyssCoinTier[] Tiers, int StaffGoodsListId = 0, string StaffBetter = "magic-boost");

/// <param name="Staff">AX-12c: the tier's best staff. It is not part of <see cref="Cost"/>, the tier's armor.</param>
public sealed record NaturalAbyssCoinTier(int Level, string When, string Name, NaturalCoinGearPurchase[] Pieces, NaturalCoinGearPurchase? Staff = null)
{
	public int Cost => Pieces.Sum(piece => piece.Cost);
}

public sealed record NaturalAbyssLevel(int Minimum, string By, string Fallback, bool Hunting, bool SoulHealing);

/// <summary>AX-12b: the operator's rule, "always soul heal when we resurrect at an obelisk". The Soul Healer beside the leg's
/// bind obelisk; Java's DialogService answers its RECOVERY action with the priced question.</summary>
public sealed record NaturalAbyssSoulHealer(int NpcId, int TitleId, float[] Position, string After, int DialogAction, int QuestionId);

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
	NaturalAbyssCoinManifest[]? CoinManifests = null, NaturalAbyssCoinPurchase[]? CoinPurchases = null, NaturalOpenedContainer[]? Opened = null,
	NaturalAbyssAttempt[]? Attempts = null, long ArenaExperience = 0, int Deaths = 0, NaturalJourneyItem[]? Discarded = null,
	long ScrollsSupplied = 0, long ScrollsUsed = 0, NaturalSoulHeal[]? SoulHeals = null, int ObeliskRevives = 0, long CourseExperience = 0);

/// <summary>What the runner counted while the leg ran. Everything else in a receipt is the client's view.</summary>
/// <param name="InventoryChecks">One at the start and one after each turn-in.</param>
/// <param name="OtherInventoryChecks">The checks that wore bought coin armor.</param>
/// <param name="Opened">Every reward container the inventory checks opened, with what it gave.</param>
/// <param name="NotOpened">Containers the server did not open (a full cube).</param>
/// <param name="Attempts">Every arena and ring-course try, failed ones included.</param>
/// <param name="ArenaExperience">The XP the arena tries changed, net: each kill pays, a death takes.</param>
/// <param name="Discarded">Every item the inventory checks discarded.</param>
/// <param name="ScrollsSupplied">Flight-speed scrolls the leg's approved supply added (AX-Q3: one at most).</param>
/// <param name="ScrollsUsed">Flight-speed scrolls used at the ring course's start.</param>
public sealed record NaturalAbyssLedger(long ExperienceGained, long Fares, long BindPaid, IReadOnlyList<NaturalAbyssPayment> Payments,
	int InventoryChecks, int OtherInventoryChecks, IReadOnlyList<NaturalAbyssCoinManifest> CoinManifests,
	IReadOnlyList<NaturalAbyssCoinPurchase> CoinPurchases, long CoinsSupplied, IReadOnlyList<NaturalOpenedContainer> Opened, int NotOpened,
	IReadOnlyList<NaturalAbyssAttempt> Attempts, long ArenaExperience, int Deaths, IReadOnlyList<NaturalJourneyItem> Discarded,
	long ScrollsSupplied = 0, long ScrollsUsed = 0, IReadOnlyList<NaturalSoulHeal>? SoulHeals = null, int ObeliskRevives = 0,
	long CourseExperience = 0);

/// <summary>The leg's incoming contract, checked against what the retained character's login showed.</summary>
public static class NaturalAbyssEntryLeg
{
	public const string StartReceipt = "altgard-ax-start.json";
	public const string StartDiagnostic = "abyss-entry-start-verified";
	public const string ProgressReceipt = "altgard-ax-progress.json";
	public const string ProgressDiagnostic = "abyss-entry-frontier";
	private const ushort NotWorn = ushort.MaxValue;
	private const long Torso = 8;

	/// <summary>AX-05..AX-12: what has to be true at the frontier <paramref name="frontier"/>, from the client's view. Morheim and
	/// the commander: bound at the fortress obelisk; Q24020 complete; its hauberk and the best owned staff worn; every Kinah
	/// that left went to the teleports and the bind; an inventory check ran at the start and after each turn-in. The level-21
	/// coin armor: one manifest decided; every piece it bought worn; no piece on offer beats what is worn. The capital missions:
	/// Q2945 and Q2946 turned in, in order, each for its shipped XP; Q2947 taken at Kvasir and waiting for Garm; every reward
	/// container opened. The coins held are the incoming ones, plus the supplied ones and what the chests gave, less what the
	/// manifests cost. The arena: its tries in order, the last one the clear. The return: Q2947 turned in for the staff, which
	/// is worn; the flight-time manastone discarded; Q2042 taken at Aegir and waiting for Yornduf. The ring course: its tries
	/// in order, the last one all six rings; Q2042 turned in; at most the one approved scroll supplied, and used. The level-26
	/// coin armor: level 26 first; its manifest decided once and bought; the supplied coins are what the manifests were short.</summary>
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
		NaturalJourneyItem[] discards = [.. ledger.Discarded];
		// The frontiers in the leg's order; each one keeps what the ones before it established.
		string[] frontiers = [NaturalAbyssEntryDecisionEngine.CoinArmor21Phase, NaturalAbyssEntryDecisionEngine.CapitalPhase,
			NaturalAbyssEntryDecisionEngine.ArenaPhase, NaturalAbyssEntryDecisionEngine.ReturnPhase, NaturalAbyssEntryDecisionEngine.RingCoursePhase,
			NaturalAbyssEntryDecisionEngine.CoinArmor26Phase, NaturalAbyssEntryDecisionEngine.EndpointPhase];
		int stage = Array.IndexOf(frontiers, frontier);
		if (stage < 0) throw new InvalidDataException($"The frontier '{frontier}' has no check yet.");
		bool settled = stage >= 6, flown = stage >= 5, returned = stage >= 4, cleared = stage >= 3, capital = stage >= 2;
		int[] paid = flown ? [scope.CommanderQuestId, .. scope.MissionIds]
			: returned ? [scope.CommanderQuestId, .. scope.MissionIds.TakeWhile(id => id != scope.RingCourse.QuestId)]
			: capital ? [scope.CommanderQuestId, .. scope.MissionIds.TakeWhile(id => id != scope.Arena.QuestId)] : [scope.CommanderQuestId];
		int map = capital && !returned ? Aion.Bots.Scenarios.NaturalAbyssEntry.Pandaemonium : Aion.Bots.Scenarios.NaturalAbyssEntry.Morheim;
		(long experienceGained, long fares, long bindPaid, IReadOnlyList<NaturalAbyssPayment> payments) =
			(ledger.ExperienceGained, ledger.Fares, ledger.BindPaid, ledger.Payments);
		int inventoryChecks = ledger.InventoryChecks;
		NaturalAltgardBind bind = leg.Bind ?? throw new InvalidDataException($"{leg.Leg} has no bind.");
		NaturalAltgardRewardChoice hauberk = leg.RewardChoiceList.Single(choice => choice.QuestId == scope.CommanderQuestId);
		Require(state.Synchronized && !state.IsDead && state.MapId == map, $"the Cleric is on map {state.MapId}, not {map}");
		Require(NaturalAltgardDecisionEngine.BoundAt(bind, leg.Hub.MapId, state.Bind), $"the bind is {state.Bind}, not obelisk {bind.NpcId}");
		Require(paid.All(state.CompletedQuestIds.Contains), $"not all of Q{string.Join(", Q", paid)} are complete");
		Require(payments.Select(payment => payment.QuestId).SequenceEqual(paid), $"the turn-ins so far are not Q{string.Join(", Q", paid)}, in order");
		Require(payments.All(payment => payment.Experience == leg.Quest(payment.QuestId).RewardExperience),
			"a turn-in paid " + string.Join(", ", payments.Select(payment => $"Q{payment.QuestId} {payment.Experience} XP")) + ", not its shipped XP");
		// AX-12b: a death on the ring course takes XP and the soul healing after its obelisk revive gives the recoverable part back.
		NaturalSoulHeal[] soulHeals = [.. ledger.SoulHeals ?? []];
		Require(experienceGained == payments.Sum(payment => payment.Experience) + ledger.ArenaExperience + ledger.CourseExperience,
			$"{experienceGained} XP was gained; the turn-ins paid {payments.Sum(payment => payment.Experience)}, the arena changed {ledger.ArenaExperience} " +
			$"and the course's deaths {ledger.CourseExperience}");
		Require(ledger.Deaths > 0 || ledger.CourseExperience == 0, $"the course changed {ledger.CourseExperience} XP without a death");
		Require(ledger.ObeliskRevives >= 0 && ledger.ObeliskRevives <= ledger.Deaths && soulHeals.Length == ledger.ObeliskRevives,
			$"{soulHeals.Length} soul healings followed {ledger.ObeliskRevives} obelisk revives");
		Require(soulHeals.All(heal => heal.HealerNpcId == scope.SoulHealer.NpcId && heal.Recovered >= 0 &&
			heal.Price == NaturalServicePolicy.SoulHealPrice(heal.Recovered) && heal.KinahBefore - heal.KinahAfter == heal.Price),
			"a soul healing was not Golenthor's, or its price is not the shipped formula's");
		NaturalAbyssAttempt[] tries = ledger.Attempts.Where(attempt => attempt.Kind == NaturalAbyssAttempts.Arena).ToArray();
		if (cleared)
		{
			// The arena: reported to Garm; one try in order per attempt, the last one the clear; no more than the allowed tries.
			Require(returned || state.Quests.GetValueOrDefault(scope.Arena.QuestId) is { Status: 4 }, $"Q{scope.Arena.QuestId} is not at its reward");
			Require(tries.Length is >= 1 && tries.Length <= scope.Arena.MaxAttempts && tries.Select(attempt => attempt.Number).SequenceEqual(Enumerable.Range(1, tries.Length)),
				$"{tries.Length} arena tries are recorded");
			Require(tries[^1] is { Outcome: NaturalAbyssAttempts.Done } won && won.Progress >= scope.Arena.RequiredKills &&
				tries[..^1].All(attempt => attempt.Outcome != NaturalAbyssAttempts.Done), "the last arena try is not the one clear");
			Require(tries.All(attempt => attempt.EndedMillis - attempt.StartedMillis <= (scope.Arena.Seconds + 30) * 1000L), "an arena try outlasted its timer");
			// Every counted kill pays its spirit's XP, and a death takes some. With no death the arena's XP is exactly some mix
			// of Mage and Warrior kills that adds up to the kills the tries counted.
			int kills = tries.Sum(attempt => attempt.Progress);
			long[] pays = scope.Arena.Spirits.Select(spirit => (long)spirit.Experience).Order().ToArray();
			Require(ledger.Deaths > 0 || pays is [long low, long high] && Enumerable.Range(0, kills + 1).Any(cheap => cheap * low + (kills - cheap) * high == ledger.ArenaExperience),
				$"the arena paid {ledger.ArenaExperience} XP, which no mix of {kills} kills at {string.Join(" and ", pays)} XP gives");
		}
		if (returned)
		{
			// The return: Q2947's staff taken and worn, the manastone discarded once, Q2042 waiting for Yornduf.
			NaturalAltgardRewardChoice staffChoice = leg.RewardChoiceList.Single(choice => choice.QuestId == scope.Arena.QuestId);
			int waiting = leg.Steps.Single(step => step.Key == scope.RingCourse.StartStep).Var ?? throw new InvalidDataException("The ring course's start step has no var.");
			Require(flown || state.Quests.GetValueOrDefault(scope.RingCourse.QuestId) is { Status: 3 } course && (course.StepAndFlags & 0x3F) == waiting,
				$"Q{scope.RingCourse.QuestId} is not waiting for Yornduf at var {waiting}");
			// AX-12c: a coin staff with more magic boost may be worn over it; the staff rule below checks which staff is worn.
			Require(inventory.Any(item => item.ItemId == staffChoice.ItemId &&
					(settled || item.EquipmentSlot != NotWorn && (item.EquipmentSlot & 1) != 0)),
				$"Q{scope.Arena.QuestId}'s staff {staffChoice.ItemId} is not {(settled ? "owned" : "worn")}");
			Require(discards.Select(item => item.ItemId).SequenceEqual(scope.Inventory.Discard) && discards.All(item => item.Count == 1),
				$"the discarded items are [{string.Join(", ", discards.Select(item => item.ItemId))}], not the one flight-time manastone");
		}
		else
			Require(discards.Length == 0, "an item was discarded before Q2947's reward");
		NaturalAbyssAttempt[] flights = ledger.Attempts.Where(attempt => attempt.Kind == NaturalAbyssAttempts.RingCourse).ToArray();
		NaturalAbyssSupply scrollSupply = scope.Supplies.Single(supply => supply.Family == "flight-speed");
		Require(ledger.ScrollsSupplied <= scrollSupply.MaxCount && ledger.ScrollsUsed <= 1, $"{ledger.ScrollsSupplied} flight-speed scrolls were supplied and {ledger.ScrollsUsed} used");
		if (flown)
		{
			// The ring course: one try in order per attempt, the last one all six rings; no more than the allowed tries. No
			// quest of the leg is left in the journal. Q2042 pays ten scrolls of its own; the supplied one was used.
			Require(flights.Length is >= 1 && flights.Length <= scope.RingCourse.MaxAttempts &&
				flights.Select(attempt => attempt.Number).SequenceEqual(Enumerable.Range(1, flights.Length)), $"{flights.Length} ring-course tries are recorded");
			Require(flights[^1] is { Outcome: NaturalAbyssAttempts.Done } won && won.Progress == scope.RingCourse.Rings.Length &&
				flights[..^1].All(attempt => attempt.Outcome != NaturalAbyssAttempts.Done), "the last ring-course try is not the one flight through all six rings");
			Require(flights.All(attempt => attempt.EndedMillis - attempt.StartedMillis <= (scope.RingCourse.Seconds + 60) * 1000L), "a ring-course try outlasted its timer");
			Require(leg.Order.All(id => state.Quests.GetValueOrDefault(id) is not { Status: 3 or 4 }), "a quest of the leg is still in the journal");
			Require(ledger.ScrollsUsed == 1, "the flight-speed scroll was not used at the course's start");
			// AX-11: the level is reached by the leg's quests (AX-Q6), before any level-26 piece is bought.
			Require(state.Level >= scope.Level.Minimum, $"the Cleric is level {state.Level}, below {scope.Level.Minimum}");
		}
		else
			Require(flights.Length == 0 && ledger.ScrollsSupplied == 0 && ledger.ScrollsUsed == 0, "a ring-course try or a scroll is recorded before Yornduf's talk");
		if (cleared) { }
		else if (capital)
		{
			int taken = leg.Steps.Single(step => step.Key == scope.Arena.StartStep).Var ?? throw new InvalidDataException("The arena's start step has no var.");
			Require(state.Quests.GetValueOrDefault(scope.Arena.QuestId) is { Status: 3 } trial && (trial.StepAndFlags & 0x3F) == taken,
				$"Q{scope.Arena.QuestId} is not waiting for Garm at var {taken}");
			Require(tries.Length == 0 && ledger.ArenaExperience == 0, "an arena try is recorded before Garm's talk");
		}
		else
			Require((leg.Start.StartedQuestIds ?? []).All(id => state.Quests.GetValueOrDefault(id) is { Status: 3, StepAndFlags: 0 }), "an incoming quest moved before its turn");
		long healed = soulHeals.Sum(heal => heal.Price);
		Require(state.Kinah == start.Kinah - fares - bindPaid - healed + payments.Sum(payment => payment.Kinah),
			$"{state.Kinah} Kinah is not {start.Kinah} less {fares} in fares, {bindPaid} for the bind and {healed} for soul healing, plus the quest's pay");
		Require(fares > 0 && bindPaid == bind.Price, $"the fares were {fares} and the bind {bindPaid}");
		NaturalJourneyItem[] torso = inventory.Where(item => item.EquipmentSlot != NotWorn && (item.EquipmentSlot & Torso) != 0).ToArray();
		Require(torso is [{ } worn] && worn.ItemId == hauberk.ItemId, $"the worn torso is [{string.Join(", ", torso.Select(item => item.ItemId))}], not {hauberk.ItemId}");
		NaturalJourneyItem[] hands = inventory.Where(item => item.EquipmentSlot != NotWorn && (item.EquipmentSlot & 1) != 0).ToArray();
		int best = inventory.Select(item => staffMagicBoost(item.ItemId)).DefaultIfEmpty(0).Max();
		Require(hands is [{ } staff] && staffMagicBoost(staff.ItemId) == best && best > 0, "the worn weapon is not the owned staff with the most magic boost");
		Require(scope.Inventory.KeepSealed.All(id => inventory.Count(item => item.ItemId == id) == 1), "the sealed stigma bundle changed");
		Require(inventoryChecks == 1 + payments.Count, $"{inventoryChecks} inventory checks ran for {payments.Count} turn-ins");
		long coins = state.ItemCounts.GetValueOrDefault(scope.CoinArmor.CoinItemId);
		long fromChests = ledger.Opened.Sum(container => container.Gained.GetValueOrDefault(scope.CoinArmor.CoinItemId));
		Require(ledger.CoinPurchases.All(purchase => purchase.KinahAfter == purchase.KinahBefore), "a coin armor piece cost Kinah");
		Require(coins == start.BronzeCoins + ledger.CoinsSupplied + fromChests - ledger.CoinPurchases.Sum(purchase => purchase.Cost),
			$"{coins} Bronze Coins are not the {start.BronzeCoins} brought, plus {ledger.CoinsSupplied} supplied and {fromChests} from chests, " +
			$"less {ledger.CoinPurchases.Sum(purchase => purchase.Cost)} spent");
		Require(ledger.NotOpened == 0 && scope.Inventory.Open.All(id => state.ItemCounts.GetValueOrDefault(id) == 0), "a reward container is still closed");
		Require(ledger.Opened.All(container => scope.Inventory.Open.Contains(container.ItemId) && container.Gained.Count > 0), "an opened container is not the leg's, or gave nothing");
		Require(scope.Inventory.Discard.All(id => state.ItemCounts.GetValueOrDefault(id) == 0), "an item the leg discards is still owned");
		// The coin armor tiers due by now: level 21 after the commander, level 26 at the endpoint. Each has one decided manifest,
		// bought exactly; nothing of it still beats what is worn; and in each slot the piece bought last is the one worn.
		NaturalAbyssCoinTier[] due = [.. scope.CoinArmor.Tiers.Where(tier => tier.When == "after-commander" ? coinArmor : settled)];
		Require(ledger.CoinManifests.Count == due.Length && ledger.CoinPurchases.All(purchase => due.Any(tier => tier.Level == purchase.Level)),
			$"{ledger.CoinManifests.Count} coin armor manifests are decided where {due.Length} are due");
		long shortfall = 0;
		foreach (NaturalAbyssCoinTier tier in due)
		{
			Require(ledger.CoinManifests.Count(manifest => manifest.Level == tier.Level) == 1, $"the {tier.Name} manifest was not decided exactly once");
			NaturalAbyssCoinManifest decided = ledger.CoinManifests.Single(manifest => manifest.Level == tier.Level);
			Require(decided.Buys.Select(slot => slot.CoinItemId).Order().SequenceEqual(
				ledger.CoinPurchases.Where(purchase => purchase.Level == tier.Level).Select(purchase => purchase.ItemId).Order()), $"the {tier.Name} purchases are not the manifest");
			Require(NaturalAbyssCoinArmorPolicy.Plan(scope.CoinArmor, tier, inventory, physicalDefence, staffMagicBoost).Done, $"a {tier.Name} piece still beats what is worn");
			shortfall += decided.CoinsToSupply;
		}
		Require(ledger.CoinsSupplied == shortfall, $"{ledger.CoinsSupplied} coins were supplied where the manifests were {shortfall} short");
		Require(ledger.CoinsSupplied <= scope.Supplies.Single(supply => supply.ItemId == scope.CoinArmor.CoinItemId).MaxCount, $"{ledger.CoinsSupplied} supplied coins exceed the approval");
		Require(ledger.CoinPurchases.GroupBy(purchase => purchase.Slot).Select(slot => slot.Last()).All(purchase => inventory.Any(item =>
			item.ObjectId == purchase.ObjectId && item.ItemId == purchase.ItemId && item.EquipmentSlot != NotWorn && (item.EquipmentSlot & purchase.Slot) != 0)),
			"a bought piece is not worn");
		BotBindPoint bound = state.Bind!;
		return new(leg.Leg, frontier, start.CharacterId, state.MapId!.Value, state.Position, state.Level, experienceGained, state.Kinah, start.Kinah, fares,
			bindPaid, [.. payments], bound.MapId, bound.Position, hands[0].ItemId, torso[0].ItemId,
			leg.Order.Where(state.CompletedQuestIds.Contains).ToArray(),
			state.Quests.Values.Where(quest => quest.Status is 3 or 4).Select(quest => quest.QuestId).Order().ToArray(),
			state.Quests.Values.Where(quest => quest.Status == NaturalAltgardDecisionEngine.Locked).Select(quest => quest.QuestId).Order().ToArray(),
			ledger.InventoryChecks + ledger.OtherInventoryChecks, gameMillis, coins, ledger.CoinsSupplied, [.. ledger.CoinManifests],
			[.. ledger.CoinPurchases], [.. ledger.Opened], [.. ledger.Attempts], ledger.ArenaExperience, ledger.Deaths, discards,
			ledger.ScrollsSupplied, ledger.ScrollsUsed, soulHeals, ledger.ObeliskRevives, ledger.CourseExperience);
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
			.Concat(scope.CoinArmor.Tiers.Where(tier => tier.Staff != null).Select(tier => tier.Staff!.ItemId))
			.Where(id => state.ItemCounts.GetValueOrDefault(id) > 0).ToArray();
		Require(early.Length == 0, $"items this leg earns are already owned: [{string.Join(", ", early)}]");
		Require(state.Kinah >= BaseTravelCost(leg) * 2, $"{state.Kinah} Kinah does not cover the teleports and the bind");
		return new(leg.Leg, true, characterId, state.MapId!.Value, state.Position, state.Level, state.Kinah,
			state.ItemCounts.GetValueOrDefault(scope.CoinArmor.CoinItemId), state.CompletedQuestIds.Count, started, staffs[0].ObjectId,
			inventory.Where(item => scope.ProtectedItemIds.Contains(item.ItemId)).OrderBy(item => item.ItemId).ThenBy(item => item.ObjectId).ToArray(),
			gameMillis);
	}
}
