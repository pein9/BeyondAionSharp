using Aion.Bots.Timing;
using Aion.Bots.Api;
using System.Buffers.Binary;
using Aion.Bots.Protocol;
using Aion.Bots.Reflexes;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotReflexesTests
{
	[Fact]
	public void WatchModeHoldsTheMovieEndAndSkipModeAnswersAtOnce()
	{
		DecodedBotServerPacket Movie(bool canSkip) => Packet<SM_PLAY_MOVIE>(("isMovie", false), ("objectId", 7),
			("questId", 2008), ("cutsceneId", 57), ("canSkip", canSkip));
		// Skip (default): answered immediately, nothing held — the Ishalgen behaviour.
		var skip = new BotReflexes();
		Assert.Equal(BotMovieMode.Skip, skip.MoviePolicy.Mode);
		Assert.NotNull(skip.RespondTo(Movie(canSkip: true)));
		Assert.Null(skip.PendingMovie);
		// An unskippable movie is watched even in skip mode.
		Assert.Null(skip.RespondTo(Movie(canSkip: false)));
		Assert.Equal(BotMoviePolicy.DefaultLength, skip.PendingMovie!.Length);
		// Watch: held for the configured length, then handed over once.
		var watch = new BotReflexes(moviePolicy: new BotMoviePolicy(BotMovieMode.Watch,
			new Dictionary<int, TimeSpan> { [57] = TimeSpan.FromSeconds(42) }));
		Assert.Null(watch.RespondTo(Movie(canSkip: true)));
		BotPendingMovie held = watch.TakePendingMovie()!;
		Assert.Equal((57, 2008, TimeSpan.FromSeconds(42)), (held.MovieId, held.QuestId, held.Length));
		Assert.Equal(typeof(CM_PLAY_MOVIE_END), held.End.PacketType);
		Assert.Null(watch.TakePendingMovie());
	}

	[Fact]
	public void AWatchedMovieBlocksMovementUntilItsEndIsSent()
	{
		var api = new BotApi(reflexes: new BotReflexes(moviePolicy: new BotMoviePolicy(BotMovieMode.Watch)));
		Assert.Null(api.Observe(Packet<SM_PLAY_MOVIE>(("isMovie", false), ("objectId", 7), ("questId", 2009),
			("cutsceneId", 121), ("canSkip", true))));
		Assert.Contains(BotBlockingActivity.Cutscene, api.Timing.BlockingActivities);
		Assert.Throws<InvalidOperationException>(() => api.Timing.EnsureCanMove());
		BotPendingMovie finished = api.FinishPendingMovie()!;
		Assert.Equal(121, finished.MovieId);
		Assert.Empty(api.Timing.BlockingActivities);
		api.Timing.EnsureCanMove();
		// Skip mode never blocks: the reflex's end goes out with the reply.
		var skipping = new BotApi();
		Assert.NotNull(skipping.Observe(Packet<SM_PLAY_MOVIE>(("isMovie", false), ("objectId", 7), ("questId", 2009),
			("cutsceneId", 121), ("canSkip", true))));
		Assert.Empty(skipping.Timing.BlockingActivities);
	}

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
