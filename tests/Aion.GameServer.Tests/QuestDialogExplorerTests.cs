using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

public sealed class QuestDialogExplorerTests
{
	[Fact]
	public void CheckedInKnowledgeCombinesRoslynRegistrationsAndClientButtons()
	{
		string root = RepositoryRoot();
		IReadOnlyDictionary<int, QuestDialogKnowledge> all = QuestDialogKnowledgeLoader.LoadAll(
			Path.Combine(root, "parity-artifacts/e2e/custom-quest-handler-drafts.json"),
			Path.Combine(root, "parity-artifacts/e2e/custom-quest-client-dialogs.json"));
		QuestDialogKnowledge knowledge = all[1100];
		QuestDialogKnowledge troubleWithTwos = all[19638];

		Assert.Equal(927, all.Count);
		Assert.Equal(105, all.Values.Count(entry => entry.SpecialOperations is { Count: > 0 }));
		Assert.All(all.Values.SelectMany(entry => entry.SpecialOperations ?? []), operation =>
		{
			Assert.Contains(operation.Kind, new[] { "spawn", "teleport", "instance" });
			Assert.NotEmpty(operation.CompletionOracle);
		});
		Assert.Contains(203067, knowledge.RegisteredNpcIds);
		Assert.Contains(DialogAction.QUEST_SELECT, knowledge.HandlerCandidateActions);
		Assert.Equal([DialogAction.QUEST_SELECT], troubleWithTwos.HandlerCandidateActions);
		Assert.Equal(
			[DialogAction.SELECT_QUEST_REWARD],
			knowledge.ClientPageActions["select1"]);
	}

	[Fact]
	public async Task SimExplorerLearnsAcceptedSequenceAndLiveLoaderRequiresCompletion()
	{
		var knowledge = new QuestDialogKnowledge(
			1100,
			[203067],
			[DialogAction.QUEST_SELECT, DialogAction.SETPRO1],
			new Dictionary<string, IReadOnlyList<int>>(StringComparer.OrdinalIgnoreCase)
			{
				["select1"] = [DialogAction.SELECT1_1],
			});
		var driver = new FakeDriver();

		QuestDialogLearnedScript script = await QuestDialogExplorer.ExploreAsync(
			knowledge, driver, maxAcceptedSteps: 4, CancellationToken.None);

		Assert.True(script.Complete);
		Assert.Equal([DialogAction.QUEST_SELECT, DialogAction.SETPRO1], script.Steps.Select(step => step.ActionId));
		Assert.Equal(
			[DialogAction.SELECT1_1, DialogAction.QUEST_SELECT, DialogAction.QUEST_SELECT, DialogAction.SETPRO1],
			driver.Probes);

		string path = Path.Combine(Path.GetTempPath(), $"learned-quest-{Guid.NewGuid():N}.json");
		try
		{
			script.Save(path);
			QuestDialogLearnedScript loaded = QuestDialogLearnedScript.LoadForLive(path);
			Assert.Equal(script.QuestId, loaded.QuestId);
			Assert.Equal(script.Complete, loaded.Complete);
			Assert.Equal(script.Steps, loaded.Steps);
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void LiveLoaderRejectsPartialExploration()
	{
		string path = Path.Combine(Path.GetTempPath(), $"learned-quest-{Guid.NewGuid():N}.json");
		try
		{
			new QuestDialogLearnedScript(1, 1002, false, "instance setup required", []).Save(path);
			InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
				QuestDialogLearnedScript.LoadForLive(path));
			Assert.Contains("instance setup required", exception.Message);
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void HandWrittenSpecialHandlerScriptsUseObservableCompletionSignals()
	{
		var before = new QuestSpecialObservation(
			210010000, 1, new BotPosition(1, 2, 3, 4), "objects-a", "respawns-a");

		Assert.True(QuestSpecialHandlerScripts.Resolve("spawn").IsComplete(
			before, before with { WorldObjectFingerprint = "objects-b" }));
		Assert.True(QuestSpecialHandlerScripts.Resolve("teleport").IsComplete(
			before, before with { Position = new BotPosition(2, 2, 3, 4) }));
		Assert.True(QuestSpecialHandlerScripts.Resolve("instance").IsComplete(
			before, before with { InstanceId = 2 }));
		Assert.False(QuestSpecialHandlerScripts.Resolve("spawn").IsComplete(before, before));
		Assert.Throws<InvalidDataException>(() => QuestSpecialHandlerScripts.Resolve("movie"));
	}

	[Fact]
	public void CheckedInSimLearnedScriptIsReadyForLiveReplay()
	{
		QuestDialogLearnedScript script = QuestDialogLearnedScript.LoadForLive(Path.Combine(
			RepositoryRoot(), "parity-artifacts/e2e/learned-custom-quests/1100.json"));
		Assert.Equal(1100, script.QuestId);
		Assert.Equal([DialogAction.QUEST_SELECT, DialogAction.SELECTED_QUEST_REWARD1],
			script.Steps.Select(step => step.ActionId));
	}

	private static string RepositoryRoot()
	{
		string current = AppContext.BaseDirectory;
		while (!File.Exists(Path.Combine(current, "AionServer.slnx")))
			current = Directory.GetParent(current)?.FullName
				?? throw new DirectoryNotFoundException("Could not locate repository root.");
		return current;
	}

	private sealed class FakeDriver : IQuestDialogExplorerDriver
	{
		private QuestDialogProbeState state = new(1100, 1, 203067, DialogAction.SELECT1, 1, 0);

		public List<int> Probes { get; } = [];

		public Task<QuestDialogProbeState> CaptureAsync(CancellationToken cancellationToken) => Task.FromResult(state);

		public Task<QuestDialogProbeResult> ProbeAsync(int actionId, CancellationToken cancellationToken)
		{
			Probes.Add(actionId);
			if (state.PageId == DialogAction.SELECT1 && actionId == DialogAction.QUEST_SELECT)
			{
				state = state with { PageId = 1352 };
				return Task.FromResult(new QuestDialogProbeResult(QuestDialogProbeOutcome.Advanced, state));
			}
			if (state.PageId == 1352 && actionId == DialogAction.SETPRO1)
			{
				state = state with { QuestStatus = 2, QuestVar = 1 };
				return Task.FromResult(new QuestDialogProbeResult(QuestDialogProbeOutcome.Completed, state));
			}
			return Task.FromResult(new QuestDialogProbeResult(QuestDialogProbeOutcome.Rejected, state));
		}
	}
}
