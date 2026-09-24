using Aion.Bots.Protocol;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotMovePacketTests
{
	private readonly BotServerPacketDecoder decoder = new();

	// Java SM_MOVE.writeImpl: objectId, x, y, z, heading, mask; POSITION|MANUAL adds three floats, the
	// absolute move target with ABSOLUTE (NPC walks and chases) or a player's relative vector without it.
	private static byte[] Body(byte mask, params float[] extra)
	{
		var bytes = new List<byte>();
		bytes.AddRange(BitConverter.GetBytes(134554));
		foreach (float value in (float[])[755.17f, 1496.10f, 287.28f]) bytes.AddRange(BitConverter.GetBytes(value));
		bytes.Add(63);
		bytes.Add(mask);
		foreach (float value in extra) bytes.AddRange(BitConverter.GetBytes(value));
		return bytes.ToArray();
	}

	[Fact]
	public void NpcWalkCarriesItsAbsoluteTarget()
	{
		DecodedBotServerPacket move = decoder.Decode(typeof(SM_MOVE), Body(0xE0, 740.5f, 1497.25f, 291f));
		Assert.Equal(755.17f, move.Get<float>("x"));
		Assert.Equal(740.5f, move.Get<float>("targetX"));
		Assert.Equal(1497.25f, move.Get<float>("targetY"));
		Assert.Equal(291f, move.Get<float>("targetZ"));
	}

	[Fact]
	public void PlayerVectorIsNotATargetAndAStopHasNeither()
	{
		DecodedBotServerPacket manual = decoder.Decode(typeof(SM_MOVE), Body(0xC0, 1f, 0f, 0f));
		Assert.False(manual.Fields.ContainsKey("targetX"));
		Assert.Equal(1f, manual.Get<float>("vectorX"));
		DecodedBotServerPacket stop = decoder.Decode(typeof(SM_MOVE), Body(0x00));
		Assert.False(stop.Fields.ContainsKey("targetX"));
		Assert.False(stop.Fields.ContainsKey("vectorX"));
	}
}
