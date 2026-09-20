using System.Collections.Concurrent;

namespace Aion.Bots.Scenarios;

/// <summary>
/// Independent Java ce54b7931 GatheringTask/CraftingTask completion model, not a server reward call.
/// Two independently advancing bars race to 1000. Resolve their hitting-step distributions first,
/// then mix the negative-binomial probability that the required successes precede the required failures.
/// The model assumes independent uniform random draws; it does not claim PRNG sequence equivalence.
/// </summary>
public static class SoakProgressProbability
{
	public const string Version = "java-ce54b7931-progress-v1";
	private const int DrawCount = 1 << 24;
	private const int Full = 1000;
	private static readonly ConcurrentDictionary<(bool Gather, int Lead, bool Limited, float Quality), double> Cache = new();

	public static double Gather(int skillLead) => Probability(true, skillLead, false, 1);
	/// <summary>One craft stage. Callers must rule out combo products, morphing and alternate rates.</summary>
	public static double Craft(int skillLead, bool limitedProduction = false, float qualityModifier = 1) =>
		Probability(false, skillLead, limitedProduction, qualityModifier);

	private static double Probability(bool gather, int lead, bool limited, float quality)
	{
		if (lead is < -499 or > 499 || quality is not (1f or .9f or .7f or .5f or .3f))
			throw new ArgumentOutOfRangeException(nameof(lead), "Unsupported progress model input.");
		if (lead < 0) return 0;
		if (lead >= 41) return 1;
		return Cache.GetOrAdd((gather, lead, limited, quality), key => Calculate(key.Gather, key.Lead, key.Limited, key.Quality));
	}

	private static double Calculate(bool gather, int lead, bool limited, float quality)
	{
		double stepSuccess = 1 - ChanceBelow(33 * Math.Max(1 - lead * .015f, .25f));
		double purple = gather ? ChanceBelow(1 + lead / 10f) : 0;
		double blue = ChanceBelow((gather ? 5 : 15) + lead / 3f) - purple;
		int levelBonus = lead > 10 ? (lead - 10) * 2 : 0;
		float successBase = ((lead + 1) / 2f + levelBonus) * 10;
		var success = IncrementDistribution(multi => gather
			? Round(70 + successBase * multi)
			: Round(70 + (int)(successBase * multi) * quality));
		var critical = IncrementDistribution(multi => gather
			? Round(70 + (100 + successBase) * multi)
			: Round(70 + (int)((100 + successBase) * multi) * quality));
		for (int i = 0; i <= Full; i++) success[i] = success[i] * (1 - blue - purple) + critical[i] * blue;
		success[Full] += purple;
		var failure = IncrementDistribution(multi => gather
			? Round(120 + (lead + 1) / 2f * 10 * multi)
			: Round((limited ? 70 : 120) + (int)((lead + 1) / 1.5f * 10 * multi) * quality));
		return Race(HittingSteps(success), HittingSteps(failure), stepSuccess);
	}

	// JDK 25 RandomGenerator.nextFloat uses 24 high bits. Bounded float generation
	// rounds each multiply/add and clamps a rounded upper endpoint to nextDown(bound).
	internal static float Unit(int index) => index * (1f / DrawCount);
	internal static float Multiplier(int index) => Math.Min(1 + Unit(index), MathF.BitDecrement(2));
	internal static double ChanceBelow(float threshold) => LowerBound(index => Unit(index) * 100 >= threshold) / (double)DrawCount;
	private static int Round(float value) => checked((int)Math.Floor((double)value + .5));

	internal static double[] IncrementDistribution(Func<float, int> monotonicIncrement)
	{
		var result = new double[Full + 1];
		int begin = 0;
		for (int increment = Math.Clamp(monotonicIncrement(Multiplier(0)), 1, Full); increment < Full; increment++)
		{
			int end = LowerBound(index => monotonicIncrement(Multiplier(index)) > increment);
			result[increment] = (end - begin) / (double)DrawCount;
			begin = end;
			if (begin == DrawCount) break;
		}
		result[Full] = (DrawCount - begin) / (double)DrawCount;
		return result;
	}

	private static int LowerBound(Func<int, bool> predicate)
	{
		int low = 0, high = DrawCount;
		while (low < high)
		{
			int middle = low + (high - low) / 2;
			if (predicate(middle)) high = middle; else low = middle + 1;
		}
		return low;
	}

	internal static double[] HittingSteps(double[] increments)
	{
		if (increments.Length != Full + 1 || increments[0] != 0 || increments.Any(p => !double.IsFinite(p) || p < 0) ||
			Math.Abs(increments.Sum() - 1) > 1e-12) throw new ArgumentException("Invalid positive increment distribution.");
		var support = increments.Select((probability, amount) => (probability, amount)).Where(value => value.probability > 0).ToArray();
		int maxSteps = (Full + support[0].amount - 1) / support[0].amount;
		var hits = new double[maxSteps + 1];
		var positions = new double[Full]; positions[0] = 1;
		for (int step = 1; step <= maxSteps; step++)
		{
			var next = new double[Full];
			for (int position = 0; position < Full; position++)
			{
				if (positions[position] == 0) continue;
				foreach (var (probability, amount) in support)
				{
					double mass = positions[position] * probability;
					if (position + amount >= Full) hits[step] += mass;
					else next[position + amount] += mass;
				}
			}
			positions = next;
		}
		if (Math.Abs(hits.Sum() - 1) > 1e-10) throw new InvalidDataException("Progress hitting distribution lost probability mass.");
		return hits;
	}

	internal static double Race(double[] successSteps, double[] failureSteps, double successChance)
	{
		if (!double.IsFinite(successChance) || successChance is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(successChance));
		var race = new double[successSteps.Length, failureSteps.Length];
		// With no more successes needed, win; with no more failures needed, lose.
		for (int failures = 1; failures < failureSteps.Length; failures++) race[0, failures] = 1;
		double probability = 0;
		for (int successes = 1; successes < successSteps.Length; successes++)
			for (int failures = 1; failures < failureSteps.Length; failures++)
			{
				race[successes, failures] = successChance * race[successes - 1, failures] + (1 - successChance) * race[successes, failures - 1];
				probability += successSteps[successes] * failureSteps[failures] * race[successes, failures];
			}
		return probability;
	}
}
