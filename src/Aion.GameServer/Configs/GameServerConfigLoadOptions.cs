namespace Aion.GameServer.Configs;

/// <summary>Host-specific inputs layered around the Java-shaped static config load.</summary>
public sealed class GameServerConfigLoadOptions
{
	public static GameServerConfigLoadOptions Default { get; } = new();

	/// <summary>
	/// Exact directory containing administration/, main/, network/ and mygs.properties. When set, config loading
	/// never probes the checkout and therefore cannot consume a developer's gitignored mygs.properties.
	/// </summary>
	public string? ConfigRoot { get; init; }

	/// <summary>Runs immediately after static config holders have been populated.</summary>
	public Action? PostLoadOverride { get; init; }
}
