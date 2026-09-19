using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

public sealed class GearUpgradeDataContractTests
{
    [Fact]
    public async Task G5UsesExistingRecipeTuningBoundsConditioningPricesAndServiceSpawns()
    {
        var data = (await RealStaticData.LoadAsync()).StaticData;
        var recipe = data.ItemPurificationDataDh.GetResultItemMap(GearUpgradeScenario.BaseId)[GearUpgradeScenario.ResultId];
        Assert.Equal(5, recipe.GetMinEnchantCount());
        Assert.Equal(1000, recipe.GetNecessaryKinah());
        Assert.Equal(0, recipe.GetNecessaryAbyssPoints());
        var materials = recipe.GetRequiredMaterials();
        Assert.Equal(3, materials.Count);
        Assert.Equal(new[] { (100001614, 1), (162000016, 100), (186000005, 10) },
            materials.Select(item => (item.GetItemId(), item.GetItemCount())).OrderBy(item => item.Item1));
        Assert.Equal(GearUpgradeScenario.Grants.Count, GearUpgradeScenario.Grants.Select(grant => grant.Id).Distinct().Count());
        foreach (var material in materials)
            Assert.Equal(material.GetItemCount(), GearUpgradeScenario.Grants.Single(grant => grant.Id == material.GetItemId()).Count);

        var input = data.ItemDataDh.GetItemTemplate(GearUpgradeScenario.BaseId);
        Assert.True(input.CanTune());
        Assert.Equal(3, input.GetOptionSlotBonus());
        Assert.Equal(5, input.GetMaxEnchantBonus());
        var tunable = data.ItemDataDh.GetItemTemplate(GearUpgradeScenario.TunableId);
        Assert.True(tunable.GetMaxTuneCount() >= 2);
        Assert.Equal(1, tunable.GetOptionSlotBonus());
        Assert.Equal(2, tunable.GetMaxEnchantBonus());
        var scroll = data.ItemDataDh.GetItemTemplate(GearUpgradeScenario.ScrollId);
        Assert.NotNull(scroll.GetActions().GetTuningAction());
        Assert.True(scroll.GetLevel() >= tunable.GetLevel());
        var improvement = data.ItemDataDh.GetItemTemplate(GearUpgradeScenario.ConditionedId).GetImprovement();
        Assert.Equal(1, improvement.GetChargeWay());
        Assert.Equal(720000, improvement.GetPrice1());
        Assert.Equal(1440000, improvement.GetPrice2());
        Assert.True(data.NpcDataDh.GetNpcTemplate(GearUpgradeScenario.RemodelNpcId).SupportsAction(43));
        Assert.True(data.NpcDataDh.GetNpcTemplate(GearUpgradeScenario.ConditioningNpcId).SupportsAction(75));
        var spawns = XDocument.Load(Path.Combine(RealStaticData.RepoRoot(), "game-server/data/static_data/spawns/Npcs/110010000_Sanctum.xml"));
        foreach (int id in new[] { GearUpgradeScenario.RemodelNpcId, GearUpgradeScenario.ConditioningNpcId })
            Assert.Single(spawns.Descendants("spawn"), spawn => (int?)spawn.Attribute("npc_id") == id);
    }
}
