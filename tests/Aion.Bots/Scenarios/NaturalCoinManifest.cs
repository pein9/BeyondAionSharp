using Aion.Bots.Scenarios.Classes;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Templates.Tradelist;

namespace Aion.Bots.Scenarios;

/// <summary>One piece of a coin manifest: what it is, the slot it competes for and what it costs in the tier's coin.</summary>
public sealed record NaturalCoinPiece(int ItemId, string Group, string Slot, int ItemLevel, int Cost);

/// <summary>
/// NR-38: what one coin vendor sells a class at one tier: a piece for each of the five body slots in the armor it
/// prefers, the weapon of its first weapon group the vendor has, and a shield for a class that holds one. Whether a
/// piece is bought is the leg's question: it buys what beats the piece worn.
/// </summary>
public sealed record NaturalCoinManifest(int VendorNpcId, int CoinItemId, int ItemLevel, IReadOnlyList<NaturalCoinPiece> Armor,
	NaturalCoinPiece? Weapon, NaturalCoinPiece? Shield)
{
	public IEnumerable<NaturalCoinPiece> Pieces => Armor.Concat(new[] { Weapon, Shield }.OfType<NaturalCoinPiece>());
}

/// <summary>
/// NR-38: the coin manifests by class, read from the shipped data. A coin vendor is a reward shop (Java
/// TradeService.performBuyFromShop, the REWARD case): it sells what its trade tabs' goods lists hold, for the coin and
/// count each item's template names as its acquisition, and asks nothing of the buyer's class or level. The two coin
/// tiers of the route each have two vendors side by side: one with chain and robe armor and the casters' weapons, one
/// with plate, leather and chain armor and the other weapons.
/// </summary>
public static class NaturalCoinManifests
{
	public static readonly IReadOnlyList<string> BodySlots = ["TORSO", "GLOVE", "SHOULDER", "PANTS", "SHOES"];

	/// <summary>Vendors stand this near each other at a tier (Lohaban and Lateni 4 m apart, Vebna and Nott 14 m).</summary>
	public const float BesideMeters = 30f;

	/// <summary>The item ids a vendor's trade tabs hold.</summary>
	public static IReadOnlyList<int> Sold(StaticData data, int vendorNpcId) =>
		(data.TradeListDataDh.GetTradeListTemplate(vendorNpcId)?.GetTradeTablist() ?? [])
		.SelectMany(tab => data.GoodsListDataDh.GetGoodsListById(tab.GetId())?.GetItemIdList() ?? [])
		.Distinct().ToArray();

	/// <summary>
	/// The leg's vendor and every other reward shop that sells for the same coin and stands beside it on the map.
	/// </summary>
	public static IReadOnlyList<int> VendorsBeside(StaticData data, int mapId, int vendorNpcId, int coinItemId)
	{
		(float X, float Y)[] Spots(int npc) => data.SpawnsDh.GetSpawnsByWorldId(mapId).Where(group => group.GetNpcId() == npc)
			.SelectMany(group => group.GetSpawnTemplates()).Select(spot => (spot.GetX(), spot.GetY())).ToArray();
		(float X, float Y)[] own = Spots(vendorNpcId);
		bool SellsForCoin(int npc) => Sold(data, npc).Any(id => data.ItemDataDh.GetItemTemplate(id)?.acquisition?.GetItemId() == coinItemId);
		return data.TradeListDataDh.GetTradeListTemplate()
			.Where(entry => entry.Key != vendorNpcId && entry.Value.GetTradeNpcType() == TradeNpcType.REWARD && SellsForCoin(entry.Key) &&
				Spots(entry.Key).Any(spot => own.Any(mine => MathF.Sqrt((spot.X - mine.X) * (spot.X - mine.X) + (spot.Y - mine.Y) * (spot.Y - mine.Y)) <= BesideMeters)))
			.Select(entry => entry.Key).Order().Prepend(vendorNpcId).ToArray();
	}

	/// <summary>What one vendor sells the class at a tier. In a slot the class's gear rules rank the pieces; of two they
	/// rank alike, the better quality, then the dearer.</summary>
	/// <param name="item">The shipped facts of an item id (<see cref="NaturalIshalgenInventoryPolicy.Item"/>).</param>
	public static NaturalCoinManifest At(StaticData data, int vendorNpcId, int coinItemId, int itemLevel, NaturalGearRules rules,
		Func<int, NaturalItem> item)
	{
		(NaturalItem Item, int Cost)[] sold = [.. Sold(data, vendorNpcId)
			.Select(id => (Id: id, Template: data.ItemDataDh.GetItemTemplate(id)))
			.Where(entry => entry.Template?.acquisition is { } cost && cost.GetItemId() == coinItemId && entry.Template.GetLevel() == itemLevel)
			.Select(entry => (Item: item(entry.Id), Cost: entry.Template!.acquisition.GetItemCount()))
			.Where(entry => rules.Slot(entry.Item) != null && rules.UsableNowOrLater(entry.Item, itemLevel))];
		NaturalCoinPiece? Best(string slot) => sold.Where(entry => rules.Slot(entry.Item) == slot)
			.OrderByDescending(entry => rules.Score(entry.Item)).ThenByDescending(entry => entry.Item.Quality)
			.ThenByDescending(entry => entry.Cost).ThenBy(entry => entry.Item.Id)
			.Select(entry => new NaturalCoinPiece(entry.Item.Id, entry.Item.Group, slot, entry.Item.ItemLevel, entry.Cost)).FirstOrDefault();
		return new(vendorNpcId, coinItemId, itemLevel, [.. BodySlots.Select(Best).OfType<NaturalCoinPiece>()], Best("WEAPON"),
			rules.OffHand == NaturalOffHand.Shield ? Best("SHIELD") : null);
	}

	/// <summary>The class's manifest at a tier: of the vendors that stand there, the one whose weapon the class's gear
	/// rules rank highest (a sword before a mace for a class that names them so); of two alike, the one whose armor they
	/// rank highest; of two alike again, the first named.</summary>
	public static NaturalCoinManifest For(StaticData data, IReadOnlyList<int> vendorNpcIds, int coinItemId, int itemLevel,
		NaturalGearRules rules, Func<int, NaturalItem> item) => vendorNpcIds
		.Select((npc, index) => (Manifest: At(data, npc, coinItemId, itemLevel, rules, item), index))
		.OrderByDescending(entry => entry.Manifest.Weapon is { } weapon ? rules.Score(item(weapon.ItemId)) : long.MinValue)
		.ThenByDescending(entry => entry.Manifest.Armor.Sum(piece => rules.Score(item(piece.ItemId))))
		.ThenBy(entry => entry.index).First().Manifest;
}
