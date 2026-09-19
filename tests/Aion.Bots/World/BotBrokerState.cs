using Aion.Bots.Protocol;

namespace Aion.Bots.World;

public sealed record BotBrokerListing(int ObjectId, int ItemId, long TotalPrice, long Count, long? AverageUnitPrice,
	long? RepeatedCount, byte? DaysLeft, string? Seller, string Creator, BotItemEnchantment Enchantment,
	int PolishCharge, byte PackCount, bool SplittingAvailable);
public sealed record BotBrokerSettlement(int ItemId, long Proceeds, long Count, long RepeatedCount,
	int SettledAtMinutes, BotItemEnchantment Enchantment, string Creator);
public sealed record BotBrokerSearchPage(int TotalCount, ushort Page, IReadOnlyList<BotBrokerListing> Items);
public sealed record BotBrokerSettlementPage(int TotalCount, ushort Page, IReadOnlyList<BotBrokerSettlement> Items);

public sealed partial class BotWorldModel
{
	private readonly Dictionary<int, BotBrokerListing> brokerRegistered = [];
	public IReadOnlyDictionary<int, BotBrokerListing> BrokerRegistered => brokerRegistered;
	public BotBrokerSearchPage? BrokerSearch { get; private set; }
	public BotBrokerSettlementPage? BrokerSettlements { get; private set; }
	public long BrokerSettledKinah { get; private set; }
	public bool BrokerSettlementIcon { get; private set; }

	private void ApplyBroker(DecodedBotServerPacket packet)
	{
		switch (packet.Get<byte>("action"))
		{
			case 0:
				BrokerSearch = new(packet.Get<int>("totalCount"), packet.Get<ushort>("page"), packet.Get<BotBrokerListing[]>("items"));
				break;
			case 1:
				brokerRegistered.Clear();
				foreach (var item in packet.Get<BotBrokerListing[]>("items")) brokerRegistered[item.ObjectId] = item;
				break;
			case 3 when packet.Get<byte>("message") == 0:
				foreach (var item in packet.Get<BotBrokerListing[]>("items")) brokerRegistered[item.ObjectId] = item;
				break;
			case 4 when packet.Get<byte>("result") == 0:
				brokerRegistered.Remove(packet.Get<int>("objectId"));
				break;
			case 5:
				BrokerSettledKinah = packet.Get<long>("settledKinah");
				if (packet.Get<bool>("iconOnly")) BrokerSettlementIcon = true;
				else BrokerSettlements = new(packet.Get<int>("totalCount"), packet.Get<ushort>("page"), packet.Get<BotBrokerSettlement[]>("settlements"));
				break;
			case 6: BrokerSettlementIcon = false; break;
		}
	}
}
