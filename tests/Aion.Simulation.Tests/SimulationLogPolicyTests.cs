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
	public async Task WarningWithExceptionFailsWithoutOptingIntoPlainWarnings()
	{
		await using var clock = new VirtualThreadPool(strict: true);
		using var policy = NewPolicy(clock);
		using (policy.BeginBotStep("b01", "reward"))
			AionLog.For("QuestService").LogWarning(new InvalidOperationException("invalid reward index"), "Reward selection failed");

		var error = Assert.Throws<SimulationLogPolicyException>(policy.AssertClean);
		var problem = Assert.Single(error.Problems);
		Assert.Equal(LogLevel.Warning, problem.Level);
		Assert.Equal("b01", problem.Bot);
		Assert.Equal("reward", problem.Step);
		Assert.Contains("InvalidOperationException: invalid reward index", error.Message, StringComparison.Ordinal);
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

	[Theory]
	[InlineData("BaseClientPacket")]
	[InlineData("AionClientPacketFactory")]
	[InlineData("AionConnection")]
	public async Task EconomyPolicySelectsProtocolWarningsAndAuditWithoutPlainStartupWarnings(string category)
	{
		await using var clock = new VirtualThreadPool(strict: true);
		using var policy = NewPolicy(clock, new SimulationLogPolicyOptions
		{
			FailOnProtocolWarnings = true,
			FailOnAuditLog = true,
		});
		AionLog.For("GeoWorldLoader").LogWarning("existing startup warning");
		using (policy.BeginBotStep("b01", "trade"))
		{
			AionLog.For(category).LogWarning("malformed protocol");
			AionLog.For("AUDIT_LOG").LogInformation("invalid client action");
		}

		var error = Assert.Throws<SimulationLogPolicyException>(policy.AssertClean);
		Assert.Equal(2, error.Problems.Count);
		Assert.Contains(error.Problems, problem => problem.Kind == "log" && problem.Level == LogLevel.Warning);
		Assert.Contains(error.Problems, problem => problem.Kind == "audit");
		Assert.All(error.Problems, problem =>
		{
			Assert.Equal("b01", problem.Bot);
			Assert.Equal("trade", problem.Step);
		});
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

	[Fact]
	public async Task SimAllowlistHonorsScenarioScope()
	{
		await using var clock = new VirtualThreadPool(strict: true);
		string path = Path.GetTempFileName();
		try
		{
			string fingerprint;
			using (var probe = NewPolicy(clock))
			{
				AionLog.For("SCOPED_LOG").LogError("scenario-scoped problem");
				fingerprint = Assert.Single(Assert.Throws<SimulationLogPolicyException>(probe.AssertClean).Problems).Fingerprint;
			}
			await File.WriteAllTextAsync(path, $$"""
				[{"fp":"{{fingerprint}}","reason":"scenario-scoped test issue","owner":"tests","tracking":"P6-05","modes":["SIM"],"servers":["gs"],"scenarios":["M2"],"maxCount":1,"expires":"2099-01-01"}]
				""");
			using (var matching = new SimulationLogPolicy("r1", "M2", clock, path))
			{
				AionLog.For("SCOPED_LOG").LogError("scenario-scoped problem");
				matching.AssertClean();
			}
			using var different = new SimulationLogPolicy("r1", "M1", clock, path);
			AionLog.For("SCOPED_LOG").LogError("scenario-scoped problem");
			Assert.Throws<SimulationLogPolicyException>(different.AssertClean);
		}
		finally
		{
			File.Delete(path);
		}
	}

	private static SimulationLogPolicy NewPolicy(VirtualThreadPool clock, SimulationLogPolicyOptions? options = null) =>
		new("r1", "S0", clock, Path.Combine(RealStaticData.RepoRoot(), "parity-artifacts", "e2e", "log-allowlist.json"), options);
}
