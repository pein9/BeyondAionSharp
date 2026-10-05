using System.Text.Json;
using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>RC-01: the approved PC-08 schedule, separate from historical leg contracts.</summary>
public sealed record NaturalLaterCapitalContract(int SchemaVersion, string JavaReference,
	NaturalLaterCapitalQuest[] Quests, NaturalLaterCapitalAssignment[] Assignments)
{
	public static NaturalLaterCapitalContract LoadDefault()
	{
		string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
		return JsonSerializer.Deserialize<NaturalLaterCapitalContract>(
			File.ReadAllText(Path.Combine(root, "parity-artifacts/e2e/natural-later-capital-contract.json")),
			new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new InvalidDataException("Empty later capital contract.");
	}

	public IReadOnlySet<int> CompletedQuestIds => Quests.Select(q => q.Id).ToHashSet();
	public IReadOnlySet<int> ProtectedItemIds => Quests.SelectMany(q => q.ProtectedItemIds).ToHashSet();

	public void ValidateScope(NaturalJourneyOptions options, string profile)
	{
		if (!options.LaterCapital) return;
		bool leg = options.AltgardLeg1 || options.AltgardLegId == "all" ||
			options.AltgardLegId != null && NaturalAltgardContract.Legs.ContainsKey(options.AltgardLegId);
		if (!profile.StartsWith("SIM-", StringComparison.Ordinal) || !(options.AscensionBridge || leg) ||
			options.CapitalStage != null || options.StopAfterQuest != null || options.StopAt != null ||
			options.Course != null || options.Encounter != null || options.MauPolicy != null || options.ClericEncounter ||
			options.AltgardOnlyQuests != null)
			throw new InvalidOperationException("Later capital work requires an explicit natural SIM bridge or Altgard segment, without unrelated diagnostic scopes.");
	}

	/// <summary>One assignment's availability from the client. No quest state or inventory is restored here.</summary>
	public NaturalLaterCapitalAvailability Observe(string key, NaturalJourneyCheckpoint checkpoint)
	{
		NaturalLaterCapitalAssignment assignment = Assignments.Single(a => a.Key == key);
		NaturalLaterCapitalQuest quest = Quests.Single(q => q.Id == assignment.QuestId);
		if (checkpoint.CompletedQuestIds.Contains(quest.Id)) return new(assignment, "complete");
		if (checkpoint.Level < quest.MinimumLevel) return new(assignment, "deferred-level");
		if (!quest.Prerequisites.Concat(assignment.Prerequisites ?? []).All(checkpoint.CompletedQuestIds.Contains))
			return new(assignment, "deferred-prerequisites");
		BotQuestState? state = checkpoint.Quests.SingleOrDefault(q => q.QuestId == quest.Id);
		int step = (state?.StepAndFlags ?? 0) & 0x00FFFFFF;
		bool itemsReady = (assignment.Items ?? []).All(required =>
			checkpoint.Inventory.Where(item => item.ItemId == required.ItemId).Sum(item => item.Count) >= required.Count);
		bool targetReached = assignment.TargetStatus == 3 ? state?.Status is 3 or 4 && step >= assignment.TargetStep
			: assignment.TargetStatus == 4 && state?.Status == 4 && step == assignment.TargetStep;
		if (!assignment.Finish && itemsReady && targetReached)
			return new(assignment, "complete");
		if (assignment.RequiredStatus != null &&
			!targetReached && (state?.Status != assignment.RequiredStatus || step != assignment.RequiredStep))
			return new(assignment, "blocked-incoming-state");
		if (!assignment.Finish && itemsReady && assignment.TargetStatus == null && assignment.Items?.Length > 0)
			return new(assignment, "complete");
		return new(assignment, "work");
	}

	/// <summary>Freeze only observed carried state; the normal checkpoint also retains the complete journal/inventory.</summary>
	public NaturalLaterCapitalState Capture(NaturalJourneyCheckpoint checkpoint)
	{
		NaturalJourneyCompletedQuest[] completed = (checkpoint.CompletedQuests ?? [])
			.Where(q => CompletedQuestIds.Contains(q.QuestId)).OrderBy(q => q.QuestId).ToArray();
		if (completed.Any(q => q.CompleteCount != 1) ||
			checkpoint.CompletedQuestIds.Where(CompletedQuestIds.Contains).Any(id => !completed.Any(q => q.QuestId == id)))
			throw new InvalidDataException("Later capital completions must have one observed payment, including the repeatable juice delivery.");
		return new(checkpoint.CharacterId, checkpoint.MapId, checkpoint.Level,
			checkpoint.Quests.Where(q => CompletedQuestIds.Contains(q.QuestId) && q.Status is 3 or 4)
				.OrderBy(q => q.QuestId).ToArray(), completed,
			checkpoint.Inventory.Where(item => ProtectedItemIds.Contains(item.ItemId)).OrderBy(item => item.ObjectId).ToArray());
	}

	public async Task WriteCheckpointAsync(string directory, string segment, NaturalJourneyCheckpoint before,
		NaturalJourneyCheckpoint after, CancellationToken token)
	{
		if (segment != "bridge" && !NaturalAltgardContract.Legs.ContainsKey(segment))
			throw new InvalidDataException("A later capital receipt needs the canonical bridge or Altgard segment selector.");
		NaturalJourneyPersistence.Verify(before, after);
		NaturalLaterCapitalState incoming = Capture(before), retained = Capture(after);
		await File.WriteAllTextAsync(Path.Combine(directory, "later-capital-checkpoint.json"),
			JsonSerializer.Serialize(new { schemaVersion = SchemaVersion, javaReference = JavaReference,
				segment, characterId = after.CharacterId, verified = true, before = incoming, after = retained },
				new JsonSerializerOptions(JsonSerializerDefaults.Web)), token);
	}
}

public sealed record NaturalLaterCapitalQuest(int Id, string Name, int MinimumLevel, int[] Prerequisites, int[] ProtectedItemIds);
public sealed record NaturalLaterCapitalItem(int ItemId, long Count);
public sealed record NaturalLaterCapitalAssignment(string Key, string Segment, int QuestId, bool Finish = false,
	byte? TargetStatus = null, int? TargetStep = null, byte? RequiredStatus = null, int? RequiredStep = null,
	int[]? Prerequisites = null, NaturalLaterCapitalItem[]? Items = null);
public sealed record NaturalLaterCapitalAvailability(NaturalLaterCapitalAssignment Assignment, string Outcome);
public sealed record NaturalLaterCapitalState(int CharacterId, int MapId, ushort Level, BotQuestState[] ActiveQuests,
	NaturalJourneyCompletedQuest[] CompletedQuests, NaturalJourneyItem[] Inventory);
