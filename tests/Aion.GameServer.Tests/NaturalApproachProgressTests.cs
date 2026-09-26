using Aion.Bots.Navigation;

namespace Aion.GameServer.Tests;

public sealed class NaturalApproachProgressTests
{
	[Fact]
	public void TreasureMapApproachCanReplanAfterUnsuccessfulGuardClearingMovesTheBot()
	{
		var progress = new NaturalApproachProgress();
		// LIVE a2 abandoned the map after moving from (1118,1830,270) to (1218,1866,246)
		// without killing its selected guards. The original route failure is stale at that new position.
		Assert.True(progress.Observe(108, clearedGuard: false));
		Assert.True(progress.CanRetry(1));
		for (int stalled = 0; stalled < 8; stalled++) Assert.False(progress.Observe(0, clearedGuard: false));
		Assert.False(progress.CanRetry(9));
		// Moving again cannot evade the overall attempt bound.
		Assert.True(progress.Observe(108, clearedGuard: false));
		Assert.False(progress.CanRetry(NaturalApproachProgress.MaximumAttempts));
	}

	[Fact]
	public void ReturnFromBindCanClearMoreThanEightGuardsAndStillFightThroughAtTheCave()
	{
		var progress = new NaturalApproachProgress();
		for (int attempt = 0; attempt < 12; attempt++)
		{
			Assert.True(progress.CanRetry(attempt));
			progress.Observe(attempt % 2 == 0 ? 40 : 0, attempt % 2 != 0);
		}
		Assert.True(progress.CanRetry(12));
		Assert.False(progress.CanRetry(NaturalApproachProgress.MaximumAttempts));
	}

	[Fact]
	public void RepeatedFailedChasesStillStopAndRealProgressResetsTheStallBudget()
	{
		var progress = new NaturalApproachProgress();
		for (int attempt = 0; attempt < 7; attempt++) progress.Observe(0, false);
		Assert.True(progress.CanRetry(7));
		progress.Observe(3, false);
		for (int attempt = 0; attempt < 7; attempt++) progress.Observe(1, false);
		Assert.True(progress.CanRetry(15));
		progress.Observe(0, false);
		Assert.False(progress.CanRetry(16));
	}
}
