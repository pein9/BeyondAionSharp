namespace Aion.Bots.Navigation;

/// <summary>Keep trying a guarded approach while walking or clearing guards makes progress.</summary>
public sealed class NaturalApproachProgress
{
	public const int MaximumAttempts = 120;
	private int stalledAttempts;

	public bool CanRetry(int attempts) => attempts < MaximumAttempts && stalledAttempts < 8;

	public void Observe(float distanceWalked, bool clearedGuard) =>
		stalledAttempts = distanceWalked > 2 || clearedGuard ? 0 : stalledAttempts + 1;
}
