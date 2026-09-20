using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;
using System.Xml.Linq;

namespace Aion.GameServer.Tests;

public sealed class BotEffectRewardObservationTests
{
	// Java SM_ABNORMAL_STATE body for mixed50-2h-a b49's observed 10549/level1/BOOST/1h effect.
	private static readonly byte[] BoostBody = Convert.FromHexString("000000000000000000000000200100970C02003529010580EE3600");
	private static DecodedBotServerPacket Boost() => new BotServerPacketDecoder().Decode(typeof(SM_ABNORMAL_STATE), BoostBody);
	private static DecodedBotServerPacket Empty(byte slot = 32) => new(typeof(SM_ABNORMAL_STATE),
		new Dictionary<string, object?> { ["slot"] = slot, ["effectCount"] = (ushort)0,
			["effects"] = new List<IReadOnlyDictionary<string, object?>>() });
	private static DecodedBotServerPacket Rank(long ap, int kills, long earned, int position = 0) => new(typeof(SM_ABYSS_RANK),
		new Dictionary<string, object?>
		{
			["ap"] = ap, ["currentGp"] = 0, ["rank"] = 1, ["rankingListPosition"] = position, ["allKill"] = kills, ["maxRank"] = 1,
			["dailyKill"] = kills, ["dailyAp"] = earned, ["dailyGp"] = 0, ["weeklyKill"] = kills, ["weeklyAp"] = earned, ["weeklyGp"] = 0,
			["lastKill"] = 0, ["lastAp"] = 0L, ["lastGp"] = 0,
		});

	[Fact]
	public void ObservedLiveBoostExplainsExactly330ApAndSurvivesLaterExpiryAndRankingRefresh()
	{
		var world = new BotWorldModel();
		world.Apply(Empty()); world.Apply(Rank(710, 1, 800));
		var before = world.AbyssRank!;
		world.Apply(Boost());
		world.Apply(Rank(1040, 2, 1130));
		var reward = Assert.IsType<BotAbyssRewardObservation>(world.LastAbyssReward);
		Assert.Equal(before, reward.Before);
		var effect = Assert.Single(reward.Effects!);
		Assert.Equal(new BotVisibleEffect(134295, 10549, 1, 5, 3600000), effect);
		world.Apply(Empty()); // Later expiry cannot rewrite reward-time evidence.
		world.Apply(Rank(1040, 2, 1130, position: 123));
		Assert.Same(reward, world.LastAbyssReward);
		Assert.Empty(world.VisibleEffects!);
		Assert.Equal(110, PvpApBoostCatalog.Percent(reward.Effects));
		var expected = SoloPvpRewardContract.Predict(before, before, 10, 10, 10, 2, PvpApBoostCatalog.Percent(reward.Effects));
		Assert.Equal(1040, expected.Winner.Ap);
		SoloPvpRewardContract.AssertMatches(expected, world.AbyssRank!, expected.Victim);
		Assert.Throws<InvalidDataException>(() => SoloPvpRewardContract.AssertMatches(expected,
			world.AbyssRank! with { Ap = 1010 }, expected.Victim));
	}

	[Fact]
	public void SlotSpecificPacketsStillReplaceCompleteVisibleListAndEmptyMeansKnownEmpty()
	{
		var world = new BotWorldModel();
		Assert.Null(world.VisibleEffects);
		world.Apply(Boost());
		Assert.Single(world.VisibleEffects!);
		world.Apply(Empty(slot: 1)); // BUFF change, complete list empty: do not retain a stale BOOST.
		Assert.Empty(world.VisibleEffects!);
		Assert.Equal(100, PvpApBoostCatalog.Percent(world.VisibleEffects));
		world.Apply(Boost());
		world.BeginWorldReload();
		Assert.Null(world.VisibleEffects);
		Assert.Null(world.LastAbyssReward);
		Assert.Throws<InvalidDataException>(() => PvpApBoostCatalog.Percent(world.VisibleEffects));
		world.Apply(Empty(slot: 127));
		Assert.Equal(100, PvpApBoostCatalog.Percent(world.VisibleEffects));
	}

	[Fact]
	public void RewardWithoutPostEntryEffectsCannotClaimAnUnboostedReward()
	{
		var world = new BotWorldModel();
		world.Apply(Rank(500, 0, 0)); world.Apply(Rank(800, 1, 300));
		Assert.Null(world.LastAbyssReward!.Effects);
		Assert.Throws<InvalidDataException>(() => PvpApBoostCatalog.Percent(world.LastAbyssReward.Effects));
		world.Apply(Boost());
		world.Apply(new(typeof(SM_PLAYER_SPAWN), new Dictionary<string, object?>
		{ ["worldId"] = 400010000, ["x"] = 1f, ["y"] = 2f, ["z"] = 3f, ["heading"] = (byte)0 }));
		Assert.Null(world.VisibleEffects); Assert.Null(world.LastAbyssReward);
	}

	[Fact]
	public void DecoderRejectsTruncatedAndTrailingSnapshotBytes()
	{
		var decoder = new BotServerPacketDecoder();
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_ABNORMAL_STATE), BoostBody[..^1]));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_ABNORMAL_STATE), BoostBody.Concat(new byte[] { 0 }).ToArray()));
	}

	[Fact]
	public void CatalogUsesObservedShippedEffectsAndRejectsUnknownOrContradictoryIdentity()
	{
		var effect = new BotVisibleEffect(1, 10549, 1, 5, -1); // Java's permanent-display sentinel is still an active effect.
		Assert.Equal(110, PvpApBoostCatalog.Percent([effect]));
		Assert.Equal(130, PvpApBoostCatalog.Percent([effect, effect with { SkillId = 10548 }]));
		Assert.Equal(100, PvpApBoostCatalog.Percent([effect with { SkillId = 1282 }]));
		Assert.Throws<InvalidDataException>(() => PvpApBoostCatalog.Percent([effect, effect]));
		Assert.Throws<InvalidDataException>(() => PvpApBoostCatalog.Percent([effect with { SkillId = 65000 }]));
		Assert.Throws<InvalidDataException>(() => PvpApBoostCatalog.Percent([effect with { TargetSlot = 0 }]));
		Assert.Throws<InvalidDataException>(() => PvpApBoostCatalog.Percent([effect with { SkillLevel = 0 }]));
	}

	[Theory]
	[InlineData("func=\"PERCENT\" value=\"10\"")]
	[InlineData("func=\"ADD\" value=\"10\" delta=\"1\"")]
	[InlineData("func=\"ADD\" value=\"-10\"")]
	[InlineData("func=\"ADD\"")]
	public void UnsupportedFutureApModifiersFailClosed(string attributes)
	{
		var xml = XElement.Parse($"<skills><skill_template skill_id='1' activation='ACTIVE' tslot='BOOST'><effects><apboost><change stat='AP_BOOST' {attributes}/></apboost></effects></skill_template></skills>");
		Assert.Throws<InvalidDataException>(() => PvpApBoostCatalog.Percent([], xml));
	}

	[Theory]
	[InlineData(110, 1, 330)]
	[InlineData(200, 4, 600)]
	[InlineData(200, 5, 1)]
	public void BoostIsAppliedOnlyToFullRewards(int percent, int kills, int gain)
	{
		var world = new BotWorldModel(); world.Apply(Rank(500, 0, 0));
		var expected = SoloPvpRewardContract.Predict(world.AbyssRank!, world.AbyssRank!, 10, 10, 10, kills, percent);
		Assert.Equal(500 + gain, expected.Winner.Ap);
		Assert.Equal(gain, expected.Winner.Daily.Ap);
		Assert.Equal(410, expected.Victim.Ap);
	}

	[Fact]
	public void BoostTruncatesAfterRankPenaltyInsteadOfRoundingAgain()
	{
		var winner = new BotAbyssRank(1500, 0, 2, 0, 0, 2, new(0, 0, 0), new(0, 0, 0), new(0, 0, 0));
		var victim = winner with { Ap = 500, Rank = 1, MaxRank = 1 };
		var expected = SoloPvpRewardContract.Predict(winner, victim, 10, 10, 10, 1, 110);
		Assert.Equal(1813, expected.Winner.Ap); // 300 - round(300 * .05) = 285; truncate(285 * 1.1f) = 313.
		Assert.Throws<ArgumentOutOfRangeException>(() => SoloPvpRewardContract.Predict(winner, victim, 10, 10, 10, 1, -1));
	}
}
