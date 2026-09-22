using Aion.Bots.Scenarios;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

public sealed class NaturalIshalgenGatheringPolicyTests
{
	private static readonly SoakGatheringSpot Near = new(220010000, 1, 400651, 10, 0, 0);
	private static readonly SoakGatheringSpot Far = new(220010000, 1, 400651, 20, 0, 0);
	private static readonly BotPosition Home = new(0, 0, 0, 0);

	[Fact]
	public void FailedAttemptsConsumeNodeUsesAndMoveToNextClosestNode()
	{
		var policy = new NaturalIshalgenGatheringPolicy([Near, Far]);
		NaturalGatherNode[] observed = [new(101, Near.Position), new(202, Far.Position)];
		Assert.Equal(101, policy.Decide(Home, observed, 0, TimeSpan.Zero).ObjectId);
		for (int use = 0; use < 3; use++)
			policy.RecordAttempt(Near, 101, 7, TimeSpan.FromSeconds(use + 1));
		NaturalGatherChoice next = policy.Decide(Home, observed, 0, TimeSpan.FromSeconds(4));
		Assert.Equal("gather", next.Action);
		Assert.Equal(202, next.ObjectId);
	}

	[Fact]
	public void ExhaustedNodesWaitForRealRespawnThenRequireObservedObject()
	{
		var policy = new NaturalIshalgenGatheringPolicy([Near]);
		for (int use = 0; use < 3; use++) policy.RecordAttempt(Near, 101, 7, TimeSpan.Zero);
		Assert.Equal("wait", policy.Decide(Home, [], 0, TimeSpan.FromSeconds(1)).Action);
		Assert.Equal("explore", policy.Decide(Home, [], 0, GatheringTarget.RespawnDelay).Action);
		Assert.Equal(303, policy.Decide(Home, [new(303, Near.Position)], 0,
			GatheringTarget.RespawnDelay).ObjectId);
		policy.RecordAttempt(Near, 101, 6, GatheringTarget.RespawnDelay);
		Assert.Equal("gather", policy.Decide(Home, [new(101, Near.Position)], 1,
			GatheringTarget.RespawnDelay + TimeSpan.FromSeconds(1)).Action);
	}

	[Fact]
	public void OccupiedNodeDoesNotConsumeUseAndCanBeRetried()
	{
		var policy = new NaturalIshalgenGatheringPolicy([Near, Far]);
		policy.RecordAttempt(Near, 101, 8, TimeSpan.Zero);
		Assert.Equal(202, policy.Decide(Home, [new(101, Near.Position), new(202, Far.Position)],
			0, TimeSpan.FromSeconds(1)).ObjectId);
		Assert.Equal(101, policy.Decide(Home, [new(101, Near.Position)], 0,
			TimeSpan.FromSeconds(15)).ObjectId);
	}

	[Fact]
	public void InterruptedGatherConsumesNodeUseAsJavaDoes()
	{
		var policy = new NaturalIshalgenGatheringPolicy([Near, Far]);
		policy.RecordAttempt(Near, 101, 5, TimeSpan.Zero);
		policy.RecordAttempt(Near, 101, 5, TimeSpan.FromSeconds(1));
		policy.RecordAttempt(Near, 101, 5, TimeSpan.FromSeconds(2));
		Assert.Equal(202, policy.Decide(Home, [new(101, Near.Position), new(202, Far.Position)],
			0, TimeSpan.FromSeconds(3)).ObjectId);
	}

	[Fact]
	public void InventoryCountEndsSearchWithoutAnExtraGather()
	{
		var policy = new NaturalIshalgenGatheringPolicy([Near]);
		Assert.Equal("complete", policy.Decide(Home, [new(101, Near.Position)], 3, TimeSpan.Zero).Action);
	}

	[Fact]
	public void SpawnHintsNeverProvideAnUnseenGatherObjectId()
	{
		var policy = new NaturalIshalgenGatheringPolicy([Near, Far]);
		NaturalGatherChoice choice = policy.Decide(Home, [], 0, TimeSpan.Zero);
		Assert.Equal("explore", choice.Action);
		Assert.Null(choice.ObjectId);
		Assert.Equal(Near, choice.Spot);
		policy.RecordUnobserved(Near, TimeSpan.Zero);
		Assert.Equal(Far, policy.Decide(Home, [], 0, TimeSpan.FromSeconds(1)).Spot);
	}
}
