using Aion.Commons.Nio;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed class PartialReadWarningResetTests
{
	[Fact]
	public async Task EveryScenarioSeesTheFirstPartialReadWarningForAnOpcode()
	{
		await using var clock = new VirtualThreadPool(strict: true);
		AssertScenarioFailsOnPartialRead(clock, "S0");
		AssertScenarioFailsOnPartialRead(clock, "S1");
	}

	private static void AssertScenarioFailsOnPartialRead(
		VirtualThreadPool clock,
		string scenario)
	{
		using var policy = new SimulationLogPolicy(
			"r1",
			scenario,
			clock,
			Path.Combine(RealStaticData.RepoRoot(), "parity-artifacts", "e2e", "log-allowlist.json"),
			new SimulationLogPolicyOptions { FailOnWarnings = true });
		var packet = new UnreadPacket();
		packet.SetBuffer(ByteBuffer.Wrap([0x11, 0x22]));

		Assert.True(packet.Read());
		SimulationLogPolicyException error = Assert.Throws<SimulationLogPolicyException>(policy.AssertClean);
		SimulationProblem problem = Assert.Single(error.Problems);
		Assert.Equal("log", problem.Kind);
		Assert.Contains("was not fully read", problem.Message, StringComparison.Ordinal);
	}

	private sealed class UnreadPacket()
		: AionClientPacket(0x6EE, new HashSet<AionConnection.State> { AionConnection.State.CONNECTED })
	{
		protected override void ReadImpl()
		{
		}

		protected override void RunImpl()
		{
		}
	}
}
