using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

/// <summary>A planned flight route and why it was or was not accepted.</summary>
public sealed record NaturalFlightRoute(IReadOnlyList<BotPosition> Waypoints, float Meters, string? Refusal)
{
	public bool IsUsable => Refusal == null && Waypoints.Count > 0;
}

/// <summary>
/// AF-05 (docs/natural-altgard-leveling.md): the client side of free flight. Take off with <c>CM_EMOTION</c> FLY, fly with
/// <c>CM_MOVE_IN_AIR</c> samples (the E2E plan's flight packet), land with <c>CM_EMOTION</c> LAND on the ground or a platform,
/// and read the flight time from <c>SM_FLY_TIME</c>. Routes climb straight up, cross at a cruise height and come straight
/// down, each leg in line of sight on the server's geometry; <see cref="NaturalFlightPolicy"/> decides whether to go.
/// </summary>
public static class NaturalFlightProtocol
{
	/// <summary>Frames per second of flight movement, as the flight-path scenarios sample it.</summary>
	public static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(500);

	/// <summary>Grid spacing and reach of the climb-column search, and the heights tried for the low leg.</summary>
	public const float ColumnSpacing = 10f, ColumnReach = 100f;
	private static readonly float[] LowHeights = [3f, 10f, 20f, 35f];

	/// <summary>
	/// Climb, cross and descend from a takeoff point to a landing point, cruising at <paramref name="cruiseZ"/>. When the
	/// straight climb or descent is blocked (the floating island over Altgard Fortress), the route flies low to a clear
	/// column beside the low end, climbs or descends there, and crosses at cruise height. The shortest clear route wins.
	/// </summary>
	public static NaturalFlightRoute Plan(BotNavigationGeometry geometry, int mapId, BotPosition start, BotPosition end, float cruiseZ)
	{
		float cruise = MathF.Max(cruiseZ, MathF.Max(start.Z, end.Z));
		NaturalFlightRoute direct = Check(geometry, mapId, start, [start with { Z = cruise }, end with { Z = cruise }, end]);
		if (direct.IsUsable) return direct;
		bool startIsLow = start.Z <= end.Z;
		BotPosition low = startIsLow ? start : end;
		NaturalFlightRoute? best = null;
		for (float dx = -ColumnReach; dx <= ColumnReach; dx += ColumnSpacing)
			for (float dy = -ColumnReach; dy <= ColumnReach; dy += ColumnSpacing)
				foreach (float height in LowHeights)
				{
					var column = new BotPosition(low.X + dx, low.Y + dy, low.Z + height, 0);
					BotPosition[] waypoints = startIsLow
						? [start with { Z = column.Z }, column, column with { Z = cruise }, end with { Z = cruise }, end]
						: [start with { Z = cruise }, column with { Z = cruise }, column, end with { Z = column.Z }, end];
					float meters = Length(start, waypoints);
					if (best != null && meters >= best.Meters) continue;
					NaturalFlightRoute candidate = Check(geometry, mapId, start, waypoints);
					if (candidate.IsUsable) best = candidate;
				}
		return best ?? direct;
	}

	private static NaturalFlightRoute Check(BotNavigationGeometry geometry, int mapId, BotPosition start, BotPosition[] waypoints)
	{
		BotPosition previous = start;
		foreach (BotPosition point in waypoints)
		{
			if (!IsClear(geometry, mapId, previous, point))
				return new([], 0, $"Leg ({previous.X:F0}, {previous.Y:F0}, {previous.Z:F0}) -> ({point.X:F0}, {point.Y:F0}, {point.Z:F0}) is blocked.");
			previous = point;
		}
		return new(waypoints, Length(start, waypoints), null);
	}

	private static float Length(BotPosition start, IReadOnlyList<BotPosition> waypoints)
	{
		float meters = 0;
		BotPosition previous = start;
		foreach (BotPosition point in waypoints)
		{
			meters += NaturalFlightPolicy.Distance(previous, point);
			previous = point;
		}
		return meters;
	}

	/// <summary>The server's sight check refuses rays over 80 m, so a leg is checked in pieces of at most this length.</summary>
	public const float SightPiece = 40f;

	/// <summary>No collision along the leg, checked piece by piece.</summary>
	public static bool IsClear(BotNavigationGeometry geometry, int mapId, BotPosition a, BotPosition b)
	{
		float meters = NaturalFlightPolicy.Distance(a, b);
		int pieces = Math.Max(1, (int)MathF.Ceiling(meters / SightPiece));
		BotPosition previous = a;
		for (int i = 1; i <= pieces; i++)
		{
			float t = (float)i / pieces;
			var next = new BotPosition(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t, b.Heading);
			if (NaturalFlightPolicy.Distance(previous, next) > 0.01f && !geometry.HasLineOfSight(mapId, previous, next))
				return false;
			previous = next;
		}
		return true;
	}

	public static NaturalFlightPlan ToPlan(NaturalFlightRoute route, BotPosition start, float speedMetersPerSecond, int airborneWorkSeconds = 0)
	{
		var legs = new List<float>();
		BotPosition previous = start;
		foreach (BotPosition point in route.Waypoints)
		{
			legs.Add(NaturalFlightPolicy.Distance(previous, point));
			previous = point;
		}
		return new(route.Waypoints, legs, speedMetersPerSecond, airborneWorkSeconds);
	}

	/// <summary>Take off; returns the flight speed the server announced with the FLY emotion.</summary>
	public static async Task<float> TakeOffAsync(INaturalJourneySession session, CancellationToken token)
	{
		await session.SendPacketAsync(session.Api.Fly(), token);
		DecodedBotServerPacket flying = await session.WaitForPacketAsync(typeof(SM_EMOTION), token, packet =>
			packet.Get<int>("senderObjectId") == session.CharacterId && packet.Get<byte>("emotionType") == (byte)EmotionType.FLY);
		float speed = flying.Get<float>("movementSpeed");
		return speed > 0 ? speed : session.Api.World.MovementSpeed ?? throw new InvalidDataException("The FLY emotion carried no speed.");
	}

	/// <summary>Fly the waypoints at <paramref name="speed"/>, one <c>CM_MOVE_IN_AIR</c> sample every half second.</summary>
	public static async Task FlyAsync(INaturalJourneySession session, int mapId, BotPosition start, IReadOnlyList<BotPosition> waypoints,
		float speed, CancellationToken token)
	{
		BotPosition current = start;
		foreach (BotPosition point in waypoints)
		{
			float meters = NaturalFlightPolicy.Distance(current, point);
			if (meters <= 0.01f) continue;
			TimeSpan duration = TimeSpan.FromSeconds(meters / speed);
			if (duration < SampleInterval) duration = SampleInterval;
			await session.ExecuteMovementAsync(CapitalAscensionScenario.CreateQuestFlight(current, point, mapId, duration), token);
			current = point;
		}
	}

	/// <summary>Land where the bot hovers; the server ends the flight and starts restoring flight time.</summary>
	public static async Task LandAsync(INaturalJourneySession session, CancellationToken token)
	{
		await session.SendPacketAsync(session.Api.Land(), token);
		await session.WaitForPacketAsync(typeof(SM_EMOTION), token, packet =>
			packet.Get<int>("senderObjectId") == session.CharacterId && packet.Get<byte>("emotionType") == (byte)EmotionType.LAND);
	}
}
