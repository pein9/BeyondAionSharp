using Aion.Bots.Scenarios;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

public sealed class SoakGatheringSearchTests
{
	[Theory]
	[InlineData(50)]
	[InlineData(200)]
	[InlineData(500)]
	public void ShippedSearchAreaHasSupplyForEveryEnrolledSubjectsFixedPrefix(int population)
	{
		var spots = SoakGatheringPool.StarterSpots();
		foreach (var (map, home) in new[] { (210010000, VendorScenario.Position), (220010000, GatheringTarget.YoungAzpha.Position) })
		{
			var search = new SoakGatheringSearch(spots, map, 0, home, 0);
			int subjectsPerChannel = population / 25;
			int required = subjectsPerChannel * SoakEconomyStatistics.PrefixLength;
			// An optimistic upper bound, not a throughput prediction: instantaneous casts/travel,
			// all nodes initially full, then ordinary respawns. Even this failed at radius 150.
			int idealSupply = search.Candidates.Count * GatheringTarget.HarvestCount *
				(1 + (int)(TimeSpan.FromHours(2) / GatheringTarget.RespawnDelay));
			Assert.True(idealSupply >= required, $"Map {map}: {search.Candidates.Count} spots supply at most {idealSupply}, need {required}.");
			Assert.All(search.Candidates, spot => Assert.Contains(spot, spots));
		}
	}

	[Fact]
	public void ExplorationRotatesPastBusyUnseenAndUnreachableSpotsWithoutOwningThem()
	{
		var spots = Enumerable.Range(1, 4).Select(x => new SoakGatheringSpot(210010000, 1, 400601, x, 0, 0)).ToArray();
		var search = new SoakGatheringSearch(spots, 210010000, 2, new BotPosition(0, 0, 0, 0), 1);
		var candidates = search.Candidates;
		Assert.All(candidates, spot => Assert.Equal(3, spot.InstanceId));
		Assert.Equal(candidates[1], search.NextApproach(_ => true));
		search.Reject(candidates[2]);
		Assert.False(search.Contains(candidates[2]));
		Assert.Equal(candidates[3], search.NextApproach(_ => true));
		Assert.Equal(candidates[1], search.NextApproach(spot => spot != candidates[0]));
		Assert.Null(search.NextApproach(_ => false));
		Assert.False(search.AllRejected); // Busy is not unreachable; a later respawn can be explored.
		Assert.NotNull(search.NextApproach(_ => true));
		foreach (var candidate in candidates) search.Reject(candidate);
		Assert.True(search.AllRejected);
		Assert.Null(search.NextApproach(_ => true));
		Assert.Throws<ArgumentException>(() => search.Reject(spots[0]));
	}
}
