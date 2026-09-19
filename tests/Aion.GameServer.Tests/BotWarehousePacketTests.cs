using System.Text;
using System.Text.Json;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotWarehousePacketTests
{
	private readonly BotServerPacketDecoder decoder = new();

	[Theory]
	[InlineData(typeof(SM_WAREHOUSE_INFO), 2)]
	[InlineData(typeof(SM_WAREHOUSE_ADD_ITEM), 1)]
	[InlineData(typeof(SM_WAREHOUSE_UPDATE_ITEM), 1)]
	[InlineData(typeof(SM_DELETE_WAREHOUSE_ITEM), 1)]
	public void ExistingJavaFixturesDecodeAndRejectTruncationAndTrailingBytes(Type type, int expectedCases)
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null && !File.Exists(Path.Combine(directory.FullName, "AionServer.slnx"))) directory = directory.Parent;
		Assert.NotNull(directory);
		using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory.FullName, "parity-artifacts", "golden", "packets", type.Name + ".json")));
		Assert.Equal("Java", fixture.RootElement.GetProperty("source").GetString());
		var cases = fixture.RootElement.GetProperty("cases").EnumerateArray().ToArray();
		Assert.Equal(expectedCases, cases.Length);
		foreach (var entry in cases)
		{
			byte[] body = Convert.FromHexString(entry.GetProperty("payloadHex").GetString()!);
			var packet = decoder.Decode(type, body);
			Assert.Equal(entry.GetProperty("inputs").GetProperty("warehouseType").GetByte(), packet.Get<byte>("warehouseType"));
			if (type == typeof(SM_WAREHOUSE_INFO) || type == typeof(SM_WAREHOUSE_ADD_ITEM))
			{
				var items = packet.Get<List<BotInventoryItem>>("items");
				int expectedItems = entry.GetProperty("inputs").TryGetProperty("itemCount", out var count) ? count.GetInt32() : 1;
				Assert.Equal(expectedItems, items.Count);
				foreach (var item in items)
				{
					Assert.Equal(161000001, item.ItemId);
					Assert.Equal(7, item.Count);
					Assert.Equal("Daeva", item.Creator);
					Assert.Equal(ushort.MaxValue, item.EquipmentSlot);
				}
			}
			else if (type == typeof(SM_WAREHOUSE_UPDATE_ITEM))
			{
				Assert.Equal(268510003, packet.Get<int>("objectId"));
				Assert.Equal(7, packet.Get<long>("itemCount"));
				Assert.Equal((ushort)22, packet.Get<ushort>("updateMask"));
			}
			else
			{
				Assert.Equal(777, packet.Get<int>("itemObjectId"));
				Assert.Equal((byte)20, packet.Get<byte>("deleteType"));
			}
			for (int length = 0; length < body.Length; length++)
			{
				// A sendable update mask is optional, so removing exactly its two bytes is valid.
				if (type == typeof(SM_WAREHOUSE_UPDATE_ITEM) && length == body.Length - 2)
					Assert.Null(decoder.Decode(type, body[..length]).Fields["updateMask"]);
				else Assert.Throws<InvalidDataException>(() => decoder.Decode(type, body[..length]));
			}
			Assert.Throws<InvalidDataException>(() => decoder.Decode(type, [.. body, 0]));
		}
	}

	[Fact]
	public void PaginatedSnapshotsReplaceStaleContentsAndAccountTerminatorDoesNotEraseItems()
	{
		var world = new BotWorldModel();
		ApplyInfo(world, 1, true, 0, Row(10));
		ApplyInfo(world, 1, false, 0, Row(11));
		ApplyInfo(world, 1, false, 0);
		Assert.Equal(new[] { 10, 11 }, world.Warehouses[1].Items.Keys.Order().ToArray());
		ApplyInfo(world, 2, true, 0, Row(20));
		ApplyInfo(world, 2, false, 0);
		ApplyInfo(world, 1, true, 1, Row(12));
		ApplyInfo(world, 1, false, 1);
		ApplyInfo(world, 2, false, 0); // Warehouse expansion refresh excludes account contents.
		Assert.Equal(12, Assert.Single(world.Warehouses[1].Items).Key);
		Assert.Equal(32, world.Warehouses[1].CharacterCapacity);
		Assert.Equal(20, Assert.Single(world.Warehouses[2].Items).Key);
		Assert.Null(world.Warehouses[2].CharacterCapacity);
		world.BeginWorldReload(); // Channel/zone changes invalidate nearby objects, not storage item identities.
		Assert.Equal(12, Assert.Single(world.Warehouses[1].Items).Key);
		Assert.Equal(20, Assert.Single(world.Warehouses[2].Items).Key);
		ApplyInfo(world, 1, false, 1); // Empty regular reopen has no first packet.
		Assert.Empty(world.Warehouses[1].Items);
		ApplyInfo(world, 2, true, 0); // Empty account snapshot has a first packet.
		ApplyInfo(world, 2, false, 0);
		Assert.Empty(world.Warehouses[2].Items);
	}

	[Fact]
	public void UpdatesPreserveGearAndLongKinahWhileDeletionsAreScopedToStorage()
	{
		var world = new BotWorldModel();
		byte[] row = Row(30, BotWorldModel.KinahItemId, 5_000_000_000L);
		world.Apply(decoder.Decode(typeof(SM_WAREHOUSE_ADD_ITEM), Body(w =>
		{
			w.Write((byte)2); w.Write((ushort)19); w.Write((ushort)1); w.Write(row);
		})));
		ApplyInfo(world, 1, true, 0, Row(30));
		ApplyInfo(world, 1, false, 0);
		var before = world.Warehouses[2].Items[30];
		Assert.Equal(5_000_000_000L, world.Warehouses[2].Kinah);
		Assert.Equal(1234, before.Details.ChargePoints);
		Assert.Equal((byte)3, before.Details.PackCount);
		world.Apply(decoder.Decode(typeof(SM_WAREHOUSE_UPDATE_ITEM), Body(w =>
		{
			w.Write(30); w.Write((byte)2); WriteString(w, "updated");
			WriteBlob(w, General(4_999_999_999L)); w.Write((ushort)22);
		})));
		var after = world.Warehouses[2].Items[30];
		Assert.Equal(4_999_999_999L, after.Count);
		Assert.Equal(before.Details, after.Details);
		Assert.Equal(before.EquipmentSlot, after.EquipmentSlot);
		Assert.Equal("updated", after.Description);
		world.Apply(decoder.Decode(typeof(SM_DELETE_WAREHOUSE_ITEM), Body(w =>
		{
			w.Write((byte)1); w.Write(30); w.Write((byte)20);
		})));
		Assert.Empty(world.Warehouses[1].Items);
		Assert.Single(world.Warehouses[2].Items);
		world.Apply(decoder.Decode(typeof(SM_DELETE_ITEM), Body(w => { w.Write(30); w.Write((byte)20); })));
		Assert.Single(world.Warehouses[2].Items);
	}

	private void ApplyInfo(BotWorldModel world, byte storage, bool first, byte expansion, params byte[][] rows) =>
		world.Apply(decoder.Decode(typeof(SM_WAREHOUSE_INFO), Body(w =>
		{
			w.Write(storage); w.Write(first); w.Write(expansion); w.Write((ushort)(storage == 1 && rows.Length > 0 ? 1 : 0));
			w.Write((ushort)rows.Length);
			foreach (var row in rows) w.Write(row);
		})));

	private static byte[] Row(int objectId, int itemId = 161000001, long count = 7) => Body(w =>
	{
		w.Write(objectId); w.Write(itemId); w.Write((byte)0); WriteString(w, "item");
		WriteBlob(w, [.. General(count), 0x0F, 0xD2, 0x04, 0, 0, 0x12, 3]);
		w.Write(ushort.MaxValue);
	});

	private static byte[] General(long count) => Body(w =>
	{
		w.Write((byte)0); w.Write((ushort)0x1A2B); w.Write(count); WriteString(w, "Daeva"); w.Write(new byte[21]);
	});

	private static void WriteString(BinaryWriter writer, string value) => writer.Write(Encoding.Unicode.GetBytes(value + "\0"));
	private static void WriteBlob(BinaryWriter writer, byte[] blob) { writer.Write((ushort)blob.Length); writer.Write(blob); }
	private static byte[] Body(Action<BinaryWriter> write)
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream);
		write(writer);
		return stream.ToArray();
	}
}
