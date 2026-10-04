using System.Text.Json;

namespace Aion.Bots.Scenarios;

/// <summary>Approved city walkthrough; runtime state comes from the client journal.</summary>
public sealed record NaturalCapitalContract(int SchemaVersion, string JavaReference, int MapId, int MinimumLevel,
	int[] RequiredCompletedQuestIds, int DispatchQuestId, string StartSnapshot, string EndpointSnapshot,
	NaturalCapitalQuest[] Quests, NaturalCapitalBranch Branch, int[] ProtectedItemIds, NaturalCapitalPortal[] Portals)
{
	public static NaturalCapitalContract LoadDefault() => JsonSerializer.Deserialize<NaturalCapitalContract>(
		File.ReadAllText(Path.Combine(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../..")),
			"parity-artifacts/e2e/natural-capital-contract.json")),
		new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new InvalidDataException("Empty capital contract.");
	public int[] CompletedQuestIds => Quests.Select(quest => quest.Id).ToArray();
	public static readonly int[] RouteNpcIds = [204079, 204191, 798385, 204071, 204075, 204193,
		798443, 798441, 798442, 204089, 204088, 204240, 204236, 204147, 204236, 204147,
		204092, 798317, 204053, 798304, 730268, 798700, 730269, 204191];
}

public sealed record NaturalCapitalQuest(int Id, string Name, int Experience, long Kinah);
public sealed record NaturalCapitalBranch(int QuestId, int RewardGroup, string Action, int[] ExcludedQuestIds);
public sealed record NaturalCapitalPortal(int NpcId, int MapId, int DestinationMapId, int LocationId, ushort Action);
