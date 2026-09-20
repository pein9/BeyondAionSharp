using System.Text.Json;

namespace Aion.Bots.Scenarios;

/// <summary>Read-only, offline database evidence; never substitutes live in-memory quest state.</summary>
public static class QuestPersistenceContract
{
	public static void AssertCompletedOnce(JsonElement snapshot, IEnumerable<int> questIds)
	{
		if (!snapshot.TryGetProperty("online", out var online) || online.ValueKind != JsonValueKind.False ||
			!snapshot.TryGetProperty("quests", out var quests) || quests.ValueKind != JsonValueKind.Array)
			throw new InvalidDataException("Quest persistence requires an offline player-state snapshot with persisted quest rows.");
		var rows = quests.EnumerateArray().ToDictionary(row => row.GetProperty("questId").GetInt32());
		foreach (int id in questIds)
		{
			if (!rows.TryGetValue(id, out var row)) throw new InvalidDataException($"player_quests omitted Q{id}.");
			if (!string.Equals(row.GetProperty("status").GetString(), "COMPLETE", StringComparison.Ordinal) ||
				row.GetProperty("completeCount").GetInt32() != 1)
				throw new InvalidDataException($"player_quests Q{id} was not COMPLETE with complete_count 1.");
		}
	}
}
