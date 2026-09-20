using Aion.Bots.Scenarios;
using Xunit.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class SoakProgressProbabilityTests(ITestOutputHelper output)
{
	[Theory]
	[InlineData(0.1)]
	[InlineData(0.5)]
	[InlineData(0.9)]
	public void RacingFixedBarsMatchesClosedFormCoinProbabilities(double p)
	{
		Assert.Equal(3 * p * p - 2 * p * p * p, SoakProgressProbability.Race([0, 0, 1], [0, 0, 1], p), 12);
		Assert.Equal(1 - Math.Pow(1 - p, 3), SoakProgressProbability.Race([0, 1], [0, 0, 0, 1], p), 12);
		Assert.Equal(Math.Pow(p, 3), SoakProgressProbability.Race([0, 0, 0, 1], [0, 1], p), 12);
	}

	[Fact]
	public void HittingStepsConserveMassAndHandleCriticalCompletion()
	{
		var increments = new double[1001]; increments[500] = .75; increments[1000] = .25;
		Assert.Equal(new[] { 0d, .25, .75 }, SoakProgressProbability.HittingSteps(increments));
		increments[0] = 1;
		Assert.Throws<ArgumentException>(() => SoakProgressProbability.HittingSteps(increments));
	}

	[Fact]
	public void BinarySearchIncrementMassMatchesExhaustive24BitEnumeration()
	{
		static int Increment(float multi) => (int)MathF.Round(70 + 105 * multi, MidpointRounding.AwayFromZero);
		var actual = SoakProgressProbability.IncrementDistribution(Increment);
		var counts = new int[1001];
		for (int draw = 0; draw < 1 << 24; draw++) counts[Increment(SoakProgressProbability.Multiplier(draw))]++;
		Assert.Equal(counts.Select(count => count / (double)(1 << 24)), actual);
		Assert.True(SoakProgressProbability.Multiplier((1 << 24) - 1) < 2);
		// The current C# Rnd scaling omits JDK's nextDown correction (§7 #82).
		Assert.Equal(2, 1 + SoakProgressProbability.Unit((1 << 24) - 1));
		Assert.Equal(1, SoakProgressProbability.Multiplier(0));
	}

	[Theory]
	[InlineData(-1, 0)]
	[InlineData(41, 1)]
	[InlineData(499, 1)]
	public void UnqualifiedAndTrivialSkillOutcomesAreExact(int lead, double probability)
	{
		Assert.Equal(probability, SoakProgressProbability.Gather(lead));
		Assert.Equal(probability, SoakProgressProbability.Craft(lead));
	}

	[Fact]
	public void UnsupportedInputsAreRejectedAndNormalFiniteModelsStayWithinProbabilityBounds()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => SoakProgressProbability.Gather(500));
		Assert.Throws<ArgumentOutOfRangeException>(() => SoakProgressProbability.Craft(0, qualityModifier: .8f));
		for (int lead = 0; lead <= 40; lead++)
		{
			Assert.InRange(SoakProgressProbability.Gather(lead), .5, 1);
			Assert.InRange(SoakProgressProbability.Craft(lead), .5, 1);
		}
		Assert.True(SoakProgressProbability.Craft(0, true) > SoakProgressProbability.Craft(0)); // Smaller failure increments take longer to fill the failure bar.
	}

	[Theory]
	[InlineData(true, 0, false, 1f)]
	[InlineData(false, 0, false, 1f)]
	[InlineData(true, 10, false, 1f)]
	[InlineData(false, 10, false, 1f)]
	[InlineData(true, 11, false, 1f)]
	[InlineData(false, 11, false, 1f)]
	[InlineData(true, 40, false, 1f)]
	[InlineData(false, 40, false, 1f)]
	[InlineData(false, 0, true, .3f)]
	[InlineData(false, 9, false, .7f)]
	public void DerivedProbabilityAgreesWithSeparatelyInterleavedSeededSimulation(bool gather, int lead, bool limited, float quality)
	{
		double expected = gather ? SoakProgressProbability.Gather(lead) : SoakProgressProbability.Craft(lead, limited, quality);
		const int trials = 150000;
		var random = new Random(8731);
		int won = 0;
		for (int trial = 0; trial < trials; trial++)
		{
			int success = 0, failure = 0;
			while (success < 1000 && failure < 1000)
			{
				// Deliberately simulate the competing bars directly; do not reuse model CDFs or hitting-step/race helpers.
				float multi = MathF.Min(2f - MathF.Pow(2, -23), 1 + Roll());
				bool step = Roll() * 100 >= 33 * MathF.Max(1 - lead * .015f, .25f);
				if (step)
				{
					float crit = Roll() * 100;
					if (gather && crit < 1 + lead / 10f) { success = 1000; continue; }
					float bonus = (crit < (gather ? 5 : 15) + lead / 3f ? 100 : 0) +
						((lead + 1) / 2f + (lead > 10 ? (lead - 10) * 2 : 0)) * 10;
					success += Nearest(gather ? 70 + bonus * multi : 70 + (int)(bonus * multi) * quality);
				}
				else failure += Nearest(gather ? 120 + (lead + 1) / 2f * 10 * multi :
					(limited ? 70 : 120) + (int)((lead + 1) / 1.5f * 10 * multi) * quality);
			}
			if (success >= 1000) won++;
		}
		double observed = won / (double)trials;
		output.WriteLine($"gather={gather}, lead={lead}, limited={limited}, quality={quality}: derived={expected:R}, independent replay={observed:R}");
		Assert.InRange(Math.Abs(expected - observed), 0, .006);
		float Roll() => random.Next(1 << 24) / (float)(1 << 24);
		static int Nearest(float x) => (int)MathF.Round(x, MidpointRounding.AwayFromZero);
	}
}
