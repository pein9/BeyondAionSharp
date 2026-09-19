using System.Buffers.Binary;
using Aion.Bots.Protocol;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotPassportPacketTests
{
	[Fact]
	public void RepeatedRewardIdsKeepTheirOwnArrivalTimeAndClaimStatus()
	{
		byte[] body = Convert.FromHexString("E4070C00100002005A0100000200000002000000785634125A010000020000000100000079563412");
		var decoder = new BotServerPacketDecoder(); var packet = decoder.Decode(typeof(SM_ATREIAN_PASSPORT), body);
		Assert.Equal((ushort)2020, packet.Get<ushort>("year")); Assert.Equal((ushort)12, packet.Get<ushort>("month")); Assert.Equal((ushort)16, packet.Get<ushort>("day"));
		Assert.Equal(new[] { new BotPassport(346, 2, 2, 0x12345678), new BotPassport(346, 2, 1, 0x12345679) }, packet.Get<BotPassport[]>("passports"));
		for (int length = 0; length < body.Length; length++)
			Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_ATREIAN_PASSPORT), body.AsSpan(0, length)));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_ATREIAN_PASSPORT), [.. body, 0]));
		byte[] invalidStatus = body.ToArray(); BinaryPrimitives.WriteInt32LittleEndian(invalidStatus.AsSpan(16), 4);
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_ATREIAN_PASSPORT), invalidStatus));
		byte[] invalidStamp = body.ToArray(); BinaryPrimitives.WriteInt32LittleEndian(invalidStamp.AsSpan(12), -1);
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_ATREIAN_PASSPORT), invalidStamp));
	}

	[Fact]
	public void ClaimWriterPreservesDistinctTimestampsForSameReward()
	{
		var packet = GameClientPackets.ClaimPassports(new BotPassportClaim(346, 0x12345678), new BotPassportClaim(346, 0x12345679));
		Assert.Equal(Convert.FromHexString("02005A010000785634125A01000079563412"), packet.Body);
		Assert.Throws<ArgumentOutOfRangeException>(() => GameClientPackets.ClaimPassports(new BotPassportClaim[32768]));
	}

	[Theory]
	[InlineData("0000010001000000")]
	[InlineData("E4070D0001000000")]
	[InlineData("E40702001E000000")]
	[InlineData("E407010000000000")]
	public void ImpossibleDatesFailInsteadOfBecomingValidAttendance(string hex) =>
		Assert.Throws<InvalidDataException>(() => new BotServerPacketDecoder().Decode(typeof(SM_ATREIAN_PASSPORT), Convert.FromHexString(hex)));
}
