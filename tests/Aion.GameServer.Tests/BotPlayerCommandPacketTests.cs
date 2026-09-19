using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Tests;

public sealed class BotPlayerCommandPacketTests
{
	[Fact]
	public void CommandScenarioCoversEveryShippedPlayerHandler()
	{
		var handlers = typeof(PlayerCommand).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(PlayerCommand)) && !t.IsAbstract).ToArray();
		Assert.Equal(16, handlers.Length);
		Assert.Equal(handlers.Select(t => t.Name.ToLowerInvariant()).Order(), PlayerCommandScenario.Aliases.Order());
	}

	[Fact]
	public void AppearanceDecoderPreservesSparseSlotsDyeGodstoneAndEnchant()
		=> AssertAppearanceWireContract();

	// Audited against SM_UPDATE_PLAYER_APPEARANCE and AbstractPlayerInfoPacket at Java ce54b7931.
	// No Java-generated fixture exists for this packet; do not label these hand-authored bytes as one.
	internal static void AssertAppearanceWireContract()
	{
		using var stream = new MemoryStream(); using var w = new BinaryWriter(stream);
		w.Write(123); w.Write(0x101u);
		w.Write(100000002); w.Write(168000001); w.Write(new byte[] { 1, 0x12, 0x34, 0x56 }); w.Write((ushort)15); w.Write((ushort)0);
		w.Write(110000001); w.Write(0); w.Write(0); w.Write((ushort)0); w.Write((ushort)0);
		var decoder = new BotServerPacketDecoder(); byte[] body = stream.ToArray();
		var packet = decoder.Decode(typeof(SM_UPDATE_PLAYER_APPEARANCE), body);
		Assert.Equal(123, packet.Get<int>("playerObjectId")); Assert.Equal(0x101u, packet.Get<uint>("slotMask"));
		Assert.Equal(new[] { new BotEquipmentAppearance(1, 100000002, 168000001, 0x123456, 15), new BotEquipmentAppearance(256, 110000001, 0, null, 0) }, packet.Get<BotEquipmentAppearance[]>("equipment"));
		for (int length = 0; length < body.Length; length++) Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_UPDATE_PLAYER_APPEARANCE), body[..length]));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_UPDATE_PLAYER_APPEARANCE), [..body, 0]));
		foreach (int index in new[] { 16, 22, 32, 38 })
		{
			byte[] malformed = (byte[])body.Clone(); malformed[index] = 2;
			Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_UPDATE_PLAYER_APPEARANCE), malformed));
		}
		Assert.Empty(decoder.Decode(typeof(SM_UPDATE_PLAYER_APPEARANCE), new byte[8]).Get<BotEquipmentAppearance[]>("equipment"));
	}
}
