using System.Buffers.Binary;
using Aion.Bots.Protocol;
using Aion.Bots.Reflexes;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotReflexesTests
{
	[Fact]
	public void AcknowledgesSpawnTeleportAndMovieWithTheirMatchingClientPackets()
	{
		var reflexes = new BotReflexes();

		var levelReady = Assert.IsType<BotClientPacket>(reflexes.RespondTo(Packet<SM_PLAYER_SPAWN>()));
		Assert.Equal(typeof(CM_LEVEL_READY), levelReady.PacketType);
		Assert.Empty(levelReady.Body);

		var teleportDone = Assert.IsType<BotClientPacket>(reflexes.RespondTo(Packet<SM_TELEPORT_LOC>()));
		Assert.Equal(typeof(CM_TELEPORT_ANIMATION_DONE), teleportDone.PacketType);
		Assert.Empty(teleportDone.Body);

		var movieEnd = Assert.IsType<BotClientPacket>(reflexes.RespondTo(Packet<SM_PLAY_MOVIE>(
			("isMovie", true), ("objectId", 101), ("questId", 102), ("cutsceneId", 103), ("canSkip", true))));
		Assert.Equal(typeof(CM_PLAY_MOVIE_END), movieEnd.PacketType);
		Assert.Equal(15, movieEnd.Body.Length);
		Assert.Equal((byte)1, movieEnd.Body[0]);
		Assert.Equal(101, BinaryPrimitives.ReadInt32LittleEndian(movieEnd.Body.AsSpan(1)));
		Assert.Equal(102, BinaryPrimitives.ReadInt32LittleEndian(movieEnd.Body.AsSpan(5)));
		Assert.Equal(103, BinaryPrimitives.ReadInt32LittleEndian(movieEnd.Body.AsSpan(9)));
		Assert.Equal((byte)0, movieEnd.Body[14]);
	}

	[Fact]
	public void AppliesExplicitReviveAndQuestionPolicies()
	{
		BotDeathPrompt? deathSeen = null;
		BotQuestionPrompt? questionSeen = null;
		var reflexes = new BotReflexes(
			prompt =>
			{
				deathSeen = prompt;
				return prompt.AllowInstance ? BotReviveType.Instance : null;
			},
			prompt =>
			{
				questionSeen = prompt;
				return 1;
			});

		var revive = Assert.IsType<BotClientPacket>(reflexes.RespondTo(Packet<SM_DIE>(
			("allowReviveBySkill", false), ("allowReviveByItem", true), ("remainingKiskTimeSeconds", 45),
			("allowInstanceRevive", true), ("invasion", false))));
		Assert.Equal(typeof(CM_REVIVE), revive.PacketType);
		Assert.Equal([(byte)BotReviveType.Instance], revive.Body);
		Assert.True(deathSeen!.Value.AllowByItem);
		Assert.Equal(45, deathSeen.Value.RemainingKiskTimeSeconds);

		var answer = Assert.IsType<BotClientPacket>(reflexes.RespondTo(Packet<SM_QUESTION_WINDOW>(
			("code", 60000), ("params", new[] { "Inviter", "", "" }), ("senderId", 77),
			("rangeOrCooldownSeconds", 5))));
		Assert.Equal(typeof(CM_QUESTION_RESPONSE), answer.PacketType);
		Assert.Equal(60000, BinaryPrimitives.ReadInt32LittleEndian(answer.Body));
		Assert.Equal((byte)1, answer.Body[4]);
		Assert.Equal(77, BinaryPrimitives.ReadInt32LittleEndian(answer.Body.AsSpan(8)));
		Assert.Equal("Inviter", questionSeen!.Parameters[0]);
	}

	[Fact]
	public void DefaultPoliciesDoNotAnswerDeathOrQuestionPrompts()
	{
		var reflexes = new BotReflexes();
		Assert.Null(reflexes.RespondTo(Packet<SM_DIE>(
			("allowReviveBySkill", true), ("allowReviveByItem", true), ("remainingKiskTimeSeconds", 1),
			("allowInstanceRevive", true), ("invasion", false))));
		Assert.Null(reflexes.RespondTo(Packet<SM_QUESTION_WINDOW>(
			("code", 1), ("params", Array.Empty<string>()), ("senderId", 2), ("rangeOrCooldownSeconds", 3))));
	}

	[Fact]
	public void LivePingCadenceStaysBetweenOneHundredEightyAndOneHundredEightyThreeSeconds()
	{
		var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 17, 12, 0, 0, TimeSpan.Zero));
		var jitter = new Queue<int>([0, 3, 1]);
		var scheduler = new LiveBotPingScheduler(clock, () => jitter.Dequeue());

		Assert.Equal(clock.GetUtcNow() + TimeSpan.FromSeconds(180), scheduler.NextDueAt);
		clock.Advance(TimeSpan.FromSeconds(179));
		Assert.Null(scheduler.Poll());
		clock.Advance(TimeSpan.FromSeconds(1));
		var first = Assert.IsType<BotClientPacket>(scheduler.Poll());
		Assert.Equal(typeof(CM_PING), first.PacketType);
		Assert.Equal(clock.GetUtcNow() + TimeSpan.FromSeconds(183), scheduler.NextDueAt);

		clock.Advance(TimeSpan.FromSeconds(LiveBotPingScheduler.ServerEarlyThresholdSeconds));
		Assert.Null(scheduler.Poll());
		clock.Advance(TimeSpan.FromSeconds(5));
		Assert.IsType<BotClientPacket>(scheduler.Poll());
		Assert.Equal(clock.GetUtcNow() + TimeSpan.FromSeconds(181), scheduler.NextDueAt);
	}

	[Fact]
	public void LivePingCadenceRejectsOutOfRangeJitter()
	{
		var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
		Assert.Throws<InvalidOperationException>(() => new LiveBotPingScheduler(clock, () => 4));
	}

	private static DecodedBotServerPacket Packet<T>(params (string Name, object? Value)[] fields) =>
		new(typeof(T), fields.ToDictionary(field => field.Name, field => field.Value, StringComparer.Ordinal));

	private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
	{
		private DateTimeOffset current = now;

		public override DateTimeOffset GetUtcNow() => current;

		public void Advance(TimeSpan amount) => current += amount;
	}
}
