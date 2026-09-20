using System.Buffers.Binary;
using Aion.Bots.Protocol;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.LiveBots;

namespace Aion.GameServer.Tests;

public sealed class LiveLoginBridgeContractTests
{
	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(int.MinValue)]
	[InlineData(int.MaxValue)]
	public void ReconnectKeyPreservesEverySignedBit(int key)
	{
		byte[] body = new byte[5]; BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(1), key);
		var decoder = new BotServerPacketDecoder();
		Assert.Equal(key, decoder.Decode(typeof(SM_RECONNECT_KEY), body).Get<int>("key"));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_RECONNECT_KEY), [..body, 0]));
		body[0] = 1;
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_RECONNECT_KEY), body));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(4)]
	public void TruncatedGameKeyIsRejected(int length) => Assert.Throws<InvalidDataException>(() =>
		new BotServerPacketDecoder().Decode(typeof(SM_RECONNECT_KEY), new byte[length]));

	[Fact]
	public void ReconnectBuilderIsEmptyAndSelectionOnly()
	{
		var packet = GameClientPackets.ReconnectAuth();
		Assert.Equal(typeof(CM_RECONNECT_AUTH), packet.PacketType);
		Assert.Empty(packet.Body);
		// Registry uses the production state table, which agrees with Java's AUTHED-only registration.
		var definition = GamePacketRegistry.Instance.GetClient(packet.PacketType);
		Assert.Equal(183, definition.Opcode);
		Assert.Equal(AionConnection.State.AUTHED, Assert.Single(definition.ValidStates));
		definition.EnsureValid(AionConnection.State.AUTHED);
		Assert.Throws<InvalidOperationException>(() => definition.EnsureValid(AionConnection.State.IN_GAME));
	}

	[Fact]
	public void UpdatedSessionRequiresAccountOpcodeStatusAndCompleteKey()
	{
		byte[] valid = new byte[16]; valid[0] = 12;
		BinaryPrimitives.WriteInt32LittleEndian(valid.AsSpan(1), 123);
		BinaryPrimitives.WriteInt32LittleEndian(valid.AsSpan(5), int.MinValue);
		Assert.Equal(int.MinValue, LiveLoginBridgeContract.ReadUpdatedSession(valid, 123));
		Assert.Throws<InvalidDataException>(() => LiveLoginBridgeContract.ReadUpdatedSession(valid, 124));
		for (int length = 0; length < 10; length++)
			Assert.Throws<InvalidDataException>(() => LiveLoginBridgeContract.ReadUpdatedSession(valid.AsSpan(0, length), 123));
		valid[9] = 1;
		Assert.Throws<InvalidDataException>(() => LiveLoginBridgeContract.ReadUpdatedSession(valid, 123));
		valid[9] = 0; valid[0] = 3;
		Assert.Throws<InvalidDataException>(() => LiveLoginBridgeContract.ReadUpdatedSession(valid, 123));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(3)]
	[InlineData(7)]
	[InlineData(12)]
	public void GenericLoginFailuresCannotPassAsAccountBan(byte opcode)
	{
		LiveLoginBridgeContract.RequireBanned([9, 0, 0, 0, 0, 0, 0, 0]);
		Assert.Throws<InvalidDataException>(() => LiveLoginBridgeContract.RequireBanned([opcode]));
		Assert.Throws<InvalidDataException>(() => LiveLoginBridgeContract.RequireBanned([]));
	}

	[Fact]
	public void ScenarioIsSingleSubjectAndNotExpectedFailure()
	{
		string[] args = ["--run", "bridge-contract", "--output", "run/bridge-contract", "--git-sha", "test", "--scenario", "B3"];
		Assert.Throws<ArgumentException>(() => LiveBotOptions.Parse(args));
		var options = LiveBotOptions.Parse([..args, "--step-timeout-seconds", "120"]);
		Assert.Equal(1, options.BotCount);
		Assert.Null(options.ScenarioDefinitions.Single().ExpectedFail);
		Assert.Throws<ArgumentException>(() => LiveBotOptions.Parse([..args, "--step-timeout-seconds", "120", "--bots", "2"]));
		args[^1] = "B3,connect";
		Assert.Throws<ArgumentException>(() => LiveBotOptions.Parse([..args, "--step-timeout-seconds", "120"]));
	}
}
