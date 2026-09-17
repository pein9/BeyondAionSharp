namespace Aion.GameServer.Utils;

/// <summary>
/// Keeps Java/production collection iteration untouched while giving SIM an explicit, stable order.
/// </summary>
public static class DeterministicIteration
{
	public static IEnumerable<T> ByIntKey<T>(IEnumerable<T> source, Func<T, int> keySelector) =>
		ThreadPoolManager.IsDeterministicMode ? source.OrderBy(keySelector) : source;
}
