using Aion.GameServer.Taskmanager;

namespace Aion.GameServer.Tests;

public sealed class InsertionOrderedSetTests
{
	[Fact]
	public void AddDeduplicatesWithoutChangingFirstInsertionOrder()
	{
		var set = new InsertionOrderedSet<int>();

		Assert.True(set.Add(4));
		Assert.True(set.Add(2));
		Assert.False(set.Add(4));
		Assert.True(set.Add(3));
		Assert.False(set.Add(2));

		Assert.Equal([4, 2, 3], set);
		Assert.Equal(3, set.Count);
		set.Clear();
		Assert.Empty(set);
	}

	[Fact]
	public void HighFanOutDedupeUsesHashLookupRatherThanLinearScans()
	{
		const int count = 10_000;
		var set = new InsertionOrderedSet<EqualityProbe>();
		for (var i = 0; i < count; i++)
			Assert.True(set.Add(new EqualityProbe(i)));

		EqualityProbe.ResetComparisons();
		for (var i = 0; i < count; i++)
			Assert.False(set.Add(new EqualityProbe(i)));

		Assert.Equal(count, set.Count);
		Assert.InRange(EqualityProbe.Comparisons, count, count * 2L);
	}

	private sealed class EqualityProbe(int id) : IEquatable<EqualityProbe>
	{
		private static long comparisons;
		private readonly int value = id;

		public static long Comparisons => Interlocked.Read(ref comparisons);

		public static void ResetComparisons() => Interlocked.Exchange(ref comparisons, 0);

		public bool Equals(EqualityProbe? other)
		{
			Interlocked.Increment(ref comparisons);
			return other is not null && value == other.value;
		}

		public override bool Equals(object? obj) => obj is EqualityProbe other && Equals(other);

		public override int GetHashCode() => value;
	}
}
