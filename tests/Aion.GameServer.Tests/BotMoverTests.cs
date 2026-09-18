using System.Buffers.Binary;
using Aion.Bots.Movement;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotMoverTests
{
	[Fact]
	public void GroundPlanStartsWithTargetUpdatesAtObservedSpeedAndStops()
	{
		var mover = new BotMover(WorldAt(new BotPosition(0, 0, 0, 0), 4));
		var plan = mover.CreateGroundPlan([new BotPosition(10, 0, 5, 0)]);

		Assert.Equal(TimeSpan.FromSeconds(Math.Sqrt(125) / 4), plan.Duration);
		Assert.Equal(MathF.Sqrt(125), plan.Distance, 4);
		Assert.IsType<BotClientPacket>(plan.Frames[0].Packet);
		Assert.Equal(typeof(CM_MOVE), plan.Frames[0].Packet.PacketType);
		Assert.Equal((byte)(MovementMask.POSITION | MovementMask.MANUAL | MovementMask.ABSOLUTE),
			plan.Frames[0].Packet.Body[13]);
		Assert.Equal(10f, BitConverter.ToSingle(plan.Frames[0].Packet.Body, 14));
		Assert.Equal(5f, BitConverter.ToSingle(plan.Frames[0].Packet.Body, 22));
		Assert.Equal(MovementMask.IMMEDIATE, plan.Frames[^1].Packet.Body[13]);
		Assert.Equal(new BotPosition(10, 0, 5, 0), plan.Frames[^1].Position with { Heading = 0 });

		var previous = new BotPosition(0, 0, 0, 0);
		foreach (var frame in plan.Frames.Where(frame => frame.DelayBefore > TimeSpan.Zero))
		{
			Assert.InRange(Distance(previous, frame.Position), 0,
				(float)(4 * frame.DelayBefore.TotalSeconds) + 0.0001f);
			Assert.InRange(frame.DelayBefore, TimeSpan.Zero, BotMover.DefaultUpdateInterval);
			previous = frame.Position;
		}
		Assert.Equal(plan.Duration, TimeSpan.FromTicks(plan.Frames.Sum(frame => frame.DelayBefore.Ticks)));
	}

	[Fact]
	public void JumpAndFallUseTheJavaMovementMasks()
	{
		var jump = new BotMover(WorldAt(new BotPosition(0, 0, 0, 0), 5))
			.CreateJumpPlan([new BotPosition(3, 0, 4, 0)]);
		Assert.Equal(typeof(CM_EMOTION), jump.Frames[0].Packet.PacketType);
		Assert.Equal((byte)EmotionType.JUMP, jump.Frames[0].Packet.Body[0]);
		Assert.Equal((byte)(MovementMask.POSITION | MovementMask.MANUAL), jump.Frames[1].Packet.Body[13]);
		Assert.Equal(4f, BitConverter.ToSingle(jump.Frames[1].Packet.Body, 22));
		Assert.All(jump.Frames.Where(frame => frame.DelayBefore > TimeSpan.Zero),
			frame => Assert.Equal(MovementMask.POSITION, frame.Packet.Body[13]));

		var fall = new BotMover(WorldAt(new BotPosition(0, 0, 10, 0), 5))
			.CreateFallPlan([new BotPosition(0, 0, 0, 0)]);
		Assert.Equal((byte)(MovementMask.POSITION | MovementMask.MANUAL | MovementMask.ABSOLUTE | MovementMask.FALL),
			fall.Frames[0].Packet.Body[13]);
		Assert.All(fall.Frames.Where(frame => frame.DelayBefore > TimeSpan.Zero), frame =>
			Assert.Equal((byte)(MovementMask.POSITION | MovementMask.ABSOLUTE | MovementMask.FALL),
				frame.Packet.Body[13]));
	}

	[Fact]
	public void FlightUsesMoveInAirWithCumulativeDistance()
	{
		var plan = new BotMover(WorldAt(new BotPosition(0, 0, 0, 0), 4))
			.CreateFlightPlan([new BotPosition(0, 0, 8, 0)]);

		Assert.NotEmpty(plan.Frames);
		Assert.All(plan.Frames, frame => Assert.Equal(typeof(CM_MOVE_IN_AIR), frame.Packet.PacketType));
		Assert.All(plan.Frames, frame => Assert.Equal(220010000,
			BinaryPrimitives.ReadInt32LittleEndian(frame.Packet.Body)));
		Assert.Equal(8, BinaryPrimitives.ReadInt32LittleEndian(plan.Frames[^1].Packet.Body.AsSpan(17)));
		Assert.Equal(TimeSpan.FromSeconds(2), plan.Duration);
	}

	[Fact]
	public void LatestPositiveEmotionSpeedControlsPacing()
	{
		var world = WorldAt(new BotPosition(0, 0, 0, 0), 4);
		world.Apply(Packet<SM_EMOTION>(
			("senderObjectId", 100), ("emotionType", (byte)EmotionType.CHANGE_SPEED),
			("state", (ushort)0), ("movementSpeed", 2f)));
		var slowed = new BotMover(world).CreateGroundPlan([new BotPosition(10, 0, 0, 0)]);
		Assert.Equal(TimeSpan.FromSeconds(5), slowed.Duration);

		world.Apply(Packet<SM_EMOTION>(
			("senderObjectId", 100), ("emotionType", (byte)EmotionType.STAND),
			("state", (ushort)0), ("movementSpeed", 0f)));
		Assert.Equal(2f, world.MovementSpeed);
	}

	[Fact]
	public async Task ExecutionUsesEveryPlannedDelayAndPacketInOrder()
	{
		var plan = new BotMover(WorldAt(new BotPosition(0, 0, 0, 0), 10))
			.CreateGroundPlan([new BotPosition(10, 0, 0, 0)]);
		var sent = new List<BotClientPacket>();
		var delays = new List<TimeSpan>();

		await BotMover.ExecuteAsync(plan,
			(packet, _) =>
			{
				sent.Add(packet);
				return ValueTask.CompletedTask;
			},
			(delay, _) =>
			{
				delays.Add(delay);
				return ValueTask.CompletedTask;
			});

		Assert.Equal(plan.Frames.Select(frame => frame.Packet), sent);
		Assert.Equal(plan.Frames.Where(frame => frame.DelayBefore > TimeSpan.Zero)
			.Select(frame => frame.DelayBefore), delays);
	}

	private static BotWorldModel WorldAt(BotPosition position, float movementSpeed)
	{
		var world = new BotWorldModel();
		world.Apply(Packet<SM_PLAYER_INFO>(
			("x", position.X), ("y", position.Y), ("z", position.Z), ("heading", position.Heading),
			("objectId", 100), ("race", (byte)0), ("playerClass", (byte)0), ("state", (ushort)0),
			("name", "Mover"), ("movementSpeed", movementSpeed)));
		world.Apply(Packet<SM_STATS_INFO>(
			("objectId", 100), ("level", (ushort)1), ("expNeeded", 400L), ("expRecoverable", 0L),
			("expShown", 0L), ("maxHp", 100), ("currentHp", 100), ("maxMp", 100), ("currentMp", 100),
			("maxDp", (ushort)4000), ("dp", (ushort)0), ("maxFp", 60), ("currentFp", 60)));
		world.Apply(Packet<SM_PLAYER_SPAWN>(
			("worldId", 220010000), ("x", position.X), ("y", position.Y), ("z", position.Z),
			("heading", position.Heading)));
		return world;
	}

	private static DecodedBotServerPacket Packet<T>(params (string Name, object? Value)[] fields) =>
		new(typeof(T), fields.ToDictionary(field => field.Name, field => field.Value, StringComparer.Ordinal));

	private static float Distance(BotPosition left, BotPosition right)
	{
		var x = left.X - right.X;
		var y = left.Y - right.Y;
		var z = left.Z - right.Z;
		return MathF.Sqrt(x * x + y * y + z * z);
	}
}
