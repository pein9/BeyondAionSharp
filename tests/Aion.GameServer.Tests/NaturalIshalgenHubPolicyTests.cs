using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model.Templates.Quest;

namespace Aion.GameServer.Tests;

public sealed class NaturalIshalgenHubPolicyTests
{
	[Fact]
	public void EveryFrozenQuestBelongsToExactlyOneNamedHub()
	{
		int[] assigned = NaturalIshalgenHubPolicy.Hubs.SelectMany(hub => hub.QuestIds).ToArray();
		Assert.Equal(assigned.Length, assigned.Distinct().Count());
		Assert.Equal(NaturalIshalgenContract.LoadDefault().Quests.Where(quest => quest.Id != 2000)
			.Select(quest => quest.Id).Order(),
			assigned.Order());
	}

	[Fact]
	public void LocalLowestLevelBlueComesBeforeCampaignAndClaimedWork()
	{
		var contract = new NaturalIshalgenContract(220010000, 2008, 9,
			[new(2001, 3, []), new(2112, 3, []), new(2108, 2, []), new(2117, 3, [])]);
		int[] ordered = NaturalIshalgenHubPolicy.Order([2001, 2112, 2108, 2117],
			contract, id => id == 2001 ? QuestCategory.MISSION : QuestCategory.QUEST,
			NaturalIshalgenHubPolicy.ForQuest(2112), new HashSet<int> { 2112 });
		Assert.Equal([2108, 2112, 2001, 2117], ordered);
	}

	[Fact]
	public void TravelChoosesOnlyAvailableWorthwhileShortcut()
	{
		var aldelle = NaturalJourneyTravelPolicy.AldelleDeparture;
		var anturoon = NaturalJourneyTravelPolicy.AnturoonArrival;
		Assert.Equal(NaturalTravelChoice.FlightToAnturoon,
			NaturalJourneyTravelPolicy.Choose(aldelle, anturoon, null, false, true));
		Assert.Equal(NaturalTravelChoice.Walk,
			NaturalJourneyTravelPolicy.Choose(aldelle, anturoon, null, false, false));
		var eastern = new BotPosition(729, 1157, 303, 0);
		var bind = new BotBindPoint(220010000, new BotPosition(588, 2467, 279, 0), 0);
		Assert.Equal(NaturalTravelChoice.Return,
			NaturalJourneyTravelPolicy.Choose(eastern, bind.Position, bind, true, false));
		Assert.Equal(NaturalTravelChoice.Walk,
			NaturalJourneyTravelPolicy.Choose(eastern, bind.Position, bind, false, false));
	}

	[Fact]
	public void HazardousTreasureMapCanBeAcceptedEarlyButWorkedAfterCampaign()
	{
		Assert.False(NaturalIshalgenHubPolicy.CanWorkNow(2116, new HashSet<int>()));
		Assert.True(NaturalIshalgenHubPolicy.CanWorkNow(2116, new HashSet<int> { 2007 }));
		Assert.True(NaturalIshalgenHubPolicy.CanWorkNow(2117, new HashSet<int>()));
	}

	[Fact]
	public void SafeGroupsKeepEarlyCampaignAheadOfEasternObjectives()
	{
		var completed = new HashSet<int>();
		var order = new List<int>();
		while (NaturalIshalgenHubPolicy.CurrentSafeGroup(completed) is { Length: > 0 } group)
		{
			foreach (int id in group)
			{
				Assert.True(completed.Add(id), $"Q{id} appears in more than one safe group.");
				order.Add(id);
			}
		}
		Assert.Equal(NaturalIshalgenContract.LoadDefault().Quests.Where(quest => quest.Id != 2000)
			.Select(quest => quest.Id).Order(), order.Order());
		Assert.True(order.IndexOf(2007) < order.IndexOf(2116));
		Assert.True(order.IndexOf(2007) < order.IndexOf(2129));
	}
}
