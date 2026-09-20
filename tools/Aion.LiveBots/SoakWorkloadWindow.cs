namespace Aion.LiveBots;

/// <summary>Evidence window anchored to the same monotonic start as every cohort; cleanup never extends it.</summary>
public sealed record SoakWorkloadWindow(string Run, int BotCount, int PlannedSeconds, DateTimeOffset StartedUtc)
{
	public int SchemaVersion => 1;
	public DateTimeOffset EndedUtc => StartedUtc.AddSeconds(PlannedSeconds);
	public string Status { get; private init; } = "running";
	public DateTimeOffset? CompletedUtc { get; private init; }
	public double? ElapsedSeconds { get; private init; }
	public double? WallClockDriftSeconds { get; private init; }
	public bool ClockConsistent { get; private init; }
	public bool OverallSoakAccepted => false;

	public SoakWorkloadWindow Finish(bool successful, DateTimeOffset completedUtc, TimeSpan elapsed)
	{
		if (Status != "running") throw new InvalidOperationException("Soak window is already terminal.");
		if (elapsed < TimeSpan.Zero || PlannedSeconds <= 0 || BotCount <= 0)
			throw new ArgumentOutOfRangeException(nameof(elapsed));
		double drift = (completedUtc - StartedUtc - elapsed).TotalSeconds;
		return this with
		{
			Status = successful && elapsed.TotalSeconds >= PlannedSeconds ? "completed" : "failed",
			CompletedUtc = completedUtc, ElapsedSeconds = elapsed.TotalSeconds,
			WallClockDriftSeconds = drift, ClockConsistent = Math.Abs(drift) <= 2,
		};
	}
}
