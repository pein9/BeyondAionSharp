using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

public sealed record StarterQuestNpc(int TemplateId, BotPosition Position);
public sealed record StarterQuestObjective(int TemplateId, IReadOnlyList<BotPosition> Positions, bool Kill, int ItemId = 0);
public sealed record StarterSoakQuest(int Id, StarterQuestNpc? Start, StarterQuestNpc End, int Gold, int Experience,
	StarterQuestObjective? Objective = null, int RewardItem = 0, int RewardCount = 1, int WorkItem = 0, bool Campaign = false)
{
	/// <summary>Finite Q1/Q2 content at Java ce54b7931; no repeatable substitutes or state resets.</summary>
	public static IReadOnlyList<StarterSoakQuest> ForRace(ScenarioRace race)
	{
		var mires = Npc(203057, 1141, 1032, 128.875f);
		var kales = Npc(203050, 984.994f, 1133.94f, 108.563f);
		var vandar = Npc(203504, 527.004f, 2775.671f, 295.751f);
		var vanar = Npc(203502, 228.848f, 2694.775f, 295.173f);
		return race switch
		{
			ScenarioRace.Elyos => [
				new(1101, Npc(203049, 1204.29f, 1053.18f, 138.962f), mires, 120, 130),
				new(1102, mires, mires, 400, 180, new(210133, [P(1098.26f, 1000.43f, 125.6f), P(1073.99f, 1057.03f, 125.867f), P(1052.9f, 1070.34f, 117.854f)], true)),
				new(1103, mires, mires, 290, 590, new(700105, [P(1121.36f, 993.115f, 130.656f), P(1124.9f, 979.713f, 132.054f), P(1130.27f, 978.243f, 132.892f)], false, 182200201)),
				new(1104, mires, Npc(203059, 825.436f, 1241.98f, 118.839f), 90, 520),
				new(1100, null, Npc(203067, 820.908f, 1241.05f, 118.682f), 0, 510, RewardItem: 100000095, Campaign: true),
				new(1105, kales, kales, 450, 535, new(210079, [P(953.314f, 1114.72f, 104.951f), P(946.14f, 1066.48f, 107.348f), P(925.972f, 1105.12f, 103.18f)], true, 182200202)),
				new(1106, kales, Npc(203061, 847.263f, 1256.88f, 118.75f), 0, 300, WorkItem: 182200203),
			],
			ScenarioRace.Asmodians => [
				new(2101, Npc(203500, 560.989f, 2789.895f, 299.117f), vandar, 80, 130),
				new(2102, vandar, vandar, 120, 180, new(210363, [P(454.128f, 2774.140f, 288.875f), P(476.033f, 2758.439f, 290.032f), P(520.441f, 2716.861f, 293.456f), P(420.613f, 2810.292f, 293.230f)], true), 169300002, 10),
				new(2103, vandar, Npc(203501, 223.372f, 2676.063f, 295.250f), 410, 590),
				new(2104, vanar, vanar, 770, 520, new(700124, [P(135.390f, 2643.841f, 306.346f), P(140.963f, 2653.232f, 305.952f), P(145.208f, 2668.289f, 305.299f)], false, 182203104)),
				new(2105, vanar, vanar, 530, 420, new(210367, [P(157.943f, 2696.984f, 304.504f), P(166.709f, 2716.284f, 305.403f), P(282.344f, 2842.345f, 308.772f)], true, 182203105)),
				new(2100, null, Npc(203516, 584.949f, 2418.722f, 278.625f), 0, 510, RewardItem: 100000107, Campaign: true),
			],
			_ => throw new ArgumentOutOfRangeException(nameof(race)),
		};
	}

	public IEnumerable<BotPosition> Stops()
	{
		if (Start != null) yield return Start.Position;
		if (Objective != null) foreach (var position in Objective.Positions) yield return position;
		yield return End.Position;
	}

	public static bool ObservedComplete(BotWorldModel world, int questId) =>
		world.Quests.GetValueOrDefault(questId)?.Status == 5 || world.CompletedQuests.ContainsKey(questId);

	public void AssertImmediateReward(BotWorldModel world, long experienceBefore)
	{
		// SM_QUEST_ACTION carries status/vars, not complete_count. Check that count later
		// against persistence and SM_QUEST_COMPLETED_LIST instead of inventing a wire field.
		if (world.CurrentExperience != experienceBefore + Experience || world.Quests.GetValueOrDefault(Id)?.Status != 5)
			throw new InvalidDataException($"Soak Q{Id}: expected XP {experienceBefore + Experience}/status 5; observed {world.CurrentExperience}/{world.Quests.GetValueOrDefault(Id)?.Status}.");
	}

	// The bounded direct search from Ulgorn to the hub has no checked route. Return via
	// the already visited western camp and Vandar; validate these legs just like the outward trip.
	public static IReadOnlyList<BotPosition> ReturnVia(ScenarioRace race) => race switch
	{
		ScenarioRace.Elyos => [],
		ScenarioRace.Asmodians => [P(228.848f, 2694.775f, 295.173f), P(527.004f, 2775.671f, 295.751f)],
		_ => throw new ArgumentOutOfRangeException(nameof(race)),
	};

	private static BotPosition P(float x, float y, float z) => new(x, y, z, 0);
	private static StarterQuestNpc Npc(int id, float x, float y, float z) => new(id, P(x, y, z));
}
