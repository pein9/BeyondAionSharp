namespace Aion.Bots.Scenarios;

public enum QuestRunOperationKind
{
	Prepare,
	StartAtNpc,
	StartWithItem,
	StartOnLevelUp,
	StartOnZoneEntry,
	StartOnWorldEntry,
	StartAutomatically,
	Kill,
	CollectQuestDrop,
	Gather,
	UseQuestObject,
	Report,
	Craft,
	UseSkill,
	KillInWorld,
	KillInZone,
	KillSpawned,
	ClaimReward,
}

public sealed record QuestRunOperation(
	QuestRunOperationKind Kind,
	int Count = 0,
	int ItemId = 0,
	int RecipeId = 0,
	int Sequence = 0,
	string? Zone = null,
	int MapId = 0,
	IReadOnlyList<int>? SkillIds = null,
	IReadOnlyList<QuestRunNpc>? Npcs = null,
	QuestRunSource? Source = null);

public sealed record QuestRunBook(QuestRunPlan Plan, IReadOnlyList<QuestRunOperation> Operations)
{
	private static readonly IReadOnlyDictionary<string, QuestRunOperationKind> StepKinds =
		new Dictionary<string, QuestRunOperationKind>(StringComparer.Ordinal)
		{
			["kill"] = QuestRunOperationKind.Kill,
			["report"] = QuestRunOperationKind.Report,
			["craft"] = QuestRunOperationKind.Craft,
			["useSkill"] = QuestRunOperationKind.UseSkill,
			["killinworld"] = QuestRunOperationKind.KillInWorld,
			["killinzone"] = QuestRunOperationKind.KillInZone,
			["killSpawned"] = QuestRunOperationKind.KillSpawned,
		};

	public static QuestRunBook Build(QuestRunPlan plan)
	{
		ArgumentNullException.ThrowIfNull(plan);
		var operations = new List<QuestRunOperation> { new(QuestRunOperationKind.Prepare) };
		operations.Add(StartOperation(plan));
		foreach (QuestRunStep step in plan.Steps)
		{
			if (step.Kind == "collect")
			{
				if (step.Sources.Count == 0 && plan.Template == "work_order")
					continue;
				QuestRunSource source = step.Sources.FirstOrDefault(SourceIsReachable)
					?? throw new InvalidDataException($"Q{plan.Id} item {step.ItemId} has no runnable source.");
				operations.Add(new QuestRunOperation(SourceOperation(source), step.Count, step.ItemId, Source: source));
				continue;
			}
			if (!StepKinds.TryGetValue(step.Kind, out QuestRunOperationKind kind))
				throw new InvalidDataException($"Q{plan.Id} has unsupported step kind '{step.Kind}'.");
			operations.Add(new QuestRunOperation(
				kind,
				step.Count,
				step.ItemId,
				step.RecipeId,
				step.Sequence,
				SkillIds: step.SkillIds,
				Npcs: step.Npcs));
		}
		operations.Add(new QuestRunOperation(QuestRunOperationKind.ClaimReward, Npcs: plan.EndNpcs));
		return new QuestRunBook(plan, operations);
	}

	private static QuestRunOperation StartOperation(QuestRunPlan plan) => plan.StartTrigger.Kind switch
	{
		"npc" => new QuestRunOperation(QuestRunOperationKind.StartAtNpc, Npcs: plan.StartTrigger.Npcs),
		"item" => new QuestRunOperation(QuestRunOperationKind.StartWithItem, ItemId: plan.StartTrigger.ItemId),
		"levelUp" => new QuestRunOperation(QuestRunOperationKind.StartOnLevelUp, Count: plan.StartTrigger.MinimumLevel),
		"zone" => new QuestRunOperation(QuestRunOperationKind.StartOnZoneEntry, Zone: plan.StartTrigger.Zone),
		"world" => new QuestRunOperation(QuestRunOperationKind.StartOnWorldEntry, MapId: plan.StartTrigger.MapId),
		"automatic" => new QuestRunOperation(QuestRunOperationKind.StartAutomatically),
		_ => throw new InvalidDataException($"Q{plan.Id} has unsupported start trigger '{plan.StartTrigger.Kind}'."),
	};

	private static bool SourceIsReachable(QuestRunSource source) => source.Kind switch
	{
		"questDrop" or "questObject" => source.Npc != null && (source.Npc.HandlerSpawned || source.Npc.Positions.Count != 0),
		"gatherable" => source.GatherableId > 0 && source.Positions.Count != 0,
		_ => false,
	};

	private static QuestRunOperationKind SourceOperation(QuestRunSource source) => source.Kind switch
	{
		"questDrop" => QuestRunOperationKind.CollectQuestDrop,
		"gatherable" => QuestRunOperationKind.Gather,
		"questObject" => QuestRunOperationKind.UseQuestObject,
		_ => throw new InvalidDataException($"Unsupported quest item source '{source.Kind}'."),
	};
}

public interface IQuestRunDriver
{
	Task ExecuteAsync(QuestRunPlan plan, QuestRunOperation operation, CancellationToken cancellationToken);
}

public static class QuestRunExecutor
{
	public static async Task ExecuteAsync(
		QuestRunBook book,
		IQuestRunDriver driver,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(book);
		ArgumentNullException.ThrowIfNull(driver);
		foreach (QuestRunOperation operation in book.Operations)
		{
			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				await driver.ExecuteAsync(book.Plan, operation, cancellationToken);
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				throw new InvalidOperationException(
					$"Quest operation {operation.Kind} failed: {exception.Message}", exception);
			}
		}
	}
}
