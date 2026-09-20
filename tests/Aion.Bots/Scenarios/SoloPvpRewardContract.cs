using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

public sealed record SoloPvpRewardExpectation(BotAbyssRank Winner, BotAbyssRank Victim);

/// <summary>
/// Independent oracle for ordinary-rate, solo cross-race kills between GP-zero soldiers.
/// No production reward/rank function is used. Officers, teams, rate boosts and AP caps are outside this contract.
/// </summary>
public static class SoloPvpRewardContract
{
	public const int FullRewardKillLimit = 5;
	private static readonly int[] Gains = [300, 345, 396, 455, 523, 601, 721, 865, 1038];
	private static readonly int[] Losses = [90, 103, 118, 136, 156, 180, 216, 259, 311];
	private static readonly int[] Thresholds = [0, 1200, 4220, 10990, 23500, 42780, 69700, 105600, 150800];

	public static SoloPvpRewardExpectation Predict(BotAbyssRank winner, BotAbyssRank victim,
		int winnerLevelBeforeKill, int victimLevel, int winnerLevelAfterKill, int killsAgainstVictimInWindow)
	{
		Validate(winner); Validate(victim);
		if (winnerLevelBeforeKill is < 1 or > 65 || victimLevel is < 1 or > 65 ||
			winnerLevelAfterKill < winnerLevelBeforeKill || winnerLevelAfterKill > 65 || killsAgainstVictimInWindow < 1)
			throw new ArgumentOutOfRangeException(nameof(killsAgainstVictimInWindow), "Invalid level or updated opponent kill count.");
		int difference = winnerLevelBeforeKill - victimLevel;
		float gainFactor = difference switch { > 4 => .1f, < -3 => 1.3f, 3 => .85f, 4 => .65f, -2 => 1.1f, -3 => 1.2f, _ => 1f };
		int gain = Round(Gains[victim.Rank - 1] * gainFactor);
		if (winner.Rank <= 7 && winner.Rank > victim.Rank)
			gain -= Round(gain * ((winner.Rank - victim.Rank) * .05f));
		// Java increments before comparing strictly less than five: only kills 1..4 receive full rewards.
		if (killsAgainstVictimInWindow >= FullRewardKillLimit) gain = 1;
		// Victim loss is calculated after the winner's XP reward, which may have raised their level.
		int lossDifference = winnerLevelAfterKill - victimLevel;
		float lossFactor = lossDifference switch { >= 5 => .1f, 4 => .65f, 3 => .85f, _ => 1f };
		int loss = Round(Losses[victim.Rank - 1] * lossFactor);
		long winnerAp = checked(winner.Ap + gain), victimAp = Math.Max(0, victim.Ap - loss);
		int winnerRank = RankForAp(winnerAp), victimRank = RankForAp(victimAp);
		return new(winner with
		{
			Ap = winnerAp, Rank = winnerRank, MaxRank = Math.Max(winner.MaxRank, winnerRank), AllKills = checked(winner.AllKills + 1),
			Daily = winner.Daily with { Kills = checked(winner.Daily.Kills + 1), Ap = checked(winner.Daily.Ap + gain) },
			Weekly = winner.Weekly with { Kills = checked(winner.Weekly.Kills + 1), Ap = checked(winner.Weekly.Ap + gain) },
		}, victim with { Ap = victimAp, Rank = victimRank, MaxRank = Math.Max(victim.MaxRank, victimRank) });
	}

	public static void AssertMatches(SoloPvpRewardExpectation expected, BotAbyssRank winner, BotAbyssRank victim)
	{
		// The ranking cache refreshes independently; its leaderboard position is not a per-kill reward.
		if (winner != expected.Winner with { RankingListPosition = winner.RankingListPosition } ||
			victim != expected.Victim with { RankingListPosition = victim.RankingListPosition })
			throw new InvalidDataException($"Solo PvP AP/rank/counter mismatch. Expected {expected}; observed winner {winner}, victim {victim}.");
	}

	private static int RankForAp(long ap)
	{
		for (int index = Thresholds.Length - 1; index >= 0; index--) if (ap >= Thresholds[index]) return index + 1;
		throw new ArgumentOutOfRangeException(nameof(ap));
	}
	private static int Round(float value) => checked((int)MathF.Floor(value + .5f));
	private static void Validate(BotAbyssRank rank)
	{
		ArgumentNullException.ThrowIfNull(rank);
		if (rank.CurrentGp != 0 || rank.Rank is < 1 or > 9 || rank.Ap is < 0 or > 1_000_000 || RankForAp(rank.Ap) != rank.Rank)
			throw new ArgumentException("Solo PvP contract requires a GP-zero soldier with a consistent observed AP rank.", nameof(rank));
	}
}
