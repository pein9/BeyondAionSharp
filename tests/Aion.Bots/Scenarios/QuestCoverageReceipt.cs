using System.Text.Json;
using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

public sealed record QuestCoverageProblem(int QuestId, string Reason);

public sealed record QuestCoverageReceipt(
	int SchemaVersion,
	string Mode,
	string Scenario,
	IReadOnlyList<int> AcceptedQuestIds,
	IReadOnlyList<int> CompletedQuestIds,
	IReadOnlyList<QuestCoverageProblem> EchoFailures,
	IReadOnlyList<QuestCoverageProblem> StuckReasons)
{
	public static string SaveFromEnvironment(
		string mode,
		string scenario,
		BotWorldModel world,
		IReadOnlyList<QuestCoverageProblem>? echoFailures = null,
		IReadOnlyList<QuestCoverageProblem>? stuckReasons = null)
	{
		string root = Environment.GetEnvironmentVariable("AION_E2E_RUN_DIR")
			?? throw new InvalidOperationException("AION_E2E_RUN_DIR is required for quest coverage receipts.");
		return Save(root, mode, scenario, world, echoFailures, stuckReasons);
	}

	public static string Save(
		string runDirectory,
		string mode,
		string scenario,
		BotWorldModel world,
		IReadOnlyList<QuestCoverageProblem>? echoFailures = null,
		IReadOnlyList<QuestCoverageProblem>? stuckReasons = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(runDirectory);
		ArgumentException.ThrowIfNullOrWhiteSpace(mode);
		ArgumentException.ThrowIfNullOrWhiteSpace(scenario);
		ArgumentNullException.ThrowIfNull(world);

		var receipt = new QuestCoverageReceipt(
			1,
			mode.ToUpperInvariant(),
			scenario.ToUpperInvariant(),
			world.AcceptedQuestIds.Order().ToArray(),
			world.CompletedQuestIds.Order().ToArray(),
			echoFailures ?? [],
			stuckReasons ?? []);
		string directory = Path.Combine(runDirectory, "quest-coverage");
		Directory.CreateDirectory(directory);
		string path = Path.Combine(directory, $"{mode.ToLowerInvariant()}-{scenario.ToLowerInvariant()}.json");
		File.WriteAllText(path, JsonSerializer.Serialize(receipt, JsonOptions));
		return path;
	}

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true,
	};
}
