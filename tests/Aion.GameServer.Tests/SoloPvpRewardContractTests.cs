using Aion.Bots.Scenarios;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

public sealed class SoloPvpRewardContractTests
{
	private static BotAbyssRank Soldier(long ap = 500, int rank = 1) => new(ap, 0, rank, 0, 0, rank,
		new(0, 0, 0), new(0, 0, 0), new(0, 0, 0));

	[Theory]
	[InlineData(1, 300)]
	[InlineData(4, 300)]
	[InlineData(5, 1)]
	[InlineData(100, 1)]
	public void RepeatedKillBoundaryKeepsVictimLossAndKillCounters(int kills, int gain)
	{
		var expected = SoloPvpRewardContract.Predict(Soldier(), Soldier(), 10, 10, 10, kills);
		Assert.Equal(500 + gain, expected.Winner.Ap); Assert.Equal(410, expected.Victim.Ap);
		Assert.Equal(1, expected.Winner.AllKills); Assert.Equal(new(1, gain, 0), expected.Winner.Daily);
		Assert.Equal(expected.Winner.Daily, expected.Winner.Weekly);
		Assert.Equal(Soldier().Daily, expected.Victim.Daily); Assert.Equal(0, expected.Victim.AllKills);
	}

	[Theory]
	[InlineData(15, 10, 30, 9)]
	[InlineData(14, 10, 195, 58)]
	[InlineData(13, 10, 255, 77)]
	[InlineData(12, 10, 300, 90)]
	[InlineData(8, 10, 330, 90)]
	[InlineData(7, 10, 360, 90)]
	[InlineData(6, 10, 390, 90)]
	public void LevelPenaltiesPreserveJavaSinglePrecisionRounding(int attacker, int victim, int gain, int loss)
	{
		var expected = SoloPvpRewardContract.Predict(Soldier(), Soldier(), attacker, victim, attacker, 1);
		Assert.Equal(500 + gain, expected.Winner.Ap); Assert.Equal(500 - loss, expected.Victim.Ap);
	}

	[Fact]
	public void XpLevelUpHappensBeforeVictimLossCalculation()
	{
		var expected = SoloPvpRewardContract.Predict(Soldier(), Soldier(), 13, 10, 14, 1);
		Assert.Equal(755, expected.Winner.Ap); Assert.Equal(442, expected.Victim.Ap);
	}

	[Fact]
	public void RankPenaltyPromotionDemotionAndZeroApFloorAreIndependent()
	{
		var promoted = SoloPvpRewardContract.Predict(Soldier(1000), Soldier(1250, 2), 10, 10, 10, 1);
		Assert.Equal(1345, promoted.Winner.Ap); Assert.Equal(2, promoted.Winner.Rank); Assert.Equal(2, promoted.Winner.MaxRank);
		Assert.Equal(1147, promoted.Victim.Ap); Assert.Equal(1, promoted.Victim.Rank); Assert.Equal(2, promoted.Victim.MaxRank);
		var penalty = SoloPvpRewardContract.Predict(Soldier(1500, 2), Soldier(50), 10, 10, 10, 1);
		Assert.Equal(1785, penalty.Winner.Ap); Assert.Equal(0, penalty.Victim.Ap);
		var highRank = SoloPvpRewardContract.Predict(Soldier(105600, 8), Soldier(), 10, 10, 10, 1);
		Assert.Equal(105900, highRank.Winner.Ap); // Java's rank penalty applies only through soldier rank seven.
	}

	[Fact]
	public void RegressedRewardsCountersAndUnexpectedGpAreRejected()
	{
		var expected = SoloPvpRewardContract.Predict(Soldier(), Soldier(), 10, 10, 10, 5);
		SoloPvpRewardContract.AssertMatches(expected, expected.Winner, expected.Victim);
		SoloPvpRewardContract.AssertMatches(expected, expected.Winner with { RankingListPosition = 12 }, expected.Victim);
		foreach (var bad in new[] { expected.Winner with { Ap = 800 }, expected.Winner with { AllKills = 0 },
			expected.Winner with { Daily = new(1, 300, 0) }, expected.Winner with { CurrentGp = 1 }, expected.Winner with { Rank = 2 } })
			Assert.Throws<InvalidDataException>(() => SoloPvpRewardContract.AssertMatches(expected, bad, expected.Victim));
		Assert.Throws<InvalidDataException>(() => SoloPvpRewardContract.AssertMatches(expected, expected.Winner, expected.Victim with { Ap = 500 }));
		Assert.Throws<ArgumentException>(() => SoloPvpRewardContract.Predict(Soldier() with { CurrentGp = 1 }, Soldier(), 10, 10, 10, 1));
		Assert.Throws<ArgumentException>(() => SoloPvpRewardContract.Predict(Soldier(1300), Soldier(), 10, 10, 10, 1));
		Assert.Throws<ArgumentOutOfRangeException>(() => SoloPvpRewardContract.Predict(Soldier(), Soldier(), 10, 10, 10, 0));
	}
}
