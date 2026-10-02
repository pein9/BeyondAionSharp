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

/// <summary>One leg of a journey: a walk (<see cref="Flight"/> null) or a flight from its pad to its landing.</summary>
public sealed record NaturalAirlineLeg(NaturalAirlineRoute? Flight, BotPosition From, BotPosition To);

/// <summary>AG-00: a journey by walks and flights; <see cref="Fares"/> are the routes' base prices (before SM_PRICES).</summary>
public sealed record NaturalAirlineJourney(IReadOnlyList<NaturalAirlineLeg> Legs, float Seconds, float WalkAllSeconds, long Fares)
{
	public IEnumerable<NaturalAirlineRoute> Flights => Legs.Select(leg => leg.Flight).OfType<NaturalAirlineRoute>();
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

	/// <summary>The first flight of the <see cref="Journey"/> toward <paramref name="destination"/>; null when walking is better.</summary>
	public static NaturalAirlineRoute? Toward(IEnumerable<NaturalAirlineRoute> routes, int mapId, BotPosition from, BotPosition destination) =>
		Journey(routes, mapId, from, destination)?.Legs.Select(leg => leg.Flight).OfType<NaturalAirlineRoute>().First();

	/// <summary>
	/// AG-00: the quickest way to <paramref name="destination"/> by walking and flight transporters, on one map. Walks are
	/// straight lines at <paramref name="walkSpeed"/> (a road is never shorter, so this favours walking); a flight costs its
	/// route's time plus <paramref name="flightOverheadSeconds"/> for the dialog and the fare. Null when no flight saves at
	/// least <paramref name="minimumSavingSeconds"/> over walking all the way.
	/// </summary>
	public static NaturalAirlineJourney? Journey(IEnumerable<NaturalAirlineRoute> routes, int mapId, BotPosition from, BotPosition destination,
		float walkSpeed = 6, float flightOverheadSeconds = 10, float minimumSavingSeconds = 20)
	{
		NaturalAirlineRoute[] flights = routes.Where(route => route.MapId == mapId).ToArray();
		// Nodes: 0 the start, 1 the destination, then each flight's pad (2 + 2i) and landing (3 + 2i).
		var points = new List<BotPosition> { from, destination };
		foreach (NaturalAirlineRoute flight in flights) { points.Add(flight.Departure); points.Add(flight.Landing); }
		int count = points.Count;
		var seconds = Enumerable.Repeat(float.PositiveInfinity, count).ToArray();
		var previous = Enumerable.Repeat(-1, count).ToArray();
		var flownFrom = new int?[count]; // the flight index that reached a landing node
		var done = new bool[count];
		seconds[0] = 0;
		for (int round = 0; round < count; round++)
		{
			int node = -1;
			for (int i = 0; i < count; i++)
				if (!done[i] && (node < 0 || seconds[i] < seconds[node])) node = i;
			if (node < 0 || float.IsPositiveInfinity(seconds[node])) break;
			done[node] = true;
			if (node == 1) break;
			void Relax(int to, float cost, int? flight)
			{
				if (seconds[node] + cost >= seconds[to]) return;
				seconds[to] = seconds[node] + cost;
				previous[to] = node;
				flownFrom[to] = flight;
			}
			for (int to = 0; to < count; to++)
				if (to != node && !done[to]) Relax(to, Distance(points[node], points[to]) / walkSpeed, null);
			if (node >= 2 && node % 2 == 0)
				Relax(node + 1, flights[(node - 2) / 2].Seconds + flightOverheadSeconds, (node - 2) / 2);
		}
		float walkAll = Distance(from, destination) / walkSpeed;
		if (float.IsPositiveInfinity(seconds[1]) || seconds[1] + minimumSavingSeconds > walkAll) return null;
		var legs = new List<NaturalAirlineLeg>();
		for (int node = 1; previous[node] >= 0; node = previous[node])
		{
			NaturalAirlineRoute? flight = flownFrom[node] is int index ? flights[index] : null;
			// Consecutive walks merge: walking via a pad without flying is just walking.
			if (flight == null && legs.Count > 0 && legs[0].Flight == null)
				legs[0] = legs[0] with { From = points[previous[node]] };
			else
				legs.Insert(0, new NaturalAirlineLeg(flight, points[previous[node]], points[node]));
		}
		if (legs.All(leg => leg.Flight == null)) return null;
		return new NaturalAirlineJourney(legs, seconds[1], walkAll, legs.Select(leg => leg.Flight?.Price ?? 0).Sum());
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
