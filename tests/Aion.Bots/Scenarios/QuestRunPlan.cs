using System.Globalization;
using System.Text.Json;

namespace Aion.Bots.Scenarios;

public sealed record QuestRunPosition(int MapId, float X, float Y, float Z, int Heading, string Source, bool ConditionalEvent);

public sealed record QuestRunNpc(int Id, string? Name, IReadOnlyList<QuestRunPosition> Positions, bool HandlerSpawned);

public sealed record QuestRunSource(
	string Kind,
	int? NpcId,
	int? GatherableId,
	int SkillLevel,
	IReadOnlyList<QuestRunPosition> Positions,
	QuestRunNpc? Npc);

public sealed record QuestRunStep(
	string Kind,
	int Count,
	int ItemId,
	int RecipeId,
	int Sequence,
	IReadOnlyList<int> SkillIds,
	IReadOnlyList<QuestRunNpc> Npcs,
	IReadOnlyList<QuestRunSource> Sources,
	JsonElement Data);

public sealed record QuestRunTrigger(
	string Kind,
	int ItemId,
	int MinimumLevel,
	string? Zone,
	int MapId,
	IReadOnlyList<QuestRunNpc> Npcs);

public sealed record QuestRunPlan(
	int SchemaVersion,
	int Id,
	string Name,
	string? Zone,
	string? Race,
	int MinimumLevel,
	IReadOnlyList<IReadOnlyList<int>> FinishedQuestGroups,
	string Template,
	QuestRunTrigger StartTrigger,
	IReadOnlyList<QuestRunNpc> StartNpcs,
	IReadOnlyList<QuestRunNpc> EndNpcs,
	IReadOnlyList<QuestRunStep> Steps,
	bool HasSelectableReward)
{
	public static QuestRunPlan Load(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		try
		{
			using var input = File.OpenRead(path);
			using JsonDocument document = JsonDocument.Parse(input);
			return Parse(document.RootElement, path);
		}
		catch (JsonException exception)
		{
			throw new InvalidDataException($"Quest run plan '{path}' is invalid: {exception.Message}", exception);
		}
	}

	public static IReadOnlyList<QuestRunPlan> LoadDirectory(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		if (!Directory.Exists(path))
			throw new DirectoryNotFoundException($"Quest run plan directory does not exist: {path}");
		return Directory.EnumerateFiles(path, "*.json", SearchOption.TopDirectoryOnly)
			.OrderBy(file => int.Parse(Path.GetFileNameWithoutExtension(file), CultureInfo.InvariantCulture))
			.Select(Load)
			.ToArray();
	}

	private static QuestRunPlan Parse(JsonElement root, string path)
	{
		int schemaVersion = RequiredInt(root, "schemaVersion", path);
		if (schemaVersion != 1)
			throw new InvalidDataException($"Quest run plan '{path}' has unsupported schema {schemaVersion}.");
		JsonElement quest = RequiredObject(root, "quest", path);
		JsonElement gates = RequiredObject(root, "gates", path);
		JsonElement handler = RequiredObject(root, "handler", path);
		if (!string.Equals(RequiredString(handler, "kind", path), "template", StringComparison.Ordinal))
			throw new InvalidDataException($"Quest run plan '{path}' is not an XML-template plan.");

		return new QuestRunPlan(
			schemaVersion,
			RequiredInt(quest, "id", path),
			RequiredString(quest, "name", path),
			OptionalString(quest, "zone"),
			OptionalString(quest, "race"),
			RequiredInt(gates, "minimumLevel", path),
			ParseFinishedQuestGroups(gates),
			RequiredString(handler, "template", path),
			ParseTrigger(RequiredObject(root, "startTrigger", path), path),
			ParseNpcs(RequiredArray(root, "startNpcs", path), path),
			ParseNpcs(RequiredArray(root, "endNpcs", path), path),
			ParseSteps(RequiredArray(root, "steps", path), path),
			ParseHasSelectableReward(RequiredObject(root, "rewards", path)));
	}

	private static bool ParseHasSelectableReward(JsonElement rewards)
	{
		if (rewards.TryGetProperty("standard", out JsonElement standard) && standard.EnumerateArray().Any(
			reward => reward.TryGetProperty("selectableItems", out JsonElement items) && items.GetArrayLength() != 0))
			return true;
		if (!rewards.TryGetProperty("classSelectable", out JsonElement classSelectable))
			return false;
		return classSelectable.EnumerateObject().Any(group => group.Value.GetArrayLength() != 0);
	}

	private static IReadOnlyList<IReadOnlyList<int>> ParseFinishedQuestGroups(JsonElement gates)
	{
		if (!gates.TryGetProperty("startConditions", out JsonElement conditions) || conditions.ValueKind != JsonValueKind.Array)
			return [];
		return conditions.EnumerateArray()
			.Select(condition => condition.TryGetProperty("finished", out JsonElement finished) && finished.ValueKind == JsonValueKind.Array
				? (IReadOnlyList<int>)finished.EnumerateArray().Select(item => item.GetProperty("quest_id").GetInt32()).ToArray()
				: [])
			.Where(group => group.Count != 0)
			.ToArray();
	}

	private static QuestRunTrigger ParseTrigger(JsonElement value, string path) => new(
		RequiredString(value, "kind", path),
		OptionalInt(value, "itemId"),
		OptionalInt(value, "minimumLevel"),
		OptionalString(value, "zone"),
		OptionalInt(value, "mapId"),
		value.TryGetProperty("npcs", out JsonElement npcs) ? ParseNpcs(npcs, path) : []);

	private static IReadOnlyList<QuestRunStep> ParseSteps(JsonElement values, string path) => values
		.EnumerateArray()
		.Select(value => new QuestRunStep(
			RequiredString(value, "kind", path),
			OptionalInt(value, "count"),
			OptionalInt(value, "item_id"),
			OptionalInt(value, "recipeId"),
			OptionalInt(value, "sequence"),
			ParseIntList(value, "ids", "skill_ids", "skill_id"),
			value.TryGetProperty("npcs", out JsonElement npcs) ? ParseNpcs(npcs, path) : [],
			value.TryGetProperty("sources", out JsonElement sources) ? ParseSources(sources, path) : [],
			value.Clone()))
		.ToArray();

	private static IReadOnlyList<QuestRunSource> ParseSources(JsonElement values, string path) => values
		.EnumerateArray()
		.Select(value => new QuestRunSource(
			RequiredString(value, "kind", path),
			value.TryGetProperty("npc", out JsonElement npc) ? RequiredInt(npc, "id", path) : null,
			value.TryGetProperty("gatherableId", out JsonElement gatherable) ? gatherable.GetInt32() : null,
			OptionalInt(value, "skillLevel"),
			value.TryGetProperty("positions", out JsonElement positions) ? ParsePositions(positions, path) : [],
			value.TryGetProperty("npc", out npc) ? ParseNpc(npc, path) : null))
		.ToArray();

	private static IReadOnlyList<QuestRunNpc> ParseNpcs(JsonElement values, string path) => values
		.EnumerateArray()
		.Select(value => ParseNpc(value, path))
		.ToArray();

	private static QuestRunNpc ParseNpc(JsonElement value, string path) => new(
		RequiredInt(value, "id", path),
		OptionalString(value, "name"),
		ParsePositions(RequiredArray(value, "positions", path), path),
		value.TryGetProperty("handlerSpawned", out JsonElement spawned) && spawned.GetBoolean());

	private static IReadOnlyList<QuestRunPosition> ParsePositions(JsonElement values, string path) => values
		.EnumerateArray()
		.Select(value => new QuestRunPosition(
			RequiredInt(value, "mapId", path),
			RequiredFloat(value, "x", path),
			RequiredFloat(value, "y", path),
			RequiredFloat(value, "z", path),
			RequiredInt(value, "heading", path),
			RequiredString(value, "source", path),
			value.TryGetProperty("conditionalEvent", out JsonElement conditional) && conditional.GetBoolean()))
		.ToArray();

	private static IReadOnlyList<int> ParseIntList(JsonElement value, params string[] names)
	{
		foreach (string name in names)
		{
			if (!value.TryGetProperty(name, out JsonElement item))
				continue;
			if (item.ValueKind == JsonValueKind.Number)
				return [item.GetInt32()];
			if (item.ValueKind == JsonValueKind.String)
				return item.GetString()!.Split(' ', StringSplitOptions.RemoveEmptyEntries)
					.Select(part => int.Parse(part, CultureInfo.InvariantCulture)).ToArray();
		}
		return [];
	}

	private static JsonElement RequiredObject(JsonElement value, string name, string path)
	{
		if (!value.TryGetProperty(name, out JsonElement property) || property.ValueKind != JsonValueKind.Object)
			throw new InvalidDataException($"Quest run plan '{path}' needs object '{name}'.");
		return property;
	}

	private static JsonElement RequiredArray(JsonElement value, string name, string path)
	{
		if (!value.TryGetProperty(name, out JsonElement property) || property.ValueKind != JsonValueKind.Array)
			throw new InvalidDataException($"Quest run plan '{path}' needs array '{name}'.");
		return property;
	}

	private static string RequiredString(JsonElement value, string name, string path)
	{
		string? result = OptionalString(value, name);
		return string.IsNullOrWhiteSpace(result)
			? throw new InvalidDataException($"Quest run plan '{path}' needs string '{name}'.")
			: result;
	}

	private static string? OptionalString(JsonElement value, string name) =>
		value.TryGetProperty(name, out JsonElement property) && property.ValueKind == JsonValueKind.String
			? property.GetString()
			: null;

	private static int RequiredInt(JsonElement value, string name, string path) =>
		value.TryGetProperty(name, out JsonElement property) && property.TryGetInt32(out int result)
			? result
			: throw new InvalidDataException($"Quest run plan '{path}' needs integer '{name}'.");

	private static int OptionalInt(JsonElement value, string name) =>
		value.TryGetProperty(name, out JsonElement property) && property.TryGetInt32(out int result) ? result : 0;

	private static float RequiredFloat(JsonElement value, string name, string path) =>
		value.TryGetProperty(name, out JsonElement property) && property.TryGetSingle(out float result)
			? result
			: throw new InvalidDataException($"Quest run plan '{path}' needs number '{name}'.");
}
