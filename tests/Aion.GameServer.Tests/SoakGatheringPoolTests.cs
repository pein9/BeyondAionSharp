using Aion.Bots.Scenarios;

namespace Aion.GameServer.Tests;

public sealed class SoakGatheringPoolTests
{
	private static readonly SoakGatheringSpot Spot = new(210010000, 1, 400601, 1, 2, 3);

	[Fact]
	public void ShippedSpotsIncludeBothBreadthAnchorsAndSeparateInstances()
	{
		var spots = SoakGatheringPool.StarterSpots();
		Assert.True(spots.Count > 10);
		foreach (var target in new[] { GatheringTarget.YoungAria, GatheringTarget.YoungAzpha })
			Assert.Contains(spots, spot => spot.MapId == target.MapId && spot.TemplateId == target.TemplateId &&
				Math.Abs(spot.X - target.Position.X) < .01f && Math.Abs(spot.Y - target.Position.Y) < .01f);
		Assert.Empty(spots.Intersect(SoakGatheringPool.StarterSpots(2)));
	}

	[Fact]
	public async Task ConcurrentSubjectsCannotOwnTheSameNodeButDifferentNodesRemainIndependent()
	{
		var other = Spot with { InstanceId = 2 };
		var pool = new SoakGatheringPool([Spot, other]);
		var attempts = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(() => pool.TryAcquire(Spot, 11, TimeSpan.Zero))));
		using var winner = Assert.Single(attempts.OfType<SoakGatheringPool.Lease>());
		using var second = pool.TryAcquire(other, 22, TimeSpan.Zero);
		Assert.NotNull(second);
		winner.Dispose();
		winner.Dispose();
		using var next = pool.TryAcquire(Spot, 11, TimeSpan.Zero);
		Assert.NotNull(next);
		Assert.Equal(1, next.UseNumber);
	}

	[Fact]
	public void EveryCompletedAttemptConsumesOneUseAndRespawnDoesNotGrowThePool()
	{
		var pool = new SoakGatheringPool([Spot]);
		var elapsed = TimeSpan.Zero;
		for (int generation = 0; generation < 100; generation++)
		{
			Assert.True(pool.IsAvailable(Spot, elapsed));
			for (int use = 1; use <= 3; use++)
			{
				using var lease = pool.TryAcquire(Spot, generation + 1, elapsed);
				Assert.NotNull(lease);
				Assert.False(pool.IsAvailable(Spot, elapsed));
				Assert.Equal(use, lease.UseNumber);
				Assert.Equal(use == 3, lease.Depletes);
				// No success/failure parameter: Java consumes a use for either outcome.
				lease.Complete(elapsed);
				Assert.Throws<InvalidOperationException>(() => lease.Complete(elapsed));
			}
			Assert.Null(pool.TryAcquire(Spot, generation + 2, elapsed + GatheringTarget.RespawnDelay - TimeSpan.FromMilliseconds(1)));
			Assert.False(pool.IsAvailable(Spot, elapsed + GatheringTarget.RespawnDelay - TimeSpan.FromMilliseconds(1)));
			elapsed += GatheringTarget.RespawnDelay;
			Assert.Equal(1, pool.Count);
		}
	}

	[Fact]
	public void ChangedIdentityAndCompletionAfterReleaseAreRejected()
	{
		var pool = new SoakGatheringPool([Spot]);
		var lease = pool.TryAcquire(Spot, 1, TimeSpan.Zero)!;
		lease.Complete(TimeSpan.Zero);
		lease.Dispose();
		Assert.Throws<InvalidOperationException>(() => lease.Complete(TimeSpan.Zero));
		Assert.Throws<InvalidDataException>(() => pool.TryAcquire(Spot, 2, TimeSpan.Zero));
		Assert.Throws<KeyNotFoundException>(() => pool.TryAcquire(Spot with { X = 5 }, 1, TimeSpan.Zero));
		Assert.Throws<ArgumentOutOfRangeException>(() => pool.TryAcquire(Spot, 0, TimeSpan.Zero));
	}
}
