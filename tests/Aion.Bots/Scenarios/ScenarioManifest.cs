using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aion.Bots.Scenarios;

public enum ScenarioMode
{
	Sim,
	Live,
}

public enum ScenarioTier
{
	Fast,
	Full,
	Soak,
}

public enum ScenarioRace
{
	None,
	Elyos,
	Asmodians,
	Both,
}

public enum ScenarioChannelNeeds
{
	None,
	Dedicated,
	Exclusive,
}

public enum ScenarioResourceKind
{
	Npc,
	Gatherable,
}

public enum ScenarioRequirement
{
	Geo,
	D7,
	Chat,
	Db,
}

public sealed record ScenarioConsumedResource
{
	[JsonRequired]
	public required ScenarioResourceKind Kind { get; init; }

	[JsonRequired]
	public required int Id { get; init; }
}

public sealed record ScenarioDefinition
{
	[JsonRequired]
	public required string Id { get; init; }

	[JsonRequired]
	public required ScenarioMode[] Modes { get; init; }

	[JsonRequired]
	public required ScenarioTier Tier { get; init; }

	[JsonRequired]
	public required ScenarioRace Race { get; init; }

	[JsonRequired]
	public required int? Map { get; init; }

	[JsonRequired]
	public required ScenarioChannelNeeds ChannelNeeds { get; init; }

	[JsonRequired]
	public required int Bots { get; init; }

	[JsonRequired]
	public required TimeSpan VirtualDuration { get; init; }

	[JsonRequired]
	public required ScenarioConsumedResource[] Consumes { get; init; }

	[JsonRequired]
	public required ScenarioRequirement[] Requires { get; init; }

	[JsonRequired]
	public required string? ExpectedFail { get; init; }

	[JsonRequired]
	public required bool ResetEpoch { get; init; }
}

public sealed class ScenarioManifest
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = false,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
		Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
	};

	private readonly Dictionary<string, ScenarioDefinition> byId;

	private ScenarioManifest(IReadOnlyList<ScenarioDefinition> scenarios)
	{
		Scenarios = scenarios;
		byId = scenarios.ToDictionary(scenario => scenario.Id, StringComparer.Ordinal);
	}

	public IReadOnlyList<ScenarioDefinition> Scenarios { get; }

	public ScenarioDefinition Get(string id) => byId.TryGetValue(id, out ScenarioDefinition? scenario)
		? scenario
		: throw new KeyNotFoundException($"Scenario manifest does not contain '{id}'.");

	public IReadOnlyList<ScenarioDefinition> For(ScenarioMode mode, ScenarioTier tier) => Scenarios
		.Where(scenario => scenario.Modes.Contains(mode) && scenario.Tier <= tier)
		.ToArray();

	public static ScenarioManifest Load(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		try
		{
			using var input = File.OpenRead(path);
			ScenarioDefinition[] scenarios = JsonSerializer.Deserialize<ScenarioDefinition[]>(input, JsonOptions)
				?? throw new InvalidDataException($"Scenario manifest '{path}' must contain a JSON array.");
			Validate(scenarios);
			return new ScenarioManifest(scenarios);
		}
		catch (JsonException exception)
		{
			throw new InvalidDataException($"Scenario manifest '{path}' is invalid: {exception.Message}", exception);
		}
	}

	public static string FindDefaultPath()
	{
		foreach (string root in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory }.Distinct(StringComparer.OrdinalIgnoreCase))
		{
			for (DirectoryInfo? directory = new(root); directory != null; directory = directory.Parent)
			{
				string candidate = Path.Combine(directory.FullName, "parity-artifacts", "e2e", "scenarios.json");
				if (File.Exists(candidate))
					return candidate;
			}
		}
		throw new FileNotFoundException("Could not locate parity-artifacts/e2e/scenarios.json from the working or application directory.");
	}

	private static void Validate(IReadOnlyList<ScenarioDefinition> scenarios)
	{
		if (scenarios.Count == 0)
			throw new InvalidDataException("Scenario manifest must contain at least one scenario.");
		var ids = new HashSet<string>(StringComparer.Ordinal);
		foreach (ScenarioDefinition scenario in scenarios)
		{
			if (string.IsNullOrWhiteSpace(scenario.Id) ||
				scenario.Id.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
				throw new InvalidDataException($"Scenario id '{scenario.Id}' is not a stable ASCII identifier.");
			if (!ids.Add(scenario.Id))
				throw new InvalidDataException($"Scenario id '{scenario.Id}' is duplicated.");
			if (scenario.Modes == null || scenario.Modes.Length == 0 || scenario.Modes.Distinct().Count() != scenario.Modes.Length)
				throw new InvalidDataException($"Scenario '{scenario.Id}' must declare distinct modes.");
			if (scenario.Map is <= 0)
				throw new InvalidDataException($"Scenario '{scenario.Id}' has invalid map id {scenario.Map}.");
			if (scenario.ChannelNeeds != ScenarioChannelNeeds.None && scenario.Map == null)
				throw new InvalidDataException($"Scenario '{scenario.Id}' needs channel isolation but declares no map.");
			if (scenario.Bots < 0)
				throw new InvalidDataException($"Scenario '{scenario.Id}' has a negative bot count.");
			if (scenario.VirtualDuration < TimeSpan.Zero)
				throw new InvalidDataException($"Scenario '{scenario.Id}' has a negative virtual duration.");
			if (scenario.Consumes == null || scenario.Consumes.Any(resource => resource.Id <= 0) ||
				scenario.Consumes.DistinctBy(resource => (resource.Kind, resource.Id)).Count() != scenario.Consumes.Length)
				throw new InvalidDataException($"Scenario '{scenario.Id}' has invalid or duplicate consumed resources.");
			if (scenario.Requires == null || scenario.Requires.Distinct().Count() != scenario.Requires.Length)
				throw new InvalidDataException($"Scenario '{scenario.Id}' has duplicate requirements.");
			if (scenario.ExpectedFail != null && string.IsNullOrWhiteSpace(scenario.ExpectedFail))
				throw new InvalidDataException($"Scenario '{scenario.Id}' has an empty expected-failure reason.");
		}
	}
}

public sealed record ScenarioExecution(
	ScenarioDefinition Scenario,
	int Order,
	string ProcessKey,
	int? Channel,
	bool Exclusive,
	TimeSpan AdvanceBefore,
	string DatabaseShard,
	string CacheDirectoryName);

public static class ScenarioIsolationPlanner
{
	public static IReadOnlyList<ScenarioExecution> Plan(
		IReadOnlyList<ScenarioDefinition> scenarios,
		ScenarioMode mode,
		ScenarioTier tier,
		int shardCount,
		Func<ScenarioConsumedResource, TimeSpan> respawnDuration)
	{
		ArgumentNullException.ThrowIfNull(scenarios);
		ArgumentNullException.ThrowIfNull(respawnDuration);
		if (shardCount <= 0)
			throw new ArgumentOutOfRangeException(nameof(shardCount));

		ScenarioDefinition[] selected = scenarios
			.Where(scenario => scenario.Modes.Contains(mode) && scenario.Tier <= tier)
			.ToArray();
		// Processes own independent worlds, so channel allocation only has to be unique inside one process/map.
		// Reusing channel 1 in another shard also avoids exhausting the finite twin count as the manifest grows.
		var channelByProcessAndMap = new Dictionary<(string ProcessKey, int Map), int>();
		var lastByProcess = new Dictionary<string, ScenarioDefinition>(StringComparer.Ordinal);
		var result = new List<ScenarioExecution>(selected.Length);
		int sharedScenario = 0;
		for (int order = 0; order < selected.Length; order++)
		{
			ScenarioDefinition scenario = selected[order];
			string processKey = scenario.ResetEpoch
				? $"reset-{scenario.Id}"
				: $"shard-{sharedScenario++ % shardCount:D2}";
			int? channel = null;
			if (scenario.ChannelNeeds == ScenarioChannelNeeds.Dedicated)
			{
				int map = scenario.Map!.Value;
				var channelKey = (processKey, map);
				channel = channelByProcessAndMap.GetValueOrDefault(channelKey) + 1;
				channelByProcessAndMap[channelKey] = channel.Value;
			}

			TimeSpan advanceBefore = TimeSpan.Zero;
			if (lastByProcess.TryGetValue(processKey, out ScenarioDefinition? previous) && previous.Consumes.Length != 0)
			{
				TimeSpan longest = previous.Consumes.Max(respawnDuration);
				if (longest < TimeSpan.Zero)
					throw new InvalidDataException($"Scenario '{previous.Id}' resolved a negative respawn duration.");
				advanceBefore = longest + TimeSpan.FromMilliseconds(1);
			}
			lastByProcess[processKey] = scenario;
			result.Add(new ScenarioExecution(
				scenario,
				order,
				processKey,
				channel,
				scenario.ChannelNeeds == ScenarioChannelNeeds.Exclusive,
				advanceBefore,
				processKey,
				$"cache-{processKey}"));
		}
		return result;
	}
}
