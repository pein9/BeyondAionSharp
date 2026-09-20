using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aion.Bots.Scenarios;
using Aion.LiveBots;

namespace Aion.GameServer.Tests;

public sealed class SoakWorkloadEvidenceTests
{
	private static readonly DateTimeOffset Start = new(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);
	private static ScenarioManifest Manifest => ScenarioManifest.Load(ScenarioManifest.FindDefaultPath());
	private static SoakCohort Cohort(params SoakActivity[] activities) =>
		SoakLifePolicy.CreatePopulation(10, Manifest)[0] with { Actions = activities.Select(activity => new SoakAction(activity,
			activity == SoakActivity.Quest ? "Q1" : activity == SoakActivity.Gather ? "E1" : "L0")).ToArray() };

	[Fact]
	public void CompleteDiagnosticIsHashBoundButNeverCapacityAcceptance()
	{
		WithRun(path =>
		{
			var report = SoakWorkloadEvidence.Analyze(path, Manifest);
			Assert.Equal("passed", report.Status);
			Assert.False(report.CapacityConfiguration);
			Assert.False(report.OverallSoakAccepted);
			Assert.Equal(10, report.Subjects.Count);
			Assert.Equal("insufficient", report.RecomputedEconomy!.Status);
			Assert.Equal(15, report.SourceSha256.Count); // Eleven traces and four JSON inputs.
			foreach (var source in report.SourceSha256)
				Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(path, source.Key)))).ToLowerInvariant(), source.Value);
		});
	}

	[Theory]
	[InlineData("missing-subject")]
	[InlineData("extra-subject")]
	[InlineData("missing-completion")]
	[InlineData("counter")]
	[InlineData("running-window")]
	[InlineData("forged-window-end")]
	[InlineData("economy-summary")]
	[InlineData("director-active")]
	[InlineData("truncated-json")]
	public void MissingOrContradictoryEvidenceCannotPass(string mutation)
	{
		WithRun(path =>
		{
			string subject = Path.Combine(path, "bots/b01.trace.jsonl");
			switch (mutation)
			{
				case "missing-subject": File.Delete(subject); break;
				case "extra-subject": File.Copy(subject, Path.Combine(path, "bots/b99.trace.jsonl")); break;
				case "missing-completion": File.WriteAllLines(subject, File.ReadAllLines(subject).SkipLast(1)); break;
				case "counter": Edit("soak-runtime.json", root => root["Cohorts"]![0]!["Actions"] = 999); break;
				case "running-window": Edit("soak-window.json", root => root["Status"] = "running"); break;
				case "forged-window-end": Edit("soak-window.json", root => root["EndedUtc"] = Start.AddSeconds(101)); break;
				case "economy-summary": Edit("soak-economy.json", root => root["Status"] = "passed"); break;
				case "director-active": File.AppendAllLines(Path.Combine(path, "bots/gm.trace.jsonl"), [Row("gm", "command", new { }, Start.AddSeconds(2))]); break;
				case "truncated-json": File.AppendAllText(subject, "{"); break;
			}
			var report = SoakWorkloadEvidence.Analyze(path, Manifest);
			Assert.Equal("failed", report.Status);
			Assert.False(report.OverallSoakAccepted);
			Assert.NotEmpty(report.Failures);
			void Edit(string file, Action<JsonNode> change)
			{
				string target = Path.Combine(path, file);
				var json = JsonNode.Parse(File.ReadAllText(target))!; change(json); File.WriteAllText(target, json.ToJsonString());
			}
		});
	}

	[Theory]
	[InlineData("no-activity")]
	[InlineData("wrong-map")]
	[InlineData("early-final")]
	[InlineData("wrong-order")]
	[InlineData("sequence")]
	[InlineData("seed")]
	[InlineData("wrong-identity")]
	public void SubjectTraceMustProveItsActualScheduleAndFinalChecks(string mutation)
	{
		var cohort = Cohort(SoakActivity.Relog, SoakActivity.CrashDisconnect);
		var lines = Trace("b01", cohort);
		switch (mutation)
		{
			case "no-activity": lines.RemoveAll(line => line.Contains("\"packet\":\"soak-Relog\"")); break;
			case "wrong-map": lines[1] = Row("b01", "SM_PLAYER_SPAWN", new { worldId = 1 }, Start, "<"); break;
			case "early-final": lines[^4] = Row("b01", "verify-final-inventory", new { }, Start.AddSeconds(50)); break;
			case "wrong-order": (lines[^3], lines[^2]) = (lines[^2], lines[^3]); break;
			case "sequence": lines[2] = lines[2].Replace("\"sequence\":1", "\"sequence\":2"); break;
			case "wrong-identity": lines[0] = lines[0].Replace("\"b01\"", "\"b99\""); break;
		}
		Assert.Throws<InvalidDataException>(() => Read(lines, cohort, new([]), mutation == "seed" ? 928 : 73));
	}

	[Fact]
	public void QuestRetirementRequiresEveryRewardAndOnePersistedJourney()
	{
		var cohort = Cohort(SoakActivity.Quest, SoakActivity.Relog);
		var lines = Trace("b01", cohort);
		var report = Read(lines, cohort, new([]));
		Assert.True(report.QuestPersisted);
		Assert.Equal(1, report.Counts["Quest"]);
		Assert.Equal(19, report.Counts["Relog"]);
		var missing = lines.Where(line => !line.Contains("soak:quest-journey-persisted")).ToArray();
		Assert.Throws<InvalidDataException>(() => Read(missing, cohort, new([])));
		var duplicate = lines.ToList();
		int index = duplicate.FindIndex(line => line.Contains("soak:quest-complete"));
		duplicate.Insert(index, duplicate[index]);
		Assert.Throws<InvalidDataException>(() => Read(duplicate, cohort, new([])));
	}

	[Fact]
	public void EconomyProbabilitiesAreRecomputedNotTrustedFromTrace()
	{
		var cohort = Cohort(SoakActivity.Gather, SoakActivity.Relog);
		var lines = Trace("b01", cohort);
		var statistics = new SoakEconomyStatistics([new("b01", "Gather")]);
		Read(lines, cohort, statistics);
		Assert.Equal(10, Assert.Single(statistics.Snapshot().Prefixes).Observed);
		int index = lines.FindIndex(line => line.Contains("soak:gather-outcome"));
		var edited = JsonNode.Parse(lines[index])!;
		edited["fields"]!["expectedProbability"] = .99;
		lines[index] = edited.ToJsonString();
		Assert.Throws<InvalidDataException>(() => Read(lines, cohort, new([new("b01", "Gather")])));
	}

	private static SoakSubjectEvidence Read(IEnumerable<string> lines, SoakCohort cohort, SoakEconomyStatistics statistics, int seed = 73) =>
		SoakTraceEvidence.Read(lines, "test", "b01", cohort, seed, Start, Start.AddSeconds(100), Start.AddSeconds(101), statistics, new Dictionary<int, SoakCookingOrder>());

	private static string Row(string bot, string packet, object fields, DateTimeOffset at, string direction = "action") =>
		JsonSerializer.Serialize(new { ts = at, vt = (string?)null, run = "test", bot, account = bot, step = "s01", dir = direction, packet, fields });

	private static List<string> Trace(string bot, SoakCohort cohort)
	{
		var lines = new List<string> { Row(bot, "scenario:start", new { scenario = "SOAK" }, Start.AddSeconds(-1)),
			Row(bot, "SM_PLAYER_SPAWN", new { worldId = cohort.MapId }, Start, "<") };
		var policy = new SoakLifePolicy(73, cohort);
		for (int i = 0; i < 20; i++)
		{
			var decision = policy.Next(); string activity = decision.Action.Activity.ToString(); var at = Start.AddSeconds(i);
			lines.Add(Row(bot, "soak:decision", new { cohort = cohort.Number, sequence = decision.Sequence, activity, source = decision.Action.SourceScenario }, at));
			lines.Add(Row(bot, "soak-" + activity, new { }, at));
			if (activity == "Quest")
			{
				var quests = StarterSoakQuest.ForRace(cohort.FirstRace);
				foreach (var quest in quests) lines.Add(Row(bot, "soak:quest-complete", new { quest = quest.Id, xp = quest.Experience, gold = quest.Gold }, at));
				lines.Add(Row(bot, "soak:quest-journey-persisted", new { quests = quests.Select(quest => quest.Id).Prepend(1000) }, at));
				policy.CompleteQuestJourney();
			}
			if (activity == "Gather") lines.Add(Row(bot, "soak:gather-outcome", new { skillBefore = 1, skillDifference = 0, channel = 0,
				expectedProbability = SoakProgressProbability.Gather(0), probabilityModel = SoakProgressProbability.Version, success = true }, at));
		}
		foreach (string action in new[] { "verify-final-inventory", "quit", "verify-offline", "scenario:complete" })
			lines.Add(Row(bot, action, new { scenario = "SOAK" }, Start.AddSeconds(100.05)));
		return lines;
	}

	private static void WithRun(Action<string> test)
	{
		string path = Path.Combine(Path.GetTempPath(), "aion-soak-evidence-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(path, "bots"));
		try
		{
			var activities = new[] { "Relog", "CrashDisconnect" };
			Write("bots-run.json", new { run = "test", gitSha = new string('a', 40), configProfile = "docker-bots-soak", scenarios = new[] { "SOAK" }, bots = 10, seed = 73, soakSeconds = 100, soakActivities = activities });
			Write("soak-window.json", new SoakWorkloadWindow("test", 10, 100, Start).Finish(true, Start.AddSeconds(101), TimeSpan.FromSeconds(101)));
			var cohorts = SoakLifePolicy.CreatePopulation(10, Manifest).Select(cohort => cohort with { Actions = cohort.Actions.Where(action => activities.Contains(action.Activity.ToString())).ToArray() }).ToArray();
			Write("soak-runtime.json", new { Seed = 73, BotCount = 10, SoakSeconds = 100, Activities = activities, Acceptance = false,
				Cohorts = cohorts.Select(cohort => new { Cohort = cohort.Number, Actions = 20, Counts = new Dictionary<string, long> { ["Relog"] = 10, ["CrashDisconnect"] = 10 } }) });
			Write("soak-economy.json", new SoakEconomyStatistics([]).Snapshot());
			foreach (var cohort in cohorts)
				foreach (int id in new[] { cohort.FirstSubject, cohort.SecondSubject }) File.WriteAllLines(Path.Combine(path, $"bots/b{id:D2}.trace.jsonl"), Trace($"b{id:D2}", cohort));
			File.WriteAllLines(Path.Combine(path, "bots/gm.trace.jsonl"), [Row("gm", "SM_QUIT_RESPONSE", new { mode = 1 }, Start.AddSeconds(-1), "<")]);
			test(path);
			void Write(string name, object value) => File.WriteAllText(Path.Combine(path, name), JsonSerializer.Serialize(value));
		}
		finally { Directory.Delete(path, recursive: true); }
	}
}
