namespace Aion.Bots.Scenarios;

/// <summary>Exclusive observed quest objects. Completed ids are retained only for the finite Q1/Q2 workload,
/// capped by the population's objective count; unlike repeating activities this cannot grow with soak duration.</summary>
public sealed class SoakQuestResources(int maximumObjects)
{
	private readonly object gate = new();
	private readonly HashSet<(int Map, int Channel, int Object)> owned = [], consumed = [];
	private readonly int limit = maximumObjects > 0 ? maximumObjects : throw new ArgumentOutOfRangeException(nameof(maximumObjects));
	public int Count { get { lock (gate) return owned.Count + consumed.Count; } }

	public Lease? TryAcquire(int map, int channel, int objectId)
	{
		if (map is not (210010000 or 220010000) || channel is < 0 or > 4 || objectId <= 0)
			throw new ArgumentOutOfRangeException(nameof(objectId));
		lock (gate)
		{
			var key = (map, channel, objectId);
			if (owned.Contains(key) || consumed.Contains(key)) return null;
			if (owned.Count + consumed.Count >= limit) throw new InvalidOperationException("Finite quest resource budget exhausted.");
			owned.Add(key);
			return new(this, key);
		}
	}

	public sealed class Lease : IDisposable
	{
		private readonly SoakQuestResources pool;
		private readonly (int Map, int Channel, int Object) key;
		private bool completed, disposed;
		internal Lease(SoakQuestResources pool, (int Map, int Channel, int Object) key) { this.pool = pool; this.key = key; }
		public int ObjectId => key.Object;
		public void Complete()
		{
			lock (pool.gate)
			{
				if (disposed || completed) throw new InvalidOperationException("Quest resource already completed or released.");
				completed = true;
				pool.owned.Remove(key);
				pool.consumed.Add(key);
			}
		}
		public void Dispose()
		{
			lock (pool.gate)
			{
				if (disposed) return;
				disposed = true;
				pool.owned.Remove(key);
			}
		}
	}
}
