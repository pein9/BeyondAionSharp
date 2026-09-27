using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class NaturalCombatRetreatPolicyTests
{
	[Fact]
	public void RetreatWaitsForEveryPursuerAndIncludesNewAttackers()
	{
		var active = new HashSet<int> { 101, 102, 103 };
		NaturalCombatRetreatPolicy.ObserveEngagement(active,
			[Emotion(101, EmotionType.NEUTRALMODE_IN_MOVE), Attack(104)], 7);
		Assert.Equal([102, 103, 104], active.Order());
		NaturalCombatRetreatPolicy.ObserveEngagement(active,
			[Emotion(102, EmotionType.NEUTRALMODE_IN_MOVE), Delete(103)], 7);
		Assert.Equal([104], active.Order());
		NaturalCombatRetreatPolicy.ObserveEngagement(active,
			[Emotion(104, EmotionType.NEUTRALMODE_IN_MOVE), Attack(104)], 7);
		Assert.Equal([104], active.Order()); // A new swing cancels an earlier return signal.
		NaturalCombatRetreatPolicy.ObserveEngagement(active, [Emotion(104, EmotionType.NEUTRALMODE_IN_MOVE)], 7);
		Assert.Empty(active);
	}

	[Fact]
	public void DepartureAllowsSidewaysTerrainDetourButRejectsRunningIntoPack()
	{
		BotPosition current = At(0, 0);
		BotPosition[] attackers = [At(-5, 0), At(-5, 2), At(-5, -2)];
		Assert.True(NaturalCombatRetreatPolicy.ClearsPackOnDeparture(current,
			[At(-1, 1), At(0, 3), At(5, 3)], attackers));
		Assert.False(NaturalCombatRetreatPolicy.ClearsPackOnDeparture(current,
			[At(-4, 0), At(-6, 0), At(5, 0)], attackers));
	}

	private static DecodedBotServerPacket Emotion(int npc, EmotionType type) =>
		new(typeof(SM_EMOTION), new Dictionary<string, object?>
		{
			["senderObjectId"] = npc, ["emotionType"] = (byte)type,
		});

	private static DecodedBotServerPacket Attack(int npc) =>
		new(typeof(SM_ATTACK), new Dictionary<string, object?>
		{
			["attackerObjId"] = npc, ["targetObjId"] = 7,
		});

	private static DecodedBotServerPacket Delete(int npc) =>
		new(typeof(SM_DELETE), new Dictionary<string, object?> { ["objectId"] = npc });

	[Fact]
	public void SamePositionRefugeFallsBackToWalkedGroundOutsideObservedPack()
	{
		BotPosition current = At(762, 1502);
		BotPosition attacker = At(756, 1498);
		NaturalNavigationEvent[] events =
		[
			Progress(1, At(700, 1436)),
			Progress(2, At(711, 1473)),
			Progress(3, At(719, 1481)),
			Progress(4, At(740, 1492)),
			Progress(5, current),
		];
		BotPosition[] checkpoints = NaturalCombatRetreatPolicy.SelectCheckpoints(
			current, current, events, [attacker]);
		Assert.NotEmpty(checkpoints);
		Assert.Equal(At(719, 1481), checkpoints[0]);
		Assert.DoesNotContain(current, checkpoints);
		Assert.DoesNotContain(At(740, 1492), checkpoints);
	}

	[Fact]
	public void DoesNotInventARefugeWithoutWalkedOrObservedClearGround()
	{
		BotPosition current = At(0, 0);
		BotPosition[] checkpoints = NaturalCombatRetreatPolicy.SelectCheckpoints(
			current, current, [Progress(1, At(5, 0))], [At(2, 0)]);
		Assert.Empty(checkpoints);
	}

	[Fact]
	public void TombstoneGuardianCanRetreatTowardRecordedIngressWithoutNamedRefuge()
	{
		BotPosition current = At(598.83f, 1880.17f);
		NaturalNavigationEvent[] walked =
		[
			Progress(1, At(633.1f, 1854.8f)),
			Progress(2, At(621.9f, 1864.2f)),
			Progress(3, At(612.9f, 1876.1f)),
			Progress(4, current),
		];
		BotPosition[] checkpoints = NaturalCombatRetreatPolicy.SelectCheckpoints(
			current, current, walked, [At(600.1f, 1880.8f)]);
		Assert.Contains(At(633.1f, 1854.8f), checkpoints);
	}

	private static NaturalNavigationEvent Progress(int sequence, BotPosition position) =>
		new(sequence, "segment-progress", "completed", "", 220010000, -1,
			position, position, null, 0, 1, null);

	private static BotPosition At(float x, float y) => new(x, y, 285, 0);
}
