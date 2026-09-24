namespace Aion.Bots.Scenarios;

public enum NaturalRestOutcome { Completed, InterruptedByAttack, Dead }

public sealed record NaturalRestTick(bool Dead, IReadOnlyList<int> Attackers);

/// <summary>Rest in short, observable slices; stand before invoking ordinary combat defense.</summary>
public static class NaturalRestCadence
{
	public static async Task<NaturalRestOutcome> RunAsync(
		Func<bool, CancellationToken, Task> setRest,
		Func<TimeSpan, CancellationToken, Task<NaturalRestTick>> advanceAndObserve,
		Func<IReadOnlyList<int>, CancellationToken, Task> defend,
		CancellationToken token = default)
	{
		ArgumentNullException.ThrowIfNull(setRest);
		ArgumentNullException.ThrowIfNull(advanceAndObserve);
		ArgumentNullException.ThrowIfNull(defend);
		await setRest(true, token);
		bool sitting = true;
		try
		{
			for (int tick = 0; tick < 5; tick++)
			{
				token.ThrowIfCancellationRequested();
				NaturalRestTick observation = await advanceAndObserve(TimeSpan.FromSeconds(2), token);
				if (observation.Dead) return NaturalRestOutcome.Dead;
				int[] attackers = observation.Attackers.Distinct().ToArray();
				if (attackers.Length == 0) continue;
				await setRest(false, token);
				sitting = false;
				await defend(attackers, token);
				return NaturalRestOutcome.InterruptedByAttack;
			}
			return NaturalRestOutcome.Completed;
		}
		finally
		{
			if (sitting) await setRest(false, CancellationToken.None);
		}
	}
}
