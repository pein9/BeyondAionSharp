using System.Text.Json;
using System.Text.Json.Serialization;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// What the server decides per class (docs/natural-class-profiles.md, CP-01): for each Asmodian starter the Q2132
/// trainer, the Q2008 class page and the Q2009 preceptor, and for each second class its class-choice action, level-9
/// masteries, ceremony reward list and dispatch quest. Server facts only; a class line's account and name are not here.
/// </summary>
public sealed record NaturalClassLineContract(
	int SchemaVersion,
	string JavaReference,
	string Race,
	int NewSkillQuestId,
	int AscensionQuestId,
	int ClassChoiceVar,
	int CeremonyQuestId,
	NaturalClassMasteryLevels MasteryLevels,
	NaturalClassClientDialogs ClientDialogs,
	NaturalStarterClass[] Starters,
	NaturalSecondClass[] SecondClasses)
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static NaturalClassLineContract Load(string path)
	{
		NaturalClassLineContract contract = JsonSerializer.Deserialize<NaturalClassLineContract>(File.ReadAllText(path), JsonOptions)
			?? throw new InvalidDataException("Empty natural class-line contract.");
		if (contract.SchemaVersion != 1)
			throw new InvalidDataException("Unsupported natural class-line contract schema.");
		if (contract.Starters.Select(starter => starter.Class).Distinct().Count() != contract.Starters.Length ||
			contract.SecondClasses.Select(second => second.Class).Distinct().Count() != contract.SecondClasses.Length)
			throw new InvalidDataException("Natural class-line contract names a class twice.");
		foreach (NaturalStarterClass starter in contract.Starters)
		{
			if (!starter.PlayerClass.IsStartingClass() || starter.PlayerClass.GetClassId() != starter.ClassId)
				throw new InvalidDataException($"{starter.Class} is not a starting class with id {starter.ClassId}.");
			foreach (string action in starter.ClassPage.ClientActions)
				NaturalAscensionContract.DialogActionId(action);
		}
		foreach (NaturalSecondClass second in contract.SecondClasses)
		{
			if (second.PlayerClass.IsStartingClass() || second.PlayerClass.GetClassId() != second.ClassId)
				throw new InvalidDataException($"{second.Class} is not a second class with id {second.ClassId}.");
			if (contract.Starters.All(starter => starter.Class != second.Parent))
				throw new InvalidDataException($"{second.Class} names the parent {second.Parent}, which is not a starter of the contract.");
			NaturalAscensionContract.DialogActionId(second.Action);
		}
		return contract;
	}

	public static NaturalClassLineContract LoadDefault() => Load(Path.Combine(
		Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../..")),
		"parity-artifacts/e2e/natural-class-lines.json"));

	public NaturalStarterClass Starter(PlayerClass starter) => Starters.SingleOrDefault(row => row.PlayerClass == starter)
		?? throw new InvalidDataException($"{starter} is not a starter of the natural class-line contract.");

	public NaturalSecondClass Second(PlayerClass second) => SecondClasses.SingleOrDefault(row => row.PlayerClass == second)
		?? throw new InvalidDataException($"{second} is not a second class of the natural class-line contract.");

	public IEnumerable<NaturalSecondClass> SecondClassesOf(PlayerClass starter) =>
		SecondClasses.Where(row => row.PlayerClass.GetStartingClass() == starter);
}

public sealed record NaturalClassMasteryLevels(int Starter, int SecondClass);

/// <summary>Where the class-page buttons were read: the checked-in page map of the 4.8 client's quest dialogs.</summary>
public sealed record NaturalClassClientDialogs(string Map, string ClientVersion, string ClassPageButtons);

/// <summary>A mastery skill and the weapon group, armor type or shield it lets the class wear.</summary>
public sealed record NaturalClassMastery(int SkillId, string Kind, string Unlocks, int? ReplacesSkillId);

public sealed record NaturalStarterNewSkill(int Var, int RewardGroup, int TrainerNpcId, float[] TrainerPosition, int TalkRange, int PageId);

public sealed record NaturalStarterClassPage(int PageId, string ClientPage, string[] ClientActions);

public sealed record NaturalStarterCeremony(int Var, int RewardGroup, int PreceptorNpcId, float[] PreceptorPosition, int TalkRange, int PageId);

public sealed record NaturalStarterClass(string Class, int ClassId, NaturalStarterNewSkill NewSkill, NaturalStarterClassPage ClassPage,
	NaturalStarterCeremony Ceremony, NaturalClassMastery[] Masteries)
{
	[JsonIgnore]
	public PlayerClass PlayerClass => Enum.Parse<PlayerClass>(Class);
}

public sealed record NaturalSecondClassRewardItem(int ItemId, string ItemGroup);

public sealed record NaturalSecondClassCeremonyReward(string SelectableList, NaturalSecondClassRewardItem[] Items);

public sealed record NaturalSecondClassDispatch(int QuestId, int StartReward, int WorkItemId);

public sealed record NaturalSecondClass(string Class, int ClassId, string Parent, string Action, NaturalClassMastery[] Masteries,
	NaturalSecondClassCeremonyReward CeremonyReward, NaturalSecondClassDispatch Dispatch)
{
	[JsonIgnore]
	public PlayerClass PlayerClass => Enum.Parse<PlayerClass>(Class);
}
