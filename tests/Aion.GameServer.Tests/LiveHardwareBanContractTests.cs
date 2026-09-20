using Aion.LiveBots;

namespace Aion.GameServer.Tests;

public sealed class LiveHardwareBanContractTests
{
	[Theory]
	[InlineData(true, "subject")]
	[InlineData(false, "")]
	[InlineData(false, null)]
	[InlineData(false, "other")]
	[InlineData(false, "SUBJECT")]
	public void GenericFailuresOrSuccessfulAuthenticationCannotProveHardwareRefusal(bool ok, string? actual) =>
		Assert.Throws<InvalidDataException>(() => LiveHardwareBanContract.RequireRefusal(ok, actual, "subject"));

	[Fact]
	public void AuthenticatedAccountHardwareRefusalIsRecognized() => LiveHardwareBanContract.RequireRefusal(false, "subject", "subject");

	[Fact]
	public void HardwareScenarioRequiresFiveSubjectsAndIsNotExpectedFailed()
	{
		string[] args = ["--run", "hardware-contract", "--output", "run/hardware-contract", "--git-sha", "test", "--scenario", "B4"];
		Assert.Throws<ArgumentException>(() => LiveBotOptions.Parse(args));
		var options = LiveBotOptions.Parse([..args, "--step-timeout-seconds", "180", "--bots", "5"]);
		Assert.Equal(5, options.BotCount);
		Assert.Null(Assert.Single(options.ScenarioDefinitions).ExpectedFail);
		Assert.Throws<ArgumentException>(() => LiveBotOptions.Parse([..args, "--step-timeout-seconds", "180", "--bots", "6"]));
		args[^1] = "B4,connect";
		Assert.Throws<ArgumentException>(() => LiveBotOptions.Parse([..args, "--step-timeout-seconds", "180"]));
	}
}
