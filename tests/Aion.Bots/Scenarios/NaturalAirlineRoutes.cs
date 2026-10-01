using System.Text.Json;
using Aion.Bots.Movement;
using Aion.Bots.Protocol;
using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>One timed key of a client flight route.</summary>
public sealed record NaturalAirlineKey(float T, float X, float Y, float Z)
{
	public BotPosition Position => new(X, Y, Z, 0);
}

/// <summary>A flight transporter's route (npc_teleporter.xml FLIGHT location), flown by the client from its own data.</summary>
public sealed record NaturalAirlineRoute(int NpcId, int MapId, int LocationId, int TeleportId, long Price, string Route, float Seconds,
	NaturalAirlineKey[] Keys)
{
	public BotPosition Departure => Keys[0].Position;
	public BotPosition Landing => Keys[^1].Position;
}

/// <summary>
/// The maintainer's 2026-10-01 note: hubs have flight teleporters, so the natural bot flies between them instead of walking.
/// The routes are generated from the client (<c>tools/client-extract/extract_flight_routes.py</c>, do not hand-edit): Java
/// <c>TeleportService.teleport</c> sends START_FLYTELEPORT for a FLIGHT location, the client flies its route reporting
/// CM_MOVE_IN_AIR, and lands with LAND_FLYTELEPORT (<c>CM_EMOTION</c> -> <c>onFlyTeleportEnd</c>).
/// </summary>
public static class NaturalAirlineRoutes
{
	public const string RelativePath = "parity-artifacts/e2e/natural-flight-routes.json";
	private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

	public static IReadOnlyList<NaturalAirlineRoute> Load(string repoRoot)
	{
		using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(repoRoot, RelativePath)));
		return document.RootElement.GetProperty("routes").Deserialize<NaturalAirlineRoute[]>(Json)
			?? throw new InvalidDataException($"{RelativePath} has no routes.");
	}

	/// <summary>The flight worth taking toward <paramref name="destination"/>: it lands within <paramref name="landingRadius"/>
	/// of it, and walking to its pad plus walking on from its landing is shorter than walking there. Null when none is.</summary>
	public static NaturalAirlineRoute? Toward(IEnumerable<NaturalAirlineRoute> routes, int mapId, BotPosition from, BotPosition destination,
		float landingRadius = 150)
	{
		float walk = Distance(from, destination);
		return routes.Where(route => route.MapId == mapId && Distance(route.Landing, destination) <= landingRadius)
			.Select(route => (route, cost: Distance(from, route.Departure) + Distance(route.Landing, destination)))
			.Where(entry => entry.cost + 100 < walk)
			.OrderBy(entry => entry.cost).Select(entry => entry.route).FirstOrDefault();
	}

	/// <summary>The client's flight along the route's keys, a CM_MOVE_IN_AIR every 500 ms, timed as the route is.</summary>
	public static BotMovementPlan Plan(NaturalAirlineRoute route)
	{
		var frames = new List<BotMovementFrame>();
		float travelled = 0;
		BotPosition previous = route.Departure;
		long previousTicks = 0;
		for (long ms = 500; ; ms += 500)
		{
			float t = MathF.Min(ms / 1000f, route.Seconds);
			BotPosition point = At(route, t);
			travelled += Distance3(previous, point);
			long ticks = TimeSpan.FromSeconds(t).Ticks;
			frames.Add(new BotMovementFrame(TimeSpan.FromTicks(ticks - previousTicks),
				GameClientPackets.MoveInAir(route.MapId, point.X, point.Y, point.Z, point.Heading, (int)MathF.Round(travelled)), point));
			previous = point;
			previousTicks = ticks;
			if (t >= route.Seconds) break;
		}
		return new BotMovementPlan(frames, TimeSpan.FromSeconds(route.Seconds), travelled);
	}

	/// <summary>The position at <paramref name="t"/> seconds, linear between the route's keys.</summary>
	public static BotPosition At(NaturalAirlineRoute route, float t)
	{
		NaturalAirlineKey[] keys = route.Keys;
		if (t <= keys[0].T) return keys[0].Position;
		for (int i = 1; i < keys.Length; i++)
		{
			if (t > keys[i].T) continue;
			NaturalAirlineKey a = keys[i - 1], b = keys[i];
			float f = b.T > a.T ? (t - a.T) / (b.T - a.T) : 1;
			return new BotPosition(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f, a.Z + (b.Z - a.Z) * f, 0);
		}
		return keys[^1].Position;
	}

	private static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2));
	private static float Distance3(BotPosition a, BotPosition b) => MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));
}
