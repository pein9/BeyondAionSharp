using Aion.Bots.Scenarios;

namespace Aion.GameServer.Tests;

/// <summary>AB-03: the timed-quest policy against the Leg 4 contract's Q2288 and Q2230 timers.</summary>
public sealed class NaturalTimedQuestPolicyTests
{
	private static readonly NaturalAltgardContract Leg4 = NaturalAltgardContract.LoadLeg("l4");
	private static readonly NaturalAltgardTimer Money = Leg4.TimerList.Single(timer => timer.QuestId == 2288);
	private static readonly NaturalAltgardTimer Wager = Leg4.TimerList.Single(timer => timer.QuestId == 2230);
	private const long Now = 10_000_000;

	[Fact]
	public void OneAttemptFitsEachTimerWithRoom()
	{
		// Q2288: 3 kills at about 40 s each with rests, and the ~60 s walk back, in 600 s.
		Assert.True(NaturalTimedQuestPolicy.FullBudgetSlack(Money, 3, 1, 40, 60) > 300);
		// Q2230: 10 tusks at 80% (12.5 kills) at 40 s, and the walk back, in 1,800 s.
		Assert.True(NaturalTimedQuestPolicy.FullBudgetSlack(Wager, 10, 0.8, 40, 60) > 1000);
		Assert.True(NaturalTimedQuestPolicy.TimerStartsAtAccept(Wager));
		Assert.False(NaturalTimedQuestPolicy.TimerStartsAtAccept(Money));
	}

	[Fact]
	public void Q2288StartsItsTimerOnlyWhenReadyHuntsAndTurnsInAtVar4()
	{
		Assert.Equal("take", Decide(Money, Observation(null, 0, null, 3, attempts: 0, ready: false)).Action);
		Assert.Equal("wait-until-ready", Decide(Money, Observation("START", 0, null, 3, attempts: 0, ready: false)).Action);
		Assert.Equal("start-timer", Decide(Money, Observation("START", 0, null, 3, attempts: 0)).Action);
		NaturalTimedChoice hunt = Decide(Money, Observation("START", 2, Now + 400_000, 2, attempts: 1));
		Assert.Equal("hunt", hunt.Action);
		Assert.True(hunt.SlackSeconds > 0);
		// Short on time: hunt on, since stopping gains nothing.
		NaturalTimedChoice short_ = Decide(Money, Observation("START", 2, Now + 60_000, 2, attempts: 1));
		Assert.Equal("hunt", short_.Action);
		Assert.True(short_.SlackSeconds < 0);
		Assert.Equal("turn-in", Decide(Money, Observation("START", 4, Now + 30_000, 0, attempts: 1)).Action);
		Assert.Equal("done", Decide(Money, Observation("REWARD", 4, Now + 30_000, 0, attempts: 1)).Action);
	}

	[Fact]
	public void Q2288ExpiryAbandonsRetakesAndGivesUpAfterThreeTimers()
	{
		// The timer ran out: the server abandons the quest; the bot waits for the journal, then retakes it.
		Assert.Equal("wait-for-journal", Decide(Money, Observation("START", 2, Now - 1, 2, attempts: 1)).Action);
		Assert.Equal("take", Decide(Money, Observation(null, 0, Now - 1, 3, attempts: 1)).Action);
		Assert.Equal("take", Decide(Money, Observation(null, 0, Now - 1, 3, attempts: 2)).Action);
		Assert.Equal("give-up", Decide(Money, Observation(null, 0, Now - 1, 3, attempts: NaturalTimedQuestPolicy.MaxAttempts)).Action);
	}

	[Fact]
	public void Q2230AcceptsOnlyWhenReadyAndSpendsANewChanceOnExpiry()
	{
		Assert.Equal("wait-until-ready", Decide(Wager, Observation(null, 0, null, 10, attempts: 0, ready: false)).Action);
		Assert.Equal("take", Decide(Wager, Observation(null, 0, null, 10, attempts: 0)).Action);
		Assert.Equal("hunt", Decide(Wager, Observation("START", 0, Now + 1_200_000, 6, attempts: 1, unitsPerKill: 0.8)).Action);
		Assert.Equal("turn-in", Decide(Wager, Observation("START", 0, Now + 200_000, 0, attempts: 1)).Action);
		NaturalTimedChoice expired = Decide(Wager, Observation("START", 0, Now - 1, 3, attempts: 1));
		Assert.Equal("new-chance", expired.Action);
		Assert.Contains("3057", expired.Reason, StringComparison.Ordinal);
		Assert.Contains(Wager.NewChanceAction!, expired.Reason, StringComparison.Ordinal);
		Assert.Equal("give-up", Decide(Wager, Observation("START", 0, Now - 1, 3, attempts: NaturalTimedQuestPolicy.MaxAttempts)).Action);
	}

	[Fact]
	public void NothingThatLosesTheQuestIsAllowedWhileTheTimerRuns()
	{
		NaturalTimedObservation running = Observation("START", 2, Now + 300_000, 1, attempts: 1);
		NaturalTimedObservation idle = Observation("START", 0, null, 3, attempts: 0);
		foreach (string action in NaturalTimedQuestPolicy.ForbiddenWhileTimed)
		{
			Assert.False(NaturalTimedQuestPolicy.Allowed(action, running), action);
			Assert.True(NaturalTimedQuestPolicy.Allowed(action, idle), action);
		}
		Assert.Contains("logout", NaturalTimedQuestPolicy.ForbiddenWhileTimed);
		// A rest fits only inside the slack.
		double slack = NaturalTimedQuestPolicy.Slack(running);
		Assert.True(NaturalTimedQuestPolicy.MayRest(running, slack - 1));
		Assert.False(NaturalTimedQuestPolicy.MayRest(running, slack + 1));
		Assert.True(NaturalTimedQuestPolicy.MayRest(idle, 10_000));
	}

	private static NaturalTimedChoice Decide(NaturalAltgardTimer timer, NaturalTimedObservation state) => NaturalTimedQuestPolicy.Decide(state, timer);

	private static NaturalTimedObservation Observation(string? status, int var, long? endsAt, int unitsLeft, int attempts, bool ready = true,
		double unitsPerKill = 1) =>
		new(status, var, Now, endsAt, unitsLeft, unitsPerKill, 40, 60, attempts, ready);
}
