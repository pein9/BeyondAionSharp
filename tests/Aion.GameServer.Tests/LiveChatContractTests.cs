using Aion.LiveBots;

namespace Aion.GameServer.Tests;

public sealed class LiveChatContractTests
{
	[Fact]
	public void FaultScenarioRequiresTwoSubjectsAndEnoughTimeForNormalReconnect()
	{
		string[] args = ["--run", "chat-fault-contract", "--output", "run/chat-fault-contract", "--git-sha", "test", "--scenario", "B2F"];
		Assert.Throws<ArgumentException>(() => LiveBotOptions.Parse(args));
		Assert.Equal(2, LiveBotOptions.Parse([..args, "--step-timeout-seconds", "180"]).BotCount);
		Assert.Throws<ArgumentException>(() => LiveBotOptions.Parse([..args, "--step-timeout-seconds", "180", "--bots", "3"]));
	}

	[Fact]
	public void ScenarioRequiresExactlyTwoOrdinarySubjectsAndIsNeverAnExpectedFailure()
	{
		string[] args = ["--run", "chat-contract", "--output", "run/chat-contract", "--git-sha", "test"];
		var options = LiveBotOptions.Parse([..args, "--scenario", "B2"]);
		Assert.Equal(2, options.BotCount);
		Assert.Null(options.ScenarioDefinitions.Single().ExpectedFail);
		Assert.Throws<ArgumentException>(() => LiveBotOptions.Parse([..args, "--scenario", "B2", "--bots", "3"]));
		Assert.Throws<ArgumentException>(() => LiveBotOptions.Parse([..args, "--scenario", "B2,connect"]));
	}

	[Fact]
	public void ControlNeedsOnePositiveMarkerWithoutAnyLeakedMessage()
	{
		LiveChatContract.RequireControlBarrier(["marker"], "marker");
		Assert.Throws<InvalidDataException>(() => LiveChatContract.RequireControlBarrier([], "marker"));
		Assert.Throws<InvalidDataException>(() => LiveChatContract.RequireControlBarrier(["forbidden"], "marker"));
		Assert.Throws<InvalidDataException>(() => LiveChatContract.RequireControlBarrier(["forbidden", "marker"], "marker"));
		Assert.Throws<InvalidDataException>(() => LiveChatContract.RequireControlBarrier(["marker", "marker"], "marker"));
	}

	[Fact]
	public void AuthenticationRequiresEveryJavaResponseByte()
	{
		byte[] valid = [2, 0x40, 1, 0, 0, 0, 0, 0, 0x22, 8];
		LiveChatContract.RequireAuthResponse(valid);
		for (int index = 0; index < valid.Length; index++)
		{
			var bad = valid.ToArray(); bad[index] ^= 1;
			Assert.Throws<InvalidDataException>(() => LiveChatContract.RequireAuthResponse(bad));
			Assert.Throws<InvalidDataException>(() => LiveChatContract.RequireAuthResponse(valid[..index]));
		}
		Assert.Throws<InvalidDataException>(() => LiveChatContract.RequireAuthResponse([..valid, 0]));
	}

	[Fact]
	public void RefreshRequiresRandomPrefixAndUnchangedAccountDigest()
	{
		byte[] first = Enumerable.Range(0, 48).Select(i => (byte)i).ToArray();
		byte[] second = first.ToArray(); second[0] ^= 1;
		LiveChatContract.RequireTokenRefresh(first, second);
		Assert.Throws<InvalidDataException>(() => LiveChatContract.RequireTokenRefresh(first, first));
		Assert.Throws<InvalidDataException>(() => LiveChatContract.RequireTokenRefresh(first[..47], second));
		Assert.Throws<InvalidDataException>(() => LiveChatContract.RequireTokenRefresh(first, second[..47]));
		second[16] ^= 1;
		Assert.Throws<InvalidDataException>(() => LiveChatContract.RequireTokenRefresh(first, second));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(4)]
	[InlineData(5)]
	public void GagReplyAllowsOnlyTheObservedRemainingFiveMinuteWindow(int minutes) =>
		LiveChatContract.RequireGagResponse($"You have been gagged for {minutes} minutes.");

	[Theory]
	[InlineData("B2 must not broadcast while gagged")]
	[InlineData("You can chat again in this channel in 1 second.")]
	[InlineData("You have been gagged for 6 minutes.")]
	[InlineData("You have been gagged for -1 minutes.")]
	[InlineData("You have been gagged for five minutes.")]
	[InlineData("You have been gagged for 5 minutes")]
	[InlineData("")]
	public void GagAssertionNeverAcceptsBroadcastOrFloodControl(string text) =>
		Assert.Throws<InvalidDataException>(() => LiveChatContract.RequireGagResponse(text));
}
