using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

public sealed class MorphRecipeMerchantSpawnPortTests
{
    [Theory]
    [InlineData(110010000, 799060, 1887.0967f, 1524.9606f, 590.1345f, 60)]
    [InlineData(120010000, 799373, 1186.2657f, 1552.545f, 213.96336f, 6)]
    [InlineData(110010000, 832773, 1887.0967f, 1526.392f, 590.1345f, 60)]
    [InlineData(120010000, 832787, 1186.7118f, 1551.3257f, 213.96336f, 6)]
    public void CustomFileLoadsNewAndExistingMerchantsAtExactJavaPositions(
        int worldId, int npcId, float x, float y, float z, int heading)
    {
        // Exact entries from merged Java 4990b35267882bb6ae85769ba4c8b9750430565a.
        using var file = File.OpenRead(Path.Combine(RealStaticData.RepoRoot(),
            "game-server/data/static_data/spawns/Npcs/Custom/Morph_Recipe_Merchants.xml"));
        var data = (SpawnsData)new XmlSerializer(typeof(SpawnsData)).Deserialize(file)!;
        Assert.Equal(new[] { 110010000, 120010000 }, data.Templates!.Select(map => map.GetMapId()));
        data.AfterUnmarshal(null);
        Assert.Null(data.Templates);
        AssertSpawn(data, worldId, npcId, x, y, z, heading);
    }

    [Theory]
    [InlineData(110010000, 799060, 1887.0967f, 1524.9606f, 590.1345f, 60, Race.ELYOS, 788, 5033)]
    [InlineData(120010000, 799373, 1186.2657f, 1552.545f, 213.96336f, 6, Race.ASMODIANS, 789, 5034)]
    public async Task MergedProductionDataHasSpawnNpcAndExistingRecipeGoods(
        int worldId, int npcId, float x, float y, float z, int heading, Race race, int factionTab, int morphTab)
    {
        var data = (await RealStaticData.LoadAsync()).StaticData;
        AssertSpawn(data.SpawnsDh, worldId, npcId, x, y, z, heading);
        var npc = data.NpcDataDh.GetNpcTemplate(npcId);
        Assert.NotNull(npc); Assert.Equal(race, npc.GetRace()); Assert.True(npc.SupportsAction(DialogAction.BUY));
        var trade = data.TradeListDataDh.GetTradeListTemplate(npcId);
        Assert.NotNull(trade); Assert.Equal(new[] { 1619, factionTab, morphTab }, trade.GetTradeTabIds());
        foreach (int tab in trade.GetTradeTabIds())
        {
            var goods = data.GoodsListDataDh.GetGoodsListById(tab);
            Assert.NotNull(goods); Assert.NotEmpty(goods.GetItemIdList());
            foreach (int id in goods.GetItemIdList()) Assert.NotNull(data.ItemDataDh.GetItemTemplate(id));
        }
    }

    private static void AssertSpawn(SpawnsData data, int worldId, int npcId, float x, float y, float z, int heading)
    {
        var group = Assert.Single(data.GetSpawnsForNpc(worldId, npcId));
        Assert.Equal(worldId, group.GetWorldId()); Assert.Equal(npcId, group.GetNpcId()); Assert.Equal(295, group.GetRespawnTime());
        var spot = Assert.Single(group.GetSpawnTemplates());
        Assert.Equal(x, spot.GetX()); Assert.Equal(y, spot.GetY()); Assert.Equal(z, spot.GetZ());
        Assert.Equal((byte)heading, spot.GetHeading()); Assert.False(spot.IsNoRespawn());
    }
}
