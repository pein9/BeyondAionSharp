using System.Text;
using System.Text.Json;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotMailPacketTests
{
	private readonly BotServerPacketDecoder decoder = new();

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void MailReadRespectsAbsentItemPaddingAndLengthPrefixedItemBlob(bool attached)
	{
		// Audited SM_MAIL_SERVICE.java service 3 layout. This is a hand-built wire contract, not a Java fixture.
		string root = Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!;
		using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "..", "golden", "packets", "SM_INVENTORY_UPDATE_ITEM.json")));
		var inventory = decoder.Decode(typeof(SM_INVENTORY_UPDATE_ITEM), Convert.FromHexString(fixture.RootElement.GetProperty("cases")[0].GetProperty("payloadHex").GetString()!));
		byte[] blob = inventory.Get<byte[]>("blob");
		byte[] body = Body(w =>
		{
			w.Write((byte)3); w.Write(100); w.Write(0x10001); w.Write(0); w.Write(200); w.Write(100);
			S(w, "Sender"); S(w, "Title"); S(w, "Message");
			if (attached)
			{
				w.Write(300); w.Write(MailScenario.ItemId); w.Write(1); w.Write(0); S(w, "702016");
				w.Write((ushort)blob.Length); w.Write(blob);
			}
			else w.Write(new byte[20]);
			w.Write(125); w.Write(0); w.Write((byte)0); w.Write(123456); w.Write((byte)1);
		});
		var packet = decoder.Decode(typeof(SM_MAIL_SERVICE), body);
		Assert.Equal(100, packet.Get<int>("recipientId"));
		Assert.Equal(200, packet.Get<int>("letterId"));
		Assert.Equal("Sender", packet.Get<string>("sender"));
		Assert.Equal("Title", packet.Get<string>("title"));
		Assert.Equal("Message", packet.Get<string>("message"));
		Assert.Equal(attached ? 300 : 0, packet.Get<int>("itemObjectId"));
		Assert.Equal(attached ? 7L : 0L, packet.Get<long>("itemCount"));
		Assert.Equal(125, packet.Get<int>("kinah"));
		Assert.Equal(123456, packet.Get<int>("timestampSeconds"));
		Assert.Equal((byte)1, packet.Get<byte>("letterType"));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_MAIL_SERVICE), body[..^1]));
	}

	[Fact]
	public void MailListPreservesSignedChunkBoundaryAndLongKinah()
	{
		var packet = decoder.Decode(typeof(SM_MAIL_SERVICE), Body(w =>
		{
			w.Write((byte)2); w.Write(100); w.Write((byte)0); w.Write((short)-1);
			w.Write(200); S(w, "Sender"); S(w, "Title"); w.Write((byte)1);
			w.Write(300); w.Write(MailScenario.ItemId); w.Write(5_000_000_000L); w.Write((byte)0);
		}));
		Assert.True(packet.Get<bool>("lastPacket"));
		var row = Assert.Single(packet.Get<List<IReadOnlyDictionary<string, object?>>>("letters"));
		Assert.Equal(200, row["letterId"]); Assert.Equal(300, row["itemObjectId"]);
		Assert.Equal(5_000_000_000L, row["kinah"]); Assert.Equal(true, row["isRead"]);
	}

	[Fact]
	public void MailStateAttachmentAndDeleteHaveExplicitLayouts()
	{
		var state = decoder.Decode(typeof(SM_MAIL_SERVICE), [0, 4, 0, 3, 0, 2, 0, 1, 0]);
		Assert.Equal((ushort)4, state.Get<ushort>("totalCount"));
		Assert.Equal((ushort)3, state.Get<ushort>("unreadCount"));
		Assert.Equal((ushort)2, state.Get<ushort>("expressCount"));
		Assert.Equal((ushort)1, state.Get<ushort>("blackCloudCount"));
		var claim = decoder.Decode(typeof(SM_MAIL_SERVICE), [5, 100, 0, 0, 0, 1, 1]);
		Assert.Equal(100, claim.Get<int>("letterId"));
		Assert.Equal((byte)1, claim.Get<byte>("attachmentType"));
		Assert.Equal((byte)1, claim.Get<byte>("success"));
		var delete = decoder.Decode(typeof(SM_MAIL_SERVICE), Body(w =>
		{
			w.Write((byte)6); w.Write(0); w.Write(0); w.Write((ushort)2); w.Write(100); w.Write(101);
		}));
		Assert.Equal([100, 101], delete.Get<int[]>("deletedIds"));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_MAIL_SERVICE), [5, 100, 0, 0, 0, 1]));
	}

	[Fact]
	public void MailServiceFeeTruncatesEveryObservedPercentage()
	{
		Assert.Equal(144, new BotVendorPrices(117, 109, 113).ServicePrice(101));
		Assert.Equal(101, new BotVendorPrices(100, 100, 100).ServicePrice(101));
	}
	private static byte[] Body(Action<BinaryWriter> write)
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream, Encoding.Unicode, leaveOpen: true);
		write(writer);
		return stream.ToArray();
	}
	private static void S(BinaryWriter writer, string value)
	{
		writer.Write(value.ToCharArray()); writer.Write((ushort)0);
	}
}
