using System.Text.Json;
using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>A diagnostic receipt of client state, never an instruction to restore server state.
/// After reconnect, capture a new receipt from the new login instead of replaying this one.</summary>
public sealed record NaturalJourneyCheckpoint(
	int CharacterId, int ConnectionGeneration, int MapId, BotPosition Position, ushort Level,
	int CurrentHp, int MaxHp, int CurrentMp, int MaxMp, bool IsDead,
	BotQuestState[] Quests, int[] CompletedQuestIds, NaturalJourneyItem[] Inventory,
	BotSkill[] Skills, NaturalDecision Next)
{
	public static NaturalJourneyCheckpoint Capture(BotWorldModel world, int expectedCharacterId,
		int generation, NaturalIshalgenContract contract, BotPosition? currentPosition = null, int sequence = 1)
	{
		if (!world.LoginStateObserved)
			throw new InvalidDataException("Natural journey login is incomplete: wait for position, stats, both journals, inventory and skills.");
		if (world.SelfObjectId != expectedCharacterId)
			throw new InvalidDataException($"Natural journey expected character {expectedCharacterId}, observed {world.SelfObjectId}.");
		BotPosition position = currentPosition ?? world.Position!.Value;
		var observation = Observe(world, position);
		return new(expectedCharacterId, generation, world.MapId!.Value, position, world.Level,
			world.CurrentHp, world.MaxHp, world.CurrentMp, world.MaxMp, world.IsDead,
			world.Quests.Values.OrderBy(q => q.QuestId).ToArray(), world.CompletedQuestIds.Order().ToArray(),
			world.Inventory.Values.OrderBy(i => i.ObjectId).Select(i =>
				new NaturalJourneyItem(i.ObjectId, i.ItemId, i.Count, i.EquipmentSlot)).ToArray(),
			world.Skills.Values.OrderBy(s => s.SkillId).ToArray(),
			NaturalIshalgenDecisionEngine.Decide(contract, observation, sequence));
	}

	public static NaturalIshalgenObservation Observe(BotWorldModel world, BotPosition? currentPosition = null) =>
		new(world.LoginStateObserved, world.QuestJournalObserved, world.CompletedJournalObserved,
			world.MapId, world.Level, world.IsDead, new Dictionary<int, BotQuestState>(world.Quests),
			world.CompletedQuestIds.ToHashSet(), currentPosition ?? world.Position, world.Objects.Values.ToArray());
}

public sealed record NaturalJourneyItem(int ObjectId, int ItemId, long Count, ushort EquipmentSlot);

public sealed record NaturalJourneyFailure(
	string Kind, string Step, string Action, int ConnectionGeneration,
	string ExceptionType, string Message, string? StackTrace, NaturalJourneyCheckpoint? LastObservation,
	string? PacketTracePath, string[] RecentPackets, NaturalJourneyRunContext? Context = null)
{
	public JsonElement? ServerProblems { get; init; }

	/// <summary>Unique file per incident; later recovery must not overwrite the original failure.</summary>
	public string Write(string directory)
	{
		Directory.CreateDirectory(directory);
		string path = Path.Combine(directory, $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}.failure.json");
		File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
		return path;
	}
}

/// <summary>Reconnects and movement cannot hide an indefinitely stalled quest.</summary>
public sealed class NaturalJourneyProgress(TimeSpan maximumWithoutProgress, IReadOnlySet<int>? objectiveItemIds = null)
{
	private string? previous;
	private TimeSpan lastProgress;
	private TimeSpan lastObserved;

	public void Observe(NaturalJourneyCheckpoint checkpoint, TimeSpan elapsed)
	{
		if (maximumWithoutProgress <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maximumWithoutProgress));
		if (elapsed < lastObserved) throw new ArgumentOutOfRangeException(nameof(elapsed), "Use a monotonic journey clock across reconnects.");
		lastObserved = elapsed;
		// Ignore position, health, potions and object IDs: moving in circles, healing or reconnecting
		// is not quest progress. Quest items persist by template/count, not transient object identity.
		string fingerprint = JsonSerializer.Serialize(new
		{
			checkpoint.Level,
			Quests = checkpoint.Quests.OrderBy(q => q.QuestId).Select(q => new { q.QuestId, q.Status, q.StepAndFlags }),
			Completed = checkpoint.CompletedQuestIds.Order(),
			Skills = checkpoint.Skills.OrderBy(s => s.SkillId).Select(s => new { s.SkillId, s.Level }),
			Items = checkpoint.Inventory.Where(i => i.ItemId is >= 182200000 and < 182300000 || objectiveItemIds?.Contains(i.ItemId) == true)
				.GroupBy(i => i.ItemId).OrderBy(g => g.Key).Select(g => new { ItemId = g.Key, Count = g.Sum(i => i.Count) }),
		});
		if (fingerprint != previous)
		{
			previous = fingerprint;
			lastProgress = elapsed;
		}
		else if (elapsed - lastProgress >= maximumWithoutProgress)
			throw new TimeoutException($"Natural journey made no quest, quest-item or level progress for {maximumWithoutProgress}.");
	}
}

public sealed record NaturalJourneyRunContext(string Run, string Profile, int Seed,
	string Build, string ModuleId, long ElapsedMilliseconds);
