using Aion.GameServer.Configs.Schedule;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Services;
using Aion.GameServer.TestKit;
using Aion.GameServer.Utils.Xml;

namespace Aion.GameServer.Tests;

/// <summary>
/// The shipped data that SiegeService.initSieges (GameServer.java:141) relies on. Its updateFortressNextState looks up the
/// location of every scheduled SiegeStartRunnable and sets its next state with no null check, in Java as here, so every
/// id of config/schedule/siege_schedule.xml must be a location of siege/siege_locations.xml: a fortress for each fortress
/// entry and the agent location for the agent fight. Gap #65's NullReferenceException in that lookup came from a test
/// process whose SiegeService had empty maps, not from the data.
/// </summary>
public sealed class SiegeScheduleDataTests
{
	[Fact]
	public void EveryScheduledSiegeIsALocationOfTheShippedData()
	{
		string root = RealStaticData.RepoRoot();
		var locations = JaxbHolderLoader.LoadFromFile<SiegeLocationData>(
			Path.Combine(root, "game-server", "data", "static_data", "siege", "siege_locations.xml"));
		var schedule = JAXBUtil.Deserialize<SiegeSchedules>(
			new FileInfo(Path.Combine(root, "game-server", "config", "schedule", "siege_schedule.xml")));

		Assert.NotEmpty(schedule.GetFortresses());
		foreach (SiegeSchedules.Fortress fortress in schedule.GetFortresses())
		{
			Assert.True(locations.GetFortress().ContainsKey(fortress.GetId()), $"scheduled fortress {fortress.GetId()} is not a fortress location");
			Assert.Same(locations.GetFortress()[fortress.GetId()], locations.GetSiegeLocations()[fortress.GetId()]);
		}
		SiegeSchedules.AgentFight agentFight = Assert.Single(schedule.GetAgentFights());
		AgentLocation agent = locations.GetAgentLoc();
		Assert.NotNull(agent);
		Assert.Equal(agent.GetLocationId(), agentFight.GetId());
		Assert.Same(agent, locations.GetSiegeLocations()[agentFight.GetId()]);
	}

	/// <summary>
	/// InitSieges schedules one SiegeStartRunnable per siege time: 21 fortress times, each turned into its preparation time
	/// (none of which crosses midnight, which would throw), and the agent fight's one time as it stands.
	/// </summary>
	[Fact]
	public void TheShippedScheduleHasTwentyOneFortressTimesAndOneAgentFight()
	{
		var schedule = JAXBUtil.Deserialize<SiegeSchedules>(
			new FileInfo(Path.Combine(RealStaticData.RepoRoot(), "game-server", "config", "schedule", "siege_schedule.xml")));

		string[] preparations = schedule.GetFortresses()
			.SelectMany(fortress => fortress.GetSiegeTimes().Select(time => SiegeService.GetInstance().GetPreparationCronString(time, fortress.GetId())))
			.ToArray();

		Assert.Equal(21, preparations.Length);
		Assert.Equal(1, schedule.GetAgentFights().Sum(agentFight => agentFight.GetSiegeTimes().Count));
	}
}
