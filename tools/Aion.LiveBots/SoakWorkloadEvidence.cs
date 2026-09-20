using System.Security.Cryptography;
using System.Text.Json;
using Aion.Bots.Scenarios;
using static Aion.LiveBots.SoakTraceEvidence;

namespace Aion.LiveBots;

public sealed record SoakWorkloadReport(string Policy, string Status, string? Run, string? GitSha,
	bool CapacityConfiguration, IReadOnlyList<string> Failures, IReadOnlyDictionary<string, string> SourceSha256,
	IReadOnlyList<SoakSubjectEvidence> Subjects, SoakEconomyReport? RecomputedEconomy, bool OverallSoakAccepted = false);

/// <summary>Checks retained workload evidence; telemetry, error watching and runner exit remain separate gates.</summary>
public static class SoakWorkloadEvidence
{
	public const string Policy = "p10-02-workload-v2";

	public static SoakWorkloadReport Analyze(string directory, ScenarioManifest manifest)
	{
		var sources = new Dictionary<string, string>();
		var subjects = new List<SoakSubjectEvidence>();
		string? run = null, git = null;
		bool capacity = false;
		SoakEconomyReport? replay = null;
		try
		{
			var metadata = ReadJson("bots-run.json");
			run = metadata.GetProperty("run").GetString(); git = metadata.GetProperty("gitSha").GetString();
			Require(!string.IsNullOrWhiteSpace(run) && git?.Length == 40 && git.All(Uri.IsHexDigit), "Missing run/source identity.");
			Require(metadata.GetProperty("configProfile").GetString() == "docker-bots-soak" &&
				metadata.GetProperty("scenarios").EnumerateArray().Select(value => value.GetString()).SequenceEqual(["SOAK"]), "Wrong workload profile/scenario.");
			int bots = metadata.GetProperty("bots").GetInt32(), seed = metadata.GetProperty("seed").GetInt32(), seconds = metadata.GetProperty("soakSeconds").GetInt32();
			var activities = metadata.GetProperty("soakActivities").EnumerateArray().Select(value => value.GetString()!).ToArray();
			Require(activities.Length > 0 && activities.Distinct().Count() == activities.Length && activities.All(value => Enum.GetNames<SoakActivity>().Contains(value)), "Invalid activity selection.");
			capacity = bots is 50 or 200 or 500 && seconds == 7200 && activities.ToHashSet().SetEquals(Enum.GetNames<SoakActivity>());
			var rawWindow = ReadJson("soak-window.json");
			Require(rawWindow.GetProperty("Status").GetString() == "completed", "Workload window is not terminal and completed.");
			var window = new SoakWorkloadWindow(run!, bots, seconds, rawWindow.GetProperty("StartedUtc").GetDateTimeOffset())
				.Finish(true, rawWindow.GetProperty("CompletedUtc").GetDateTimeOffset(), TimeSpan.FromSeconds(rawWindow.GetProperty("ElapsedSeconds").GetDouble()));
			Require(Equivalent(JsonSerializer.SerializeToElement(window), rawWindow), "Recorded window contradicts its reconstructed terminal state.");
			Require(window.SchemaVersion == 1 && window.Run == run && window.BotCount == bots && window.PlannedSeconds == seconds && seconds > 0 &&
				window.Status == "completed" && window.ClockConsistent && !window.OverallSoakAccepted && window.CompletedUtc >= window.EndedUtc &&
				window.ElapsedSeconds >= seconds && window.WallClockDriftSeconds is { } drift && double.IsFinite(drift) && Math.Abs(drift) <= 2 &&
				Math.Abs((window.CompletedUtc!.Value - window.StartedUtc).TotalSeconds - window.ElapsedSeconds!.Value - drift) < .00001,
				"Missing, incomplete or contradictory workload window.");
			var runtime = ReadJson("soak-runtime.json");
			Require(runtime.GetProperty("Seed").GetInt32() == seed && runtime.GetProperty("BotCount").GetInt32() == bots &&
				runtime.GetProperty("SoakSeconds").GetInt32() == seconds && !runtime.GetProperty("Acceptance").GetBoolean() &&
				runtime.GetProperty("Activities").EnumerateArray().Select(value => value.GetString()).SequenceEqual(activities), "Runtime summary contradicts metadata.");
			var cohorts = SoakLifePolicy.CreatePopulation(bots, manifest).Select(cohort => cohort with
			{ Actions = cohort.Actions.Where(action => activities.Contains(action.Activity.ToString())).ToArray() }).ToArray();
			var enrollment = cohorts.SelectMany(cohort => cohort.Actions.Where(action => action.Activity is SoakActivity.Craft or SoakActivity.Gather)
				.SelectMany(action => new[] { cohort.FirstSubject, cohort.SecondSubject }.Select(subject => new SoakEconomySubject($"b{subject:D2}", action.Activity.ToString()))));
			var economy = new SoakEconomyStatistics(enrollment);
			var recipes = activities.Contains("Craft") ? SoakCookingCatalog.All.ToDictionary(value => value.Order.RecipeId) : [];
			var expectedFiles = Enumerable.Range(1, bots).Select(id => $"b{id:D2}.trace.jsonl").Append("gm.trace.jsonl").ToHashSet();
			Require(expectedFiles.SetEquals(Directory.EnumerateFiles(Path.Combine(directory, "bots"), "*.trace.jsonl").Select(path => Path.GetFileName(path)!)), "Missing or unexpected subject traces.");
			var reported = runtime.GetProperty("Cohorts").EnumerateArray().ToArray();
			Require(reported.Length == cohorts.Length && reported.Select(value => value.GetProperty("Cohort").GetInt32()).Distinct().Count() == cohorts.Length,
				"Missing or duplicate runtime cohorts.");
			foreach (var cohort in cohorts)
			{
				var counter = reported.Single(value => value.GetProperty("Cohort").GetInt32() == cohort.Number);
				foreach (int id in new[] { cohort.FirstSubject, cohort.SecondSubject })
				{
					string bot = $"b{id:D2}", path = $"bots/{bot}.trace.jsonl";
					SoakSubjectEvidence subject;
					try { subject = Read(ReadLines(path), run!, bot, cohort, seed, window.StartedUtc, window.EndedUtc, window.CompletedUtc!.Value, economy, recipes); }
					catch (Exception exception) when (exception is InvalidDataException or JsonException or KeyNotFoundException or InvalidOperationException)
					{ throw new InvalidDataException($"{bot}: {exception.Message}", exception); }
					Require(subject.Decisions == counter.GetProperty("Actions").GetInt64() && Equivalent(JsonSerializer.SerializeToElement(subject.Counts), counter.GetProperty("Counts")),
						$"{bot}: runtime counters differ from actual decisions.");
					if (capacity) Require(subject.Counts.All(pair => pair.Value >= (pair.Key == "Quest" ? 1 : 2)), $"{bot}: selected activities did not repeat.");
					subjects.Add(subject);
				}
			}
			bool directorQuit = false;
			foreach (string line in ReadLines("bots/gm.trace.jsonl"))
			{
				using var document = JsonDocument.Parse(line); var row = document.RootElement;
				Require(row.GetProperty("run").GetString() == run && row.GetProperty("bot").GetString() == "gm" &&
					row.GetProperty("ts").GetDateTimeOffset() < window.StartedUtc.AddMilliseconds(1), "Director operated after the workload began.");
				if (row.GetProperty("dir").GetString() == "<" && row.GetProperty("packet").GetString() == "SM_QUIT_RESPONSE") directorQuit = true;
			}
			Require(directorQuit, "Director logout evidence missing.");
			replay = economy.Snapshot();
			Require(Equivalent(JsonSerializer.SerializeToElement(replay), ReadJson("soak-economy.json")), "Economic summary disagrees with source-derived trace replay.");
			Require(replay.Status != "failed", "Replayed economy observations reject the model.");
			return new(Policy, "passed", run, git, capacity, [], sources, subjects, replay);
		}
		catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or KeyNotFoundException or
			InvalidOperationException or ArgumentException or FormatException or OverflowException)
		{
			return new(Policy, "failed", run, git, capacity, [exception.Message], sources, subjects, replay);
		}

		JsonElement ReadJson(string relative)
		{
			using var document = JsonDocument.Parse(string.Join('\n', ReadLines(relative)));
			return document.RootElement.Clone();
		}
		IEnumerable<string> ReadLines(string relative)
		{
			// Deny concurrent writers. A running file is not a stable evidence snapshot.
			using var stream = new FileStream(Path.Combine(directory, relative), FileMode.Open, FileAccess.Read, FileShare.Read);
			using var hash = SHA256.Create();
			using var hashing = new CryptoStream(stream, hash, CryptoStreamMode.Read);
			using var reader = new StreamReader(hashing);
			while (reader.ReadLine() is { } line) yield return line;
			sources[relative] = Convert.ToHexString(hash.Hash!).ToLowerInvariant();
		}
	}

	internal static bool Equivalent(JsonElement expected, JsonElement actual)
	{
		if (expected.ValueKind != actual.ValueKind) return false;
		return expected.ValueKind switch
		{
			JsonValueKind.Object => expected.EnumerateObject().Count() == actual.EnumerateObject().Count() &&
				expected.EnumerateObject().All(property => actual.TryGetProperty(property.Name, out var value) && Equivalent(property.Value, value)),
			JsonValueKind.Array => expected.GetArrayLength() == actual.GetArrayLength() && expected.EnumerateArray().Zip(actual.EnumerateArray()).All(pair => Equivalent(pair.First, pair.Second)),
			JsonValueKind.Number => expected.TryGetInt64(out long x) && actual.TryGetInt64(out long y) ? x == y :
				double.IsFinite(actual.GetDouble()) && Math.Abs(expected.GetDouble() - actual.GetDouble()) < 1e-8,
			_ => expected.GetRawText() == actual.GetRawText(),
		};
	}

	public static int WriteReport(string directory)
	{
		var report = Analyze(directory, ScenarioManifest.Load(ScenarioManifest.FindDefaultPath()));
		string path = Path.Combine(directory, "soak-workload.json");
		File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
		Console.WriteLine($"Soak workload evidence {report.Status}; overall acceptance is not granted. {path}");
		foreach (string failure in report.Failures) Console.Error.WriteLine(failure);
		return report.Status == "passed" ? 0 : 1;
	}
}
