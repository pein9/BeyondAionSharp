using Aion.Bots.Navigation;
using Aion.Bots.World;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios;

/// <summary>A shared Reshanta load-test encounter, not a general safe-camp finder or navigation system.</summary>
public static class SoakPvpCamp
{
	public const int MapId = 400010000;
	public const int InitialKisks = 4;
	public const int MaxResurrects = 72;
	public static readonly BotPosition ElyosHome = new(3180, 2480, 1557.9525f, 0);
	public static readonly BotPosition AsmodianHome = new(3192, 2480, 1557.6388f, 60);
	public static BotPosition Home(Race race) => race switch
	{
		Race.ELYOS => ElyosHome,
		Race.ASMODIANS => AsmodianHome,
		_ => throw new ArgumentOutOfRangeException(nameof(race)),
	};
	public static int Item(Race race) => race == Race.ELYOS ? 184000012 : race == Race.ASMODIANS ? 184000015 : throw new ArgumentOutOfRangeException(nameof(race));
	public static int Npc(Race race) => race == Race.ELYOS ? 700274 : race == Race.ASMODIANS ? 700277 : throw new ArgumentOutOfRangeException(nameof(race));
	public static BotPosition Encounter(BotNavigationGeometry geometry, Race race)
	{
		var route = geometry.TraceEdge(MapId, ElyosHome, AsmodianHome);
		if (route is not { Count: >= 4 }) throw new InvalidDataException("Reshanta camp has no checked ground encounter edge.");
		return race == Race.ELYOS ? route[0] : race == Race.ASMODIANS ? route[^2] with { Heading = 60 } : throw new ArgumentOutOfRangeException(nameof(race));
	}
	public static IReadOnlyList<BotPosition> Walk(BotNavigationGeometry geometry, BotPosition from, BotPosition to)
	{
		// The camp is one checked edge, not permission to search adjacent dynamic siege geometry.
		// Every requested segment is rechecked; failure never falls back to unchecked interpolation.
		if (MathF.Abs(from.Y - ElyosHome.Y) > .1f || MathF.Abs(to.Y - ElyosHome.Y) > .1f ||
			from.X < ElyosHome.X - .1f || from.X > AsmodianHome.X + .1f || to.X < ElyosHome.X - .1f || to.X > AsmodianHome.X + .1f)
			throw new InvalidDataException("Requested PvP walk leaves the checked camp edge.");
		// Already at the requested location: don't manufacture a vertical movement to reconcile
		// sub-centimetre ground/DB float rounding. Return no frames, not a snapped destination.
		if (from.X == to.X && from.Y == to.Y && MathF.Abs(from.Z - to.Z) < .01f) return [];
		try
		{
			return geometry.TraceEdge(MapId, from, to) ?? throw new InvalidDataException("PvP camp edge is not traversable over checked ground.");
		}
		catch (Exception error) when (error is not OperationCanceledException)
		{
			throw new InvalidDataException($"PvP ground query failed from ({from.X:R},{from.Y:R},{from.Z:R}) to ({to.X:R},{to.Y:R},{to.Z:R}).", error);
		}
	}
}
