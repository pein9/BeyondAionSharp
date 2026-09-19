using Aion.GameServer.Dao;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	[SkippableFact]
	public void EventBuffPersistenceAcceptsEmptyBatchesAndClearsOnlyTheNamedEvent()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		using var policy = NewEconomyPolicy("event-dao-empty-batch", includeHistory: false);
		const string target = "E2E buff DAO target", unrelated = "E2E buff DAO unrelated";
		try
		{
			Assert.True(EventDAO.StoreBuffData(target, []));
			Assert.Empty(EventDAO.LoadStoredBuffData(target));
			Assert.True(EventDAO.StoreBuffData(unrelated, [new(9, [10821], [1, 7])]));
			Assert.True(EventDAO.StoreBuffData(target,
				[new(0, [10821, 10822], [2, 4]), new(1, [10823], [6])]));
			var stored = EventDAO.LoadStoredBuffData(target).OrderBy(row => row.GetBuffIndex()).ToArray();
			Assert.Equal(2, stored.Length);
			Assert.Equal(0, stored[0].GetBuffIndex());
			Assert.True(stored[0].GetActivePoolSkillIds().SetEquals([10821, 10822]));
			Assert.True(stored[0].GetAllowedBuffDays().SetEquals([2, 4]));
			Assert.Equal(1, stored[1].GetBuffIndex());
			Assert.True(stored[1].GetActivePoolSkillIds().SetEquals([10823]));
			Assert.True(stored[1].GetAllowedBuffDays().SetEquals([6]));
			for (int attempt = 0; attempt < 2; attempt++)
			{
				Assert.True(EventDAO.StoreBuffData(target, []));
				Assert.Empty(EventDAO.LoadStoredBuffData(target));
				var retained = Assert.Single(EventDAO.LoadStoredBuffData(unrelated));
				Assert.Equal(9, retained.GetBuffIndex());
				Assert.True(retained.GetActivePoolSkillIds().SetEquals([10821]));
				Assert.True(retained.GetAllowedBuffDays().SetEquals([1, 7]));
			}
		}
		finally
		{
			Assert.True(EventDAO.StoreBuffData(target, []));
			Assert.True(EventDAO.StoreBuffData(unrelated, []));
		}
		policy.AssertClean();
	}
}
