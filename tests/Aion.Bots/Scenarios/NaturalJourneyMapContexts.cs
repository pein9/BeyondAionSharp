using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>The map the client is in: its world id and, on twin-channel maps such as Altgard, its channel.</summary>
public readonly record struct NaturalMapKey(int MapId, int Channel)
{
	/// <summary>The client's own view after a spawn: SM_PLAYER_SPAWN's world and SM_CHANNEL_INFO's channel.</summary>
	public static NaturalMapKey Observe(BotWorldModel world) =>
		new(world.MapId ?? throw new InvalidOperationException("No map observed yet."), world.ChannelInfo?.Index ?? 0);

	/// <summary>Instance maps (3xxxxxxxx) are created anew on every entry, so nothing built for one entry is reused.</summary>
	public bool IsInstance => MapId / 100_000_000 == 3;
}

/// <summary>
/// Per-map navigation state for a journey that crosses maps (NA-06): geometry, graph and navigator are built for
/// the map the client entered and reused while it stays there. An instance is rebuilt on every entry because
/// its geometry is bound to that entry's instance id.
/// </summary>
public sealed class NaturalJourneyMapContexts<TContext>(Func<NaturalMapKey, TContext> build) where TContext : class
{
	private readonly Dictionary<NaturalMapKey, TContext> contexts = [];

	public NaturalMapKey? Current { get; private set; }

	/// <summary>How many contexts were built, for tests and traces.</summary>
	public int Built { get; private set; }

	/// <summary>Seed a context that already exists (the journey's home map).</summary>
	public void Register(NaturalMapKey key, TContext context) => contexts[key] = context;

	/// <summary>The context for the map just entered. <paramref name="newEntry"/> marks an arrival (a spawn),
	/// which rebuilds an instance's context; a repeated lookup within the same stay reuses it.</summary>
	public TContext Enter(NaturalMapKey key, bool newEntry = true)
	{
		if (newEntry && key.IsInstance) contexts.Remove(key);
		Current = key;
		if (!contexts.TryGetValue(key, out TContext? context))
		{
			context = build(key);
			contexts[key] = context;
			Built++;
		}
		return context;
	}

	public bool Has(NaturalMapKey key) => contexts.ContainsKey(key);
}
