using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>Client-visible progress for the frozen, old-style Ishalgen monster_hunt plans.
/// Java MonsterHunt and QuestVars pack each counter into six bits; sequence is not var ID.</summary>
public static class NaturalQuestProgress
{
	public static int RemainingKills(QuestRunPlan plan, QuestRunOperation operation, BotWorldModel world)
	{
		if (world.CompletedQuestIds.Contains(plan.Id)) return 0;
		if (!world.Quests.TryGetValue(plan.Id, out var quest) || quest.Status < 3)
			throw new InvalidDataException($"Q{plan.Id} has not been accepted in the client journal.");
		if (quest.Status >= 4) return 0;
		if (plan.Template != "monster_hunt" || operation.Kind != QuestRunOperationKind.Kill)
			throw new InvalidDataException("Natural kill reconstruction requires a frozen monster_hunt operation.");
		QuestRunStep step = plan.Steps.Single(s => s.Kind == "kill" && s.Sequence == operation.Sequence);
		int varId = step.Data.GetProperty("var").GetInt32();
		return Math.Max(0, operation.Count - KillCount(quest, varId, operation.Count));
	}

	/// <summary>Java MonsterHunt reads consecutive six-bit QuestVars until the required count fits.
	/// The high flag byte is never part of the counter (Haramel Q28504 needs twelve bits for 65).</summary>
	public static int KillCount(BotQuestState quest, int varId, int requiredCount)
	{
		if (requiredCount < 0) throw new ArgumentOutOfRangeException(nameof(requiredCount));
		int bits = 6;
		for (int count = requiredCount >> 6; count > 0; count >>= 6) bits += 6;
		if (varId < 0 || varId * 6 + bits > 24)
			throw new InvalidDataException($"Q{quest.QuestId} kill counter overlaps the wire quest flags.");
		return (quest.StepAndFlags >> (varId * 6)) & ((1 << bits) - 1);
	}
}
