using System.Buffers.Binary;
using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotGroupQuestPacketTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void SharedQuestOfferIsNotAcceptanceAndResponseTargetsTheSharer(bool alliance)
	{
		// SM_QUEST_ACTION SHARE: C action, D quest, D sharer, D alliance flag.
		byte[] body = new byte[13]; body[0] = 5;
		BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(1), 1112);
		BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(5), 12345);
		BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(9), alliance ? 1 : 0);
		var decoder = new BotServerPacketDecoder();
		var api = new BotApi();
		api.Observe(decoder.Decode(typeof(SM_QUEST_ACTION), body));
		Assert.Equal(new BotQuestShare(1112, 12345, alliance), api.World.PendingQuestShare);
		Assert.DoesNotContain(1112, api.World.Quests.Keys);
		var response = api.AcceptSharedQuest();
		Assert.Equal(typeof(CM_DIALOG_SELECT), response.PacketType);
		Assert.Equal(GameClientPackets.DialogSelect(12345, 20000, 0, 0, 1112).Body, response.Body);
		Assert.DoesNotContain(1112, api.World.Quests.Keys);
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_QUEST_ACTION), body[..^1]));
	}

	[Fact]
	public void AcceptanceNeedsAnObservedShareAndShareWriterDoesNotCreateLocalQuestState()
	{
		var api = new BotApi();
		Assert.Throws<InvalidOperationException>(() => api.AcceptSharedQuest());
		var request = api.ShareQuest(1112);
		Assert.Equal(typeof(CM_QUEST_SHARE), request.PacketType);
		Assert.Equal(new byte[] { 0x58, 0x04, 0, 0 }, request.Body);
		Assert.Empty(api.World.Quests);
	}
}
