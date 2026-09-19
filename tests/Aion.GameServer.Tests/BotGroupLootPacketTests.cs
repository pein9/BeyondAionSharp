using System.Buffers.Binary;
using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotGroupLootPacketTests
{
	private readonly BotServerPacketDecoder decoder = new();

	[Theory]
	[InlineData(0, 1)]
	[InlineData(2002, 77)]
	[InlineData(2002, -1)]
	public void GroupRollKeepsStartIntermediateAndSignedCompletionValues(int playerId, int luck)
	{
		// Checked-in SM_GROUP_LOOT Java golden, with the last two scalar values varied.
		byte[] body = Convert.FromHexString("E9030000050000000300000001A7340B000000BB0B000001D207000009030000");
		BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(24), playerId);
		BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(28), luck);
		var packet = decoder.Decode(typeof(SM_GROUP_LOOT), body);
		Assert.Equal(1001, packet.Get<int>("groupId")); Assert.Equal(5, packet.Get<int>("index"));
		Assert.Equal(188000001, packet.Get<int>("itemId")); Assert.Equal(3, packet.Get<int>("itemCount"));
		Assert.Equal(3003, packet.Get<int>("lootCorpseId")); Assert.Equal((byte)1, packet.Get<byte>("distributionId"));
		Assert.Equal(playerId, packet.Get<int>("playerId")); Assert.Equal(luck, packet.Get<int>("luck"));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_GROUP_LOOT), body[..^1]));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_GROUP_LOOT), [.. body, 0]));
	}

	[Fact]
	public void GroupRulesArePerceivedAndBoundedByTheFullPacket()
	{
		byte[] body = Convert.FromHexString("04BC0D0061AE0A0000000000010000000000000000000000020000000200000002000000020000000200000002000000003F00000000000000000000000000");
		var packet = decoder.Decode(typeof(SM_GROUP_INFO), body);
		var world = new BotWorldModel(); world.Apply(packet);
		Assert.Equal(new[] { 1, 0, 0, 2, 2, 2, 2, 2 }, world.GroupLootRules);
		Assert.Equal(0, packet.Get<int>("messageId")); Assert.Equal("", packet.Get<string>("message"));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_GROUP_INFO), body[..^1]));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_GROUP_INFO), [.. body, 0]));
	}

	[Fact]
	public void LootRightsComeOnlyFromReceivedPacketsAndClearOnReloadOrDelete()
	{
		var world = new BotWorldModel();
		Assert.False(world.LootStatuses.ContainsKey(123));
		world.Apply(decoder.Decode(typeof(SM_LOOT_STATUS), [123, 0, 0, 0, 0, 0, 0, 0, 0]));
		Assert.Equal((byte)0, world.LootStatuses[123]);
		world.Apply(new DecodedBotServerPacket(typeof(SM_DELETE), new Dictionary<string, object?> { ["objectId"] = 123 }));
		Assert.Empty(world.LootStatuses);
		world.Apply(decoder.Decode(typeof(SM_LOOT_STATUS), [123, 0, 0, 0, 1, 0, 0, 0, 0]));
		Assert.Equal((byte)1, world.LootStatuses[123]);
		world.BeginWorldReload(); Assert.Empty(world.LootStatuses);
	}

	[Fact]
	public void ApiWritesRollAndPassWithoutABidAndSetsAllQualityThresholds()
	{
		var api = new BotApi();
		foreach (byte rule in new byte[] { 0, 1, 2 })
		{
			var packet = api.SetGroupLoot(rule, 2);
			Assert.Equal(typeof(CM_DISTRIBUTION_SETTINGS), packet.PacketType);
			Assert.Equal(GameClientPackets.DistributionSettings(rule, 0, 2, 2, 2, 2, 2, 2).Body, packet.Body);
		}
		foreach (bool roll in new[] { false, true })
		{
			var packet = api.RollForLoot(100, 3, 162000031, 200, roll);
			Assert.Equal(typeof(CM_GROUP_LOOT), packet.PacketType);
			Assert.Equal(GameClientPackets.GroupLoot(100, 3, 162000031, 200, 2, roll, 0).Body, packet.Body);
		}
	}
}
