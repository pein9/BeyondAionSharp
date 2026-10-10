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
				_ => MovementMask.POSITION,
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
		// NR-110b: a spirit the bot has out walks behind it.
		if (mode == BotMovementMode.Ground) WeaveSpirit(frames, speed);
		return new BotMovementPlan(frames, duration, distance);
	}

	/// <summary>NR-110b: how far behind its master a spirit walks, along the master's own route.</summary>
	public const float SpiritLagMeters = 3f;

	/// <summary>NR-110b: a spirit farther from its master than this is not driven; the server releases one whose master
	/// left its sight (Java SummonController.notKnow), and the buff check summons the next.</summary>
	public const float SpiritLostMeters = 60f;

	/// <summary>
	/// NR-110b: the steps of the bot's own spirit, woven into a ground plan. The server moves no spirit (Java
	/// VisibleObjectSpawner.spawnSummon puts it beside its master and nothing moves it after): the owner's client reports
	/// its steps by CM_SUMMON_MOVE, and CM_SUMMON_MOVE.runImpl takes each position as sent. The spirit walks to where the
	/// bot stood and then the bot's route, <see cref="SpiritLagMeters"/> behind it, no faster than its own speed, and
	/// stands when the bot stands. Its start names where it will stand (the mask with ABSOLUTE; the server ignores a
	/// spirit's start without it), its samples follow the bot's, and a last frame stops it.
	/// </summary>
	private void WeaveSpirit(List<BotMovementFrame> frames, float botSpeed)
	{
		if (world.Summon is not { } spirit || !world.Objects.TryGetValue(spirit.ObjectId, out BotKnownObject? known) || frames.Count == 0)
			return;
		BotPosition from = known.SettledPosition;
		BotPosition start = frames[0].Position;
		float gap = Distance(from, start);
		if (gap > SpiritLostMeters) return;
		float spiritSpeed = known.MovementSpeed is float own && float.IsFinite(own) && own > 0 ? own : botSpeed;
		// The spirit's path: from where it stands to where the bot starts, then every position of the bot's plan.
		var path = new List<BotPosition> { from, start };
		foreach (BotMovementFrame frame in frames)
			if (Distance(path[^1], frame.Position) > 0) path.Add(frame.Position);
		var lengths = new float[path.Count];
		for (int index = 1; index < path.Count; index++) lengths[index] = lengths[index - 1] + Distance(path[index - 1], path[index]);
		BotPosition At(float distance)
		{
			int next = 1;
			while (next < path.Count - 1 && lengths[next] < distance) next++;
			float span = lengths[next] - lengths[next - 1];
			float fraction = span <= 0 ? 1 : Math.Clamp((distance - lengths[next - 1]) / span, 0, 1);
			BotPosition a = path[next - 1], b = path[next];
			return new BotPosition(a.X + (b.X - a.X) * fraction, a.Y + (b.Y - a.Y) * fraction, a.Z + (b.Z - a.Z) * fraction,
				PositionUtil.GetHeadingTowards(a.X, a.Y, b.X, b.Y));
		}
		// How far along its path the spirit is after each of the bot's frames.
		var walked = new float[frames.Count];
		float botWalked = 0, spiritWalked = 0;
		BotPosition last = start;
		for (int index = 0; index < frames.Count; index++)
		{
			botWalked += Distance(last, frames[index].Position);
			last = frames[index].Position;
			float allowed = Math.Max(0, gap + botWalked - SpiritLagMeters);
			float reachable = spiritWalked + spiritSpeed * (float)frames[index].DelayBefore.TotalSeconds;
			spiritWalked = Math.Max(spiritWalked, Math.Min(allowed, reachable));
			walked[index] = spiritWalked;
		}
		if (spiritWalked < 0.1f) return;
		BotPosition end = At(spiritWalked);
		var woven = new List<BotMovementFrame>(frames.Count * 2 + 2);
		bool started = false;
		float sent = 0;
		for (int index = 0; index < frames.Count; index++)
		{
			woven.Add(frames[index]);
			if (!started)
			{
				// The start, sent with the bot's own first packet.
				woven.Add(new BotMovementFrame(TimeSpan.Zero, GameClientPackets.SummonMove(new MovementPacketData(from.X, from.Y, from.Z,
					PositionUtil.GetHeadingTowards(from.X, from.Y, start.X, start.Y),
					(byte)(MovementMask.POSITION | MovementMask.MANUAL | MovementMask.ABSOLUTE), ObjectId: spirit.ObjectId,
					X2: end.X, Y2: end.Y, Z2: end.Z)), frames[index].Position));
				started = true;
			}
			if (walked[index] - sent < 0.05f) continue;
			sent = walked[index];
			BotPosition step = At(sent);
			woven.Add(new BotMovementFrame(TimeSpan.Zero, GameClientPackets.SummonMove(new MovementPacketData(step.X, step.Y, step.Z,
				step.Heading, MovementMask.POSITION, ObjectId: spirit.ObjectId)), frames[index].Position));
		}
		woven.Add(new BotMovementFrame(TimeSpan.Zero, GameClientPackets.SummonMove(new MovementPacketData(end.X, end.Y, end.Z, end.Heading,
			MovementMask.IMMEDIATE, ObjectId: spirit.ObjectId)), frames[^1].Position));
		frames.Clear();
		frames.AddRange(woven);
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
		if (mode is BotMovementMode.Ground or BotMovementMode.Jump)
		{
			// The recorded 4.8 client uses relative velocity for ordinary keyboard movement.
			// Ground movement follows terrain through position samples, not a vertical velocity.
			var horizontalDistance = MathF.Sqrt(MathF.Pow(destination.X - start.X, 2) +
				MathF.Pow(destination.Y - start.Y, 2));
			var velocityDistance = mode == BotMovementMode.Ground && horizontalDistance > 0
				? horizontalDistance : distance;
			return new MovementPacketData(start.X, start.Y, start.Z, heading,
				(byte)(MovementMask.POSITION | MovementMask.MANUAL),
				VectorX: (destination.X - start.X) / velocityDistance * speed,
				VectorY: (destination.Y - start.Y) / velocityDistance * speed,
				VectorZ: mode == BotMovementMode.Ground ? 0 : (destination.Z - start.Z) / distance * speed);
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
