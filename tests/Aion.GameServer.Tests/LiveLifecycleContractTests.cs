using System.Buffers.Binary;
using System.Net.Sockets;
using Aion.Bots.World;
using Aion.LiveBots;

namespace Aion.GameServer.Tests;

public sealed class LiveLifecycleContractTests
{
	[Theory]
	[InlineData("O1", "1", "1200", true)]
	[InlineData("O1", "2", "1200", false)]
	[InlineData("O1", "10", "1200", false)]
	[InlineData("O1", "1", "15", false)]
	[InlineData("O1", "1", "1049", false)]
	[InlineData("O1", "1", "1050", true)]
	[InlineData("O1,connect", "1", "1200", false)]
	public void RequiresAnIsolatedSingleSubjectWithTimeForTheOrdinarySave(string scenario, string bots, string seconds, bool valid)
	{
		string[] args = ["--run", "lifecycle-contract", "--output", "run/lifecycle-contract", "--scenario", scenario,
			"--bots", bots, "--step-timeout-seconds", seconds, "--git-sha", "test"];
		if (!valid) { Assert.Throws<ArgumentException>(() => LiveBotOptions.Parse(args)); return; }
		var options = LiveBotOptions.Parse(args);
		Assert.Equal(1, options.BotCount);
		Assert.Equal("O1", Assert.Single(options.ScenarioDefinitions).Id);
	}

	[Theory]
	[InlineData(7, true)]
	[InlineData(0, false)]
	[InlineData(13, false)]
	[InlineData(22, false)]
	public void DuplicateAttemptMustBeRefusedWithAlreadyLoginNotAdmittedOrBanned(int code, bool valid)
	{
		byte[] frame = new byte[5]; frame[0] = 1;
		BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(1), code);
		if (valid) LiveLifecycleContract.RequireDuplicateRefusal(frame);
		else Assert.Throws<InvalidDataException>(() => LiveLifecycleContract.RequireDuplicateRefusal(frame));
		frame[0] = 3;
		Assert.Throws<InvalidDataException>(() => LiveLifecycleContract.RequireDuplicateRefusal(frame));
		Assert.Throws<InvalidDataException>(() => LiveLifecycleContract.RequireDuplicateRefusal(frame[..4]));
	}

	[Fact]
	public void OnlyPeerClosureIsExpectedNotTimeoutCancellationOrMalformedProtocol()
	{
		Assert.True(LiveLifecycleContract.IsPeerClose(new EndOfStreamException()));
		Assert.True(LiveLifecycleContract.IsPeerClose(new SocketException((int)SocketError.ConnectionReset)));
		Assert.True(LiveLifecycleContract.IsPeerClose(new IOException("reset", new SocketException((int)SocketError.ConnectionAborted))));
		foreach (Exception exception in new Exception[] { new IOException("disk"), new InvalidDataException("bad packet"),
			new SocketException((int)SocketError.TimedOut), new OperationCanceledException(), new TimeoutException() })
			Assert.False(LiveLifecycleContract.IsPeerClose(exception));
	}

	[Theory]
	[InlineData(1.09f, true)]
	[InlineData(1.11f, false)]
	[InlineData(5f, false)]
	[InlineData(float.NaN, false)]
	[InlineData(float.PositiveInfinity, false)]
	public void RecoveryMustRestoreSavedPositionWithinWireRounding(float x, bool valid)
	{
		var saved = new BotPosition(1, 2, 3, 0);
		var actual = saved with { X = x };
		if (valid) LiveLifecycleContract.RequirePosition(actual, saved);
		else Assert.Throws<InvalidDataException>(() => LiveLifecycleContract.RequirePosition(actual, saved));
	}
}
