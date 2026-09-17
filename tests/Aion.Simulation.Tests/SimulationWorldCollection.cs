namespace Aion.Simulation.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SimulationWorldCollection : ICollectionFixture<SimulationWorldFixture>
{
	public const string Name = "SimulationWorld";
}
