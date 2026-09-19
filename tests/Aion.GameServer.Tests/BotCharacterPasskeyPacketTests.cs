using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotCharacterPasskeyPacketTests
{
	[Theory]
	[InlineData("123456")]
	[InlineData("1234567")]
	[InlineData("12345678")]
	public void ScenarioPasskeyPreservesDigitsAndFixedPadding(string digits)
	{
		byte[] wire = CharacterPasskeyScenario.WireValue(digits);
		Assert.Equal(48, wire.Length);
		Assert.Equal(digits.PadRight(24, '\0'), System.Text.Encoding.Unicode.GetString(wire));
	}

	[Theory]
	[InlineData("12345")]
	[InlineData("123456789")]
	[InlineData("１２３４５６")]
	[InlineData("12 456")]
	public void ScenarioRejectsInvalidPasskeyDigits(string digits) =>
		Assert.Throws<ArgumentException>(() => CharacterPasskeyScenario.WireValue(digits));

	[Fact]
	public void ScenarioResultOracleRequiresExactOperationCountAndThreshold()
	{
		var decoder = new BotServerPacketDecoder();
		byte[] body = [2, 3, 0, 1, 5, 0, 0, 0, 5, 0, 0, 0];
		var packet = decoder.Decode(typeof(SM_CHARACTER_SELECT), body);
		CharacterPasskeyScenario.VerifyResult(packet, 3, 5);
		Assert.Throws<InvalidDataException>(() => CharacterPasskeyScenario.VerifyResult(packet, 2, 5));
		Assert.Throws<InvalidDataException>(() => CharacterPasskeyScenario.VerifyResult(packet, 3, 4));
		body[8] = 6;
		Assert.Throws<InvalidDataException>(() => CharacterPasskeyScenario.VerifyResult(decoder.Decode(typeof(SM_CHARACTER_SELECT), body), 3, 5));
		Assert.Throws<InvalidDataException>(() => CharacterPasskeyScenario.VerifyWindow(packet, 1));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(12)]
	[InlineData(47)]
	[InlineData(49)]
	public void RejectsNonWireSizedPasskeyFields(int length)
	{
		Assert.Throws<ArgumentException>(() => GameClientPackets.SetCharacterPasskey(new byte[length]));
		Assert.Throws<ArgumentException>(() => GameClientPackets.SubmitCharacterPasskey(new byte[length]));
		Assert.Throws<ArgumentException>(() => GameClientPackets.UpdateCharacterPasskey(new byte[48], new byte[length]));
		Assert.Throws<ArgumentException>(() => GameClientPackets.UpdateCharacterPasskey(new byte[length], new byte[48]));
	}

	[Fact]
	public void WritesFixedFieldsWithoutAddingAStringTerminator()
	{
		byte[] value = Enumerable.Range(0, 48).Select(i => (byte)i).ToArray();
		Assert.Equal(new byte[] { 0, 0 }.Concat(value), GameClientPackets.SetCharacterPasskey(value).Body);
		Assert.Equal(new byte[] { 3, 0 }.Concat(value), GameClientPackets.SubmitCharacterPasskey(value).Body);
		Assert.Equal(new byte[] { 2, 0 }.Concat(value).Concat(value), GameClientPackets.UpdateCharacterPasskey(value, value).Body);
	}

	[Fact]
	public void DecodesThresholdAndBeyondWithoutInventingSuccessfulLockout()
	{
		var decoder = new BotServerPacketDecoder();
		foreach (byte count in new byte[] { 0, 1, 5, 6 })
		{
			var packet = decoder.Decode(typeof(SM_CHARACTER_SELECT), [2, 3, 0, (byte)(count > 0 ? 1 : 0), count, 0, 0, 0, 5, 0, 0, 0]);
			Assert.Equal((int)count, packet.Get<int>("wrongCount")); Assert.Equal(5, packet.Get<int>("maxWrongCount"));
			Assert.Equal(count == 0, packet.Get<bool>("accepted"));
		}
	}

	[Theory]
	[InlineData("03")]
	[InlineData("020100000000000005000000")]
	[InlineData("020300000100000005000000")]
	[InlineData("02030001FFFFFFFF05000000")]
	[InlineData("020300000000000000000000")]
	public void RejectsInvalidWindowsResultsAndCounts(string hex) =>
		Assert.Throws<InvalidDataException>(() => new BotServerPacketDecoder().Decode(typeof(SM_CHARACTER_SELECT), Convert.FromHexString(hex)));
}
