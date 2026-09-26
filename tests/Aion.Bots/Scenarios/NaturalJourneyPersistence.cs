namespace Aion.Bots.Scenarios;

/// <summary>Compare persisted player state with a fresh packet-derived login observation.</summary>
public static class NaturalJourneyPersistence
{
	public static void Verify(NaturalJourneyCheckpoint before, NaturalJourneyCheckpoint after)
	{
		Require(after.ConnectionGeneration > before.ConnectionGeneration, "fresh login generation");
		Require(before.CharacterId == after.CharacterId, "character identity");
		Require(before.MapId == after.MapId, "map");
		Require(before.Level == after.Level, "level");
		// Match the existing NI-08 oracle: SQL FLOAT coordinates can round-trip with small error.
		Require(Math.Abs(before.Position.X - after.Position.X) <= 0.05f &&
			Math.Abs(before.Position.Y - after.Position.Y) <= 0.05f &&
			Math.Abs(before.Position.Z - after.Position.Z) <= 0.05f &&
			before.Position.Heading == after.Position.Heading, "position");
		Require(before.CompletedQuestIds.Order().SequenceEqual(after.CompletedQuestIds.Order()), "completed journal");
		Require(ActiveQuests(before).SequenceEqual(ActiveQuests(after)), "active journal");
		Require(before.Inventory.OrderBy(i => i.ObjectId).SequenceEqual(after.Inventory.OrderBy(i => i.ObjectId)), "inventory");
		Require(Skills(before).SequenceEqual(Skills(after)), "skills");

		static IEnumerable<(int, byte, int, byte)> ActiveQuests(NaturalJourneyCheckpoint checkpoint) =>
			checkpoint.Quests.Where(q => q.Status is 3 or 4).OrderBy(q => q.QuestId)
				.Select(q => (q.QuestId, q.Status, q.StepAndFlags, q.CompleteCount));
		// The wire flag contains a time-dependent value; proficiency is the persisted state.
		static IEnumerable<(ushort, ushort, byte, byte)> Skills(NaturalJourneyCheckpoint checkpoint) =>
			checkpoint.Skills.OrderBy(s => s.SkillId).Select(s => (s.SkillId, s.Level, s.ProfessionBarSize, s.SkillType));
		static void Require(bool condition, string field)
		{
			if (!condition) throw new InvalidDataException($"Natural journey relog changed {field}.");
		}
	}
}
