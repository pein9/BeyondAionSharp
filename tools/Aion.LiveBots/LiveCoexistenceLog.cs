using Aion.Bots.Protocol;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

/// <summary>
/// NI-10 coexistence evidence: every other player the server showed the bot, and whether the journey kept
/// progressing meanwhile. The server sends SM_PLAYER_INFO only for players in mutual sight, so a sighting also
/// means that player's client was sent the bot. This is observation only; the journey never reads it.
/// </summary>
internal sealed class LiveCoexistenceLog
{
	private readonly Dictionary<int, Sighting> players = [];
	private readonly HashSet<int> contractQuestIds = Aion.Bots.Scenarios.NaturalIshalgenContract.LoadDefault().Quests
		.Select(quest => quest.Id).ToHashSet();
	private readonly object gate = new();

	public void Observe(DecodedBotServerPacket packet, BotWorldModel world, int selfObjectId, BotPosition? self,
		string step, BotActionTraceWriter trace)
	{
		if (packet.PacketType != typeof(SM_PLAYER_INFO) && packet.PacketType != typeof(SM_MOVE)) return;
		int objectId = packet.Get<int>("objectId");
		if (objectId == selfObjectId || !world.Objects.TryGetValue(objectId, out BotKnownObject? player) ||
			player.Kind != BotKnownObjectKind.Player) return;
		DateTimeOffset now = DateTimeOffset.UtcNow;
		float? distance = self is BotPosition bot ? Distance(bot, player.Position) : null;
		int completed = world.CompletedQuestIds.Count(contractQuestIds.Contains);
		lock (gate)
		{
			if (!players.TryGetValue(objectId, out Sighting? sighting))
			{
				sighting = new Sighting(objectId, player.Name, player.Race, player.PlayerClass, now, step, completed);
				players.Add(objectId, sighting);
				trace.WriteAction(step, "coexistence:player-observed", new Dictionary<string, object?>
				{
					["objectId"] = objectId, ["name"] = player.Name, ["race"] = player.Race,
					["playerClass"] = player.PlayerClass, ["distance"] = distance, ["position"] = player.Position,
					["botCompletedQuests"] = completed,
				});
			}
			sighting.Name ??= player.Name;
			sighting.LastSeenUtc = now;
			sighting.LastStep = step;
			sighting.Observations++;
			sighting.CompletedQuestsAtLastSight = completed;
			if (distance is float current && (sighting.MinimumDistance == null || current < sighting.MinimumDistance))
				sighting.MinimumDistance = current;
		}
	}

	public object Snapshot(int completedQuestsNow)
	{
		lock (gate)
		{
			return new
			{
				schemaVersion = 1,
				otherPlayersObserved = players.Count,
				botCompletedQuestsAtEnd = completedQuestsNow,
				players = players.Values.OrderBy(sighting => sighting.FirstSeenUtc).Select(sighting => new
				{
					sighting.ObjectId, sighting.Name, sighting.Race, sighting.PlayerClass,
					sighting.FirstSeenUtc, sighting.LastSeenUtc, sighting.FirstStep, sighting.LastStep,
					sighting.Observations, sighting.MinimumDistance,
					sighting.CompletedQuestsAtFirstSight, sighting.CompletedQuestsAtLastSight,
					questsCompletedSinceFirstSight = completedQuestsNow - sighting.CompletedQuestsAtFirstSight,
				}).ToArray(),
			};
		}
	}

	private static float Distance(BotPosition a, BotPosition b) =>
		MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));

	private sealed class Sighting(int objectId, string? name, byte? race, byte? playerClass, DateTimeOffset firstSeen,
		string firstStep, int completedQuests)
	{
		public int ObjectId { get; } = objectId;
		public string? Name { get; set; } = name;
		public byte? Race { get; } = race;
		public byte? PlayerClass { get; } = playerClass;
		public DateTimeOffset FirstSeenUtc { get; } = firstSeen;
		public DateTimeOffset LastSeenUtc { get; set; } = firstSeen;
		public string FirstStep { get; } = firstStep;
		public string LastStep { get; set; } = firstStep;
		public int Observations { get; set; }
		public float? MinimumDistance { get; set; }
		public int CompletedQuestsAtFirstSight { get; } = completedQuests;
		public int CompletedQuestsAtLastSight { get; set; } = completedQuests;
	}
}
