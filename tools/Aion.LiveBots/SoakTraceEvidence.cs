using System.Text.Json;
using Aion.Bots.Scenarios;

namespace Aion.LiveBots;

public sealed record SoakSubjectEvidence(string Bot, int Cohort, int Map, long Decisions,
	Dictionary<string, long> Counts, bool QuestPersisted, DateTimeOffset CompletedUtc, IReadOnlyList<long> ActivityProgressWindows);

/// <summary>Bounded-memory replay of the retained client evidence, not a second gameplay driver.</summary>
internal static class SoakTraceEvidence
{
	internal static SoakSubjectEvidence Read(IEnumerable<string> lines, string run, string bot, SoakCohort cohort,
		int seed, DateTimeOffset start, DateTimeOffset end, DateTimeOffset completed, SoakEconomyStatistics economy,
		IReadOnlyDictionary<int, SoakCookingOrder> recipes)
	{
		// Coarse sustained-work evidence, not a throughput SLA. Ambient packets/pings cannot fill a window.
		var progress = new long[checked((int)Math.Ceiling((end - start).TotalSeconds / 900))];
		var policy = new SoakLifePolicy(seed, cohort);
		var counts = cohort.Actions.ToDictionary(action => action.Activity.ToString(), _ => 0L);
		int map = 0, finalStage = 0, questOrdinal = 0, gatherOutcomes = 0, craftOutcomes = 0;
		long decisions = 0;
		bool questPersisted = false, scenarioStarted = false, activityStarted = false;
		string? active = null, account = null;
		DateTimeOffset? finished = null;
		var quests = cohort.Actions.Any(action => action.Activity == SoakActivity.Quest) ? StarterSoakQuest.ForRace(cohort.FirstRace) : [];
		foreach (string line in lines)
		{
			using var document = JsonDocument.Parse(line);
			var row = document.RootElement;
			Require(row.GetProperty("run").GetString() == run && row.GetProperty("bot").GetString() == bot, "Trace identity mismatch.");
			account ??= row.GetProperty("account").GetString();
			Require(!string.IsNullOrWhiteSpace(account) && row.GetProperty("account").GetString() == account, "Trace account changed.");
			Require(row.GetProperty("vt").ValueKind == JsonValueKind.Null, "LIVE trace has virtual time.");
			var at = row.GetProperty("ts").GetDateTimeOffset();
			Require(at <= completed.AddMilliseconds(1), "Trace extends past terminal window evidence.");
			string? packet = row.GetProperty("packet").GetString(), direction = row.GetProperty("dir").GetString();
			var fields = row.GetProperty("fields");
			if (direction == "<" && packet == "SM_PLAYER_SPAWN") map = fields.GetProperty("worldId").GetInt32();
			if (direction != "action") continue;
			Require(finished == null, "Action after scenario completion.");
			if (active != null && packet == "soak-" + active)
			{
				Require(!activityStarted && finalStage == 0, "Duplicate or late activity start.");
				activityStarted = true;
			}
			switch (packet)
			{
				case "scenario:start":
					Require(!scenarioStarted && fields.GetProperty("scenario").GetString() == "SOAK", "Duplicate/wrong scenario start.");
					scenarioStarted = true; break;
				case "soak:decision":
					Require(decisions == 0 || activityStarted, "Decision has no executed activity.");
					activityStarted = false;
					Require(scenarioStarted && finalStage == 0 && map == cohort.MapId, "Decision outside its prepared cohort map/lifecycle.");
					Require(at >= start.AddMilliseconds(-1) && at < end, "Decision outside the scheduled workload window.");
					if (decisions == 0) Require(at <= start.AddSeconds(30), "Subject did not join the shared workload start within 30 seconds.");
					var expected = policy.Next();
					active = fields.GetProperty("activity").GetString();
					Require(fields.GetProperty("cohort").GetInt32() == cohort.Number && fields.GetProperty("sequence").GetInt64() == ++decisions &&
						active == expected.Action.Activity.ToString() && fields.GetProperty("source").GetString() == expected.Action.SourceScenario,
						"Decision differs from the seeded cohort schedule.");
					counts[active!]++; RecordProgress(at); break;
				case "soak:select-channel":
					Require(cohort.MapId is 210010000 or 220010000 && fields.GetProperty("channel").GetInt32() == SoakLifePolicy.StarterChannel(cohort) &&
						fields.GetProperty("instance").GetInt32() == SoakLifePolicy.StarterChannel(cohort) + 1, "Wrong starter channel selection.");
					break;
				case "soak:quest-complete":
					Require(activityStarted && active == "Quest" && questOrdinal < quests.Count && fields.GetProperty("quest").GetInt32() == quests[questOrdinal].Id &&
						fields.GetProperty("xp").GetInt32() == quests[questOrdinal].Experience && fields.GetProperty("gold").GetInt32() == quests[questOrdinal].Gold,
						"Missing, reordered, repeated or contradictory starter quest reward.");
					questOrdinal++; RecordProgress(at); break;
				case "soak:quest-journey-persisted":
					Require(active == "Quest" && !questPersisted && questOrdinal == quests.Count && quests.Count > 0, "Invalid finite quest retirement.");
					int prologue = cohort.FirstRace == ScenarioRace.Elyos ? 1000 : 2000;
					Require(fields.GetProperty("quests").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(quests.Select(quest => quest.Id).Prepend(prologue)),
						"Persisted quest set differs from Q1/Q2.");
					questPersisted = true; policy.CompleteQuestJourney(); break;
				case "soak:gather-outcome":
				case "soak:craft-outcome":
					bool gather = packet == "soak:gather-outcome";
					Require(activityStarted && active == (gather ? "Gather" : "Craft"), "Economy outcome outside its activity.");
					int skill = fields.GetProperty("skillBefore").GetInt32(), lead = fields.GetProperty("skillDifference").GetInt32();
					if (gather)
					{
						Require(skill - 1 == lead && fields.GetProperty("channel").GetInt32() == SoakLifePolicy.StarterChannel(cohort), "Gather skill/channel evidence disagrees.");
						gatherOutcomes++;
					}
					else
					{
						Require(recipes.TryGetValue(fields.GetProperty("recipe").GetInt32(), out var recipe) && skill - recipe.Order.SkillLevel == lead,
							"Craft recipe/skill evidence disagrees.");
						craftOutcomes++;
					}
					double probability = gather ? SoakProgressProbability.Gather(lead) : SoakProgressProbability.Craft(lead);
					Require(fields.GetProperty("probabilityModel").GetString() == SoakProgressProbability.Version &&
						Math.Abs(fields.GetProperty("expectedProbability").GetDouble() - probability) < 1e-12, "Recorded probability differs from the source-derived model.");
					economy.Observe(bot, active!, probability, fields.GetProperty("success").GetBoolean()); RecordProgress(at); break;
				case "verify-final-inventory":
					Require(activityStarted && finalStage == 0 && at >= end.AddMilliseconds(-1), "Early or repeated final inventory check.");
					finalStage = 1; break;
				case "quit" when finalStage > 0:
					Require(finalStage == 1, "Final quit out of order."); finalStage = 2; break;
				case "verify-offline":
					Require(finalStage == 2, "Final offline check out of order."); finalStage = 3; break;
				case "scenario:complete":
					Require(finalStage == 3 && fields.GetProperty("scenario").GetString() == "SOAK", "Completion lacks final inventory/quit/offline checks.");
					finished = at; break;
			}
		}
		Require(finished != null && counts.Values.All(count => count > 0), "Incomplete subject or missing selected workload.");
		Require(quests.Count == 0 || questPersisted && counts["Quest"] == 1, "Finite starter journey was not persisted exactly once.");
		Require(gatherOutcomes == counts.GetValueOrDefault("Gather") && craftOutcomes >= counts.GetValueOrDefault("Craft"), "Missing economy outcomes.");
		Require((end - start).TotalSeconds < 7200 || progress.All(count => count > 0), "Subject has a fifteen-minute window without selected activity progress.");
		return new(bot, cohort.Number, cohort.MapId, decisions, counts, questPersisted, finished!.Value, progress);

		void RecordProgress(DateTimeOffset at)
		{
			if (at < start || at >= end) return; // In-flight cleanup cannot fill an earlier workload window.
			progress[(int)((at - start).TotalSeconds / 900)]++;
		}
	}

	internal static void Require(bool condition, string message)
	{
		if (!condition) throw new InvalidDataException(message);
	}
}
