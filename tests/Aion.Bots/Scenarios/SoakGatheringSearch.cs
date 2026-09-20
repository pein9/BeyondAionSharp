using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>A finite itinerary of shipped spots, never invented object ids. Availability is advisory;
/// only a subsequently observed gatherable can acquire the pool's exclusive interaction lease.</summary>
public sealed class SoakGatheringSearch
{
	public const float Radius = 300;
	private readonly HashSet<SoakGatheringSpot> rejected = [];
	private int next;
	public IReadOnlyList<SoakGatheringSpot> Candidates { get; }
	public bool AllRejected => rejected.Count == Candidates.Count;

	public SoakGatheringSearch(IEnumerable<SoakGatheringSpot> spots, int map, int channel, BotPosition home, int offset)
	{
		if (channel is < 0 or > 4 || offset < 0) throw new ArgumentOutOfRangeException(nameof(channel));
		Candidates = spots.Where(spot => spot.MapId == map && Distance(spot.Position, home) <= Radius)
			.Select(spot => spot with { InstanceId = channel + 1 })
			.OrderBy(spot => Distance(spot.Position, home)).ThenBy(spot => spot.X).ThenBy(spot => spot.Y).ThenBy(spot => spot.Z).ToArray();
		if (Candidates.Count == 0 || Candidates.Distinct().Count() != Candidates.Count)
			throw new InvalidDataException("Gathering search needs distinct shipped spots near its hub.");
		next = offset % Candidates.Count;
	}

	public bool Contains(SoakGatheringSpot spot) => Candidates.Contains(spot) && !rejected.Contains(spot);
	public void Reject(SoakGatheringSpot spot)
	{
		if (!Candidates.Contains(spot)) throw new ArgumentException("Spot is not part of this gathering search.", nameof(spot));
		rejected.Add(spot);
	}

	public SoakGatheringSpot? NextApproach(Func<SoakGatheringSpot, bool> available)
	{
		for (int count = 0; count < Candidates.Count; count++)
		{
			var spot = Candidates[next];
			next = (next + 1) % Candidates.Count;
			if (!rejected.Contains(spot) && available(spot)) return spot;
		}
		return null;
	}

	private static float Distance(BotPosition a, BotPosition b) =>
		MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));
}
