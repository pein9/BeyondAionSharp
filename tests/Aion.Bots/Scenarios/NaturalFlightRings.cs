using System.Globalization;
using System.Numerics;
using System.Xml.Linq;
using Aion.Bots.Navigation;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

/// <summary>Abyss flight rings from the shipped client/world knowledge, not live server objects.</summary>
public sealed class NaturalFlightRings(string repoRoot)
{
	public const int AbyssMap = 400010000;
	// Java FlyRingObserver applies Wings of Aether on every crossing, in either direction. Skill 265 restores 9 FP.
	private const int Restore = 9;
	private readonly Lazy<Ring[]> rings = new(() => Load(repoRoot));
	public sealed record Ring(string Name, BotPosition First, BotPosition Second);
	public sealed record Stop(Ring Ring, BotPosition Entry, BotPosition Exit, NaturalFlightRoute Approach,
		BotPosition Landing, NaturalFlightRoute Escape, int Reserve);
	public sealed record Route(IReadOnlyList<Stop> Stops, NaturalFlightRoute Finish, float Speed);

	private static Ring[] Load(string root) => [.. XDocument.Load(Path.Combine(root,
		"game-server/data/static_data/fly_rings/fly_rings.xml")).Root!.Elements("fly_ring")
		.Where(node => (int?)node.Attribute("map") == AbyssMap).Select(node =>
		{
			Vector3 center = Point(node.Element("center")!);
			Vector3 normal = Vector3.Cross(Point(node.Element("p1")!) - center, Point(node.Element("p2")!) - center);
			if (normal.LengthSquared() < 0.0001f) throw new InvalidDataException("A flight ring has no plane.");
			// Stop on opposite sides, away from the plane: arriving at the center can count the same crossing twice.
			Vector3 offset = Vector3.Normalize(normal) * 3;
			return new Ring((string)node.Attribute("name")!, Position(center - offset), Position(center + offset));
		})];

	private static Vector3 Point(XElement node) => new(Number(node, "x"), Number(node, "y"), Number(node, "z"));
	private static float Number(XElement node, string name) => float.Parse((string)node.Attribute(name)!, CultureInfo.InvariantCulture);
	private static BotPosition Position(Vector3 point) => new(point.X, point.Y, point.Z, 0);
	private static int Cost(NaturalFlightRoute route, float speed) => (int)MathF.Ceiling(route.Meters / speed) + 2;
	private static bool Inside(BotPosition at, IReadOnlyList<NaturalFlyZone> zones) =>
		zones.Any(zone => !zone.Forbids && zone.Contains(at.X, at.Y, at.Z)) &&
		!zones.Any(zone => zone.Forbids && zone.Contains(at.X, at.Y, at.Z));

	/// <summary>Find a chain of rings, retaining enough FP to land beside each if its refill fails.</summary>
	public Route? Plan(BotNavigationGeometry geometry, int map, BotPosition start, BotPosition destination,
		float takeoffSpeed, int currentFp, int maxFp, IReadOnlyList<NaturalFlyZone> zones)
	{
		if (map != AbyssMap || takeoffSpeed <= 0) return null;
		// A ring's six-second speed boost can expire between samples. Never budget or move at that temporary speed.
		float speed = MathF.Min(takeoffSpeed, 9f);
		NaturalFlightRoute? Clear(BotPosition from, BotPosition to, int budget)
		{
			if (!Inside(to, zones) || NaturalFlightPolicy.Distance(from, to) / speed + 2 > budget ||
				!NaturalFlightProtocol.DirectlyClear(geometry, map, from, to, MathF.Max(from.Z, to.Z) + 15)) return null;
			NaturalFlightRoute route = NaturalFlightProtocol.Plan(geometry, map, from, to, MathF.Max(from.Z, to.Z) + 15);
			return route.IsUsable && Cost(route, speed) <= budget && route.Waypoints.All(at => Inside(at, zones)) ? route : null;
		}
		var stations = new List<Stop>();
		foreach (Ring ring in rings.Value)
		{
			if (!Inside(ring.First, zones) || !Inside(ring.Second, zones) ||
				!NaturalFlightProtocol.IsClear(geometry, map, ring.First, ring.Second)) continue;
			// A rejected/missing ring must not strand the bot over the Abyss. The reserve covers a real checked landing.
			BotPosition middle = ring.First with { X = (ring.First.X + ring.Second.X) / 2,
				Y = (ring.First.Y + ring.Second.Y) / 2, Z = (ring.First.Z + ring.Second.Z) / 2 };
			BotPosition[] ground = [.. geometry.GroundNear(map, middle, maxFp * speed / 2, at => Inside(at, zones))
				.OrderBy(at => NaturalFlightPolicy.Distance(at, middle)).Take(12)];
			foreach ((BotPosition entry, BotPosition exit) in new[] { (ring.First, ring.Second), (ring.Second, ring.First) })
			{
				foreach (BotPosition landing in ground)
					if (Clear(exit, landing, maxFp / 2) is { } escape)
					{
						int reserve = Math.Max(NaturalFlightPolicy.LandingReserveFp, Cost(escape, speed) + 2);
						stations.Add(new(ring, entry, exit, new([], 0, null), landing, escape, reserve));
						break;
					}
			}
		}
		// Dijkstra over the two crossing directions of each ring. Every station can refill to a full bar.
		int origin = stations.Count;
		var distance = new Dictionary<int, float> { [origin] = 0 };
		var previous = new Dictionary<int, (int Node, Stop Stop)>();
		var queue = new PriorityQueue<int, float>();
		queue.Enqueue(origin, 0);
		while (queue.TryDequeue(out int node, out float spent))
		{
			if (spent > distance[node]) continue;
			BotPosition from = node == origin ? start : stations[node].Exit;
			int fp = node == origin ? currentFp : maxFp;
			if (node != origin && Clear(from, destination, fp - NaturalFlightPolicy.LandingReserveFp) is { } finish)
			{
				var stops = new List<Stop>();
				for (int at = node; at != origin; at = previous[at].Node) stops.Add(previous[at].Stop);
				stops.Reverse();
				return new(stops, finish, speed);
			}
			for (int index = 0; index < stations.Count; index++)
			{
				Stop candidate = stations[index];
				if (index == node || node != origin && candidate.Ring == stations[node].Ring) continue;
				int crossing = (int)MathF.Ceiling(NaturalFlightPolicy.Distance(candidate.Entry, candidate.Exit) / speed) + 1;
				if (crossing >= Restore) continue;
				if (Clear(from, candidate.Entry, fp - candidate.Reserve - crossing) is not { } approach) continue;
				float cost = spent + approach.Meters + NaturalFlightPolicy.Distance(candidate.Entry, candidate.Exit) +
					// Include time spent recrossing to restore what this approach consumed.
					(Cost(approach, speed) + crossing) * 6f / (Restore - crossing);
				if (distance.TryGetValue(index, out float best) && best <= cost) continue;
				distance[index] = cost;
				previous[index] = (node, candidate with { Approach = approach });
				queue.Enqueue(index, cost);
			}
		}
		return null;
	}

	/// <summary>Already airborne: follow the chain, verifying refills in SM_FLY_TIME. False lands at a checked fallback.</summary>
	public static async Task<bool> FlyAsync(INaturalJourneySession session, BotNavigationGeometry geometry,
		IReadOnlyList<NaturalFlyZone> zones, Route route, CancellationToken token)
	{
		BotWorldModel world = session.Api.World;
		BotPosition departure = session.CurrentPosition;
		Stop? last = null;
		session.TraceDiagnostic("flight-ring-route", new Dictionary<string, object?>
		{ ["rings"] = route.Stops.Select(stop => stop.Ring.Name).ToArray(), ["speed"] = route.Speed, ["fp"] = world.CurrentFlightTime });
		foreach (Stop stop in route.Stops)
		{
			if (!await FlyLegAsync(stop.Approach, stop.Reserve + 2)) return await EscapeAsync(last);
			last = stop;
			bool atExit = false;
			// Each recross advances game time. At most 32 crosses; a ring that supplies no FP is rejected immediately.
			for (int pass = 0; pass < 32; pass++)
			{
				int before = world.CurrentFlightTime, cursor = session.PacketHistory.Count;
				BotPosition target = atExit ? stop.Entry : stop.Exit;
				await NaturalFlightProtocol.FlyAsync(session, AbyssMap, session.CurrentPosition, [target], Speed(), token);
				await session.SynchronizeAsync(token);
				if (world.IsDead || world.MapId != AbyssMap) return false;
				atExit = !atExit;
				int peak = session.PacketHistory.Skip(cursor).Where(packet => packet.PacketType == typeof(SM_FLY_TIME))
					.Select(packet => packet.Get<int>("currentFp")).DefaultIfEmpty(-1).Max();
				bool restored = peak >= Math.Min(world.MaxFlightTime, before + Restore - 2);
				session.TraceDiagnostic("flight-ring-crossed", new Dictionary<string, object?>
				{ ["ring"] = stop.Ring.Name, ["pass"] = pass + 1, ["beforeFp"] = before, ["afterFp"] = world.CurrentFlightTime,
					["peakFp"] = peak, ["restored"] = restored, ["position"] = session.CurrentPosition });
				if (!restored || world.CurrentFlightTime < stop.Reserve) return await EscapeAsync(stop);
				// Leave from the planned side with a full bar. Do not count any future refill before seeing it.
				if (atExit && world.CurrentFlightTime >= world.MaxFlightTime - 1) break;
				if (pass == 31) return await EscapeAsync(stop);
			}
		}
		if (!await FlyLegAsync(route.Finish, NaturalFlightPolicy.LandingReserveFp)) return await EscapeAsync(last);
		return true;

		float Speed() => MathF.Min(route.Speed, world.MovementSpeed is > 0 ? world.MovementSpeed.Value : route.Speed);
		async Task<bool> FlyLegAsync(NaturalFlightRoute leg, int reserve)
		{
			for (int waypoint = 0; waypoint < leg.Waypoints.Count; waypoint++)
			{
				BotPosition point = leg.Waypoints[waypoint];
				for (int piece = 0; NaturalFlightPolicy.Distance(session.CurrentPosition, point) > 0.1f; piece++)
				{
					if (piece >= 256 || world.IsDead || world.MapId != AbyssMap) return false;
					float speed = Speed();
					float meters = NaturalFlightPolicy.Distance(session.CurrentPosition, point);
					// A temporary slow must not turn a planned crossing into an exhausted bar.
					BotPosition previous = session.CurrentPosition;
					float remaining = 0;
					for (int tail = waypoint; tail < leg.Waypoints.Count; tail++)
					{
						remaining += NaturalFlightPolicy.Distance(previous, leg.Waypoints[tail]);
						previous = leg.Waypoints[tail];
					}
					if (world.CurrentFlightTime < MathF.Ceiling(remaining / speed) + reserve) return false;
					float fraction = Math.Min(1, 4 * speed / meters);
					BotPosition from = session.CurrentPosition;
					BotPosition next = new(from.X + (point.X - from.X) * fraction, from.Y + (point.Y - from.Y) * fraction,
						from.Z + (point.Z - from.Z) * fraction, point.Heading);
					await NaturalFlightProtocol.FlyAsync(session, AbyssMap, from, [next], speed, token);
					await session.SynchronizeAsync(token);
					if (NaturalFlightPolicy.Distance(from, session.CurrentPosition) < 0.01f) return false;
				}
			}
			return !world.IsDead;
		}
		async Task<bool> EscapeAsync(Stop? stop)
		{
			if (world.IsDead || world.MapId != AbyssMap) return false;
			BotPosition from = session.CurrentPosition;
			IEnumerable<BotPosition> landings = geometry.GroundNear(AbyssMap, from, world.CurrentFlightTime * Speed(), at => Inside(at, zones))
				.OrderBy(at => NaturalFlightPolicy.Distance(from, at)).Take(20).Prepend(departure);
			if (stop != null) landings = landings.Prepend(stop.Landing);
			foreach (BotPosition landing in landings)
			{
				NaturalFlightRoute escape = NaturalFlightProtocol.Plan(geometry, AbyssMap, from, landing, MathF.Max(from.Z, landing.Z) + 15);
				if (!escape.IsUsable || !NaturalFlightPolicy.CanFly(NaturalFlightProtocol.ToPlan(escape, from, Speed()),
					world.CurrentFlightTime + NaturalFlightPolicy.LandingReserveFp - 2, zones).Allowed) continue;
				session.TraceDiagnostic("flight-ring-fallback", new Dictionary<string, object?>
				{ ["ring"] = stop?.Ring.Name, ["landing"] = landing, ["fp"] = world.CurrentFlightTime });
				if (!await FlyLegAsync(escape, 1)) throw new IOException("Flight-ring fallback was interrupted.");
				await NaturalFlightProtocol.LandAsync(session, token);
				await session.SynchronizeAsync(token);
				return false;
			}
			throw new IOException("Flight-ring crossing interrupted with no reachable checked landing.");
		}
	}
}
