using System.Text;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotPrivateStorePacketTests
{
	private readonly BotServerPacketDecoder decoder = new();
	public static bool AssertAuditedWireContract(Type type)
	{
		if (type != typeof(SM_PRIVATE_STORE) && type != typeof(SM_PRIVATE_STORE_NAME)) return false;
		var tests = new BotPrivateStorePacketTests();
		tests.CatalogPreservesOfferOrderDistinctCountsAndLongPrices();
		tests.PeerNameAndOpenCloseNotificationsDoNotTransferInventory();
		tests.WorldReloadAndDespawnForgetVisibleStores();
		return true;
	}
	[Fact]
	public void CatalogPreservesOfferOrderDistinctCountsAndLongPrices()
	{
		var packet = Checked(typeof(SM_PRIVATE_STORE), Catalog(21, 22));
		var world = new BotWorldModel(); world.Apply(packet);
		Assert.Equal(42, packet.Get<int>("sellerObjectId"));
		Assert.Equal(new[] { 21, 22 }, world.PrivateStoreListings[42].Select(i => i.ObjectId));
		var item = world.PrivateStoreListings[42][0];
		Assert.Equal(152000102, item.ItemId); Assert.Equal(ushort.MaxValue, item.Count);
		Assert.Equal(5_000_000_001, item.UnitPrice); Assert.Equal(8_000_000_000, item.General.ItemCount);
		Assert.Equal((ushort)0x7FFF, item.General.ItemMask); Assert.Equal("Crafter", item.General.Creator);
		Assert.Equal(1234, item.Details.ChargePoints); Assert.Equal((byte)3, item.Details.PackCount);
		world.Apply(Checked(typeof(SM_PRIVATE_STORE), Catalog(22)));
		Assert.Equal(22, Assert.Single(world.PrivateStoreListings[42]).ObjectId);
		world.Apply(Checked(typeof(SM_PRIVATE_STORE), Catalog())); Assert.Empty(world.PrivateStoreListings[42]);
		Assert.False(decoder.Decode(typeof(SM_PRIVATE_STORE), []).Get<bool>("hasStore"));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_PRIVATE_STORE), Body(w =>
		{
			w.Write(42); w.Write((ushort)1); w.Write(21); w.Write(152000102); w.Write((ushort)1); w.Write(2L); w.Write((ushort)0);
		})));
	}
	[Fact]
	public void PeerNameAndOpenCloseNotificationsDoNotTransferInventory()
	{
		var world = new BotWorldModel();
		world.Apply(Emotion(EmotionType.OPEN_PRIVATESHOP));
		world.Apply(Checked(typeof(SM_PRIVATE_STORE_NAME), Body(w => { w.Write(42); S(w, "Ore – 商店"); })));
		world.Apply(Checked(typeof(SM_PRIVATE_STORE), Catalog(21)));
		Assert.Contains(42, world.OpenPrivateStores); Assert.Equal("Ore – 商店", world.PrivateStoreNames[42]);
		world.Apply(Emotion(EmotionType.CLOSE_PRIVATESHOP));
		Assert.Empty(world.OpenPrivateStores); Assert.Empty(world.PrivateStoreNames); Assert.Empty(world.PrivateStoreListings);
		Assert.Empty(world.Inventory);
	}
	[Fact]
	public void WorldReloadAndDespawnForgetVisibleStores()
	{
		foreach (bool reload in new[] { false, true })
		{
			var world = new BotWorldModel(); world.Apply(Emotion(EmotionType.OPEN_PRIVATESHOP));
			world.Apply(Checked(typeof(SM_PRIVATE_STORE_NAME), Body(w => { w.Write(42); S(w, "Shop"); })));
			world.Apply(Checked(typeof(SM_PRIVATE_STORE), Catalog(21)));
			if (reload) world.BeginWorldReload();
			else world.Apply(decoder.Decode(typeof(SM_DELETE), Body(w => { w.Write(42); w.Write((byte)0); })));
			Assert.Empty(world.OpenPrivateStores); Assert.Empty(world.PrivateStoreNames); Assert.Empty(world.PrivateStoreListings);
		}
	}
	private DecodedBotServerPacket Emotion(EmotionType type) => decoder.Decode(typeof(SM_EMOTION), Body(w =>
	{
		w.Write(42); w.Write((byte)type); w.Write((ushort)0); w.Write(0f);
	}));
	private DecodedBotServerPacket Checked(Type type, byte[] body)
	{
		var packet = decoder.Decode(type, body);
		for (int length = type == typeof(SM_PRIVATE_STORE) ? 1 : 0; length < body.Length; length++)
			Assert.Throws<InvalidDataException>(() => decoder.Decode(type, body[..length]));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(type, [.. body, 0]));
		return packet;
	}
	private static byte[] Catalog(params int[] ids) => Body(w =>
	{
		w.Write(42); w.Write((ushort)ids.Length);
		foreach (int id in ids)
		{
			w.Write(id); w.Write(152000102); w.Write(ushort.MaxValue); w.Write(5_000_000_001L);
			var blob = Body(b =>
			{
				b.Write((byte)0); b.Write((ushort)0x7FFF); b.Write(8_000_000_000L); S(b, "Crafter"); b.Write(new byte[21]);
				b.Write((byte)0x0F); b.Write(1234); b.Write(new byte[] { 0x12, 3 });
			});
			w.Write((ushort)blob.Length); w.Write(blob);
		}
	});
	private static void S(BinaryWriter w, string value) => w.Write(Encoding.Unicode.GetBytes(value + "\0"));
	private static byte[] Body(Action<BinaryWriter> action)
	{
		using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream); action(writer); return stream.ToArray();
	}
}
