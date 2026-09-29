using Aion.Bots.Scenarios;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

/// <summary>AM-03: choosing a talk spot at a quest NPC with aggressive monsters around.</summary>
public sealed class NaturalGuardedTalkPolicyTests
{
	private static readonly BotPosition Npc = new(100, 100, 0, 0);
	private static readonly BotPosition[] Ring = Enumerable.Range(0, 12)
		.Select(i => new BotPosition(100 + 4 * MathF.Cos(i * MathF.PI / 6), 100 + 4 * MathF.Sin(i * MathF.PI / 6), 0, 0)).ToArray();

	[Fact]
	public void WithNoHostileInViewAnySpotInTalkRangeWillDo()
	{
		NaturalGuardedTalkDecision decision = NaturalGuardedTalkPolicy.Decide(Npc, 6, [], Ring);
		Assert.Equal("talk", decision.Action);
		Assert.True(NaturalGuardedTalkPolicy.Distance(decision.Spot!.Value, Npc) <= 5.5f);
	}

	[Fact]
	public void TheBotTalksFromTheSideAwayFromAMonsterWhoseCircleReachesTheNpc()
	{
		// A pluma 12 m east with an 8 m circle (+3 m margin) rules out the eastern spots only.
		var pluma = new NaturalTalkHostile(1, 210656, new BotPosition(112, 100, 0, 0), 8);
		NaturalGuardedTalkDecision decision = NaturalGuardedTalkPolicy.Decide(Npc, 6, [pluma], Ring);
		Assert.Equal("talk", decision.Action);
		Assert.True(decision.Spot!.Value.X < 100, $"spot {decision.Spot}");
		Assert.True(NaturalGuardedTalkPolicy.Distance(decision.Spot.Value, pluma.Position) > pluma.AggroRadius + NaturalGuardedTalkPolicy.Margin);
	}

	[Fact]
	public void WhenEverySpotIsCoveredTheMonsterCoveringMostIsPulledFirst()
	{
		var onTop = new NaturalTalkHostile(7, 210656, new BotPosition(101, 100, 0, 0), 8);
		var aside = new NaturalTalkHostile(8, 210657, new BotPosition(115, 100, 0, 0), 8);
		NaturalGuardedTalkDecision decision = NaturalGuardedTalkPolicy.Decide(Npc, 6, [aside, onTop], Ring);
		Assert.Equal(("clear", 7), (decision.Action, decision.PullFirst));
		Assert.Null(decision.Spot);
		// Ground out of talk range is no talk spot.
		Assert.Equal("clear", NaturalGuardedTalkPolicy.Decide(Npc, 2, [], Ring).Action);
	}
}
