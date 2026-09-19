using Aion.Bots.Protocol;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Tests;

public sealed partial class BotGameClientPacketWriterTests
{
	private static IEnumerable<object[]> BrokerPacketCases(AionConnection.State game)
	{
		yield return C("broker-register", GameClientPackets.RegisterBrokerItem(1, 2, 5_000_000_001, 6_000_000_002, true), game,
			new Dictionary<string, object?> { ["brokerObjId"] = 1, ["itemUniqueId"] = 2, ["price"] = 5_000_000_001L, ["itemCount"] = 6_000_000_002L, ["splittingAvailable"] = true });
		yield return C("broker-buy", GameClientPackets.BuyBrokerItem(1, 2, 6_000_000_002), game,
			new Dictionary<string, object?> { ["brokerObjId"] = 1, ["itemUniqueId"] = 2, ["itemCount"] = 6_000_000_002L });
		yield return C("broker-list", GameClientPackets.BrokerList(1, 6, 65535, 32768), game,
			new Dictionary<string, object?> { ["brokerObjId"] = 1, ["sortType"] = (byte)6, ["page"] = 65535, ["listMask"] = 32768 });
		yield return C("broker-search", GameClientPackets.BrokerSearch(1, 6, 65535, 32768, 152000102, 152000103), game,
			new Dictionary<string, object?> { ["brokerObjId"] = 1, ["sortType"] = (byte)6, ["page"] = 65535, ["mask"] = 32768, ["itemList"] = new List<int> { 152000102, 152000103 } });
		yield return C("broker-registered", GameClientPackets.BrokerRegistered(1), game, new Dictionary<string, object?> { ["brokerObjId"] = 1 });
		yield return C("broker-cancel", GameClientPackets.CancelBrokerItem(1, 2), game, new Dictionary<string, object?> { ["brokerObjId"] = 1, ["brokerItemId"] = 2 });
		yield return C("broker-settlements", GameClientPackets.BrokerSettlements(1, 65535), game, new Dictionary<string, object?> { ["brokerObjId"] = 1, ["startPageIndex"] = 65535 });
		yield return C("broker-settle", GameClientPackets.SettleBrokerAccount(1), game, new Dictionary<string, object?> { ["brokerObjId"] = 1 });
		yield return C("broker-sell-window", GameClientPackets.BrokerSellWindow(2), game, new Dictionary<string, object?> { ["itemUniqueId"] = 2 });
	}

	[Fact]
	public void BrokerClientBytesMatchAuditedJavaFieldOrder()
	{
		Assert.Equal("01000000020000000300000000000000040000000000000001", Convert.ToHexString(GameClientPackets.RegisterBrokerItem(1, 2, 3, 4, true).Body));
		Assert.Equal("01000000020000000400000000000000", Convert.ToHexString(GameClientPackets.BuyBrokerItem(1, 2, 4).Body));
		Assert.Equal("0100000006FFFF0080", Convert.ToHexString(GameClientPackets.BrokerList(1, 6, 65535, 32768).Body));
		Assert.Equal("01000000060000000002000200000003000000", Convert.ToHexString(GameClientPackets.BrokerSearch(1, 6, 0, 0, 2, 3).Body));
		Assert.Equal("0100000002000000", Convert.ToHexString(GameClientPackets.CancelBrokerItem(1, 2).Body));
		Assert.Equal("01000000FFFF", Convert.ToHexString(GameClientPackets.BrokerSettlements(1, 65535).Body));
	}
}
