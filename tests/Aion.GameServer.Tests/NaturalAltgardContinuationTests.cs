using Aion.Bots.Scenarios;

namespace Aion.GameServer.Tests;

public sealed class NaturalAltgardContinuationTests
{
	[Fact]
	public void TheCompleteRouteIncludesTheCorrectedDeliveryAndOnlyApprovedLegs()
	{
		Assert.Equal(13, NaturalAltgardContinuation.Order.Count);
		Assert.Equal(["l1", "l2", "l3", "l4", "l5", "l6", "l7", "l8", "l9", "l10", "l11", "cg", "l12"], NaturalAltgardContinuation.Order);
		int[] completed = NaturalAltgardContinuation.Order.SelectMany(id => NaturalAltgardContract.LoadLeg(id).Endpoint.CompletedQuestIds).ToArray();
		Assert.Contains(2217, completed);
		Assert.DoesNotContain(24114, completed);
		Assert.Contains(2900, completed);
		Assert.Contains(2293, completed);
		Assert.Contains(28511, completed);
	}

	[Fact]
	public void IncomingStateRetainsTheCorrectionAndTheActualStaffWithoutRewritingHistoricalContracts()
	{
		NaturalAltgardContract historical = NaturalAltgardContract.LoadLeg("l12");
		var completed = historical.Start.CompletedQuestIds.Append(2217).ToHashSet();
		NaturalAltgardContract current = NaturalAltgardContinuation.BindIncoming(historical, completed,
			[new NaturalJourneyItem(900001, historical.Haramel!.StaffItemId, 1, 3)]);
		Assert.Contains(2217, current.Start.CompletedQuestIds);
		Assert.Equal(157, current.Start.CompletedQuestIds.Union(current.Endpoint.CompletedQuestIds).Count());
		Assert.Equal(900001, current.Haramel!.StaffObjectId);
		Assert.Equal(137763, historical.Haramel.StaffObjectId);
		Assert.DoesNotContain(2217, historical.Start.CompletedQuestIds);
		Assert.Throws<InvalidDataException>(() => NaturalAltgardContinuation.BindIncoming(historical, completed, []));
		completed.Remove(2900);
		Assert.Throws<InvalidDataException>(() => NaturalAltgardContinuation.BindIncoming(historical, completed,
			[new NaturalJourneyItem(900001, historical.Haramel.StaffItemId, 1, 3)]));
	}

	[Theory]
	[InlineData(24014, 20, 3, 0, 0, true, true)]
	[InlineData(24015, 20, 3, 0, 0, true, true)]
	[InlineData(24014, 19, 3, 0, 0, true, false)]
	[InlineData(24014, 20, 3, 0, 0, false, false)]
	[InlineData(24014, 20, 3, 1, 0, true, false)]
	[InlineData(24014, 20, 3, 0, 1, true, false)]
	[InlineData(24015, 20, 4, 0, 0, true, false)]
	[InlineData(24016, 20, 3, 0, 0, true, false)]
	public void AutomaticUnlockDoesNotPermitDeferredObjectivesOrCompletion(int id, int level, byte status,
		int flags, byte count, bool prerequisite, bool allowed)
	{
		Assert.Equal(allowed, NaturalAltgardContinuation.AllowsAutomaticCampaignUnlock(id, (6, 0),
			new(id, status, flags, count, null), level, prerequisite ? new HashSet<int> { 24010 } : []));
		Assert.False(NaturalAltgardContinuation.AllowsAutomaticCampaignUnlock(id, (3, 0),
			new(id, status, flags, count, null), level, new HashSet<int> { 24010 }));
	}
}
