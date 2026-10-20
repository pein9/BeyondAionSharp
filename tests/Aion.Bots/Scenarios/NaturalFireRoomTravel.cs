using System.Text.Json;
using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Controllers.Movement;

namespace Aion.Bots.Scenarios;

/// <summary>The operator's 4.8 Fire Room approach and turning descent, compiled by tools/recording/compile-fire-room-route.py.
/// The recording used a GM account; clearance, ordinary flight zones and a finite FP budget are checked afresh.</summary>
public sealed class NaturalFireRoomTravel
{
	public const float LaunchRadius = 2;
	private const int MaximumLaunchSettles = 8;
	public sealed record Point(int Sequence, long Milliseconds, float X, float Y, float Z, byte Heading, byte Mask, byte GlideFlag)
	{
		public BotPosition At => new(X, Y, Z, Heading);
	}
	public sealed record Route(int MapId, Point[] Points);
	private sealed record Routes(int Schema, Route Entrance, Route Descent);
	private readonly INaturalJourneySession session;
	private readonly NaturalJourneyRuntime runtime;
	private readonly Func<BotNavigationGeometry> geometry;
	private readonly Routes routes;
	public NaturalFireRoomTravel(INaturalJourneySession session, NaturalJourneyRuntime runtime, Func<BotNavigationGeometry> geometry)
	{
		this.session = session;
		this.runtime = runtime;
		this.geometry = geometry;
		routes = JsonSerializer.Deserialize<Routes>(File.ReadAllText(Path.Combine(runtime.RepoRoot,
			"parity-artifacts/e2e/natural-fire-room-route.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
			?? throw new InvalidDataException("Missing recorded Fire Room routes.");
		if (routes.Schema != 1 || routes.Entrance.MapId != 220020000 || routes.Descent.MapId != 320050000 ||
			routes.Entrance.Points.Length < 2 || routes.Descent.Points.Length < 2)
			throw new InvalidDataException("Invalid recorded Fire Room routes.");
	}

	public BotPosition EntranceStart => routes.Entrance.Points[0].At;
	public BotPosition EntranceLanding => routes.Entrance.Points[^1].At;
	public BotPosition DescentStart => routes.Descent.Points[0].At;
	public BotPosition DescentLanding => routes.Descent.Points[^1].At;
	public float DescentMeters => routes.Descent.Points.Zip(routes.Descent.Points.Skip(1),
		(a, b) => NaturalFlightPolicy.Distance(a.At, b.At)).Sum();

	/// <summary>Only the upper Fire floor to the lower Fire floor; never intercept another room or an upward trip.</summary>
	public bool HandlesDescent(int map, BotPosition goal) => map == routes.Descent.MapId &&
		session.CurrentPosition.X < 140 && session.CurrentPosition.Y < 130 && session.CurrentPosition.Z > 605 &&
		goal.X > 170 && goal.X < 300 && goal.Y > 150 && goal.Y < 310 && goal.Z is > 540 and < 565;

	public async Task<bool> EnterAsync(Func<BotPosition, Task<bool>> walk, Func<Task<bool>> settle, CancellationToken token)
	{
		Route route = routes.Entrance;
		if (session.Api.World.MapId != route.MapId || session.Api.World.IsDead) return false;
		// The bot may already be on the Fire portal's side after an earlier attempt.
		if (NaturalFlightPolicy.Distance(session.CurrentPosition, route.Points[^1].At) < 3) return true;
		if (!await PrepareAtStartAsync(route, walk, settle)) return false;
		BotNavigationGeometry geo = geometry();
		IReadOnlyList<NaturalFlyZone> zones = NaturalFlyZone.ForMap(runtime.RepoRoot, runtime.Data.WorldMaps2.GetTemplate(route.MapId), route.MapId);
		CheckClear(route, geo);
		// Check every sampled point of every segment, not just its endpoints. Java ZoneInstance uses the feet Z.
		foreach (var pair in route.Points.Zip(route.Points.Skip(1)))
		{
			int count = Math.Max(1, (int)MathF.Ceiling(NaturalFlightPolicy.Distance(pair.First.At, pair.Second.At)));
			for (int i = 0; i <= count; i++)
			{
				BotPosition at = Between(pair.First.At, pair.Second.At, (float)i / count);
				if (!zones.Any(zone => !zone.Forbids && zone.Contains(at.X, at.Y, at.Z)) ||
					zones.Any(zone => zone.Forbids && zone.Contains(at.X, at.Y, at.Z)))
					throw new InvalidDataException($"Recorded Fire entrance leaves the ordinary FLY zone at {at}.");
			}
		}
		// Budget conservatively at ground speed before takeoff; replan at the announced flight speed afterward.
		float speed = session.Api.World.MovementSpeed ?? 0;
		if (speed <= 0 || !await ReadyAsync(route, Needed(route, speed, false), () => PrepareAtStartAsync(route, walk, settle), token)) return false;
		speed = await NaturalFlightProtocol.TakeOffAsync(session, token);
		if (session.Api.World.CurrentFlightTime < Needed(route, speed, false))
		{
			await NaturalFlightProtocol.LandAsync(session, token);
			return false;
		}
		await session.ExecuteMovementAsync(Plan(route, speed, false), token);
		await NaturalFlightProtocol.LandAsync(session, token);
		await session.SynchronizeAsync(token);
		return Finish(route, "fire-entrance-flight");
	}

	public async Task<bool> DescendAsync(Func<BotPosition, Task<bool>> walk, Func<Task<bool>> settle, CancellationToken token)
	{
		Route route = routes.Descent;
		if (!await PrepareAtStartAsync(route, walk, settle)) return false;
		CheckClear(route, geometry());
		float speed = session.Api.World.MovementSpeed ?? 0;
		if (speed <= 0 || !await ReadyAsync(route, Needed(route, speed, true), () => PrepareAtStartAsync(route, walk, settle), token)) return false;
		speed = session.Api.World.MovementSpeed ?? 0;
		if (speed <= 0 || session.Api.World.CurrentFlightTime < Needed(route, speed, true)) return false;
		int firstGlide = Array.FindIndex(route.Points, point => (point.Mask & MovementMask.GLIDE) != 0);
		if (firstGlide < 1) throw new InvalidDataException("Recorded Fire descent has no jump before gliding.");
		if (!NaturalFlightProtocol.IsClear(geometry(), route.MapId, session.CurrentPosition, route.Points[firstGlide].At))
			throw new InvalidDataException("Recorded Fire jump is blocked.");
		// Walk to the recorded brink, jump off, then keep GLIDE set through all the turns and upwind samples.
		var mover = new BotMover(session.Api.World, session.Api.Timing);
		await session.ExecuteMovementAsync(mover.CreateJumpPlan([route.Points[firstGlide].At], session.CurrentPosition, speed), token);
		await session.ExecuteMovementAsync(Plan(route with { Points = route.Points[firstGlide..] }, speed, true), token);
		await session.SynchronizeAsync(token);
		return Finish(route, "fire-recorded-descent");
	}

	private bool AtStart(Route route) => !session.Api.World.IsDead && session.Api.World.CurrentHp > 0 && session.Api.World.MapId == route.MapId &&
		NaturalFlightPolicy.Distance(session.CurrentPosition, route.Points[0].At) <= LaunchRadius;

	private async Task<bool> PrepareAtStartAsync(Route route, Func<BotPosition, Task<bool>> walk, Func<Task<bool>> settle)
	{
		for (int attempt = 0; attempt < MaximumLaunchSettles; attempt++)
		{
			if (session.Api.World.MapId != route.MapId || session.Api.World.IsDead || !await walk(route.Points[0].At)) return false;
			if (!await settle()) return false;
			if (AtStart(route)) return true;
			// Defense, rest and the required corpse-loot sweep can move away from the brink. Finish that work,
			// then walk back; never glide from the last corpse's position or skip its loot to keep the launch fixed.
			if (session.Api.World.MapId != route.MapId || session.Api.World.IsDead || session.Api.World.CurrentHp <= 0) return false;
			session.TraceDiagnostic("fire-launch-moved-during-settle", new Dictionary<string, object?>
			{
				["map"] = route.MapId, ["position"] = session.CurrentPosition, ["launch"] = route.Points[0].At, ["attempt"] = attempt,
			});
		}
		return false;
	}

	private void CheckClear(Route route, BotNavigationGeometry geo)
	{
		if (geo.StaticGroundAt(route.MapId, route.Points[^1].At) is not { } ground ||
			MathF.Abs(ground.Z - route.Points[^1].Z) > 1)
			throw new InvalidDataException("Recorded Fire route no longer ends on static ground.");
		foreach (var pair in route.Points.Zip(route.Points.Skip(1)))
			if (!NaturalFlightProtocol.IsClear(geo, route.MapId, pair.First.At, pair.Second.At))
				throw new InvalidDataException($"Recorded Fire route is blocked before source sequence {pair.Second.Sequence}.");
		if (!NaturalFlightProtocol.IsClear(geo, route.MapId, session.CurrentPosition, route.Points[0].At))
			throw new InvalidDataException("Recorded Fire route start is blocked.");
	}

	private static int Needed(Route route, float speed, bool glide) =>
		checked((int)Math.Ceiling(Plan(route, speed, glide).Duration.TotalSeconds) * (glide ? 2 : 1) +
			(glide ? 5 : NaturalFlightPolicy.LandingReserveFp)); // The same five-FP reserve as NaturalFloorGlide.

	private async Task<bool> ReadyAsync(Route route, int need, Func<Task<bool>> prepare, CancellationToken token)
	{
		if (need > session.Api.World.MaxFlightTime)
		{
			session.TraceDiagnostic("fire-route-fp-refused", new Dictionary<string, object?> { ["need"] = need, ["max"] = session.Api.World.MaxFlightTime });
			return false;
		}
		// Stay on the launch floor and defend between recovery ticks. Never wait while already airborne.
		for (int tick = 0; tick < 100 && session.Api.World.CurrentFlightTime < need; tick++)
		{
			await session.AdvanceAsync(TimeSpan.FromSeconds(3), token);
			await session.SynchronizeAsync(token);
			if (!await prepare()) return false;
		}
		// Java rejects a second glide inside the ten-second flight reuse window.
		await session.AdvanceAsync(TimeSpan.FromSeconds(10), token);
		return await prepare() && AtStart(route) && session.Api.World.CurrentFlightTime >= need;
	}

	private bool Finish(Route route, string diagnostic)
	{
		bool landed = !session.Api.World.IsDead && session.Api.World.MapId == route.MapId &&
			NaturalFlightPolicy.Distance(session.CurrentPosition, route.Points[^1].At) < 2;
		session.TraceDiagnostic(diagnostic, new Dictionary<string, object?>
		{
			["landed"] = landed, ["position"] = session.CurrentPosition, ["fp"] = session.Api.World.CurrentFlightTime,
			["hp"] = session.Api.World.CurrentHp, ["sourceStart"] = route.Points[0].Sequence, ["sourceEnd"] = route.Points[^1].Sequence,
		});
		return landed;
	}

	/// <summary>Never faster than either the observed client timing or the bot's own speed. Absolute glide samples preserve
	/// the recording's upwind flags; the short final drop uses FALL, so Java still applies ordinary fall damage.</summary>
	private static BotMovementPlan Plan(Route route, float speed, bool glide)
	{
		if (!float.IsFinite(speed) || speed <= 0) throw new ArgumentOutOfRangeException(nameof(speed));
		var frames = new List<BotMovementFrame>();
		TimeSpan total = TimeSpan.Zero;
		float meters = 0;
		foreach (var pair in route.Points.Zip(route.Points.Skip(1)))
		{
			BotPosition from = pair.First.At, to = pair.Second.At;
			float distance = NaturalFlightPolicy.Distance(from, to);
			TimeSpan duration = TimeSpan.FromSeconds(Math.Max(distance / speed, (pair.Second.Milliseconds - pair.First.Milliseconds) / 1000d));
			bool gliding = glide && (pair.First.Mask & MovementMask.GLIDE) != 0;
			byte mask = (byte)(MovementMask.POSITION | MovementMask.ABSOLUTE | (gliding ? MovementMask.GLIDE : glide && to.Z < from.Z - .05f ? MovementMask.FALL : 0));
			frames.Add(new(TimeSpan.Zero, GameClientPackets.Move(new MovementPacketData(from.X, from.Y, from.Z, from.Heading,
				(byte)(mask | MovementMask.MANUAL), X2: to.X, Y2: to.Y, Z2: to.Z, GlideFlag: pair.First.GlideFlag)), from));
			int pieces = Math.Max(1, (int)Math.Ceiling(duration.TotalMilliseconds / 500));
			for (int i = 1; i <= pieces; i++)
			{
				BotPosition at = Between(from, to, (float)i / pieces);
				frames.Add(new(duration / pieces, GameClientPackets.Move(new MovementPacketData(at.X, at.Y, at.Z, at.Heading, mask,
					GlideFlag: pair.First.GlideFlag)), at));
			}
			total += duration;
			meters += distance;
		}
		BotPosition end = route.Points[^1].At;
		frames.Add(new(TimeSpan.Zero, GameClientPackets.Move(new MovementPacketData(end.X, end.Y, end.Z, end.Heading, MovementMask.IMMEDIATE)), end));
		return new(frames, total, meters);
	}

	private static BotPosition Between(BotPosition a, BotPosition b, float t) =>
		new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t, b.Heading);
}
