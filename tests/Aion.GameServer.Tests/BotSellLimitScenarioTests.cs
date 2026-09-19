using System.Text;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotSellLimitScenarioTests
{
	[Fact]
	public void RequiresTheSpecificRefusalAndExactRemainingAllowance()
	{
		var decoder = new BotServerPacketDecoder();
		var refused = decoder.Decode(typeof(SM_SYSTEM_MESSAGE), Refusal(1400938, "47"));
		SellLimitScenario.VerifyRefusal(refused, 47);
		Assert.Throws<InvalidDataException>(() => SellLimitScenario.VerifyRefusal(refused, 0));
		Assert.Throws<InvalidDataException>(() => SellLimitScenario.VerifyRefusal(decoder.Decode(typeof(SM_SYSTEM_MESSAGE), Refusal(1300013, "47")), 47));
	}
	[Fact]
	public void ExistingGoldIngotsCrossTheUnmodifiedOrdinaryAccountCap()
	{
		Assert.Equal(500000, VendorScenario.ReadBasePrice(SellLimitScenario.ItemId));
		Assert.Equal(53, SellLimitScenario.DailyCap / 100000);
		Assert.Equal(47, SellLimitScenario.DailyCap % 100000);
		Assert.True(SellLimitScenario.SetupCount > 55);
	}
	private static byte[] Refusal(int id, string remaining)
	{
		using var stream = new MemoryStream(); using var w = new BinaryWriter(stream);
		w.Write((byte)25); w.Write((byte)0); w.Write(0); w.Write(id); w.Write((byte)1);
		w.Write(Encoding.Unicode.GetBytes(remaining + "\0")); w.Write((byte)0); return stream.ToArray();
	}
}
