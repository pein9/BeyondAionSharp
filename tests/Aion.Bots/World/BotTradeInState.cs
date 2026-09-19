using Aion.Bots.Protocol;

namespace Aion.Bots.World;

public sealed record BotTradeInWindow(int TargetObjectId, byte NpcType, int BuyPriceModifier,
	int PriceRate, IReadOnlyList<int> Tabs);

public sealed partial class BotWorldModel
{
	public BotTradeInWindow? TradeIn { get; private set; }

	private void ApplyTradeIn(DecodedBotServerPacket packet)
	{
		TradeIn = packet.Get<bool>("hasTradeIn")
			? new(packet.Get<int>("targetObjectId"), packet.Get<byte>("tradeNpcType"),
				packet.Get<int>("buyPriceModifier"), packet.Get<int>("priceRate"), packet.Get<int[]>("tabs"))
			: null;
	}
}
