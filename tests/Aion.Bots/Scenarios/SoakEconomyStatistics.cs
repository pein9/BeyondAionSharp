namespace Aion.Bots.Scenarios;

public sealed record SoakEconomySubject(string Bot, string Kind);
public sealed record SoakEconomyPrefix(string Bot, string Kind, long Observed, int Samples, int Successes, double ExpectedSuccesses);
public sealed record SoakEconomyTest(string Kind, string Scope, string Status, int RequiredSubjects, long Samples, double ExpectedFailures, double LogEvidence, double LogThreshold);
public sealed record SoakEconomyReport(string Policy, string ProbabilityModel, int PrefixLength, double FamilyAlpha,
	double ProbabilityMargin, string Status, int ImpossibleOutcomes, IReadOnlyList<SoakEconomyPrefix> Prefixes,
	IReadOnlyList<SoakEconomyTest> Tests, bool OverallSoakAccepted = false);

/// <summary>
/// A predeclared fixed prefix per eligible subject avoids selecting a convenient sample size after seeing results.
/// A mixture of likelihood ratios tests both directions while allowing a different, pre-action probability
/// for every attempt (skill progression). Whole-stream, per-subject test martingales also retain every
/// later outcome; their mean is valid without multiplying adaptively stopped subject streams together.
/// </summary>
public sealed class SoakEconomyStatistics
{
	public const int PrefixLength = 20;
	public const double FamilyAlpha = .001;
	// Conservative completion-probability envelope for floating-point endpoint differences:
	// at most 29 bar updates, each with at most one exceptional 24-bit multiplier draw.
	public const double ProbabilityMargin = 2e-6;
	private static readonly double[] Odds = [.25, .5, .75, 1.25, 1.5, 2, 4];
	private readonly Dictionary<SoakEconomySubject, Prefix> prefixes;
	private readonly Dictionary<string, double[]> evidence;
	private readonly object gate = new();
	private int impossible;
	private sealed class Prefix
	{
		public long Observed;
		public int Samples;
		public int Successes;
		public double Expected;
		public double AllExpected;
		public readonly double[] AllEvidence = new double[Odds.Length];
	}

	public SoakEconomyStatistics(IEnumerable<SoakEconomySubject> required)
	{
		prefixes = required.ToDictionary(subject => subject, _ => new Prefix());
		if (prefixes.Keys.Any(subject => string.IsNullOrWhiteSpace(subject.Bot) || subject.Kind is not ("Gather" or "Craft")))
			throw new ArgumentException("Invalid economy enrollment.");
		evidence = prefixes.Keys.Select(subject => subject.Kind).Distinct().ToDictionary(kind => kind, _ => new double[Odds.Length]);
	}

	public void Observe(string bot, string kind, double probability, bool success)
	{
		if (!double.IsFinite(probability) || probability is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(probability));
		lock (gate)
		{
			if (!prefixes.TryGetValue(new(bot, kind), out var prefix)) throw new InvalidDataException("Unenrolled economy observation.");
			prefix.Observed++;
			prefix.AllExpected += probability;
			if ((probability == 0 && success) || (probability == 1 && !success)) impossible++;
			bool included = prefix.Samples < PrefixLength;
			if (included) { prefix.Samples++; prefix.Successes += success ? 1 : 0; prefix.Expected += probability; }
			if (probability is 0 or 1) return;
			for (int i = 0; i < Odds.Length; i++)
			{
				// Use the least-favourable endpoint of the small null probability interval.
				double p = Odds[i] < 1 ? Math.Max(0, probability - ProbabilityMargin) : Math.Min(1, probability + ProbabilityMargin);
				if (p is 0 or 1) continue;
				double q = Odds[i] * p / (1 - p + Odds[i] * p);
				double change = success ? Math.Log(q / p) : Math.Log((1 - q) / (1 - p));
				prefix.AllEvidence[i] += change;
				if (included) evidence[kind][i] += change;
			}
		}
	}

	public SoakEconomyReport Snapshot()
	{
		lock (gate)
		{
			var rows = prefixes.OrderBy(pair => pair.Key.Kind).ThenBy(pair => pair.Key.Bot, StringComparer.Ordinal)
				.Select(pair => new SoakEconomyPrefix(pair.Key.Bot, pair.Key.Kind, pair.Value.Observed, pair.Value.Samples, pair.Value.Successes, pair.Value.Expected)).ToArray();
			var tests = new List<SoakEconomyTest>();
			foreach (string kind in evidence.Keys.Order())
			{
				var group = rows.Where(row => row.Kind == kind).ToArray();
				double logEvidence = LogMean(evidence[kind]);
				double threshold = Math.Log(4 / FamilyAlpha); // Two kinds, each with a fixed-prefix and a whole-stream test.
				double failures = group.Sum(row => row.Samples - row.ExpectedSuccesses);
				string status = group.Any(row => row.Samples != PrefixLength) || failures < 10 ? "insufficient" :
					logEvidence >= threshold ? "failed" : "passed";
				tests.Add(new(kind, "fixed-prefix", status, group.Length, group.Sum(row => row.Samples), failures, logEvidence, threshold));
				var streams = prefixes.Where(pair => pair.Key.Kind == kind).Select(pair => pair.Value).ToArray();
				double wholeEvidence = LogMean(streams.SelectMany(prefix => prefix.AllEvidence).ToArray());
				double allFailures = streams.Sum(prefix => prefix.Observed - prefix.AllExpected);
				string wholeStatus = wholeEvidence >= threshold ? "failed" :
					group.Any(row => row.Samples != PrefixLength) || allFailures < 10 ? "insufficient" : "passed";
				tests.Add(new(kind, "whole-stream", wholeStatus, group.Length, streams.Sum(prefix => prefix.Observed), allFailures, wholeEvidence, threshold));
			}
			string overall = impossible != 0 || tests.Any(test => test.Status == "failed") ? "failed" :
				tests.Count != 4 || tests.Any(test => test.Status == "insufficient") ? "insufficient" : "passed";
			return new("p10-02-economy-v1", SoakProgressProbability.Version, PrefixLength, FamilyAlpha, ProbabilityMargin,
				overall, impossible, rows, tests);
		}
	}

	private static double LogMean(double[] values)
	{
		double maximum = values.Max();
		return maximum + Math.Log(values.Sum(value => Math.Exp(value - maximum)) / values.Length);
	}
}
