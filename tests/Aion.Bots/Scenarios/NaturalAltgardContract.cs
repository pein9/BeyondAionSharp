using System.Text.Json;

namespace Aion.Bots.Scenarios;

/// <summary>
/// Leg 1 of the Altgard leveling leg (docs/natural-altgard-leveling.md): the fortress quests Q2201-Q2209 and the Q24011
/// campaign, from the <c>altgard</c> snapshot to the <c>altgard-l12</c> endpoint. Static walkthrough knowledge only; at
/// run time every step is still driven by what the client observes. The template quests' plans live beside it in
/// <c>natural-altgard-plans/</c>.
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
	NaturalAltgardItemUse ItemUse,
	NaturalAltgardAirKills AirKills,
	NaturalAltgardRewardChoice RewardChoice,
	Dictionary<int, int> UnhandedWorkItemIds,
	NaturalAltgardFlight Flight,
	NaturalAltgardExclusion[] Excluded,
	NaturalAltgardEndpoint Endpoint)
{
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
		return contract;
	}

	public static NaturalAltgardContract LoadDefault() => Load(Path.Combine(RepoRoot(), "parity-artifacts/e2e/natural-altgard-contract.json"));

	/// <summary>The compiled template-quest plans (Q2201-Q2206), keyed by quest id.</summary>
	public static IReadOnlyDictionary<int, QuestRunPlan> LoadPlans() =>
		QuestRunPlan.LoadDirectory(Path.Combine(RepoRoot(), "parity-artifacts/e2e/natural-altgard-plans")).ToDictionary(plan => plan.Id);

	public NaturalAltgardQuest Quest(int questId) => Quests.Single(quest => quest.Id == questId);

	/// <summary>Every NPC Leg 1 walks to, talks to or hunts: the navigation graph's waypoints for Altgard.</summary>
	public int[] GraphNpcIds(IReadOnlyDictionary<int, QuestRunPlan> plans) =>
		Quests.Select(quest => quest.StartNpcId).OfType<int>()
			.Concat(Steps.Select(step => step.NpcId))
			.Concat(plans.Values.SelectMany(plan => plan.StartNpcs.Concat(plan.EndNpcs)
				.Concat(plan.Steps.SelectMany(step => step.Npcs.Concat(step.Sources.Select(source => source.Npc).OfType<QuestRunNpc>())))
				.Select(npc => npc.Id)))
			.Append(Start.BindNpcId).Append(AirKills.NpcId)
			.Distinct().Order().ToArray();

	public NaturalAltgardArea Area(string key) => Areas.Single(area => area.Key == key);

	public IEnumerable<NaturalAltgardStep> StepsFor(int questId) => Steps.Where(step => step.QuestId == questId);

	private static string RepoRoot() =>
		Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
}

public sealed record NaturalAltgardStart(string Contract, string Snapshot, int MapId, string Race, string Class, int Level,
	int BindNpcId, int[] CompletedQuestIds, int[] LockedQuestIds);

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

public sealed record NaturalAltgardEndpoint(int MapId, int[] CompletedQuestIds, bool InHub, bool Alive, int MinimumLevel, string Snapshot);
