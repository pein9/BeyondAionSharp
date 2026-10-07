using System.Text.RegularExpressions;
using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.World;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

/// <summary>
/// CP-24: the restock table of the class profile. The Priest line's table must answer as the potion policy's two
/// functions did, which the journey's inventory maintenance asked before; a floor must hold; no table may open the
/// bandage list.
/// </summary>
public sealed class NaturalRestockRulesTests
{
	private static readonly int[] LifeItems = [162000002, 162000052, 162000053, 162000003, 162000004, 162000006];

	[Fact]
	public void ThePriestLinesTableIsOneLineOfMinorLifeElixirs()
	{
		foreach (NaturalClassProfile profile in new[] { NaturalPriestProfile.Priest, NaturalPriestProfile.Cleric })
		{
			NaturalRestockRules rules = profile.Restock;
			Assert.Equal(0, rules.KinahFloor);
			NaturalRestockLine line = Assert.Single(rules.Lines);
			Assert.Equal((162000052, 5, 12, 721), (line.ItemId, line.AtOrBelow, line.Target, line.TradeListId));
			Assert.Equal(LifeItems.Order(), line.CountedWith.Order());
		}
		Assert.Same(NaturalPriestProfile.Priest.Restock, NaturalPriestProfile.Cleric.Restock);
	}

	[Fact]
	public void ThePriestLinesTableReproducesNeedsRestockAndAffordablePurchaseCount()
	{
		NaturalRestockRules rules = NaturalPriestProfile.Priest.Restock;
		NaturalRestockLine line = rules.Lines.Single();
		// Stock: every owned life potion and elixir counts, alone and mixed, and nothing else does.
		int states = 0;
		foreach (int item in LifeItems)
		foreach (int own in new[] { 0, 1, 4, 5, 6, 11, 12, 30 })
		foreach (int other in new[] { 0, 1, 3 })
		{
			BotInventoryItem[] bag = [Item(1, item, own), Item(2, LifeItems[(Array.IndexOf(LifeItems, item) + 1) % LifeItems.Length], other),
				Item(3, 162000007, 100), Item(4, 164000067, 30), Item(5, BotWorldModel.KinahItemId, 5000)];
			Assert.Equal(NaturalIshalgenPotionPolicy.TotalHealingCount(bag), rules.Stock(line, bag));
			Assert.Equal(NaturalIshalgenPotionPolicy.NeedsRestock(bag), rules.Needed(bag) != null);
			if (rules.Needed(bag) is { } needed) Assert.Same(line, needed);
			states++;
		}
		Assert.Equal(6 * 8 * 3, states);
		// The count to buy, over a grid of stock, Kinah and displayed price.
		foreach (long stock in new long[] { 0, 1, 4, 5, 6, 11, 12, 13 })
		foreach (long kinah in new long[] { 0, 1, 249, 250, 499, 500, 2203, 4093, 40342 })
		foreach (long price in new long[] { -1, 0, 1, 250, 500, 501, 5000 })
		{
			Assert.Equal(NaturalIshalgenPotionPolicy.AffordablePurchaseCount(stock, kinah, price), rules.PurchaseCount(line, stock, kinah, price));
			Assert.Equal(kinah, rules.Spendable(kinah));
		}
	}

	[Fact]
	public void ATableWithAFloorNeverSpendsBelowIt()
	{
		NaturalRestockLine line = new(162000052, AtOrBelow: 5, Target: 12, TradeListId: 721, CountedWith: [162000052]);
		var floored = new NaturalRestockRules([line], kinahFloor: 1000);
		foreach (long stock in new long[] { 0, 3, 5 })
		foreach (long kinah in new long[] { 0, 999, 1000, 1249, 1250, 1499, 1500, 4093, 100_000 })
		foreach (long price in new long[] { 1, 250, 500 })
		{
			long count = floored.PurchaseCount(line, stock, kinah, price);
			Assert.True(count >= 0 && count <= line.Target - stock);
			// What is left after the purchase is never below the floor, unless the purse was below it already and nothing was bought.
			Assert.True(count == 0 || kinah - count * price >= floored.KinahFloor, $"{stock}/{kinah}/{price}: bought {count}.");
			if (kinah - floored.KinahFloor >= price) Assert.True(count > 0);
			// One more would break the floor or overshoot the target.
			if (count > 0 && count < line.Target - stock) Assert.True(kinah - (count + 1) * price < floored.KinahFloor);
		}
		Assert.Equal(0, floored.PurchaseCount(line, 0, 1249, 250));
		Assert.Equal(1, floored.PurchaseCount(line, 0, 1250, 250));
		Assert.Equal(12, floored.PurchaseCount(line, 0, 100_000, 250));
		Assert.Equal(0, floored.PurchaseCount(line, 6, 100_000, 250));
		Assert.Throws<ArgumentOutOfRangeException>(() => new NaturalRestockRules([line], kinahFloor: -1));
	}

	[Fact]
	public void TheFirstNeededLineOfATableIsBought()
	{
		NaturalRestockLine life = new(162000052, 5, 12, 721, [162000052, 162000002]), mana = new(162000057, 3, 8, 721, [162000057]);
		var rules = new NaturalRestockRules([life, mana]);
		Assert.Same(life, rules.Needed([Item(1, 162000002, 5), Item(2, 162000057, 0)]));
		Assert.Same(mana, rules.Needed([Item(1, 162000002, 6), Item(2, 162000057, 3)]));
		Assert.Null(rules.Needed([Item(1, 162000052, 6), Item(2, 162000057, 4)]));
		Assert.Null(new NaturalRestockRules([]).Needed([]));
	}

	[Fact]
	public void NoTableNamesTheBandageList()
	{
		// Both Ishalgen restock vendors carry lists 264 and 721; 721 sells the elixirs and 264 is the other one (CP-Q11).
		string root = RealStaticData.RepoRoot();
		string trade = File.ReadAllText(Path.Combine(root, "game-server/data/static_data/npc_trade_list.xml"));
		foreach ((int npcId, _) in NaturalIshalgenPotionPolicy.Vendors)
		{
			Match vendor = Regex.Match(trade, $"<tradelist_template npc_id=\"{npcId}\"[^>]*>(.*?)</tradelist_template>", RegexOptions.Singleline);
			Assert.True(vendor.Success, $"Vendor {npcId} has no trade list.");
			Assert.Equal(new[] { "264", "721" }, Regex.Matches(vendor.Groups[1].Value, "tradelist id=\"(\\d+)\"").Select(match => match.Groups[1].Value));
		}
		string goods = File.ReadAllText(Path.Combine(root, "game-server/data/static_data/goodslists/goodslists.xml"));
		string[] Sold(int list) => Regex.Matches(Regex.Match(goods, $"<list id=\"{list}\">(.*?)</list>", RegexOptions.Singleline).Groups[1].Value,
			"id=\"(\\d+)\"").Select(match => match.Groups[1].Value).ToArray();
		Assert.Contains("162000052", Sold(721));
		Assert.DoesNotContain("162000052", Sold(NaturalRestockRules.BandageTradeListId));
		Assert.Equal(264, NaturalRestockRules.BandageTradeListId);
		foreach (NaturalClassProfile profile in new[] { NaturalPriestProfile.Priest, NaturalPriestProfile.Cleric })
			Assert.DoesNotContain(profile.Restock.Lines, line => line.TradeListId == NaturalRestockRules.BandageTradeListId);
		// A table that names it cannot be built at all.
		Assert.Throws<ArgumentException>(() => new NaturalRestockRules([new(169000003, 5, 12, 264, [169000003])]));
		// Nor one with nothing to buy or nothing to count.
		Assert.Throws<ArgumentException>(() => new NaturalRestockRules([new(162000052, 5, 5, 721, [162000052])]));
		Assert.Throws<ArgumentException>(() => new NaturalRestockRules([new(162000052, 5, 12, 721, [162000002])]));
	}

	private static BotInventoryItem Item(int objectId, int itemId, long count) => new(objectId, itemId, "", count, 4, "", 0, false);
}
