using System.Text.Json;
using System.Diagnostics;
using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

public sealed record NaturalIshalgenQuestContract(int Id, int MinimumLevel, int[] Prerequisites);

public sealed record NaturalIshalgenContract(int MapId, int AscensionQuestId, int AscensionLevel,
	NaturalIshalgenQuestContract[] Quests)
{
	public int AscensionNpcId { get; init; } = 203550;

	public static NaturalIshalgenContract Load(string path)
	{
		using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
		JsonElement root = document.RootElement;
		if (root.GetProperty("schemaVersion").GetInt32() != 1)
			throw new InvalidDataException("Unsupported Natural Ishalgen contract schema.");
		var quests = root.GetProperty("quests").EnumerateArray()
			.Select(quest => new NaturalIshalgenQuestContract(
				quest.GetProperty("id").GetInt32(),
				quest.GetProperty("minimumLevel").GetInt32(),
				quest.GetProperty("prerequisites").EnumerateArray().Select(item => item.GetInt32()).ToArray()))
			.OrderBy(quest => quest.Id).ToArray();
		int expected = root.GetProperty("journey").GetProperty("includedQuestCount").GetInt32();
		if (quests.Length != expected || quests.Select(quest => quest.Id).Distinct().Count() != quests.Length ||
			quests.Any(quest => quest.Prerequisites.Any(prerequisite => !quests.Any(candidate => candidate.Id == prerequisite))))
			throw new InvalidDataException("Natural Ishalgen quest contract has missing, duplicate, or external prerequisites.");
		return new NaturalIshalgenContract(
			root.GetProperty("journey").GetProperty("mapId").GetInt32(),
			root.GetProperty("ascensionStop").GetProperty("questId").GetInt32(),
			root.GetProperty("ascensionStop").GetProperty("activationLevel").GetInt32(), quests)
		{
			AscensionNpcId = root.GetProperty("ascensionStop").GetProperty("firstObjectiveNpcId").GetInt32(),
		};
	}

	public static NaturalIshalgenContract LoadDefault() => Load(Path.Combine(
		Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../..")),
		"parity-artifacts/e2e/natural-ishalgen-contract.json"));
}

/// <summary>A copied client observation; no server oracle or mutable world model enters policy.</summary>
public sealed record NaturalIshalgenObservation(
	bool Fresh, bool JournalObserved, bool CompletedJournalObserved,
	int? MapId, ushort Level, bool IsDead,
	IReadOnlyDictionary<int, BotQuestState> Quests, IReadOnlySet<int> CompletedQuestIds,
	BotPosition? Position = null, IReadOnlyList<BotKnownObject>? ObservedObjects = null);

public sealed record NaturalDecisionCheck(string Rule, string Verdict, string Reason);
public sealed record NaturalQuestDecision(int QuestId, string Verdict, NaturalDecisionCheck[] Checks);
public sealed record NaturalDecision(
	int Sequence, string SelectedAction, int? SelectedQuestId, string Outcome, string Reason,
	NaturalDecisionCheck[] GlobalChecks, NaturalQuestDecision[] Quests);

public static class NaturalIshalgenDecisionEngine
{
	public static NaturalDecision Decide(NaturalIshalgenContract contract, NaturalIshalgenObservation state, int sequence)
	{
		var global = new List<NaturalDecisionCheck>();
		if (!state.Fresh || !state.JournalObserved || !state.CompletedJournalObserved || state.MapId == null)
		{
			global.Add(new("client-observation", "unknown",
				$"fresh={state.Fresh}; journal={state.JournalObserved}; completedJournal={state.CompletedJournalObserved}; map={state.MapId?.ToString() ?? "unknown"}"));
			return new(sequence, "refresh-observation", null, "planned", "Wait for a synchronized client view.", [.. global], []);
		}
		global.Add(new("client-observation", "pass", "Client journal, completed journal, and map were observed."));
		bool inRaeInstance = state.MapId == 320010000 &&
			contract.Quests.Any(quest => quest.Id == 2002) &&
			state.Quests.TryGetValue(2002, out BotQuestState? rae) &&
			rae.Status == 3 && rae.StepAndFlags == 99;
		if (state.MapId != contract.MapId && !inRaeInstance)
			return Stop("wrong-map", $"Observed map {state.MapId}, expected {contract.MapId}.", "blocked");
		if (inRaeInstance)
			global.Add(new("quest-transport", "pass", "Q2002 START/99 authorizes temporary Ataxiar map 320010000; return to Ishalgen by quest dialogue."));
		if (state.Level >= 10)
			return Stop("pre-ascension-level", $"Observed level {state.Level}; the journey must stop below level 10.", "blocked");
		if (state.CompletedQuestIds.Contains(contract.AscensionQuestId))
			return Stop("ascension-boundary", "Ascension is already completed in the client journal.", "blocked");
		if (state.IsDead)
			return Stop("survival", "Death recovery requires NI-04.", "awaiting-capability");
		if (state.Level >= contract.AscensionLevel &&
			state.Quests.TryGetValue(contract.AscensionQuestId, out BotQuestState? ascension) &&
			(ascension.Status != 3 || ascension.StepAndFlags != 0))
			return Stop("ascension-boundary", "Ascension advanced beyond START/0.", "blocked");
		global.Add(new("journey-boundary", "pass", $"Map {state.MapId}, level {state.Level}, Ascension untouched."));

		var candidates = new List<(int Priority, int MinimumLevel, int Id, string Action)>();
		var quests = new List<NaturalQuestDecision>(contract.Quests.Length);
		foreach (NaturalIshalgenQuestContract quest in contract.Quests)
		{
			var checks = new List<NaturalDecisionCheck>();
			if (state.CompletedQuestIds.Contains(quest.Id))
			{
				checks.Add(new("completion", "pass", "Completed in the client journal."));
				quests.Add(new(quest.Id, "complete", [.. checks]));
				continue;
			}
			checks.Add(new("completion", "pass", "Not completed in the client journal."));
			if (state.Level < quest.MinimumLevel)
				checks.Add(new("level", "blocked", $"Level {state.Level} is below {quest.MinimumLevel}."));
			else
				checks.Add(new("level", "pass", $"Level {state.Level} meets {quest.MinimumLevel}."));
			int[] missing = quest.Prerequisites.Where(id => !state.CompletedQuestIds.Contains(id)).ToArray();
			checks.Add(new("prerequisites", missing.Length == 0 ? "pass" : "blocked",
				missing.Length == 0 ? "All frozen prerequisites completed." : $"Missing completed quests: {string.Join(", ", missing)}."));
			if (checks.Any(check => check.Verdict == "blocked"))
			{
				quests.Add(new(quest.Id, "blocked", [.. checks]));
				continue;
			}
			if (state.Quests.TryGetValue(quest.Id, out BotQuestState? active) && active.Status is >= 3 and < 5)
			{
				checks.Add(new("client-journal", "pass", $"Active status {active.Status}, step/flags {active.StepAndFlags}."));
				checks.Add(new("objective-handler", "unknown", "Natural quest objective handling arrives in NI-03 through NI-07."));
				quests.Add(new(quest.Id, "active", [.. checks]));
				candidates.Add((0, quest.MinimumLevel, quest.Id, "continue-quest"));
			}
			else
			{
				checks.Add(new("starter-observation", "unknown", "Contract gates pass; no starter interaction has been observed."));
				quests.Add(new(quest.Id, "candidate", [.. checks]));
				candidates.Add((1, quest.MinimumLevel, quest.Id, "find-quest-starter"));
			}
		}
		if (quests.All(quest => quest.Verdict == "complete"))
		{
			if (state.Level == contract.AscensionLevel &&
				state.Quests.TryGetValue(contract.AscensionQuestId, out BotQuestState? stop) &&
				stop.Status == 3 && stop.StepAndFlags == 0)
			{
				BotKnownObject? munin = state.ObservedObjects?.FirstOrDefault(item =>
					item.Kind == BotKnownObjectKind.Npc && item.TemplateId == contract.AscensionNpcId);
				if (state.Position is BotPosition position && munin != null &&
					MathF.Sqrt(MathF.Pow(position.X - munin.Position.X, 2) +
						MathF.Pow(position.Y - munin.Position.Y, 2) +
						MathF.Pow(position.Z - munin.Position.Z, 2)) <= 6f)
					return new(sequence, "journey-complete", null, "complete",
						$"All included quests complete; standing at client-observed Munin {munin.ObjectId} with untouched Ascension START/0.",
						[.. global], [.. quests]);
				return new(sequence, "approach-ascension-npc", null, "awaiting-capability",
					$"All included quests complete; approach client-observed Munin (template {contract.AscensionNpcId}) without Q{contract.AscensionQuestId} dialogue.",
					[.. global], [.. quests]);
			}
			return new(sequence, "await-level-or-ascension", null, "awaiting-capability",
				"All included quests complete; the level-9 Ascension stop has not been observed.", [.. global], [.. quests]);
		}
		var selected = candidates.OrderBy(candidate => candidate.Priority)
			.ThenBy(candidate => candidate.MinimumLevel).ThenBy(candidate => candidate.Id).FirstOrDefault();
		if (selected.Id == 0)
			return new(sequence, "await-progression", null, "awaiting-capability",
				"No quest passes the current level and prerequisite gates.", [.. global], [.. quests]);
		return new(sequence, selected.Action, selected.Id, "awaiting-capability",
			$"Q{selected.Id} wins by active-journal priority, then minimum level, then quest ID. " +
			"Execution needs the corresponding natural gameplay capability.", [.. global], [.. quests]);

		NaturalDecision Stop(string action, string reason, string outcome)
		{
			global.Add(new(action, outcome == "blocked" ? "blocked" : "unknown", reason));
			return new(sequence, action, null, outcome, reason, [.. global], []);
		}
	}
}

public interface INaturalIshalgenDecisionDriver
{
	NaturalIshalgenObservation Observe(bool fresh);
	Task RefreshAsync(CancellationToken token);
	void Record(NaturalDecision decision);
}

public static class NaturalIshalgenDecisionLoop
{
	public static async Task<NaturalDecision> RunAsync(
		NaturalIshalgenContract contract, INaturalIshalgenDecisionDriver driver,
		int maximumRefreshes = 2, CancellationToken token = default)
	{
		if (maximumRefreshes < 1) throw new ArgumentOutOfRangeException(nameof(maximumRefreshes));
		for (int sequence = 1; sequence <= maximumRefreshes + 1; sequence++)
		{
			NaturalDecision decision = NaturalIshalgenDecisionEngine.Decide(
				contract, driver.Observe(sequence > 1), sequence);
			if (decision.SelectedAction != "refresh-observation")
			{
				driver.Record(decision);
				return decision;
			}
			if (sequence > maximumRefreshes)
			{
				decision = decision with { Outcome = "blocked", Reason = "Client observation stayed incomplete after bounded refreshes." };
				driver.Record(decision);
				return decision;
			}
			try
			{
				await driver.RefreshAsync(token);
				decision = decision with { Outcome = "completed", Reason = "Client synchronization request completed; reevaluating observed packets." };
			}
			catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
			catch (Exception exception)
			{
				decision = decision with { Outcome = "blocked", Reason = $"Client synchronization failed: {exception.Message}" };
				driver.Record(decision);
				return decision;
			}
			driver.Record(decision);
		}
		throw new UnreachableException();
	}
}
