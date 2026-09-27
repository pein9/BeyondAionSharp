using Aion.Bots.World;
using Aion.GameServer.Model.Templates.Quest;

namespace Aion.Bots.Scenarios;

/// <summary>
/// The named stops are journey knowledge, not a discovery oracle. Quest availability still
/// comes from the observed journal and the Java quest templates. A quest belongs to the
/// hub where its starter lives, even when its objectives or reward are elsewhere.
/// </summary>
public static class NaturalIshalgenHubPolicy
{
	public sealed record Hub(string Name, BotPosition Center, int[] QuestIds);

	// The proven NI-07 route supplies safety boundaries: the Mau campaign levels and
	// equips the Priest before the eastern objectives. Within a boundary, compatible
	// quests may be worked together and then claimed on the same return to a hub.
	private static readonly int[][] SafeWorkGroups =
	[
		[2101], [2102], [2103], [2104], [2100], [2001], [2002], [2132],
		[2003], [2004], [2005], [2006], [2007], [2105], [2106],
		[2108, 2133], [2109, 2110, 2112, 2135], [2113, 2114, 2115],
		[2117, 2118], [2120, 2121], [2131], [2137], [2116], [2119],
		[2123], [2124], [2125], [2126, 2127, 2128], [2134], [2129],
	];

	public static int[] CurrentSafeGroup(IReadOnlySet<int> completedQuestIds) =>
		SafeWorkGroups.FirstOrDefault(group => group.Any(id => !completedQuestIds.Contains(id))) ?? [];

	public static readonly Hub[] Hubs =
	[
		new("starter", new(530, 2780, 296, 0), [2101, 2102, 2103]),
		new("vanar", new(224, 2680, 295, 0), [2104, 2105, 2106]),
		new("aldelle", new(575, 2440, 278, 0),
			[2100, 2001, 2002, 2108, 2109, 2110, 2112, 2131, 2132, 2133, 2135]),
		new("dabi", new(850, 2220, 266, 0), [2113, 2114, 2115]),
		new("mijou", new(940, 1700, 260, 0),
			[2003, 2004, 2116, 2117, 2118, 2119, 2120, 2121, 2125, 2137]),
		new("western-camp", new(670, 1730, 271, 0), [2124]),
		new("anturoon", new(930, 1565, 260, 0),
			[2005, 2006, 2007, 2126, 2127, 2128, 2134]),
		new("munin", new(379, 1895, 329, 0), [2123]),
		new("hatata", new(729, 1157, 303, 0), [2129]),
	];

	// These hand-written quest handlers use an ordinary NPC starter. Campaigns and
	// auto-started quests have no generic accept packet, and Q2114 needs SETPRO1.
	public static readonly IReadOnlyDictionary<int, int> CustomNpcStarters = new Dictionary<int, int>
	{
		[2101] = 203500, [2102] = 203504, [2103] = 203504,
		[2104] = 203502, [2105] = 203502, [2106] = 203502,
		[2123] = 203550, [2125] = 203540, [2135] = 203532,
	};

	public static Hub? At(BotPosition position, float radius = 110f) =>
		Hubs.Select(hub => (Hub: hub, Distance: Distance(position, hub.Center)))
			.Where(entry => entry.Distance <= radius)
			.OrderBy(entry => entry.Distance).Select(entry => entry.Hub).FirstOrDefault();

	public static Hub? ForQuest(int questId) => Hubs.FirstOrDefault(hub => hub.QuestIds.Contains(questId));

	/// <summary>Q2116's sole object lies inside a seven-mob pack at (1206, 1885).
	/// The completed baseline handles it after the Mau campaign; entering at its
	/// minimum level repeatedly kills the Priest, so defer work but keep its pickup.</summary>
	public static bool CanWorkNow(int questId, IReadOnlySet<int> completedQuestIds) =>
		questId != 2116 || completedQuestIds.Contains(2007);

	public static bool CanAccept(NaturalIshalgenQuestContract quest, BotWorldModel world) =>
		!world.CompletedQuestIds.Contains(quest.Id) && !world.Quests.ContainsKey(quest.Id) &&
		world.Level >= quest.MinimumLevel && quest.Prerequisites.All(world.CompletedQuestIds.Contains);

	public static int[] Order(IEnumerable<int> questIds, NaturalIshalgenContract contract,
		Func<int, QuestCategory> category, Hub? nearby, IReadOnlySet<int>? worked = null)
	{
		var quests = contract.Quests.ToDictionary(quest => quest.Id);
		return questIds.OrderBy(id => nearby != null && nearby.QuestIds.Contains(id) ? 0 : 1)
			.ThenBy(id => quests[id].MinimumLevel)
			.ThenBy(id => category(id) == QuestCategory.MISSION ? 1 : 0)
			.ThenBy(id => worked?.Contains(id) == true ? 1 : 0)
			.ThenBy(id => id).ToArray();
	}

	public static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt(
		MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));
}
