using Aion.Bots.Scenarios;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

public sealed class NaturalStalkerSearchPolicyTests
{
	[Fact]
	public void ChangedSpawnPositionsReplaceOldSearchAreas()
	{
		BotPosition origin = new(946, 1703, 260, 0);
		BotPosition western = new(649, 1535, 294, 0);
		BotPosition nearbyWestern = new(647, 1538, 294, 0);
		BotPosition movedEastern = new(709, 1408, 287, 0);

		BotPosition[] areas = NaturalStalkerSearchPolicy.SelectAreas(
			[western, nearbyWestern, movedEastern], origin);

		Assert.Equal(2, areas.Length);
		Assert.Contains(western, areas);
		Assert.Contains(movedEastern, areas);
	}

	[Fact]
	public void ARejectedStalkerIsRetriedFromAnotherSideOrAfterPatrolsMove()
	{
		BotPosition rejectedAt = new(758, 1498, 286, 0);
		Assert.False(NaturalStalkerSearchPolicy.ShouldRetryRejected(
			new BotPosition(770, 1498, 286, 0), rejectedAt, 10_000));
		Assert.True(NaturalStalkerSearchPolicy.ShouldRetryRejected(
			new BotPosition(790, 1498, 286, 0), rejectedAt, 10_000));
		Assert.True(NaturalStalkerSearchPolicy.ShouldRetryRejected(rejectedAt, rejectedAt, 31_000));
	}
}
