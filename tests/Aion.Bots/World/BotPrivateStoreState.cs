using Aion.Bots.Protocol;
using Aion.GameServer.Model;

namespace Aion.Bots.World;

public sealed record BotPrivateStoreListing(int ObjectId, int ItemId, ushort Count, long UnitPrice,
	BotItemGeneralInfo General, BotItemDetails Details);

public sealed partial class BotWorldModel
{
	private readonly HashSet<int> openPrivateStores = [];
	private readonly Dictionary<int, string> privateStoreNames = [];
	private readonly Dictionary<int, IReadOnlyList<BotPrivateStoreListing>> privateStoreListings = [];
	public IReadOnlySet<int> OpenPrivateStores => openPrivateStores;
	public IReadOnlyDictionary<int, string> PrivateStoreNames => privateStoreNames;
	public IReadOnlyDictionary<int, IReadOnlyList<BotPrivateStoreListing>> PrivateStoreListings => privateStoreListings;

	private void ApplyPrivateStore(DecodedBotServerPacket packet)
	{
		if (packet.Get<bool>("hasStore"))
			privateStoreListings[packet.Get<int>("sellerObjectId")] = packet.Get<BotPrivateStoreListing[]>("items");
	}
	private void ApplyPrivateStoreEmotion(DecodedBotServerPacket packet)
	{
		int id = packet.Get<int>("senderObjectId");
		if (packet.Get<byte>("emotionType") == (byte)EmotionType.OPEN_PRIVATESHOP) openPrivateStores.Add(id);
		else if (packet.Get<byte>("emotionType") == (byte)EmotionType.CLOSE_PRIVATESHOP) ForgetPrivateStore(id);
	}
	private void ForgetPrivateStore(int id)
	{
		openPrivateStores.Remove(id); privateStoreNames.Remove(id); privateStoreListings.Remove(id);
	}
}
