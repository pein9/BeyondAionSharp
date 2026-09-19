using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

public sealed class TradeInVendorDataContractTests
{
	[Fact]
	public async Task E11UsesExistingTradeInRecipeAndLimitedStockAtNormallySpawnedVendors()
	{
		var data = (await RealStaticData.LoadAsync()).StaticData;
		var trade = data.TradeListDataDh.GetTradeInListTemplate(TradeInVendorScenario.TradeInNpcId);
		Assert.Equal(TradeInVendorScenario.TradeInTab, Assert.Single(trade.GetTradeTablist()).GetId());
		Assert.Equal(TradeInVendorScenario.ChestId, Assert.Single(data.GoodsListDataDh.GetGoodsInListById(TradeInVendorScenario.TradeInTab).GetItemIdList()));
		var recipe = data.ItemDataDh.GetItemTemplate(TradeInVendorScenario.ChestId);
		Assert.Equal(100, recipe.GetMaxStackCount());
		var materials = recipe.GetTradeinList().GetTradeinItem();
		Assert.NotNull(materials);
		Assert.Equal(new[] { (TradeInVendorScenario.InsigniaId, 9800L), (TradeInVendorScenario.CourageId, 400L) },
			materials.Select(i => (i.GetId(), i.GetPrice())));
		Assert.Null(recipe.GetAcquisition());
		Assert.True(data.NpcDataDh.GetNpcTemplate(TradeInVendorScenario.TradeInNpcId).SupportsAction(DialogAction.TRADE_IN));
		Assert.True(data.NpcDataDh.GetNpcTemplate(TradeInVendorScenario.LimitedNpcId).SupportsAction(DialogAction.BUY));
		var vendor = data.TradeListDataDh.GetTradeListTemplate(TradeInVendorScenario.LimitedNpcId);
		Assert.Contains(vendor.GetTradeTablist(), t => t.GetId() == TradeInVendorScenario.LimitedTab);
		Assert.Equal(TradeInVendorScenario.LimitedSellRate, vendor.GetSellPriceRate());
		var limited = Assert.Single(data.GoodsListDataDh.GetGoodsListById(TradeInVendorScenario.LimitedTab).GetLimitedItems(), i => i.GetItemId() == TradeInVendorScenario.FluxId);
		Assert.Equal(1, limited.GetBuyLimit()); Assert.Equal(2, limited.GetDefaultSellLimit());
		Assert.Equal("0 0 0,10,12,14,18,22 ? * *", limited.GetSalesTime());
		Assert.Equal(VendorScenario.ReadBasePrice(TradeInVendorScenario.FluxId), data.ItemDataDh.GetItemTemplate(TradeInVendorScenario.FluxId).GetPrice());
		AssertSpawn("110070000_Kaisinel_Academy.xml", TradeInVendorScenario.TradeInNpcId, TradeInVendorScenario.TradeInPosition);
		AssertSpawn("110010000_Sanctum.xml", TradeInVendorScenario.LimitedNpcId, TradeInVendorScenario.LimitedPosition);
	}
	private static void AssertSpawn(string filename, int npcId, BotPosition position)
	{
		var spawns = XDocument.Load(Path.Combine(RealStaticData.RepoRoot(), "game-server/data/static_data/spawns/Npcs", filename));
		var spawn = Assert.Single(spawns.Descendants("spawn"), row => (int?)row.Attribute("npc_id") == npcId);
		var spot = Assert.Single(spawn.Elements("spot"));
		Assert.Equal(position.X, (float)spot.Attribute("x")!);
		Assert.Equal(position.Y, (float)spot.Attribute("y")!);
		Assert.Equal(position.Z, (float)spot.Attribute("z")!);
	}
}
