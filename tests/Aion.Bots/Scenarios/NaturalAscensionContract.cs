using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aion.Bots.Scenarios.Classes;
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
		return Checked(contract);
	}

	private static NaturalAscensionContract Checked(NaturalAscensionContract contract)
	{
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

	/// <summary>CP-26: the starter this bridge begins with.</summary>
	[JsonIgnore]
	public PlayerClass StarterClass => Enum.Parse<PlayerClass>(ClassChoice.FromClass);

	/// <summary>The class this bridge chooses at Ascension.</summary>
	[JsonIgnore]
	public PlayerClass SecondClass => Enum.Parse<PlayerClass>(ClassChoice.ToClass);

	/// <summary>CP-25: one of the four steps that depend on the class pair, found by what it does, not by its key.</summary>
	public NaturalAscensionStep Step(NaturalAscensionStepRole role) => role switch
	{
		NaturalAscensionStepRole.ClassChoice => Steps.Single(step => step.QuestId == ClassChoice.QuestId && step.Actions.Contains(ClassChoice.Action)),
		NaturalAscensionStepRole.Ceremony => Steps.Single(step => step.QuestId == CeremonyReward.QuestId && step.ExpectedStatus == "REWARD"),
		NaturalAscensionStepRole.DispatchStart => Steps.Single(step => step.QuestId == Dispatch.QuestId && step.Var == 0),
		NaturalAscensionStepRole.DispatchReward => Steps.Single(step => step.QuestId == Dispatch.QuestId && step.Var != 0),
		_ => throw new ArgumentOutOfRangeException(nameof(role)),
	};

	/// <summary>The bridge of a class line that takes a second class. The accepted line's is the reviewed file's, value
	/// for value.</summary>
	public static NaturalAscensionContract ForLine(NaturalClassLine line, int? ceremonyItemId = null) => line.Second is { } second
		? ForChoice(LoadDefault(), NaturalClassLineContract.LoadDefault(), line.Starter, second, ceremonyItemId)
		: throw new InvalidOperationException(
			$"Class line {line.Id} takes no second class, so it has no Ascension bridge: run it with the bridge off, to the Munin stop.");

	/// <summary>
	/// CP-25: the bridge of another class pair, built in memory from the reviewed bridge and the class-line contract
	/// (docs/natural-class-profiles.md, the seam, section 3). What changes with the pair: the class page and the action
	/// of the choice, the masteries, the Q2009 var with its preceptor, page and reward list, the ceremony pick, the
	/// dispatch quest with its two steps, the endpoint's class and the protected ceremony item. Everything else is the
	/// reviewed bridge: Java's six dispatch handlers are one handler with six quest ids, and the six quests differ only in
	/// class, start reward and work item.
	/// </summary>
	/// <param name="ceremonyItemId">The weapon taken at the ceremony, an operator decision; unset means the reviewed
	/// bridge's pick, which the second class's reward list must then offer.</param>
	public static NaturalAscensionContract ForChoice(NaturalAscensionContract core, NaturalClassLineContract classLines,
		PlayerClass starter, PlayerClass second, int? ceremonyItemId = null)
	{
		NaturalStarterClass from = classLines.Starter(starter), coreFrom = classLines.Starter(Enum.Parse<PlayerClass>(core.ClassChoice.FromClass));
		NaturalSecondClass to = classLines.Second(second);
		if (to.Parent != from.Class)
			throw new InvalidDataException($"{second} is not a second class of {starter}.");
		if (core.ClassChoice.QuestId != classLines.AscensionQuestId || core.CeremonyReward.QuestId != classLines.CeremonyQuestId)
			throw new InvalidDataException("The Ascension bridge and the class-line contract name different Ascension or ceremony quests.");
		int pick = ceremonyItemId ?? core.CeremonyReward.ItemId;
		int index = Array.FindIndex(to.CeremonyReward.Items, item => item.ItemId == pick);
		if (index < 0)
			throw new InvalidDataException($"The {second}'s ceremony list {to.CeremonyReward.SelectableList} does not offer item {pick}.");
		string pickAction = $"SELECTED_QUEST_REWARD{index + 1}";
		int oldDispatch = core.Dispatch.QuestId, newDispatch = to.Dispatch.QuestId;
		NaturalAscensionStep choice = core.Step(NaturalAscensionStepRole.ClassChoice), ceremony = core.Step(NaturalAscensionStepRole.Ceremony);

		NaturalAscensionStep[] steps = core.Steps.Select(step =>
			ReferenceEquals(step, choice) ? step with
			{
				Actions = Swap(step.Actions, core.ClassChoice.Action, to.Action),
				Pages = Swap(step.Pages, core.ClassChoice.ClassPageId, from.ClassPage.PageId),
			}
			: ReferenceEquals(step, ceremony) ? step with
			{
				// The reviewed key names the Priest's preceptor; another starter's preceptor is named by its id.
				Key = from.Ceremony.PreceptorNpcId == step.NpcId ? step.Key : $"q{step.QuestId}-reward-preceptor-{from.Ceremony.PreceptorNpcId}",
				Var = from.Ceremony.Var,
				NpcId = from.Ceremony.PreceptorNpcId,
				Position = from.Ceremony.PreceptorPosition,
				TalkRange = from.Ceremony.TalkRange,
				Actions = Swap(step.Actions, core.CeremonyReward.Action, pickAction),
				// CP-67: the reward window is the reward group's (Java AbstractQuestHandler.sendQuestEndDialog with
				// DialogPage.getRewardPageByIndex), and the group is the starter's.
				Pages = Swap(Swap(step.Pages, coreFrom.Ceremony.PageId, from.Ceremony.PageId),
					RewardPage(coreFrom.Ceremony.RewardGroup), RewardPage(from.Ceremony.RewardGroup)),
			}
			: step.QuestId == oldDispatch ? step with
			{
				Key = step.Key.Replace($"q{oldDispatch}-", $"q{newDispatch}-", StringComparison.Ordinal),
				QuestId = newDispatch,
			}
			: step).ToArray();

		return Checked(core with
		{
			Start = core.Start with { Class = from.Class },
			Quests = core.Quests.Select(quest => quest.Id == oldDispatch ? quest with { Id = newDispatch } : quest).ToArray(),
			Steps = steps,
			ClassChoice = core.ClassChoice with
			{
				FromClass = from.Class,
				ToClass = to.Class,
				ClassPageId = from.ClassPage.PageId,
				Action = to.Action,
				MasterySkillIds = to.Masteries.Select(mastery => mastery.SkillId).ToArray(),
			},
			CeremonyReward = core.CeremonyReward with
			{
				RewardGroup = from.Ceremony.RewardGroup,
				SelectableList = to.CeremonyReward.SelectableList,
				Action = pickAction,
				ItemId = pick,
				ItemGroup = to.CeremonyReward.Items[index].ItemGroup,
			},
			Dispatch = core.Dispatch with
			{
				QuestId = newDispatch,
				StartReward = to.Dispatch.StartReward,
				ClassesPermitted = classLines.SecondClasses.Where(row => row.Dispatch.QuestId == newDispatch).Select(row => row.Class)
					.Order(StringComparer.Ordinal).ToArray(),
				UnhandedWorkItemId = to.Dispatch.WorkItemId,
			},
			ProtectedItemIds = Swap(core.ProtectedItemIds, core.CeremonyReward.ItemId, pick),
			Endpoint = core.Endpoint with
			{
				Class = to.Class,
				ClassId = to.ClassId,
				CompletedQuestIds = Swap(core.Endpoint.CompletedQuestIds, oldDispatch, newDispatch),
				EquippedItemIds = Swap(core.Endpoint.EquippedItemIds, core.CeremonyReward.ItemId, pick),
			},
		});
	}

	/// <summary>The reward window the server shows for a quest's reward group: 5 to 8 for groups 0 to 3, then 45 and up.</summary>
	private static int RewardPage(int rewardGroup) => DialogPageExtensions.GetRewardPageByIndex(rewardGroup).Id();

	/// <summary>The values with the one occurrence of <paramref name="old"/> replaced; the reviewed bridge must hold it once.</summary>
	private static T[] Swap<T>(T[] values, T old, T replacement)
	{
		if (values.Count(value => EqualityComparer<T>.Default.Equals(value, old)) != 1)
			throw new InvalidDataException($"The reviewed Ascension bridge does not hold {old} exactly once where a class pair replaces it.");
		return values.Select(value => EqualityComparer<T>.Default.Equals(value, old) ? replacement : value).ToArray();
	}
}

/// <summary>The four steps of the Ascension bridge that depend on the class pair.</summary>
public enum NaturalAscensionStepRole
{
	/// <summary>Q2008 at Munin: the class page and the choice.</summary>
	ClassChoice,
	/// <summary>Q2009 at the starter's preceptor: the ceremony reward and its pick.</summary>
	Ceremony,
	/// <summary>The dispatch quest at Doman, var 0.</summary>
	DispatchStart,
	/// <summary>The dispatch quest's turn-in at Meiyer.</summary>
	DispatchReward,
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
