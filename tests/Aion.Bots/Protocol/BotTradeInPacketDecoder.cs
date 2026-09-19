namespace Aion.Bots.Protocol;

public sealed partial class BotServerPacketDecoder
{
	private static IReadOnlyDictionary<string, object?> DecodeTradeInList(ReadOnlySpan<byte> body)
	{
		// SM_TRADE_IN_LIST writes no body when the template is null or has no tabs.
		if (body.IsEmpty) return Fields(("hasTradeIn", false));
		var r = new PacketBodyReader(body);
		var fields = Fields(("hasTradeIn", true), ("targetObjectId", r.ReadInt32()),
			("tradeNpcType", r.ReadByte()), ("buyPriceModifier", r.ReadInt32()), ("priceRate", r.ReadInt32()));
		var tabs = new int[r.ReadUInt16()];
		for (int i = 0; i < tabs.Length; i++) tabs[i] = r.ReadInt32();
		fields["tabs"] = tabs;
		if (r.Remaining != 0) throw new InvalidDataException("Trailing trade-in list bytes.");
		return fields;
	}
}
