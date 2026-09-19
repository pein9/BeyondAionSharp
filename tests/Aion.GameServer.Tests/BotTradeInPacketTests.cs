using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotTradeInPacketTests
{
	private readonly BotServerPacketDecoder decoder = new();
	public static bool AssertAuditedWireContract(Type type)
	{
		if (type != typeof(SM_TRADE_IN_LIST)) return false;
		new BotTradeInPacketTests().TradeInTabsPreserveOrderAndClearOnEmptyCloseOrReload();
		return true;
	}

	[Fact]
	public void NpcSellRateIsAppliedAfterPerStageRoundingAndQuantity()
	{
		var prices = new BotVendorPrices(101, 102, 103);
		Assert.Equal(7740, prices.BuyListPrice(123, 100, 6000, 1));
		Assert.Equal(23220, prices.BuyListPrice(123, 100, 6000, 3));
		Assert.Equal(7830, prices.BuyPrice(123, 6000)); // Folding the NPC rate into the first stage is different.
		Assert.Equal(193, prices.BuyListPrice(123, 100, 50, 3)); // Quantity before division, not 64 * 3.
	}

	[Fact]
	public void TradeInWriterPinsMaskObjectIdsAndUnsignedRowCount()
	{
		Assert.Equal("44332211FE020000000300000002000400000005000000",
			Convert.ToHexString(GameClientPackets.BuyTradeIn(0x11223344, 0xFE, 2, 3, 4, 5).Body));
		Assert.Equal(15, GameClientPackets.BuyTradeIn(1, 0, 2, 1).Body.Length);
		Assert.Equal(15 + 65535 * 4, GameClientPackets.BuyTradeIn(1, 0, 2, 1, new int[65535]).Body.Length);
		Assert.Throws<OverflowException>(() => GameClientPackets.BuyTradeIn(1, 0, 2, 1, new int[65536]));
	}

	[Fact]
	public void TradeInTabsPreserveOrderAndClearOnEmptyCloseOrReload()
	{
		byte[] body = Body(w => { w.Write(42); w.Write((byte)2); w.Write(150); w.Write(100);
			w.Write((ushort)2); w.Write(39); w.Write(40); });
		var packet = Checked(typeof(SM_TRADE_IN_LIST), body, emptyValid: true);
		foreach (string action in new[] { "empty", "close", "reload" })
		{
			var world = new BotWorldModel(); world.Apply(packet);
			Assert.NotNull(world.TradeIn);
			Assert.Equal((42, (byte)2, 150, 100), (world.TradeIn.TargetObjectId, world.TradeIn.NpcType,
				world.TradeIn.BuyPriceModifier, world.TradeIn.PriceRate));
			Assert.Equal(new[] { 39, 40 }, world.TradeIn.Tabs);
			if (action == "reload") world.BeginWorldReload();
			else if (action == "empty") world.Apply(decoder.Decode(typeof(SM_TRADE_IN_LIST), []));
			else world.Apply(decoder.Decode(typeof(SM_DIALOG_WINDOW), Body(w => { w.Write(42); w.Write((ushort)0); w.Write(0); })));
			Assert.Null(world.TradeIn); Assert.Empty(world.Inventory);
		}
		var noTabs = Checked(typeof(SM_TRADE_IN_LIST), Body(w =>
		{
			w.Write(42); w.Write((byte)0); w.Write(100); w.Write(100); w.Write((ushort)0);
		}), emptyValid: true);
		Assert.Empty(noTabs.Get<int[]>("tabs"));
	}

	[Fact]
	public void LimitedVendorRowsPreserveUnsignedCountsAndReplacePreviousState()
	{
		var world = new BotWorldModel();
		world.Apply(Checked(typeof(SM_TRADELIST), Vendor(65535, 32768)));
		Assert.NotNull(world.Trade);
		Assert.Equal(new[] { 5011, 5018 }, world.Trade.Tabs);
		Assert.Equal(new BotLimitedTradeItem(169405022, 65535, 32768), Assert.Single(world.Trade.LimitedItems));
		world.Apply(Checked(typeof(SM_TRADELIST), Vendor(100, 0)));
		Assert.Equal(new BotLimitedTradeItem(169405022, 100, 0), Assert.Single(world.Trade!.LimitedItems));
		Assert.Empty(world.Inventory); // Catalog stock is not a purchase notification.
	}

	private DecodedBotServerPacket Checked(Type type, byte[] body, bool emptyValid = false)
	{
		var packet = decoder.Decode(type, body);
		for (int length = emptyValid ? 1 : 0; length < body.Length; length++)
			Assert.Throws<InvalidDataException>(() => decoder.Decode(type, body[..length]));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(type, [.. body, 0]));
		return packet;
	}
	private static byte[] Vendor(ushort purchased, ushort stock) => Body(w =>
	{
		w.Write(42); w.Write((byte)0); w.Write(100); w.Write(100); w.Write((byte)1); w.Write((byte)1);
		w.Write((ushort)2); w.Write(5011); w.Write(5018); w.Write((ushort)1);
		w.Write(169405022); w.Write(purchased); w.Write(stock);
	});
	private static byte[] Body(Action<BinaryWriter> action)
	{
		using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
		action(writer); return stream.ToArray();
	}
}
