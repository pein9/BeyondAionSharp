using System.Text.Json;
using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Controllers.Movement;

namespace Aion.Bots.Scenarios;

public sealed record NaturalElevatorKey(long Millis, float X, float Y, float Z)
{
	public BotPosition Position => new(X, Y, Z, 0);
}

/// <summary>
/// The 4.8 client's automatic wooden elevator behind Gesta, separate from portal-dialog Tower Lift.
/// Generated from mission_mission0.xml and the CGA position controller, not the static server navmesh.
/// Java CM_MOVE accepts client world-position samples; no server teleport, flight state or invented vehicle IDs.
/// A full rendered-client animation-phase/vehicle-metadata comparison remains part of later LIVE acceptance.
/// </summary>
public sealed record NaturalHaramelElevator(int MapId, long PeriodMillis, NaturalElevatorKey[] Keys, float[][] FloorBounds)
{
	public const string RelativePath = "parity-artifacts/e2e/natural-haramel-elevator.json";
	public BotPosition Bottom => Keys[0].Position;
	public BotPosition Top => Keys[2].Position;
	public long AscentStarts => Keys[1].Millis;
	public long AscentEnds => Keys[2].Millis;
	public long DescentStarts => Keys[3].Millis;
	public long DescentEnds => Keys[4].Millis;

	public static NaturalHaramelElevator Load(string root)
	{
		var result = JsonSerializer.Deserialize<NaturalHaramelElevator>(File.ReadAllText(Path.Combine(root, RelativePath)),
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("No Haramel elevator.");
		if (result.MapId != 300200000 || result.Keys.Length != 5 || result.FloorBounds.Length != 2 ||
			result.FloorBounds.Any(b => b.Length != 2 || b[0] >= 0 || b[1] <= 0) || result.PeriodMillis <= result.Keys[^1].Millis ||
			result.Keys[0].Millis != 0 || result.Keys.Zip(result.Keys.Skip(1)).Any(p => p.First.Millis >= p.Second.Millis) ||
			result.Keys[0].Position != result.Keys[1].Position || result.Keys[2].Position != result.Keys[3].Position ||
			result.Keys[0].Position != result.Keys[4].Position || result.Top.Z <= result.Bottom.Z)
			throw new InvalidDataException("Unexpected Haramel elevator animation; regenerate from the client.");
		return result;
	}

	/// <summary>One synchronized client-animation cycle. SIM's client clock supplies the phase.</summary>
	public long NextBottomCycle(long nowMillis)
	{
		long cycle = nowMillis - nowMillis % PeriodMillis;
		return nowMillis == cycle ? cycle : checked(cycle + PeriodMillis);
	}

	public BotPosition At(long elapsedMillis)
	{
		long phase = elapsedMillis % PeriodMillis;
		if (phase < 0) phase += PeriodMillis;
		if (phase >= Keys[^1].Millis) return Bottom;
		for (int i = 1; i < Keys.Length; i++)
		{
			if (phase > Keys[i].Millis) continue;
			NaturalElevatorKey a = Keys[i - 1], b = Keys[i];
			float t = (float)(phase - a.Millis) / (b.Millis - a.Millis);
			// All shipped TCB3 tensions are 1: both Hermite tangents are zero; ease/continuity/bias are 0.
			float s = t * t * (3 - 2 * t);
			return new(a.X + (b.X - a.X) * s, a.Y + (b.Y - a.Y) * s, a.Z + (b.Z - a.Z) * s, 0);
		}
		throw new InvalidDataException("No elevator animation key covers the phase.");
	}

	/// <summary>During the upper stop, use the real client platform floor until the static landing supports each step.</summary>
	public IReadOnlyList<BotPosition> Disembark(BotNavigationGeometry geometry, BotPosition destination)
	{
		float dx = destination.X - Top.X, dy = destination.Y - Top.Y;
		float horizontal = MathF.Sqrt(dx * dx + dy * dy);
		if (horizontal > 6 || horizontal <= 0) return [];
		int steps = (int)MathF.Ceiling(horizontal / .5f);
		List<BotPosition> path = [];
		BotPosition previous = Top;
		for (int i = 1; i <= steps; i++)
		{
			float t = (float)i / steps;
			BotPosition sample = new(Top.X + dx * t, Top.Y + dy * t, Top.Z, destination.Heading);
			bool onPlatform = sample.X - Top.X >= FloorBounds[0][0] && sample.X - Top.X <= FloorBounds[0][1] &&
				sample.Y - Top.Y >= FloorBounds[1][0] && sample.Y - Top.Y <= FloorBounds[1][1];
			BotPosition? supported = onPlatform ? sample : geometry.StaticGroundAt(MapId, sample);
			if (supported is not BotPosition ground || MathF.Abs(ground.Z - previous.Z) > horizontal / steps + .05f ||
				!geometry.HasLineOfSight(MapId, previous with { Z = previous.Z - .25f }, ground with { Z = ground.Z - .25f })) return [];
			path.Add(ground);
			previous = ground;
		}
		// The endpoint must belong to static walkable ground, so onward routing no longer depends on the platform.
		return geometry.StaticGroundAt(MapId, previous) != null && geometry.NavMesh?.NavMeshes.Get(MapId)?.IslandOf(previous) >= 0 ? path : [];
	}

	/// <summary>The shipped upper landing has a small gap: a normal checked hop may replace walking across it.</summary>
	public IReadOnlyList<BotPosition> JumpDisembark(BotNavigationGeometry geometry, BotPosition destination)
	{
		float horizontal = MathF.Sqrt(MathF.Pow(destination.X - Top.X, 2) + MathF.Pow(destination.Y - Top.Y, 2));
		if (horizontal is < 1 or > 5 || geometry.StaticGroundAt(MapId, destination) is not BotPosition landing ||
			MathF.Abs(landing.Z - Top.Z) > 1 || geometry.NavMesh?.NavMeshes.Get(MapId)?.IslandOf(landing) is not >= 0) return [];
		BotPosition apex = new((Top.X + landing.X) / 2, (Top.Y + landing.Y) / 2, MathF.Max(Top.Z, landing.Z) + 1, 0);
		return NaturalFlightProtocol.IsClear(geometry, MapId, Top, apex) && NaturalFlightProtocol.IsClear(geometry, MapId, apex, landing)
			? [apex, landing] : [];
	}

	/// <summary>Client-carried position samples at the actual animation pace, ending during the upper stop.</summary>
	public BotMovementPlan Ascent(long cycleStartMillis, long nowMillis)
	{
		long elapsed = nowMillis - cycleStartMillis;
		if (elapsed < 0 || elapsed >= AscentStarts) throw new InvalidOperationException("Missed the real bottom boarding window.");
		List<BotMovementFrame> frames = [];
		long previous = elapsed;
		for (long t = elapsed; ; t = Math.Min(AscentEnds, t + 100))
		{
			BotPosition at = At(t);
			frames.Add(new(TimeSpan.FromMilliseconds(t - previous), GameClientPackets.Move(new MovementPacketData(
				at.X, at.Y, at.Z, at.Heading, (byte)(MovementMask.POSITION | MovementMask.ABSOLUTE))), at));
			if (t == AscentEnds) break;
			previous = t;
		}
		return new(frames, TimeSpan.FromMilliseconds(AscentEnds - elapsed), Top.Z - Bottom.Z);
	}

	/// <summary>Ride the same shipped controller back down after boarding during its upper stop.</summary>
	public BotMovementPlan Descent(long cycleStartMillis, long nowMillis)
	{
		long elapsed = nowMillis - cycleStartMillis;
		if (elapsed < AscentEnds || elapsed >= DescentStarts) throw new InvalidOperationException("Missed the real upper boarding window.");
		List<BotMovementFrame> frames = [];
		long previous = elapsed;
		for (long t = elapsed; ; t = Math.Min(DescentEnds, t + 100))
		{
			BotPosition at = At(t);
			frames.Add(new(TimeSpan.FromMilliseconds(t - previous), GameClientPackets.Move(new MovementPacketData(
				at.X, at.Y, at.Z, at.Heading, (byte)(MovementMask.POSITION | MovementMask.ABSOLUTE))), at));
			if (t == DescentEnds) break;
			previous = t;
		}
		return new(frames, TimeSpan.FromMilliseconds(DescentEnds - elapsed), Top.Z - Bottom.Z);
	}
}
