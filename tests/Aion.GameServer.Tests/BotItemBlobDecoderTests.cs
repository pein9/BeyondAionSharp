using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotItemBlobDecoderTests
{
	[Theory]
	[InlineData("SM_INVENTORY_ADD_ITEM.json", 1)]
	[InlineData("SM_INVENTORY_ADD_ITEM_VARIANTS.json", 3)]
	[InlineData("SM_INVENTORY_ADD_ITEM_PERTYPE.json", 5)]
	[InlineData("SM_INVENTORY_ADD_ITEM_SUBOBJECT.json", 3)]
	[InlineData("SM_INVENTORY_ADD_ITEM_SUBOBJECT2.json", 3)]
	public void ExistingJavaFixturesExposeActualGearFields(string name, int count)
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null && !File.Exists(Path.Combine(directory.FullName, "AionServer.slnx"))) directory = directory.Parent;
		Assert.NotNull(directory);
		using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory.FullName, "parity-artifacts", "golden", "packets", name)));
		Assert.Equal("Java", document.RootElement.GetProperty("source").GetString());
		var cases = document.RootElement.GetProperty("cases").EnumerateArray().ToArray();
		Assert.Equal(count, cases.Length);
		var decoder = new BotServerPacketDecoder();
		foreach (var entry in cases)
		{
			var inputs = entry.GetProperty("inputs");
			var packet = decoder.Decode(typeof(SM_INVENTORY_ADD_ITEM), Convert.FromHexString(entry.GetProperty("payloadHex").GetString()!));
			var row = Assert.Single(packet.Get<List<IReadOnlyDictionary<string, object?>>>("items"));
			var details = Assert.IsType<BotItemDetails>(row["details"]);
			var enchantment = Assert.IsType<BotItemEnchantment>(details.Enchantment);
			Assert.Equal(inputs.GetProperty("itemId").GetInt32(), enchantment.SkinId);
			Assert.Equal((byte)0, enchantment.EnchantLevel);
			Assert.False(enchantment.SoulBound);
			Assert.Equal(inputs.TryGetProperty("tempering", out var tempering) ? tempering.GetByte() : (byte)0, enchantment.Tempering);
			bool hasStones = inputs.TryGetProperty("withManastones", out var stones) && stones.GetBoolean();
			Assert.Equal(hasStones ? new BotStoneSlots(167000001, 0, 167000002, 0, 0, 0) : BotStoneSlots.Empty, enchantment.Manastones);
			Assert.Equal(inputs.TryGetProperty("withGodStone", out var godstone) && godstone.GetBoolean() ? 168000123 : 0, enchantment.GodstoneId);
			Assert.Equal(inputs.TryGetProperty("chargePoints", out var charge) ? charge.GetInt32() : (int?)null, details.ChargePoints);
			if (inputs.TryGetProperty("fusionedItemId", out var fusion))
				Assert.Equal(new BotItemFusion(fusion.GetInt32(), BotStoneSlots.Empty, 3, 0), details.Fusion);
			else Assert.Null(details.Fusion);
			Assert.Equal(0L, details.EquippedSlot);
			Assert.Equal(new BotItemPremium(0, 0), details.Premium);
			Assert.Equal((byte)0, details.PackCount);
		}
	}

	[Fact]
	public void DecodesAllGearFieldsWithUnsignedSentinelsAndSixValueSemanticSlots()
	{
		var blob = BotItemBlobDecoder.Decode([.. Enchantment(), .. Fusion(), .. Equipped(1L << 40 | 2),
			.. Charge(432109), 0x10, 255, 254, 0, .. General(), 0x12, 3]);
		Assert.Equal(new BotItemGeneralInfo(0xFEDC, 5_000_000_001L, "Crafter"), blob.General);
		Assert.Equal(new BotItemEnchantment(true, 9, 100000123, 255, 255,
			new(167000001, 167000002, 167000003, 167000004, 167000005, 167000006), 168000123, 7, true, 54321), blob.Details.Enchantment);
		Assert.Equal(new BotItemFusion(100000999, new(167000011, 167000012, 167000013, 167000014, 167000015, 167000016), 3, 4), blob.Details.Fusion);
		Assert.Equal(1L << 40 | 2, blob.Details.EquippedSlot);
		Assert.Equal(432109, blob.Details.ChargePoints);
		Assert.Equal(new BotItemPremium(255, 254), blob.Details.Premium);
		Assert.Equal((byte)3, blob.Details.PackCount);
		Assert.Equal(blob, BotItemBlobDecoder.Decode([.. Enchantment(), .. Fusion(), .. Equipped(1L << 40 | 2),
			.. Charge(432109), 0x10, 255, 254, 0, .. General(), 0x12, 3]));
	}

	[Fact]
	public void PartialUpdatesPreserveGearWhileFullUpdatesClearRemovedFields()
	{
		var decoder = new BotServerPacketDecoder();
		var world = new BotWorldModel();
		world.Apply(decoder.Decode(typeof(SM_INVENTORY_ADD_ITEM), InventoryPacket(
			[.. Enchantment(), .. Fusion(), .. Equipped(0), .. Charge(432109), 0x10, 7, 2, 0, .. General(), 0x12, 3], add: true)));
		var before = world.Inventory[123];
		world.Apply(decoder.Decode(typeof(SM_INVENTORY_UPDATE_ITEM), InventoryPacket(Equipped(1L << 40 | 2), add: false)));
		var equipped = world.Inventory[123];
		Assert.Equal(before.Count, equipped.Count);
		Assert.Equal(before.Creator, equipped.Creator);
		Assert.Equal(before.Details.Enchantment, equipped.Details.Enchantment);
		Assert.Equal(before.Details.Fusion, equipped.Details.Fusion);
		Assert.Equal(1L << 40 | 2, equipped.Details.EquippedSlot);
		Assert.Equal((ushort)2, equipped.EquipmentSlot);
		world.Apply(decoder.Decode(typeof(SM_INVENTORY_UPDATE_ITEM), InventoryPacket(Charge(0), add: false)));
		Assert.Equal(equipped.Details with { ChargePoints = 0 }, world.Inventory[123].Details);
		world.Apply(decoder.Decode(typeof(SM_INVENTORY_UPDATE_ITEM), InventoryPacket(Equipped(0), add: false)));
		Assert.Equal((ushort)0, world.Inventory[123].EquipmentSlot);
		Assert.Equal((byte)3, world.Inventory[123].Details.PackCount);
		// A full update can remove conditioning, fusion and wrapping; absence must not preserve stale state.
		world.Apply(decoder.Decode(typeof(SM_INVENTORY_UPDATE_ITEM), InventoryPacket([.. Enchantment(), .. Equipped(0), .. General()], add: false)));
		var current = world.Inventory[123];
		Assert.Null(current.Details.ChargePoints); Assert.Null(current.Details.Fusion); Assert.Null(current.Details.Premium);
		Assert.Equal((byte)0, current.Details.PackCount);
		Assert.Equal(before.Details.Enchantment, current.Details.Enchantment);
		Assert.Equal(before.Count, current.Count);
	}

	[Fact]
	public void EveryKnownEntryRejectsTruncationAndUnknownOrDuplicateEntriesAreNotHiddenAfterGeneralInfo()
	{
		byte[][] entries = [Enchantment(), Fusion(), Equipped(2), Charge(100), [0x10, 1, 2, 0], [0x12, 3], General()];
		Assert.Equal(7, entries.Length);
		foreach (var entry in entries)
		{
			BotItemBlobDecoder.Decode(entry);
			for (int length = 1; length < entry.Length; length++)
				Assert.Throws<InvalidDataException>(() => BotItemBlobDecoder.Decode(entry[..length]));
			Assert.Throws<InvalidDataException>(() => BotItemBlobDecoder.Decode([.. entry, .. entry]));
		}
		Assert.Throws<InvalidDataException>(() => BotItemBlobDecoder.Decode([.. General(), 0x09]));
		Assert.Throws<InvalidDataException>(() => BotItemBlobDecoder.Decode([.. General(), 0x0B]));
		// Multiple bonus modifiers are legal, unlike duplicate singleton entries.
		var repeatedBonus = BotItemBlobDecoder.Decode([0x0A, 1, 0, 2, 0, 0, 0, 0, 0x0A, 2, 0, 3, 0, 0, 0, 0, .. General()]);
		Assert.Equal("Crafter", repeatedBonus.General!.Value.Creator);
	}

	private static byte[] Enchantment()
	{
		// Offsets include the entry ID. Layout is independently pinned to EnchantInfoBlobEntry's 138-byte payload.
		var data = new byte[139]; data[0] = 0x0B; data[1] = 1; data[2] = 9;
		BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(3), 100000123); data[7] = 255; data[8] = 255;
		for (int i = 0; i < 6; i++) BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(9 + i * 4), 167000001 + i);
		BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(33), 168000123);
		data[55] = 7; data[126] = 1; BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(127), 54321);
		return data;
	}
	private static byte[] Fusion()
	{
		var data = new byte[31]; data[0] = 0x0E; BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(1), 100000999);
		for (int i = 0; i < 6; i++) BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(5 + i * 4), 167000011 + i);
		data[29] = 3; data[30] = 4; return data;
	}
	private static byte[] Equipped(long slot)
	{
		var data = new byte[9]; data[0] = 0x06; BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(1), slot); return data;
	}
	private static byte[] Charge(int points)
	{
		var data = new byte[5]; data[0] = 0x0F; BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(1), points); return data;
	}
	private static byte[] General()
	{
		using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.Unicode);
		writer.Write((byte)0); writer.Write((ushort)0xFEDC); writer.Write(5_000_000_001L);
		writer.Write(Encoding.Unicode.GetBytes("Crafter\0")); writer.Write(new byte[21]); return stream.ToArray();
	}
	private static byte[] InventoryPacket(byte[] blob, bool add)
	{
		using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.Unicode);
		if (add) { writer.Write((ushort)28); writer.Write((ushort)1); }
		writer.Write(123); if (add) writer.Write(100000123);
		writer.Write(Encoding.Unicode.GetBytes("Gear\0")); writer.Write((ushort)blob.Length); writer.Write(blob);
		writer.Write((ushort)0); if (add) writer.Write((byte)0); return stream.ToArray();
	}
}
