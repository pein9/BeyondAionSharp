using Aion.LiveBots;

namespace Aion.GameServer.Tests;

public sealed class SoakWorkloadWindowTests
{
	private static readonly DateTimeOffset Start = new(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);

	[Fact]
	public void SetupIsExcludedAndCleanupCannotExtendScheduledPopulationWindow()
	{
		var window = new SoakWorkloadWindow("run", 50, 7200, Start);
		Assert.Equal("running", window.Status);
		Assert.Null(window.CompletedUtc);
		Assert.False(window.ClockConsistent);
		var completed = window.Finish(true, Start.AddSeconds(7500), TimeSpan.FromSeconds(7500));
		Assert.Equal("completed", completed.Status);
		Assert.Equal(Start.AddSeconds(7200), completed.EndedUtc);
		Assert.Equal(Start.AddSeconds(7500), completed.CompletedUtc);
		Assert.True(completed.ClockConsistent);
		Assert.False(completed.OverallSoakAccepted);
		Assert.Throws<InvalidOperationException>(() => completed.Finish(true, Start, TimeSpan.Zero));
	}

	[Theory]
	[InlineData(false, 7500, "failed")]
	[InlineData(true, 7199, "failed")]
	public void FailureOrPrematureSuccessCannotClaimCompletedWindow(bool successful, int elapsed, string status)
	{
		var result = new SoakWorkloadWindow("run", 50, 7200, Start).Finish(successful, Start.AddSeconds(elapsed), TimeSpan.FromSeconds(elapsed));
		Assert.Equal(status, result.Status);
		Assert.False(result.OverallSoakAccepted);
	}

	[Theory]
	[InlineData(-3, false)]
	[InlineData(-2, true)]
	[InlineData(0, true)]
	[InlineData(2, true)]
	[InlineData(3, false)]
	public void WallClockJumpIsReportedRatherThanMovingTheWindow(int drift, bool consistent)
	{
		var result = new SoakWorkloadWindow("run", 50, 7200, Start).Finish(true, Start.AddSeconds(7200 + drift), TimeSpan.FromSeconds(7200));
		Assert.Equal(drift, result.WallClockDriftSeconds);
		Assert.Equal(consistent, result.ClockConsistent);
		Assert.Equal(Start.AddSeconds(7200), result.EndedUtc);
	}
}
