using System.Text;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotBrokerPacketTests
{
	private readonly BotServerPacketDecoder decoder = new();

	public static bool AssertAuditedWireContract(Type type)
	{
		if (type != typeof(SM_BROKER_SERVICE)) return false;
		var tests = new BotBrokerPacketTests();
		tests.ListingsPreserveLongPricesUnsignedDaysAndUnprefixedGearEntries();
		tests.SettlementIconAndRowsHaveDistinctFlagsDespiteSharingActionFive();
		tests.RegistrationCancellationAndRefusalDoNotInventInventoryTransfers();
		tests.SellWindowHasLongBoundsAndUnknownActionsAreRejected();
		return true;
	}

	[Fact]
	public void ListingsPreserveLongPricesUnsignedDaysAndUnprefixedGearEntries()
	{
		var world = new BotWorldModel();
		world.Apply(Checked(Listings(0, 11, 12)));
		Assert.Equal(2, world.BrokerSearch!.TotalCount);
		Assert.Equal((ushort)65535, world.BrokerSearch.Page);
		Assert.Equal(2, world.BrokerSearch.Items.Count);
		var row = world.BrokerSearch.Items[0];
		Assert.Equal(11, row.ObjectId); Assert.Equal(152000102, row.ItemId);
		Assert.Equal(6_000_000_000, row.TotalPrice); Assert.Equal(3_000_000_000, row.Count);
		Assert.Equal(2L, row.AverageUnitPrice); Assert.Null(row.RepeatedCount); Assert.Null(row.DaysLeft);
		Assert.Equal("Seller", row.Seller); Assert.Equal("Crafter", row.Creator);
		Assert.Equal((byte)7, row.Enchantment.EnchantLevel);
		Assert.Equal(1234, row.PolishCharge); Assert.Equal((byte)255, row.PackCount); Assert.True(row.SplittingAvailable);
		world.Apply(Checked(Listings(1, 21, 22)));
		Assert.Equal(2, world.BrokerRegistered.Count);
		row = world.BrokerRegistered[21];
		Assert.Null(row.Seller); Assert.Null(row.AverageUnitPrice);
		Assert.Equal(3_000_000_000, row.RepeatedCount); Assert.Equal((byte)255, row.DaysLeft);
		world.Apply(Checked(Listings(1, 23)));
		Assert.Equal(23, Assert.Single(world.BrokerRegistered).Key);
		world.Apply(Checked(Listings(0)));
		Assert.Empty(world.BrokerSearch.Items);
		Assert.Single(world.BrokerRegistered);
	}

	[Fact]
	public void SettlementIconAndRowsHaveDistinctFlagsDespiteSharingActionFive()
	{
		var world = new BotWorldModel();
		world.Apply(Checked(Settlements(iconOnly: false, includeRow: true)));
		Assert.False(world.BrokerSettlementIcon);
		Assert.Equal(5_000_000_001, world.BrokerSettledKinah);
		var page = world.BrokerSettlements!;
		var item = Assert.Single(page.Items);
		Assert.Equal(152000102, item.ItemId); Assert.Equal(5_000_000_001, item.Proceeds);
		Assert.Equal(3_000_000_000, item.Count); Assert.Equal(item.Count, item.RepeatedCount);
		Assert.Equal(1234567, item.SettledAtMinutes); Assert.Equal("Crafter", item.Creator);
		world.Apply(Checked(Settlements(iconOnly: true, includeRow: false)));
		Assert.True(world.BrokerSettlementIcon);
		Assert.Same(page, world.BrokerSettlements); // Notification is not an empty settlement-list response.
		world.Apply(Checked(Settlements(iconOnly: false, includeRow: false)));
		Assert.Empty(world.BrokerSettlements!.Items);
		world.Apply(Checked([6, 0]));
		Assert.False(world.BrokerSettlementIcon);
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_BROKER_SERVICE), Settlements(iconOnly: true, includeRow: true)));
	}

	[Fact]
	public void RegistrationCancellationAndRefusalDoNotInventInventoryTransfers()
	{
		var world = new BotWorldModel();
		var success = Checked(Body(w => { w.Write(new byte[] { 3, 0, 1 }); Row(w, 31, registered: true); }));
		Assert.Equal((byte)1, success.Get<byte>("position")); world.Apply(success);
		Assert.Equal(31, Assert.Single(world.BrokerRegistered).Key);
		var refused = Checked(Body(w => { w.Write(new byte[] { 3, 2 }); w.Write(new byte[174]); w.Write((ushort)255); w.Write(new byte[7]); }));
		world.Apply(refused); Assert.Single(world.BrokerRegistered);
		world.Apply(Checked(Body(w => { w.Write(new byte[] { 4, 0 }); w.Write(31); })));
		Assert.Empty(world.BrokerRegistered); Assert.Empty(world.Inventory);
	}

	[Fact]
	public void SellWindowHasLongBoundsAndUnknownActionsAreRejected()
	{
		var packet = Checked(Body(w =>
		{
			w.Write(new byte[] { 7, 0 }); w.Write(42); w.Write(0L); w.Write((byte)3); w.Write(5_000_000_001L); w.Write(9_000_000_002L);
		}));
		Assert.Equal(42, packet.Get<int>("objectId")); Assert.Equal((byte)3, packet.Get<byte>("averageType"));
		Assert.Equal(5_000_000_001, packet.Get<long>("lowestPrice")); Assert.Equal(9_000_000_002, packet.Get<long>("highestPrice"));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_BROKER_SERVICE), [2]));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_BROKER_SERVICE), [255]));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_BROKER_SERVICE), [6, 1]));
	}

	private DecodedBotServerPacket Checked(byte[] body)
	{
		var packet = decoder.Decode(typeof(SM_BROKER_SERVICE), body);
		for (int length = 0; length < body.Length; length++)
			Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_BROKER_SERVICE), body[..length]));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_BROKER_SERVICE), [.. body, 0]));
		return packet;
	}
	private static byte[] Listings(byte action, params int[] ids) => Body(w =>
	{
		w.Write(action); w.Write(action == 0 ? ids.Length : 0);
		if (action == 0) { w.Write((byte)0); w.Write(ushort.MaxValue); }
		w.Write((ushort)ids.Length);
		foreach (int id in ids) Row(w, id, action == 1);
	});
	private static void Row(BinaryWriter w, int id, bool registered)
	{
		w.Write(id); w.Write(152000102); w.Write(6_000_000_000L); w.Write(registered ? 3_000_000_000L : 2L); w.Write(3_000_000_000L);
		if (registered) w.Write((byte)255);
		var enchantment = new byte[138]; enchantment[1] = 7; w.Write(enchantment);
		if (!registered) S(w, "Seller");
		S(w, "Crafter"); w.Write(new byte[3]); w.Write(1234); w.Write((byte)255); w.Write((byte)1);
	}
	private static byte[] Settlements(bool iconOnly, bool includeRow) => Body(w =>
	{
		w.Write((byte)5); w.Write(5_000_000_001L); w.Write(includeRow ? 1 : 0); w.Write((ushort)0);
		w.Write((byte)(iconOnly ? 1 : 0)); w.Write((ushort)(includeRow ? 1 : 0));
		if (includeRow)
		{
			w.Write(152000102); w.Write(5_000_000_001L); w.Write(3_000_000_000L); w.Write(3_000_000_000L);
			w.Write(1234567); w.Write(new byte[138]); S(w, "Crafter");
		}
	});
	private static void S(BinaryWriter w, string value) => w.Write(Encoding.Unicode.GetBytes(value + "\0"));
	private static byte[] Body(Action<BinaryWriter> action)
	{
		using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream); action(writer); return stream.ToArray();
	}
}
