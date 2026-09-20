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
		var vandar = Npc(203504, 526.99f, 2775.67f, 295.751f);
		var vanar = Npc(203502, 220.15f, 2678.81f, 295.25f);
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
				new(2101, Npc(203500, 560.83f, 2788.11f, 299.062f), vandar, 80, 130),
				new(2102, vandar, vandar, 120, 180, new(210363, [P(458.924f, 2784.15f, 288.893f), P(468.672f, 2763.12f, 289.292f), P(469.915f, 2770.84f, 288.947f), P(424.609f, 2828.68f, 295.793f)], true), 169300002, 10),
				new(2103, vandar, Npc(203501, 223.975f, 2679.86f, 295.25f), 410, 590),
				new(2104, vanar, vanar, 770, 520, new(700124, [P(135.39f, 2643.84f, 306.337f), P(140.963f, 2653.23f, 305.977f), P(145.208f, 2668.29f, 305.299f)], false, 182203104)),
				new(2105, vanar, vanar, 530, 420, new(210367, [P(148.34f, 2692.48f, 305.894f), P(168.82f, 2638.2f, 305.185f), P(169.49f, 2623.05f, 307.181f)], true, 182203105)),
				new(2100, null, Npc(203516, 589.35f, 2450.09f, 278.375f), 0, 510, RewardItem: 100000107, Campaign: true),
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

	// The bounded direct search from Ulgorn to the hub has no checked route. Return via
	// the already visited western camp and Vandar; validate these legs just like the outward trip.
	public static IReadOnlyList<BotPosition> ReturnVia(ScenarioRace race) => race switch
	{
		ScenarioRace.Elyos => [],
		ScenarioRace.Asmodians => [P(220.15f, 2678.81f, 295.25f), P(526.99f, 2775.67f, 295.751f)],
		_ => throw new ArgumentOutOfRangeException(nameof(race)),
	};

	private static BotPosition P(float x, float y, float z) => new(x, y, z, 0);
	private static StarterQuestNpc Npc(int id, float x, float y, float z) => new(id, P(x, y, z));
}
