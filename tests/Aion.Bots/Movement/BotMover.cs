using Aion.Bots.Protocol;
using Aion.Bots.Timing;
using Aion.Bots.World;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Utils;

namespace Aion.Bots.Movement;

public enum BotMovementMode
{
	Ground,
	Jump,
	Fall,
	Glide,
}

public sealed record BotMovementFrame(TimeSpan DelayBefore, BotClientPacket Packet, BotPosition Position);

public sealed record BotMovementPlan(IReadOnlyList<BotMovementFrame> Frames, TimeSpan Duration, float Distance);

/// <summary>Builds and executes honest, speed-paced client movement streams.</summary>
public sealed class BotMover
{
	public static readonly TimeSpan DefaultUpdateInterval = TimeSpan.FromMilliseconds(500);

	private readonly BotWorldModel world;
	private readonly BotTimingContract timing;

	public BotMover(BotWorldModel world, BotTimingContract? timing = null)
	{
		this.world = world ?? throw new ArgumentNullException(nameof(world));
		this.timing = timing ?? new BotTimingContract();
	}

	public BotMovementPlan CreateGroundPlan(IReadOnlyList<BotPosition> route,
		TimeSpan? updateInterval = null) => CreateMovePlan(route, BotMovementMode.Ground, updateInterval, null, null);

	public BotMovementPlan CreateGroundPlan(IReadOnlyList<BotPosition> route, BotPosition start, float speed,
		TimeSpan? updateInterval = null) => CreateMovePlan(route, BotMovementMode.Ground, updateInterval, start, speed);

	public BotMovementPlan CreateJumpPlan(IReadOnlyList<BotPosition> route,
		TimeSpan? updateInterval = null) => CreateMovePlan(route, BotMovementMode.Jump, updateInterval, null, null);

	public BotMovementPlan CreateJumpPlan(IReadOnlyList<BotPosition> route, BotPosition start, float speed,
		TimeSpan? updateInterval = null) => CreateMovePlan(route, BotMovementMode.Jump, updateInterval, start, speed);

	public BotMovementPlan CreateFallPlan(IReadOnlyList<BotPosition> route,
		TimeSpan? updateInterval = null) => CreateMovePlan(route, BotMovementMode.Fall, updateInterval, null, null);

	public BotMovementPlan CreateFallPlan(IReadOnlyList<BotPosition> route, BotPosition start, float speed,
		TimeSpan? updateInterval = null) => CreateMovePlan(route, BotMovementMode.Fall, updateInterval, start, speed);

	public BotMovementPlan CreateGlidePlan(IReadOnlyList<BotPosition> route, BotPosition start, float speed,
		TimeSpan? updateInterval = null) => CreateMovePlan(route, BotMovementMode.Glide, updateInterval, start, speed);

	public BotMovementPlan CreateFlightPlan(IReadOnlyList<BotPosition> route,
		TimeSpan? updateInterval = null)
	{
		timing.EnsureCanMove();
		var (start, speed, interval) = Validate(route, updateInterval);
		var mapId = world.MapId ?? throw new InvalidOperationException("The bot has not observed its map yet.");
		var frames = new List<BotMovementFrame>();
		var current = start;
		var duration = TimeSpan.Zero;
		var distance = 0f;
		foreach (var destination in route)
		{
			var segmentDistance = Distance(current, destination);
			if (segmentDistance <= 0)
				continue;
			var heading = PositionUtil.GetHeadingTowards(current.X, current.Y, destination.X, destination.Y);
			AddSamples(frames, current, destination, heading, speed, interval, ref duration, ref distance,
				(position, cumulativeDistance) => GameClientPackets.MoveInAir(mapId, position.X, position.Y,
					position.Z, position.Heading,
					checked((int)MathF.Round(cumulativeDistance, MidpointRounding.AwayFromZero))));
			current = destination with { Heading = heading };
		}
		return new BotMovementPlan(frames, duration, distance);
	}

	public static async ValueTask ExecuteAsync(BotMovementPlan plan,
		Func<BotClientPacket, CancellationToken, ValueTask> send,
		Func<TimeSpan, CancellationToken, ValueTask>? delay = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(plan);
		ArgumentNullException.ThrowIfNull(send);
		delay ??= DefaultDelayAsync;
		foreach (var frame in plan.Frames)
		{
			if (frame.DelayBefore > TimeSpan.Zero)
				await delay(frame.DelayBefore, cancellationToken);
			await send(frame.Packet, cancellationToken);
		}
	}

	private BotMovementPlan CreateMovePlan(IReadOnlyList<BotPosition> route, BotMovementMode mode,
		TimeSpan? updateInterval, BotPosition? explicitStart, float? explicitSpeed)
	{
		timing.EnsureCanMove();
		var (start, speed, interval) = Validate(route, updateInterval, explicitStart, explicitSpeed);
		var frames = new List<BotMovementFrame>();
		var current = start;
		var duration = TimeSpan.Zero;
		var distance = 0f;
		if (mode is BotMovementMode.Jump or BotMovementMode.Glide)
			frames.Add(new BotMovementFrame(TimeSpan.Zero,
				GameClientPackets.Emotion((byte)(mode == BotMovementMode.Jump ? EmotionType.JUMP : EmotionType.START_GLIDE)), current));

		foreach (var destination in route)
		{
			var segmentDistance = Distance(current, destination);
			if (segmentDistance <= 0)
				continue;
			var heading = PositionUtil.GetHeadingTowards(current.X, current.Y, destination.X, destination.Y);
			frames.Add(new BotMovementFrame(TimeSpan.Zero,
				GameClientPackets.Move(StartPacket(current, destination, heading, speed, segmentDistance, mode)), current));
			var periodicMask = mode switch
			{
				BotMovementMode.Fall => (byte)(MovementMask.POSITION | MovementMask.ABSOLUTE | MovementMask.FALL),
				BotMovementMode.Glide => (byte)(MovementMask.POSITION | MovementMask.ABSOLUTE | MovementMask.GLIDE),
				BotMovementMode.Jump => MovementMask.POSITION,
				_ => (byte)(MovementMask.POSITION | MovementMask.ABSOLUTE),
			};
			AddSamples(frames, current, destination, heading, speed, interval, ref duration, ref distance,
				(position, _) => GameClientPackets.Move(new MovementPacketData(position.X, position.Y, position.Z,
					position.Heading, periodicMask)));
			current = destination with { Heading = heading };
		}

		if (frames.Count > (mode is BotMovementMode.Jump or BotMovementMode.Glide ? 1 : 0))
			frames.Add(new BotMovementFrame(TimeSpan.Zero,
				GameClientPackets.Move(new MovementPacketData(current.X, current.Y, current.Z, current.Heading,
					MovementMask.IMMEDIATE)), current));
		return new BotMovementPlan(frames, duration, distance);
	}

	private (BotPosition Start, float Speed, TimeSpan Interval) Validate(IReadOnlyList<BotPosition> route,
		TimeSpan? updateInterval, BotPosition? explicitStart = null, float? explicitSpeed = null)
	{
		ArgumentNullException.ThrowIfNull(route);
		if (explicitStart is not BotPosition start && world.Position is not BotPosition)
			throw new InvalidOperationException("The bot has not observed its position yet.");
		start = explicitStart ?? world.Position!.Value;
		float? selectedSpeed = explicitSpeed ?? world.MovementSpeed;
		if (selectedSpeed is not float speed || !float.IsFinite(speed) || speed <= 0)
			throw new InvalidOperationException("The bot has not observed a positive movement speed yet.");
		var interval = updateInterval ?? DefaultUpdateInterval;
		if (interval <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(updateInterval), "The update interval must be positive.");
		return (start, speed, interval);
	}

	private static MovementPacketData StartPacket(BotPosition start, BotPosition destination, byte heading,
		float speed, float distance, BotMovementMode mode)
	{
		if (mode == BotMovementMode.Jump)
		{
			return new MovementPacketData(start.X, start.Y, start.Z, heading,
				(byte)(MovementMask.POSITION | MovementMask.MANUAL),
				VectorX: (destination.X - start.X) / distance * speed,
				VectorY: (destination.Y - start.Y) / distance * speed,
				VectorZ: (destination.Z - start.Z) / distance * speed);
		}

		var mask = (byte)(MovementMask.POSITION | MovementMask.MANUAL | MovementMask.ABSOLUTE);
		if (mode == BotMovementMode.Fall)
			mask |= MovementMask.FALL;
		else if (mode == BotMovementMode.Glide)
			mask |= MovementMask.GLIDE;
		return new MovementPacketData(start.X, start.Y, start.Z, heading, mask,
			X2: destination.X, Y2: destination.Y, Z2: destination.Z);
	}

	private static void AddSamples(List<BotMovementFrame> frames, BotPosition start, BotPosition destination,
		byte heading, float speed, TimeSpan interval, ref TimeSpan totalDuration, ref float totalDistance,
		Func<BotPosition, float, BotClientPacket> packet)
	{
		var segmentDistance = Distance(start, destination);
		var segmentDuration = TimeSpan.FromSeconds(segmentDistance / speed);
		var elapsed = TimeSpan.Zero;
		while (elapsed < segmentDuration)
		{
			var next = elapsed + interval < segmentDuration ? elapsed + interval : segmentDuration;
			var fraction = (float)(next.TotalSeconds / segmentDuration.TotalSeconds);
			var position = new BotPosition(
				start.X + (destination.X - start.X) * fraction,
				start.Y + (destination.Y - start.Y) * fraction,
				start.Z + (destination.Z - start.Z) * fraction,
				heading);
			var covered = segmentDistance * fraction;
			frames.Add(new BotMovementFrame(next - elapsed, packet(position, totalDistance + covered), position));
			elapsed = next;
		}
		totalDuration += segmentDuration;
		totalDistance += segmentDistance;
	}

	private static float Distance(BotPosition left, BotPosition right)
	{
		var x = left.X - right.X;
		var y = left.Y - right.Y;
		var z = left.Z - right.Z;
		return MathF.Sqrt(x * x + y * y + z * z);
	}

	private static async ValueTask DefaultDelayAsync(TimeSpan delay, CancellationToken cancellationToken)
	{
		await Task.Delay(delay, cancellationToken);
	}
}
