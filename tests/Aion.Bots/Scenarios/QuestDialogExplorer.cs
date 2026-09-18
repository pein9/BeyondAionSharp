using System.Reflection;
using System.Text.Json;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Services;

namespace Aion.Bots.Scenarios;

public enum QuestDialogProbeOutcome
{
	Rejected,
	Advanced,
	Completed,
	ExternalActionRequired,
}

public sealed record QuestDialogProbeState(
	int QuestId,
	int TargetObjectId,
	int TargetNpcId,
	int PageId,
	int QuestStatus,
	int QuestVar);

public sealed record QuestDialogProbeResult(
	QuestDialogProbeOutcome Outcome,
	QuestDialogProbeState State,
	string? Reason = null);

public interface IQuestDialogExplorerDriver
{
	Task<QuestDialogProbeState> CaptureAsync(CancellationToken cancellationToken);

	Task<QuestDialogProbeResult> ProbeAsync(int actionId, CancellationToken cancellationToken);
}

public sealed record QuestDialogLearnedStep(
	int TargetNpcId,
	int PageId,
	int QuestStatus,
	int QuestVar,
	int ActionId,
	string ActionName);

public sealed record QuestDialogLearnedScript(
	int SchemaVersion,
	int QuestId,
	bool Complete,
	string? StopReason,
	IReadOnlyList<QuestDialogLearnedStep> Steps)
{
	public static QuestDialogLearnedScript LoadForLive(string path)
	{
		QuestDialogLearnedScript? script = JsonSerializer.Deserialize<QuestDialogLearnedScript>(
			File.ReadAllText(path), JsonOptions);
		if (script == null)
			throw new InvalidDataException($"Learned quest script is empty: {path}");
		if (script.SchemaVersion != 1)
			throw new InvalidDataException($"Unsupported learned quest script schema {script.SchemaVersion}: {path}");
		if (!script.Complete)
			throw new InvalidDataException($"Learned quest script {path} is incomplete: {script.StopReason}");
		return script;
	}

	public void Save(string path)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
		File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
	}

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		WriteIndented = true,
	};
}

public sealed record QuestDialogKnowledge(
	int QuestId,
	IReadOnlyList<int> RegisteredNpcIds,
	IReadOnlyList<int> HandlerCandidateActions,
	IReadOnlyDictionary<string, IReadOnlyList<int>> ClientPageActions,
	IReadOnlyList<QuestSpecialOperation>? SpecialOperations = null);

public sealed record QuestSpecialOperation(
	string Kind,
	string Method,
	string Expression,
	int Line,
	QuestSpecialHandlerScript Script)
{
	public string CompletionOracle => Script.CompletionOracle;
}

public sealed record QuestSpecialObservation(
	int WorldMapId,
	int InstanceId,
	BotPosition Position,
	string WorldObjectFingerprint,
	string RespawnFingerprint);

public sealed record QuestSpecialHandlerScript(
	string Kind,
	string Preparation,
	string CompletionOracle,
	Func<QuestSpecialObservation, QuestSpecialObservation, bool> IsComplete);

public static class QuestSpecialHandlerScripts
{
	private static readonly IReadOnlyDictionary<string, QuestSpecialHandlerScript> Scripts =
		new Dictionary<string, QuestSpecialHandlerScript>(StringComparer.Ordinal)
		{
			["spawn"] = new(
				"spawn",
				"capture-world-objects-and-respawn-state",
				"world-object-or-respawn-change",
				(before, after) => before.WorldObjectFingerprint != after.WorldObjectFingerprint ||
					before.RespawnFingerprint != after.RespawnFingerprint),
			["teleport"] = new(
				"teleport",
				"capture-player-map-and-position",
				"player-map-or-position-change",
				(before, after) => before.WorldMapId != after.WorldMapId || before.Position != after.Position),
			["instance"] = new(
				"instance",
				"capture-player-map-and-instance",
				"world-map-instance-change",
				(before, after) => before.WorldMapId != after.WorldMapId || before.InstanceId != after.InstanceId),
		};

	public static QuestSpecialHandlerScript Resolve(string kind) =>
		Scripts.TryGetValue(kind, out QuestSpecialHandlerScript? script)
			? script
			: throw new InvalidDataException($"Unknown custom quest special operation kind {kind}.");
}

public static class QuestDialogKnowledgeLoader
{
	private static readonly IReadOnlyDictionary<string, int> ActionIds = typeof(DialogAction)
		.GetFields(BindingFlags.Public | BindingFlags.Static)
		.Where(field => field.IsLiteral && field.FieldType == typeof(int))
		.ToDictionary(field => field.Name, field => (int)field.GetRawConstantValue()!, StringComparer.OrdinalIgnoreCase);

	public static QuestDialogKnowledge Load(int questId, string handlerDraftsPath, string clientDialogsPath)
	{
		IReadOnlyDictionary<int, QuestDialogKnowledge> all = LoadAll(handlerDraftsPath, clientDialogsPath);
		return all.TryGetValue(questId, out QuestDialogKnowledge? knowledge)
			? knowledge
			: throw new InvalidDataException($"No custom quest knowledge exists for quest {questId}.");
	}

	public static IReadOnlyDictionary<int, QuestDialogKnowledge> LoadAll(
		string handlerDraftsPath,
		string clientDialogsPath)
	{
		using JsonDocument handlerDocument = JsonDocument.Parse(File.ReadAllText(handlerDraftsPath));
		using JsonDocument clientDocument = JsonDocument.Parse(File.ReadAllText(clientDialogsPath));
		var clientByQuest = new Dictionary<int, IReadOnlyDictionary<string, IReadOnlyList<int>>>();
		foreach (JsonElement clientQuest in clientDocument.RootElement.GetProperty("quests").EnumerateArray())
		{
			int questId = clientQuest.GetProperty("questId").GetInt32();
			var pageActions = new Dictionary<string, IReadOnlyList<int>>(StringComparer.OrdinalIgnoreCase);
			foreach (JsonElement page in clientQuest.GetProperty("pages").EnumerateArray())
			{
				string name = page.GetProperty("page").GetString()!;
				int[] actions = page.GetProperty("buttons").EnumerateArray()
					.SelectMany(button => button.GetProperty("actions").EnumerateArray())
					.Select(action => ResolveAction(action.GetString()!, $"client page {name} for quest {questId}"))
					.Distinct()
					.ToArray();
				pageActions[name] = actions;
			}
			clientByQuest.Add(questId, pageActions);
		}

		var result = new Dictionary<int, QuestDialogKnowledge>();
		foreach (JsonElement handler in handlerDocument.RootElement.GetProperty("quests").EnumerateArray())
		{
			int questId = handler.GetProperty("questId").GetInt32();
			int[] registeredNpcs = handler.GetProperty("registrations").EnumerateArray()
				.SelectMany(registration => registration.GetProperty("npcIds").EnumerateArray())
				.Select(value => value.GetInt32())
				.Distinct()
				.Order()
				.ToArray();
			int[] handlerActions = handler.GetProperty("dialogCandidateActions").EnumerateArray()
				.Select(action => ResolveAction(action.GetString()!, $"handler draft for quest {questId}"))
				.Distinct()
				.ToArray();
			QuestSpecialOperation[] specialOperations = handler.GetProperty("specialOperations").EnumerateArray()
				.Select(operation =>
				{
					string kind = operation.GetProperty("kind").GetString()!;
					return new QuestSpecialOperation(
						kind,
						operation.GetProperty("method").GetString()!,
						operation.GetProperty("expression").GetString()!,
						operation.GetProperty("line").GetInt32(),
						QuestSpecialHandlerScripts.Resolve(kind));
				})
				.ToArray();
			clientByQuest.TryGetValue(questId, out IReadOnlyDictionary<string, IReadOnlyList<int>>? pageActions);
			result.Add(questId, new QuestDialogKnowledge(
				questId,
				registeredNpcs,
				handlerActions,
				pageActions ?? new Dictionary<string, IReadOnlyList<int>>(StringComparer.OrdinalIgnoreCase),
				specialOperations));
		}
		return result;
	}

	private static int ResolveAction(string name, string source)
	{
		if (ActionIds.TryGetValue(name, out int value))
			return value;
		throw new InvalidDataException($"Unknown dialog action {name} in {source}.");
	}
}

public static class QuestDialogExplorer
{
	private static readonly int[] SharedQuestActions =
	[
		DialogAction.QUEST_SELECT,
		DialogAction.QUEST_ACCEPT_1,
		DialogAction.SELECT_QUEST_REWARD,
		DialogAction.CHECK_USER_HAS_QUEST_ITEM,
		DialogAction.SELECTED_QUEST_REWARD1,
		DialogAction.SELECTED_QUEST_NOREWARD,
	];

	public static async Task<QuestDialogLearnedScript> ExploreAsync(
		QuestDialogKnowledge knowledge,
		IQuestDialogExplorerDriver driver,
		int maxAcceptedSteps,
		CancellationToken cancellationToken)
	{
		if (maxAcceptedSteps <= 0)
			throw new ArgumentOutOfRangeException(nameof(maxAcceptedSteps));

		var steps = new List<QuestDialogLearnedStep>();
		var attempted = new HashSet<ProbeKey>();
		QuestDialogProbeState state = await driver.CaptureAsync(cancellationToken);
		if (state.QuestId != knowledge.QuestId)
			throw new InvalidOperationException($"Explorer expected quest {knowledge.QuestId}, got {state.QuestId}.");

		while (steps.Count < maxAcceptedSteps)
		{
			IReadOnlyList<int> candidates = Candidates(knowledge, state.PageId);
			bool advanced = false;
			foreach (int actionId in candidates)
			{
				var key = new ProbeKey(state.TargetNpcId, state.PageId, state.QuestStatus, state.QuestVar, actionId);
				if (!attempted.Add(key))
					continue;

				QuestDialogProbeResult result = await driver.ProbeAsync(actionId, cancellationToken);
				if (result.Outcome == QuestDialogProbeOutcome.Rejected)
					continue;
				DialogActionNameResult action = DialogActionRegistry.NameOf(actionId);
				steps.Add(new QuestDialogLearnedStep(
					state.TargetNpcId,
					state.PageId,
					state.QuestStatus,
					state.QuestVar,
					actionId,
					action.Name ?? actionId.ToString()));
				state = result.State;
				if (result.Outcome == QuestDialogProbeOutcome.Completed)
					return new QuestDialogLearnedScript(1, knowledge.QuestId, true, null, steps);
				if (result.Outcome == QuestDialogProbeOutcome.ExternalActionRequired)
					return new QuestDialogLearnedScript(1, knowledge.QuestId, false, result.Reason ?? "external action required", steps);
				advanced = true;
				break;
			}

			if (!advanced)
				return new QuestDialogLearnedScript(1, knowledge.QuestId, false, "no untried action advanced the dialog", steps);
		}

		return new QuestDialogLearnedScript(1, knowledge.QuestId, false, $"accepted-step limit {maxAcceptedSteps} reached", steps);
	}

	private static IReadOnlyList<int> Candidates(QuestDialogKnowledge knowledge, int pageId)
	{
		string? pageName = DialogActionRegistry.NameOf(pageId).Name?.ToLowerInvariant();
		IEnumerable<int> client = pageName != null && knowledge.ClientPageActions.TryGetValue(pageName, out IReadOnlyList<int>? actions)
			? actions
			: [];
		return client.Concat(knowledge.HandlerCandidateActions).Concat(SharedQuestActions).Distinct().ToArray();
	}

	private sealed record ProbeKey(int TargetNpcId, int PageId, int QuestStatus, int QuestVar, int ActionId);
}
