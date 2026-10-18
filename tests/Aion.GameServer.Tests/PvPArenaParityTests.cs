using Aion.GameServer.Model.Instance.Instancescore;

namespace Aion.GameServer.Tests;

/// <summary>The PvP arenas' score (PvPArenaScore) where Java's and C#'s library calls answer differently.</summary>
public sealed class PvPArenaParityTests
{
	[Fact]
	public void NoRewardsReachNoScoreGapAsJavaSummaryStatisticsAnswer()
	{
		// IntSummaryStatistics of nothing: MIN_VALUE - MAX_VALUE overflows to 1, below every arena's gap (1500, 43000).
		Assert.False(PvPArenaScore.ReachedScoreGap([], 1500));
		Assert.True(PvPArenaScore.ReachedScoreGap([], 1));
		Assert.False(PvPArenaScore.ReachedScoreGap([13200, 12900], 1500));
		Assert.True(PvPArenaScore.ReachedScoreGap([14500, 13000], 1500));
	}
}
