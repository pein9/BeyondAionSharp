using Aion.GameServer.Configs.Network;

namespace Aion.Simulation.Tests;

[Collection(SimulationWorldCollection.Name)]
public sealed class SimulationWorldSmokeTests(SimulationWorldFixture fixture)
{
	[SkippableFact]
	public void FixtureBootsOneStrictVirtualWorldWithoutNetworkHostedServices()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

		Assert.True(fixture.Bootstrap.IsStarted);
		Assert.True(fixture.World.IsInitialized);
		Assert.True(fixture.World.ObjectCount > 0);
		Assert.True(fixture.DataManager.StaticData.ImportedFileCount > 0);
		Assert.True(fixture.Clock.Strict);
		Assert.Empty(fixture.Clock.Faults);
		Assert.True(NetworkConfig.LOG_UNKNOWN_PACKETS);
		Assert.True(NetworkConfig.LOG_IGNORED_PACKETS);
	}
}
