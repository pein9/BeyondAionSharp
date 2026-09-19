using Aion.Bots.Protocol;
using Aion.Commons.Nio;
using Aion.GameServer.Model.Trade;
using Aion.GameServer.Network;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Tests;

public sealed partial class BotGameClientPacketWriterTests
{
	[Fact]
	public void PrivateStoreOffersUseUnsignedCountsLongUnitPricesAndExplicitClose()
	{
		var outbound = GameClientPackets.PrivateStore(new(1, 2, 65535, 5_000_000_001), new(3, 4, 5, 6));
		Assert.Equal("02000100000002000000FFFF01F2052A01000000030000000400000005000600000000000000", Convert.ToHexString(outbound.Body));
		var liveCrypt = new Crypt(); var codec = new GamePacketCodec();
		codec.RecoverKeyFromSmKeyFrame(CreateKeyFrame(liveCrypt.EnableKey()));
		var payload = outbound.Encode(codec, AionConnection.State.IN_GAME)[2..];
		Assert.True(liveCrypt.Decrypt(ByteBuffer.Wrap(payload).Order(ByteOrder.LITTLE_ENDIAN)));
		var connection = new ParserConnection(); connection.SetState(AionConnection.State.IN_GAME);
		var packet = AionClientPacketFactory.TryCreatePacket(ByteBuffer.Wrap(payload).Order(ByteOrder.LITTLE_ENDIAN), connection)!;
		Assert.True(packet.Read()); Assert.Equal(0, packet.GetRemainingBytes());
		var items = Assert.IsType<TradePSItem[]>(GetFieldValue(packet, "tradePSItems"));
		Assert.Equal(2, items.Length);
		Assert.Equal((1, 2, 65535L, 5_000_000_001L), (items[0].GetItemObjId(), items[0].GetItemId(), items[0].GetCount(), items[0].GetPrice()));
		Assert.Equal((3, 4, 5L, 6L), (items[1].GetItemObjId(), items[1].GetItemId(), items[1].GetCount(), items[1].GetPrice()));
		Assert.Equal("0000", Convert.ToHexString(GameClientPackets.PrivateStore().Body));
		Assert.Equal("41002D4E0000", Convert.ToHexString(GameClientPackets.PrivateStoreName("A中").Body));
	}
}
