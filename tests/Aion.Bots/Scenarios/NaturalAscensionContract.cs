using System.Reflection;
using System.Text.Json;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios;

/// <summary>
/// The reviewed Ascension bridge (docs/natural-ascension-altgard.md): Q2008 as a Cleric, Q2009, Q2904 and Q24010
/// from the Ishalgen stop to the Altgard Fortress bind. Static walkthrough knowledge only; every step is still
/// driven by what the client observes.
/// </summary>
public sealed record NaturalAscensionContract(
	int SchemaVersion,
	string JavaReference,
	NaturalAscensionStart Start,
	NaturalAscensionQuest[] Quests,
	NaturalAscensionStep[] Steps,
	NaturalAscensionClassChoice ClassChoice,
	NaturalAscensionCeremonyReward CeremonyReward,
	NaturalAscensionDispatch Dispatch,
	NaturalAscensionInstance Instance,
	NaturalAscensionTeleporter Teleporter,
	NaturalAscensionBind Bind,
	int[] Movies,
	NaturalAscensionShop Shop,
	int[] KeptAccessories,
	int[] ProtectedItemIds,
	NaturalAscensionEndpoint Endpoint)
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static NaturalAscensionContract Load(string path)
	{
		NaturalAscensionContract contract = JsonSerializer.Deserialize<NaturalAscensionContract>(File.ReadAllText(path), JsonOptions)
			?? throw new InvalidDataException("Empty Natural Ascension contract.");
		if (contract.SchemaVersion != 1)
			throw new InvalidDataException("Unsupported Natural Ascension contract schema.");
		if (contract.Steps.Length == 0 || contract.Steps.Select(step => step.Key).Distinct().Count() != contract.Steps.Length)
			throw new InvalidDataException("Natural Ascension steps are missing or have duplicate keys.");
		int[] questIds = contract.Quests.Select(quest => quest.Id).ToArray();
		if (contract.Steps.Any(step => !questIds.Contains(step.QuestId)) ||
			contract.Endpoint.CompletedQuestIds.Any(id => !questIds.Contains(id)))
			throw new InvalidDataException("Natural Ascension steps or endpoint name a quest outside the contract.");
		foreach (NaturalAscensionStep step in contract.Steps)
			foreach (string action in step.Actions)
				DialogActionId(action);
		DialogActionId(contract.ClassChoice.Action);
		DialogActionId(contract.CeremonyReward.Action);
		DialogActionId(contract.Instance.ExitAction);
		return contract;
	}

	public static NaturalAscensionContract LoadDefault() => Load(Path.Combine(
		Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../..")),
		"parity-artifacts/e2e/natural-ascension-contract.json"));

	/// <summary>The client dialog action id for a <see cref="DialogAction"/> constant name, e.g. SETPRO14 → 10013.</summary>
	public static int DialogActionId(string name) =>
		typeof(DialogAction).GetField(name, BindingFlags.Public | BindingFlags.Static) is { IsLiteral: true } field
			? (int)field.GetRawConstantValue()!
			: throw new InvalidDataException($"Unknown dialog action '{name}'.");

	public IEnumerable<NaturalAscensionStep> StepsFor(int questId) => Steps.Where(step => step.QuestId == questId);
}

public sealed record NaturalAscensionStart(string Contract, int MapId, string Race, string Class, int Level, int QuestId,
	string Status, int Var, int NpcId, long ExperienceCap);

public sealed record NaturalAscensionQuest(int Id, string Category, int MinimumLevel, int RewardExperience, int? Prerequisite);

public sealed record NaturalAscensionTeleport(int MapId, float[] Position);

public sealed record NaturalAscensionStep(string Key, int QuestId, int? Var, string? Status, int MapId, int NpcId,
	float[] Position, int TalkRange, string[] Actions, int[] Pages, int? MovieId, int? ReceivesItemId, int? FlyPathId,
	NaturalAscensionTeleport? Teleport)
{
	public string ExpectedStatus => Status ?? "START";
}

public sealed record NaturalAscensionClassChoice(int QuestId, string FromClass, string ToClass, int ClassPageId, string Action,
	int[] MasterySkillIds);

public sealed record NaturalAscensionCeremonyReward(int QuestId, int RewardGroup, string SelectableList, string Action, int ItemId,
	string ItemGroup, long Kinah, int TeaItemId, int TeaCount);

public sealed record NaturalAscensionDispatch(int QuestId, int StartsAfter, int StartReward, string[] ClassesPermitted,
	int UnhandedWorkItemId);

public sealed record NaturalAscensionTrialGroup(int NpcId, int Count, float[][] Positions);

public sealed record NaturalAscensionInstance(int MapId, float[] Entry, int FlyPathId, float[] FlyPathEnd, int FlightSeconds,
	NaturalAscensionTrialGroup[] Trial, int TrialDamage, int[] HostileNpcIds, string ExitAction, int ResetVar);

public sealed record NaturalAscensionTeleporter(int NpcId, int TeleportId, int LocationId, int BasePrice,
	NaturalAscensionTeleport Destination);

public sealed record NaturalAscensionBind(int NpcId, int MapId, float[] Position, int Price, int QuestionId, float AcceptRange);

public sealed record NaturalAscensionPurchase(int ItemId, int? Target, int? TargetCombinedLifePotions);

public sealed record NaturalAscensionShop(int SellNpcId, int PotionNpcId, int PotionGoodsList, int ReagentNpcId,
	NaturalAscensionPurchase[] Purchases, bool BuysGear);

public sealed record NaturalAscensionEndpoint(int MapId, string Class, int ClassId, int MinimumLevel,
	int[] CompletedQuestIds, int BindNpcId, int[] EquippedItemIds);
