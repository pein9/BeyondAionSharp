using System.Text.Json;
using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>HM-01: shipped Haramel facts and an ordered walkthrough. Tasks are reconciled with
/// fresh client journals/items, never replayed from a saved cursor.</summary>
public sealed record NaturalHaramel(int MapId, int CooldownId, int MaxEntries, int ResetHour,
	int EmptyExpiryMillis, int CleanupPeriodMillis, int AnchorNpcId, int PortalNpcId, float[] PortalPosition,
	float[] Arrival, int EntryExitNpcId, int BossExitNpcId, float[] ExitArrival,
	int LiftNpcId, int LiftDialog, float[] LiftArrival, int BossNpcId, int BossMovieId, int ChestNpcId,
	int WorkingBindNpcId, float[] WorkingBindPosition, int StaffItemId, int StaffObjectId,
	int IronItemId, int IronCount, int BronzeItemId, int BronzeCount, int[] ProtectedItemIds,
	int[] CleanupItemIds, int[] KillNpcIds, int KillSpawnCount, NaturalHaramelTask[] FirstVisit,
	NaturalHaramelTask[] BetweenVisits, NaturalHaramelTask[] SecondVisit, NaturalHaramelTask[] Finish)
{
	public int[] GraphNpcIds => [AnchorNpcId, PortalNpcId, EntryExitNpcId, BossExitNpcId, LiftNpcId,
		BossNpcId, ChestNpcId, WorkingBindNpcId, 700950, 700953, 700954, 730359];
	public static bool CanUpgradeGroup(string? group) => group is "CH_TORSO" or "CH_GLOVE" or "CH_SHOULDER" or
		"CH_PANTS" or "CH_SHOES" or "RING" or "EARRING" or "NECKLACE" or "BELT";

	public void Validate(NaturalAltgardContract leg)
	{
		if (leg.Leg != "l12" || MapId != 300200000 || CooldownId != 46 || MaxEntries != 16 || ResetHour != 9 ||
			EmptyExpiryMillis != 600000 || CleanupPeriodMillis != 60000 || KillSpawnCount != 74 ||
			StaffItemId != 101501357 || StaffObjectId != 137763 || IronCount != 19 || BronzeCount != 7 ||
			WorkingBindNpcId != 700067 || leg.Start.Snapshot != "altgard-coingear" || leg.Start.CompletedQuestIds.Length != 145 ||
			leg.Endpoint.Snapshot != "altgard-l12" || leg.Endpoint.BindNpcId != 700065 ||
			!leg.Order.Order().SequenceEqual(new[] { 28500, 28501, 28503, 28504, 28505, 28506, 28507, 28508, 28509, 28510, 28511 }))
			throw new InvalidDataException("Haramel contract disagrees with the approved incoming state, two visits or shipped rules.");
		foreach (NaturalHaramelTask task in FirstVisit.Concat(BetweenVisits).Concat(SecondVisit).Concat(Finish))
		{
			if (task.QuestId is int id && !leg.Order.Contains(id) || task.StepKey != null && !leg.Steps.Any(s => s.Key == task.StepKey) ||
				task.MapId != leg.Hub.MapId && task.MapId != MapId ||
				task.Action is not ("talk" or "template-accept" or "template-work" or "template-claim" or
					"haramel-carts" or "haramel-ginseng" or "haramel-object" or "haramel-soup" or "haramel-movie" or "haramel-boss" or "haramel-loot" or "leave-haramel"))
				throw new InvalidDataException($"Invalid Haramel task {task}.");
		}
		if (!ProtectedItemIds.Contains(188053787) || !ProtectedItemIds.Contains(111101650) ||
			new[] { 111501065, 112501015, 113501074, StaffItemId, IronItemId, BronzeItemId }.Any(id => !ProtectedItemIds.Contains(id)))
			throw new InvalidDataException("Haramel must retain the staff, coin pieces, cloth gloves, currencies and sealed bundle.");
	}
}

public sealed record NaturalHaramelTask(string Action, int MapId, int? QuestId = null, string? StepKey = null);
public sealed record NaturalHaramelEquipment(int ObjectId, int ItemId, long Slot);
/// <summary>SM_CHANNEL_INFO sends instanceId minus one, including on solo maps. IDs may repeat
/// after server restart, and IDFactory reuses NPC IDs. Pair the packet ID/anchor with actual entry
/// use and live-spawn evidence; cooldown ID 46 identifies the map, never the copy.</summary>
public sealed record NaturalHaramelVisit(int InstanceId, int AnchorObjectId, int EntriesUsed, long EnteredAtMillis,
	bool BossMovieObserved = false, bool ChestResolved = false, long? LeftAtMillis = null, bool PostBossQuests = false,
	bool FreshSpawnsObserved = false, long ResetAtMillis = 0);
public sealed record NaturalHaramelSoupPayment(int CauldronObjectId, long AtMillis, long BeforeGinseng, long AfterGinseng, int Page);

/// <summary>Retained bot receipts and budgets only. This does not grant items, change quest vars or
/// restore an instance. After a lost instance, fresh client counters/items determine remaining work.</summary>
public sealed record NaturalHaramelProgress(int CharacterId, long StartedAtMillis, long LastObservedAtMillis,
	int Revives, NaturalHaramelVisit[] Visits, long? FreshEntryAfterMillis,
	NaturalHaramelEquipment[] IncomingEquipment, NaturalJourneyProgressState? StallBudget = null,
	NaturalHaramelSoupPayment? SoupPayment = null, bool NeedsInstanceObservation = false, BotInstanceEntry? EntryObservation = null)
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
	public int RecoveryVisits => Visits.Length - (Visits.Any(v => !v.PostBossQuests) ? 1 : 0) - (Visits.Any(v => v.PostBossQuests) ? 1 : 0);
	public NaturalHaramelVisit? CurrentVisit => Visits.LastOrDefault();

	public static NaturalHaramelProgress Begin(int characterId, long now, BotWorldModel world, NaturalHaramel rules)
	{
		RequireLoadout(world, rules);
		if (world.SelfObjectId != characterId || !world.LoginStateObserved || world.Level < 24 ||
			world.Inventory.Values.Where(i => i.ItemId == rules.IronItemId).Sum(i => i.Count) != 19 ||
			world.Inventory.Values.Where(i => i.ItemId == rules.BronzeItemId).Sum(i => i.Count) != 0 ||
			new[] { (111501065,16L), (112501015,2048L), (113501074,4096L) }.Any(p =>
				!world.Inventory.Values.Any(i => i.ItemId == p.Item1 && i.Details.EquippedSlot == p.Item2)))
			throw new InvalidDataException("Haramel starts from the level-24 CG endpoint with all three purchases equipped and 19 Iron.");
		return new(characterId, now, now, 0, [], null, world.Inventory.Values
			.Where(i => i.Details.EquippedSlot.GetValueOrDefault() != 0)
			.Select(i => new NaturalHaramelEquipment(i.ObjectId, i.ItemId, i.Details.EquippedSlot!.Value)).ToArray());
	}

	public NaturalHaramelProgress ObserveEntry(int instanceId, int anchorObjectId, BotInstanceEntry entry, long now,
		bool postBossQuests = false, bool freshSpawnsObserved = false)
	{
		RequireClock(now);
		if (instanceId <= 0 || anchorObjectId <= 0 || entry.PlayerId != CharacterId || entry.CooldownId != 46 || entry.MaxEntries != 16 || entry.EntriesUsed is < 0 or > 16)
			throw new InvalidDataException("Haramel entry needs actual channel/instance, Moorilerk and self entry-count observations.");
		if (CurrentVisit is { } previous && previous.InstanceId == instanceId && previous.AnchorObjectId == anchorObjectId && !freshSpawnsObserved)
		{
			if (previous.EntriesUsed != entry.EntriesUsed && now < previous.ResetAtMillis)
				throw new InvalidDataException("Same-instance re-entry changed the entry count.");
			return this with { LastObservedAtMillis = now, Visits = [.. Visits[..^1], previous with { LeftAtMillis = null }], NeedsInstanceObservation = false, EntryObservation = entry };
		}
		if (entry.EntriesUsed == 0 || CurrentVisit is { } old && (!freshSpawnsObserved ||
			entry.EntriesUsed <= old.EntriesUsed && now < old.ResetAtMillis && !NeedsInstanceObservation))
			throw new InvalidDataException("A fresh copy requires observed live spawns and actual increased entry use, daily reset or cold lost-copy evidence.");
		return this with { LastObservedAtMillis = now, Visits = [.. Visits, new(instanceId, anchorObjectId, entry.EntriesUsed, now,
			PostBossQuests: postBossQuests, FreshSpawnsObserved: freshSpawnsObserved, ResetAtMillis: checked(now + Math.Max(0,entry.ReuseSeconds) * 1000L))],
			FreshEntryAfterMillis = null, NeedsInstanceObservation = false, EntryObservation = entry };
	}

	/// <summary>Q28511 CHECK_USER_HAS_QUEST_ITEM consumes ginseng but leaves var zero. Persist
	/// its actual success page/payment before SETPRO2, so a reconnect cannot pay or give soup twice.</summary>
	public NaturalHaramelProgress ObserveSoupPayment(int cauldronObjectId, long before, long after, int page, long now)
	{
		RequireClock(now);
		if (cauldronObjectId <= 0 || before - after != 5 || after < 0 || page != 1352 || SoupPayment != null)
			throw new InvalidDataException("Soup payment needs one actual five-ginseng consumption and success page.");
		return this with { SoupPayment = new(cauldronObjectId, now, before, after, page), LastObservedAtMillis = now };
	}

	public NaturalHaramelProgress ObserveExit(long now, NaturalHaramel rules)
	{
		RequireClock(now);
		if (CurrentVisit is not { } visit) throw new InvalidDataException("No observed Haramel visit to leave.");
		long left = visit.LeftAtMillis ?? now;
		return this with { LastObservedAtMillis = now, Visits = [.. Visits[..^1], visit with { LeftAtMillis = left }],
			FreshEntryAfterMillis = checked(left + rules.EmptyExpiryMillis + rules.CleanupPeriodMillis) };
	}

	public NaturalHaramelProgress ObserveBoss(bool movie, bool chestResolved, long now)
	{
		RequireClock(now);
		if (CurrentVisit is not { } visit) throw new InvalidDataException("No observed Haramel visit for boss receipts.");
		return this with { LastObservedAtMillis = now, Visits = [.. Visits[..^1], visit with
			{ BossMovieObserved = visit.BossMovieObserved || movie, ChestResolved = visit.ChestResolved || chestResolved }] };
	}

	public NaturalHaramelProgress ObserveRevive(long now)
	{
		RequireClock(now);
		if (Revives >= 20) throw new InvalidDataException("Haramel exhausted the journey's twenty-revive budget.");
		return this with { Revives = Revives + 1, LastObservedAtMillis = now };
	}

	public void Write(string path)
	{
		File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this, JsonOptions));
		File.Move(path + ".tmp", path, overwrite: true);
	}
	public static NaturalHaramelProgress Read(string path, int characterId, long now, BotWorldModel fresh, NaturalAltgardContract leg)
	{
		NaturalHaramelProgress saved = JsonSerializer.Deserialize<NaturalHaramelProgress>(File.ReadAllText(path), JsonOptions)
			?? throw new InvalidDataException("Empty Haramel checkpoint.");
		saved.RequireClock(now);
		if (saved.CharacterId != characterId || fresh.SelfObjectId != characterId || !fresh.LoginStateObserved ||
			saved.Revives is < 0 or > 20 || saved.Visits.Any(v => v.InstanceId <= 0 || v.AnchorObjectId <= 0 || v.EntriesUsed is < 0 or > 16 ||
				v.EnteredAtMillis < saved.StartedAtMillis || v.EnteredAtMillis > saved.LastObservedAtMillis) ||
			leg.Start.CompletedQuestIds.Any(id => !fresh.CompletedQuestIds.Contains(id)))
			throw new InvalidDataException("Haramel checkpoint identity, budget or incoming journal is inconsistent.");
		RequireLoadout(fresh, leg.Haramel!);
		return saved with { NeedsInstanceObservation = fresh.MapId == leg.Haramel!.MapId }; // Nothing is replayed.
	}

	private void RequireClock(long now)
	{
		if (now < LastObservedAtMillis || LastObservedAtMillis < StartedAtMillis)
			throw new InvalidDataException("Haramel recovery cannot rewind the original clock/budget.");
	}
	private static void RequireLoadout(BotWorldModel world, NaturalHaramel rules)
	{
		if (!world.Inventory.TryGetValue(rules.StaffObjectId, out BotInventoryItem? staff) || staff.ItemId != rules.StaffItemId ||
			staff.Details.EquippedSlot != 3 || rules.ProtectedItemIds.Where(id => id is not (186000006 or 186000007))
			.Any(id => !world.Inventory.Values.Any(i => i.ItemId == id)) ||
			world.Inventory.Values.Where(i => i.ItemId == 188053787).Sum(i => i.Count) != 1 || world.Skills.ContainsKey(11504))
			throw new InvalidDataException("Haramel lost the retained staff/gear or sealed stigma bundle.");
	}
}
