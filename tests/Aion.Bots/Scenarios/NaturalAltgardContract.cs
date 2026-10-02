using System.Text.Json;

namespace Aion.Bots.Scenarios;

/// <summary>
/// One leg of the Altgard leveling leg (docs/natural-altgard-leveling.md). Leg 1 (<c>l1</c>) is the fortress: Q2201-Q2209
/// and the Q24011 campaign, from the <c>altgard</c> snapshot to <c>altgard-l12</c>. Leg 2 (<c>l2</c>) is Moslan Crossroad:
/// Q2210-Q2215, Q2218-Q2220 and the Q24012 campaign, from <c>altgard-l12</c> to <c>altgard-l2</c>. Leg 3 (<c>l3</c>) is
/// Manir's Campsite: Q2221, the Q2290 escort and Q2222, from <c>altgard-l2</c> to <c>altgard-l3</c> at Basfelt. Leg 4 (<c>l4</c>)
/// is Basfelt Village: fourteen quests with two timers, a spawned EXPERT, custom kill counters, a zone-bound item use, a monster
/// to leave alone and a bind at the hub, from <c>altgard-l3</c> to <c>altgard-l4</c>. Static walkthrough
/// knowledge only; at run time every step is still driven by what the client observes. Each leg's template-quest plans
/// live beside its contract. Sections a leg does not need (Leg 1's flight, remedy and air kills; Leg 2's town, object
/// uses, zone steps, collections and poisons) are left out of its file.
/// </summary>
public sealed record NaturalAltgardContract(
	int SchemaVersion,
	string JavaReference,
	string Leg,
	NaturalAltgardStart Start,
	NaturalAltgardHub Hub,
	NaturalAltgardArea[] Areas,
	NaturalAltgardQuest[] Quests,
	int[] Order,
	NaturalAltgardStep[] Steps,
	NaturalAltgardItemUse? ItemUse,
	NaturalAltgardAirKills? AirKills,
	NaturalAltgardRewardChoice? RewardChoice,
	Dictionary<int, int> UnhandedWorkItemIds,
	NaturalAltgardFlight? Flight,
	NaturalAltgardExclusion[] Excluded,
	NaturalAltgardEndpoint Endpoint,
	NaturalAltgardHub? Town = null,
	NaturalAltgardObjectUse[]? ObjectUses = null,
	NaturalAltgardZoneStep[]? ZoneSteps = null,
	NaturalAltgardCollection[]? Collections = null,
	NaturalAltgardPoison[]? Poisons = null,
	NaturalAltgardEscort[]? Escorts = null,
	NaturalAltgardBind? Bind = null,
	NaturalAltgardHunt[]? Hunts = null,
	NaturalAltgardTimer[]? Timers = null,
	NaturalAltgardSpawn[]? Spawns = null,
	NaturalAltgardAvoid[]? Avoid = null,
	NaturalAltgardRewardChoice[]? RewardChoices = null,
	NaturalAltgardTimedSpawn[]? TimedSpawns = null,
	NaturalAltgardHeld[]? Held = null,
	NaturalAltgardCubeExpansion? CubeExpansion = null,
	NaturalAltgardMapTrip[]? MapTrips = null,
	NaturalAltgardPillarFlight? PillarFlight = null,
	NaturalAltgardInstanceTrip[]? InstanceTrips = null)
{
	/// <summary>The contract file and plan directory of each leg (none when the leg has no template quests).</summary>
	public static readonly IReadOnlyDictionary<string, (string Contract, string? Plans)> Legs = new Dictionary<string, (string, string?)>
	{
		["l1"] = ("natural-altgard-contract.json", "natural-altgard-plans"),
		["l2"] = ("natural-altgard-l2-contract.json", "natural-altgard-l2-plans"),
		["l3"] = ("natural-altgard-l3-contract.json", "natural-altgard-l3-plans"),
		["l4"] = ("natural-altgard-l4-contract.json", "natural-altgard-l4-plans"),
		["l5"] = ("natural-altgard-l5-contract.json", "natural-altgard-l5-plans"),
		["l6"] = ("natural-altgard-l6-contract.json", "natural-altgard-l6-plans"),
		["l7"] = ("natural-altgard-l7-contract.json", "natural-altgard-l7-plans"),
		["l8"] = ("natural-altgard-l8-contract.json", "natural-altgard-l8-plans"),
		["l9"] = ("natural-altgard-l9-contract.json", "natural-altgard-l9-plans"),
		["l10"] = ("natural-altgard-l10-contract.json", "natural-altgard-l10-plans"),
	};

	public NaturalAltgardObjectUse[] ObjectUseList => ObjectUses ?? [];
	public NaturalAltgardZoneStep[] ZoneStepList => ZoneSteps ?? [];
	public NaturalAltgardCollection[] CollectionList => Collections ?? [];
	public NaturalAltgardPoison[] PoisonList => Poisons ?? [];
	public NaturalAltgardEscort[] EscortList => Escorts ?? [];
	public NaturalAltgardHunt[] HuntList => Hunts ?? [];
	public NaturalAltgardTimer[] TimerList => Timers ?? [];
	public NaturalAltgardSpawn[] SpawnList => Spawns ?? [];
	public NaturalAltgardAvoid[] AvoidList => Avoid ?? [];
	public NaturalAltgardTimedSpawn[] TimedSpawnList => TimedSpawns ?? [];
	public NaturalAltgardHeld[] HeldList => Held ?? [];
	public NaturalAltgardMapTrip[] MapTripList => MapTrips ?? [];
	public NaturalAltgardInstanceTrip[] InstanceTripList => InstanceTrips ?? [];
	public int StepMap(NaturalAltgardStep step) => step.MapId ?? Hub.MapId;
	/// <summary>Every chosen reward of the leg: the campaign's (<see cref="RewardChoice"/>) and the others'.</summary>
	public NaturalAltgardRewardChoice[] RewardChoiceList => [.. RewardChoice is { } choice ? [choice] : Array.Empty<NaturalAltgardRewardChoice>(), .. RewardChoices ?? []];

	/// <summary>Leg 1 sections, for code that only runs Leg 1.</summary>
	public NaturalAltgardFlight RequiredFlight => Flight ?? throw new InvalidDataException($"{Leg} has no flight rules.");
	public NaturalAltgardItemUse RequiredItemUse => ItemUse ?? throw new InvalidDataException($"{Leg} has no item use.");
	public NaturalAltgardAirKills RequiredAirKills => AirKills ?? throw new InvalidDataException($"{Leg} has no air kills.");
	/// <summary>Legs 1 and 2 each end a campaign with a chosen reward; Leg 3's quests have none.</summary>
	public NaturalAltgardRewardChoice RequiredRewardChoice => RewardChoice ?? throw new InvalidDataException($"{Leg} has no reward choice.");

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static NaturalAltgardContract Load(string path)
	{
		NaturalAltgardContract contract = JsonSerializer.Deserialize<NaturalAltgardContract>(File.ReadAllText(path), JsonOptions)
			?? throw new InvalidDataException("Empty Natural Altgard contract.");
		if (contract.SchemaVersion != 1)
			throw new InvalidDataException("Unsupported Natural Altgard contract schema.");
		// A leg of template quests only (Leg 5) has no scripted steps.
		if (contract.Steps.Length == 0 && contract.Quests.Any(quest => !quest.IsTemplate) ||
			contract.Steps.Select(step => step.Key).Distinct().Count() != contract.Steps.Length)
			throw new InvalidDataException("Natural Altgard steps are missing or have duplicate keys.");
		int[] questIds = contract.Quests.Select(quest => quest.Id).ToArray();
		if (questIds.Distinct().Count() != questIds.Length || !contract.Order.Order().SequenceEqual(questIds.Order()))
			throw new InvalidDataException("Natural Altgard order must name every contract quest exactly once.");
		if (contract.Steps.Any(step => !questIds.Contains(step.QuestId)) ||
			contract.Endpoint.CompletedQuestIds.Any(id => !questIds.Contains(id)) ||
			contract.Excluded.Any(excluded => questIds.Contains(excluded.Id)))
			throw new InvalidDataException("Natural Altgard steps, endpoint or exclusions disagree with the quest list.");
		string[] areas = contract.Areas.Select(area => area.Key).ToArray();
		if (contract.Steps.Any(step => step.Area != null && !areas.Contains(step.Area)) ||
			contract.Quests.Any(quest => quest.Area != null && !areas.Contains(quest.Area)))
			throw new InvalidDataException("Natural Altgard names an unknown area.");
		foreach (NaturalAltgardStep step in contract.Steps)
		{
			if (contract.StepMap(step) != contract.Hub.MapId && !contract.MapTripList.Any(trip => trip.MapId == contract.StepMap(step)) &&
				!contract.InstanceTripList.Any(trip => trip.MapId == contract.StepMap(step)))
				throw new InvalidDataException($"Natural Altgard step {step.Key} has no trip to its map.");
			if (step.ExpectedStatus is not ("OFFER" or "START" or "REWARD"))
				throw new InvalidDataException($"Natural Altgard step {step.Key} has an unknown status.");
			foreach (string action in step.Actions)
				NaturalAscensionContract.DialogActionId(action);
		}
		foreach (NaturalAltgardRewardChoice choice in contract.RewardChoiceList) NaturalAscensionContract.DialogActionId(choice.Action);
		foreach (string action in contract.ObjectUseList.Select(use => use.CloseAction).OfType<string>())
			NaturalAscensionContract.DialogActionId(action);
		int[] scripted = contract.ObjectUseList.Select(use => use.QuestId).Concat(contract.ZoneStepList.Select(zone => zone.QuestId))
			.Concat(contract.CollectionList.Select(collection => collection.QuestId)).Concat(contract.PoisonList.Select(poison => poison.QuestId))
			.ToArray();
		if (scripted.Any(id => !questIds.Contains(id)) ||
			contract.ObjectUseList.Any(use => use.Area != null && !areas.Contains(use.Area)) ||
			contract.PoisonList.Any(poison => !contract.Steps.Any(step => step.Key == poison.RemovedByStep)))
			throw new InvalidDataException("Natural Altgard object uses, zone steps, collections or poisons disagree with the quests and steps.");
		string[] stepKeys = contract.Steps.Select(step => step.Key).ToArray();
		if (contract.Bind?.BeforeStep is { } before &&
			!contract.Steps.Any(step => step.Key == before && step.ExpectedStatus == "OFFER" && contract.StepMap(step) == contract.Hub.MapId))
			throw new InvalidDataException("Natural Altgard bind prerequisite must name an offer on the hub map.");
		if (contract.EscortList.Select(escort => escort.Key).Distinct().Count() != contract.EscortList.Length ||
			contract.EscortList.Any(escort => !questIds.Contains(escort.QuestId) || !stepKeys.Contains(escort.StartStep) ||
				!stepKeys.Contains(escort.RestartStep) || !areas.Contains(escort.Area) || escort.ClearAreas.Any(area => !areas.Contains(area)) ||
				escort.MaxAttempts < 1 || escort.GoalRadius <= 0 || escort.Leash <= escort.GoalRadius || escort.Goal.Length != 3))
			throw new InvalidDataException("Natural Altgard escorts disagree with the quests, steps or areas, or have no attempts, radius or leash.");
		// AB-01: Leg 4's hunts, timers, spawns and monsters to leave alone.
		if (contract.HuntList.Any(hunt => !questIds.Contains(hunt.QuestId) || !areas.Contains(hunt.Area) || hunt.NpcIds.Length == 0 || hunt.ToVar <= hunt.FromVar) ||
			contract.TimerList.Any(timer => !questIds.Contains(timer.QuestId) || !stepKeys.Contains(timer.StartStep) || timer.Seconds <= 0 ||
				timer.OnExpiry is not ("abandon" or "new-chance") || timer.OnLogout is not "abandon" ||
				timer.OnExpiry == "new-chance" && timer.NewChanceAction == null) ||
			contract.SpawnList.Any(spawn => !questIds.Contains(spawn.QuestId) || !areas.Contains(spawn.Area) || spawn.LifetimeSeconds <= 0 ||
				spawn.Position.Length != 3) ||
			contract.AvoidList.Any(avoid => !questIds.Contains(avoid.QuestId) || !areas.Contains(avoid.Area)) ||
			contract.RewardChoiceList.Any(choice => !questIds.Contains(choice.QuestId)))
			throw new InvalidDataException("Natural Altgard hunts, timers, spawns, avoidances or reward choices disagree with the quests, steps or areas.");
		// AK-01: Leg 5's carriers by the hour, and the hand-ins held for later hubs.
		if (contract.TimedSpawnList.Any(spawn => !questIds.Contains(spawn.QuestId) || !areas.Contains(spawn.Area) || spawn.Position.Length != 3 ||
				spawn.SpawnHour is < 0 or > 23 || spawn.DespawnHour is < 0 or > 23 || spawn.SpawnHour == spawn.DespawnHour) ||
			contract.HeldList.Any(held => !questIds.Contains(held.QuestId)) ||
			contract.HeldList.Any(held => !(contract.Endpoint.HeldQuestIds ?? []).Contains(held.QuestId)))
			throw new InvalidDataException("Natural Altgard timed spawns or held hand-ins disagree with the quests, areas or endpoint.");
		foreach (string action in contract.TimerList.Select(timer => timer.NewChanceAction).OfType<string>())
			NaturalAscensionContract.DialogActionId(action);
		if (contract.PillarFlight is { } pillar && (contract.Flight == null || pillar.Upper.Length != 3 || pillar.Lower.Length != 3 ||
			pillar.Upper[2] - pillar.Lower[2] < 40))
			throw new InvalidDataException("A pillar flight needs flight rules and distinct upper/lower landings.");
		if (contract.InstanceTripList.Any(trip => !questIds.Contains(trip.QuestId) || trip.MapId == contract.Hub.MapId ||
			trip.EnterVar <= trip.FromVar || trip.ResetVar != trip.FromVar || trip.KillVar <= trip.SpawnVar ||
			trip.PortalPosition.Length != 3 || trip.Arrival.Length != 3 || trip.ExitPosition.Length != 3 ||
			trip.BossPosition.Length != 3 || trip.MovieExitPosition.Length != 3 || trip.UseMillis < 0 || trip.ExitUseMillis < 0))
			throw new InvalidDataException("Natural Altgard instance trips disagree with the quests, maps or transitions.");
		int[] otherMaps = contract.HuntList.Select(hunt => hunt.MapId).Concat(contract.ObjectUseList.Select(use => use.MapId)).OfType<int>().ToArray();
		if (otherMaps.Any(map => map != contract.Hub.MapId && !contract.InstanceTripList.Any(trip => trip.MapId == map)))
			throw new InvalidDataException("A hunt or object use has no instance trip to its map.");
		return contract;
	}

	/// <summary>Leg 1, the fortress.</summary>
	public static NaturalAltgardContract LoadDefault() => LoadLeg("l1");

	public static NaturalAltgardContract LoadLeg(string leg) =>
		Load(Path.Combine(RepoRoot(), "parity-artifacts/e2e", Legs.TryGetValue(leg, out var files) ? files.Contract
			: throw new ArgumentException($"Unknown Altgard leg '{leg}'.", nameof(leg))));

	/// <summary>Leg 1's compiled template-quest plans (Q2201-Q2206), keyed by quest id.</summary>
	public static IReadOnlyDictionary<int, QuestRunPlan> LoadPlans() => LoadPlans("l1");

	/// <summary>A leg's compiled template-quest plans, keyed by quest id.</summary>
	public static IReadOnlyDictionary<int, QuestRunPlan> LoadPlans(string leg) => Legs[leg].Plans is { } plans
		? QuestRunPlan.LoadDirectory(Path.Combine(RepoRoot(), "parity-artifacts/e2e", plans)).ToDictionary(plan => plan.Id)
		: new Dictionary<int, QuestRunPlan>();

	public NaturalAltgardQuest Quest(int questId) => Quests.Single(quest => quest.Id == questId);

	/// <summary>Every NPC the leg walks to, talks to, uses or hunts: the navigation graph's waypoints for Altgard.</summary>
	public int[] GraphNpcIds(IReadOnlyDictionary<int, QuestRunPlan> plans) =>
		Quests.Select(quest => quest.StartNpcId).OfType<int>()
			.Concat(Steps.Select(step => step.NpcId))
			.Concat(plans.Values.SelectMany(plan => plan.StartNpcs.Concat(plan.EndNpcs)
				.Concat(plan.Steps.SelectMany(step => step.Npcs.Concat(step.Sources.Select(source => source.Npc).OfType<QuestRunNpc>())))
				.Select(npc => npc.Id)))
			.Concat(ObjectUseList.Select(use => use.NpcId))
			.Concat(EscortList.SelectMany(escort => new[] { escort.FollowerNpcId, escort.GoalNpcId }))
			.Concat(HuntList.SelectMany(hunt => hunt.NpcIds))
			.Concat(SpawnList.Select(spawn => spawn.TriggerNpcId))
			.Concat(TimedSpawnList.Select(spawn => spawn.NpcId))
			.Concat(CubeExpansion is { } cube ? [cube.TeleporterNpcId] : [])
			.Concat(MapTripList.Select(trip => trip.TeleporterNpcId))
			.Concat(InstanceTripList.SelectMany(trip => new[] { trip.PortalNpcId, trip.ExitNpcId, trip.BossNpcId }))
			.Concat(Bind is { } bind ? [bind.NpcId] : [])
			.Concat(CollectionList.SelectMany(collection => collection.Items.SelectMany(item => item.SourceNpcIds)))
			.Append(Start.BindNpcId).Concat(AirKills is { } air ? [air.NpcId] : [])
			.Distinct().Order().ToArray();

	public NaturalAltgardArea Area(string key) => Areas.Single(area => area.Key == key);

	public IEnumerable<NaturalAltgardStep> StepsFor(int questId) => Steps.Where(step => step.QuestId == questId);

	private static string RepoRoot() =>
		Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
}

public sealed record NaturalAltgardStart(string Contract, string Snapshot, int MapId, string Race, string Class, int Level,
	int BindNpcId, int[] CompletedQuestIds, int[] LockedQuestIds, int[]? StartedQuestIds = null);

/// <param name="VendorNpcId">AK-08: a town's merchant, where the Cleric sells what the inventory policy calls surplus.</param>
public sealed record NaturalAltgardHub(string Key, int MapId, float[] Anchor, float Radius, int? VendorNpcId = null);

public sealed record NaturalAltgardArea(string Key, string Note, float[] Min, float[] Max)
{
	public bool Contains(float x, float y, float z) =>
		x >= Min[0] && x <= Max[0] && y >= Min[1] && y <= Max[1] && z >= Min[2] && z <= Max[2];
}

public sealed record NaturalAltgardQuest(int Id, string Category, int MinimumLevel, int RewardExperience, int? Prerequisite,
	string Handler, string? Template, int? StartNpcId, string? Area, int[]? Prerequisites = null)
{
	public bool IsTemplate => Handler == "template";
	public IEnumerable<int> PrerequisiteList => (Prerequisite is int before ? new[] { before } : []).Concat(Prerequisites ?? []).Distinct();
}

/// <summary>One scripted dialog step. <c>OFFER</c> is a quest the player has not taken yet.</summary>
public sealed record NaturalAltgardStep(string Key, int QuestId, int? Var, string? Status, int NpcId, float[] Position,
	int TalkRange, string[] Actions, int[] Pages, int? MovieId, int? ReceivesItemId, string? Area, bool Flight, string? Correction,
	int? MapId = null)
{
	public string ExpectedStatus => Status ?? "START";
}

/// <summary>AE-00: a quest trip from the hub map by teleporter; Return brings the Cleric back to its hub bind.</summary>
public sealed record NaturalAltgardMapTrip(int MapId, int TeleporterNpcId, int LocationId, int Fare, float TalkRange);

/// <summary>BC-01: an ordinary solo quest portal, its instance branch and recovery/exit facts from Java and shipped data.</summary>
public sealed record NaturalAltgardInstanceTrip(int QuestId, int MapId, int FromVar, int EnterVar, int PortalNpcId,
	float[] PortalPosition, int UseMillis, float[] Arrival, int ExitNpcId, float[] ExitPosition, int ExitUseMillis, int ResetVar,
	int BossNpcId, float[] BossPosition, int SpawnVar, int KillVar, int MovieId, float[] MovieExitPosition);

/// <param name="Zone">AB-01: where the item must be used when not <paramref name="Anywhere"/> (Q24013's poison), and the
/// monsters the use spawns (Java <c>onItemUseEvent</c>: two Feral Black Claw Sharpeyes).</param>
/// <param name="ZonePolygon">AB-08: the zone's points and height band from <c>zones_220030000.xml</c>, so the bot can tell when it
/// has stepped inside.</param>
public sealed record NaturalAltgardItemUse(int QuestId, int ItemId, int Var, int NextVar, int UseMillis, bool Anywhere,
	string? Zone = null, int? SpawnsNpcId = null, int SpawnCount = 0, float[]? ZoneAnchor = null, float[][]? ZonePolygon = null,
	float[]? ZoneBand = null)
{
	/// <summary>Whether a point lies inside the zone, at least <paramref name="margin"/> metres from every edge.</summary>
	public bool InZone(float x, float y, float z, float margin = 0)
	{
		if (ZonePolygon is not { Length: >= 3 } polygon || ZoneBand is not [float bottom, float top] || z < bottom || z > top) return false;
		bool inside = false;
		for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
		{
			float[] a = polygon[i], b = polygon[j];
			if ((a[1] > y) != (b[1] > y) && x < (b[0] - a[0]) * (y - a[1]) / (b[1] - a[1]) + a[0]) inside = !inside;
			float dx = b[0] - a[0], dy = b[1] - a[1];
			float t = Math.Clamp(((x - a[0]) * dx + (y - a[1]) * dy) / (dx * dx + dy * dy), 0, 1);
			if (MathF.Sqrt(MathF.Pow(a[0] + t * dx - x, 2) + MathF.Pow(a[1] + t * dy - y, 2)) < margin) return false;
		}
		return inside;
	}
}

public sealed record NaturalAltgardAirKills(int QuestId, int NpcId, int SpawnCount, int FromVar, int RewardVar, int KillsAfterBorender,
	string Note);

public sealed record NaturalAltgardRewardChoice(int QuestId, string Action, int ItemId, string ItemGroup);

public sealed record NaturalAltgardFlightZone(string Name, float Bottom, float Top);

public sealed record NaturalAltgardFlight(NaturalAltgardFlightZone[] Zones, float WaterLevel, int ReuseMillis, int MaxFlightTime);

/// <summary>AH-01's checked upper/lower landings; land outside the pillar structure before walking below it.</summary>
public sealed record NaturalAltgardPillarFlight(float[] Upper, float[] Lower)
{
	public bool IsUpper(float z) => z >= Upper[2] - 20;
}

public sealed record NaturalAltgardExclusion(int Id, string Reason, int Leg);

/// <param name="Anchor">Where the leg ends when that is not its hub (Leg 2: Manir's Campsite), within <paramref name="Radius"/>.</param>
/// <param name="BindNpcId">AB-01: the obelisk the character must be bound at when the leg ends (the standing bind policy).</param>
/// <param name="HeldQuestIds">AK-01: quests whose work is done but whose hand-in waits for a later leg's hub (AK-Q2).</param>
public sealed record NaturalAltgardEndpoint(int MapId, int[] CompletedQuestIds, bool InHub, bool Alive, int MinimumLevel, string Snapshot,
	float[]? Anchor = null, float Radius = 0, int? BindNpcId = null, int[]? HeldQuestIds = null);

/// <summary>AB-01, the standing bind policy (AB-Q5): bind at the obelisk of the hub the leg works out of, on arrival.</summary>
public sealed record NaturalAltgardBind(int NpcId, float[] Position, bool OnArrival, int Price, float AcceptRange, string? BeforeStep = null);

/// <summary>AB-01: a custom handler's kill counter: each kill of <paramref name="NpcIds"/> moves the var from
/// <paramref name="FromVar"/> toward <paramref name="ToVar"/> (Q2288, Q2289, Q24112, Q24013).</summary>
public sealed record NaturalAltgardHunt(int QuestId, int[] NpcIds, int FromVar, int ToVar, string Area, string? Note = null, int? MapId = null);

/// <summary>AB-01: a timed quest. The server's QUEST_TIMER starts at <paramref name="StartStep"/>; at its end the quest is
/// abandoned or, for a new chance, the next check takes <paramref name="LostItemIds"/> (page <paramref name="ExpiredPage"/>)
/// and <paramref name="NewChanceAction"/> starts another. A logout abandons either.</summary>
public sealed record NaturalAltgardTimer(int QuestId, int Seconds, string StartStep, string OnExpiry, string OnLogout, int[] LostItemIds,
	string? NewChanceAction = null, int? ExpiredPage = null);

/// <summary>AB-01: a monster a quest spawns: using <paramref name="TriggerNpcId"/> with <paramref name="RequiresItemId"/> at
/// <paramref name="AtVar"/> plays <paramref name="MovieId"/>, whose end spawns <paramref name="NpcId"/> at
/// <paramref name="Position"/> for <paramref name="LifetimeSeconds"/> (Q2223's Infernus). The used trigger respawns after
/// <paramref name="TriggerRespawnSeconds"/>; <paramref name="RefillNpcId"/> gives a new item (page <paramref name="RefillPage"/>).</summary>
/// <param name="MovieId">The movie the trigger plays, when it plays one (Q2223's 67; Q2252 has none).</param>
/// <param name="AlternateNpcIds">AG-01: what may appear instead of <paramref name="NpcId"/> (Q2252: Minushan's Spirit at 95%,
/// else Minushan Drakie).</param>
public sealed record NaturalAltgardSpawn(string Key, int QuestId, int AtVar, int TriggerNpcId, int RequiresItemId, int? MovieId, int NpcId,
	float[] Position, int LifetimeSeconds, int TriggerRespawnSeconds, int RefillPage, int RefillNpcId, string Area, int[]? AlternateNpcIds = null);

/// <summary>AK-01: a named monster that exists only in its game hours (Java <c>temporary_spawn</c>), from
/// <paramref name="SpawnHour"/> to <paramref name="DespawnHour"/> (wrapping past midnight when the first is larger), and drops
/// <paramref name="ItemId"/> for <paramref name="QuestId"/> (Q2292's ring carriers).</summary>
public sealed record NaturalAltgardTimedSpawn(int QuestId, int ItemId, int NpcId, int SpawnHour, int DespawnHour, float[] Position,
	int RespawnSeconds, string Area)
{
	/// <summary>Java TemporarySpawn.checkHour: present from the spawn hour up to (not including) the despawn hour.</summary>
	public bool PresentAt(int hour) => SpawnHour < DespawnHour ? hour >= SpawnHour && hour < DespawnHour : hour >= SpawnHour || hour < DespawnHour;
}

/// <summary>AK-01, AK-Q2: a quest finished in this leg but handed in at <paramref name="EndNpcId"/> in a later leg's hub.</summary>
public sealed record NaturalAltgardHeld(int QuestId, int EndNpcId, string Hub);

/// <summary>AK-Q4 (a): buy the NPC cube expansions before the leg's work. The Cleric walks to the teleporter, travels to the
/// expander's map, buys each level with its own kinah (Java CubeExpandService.expandCube: EXTEND_INVENTORY, then the
/// STR_WAREHOUSE_EXPAND_WARNING question), and casts Return to its bind.</summary>
/// <param name="Prices">The kinah for each NPC expansion level from 1 (cube_expander.xml for the expander).</param>
public sealed record NaturalAltgardCubeExpansion(int Levels, long[] Prices, int TeleporterNpcId, float TeleporterTalkRange,
	int LocationId, long Fare, int MapId, int ExpanderNpcId, float[] ExpanderPosition);

/// <summary>AB-01, AB-Q3: a monster left alone until a quest reaches <paramref name="UntilVar"/> (Komu Silverclaw: his horn drops
/// only then, and he respawns after <paramref name="RespawnSeconds"/>).</summary>
public sealed record NaturalAltgardAvoid(int NpcId, int QuestId, int UntilVar, int RespawnSeconds, string Area);

/// <summary>A quest object used by hand: a loot (Q2213's Okaru Tree) or a step per use (Q24012's MuMu Carts, each gone once
/// used; <paramref name="SpawnCount"/> spawns respawn after <paramref name="RespawnSeconds"/>).</summary>
/// <param name="DialogPage">A dialog the use opens before the loot (Q2221's safe: page 1693), closed with
/// <paramref name="CloseAction"/>.</param>
public sealed record NaturalAltgardObjectUse(string Key, int QuestId, int NpcId, int FromVar, int ToVar, int Uses, int? LootItemId,
	bool Disappears, string? Area, int SpawnCount = 1, int RespawnSeconds = 0, int? DialogPage = null, string? CloseAction = null,
	int? MapId = null, int? MovieId = null);

/// <summary>
/// AC-01: an escort (docs/natural-altgard-leveling.md, "The escort handler"). The start step's last action makes the follower
/// follow the player (Java <c>defaultStartFollowEvent</c>: var <paramref name="FollowVar"/>); <c>FollowingNpcCheckTask</c> then
/// checks every <paramref name="CheckMillis"/> ms, failing on a death or beyond <paramref name="Leash"/> m apart (var back to
/// <paramref name="LostVar"/>) and succeeding once the follower is within <paramref name="GoalRadius"/> m of the goal npc's
/// first spawn (var <paramref name="SuccessVar"/>, <paramref name="MovieId"/>). Either end deletes the follower, which respawns
/// after <paramref name="FollowerRespawnSeconds"/>; at <paramref name="LostVar"/> the restart step starts the follow again.
/// A logout while following also sets <paramref name="LostVar"/>. <paramref name="ClearAreas"/> are cleared before each start
/// (AC-Q3), and <paramref name="MaxAttempts"/> bounds the tries (AC-Q2).
/// </summary>
/// <param name="StartVar">AG-01: the var at which the escort starts, when not at the offer (Q2284: var 1, after the talk to the
/// first disguised Germir); null for an escort started by the offer itself (Q2290).</param>
/// <param name="FollowerSpawnHour">AG-01: the follower exists only from this game hour (Java <c>temporary_spawn</c>; Q2284's
/// second disguised Germir, 04:00) to <paramref name="FollowerDespawnHour"/> (21:00).</param>
public sealed record NaturalAltgardEscort(string Key, int QuestId, int FollowerNpcId, int GoalNpcId, float[] Goal, float GoalRadius,
	string StartStep, string RestartStep, int FollowVar, int SuccessVar, int LostVar, float Leash, int CheckMillis, int? MovieId,
	int FollowerRespawnSeconds, float FollowerRunSpeed, string[] ClearAreas, int MaxAttempts, string Area, int? StartVar = null,
	int? FollowerSpawnHour = null, int? FollowerDespawnHour = null);

/// <summary>A quest step the server takes when the player enters a zone.</summary>
public sealed record NaturalAltgardZoneStep(int QuestId, string Zone, int FromVar, int ToVar, float[]? Anchor = null, float Radius = 0);

public sealed record NaturalAltgardCollectedItem(int ItemId, int Count, int[] SourceNpcIds);

/// <summary>Items that only drop once the quest reaches <paramref name="AtVar"/> (quest_drop collecting_step).</summary>
public sealed record NaturalAltgardCollection(int QuestId, int AtVar, NaturalAltgardCollectedItem[] Items);

/// <summary>A poison a quest puts on the player, and the contract step that removes it.</summary>
public sealed record NaturalAltgardPoison(int QuestId, int SkillId, int Damage, int TickMillis, int DurationMillis, int SpeedDown,
	bool Dispellable, string RemovedByStep);
