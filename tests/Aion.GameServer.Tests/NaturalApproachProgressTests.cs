using Aion.Bots.Navigation;

namespace Aion.GameServer.Tests;

public sealed class NaturalApproachProgressTests
{
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
