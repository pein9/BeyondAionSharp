namespace Aion.Bots.Scenarios;

public enum SoakActivity
{
	Quest,
	Gather,
	Craft,
	Vendor,
	Trade,
	Group,
	Duel,
	Pvp,
	Relog,
	CrashDisconnect,
}

public sealed record SoakAction(SoakActivity Activity, string SourceScenario);
public sealed record SoakDecision(long Sequence, SoakAction Action, TimeSpan ThinkTime);
public sealed record SoakCohort(int Number, int FirstSubject, int SecondSubject, int MapId,
	ScenarioRace FirstRace, ScenarioRace SecondRace, IReadOnlyList<SoakAction> Actions);

/// <summary>
/// A reproducible mixed-workload schedule, not an autonomous progression policy.
/// Each pair owns a stream, so task interleaving cannot change another pair's choices.
/// Activities reuse the named breadth protocol contracts, not their one-shot setup.
/// </summary>
public sealed class SoakLifePolicy
{
	private readonly Random random;
	private readonly SoakAction[] actions;
	private int[] bag = [];
	private int next;
	private long sequence;

	public SoakLifePolicy(int seed, SoakCohort cohort)
	{
		ArgumentNullException.ThrowIfNull(cohort);
		if (cohort.Number < 1 || cohort.Actions.Count == 0 ||
			cohort.Actions.Select(action => action.Activity).Distinct().Count() != cohort.Actions.Count)
			throw new ArgumentException("A cohort needs a positive id and distinct, nonempty activities.", nameof(cohort));
		actions = cohort.Actions.ToArray();
		random = new Random(unchecked(seed * 397 ^ cohort.Number * 7919));
	}

	public SoakDecision Next()
	{
		if (next == bag.Length)
		{
			bag = Enumerable.Range(0, actions.Length).ToArray();
			for (int i = bag.Length - 1; i > 0; i--)
			{
				int other = random.Next(i + 1);
				(bag[i], bag[other]) = (bag[other], bag[i]);
			}
			next = 0;
		}
		return new SoakDecision(++sequence, actions[bag[next++]], TimeSpan.FromMilliseconds(random.Next(1000, 5001)));
	}

	public static IReadOnlyList<SoakCohort> CreatePopulation(int subjects, ScenarioManifest manifest)
	{
		ArgumentNullException.ThrowIfNull(manifest);
		if (subjects < 10 || subjects > Transport.BotIdentity.MaximumSubjects || subjects % 2 != 0)
			throw new ArgumentOutOfRangeException(nameof(subjects), "Use an even population from 10 to 1000; 50/200/500 are acceptance sizes.");
		var result = new List<SoakCohort>();
		for (int pair = 0; pair < subjects / 2; pair++)
		{
			(int map, ScenarioRace first, ScenarioRace second, SoakAction[] zoneActions) = (pair % 5) switch
			{
				0 => (210010000, ScenarioRace.Elyos, ScenarioRace.Elyos, new[]
				{
					new SoakAction(SoakActivity.Quest, "Q1"), new(SoakActivity.Gather, "E1"), new(SoakActivity.Vendor, "E3"),
				}),
				1 => (220010000, ScenarioRace.Asmodians, ScenarioRace.Asmodians, new[]
				{
					new SoakAction(SoakActivity.Quest, "Q2"), new(SoakActivity.Gather, "E1"),
				}),
				2 => (110010000, ScenarioRace.Elyos, ScenarioRace.Elyos, new[] { new SoakAction(SoakActivity.Craft, "E5") }),
				3 => (120010000, ScenarioRace.Asmodians, ScenarioRace.Asmodians, new[] { new SoakAction(SoakActivity.Craft, "E5") }),
				_ => (400010000, ScenarioRace.Elyos, ScenarioRace.Asmodians, new[] { new SoakAction(SoakActivity.Pvp, "S2") }),
			};
			var actions = new List<SoakAction>(zoneActions);
			if (first == second)
				actions.AddRange([new(SoakActivity.Trade, "E6"), new(SoakActivity.Group, "S1"), new(SoakActivity.Duel, "S1")]);
			actions.AddRange([new(SoakActivity.Relog, "L0"), new(SoakActivity.CrashDisconnect, "L0")]);
			foreach (SoakAction action in actions)
			{
				ScenarioDefinition source = manifest.Get(action.SourceScenario);
				if (!source.Modes.Contains(ScenarioMode.Live) || source.ExpectedFail != null || source.Requires.Contains(ScenarioRequirement.D7))
					throw new InvalidDataException($"Soak source {source.Id} is not an enabled LIVE protocol contract.");
			}
			result.Add(new SoakCohort(pair + 1, pair * 2 + 1, pair * 2 + 2, map, first, second, actions.AsReadOnly()));
		}
		return result;
	}
}
