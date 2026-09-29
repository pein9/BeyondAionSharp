using System.Text.Json;

namespace Aion.Bots.Scenarios;

/// <summary>
/// One leg of the Altgard leveling leg (docs/natural-altgard-leveling.md). Leg 1 (<c>l1</c>) is the fortress: Q2201-Q2209
/// and the Q24011 campaign, from the <c>altgard</c> snapshot to <c>altgard-l12</c>. Leg 2 (<c>l2</c>) is Moslan Crossroad:
/// Q2210-Q2215, Q2218-Q2220 and the Q24012 campaign, from <c>altgard-l12</c> to <c>altgard-l2</c>. Static walkthrough
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
	NaturalAltgardRewardChoice RewardChoice,
	Dictionary<int, int> UnhandedWorkItemIds,
	NaturalAltgardFlight? Flight,
	NaturalAltgardExclusion[] Excluded,
	NaturalAltgardEndpoint Endpoint,
	NaturalAltgardHub? Town = null,
	NaturalAltgardObjectUse[]? ObjectUses = null,
	NaturalAltgardZoneStep[]? ZoneSteps = null,
	NaturalAltgardCollection[]? Collections = null,
	NaturalAltgardPoison[]? Poisons = null)
{
	/// <summary>The contract file and plan directory of each leg.</summary>
	public static readonly IReadOnlyDictionary<string, (string Contract, string Plans)> Legs = new Dictionary<string, (string, string)>
	{
		["l1"] = ("natural-altgard-contract.json", "natural-altgard-plans"),
		["l2"] = ("natural-altgard-l2-contract.json", "natural-altgard-l2-plans"),
	};

	public NaturalAltgardObjectUse[] ObjectUseList => ObjectUses ?? [];
	public NaturalAltgardZoneStep[] ZoneStepList => ZoneSteps ?? [];
	public NaturalAltgardCollection[] CollectionList => Collections ?? [];
	public NaturalAltgardPoison[] PoisonList => Poisons ?? [];

	/// <summary>Leg 1 sections, for code that only runs Leg 1.</summary>
	public NaturalAltgardFlight RequiredFlight => Flight ?? throw new InvalidDataException($"{Leg} has no flight rules.");
	public NaturalAltgardItemUse RequiredItemUse => ItemUse ?? throw new InvalidDataException($"{Leg} has no item use.");
	public NaturalAltgardAirKills RequiredAirKills => AirKills ?? throw new InvalidDataException($"{Leg} has no air kills.");

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static NaturalAltgardContract Load(string path)
	{
		NaturalAltgardContract contract = JsonSerializer.Deserialize<NaturalAltgardContract>(File.ReadAllText(path), JsonOptions)
			?? throw new InvalidDataException("Empty Natural Altgard contract.");
		if (contract.SchemaVersion != 1)
			throw new InvalidDataException("Unsupported Natural Altgard contract schema.");
		if (contract.Steps.Length == 0 || contract.Steps.Select(step => step.Key).Distinct().Count() != contract.Steps.Length)
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
			if (step.ExpectedStatus is not ("OFFER" or "START" or "REWARD"))
				throw new InvalidDataException($"Natural Altgard step {step.Key} has an unknown status.");
			foreach (string action in step.Actions)
				NaturalAscensionContract.DialogActionId(action);
		}
		NaturalAscensionContract.DialogActionId(contract.RewardChoice.Action);
		int[] scripted = contract.ObjectUseList.Select(use => use.QuestId).Concat(contract.ZoneStepList.Select(zone => zone.QuestId))
			.Concat(contract.CollectionList.Select(collection => collection.QuestId)).Concat(contract.PoisonList.Select(poison => poison.QuestId))
			.ToArray();
		if (scripted.Any(id => !questIds.Contains(id)) ||
			contract.ObjectUseList.Any(use => use.Area != null && !areas.Contains(use.Area)) ||
			contract.PoisonList.Any(poison => !contract.Steps.Any(step => step.Key == poison.RemovedByStep)))
			throw new InvalidDataException("Natural Altgard object uses, zone steps, collections or poisons disagree with the quests and steps.");
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
	public static IReadOnlyDictionary<int, QuestRunPlan> LoadPlans(string leg) =>
		QuestRunPlan.LoadDirectory(Path.Combine(RepoRoot(), "parity-artifacts/e2e", Legs[leg].Plans)).ToDictionary(plan => plan.Id);

	public NaturalAltgardQuest Quest(int questId) => Quests.Single(quest => quest.Id == questId);

	/// <summary>Every NPC the leg walks to, talks to, uses or hunts: the navigation graph's waypoints for Altgard.</summary>
	public int[] GraphNpcIds(IReadOnlyDictionary<int, QuestRunPlan> plans) =>
		Quests.Select(quest => quest.StartNpcId).OfType<int>()
			.Concat(Steps.Select(step => step.NpcId))
			.Concat(plans.Values.SelectMany(plan => plan.StartNpcs.Concat(plan.EndNpcs)
				.Concat(plan.Steps.SelectMany(step => step.Npcs.Concat(step.Sources.Select(source => source.Npc).OfType<QuestRunNpc>())))
				.Select(npc => npc.Id)))
			.Concat(ObjectUseList.Select(use => use.NpcId))
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

public sealed record NaturalAltgardHub(string Key, int MapId, float[] Anchor, float Radius);

public sealed record NaturalAltgardArea(string Key, string Note, float[] Min, float[] Max)
{
	public bool Contains(float x, float y, float z) =>
		x >= Min[0] && x <= Max[0] && y >= Min[1] && y <= Max[1] && z >= Min[2] && z <= Max[2];
}

public sealed record NaturalAltgardQuest(int Id, string Category, int MinimumLevel, int RewardExperience, int? Prerequisite,
	string Handler, string? Template, int? StartNpcId, string? Area)
{
	public bool IsTemplate => Handler == "template";
}

/// <summary>One scripted dialog step. <c>OFFER</c> is a quest the player has not taken yet.</summary>
public sealed record NaturalAltgardStep(string Key, int QuestId, int? Var, string? Status, int NpcId, float[] Position,
	int TalkRange, string[] Actions, int[] Pages, int? MovieId, int? ReceivesItemId, string? Area, bool Flight, string? Correction)
{
	public string ExpectedStatus => Status ?? "START";
}

public sealed record NaturalAltgardItemUse(int QuestId, int ItemId, int Var, int NextVar, int UseMillis, bool Anywhere);

public sealed record NaturalAltgardAirKills(int QuestId, int NpcId, int SpawnCount, int FromVar, int RewardVar, int KillsAfterBorender,
	string Note);

public sealed record NaturalAltgardRewardChoice(int QuestId, string Action, int ItemId, string ItemGroup);

public sealed record NaturalAltgardFlightZone(string Name, float Bottom, float Top);

public sealed record NaturalAltgardFlight(NaturalAltgardFlightZone[] Zones, float WaterLevel, int ReuseMillis, int MaxFlightTime);

public sealed record NaturalAltgardExclusion(int Id, string Reason, int Leg);

/// <param name="Anchor">Where the leg ends when that is not its hub (Leg 2: Manir's Campsite), within <paramref name="Radius"/>.</param>
public sealed record NaturalAltgardEndpoint(int MapId, int[] CompletedQuestIds, bool InHub, bool Alive, int MinimumLevel, string Snapshot,
	float[]? Anchor = null, float Radius = 0);

/// <summary>A quest object used by hand: a loot (Q2213's Okaru Tree) or a step per use (Q24012's MuMu Carts, each gone once
/// used; <paramref name="SpawnCount"/> spawns respawn after <paramref name="RespawnSeconds"/>).</summary>
public sealed record NaturalAltgardObjectUse(string Key, int QuestId, int NpcId, int FromVar, int ToVar, int Uses, int? LootItemId,
	bool Disappears, string? Area, int SpawnCount = 1, int RespawnSeconds = 0);

/// <summary>A quest step the server takes when the player enters a zone.</summary>
public sealed record NaturalAltgardZoneStep(int QuestId, string Zone, int FromVar, int ToVar);

public sealed record NaturalAltgardCollectedItem(int ItemId, int Count, int[] SourceNpcIds);

/// <summary>Items that only drop once the quest reaches <paramref name="AtVar"/> (quest_drop collecting_step).</summary>
public sealed record NaturalAltgardCollection(int QuestId, int AtVar, NaturalAltgardCollectedItem[] Items);

/// <summary>A poison a quest puts on the player, and the contract step that removes it.</summary>
public sealed record NaturalAltgardPoison(int QuestId, int SkillId, int Damage, int TickMillis, int DurationMillis, int SpeedDown,
	bool Dispellable, string RemovedByStep);
