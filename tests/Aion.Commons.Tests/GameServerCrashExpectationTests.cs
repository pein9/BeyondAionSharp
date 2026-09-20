using System.Text.Json;
using Aion.LogWatch;

namespace Aion.Commons.Tests;

public sealed class GameServerCrashExpectationTests
{
	private static readonly DateTimeOffset Epoch = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
	private static readonly string Container = new('a', 64);
	private static GameServerCrashExpectation.Plan Plan() => new()
	{
		SchemaVersion = 1, Run = "test", Project = "aion-bots-test", ContainerId = Container,
		ArmedUtc = Epoch, KillDeadlineUtc = Epoch.AddSeconds(30), RecoveryDeadlineUtc = Epoch.AddSeconds(180),
	};
	private static GameServerCrashExpectation Load(GameServerCrashExpectation.Plan? plan = null) =>
		GameServerCrashExpectation.Load(JsonSerializer.Serialize(plan ?? Plan(), new JsonSerializerOptions(JsonSerializerDefaults.Web)),
			"test", "aion-bots-test", Epoch);

	[Theory]
	[InlineData("schema")]
	[InlineData("run")]
	[InlineData("project")]
	[InlineData("container")]
	[InlineData("stale")]
	[InlineData("future")]
	[InlineData("kill-too-long")]
	[InlineData("kill-before-arm")]
	[InlineData("recovery-too-long")]
	[InlineData("recovery-before-kill")]
	public void InvalidOrBroadPlansAreRejected(string change)
	{
		var plan = change switch
		{
			"schema" => Plan() with { SchemaVersion = 2 },
			"run" => Plan() with { Run = "other" },
			"project" => Plan() with { Project = "aion" },
			"container" => Plan() with { ContainerId = "gameserver" },
			"stale" => Plan() with { ArmedUtc = Epoch.AddSeconds(-6) },
			"future" => Plan() with { ArmedUtc = Epoch.AddSeconds(2) },
			"kill-too-long" => Plan() with { KillDeadlineUtc = Epoch.AddSeconds(31) },
			"kill-before-arm" => Plan() with { KillDeadlineUtc = Epoch },
			"recovery-too-long" => Plan() with { RecoveryDeadlineUtc = Epoch.AddSeconds(181) },
			_ => Plan() with { RecoveryDeadlineUtc = Epoch.AddSeconds(30) },
		};
		Assert.Throws<InvalidDataException>(() => Load(plan));
	}

	[Theory]
	[InlineData("{}")]
	[InlineData("[]")]
	[InlineData("{broken")]
	public void MalformedOrIncompletePlansAreRejected(string json) => Assert.Throws<JsonException>(() =>
		GameServerCrashExpectation.Load(json, "test", "aion-bots-test", Epoch));

	[Theory]
	[InlineData("aion-bots-other", "gameserver", "die", "137", 1)]
	[InlineData("aion-bots-test", "loginserver", "die", "137", 1)]
	[InlineData("aion-bots-test", "gameserver", "oom", "137", 1)]
	[InlineData("aion-bots-test", "gameserver", "restart", "137", 1)]
	[InlineData("aion-bots-test", "gameserver", "die", "0", 1)]
	[InlineData("aion-bots-test", "gameserver", "die", null, 1)]
	[InlineData("aion-bots-test", "gameserver", "die", "137", -1)]
	[InlineData("aion-bots-test", "gameserver", "die", "137", 31)]
	public void UnrelatedOrUnplannedEventsRemainProblems(string project, string service, string action, string? exitCode, int seconds)
	{
		var expectation = Load();
		Assert.False(expectation.ObserveDocker(project, Container, service, action, exitCode, Epoch.AddSeconds(seconds)));
		Assert.Null(expectation.DiedUtc);
		Assert.False(expectation.ExpectsHeartbeatGap("gs", Epoch.AddSeconds(25)));
	}

	[Fact]
	public void ExactContainerAndSingleDeathAreRequired()
	{
		var expectation = Load();
		Assert.False(expectation.ObserveDocker("aion-bots-test", new string('b', 64), "gameserver", "die", "137", Epoch.AddSeconds(1)));
		Assert.True(expectation.ObserveDocker("aion-bots-test", Container, "gameserver", "die", "137", Epoch.AddSeconds(1)));
		Assert.False(expectation.ObserveDocker("aion-bots-test", Container, "gameserver", "die", "137", Epoch.AddSeconds(2)));
		Assert.True(expectation.ExpectsHeartbeatGap("gs", Epoch.AddSeconds(25)));
		Assert.False(expectation.ExpectsHeartbeatGap("ls", Epoch.AddSeconds(25)));
		Assert.False(expectation.ExpectsHeartbeatGap("gs", Epoch.AddSeconds(181)));
		Assert.NotNull(expectation.Failure(Epoch.AddSeconds(181), final: false));
	}

	[Fact]
	public void StartWithoutDeathOrFreshHeartbeatDoesNotProveRecovery()
	{
		var expectation = Load();
		Assert.False(expectation.ObserveDocker("aion-bots-test", Container, "gameserver", "start", null, Epoch));
		Assert.True(expectation.ObserveDocker("aion-bots-test", Container, "gameserver", "die", "137", Epoch.AddSeconds(1)));
		expectation.ObserveHeartbeat("gs", Epoch);
		Assert.True(expectation.ObserveDocker("aion-bots-test", Container, "gameserver", "start", null, Epoch.AddSeconds(2)));
		expectation.ObserveHeartbeat("ls", Epoch.AddSeconds(3));
		Assert.False(expectation.Complete);
		Assert.NotNull(expectation.Failure(Epoch.AddSeconds(3), final: true));
		expectation.ObserveHeartbeat("gs", Epoch.AddSeconds(181));
		Assert.False(expectation.Complete);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void RecoveryNeedsStartAndFreshHeartbeatEvenWhenFilesAndDockerAreReadInDifferentOrder(bool heartbeatFirst)
	{
		var expectation = Load();
		Assert.True(expectation.ObserveDocker("aion-bots-test", Container, "gameserver", "die", "137", Epoch.AddSeconds(1)));
		if (heartbeatFirst) expectation.ObserveHeartbeat("gs", Epoch.AddSeconds(3));
		Assert.True(expectation.ObserveDocker("aion-bots-test", Container, "gameserver", "start", null, Epoch.AddSeconds(2)));
		if (!heartbeatFirst) expectation.ObserveHeartbeat("gs", Epoch.AddSeconds(3));
		Assert.True(expectation.Complete);
		Assert.False(expectation.ExpectsHeartbeatGap("gs", Epoch.AddSeconds(4)));
		Assert.Null(expectation.Failure(Epoch.AddSeconds(200), final: true));
	}

	[Fact]
	public void MissingDeathFailsOnDeadlineAndAtFinalization()
	{
		Assert.Null(Load().Failure(Epoch.AddSeconds(30), final: false));
		Assert.NotNull(Load().Failure(Epoch.AddSeconds(31), final: false));
		Assert.NotNull(Load().Failure(Epoch, final: true));
	}
}
