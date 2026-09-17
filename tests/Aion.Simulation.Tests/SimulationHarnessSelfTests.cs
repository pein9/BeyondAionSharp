using System.Runtime.CompilerServices;
using Aion.Commons.Nio;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed class SimulationHarnessSelfTests
{
	[Fact]
	public async Task ThrowingSpawnProbeAndTruncatedMoveFailWithFullStackText()
	{
		await using var clock = new VirtualThreadPool(strict: false);
		using var policy = NewPolicy(clock, "self-test-failure");
		var probe = new ThrowingSpawnProbe(UninitializedNpc());
		clock.Schedule(_ =>
		{
			probe.OnGeneralEvent(AiEventType.Spawned);
			return ValueTask.CompletedTask;
		}, TimeSpan.Zero);
		clock.Advance(TimeSpan.Zero);

		var move = NewMovePacket([0x00]);
		Assert.True(move.Read()); // Java-shaped scalar helpers log under-read errors and return defaults.

		SimulationLogPolicyException error = Assert.Throws<SimulationLogPolicyException>(policy.AssertClean);
		Assert.Contains(error.Problems, problem =>
			problem.Kind == "virtual-timer" && problem.ExceptionText!.Contains("spawn probe boom", StringComparison.Ordinal));
		Assert.Contains(error.Problems, problem =>
			problem.Kind == "log" && problem.Message.Contains("Missing F for", StringComparison.Ordinal));
		Assert.Contains("System.ApplicationException: spawn probe boom", error.Message, StringComparison.Ordinal);
		Assert.Contains("ThrowingSpawnProbe.HandleSpawned", error.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task NonThrowingSpawnProbeAndCompleteMovePass()
	{
		await using var clock = new VirtualThreadPool(strict: true);
		using var policy = NewPolicy(clock, "self-test-pass");
		var probe = new PassingSpawnProbe(UninitializedNpc());
		clock.Schedule(_ =>
		{
			probe.OnGeneralEvent(AiEventType.Spawned);
			return ValueTask.CompletedTask;
		}, TimeSpan.Zero);
		clock.Advance(TimeSpan.Zero);

		var move = NewMovePacket(new byte[14]);
		Assert.True(move.Read());
		policy.AssertClean();
		Assert.True(probe.Spawned);
	}

	private static SimulationLogPolicy NewPolicy(VirtualThreadPool clock, string scenario) => new(
		"harness-self-test",
		scenario,
		clock,
		Path.Combine(RealStaticData.RepoRoot(), "parity-artifacts", "e2e", "log-allowlist.json"));

	private static CM_MOVE NewMovePacket(byte[] body)
	{
		var packet = new CM_MOVE(0x6F0, new HashSet<AionConnection.State> { AionConnection.State.IN_GAME });
		packet.SetBuffer(ByteBuffer.Wrap(body).Order(ByteOrder.LITTLE_ENDIAN));
		return packet;
	}

	private static Npc UninitializedNpc() =>
		(Npc)RuntimeHelpers.GetUninitializedObject(typeof(Npc));

	private sealed class ThrowingSpawnProbe(Npc owner) : AITemplate<Npc>(owner)
	{
		protected override void HandleSpawned() => throw new ApplicationException("spawn probe boom");
	}

	private sealed class PassingSpawnProbe(Npc owner) : AITemplate<Npc>(owner)
	{
		public bool Spawned { get; private set; }

		protected override void HandleSpawned() => Spawned = true;
	}
}
