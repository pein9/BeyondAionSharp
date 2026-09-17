using Aion.Bots.Protocol;
using Aion.Commons.Logging;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.TestKit;
using Microsoft.Extensions.Logging;

namespace Aion.Simulation.Tests;

public sealed class SimulationLogPolicyTests
{
	[Fact]
	public async Task ErrorAndVirtualFaultFailWithContextExceptionAndRecentPackets()
	{
		await using var clock = new VirtualThreadPool(strict: false);
		using var policy = NewPolicy(clock);
		policy.ObservePacket("b01", "s01", new DecodedBotServerPacket(
			typeof(SM_PONG),
			new Dictionary<string, object?> { ["value"] = 7 }));
		using (policy.BeginBotStep("b01", "s02"))
			AionLog.For("SCENARIO_LOG").LogError(new InvalidOperationException("boom"), "step failed");
		clock.Schedule(_ => throw new ApplicationException("timer boom"), TimeSpan.Zero);
		clock.Advance(TimeSpan.Zero);

		SimulationLogPolicyException error = Assert.Throws<SimulationLogPolicyException>(policy.AssertClean);

		Assert.Contains(error.Problems, problem => problem.Kind == "log" && problem.Bot == "b01" && problem.Step == "s02");
		Assert.Contains(error.Problems, problem => problem.Kind == "virtual-timer");
		Assert.Contains("System.InvalidOperationException: boom", error.Message, StringComparison.Ordinal);
		Assert.Contains("Last packets for b01", error.Message, StringComparison.Ordinal);
		Assert.Contains("s01 SM_PONG", error.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task WarningAuditAndUnexpectedRefusalRequireOptIn()
	{
		await using var clock = new VirtualThreadPool(strict: true);
		using (var defaultPolicy = NewPolicy(clock))
		{
			AionLog.For("WARN_LOG").LogWarning("ignored warning");
			AionLog.For("AUDIT_LOG").LogInformation("ignored audit");
			defaultPolicy.ObservePacket("b02", "s03", new DecodedBotServerPacket(
				typeof(SM_SYSTEM_MESSAGE),
				new Dictionary<string, object?> { ["name"] = "STR_SKILL_NOT_READY", ["parameters"] = Array.Empty<string>() }));
			defaultPolicy.AssertClean();
		}

		using var policy = NewPolicy(clock, new SimulationLogPolicyOptions
		{
			FailOnWarnings = true,
			FailOnAuditLog = true,
			UnexpectedRefusalSystemMessages = new HashSet<string>(["STR_SKILL_NOT_READY"], StringComparer.Ordinal),
		});
		AionLog.For("WARN_LOG").LogWarning("warning");
		AionLog.For("AUDIT_LOG").LogInformation("audit");
		policy.ObservePacket("b02", "s04", new DecodedBotServerPacket(
			typeof(SM_SYSTEM_MESSAGE),
			new Dictionary<string, object?> { ["name"] = "STR_SKILL_NOT_READY", ["parameters"] = Array.Empty<string>() }));

		SimulationLogPolicyException error = Assert.Throws<SimulationLogPolicyException>(policy.AssertClean);

		Assert.Contains(error.Problems, problem => problem.Level == LogLevel.Warning);
		Assert.Contains(error.Problems, problem => problem.Kind == "audit");
		Assert.Contains(error.Problems, problem => problem.Kind == "unexpected-refusal" && problem.Bot == "b02");
	}

	[Fact]
	public async Task SimAllowlistHonorsModeServerAndMaxCount()
	{
		await using var clock = new VirtualThreadPool(strict: true);
		string path = Path.GetTempFileName();
		try
		{
			string fingerprint;
			using (var probe = NewPolicy(clock))
			{
				AionLog.For("KNOWN_LOG").LogError("known problem");
				SimulationLogPolicyException error = Assert.Throws<SimulationLogPolicyException>(probe.AssertClean);
				fingerprint = Assert.Single(error.Problems).Fingerprint;
			}
			await File.WriteAllTextAsync(path, $$"""
				[{"fp":"{{fingerprint}}","reason":"known test issue","owner":"tests","tracking":"P5-09","modes":["SIM"],"servers":["gs"],"maxCount":1,"expires":"2099-01-01"}]
				""");
			using var policy = new SimulationLogPolicy("r1", "S0", clock, path);
			AionLog.For("KNOWN_LOG").LogError("known problem");

			policy.AssertClean();
		}
		finally
		{
			File.Delete(path);
		}
	}

	private static SimulationLogPolicy NewPolicy(VirtualThreadPool clock, SimulationLogPolicyOptions? options = null) =>
		new("r1", "S0", clock, Path.Combine(RealStaticData.RepoRoot(), "parity-artifacts", "e2e", "log-allowlist.json"), options);
}
