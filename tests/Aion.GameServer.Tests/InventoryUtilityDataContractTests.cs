using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

public sealed class InventoryUtilityDataContractTests
{
	[Fact]
	public async Task G6UsesExistingWrappableGearOreRewardsBoxChoicesAndCubeService()
	{
		var data = (await RealStaticData.LoadAsync()).StaticData;
		var weapon = data.ItemDataDh.GetItemTemplate(InventoryUtilityScenario.WeaponId);
		var scroll = data.ItemDataDh.GetItemTemplate(InventoryUtilityScenario.WrapId);
		Assert.False(weapon.IsTradeable());
		Assert.False(weapon.CanTune());
		Assert.True(weapon.GetPackCount() > 0);
		Assert.True(scroll.GetLevel() >= weapon.GetLevel());
		Assert.Equal(weapon.GetItemQuality(), scroll.GetItemQuality());
		var pack = Assert.IsType<PackAction>(Assert.Single(scroll.GetActions().GetItemActions()));
		Assert.Equal(UseTarget.WEAPON, pack.GetTarget());
		var ore = data.ItemDataDh.GetItemTemplate(InventoryUtilityScenario.OreId);
		Assert.Equal(3000, ore.GetCastingDelay());
		Assert.Equal(0, ore.GetUseLimits().GetDelayTime());
		var collection = Assert.Single(data.DecomposableItemsDataDh.GetInfoByItemId(InventoryUtilityScenario.OreId));
		Assert.Empty(collection.GetRandomItems());
		var reward = Assert.Single(collection.GetItems());
		Assert.Equal(InventoryUtilityScenario.ProductId, reward.GetItemId());
		Assert.Equal(3, reward.GetMinCount());
		Assert.Equal(3, reward.GetMaxCount());
		var choices = data.DecomposableItemsDataDh.GetSelectableItems(InventoryUtilityScenario.BoxId);
		Assert.Equal(13, choices.Count);
		Assert.Equal(InventoryUtilityScenario.Choices, choices.Select(choice => choice.GetItemId()));
		foreach (var choice in choices)
		{
			Assert.Equal(1, choice.GetMinCount());
			Assert.Equal(1, choice.GetMaxCount());
			Assert.Null(choice.GetPlayerClasses());
			Assert.Equal(Race.PC_ALL, choice.GetRace());
		}
		Assert.True(data.NpcDataDh.GetNpcTemplate(InventoryUtilityScenario.NpcId).SupportsAction(DialogAction.EXTEND_INVENTORY));
		Assert.Equal(1000, data.CubeExpandDataDh.GetCubeExpansionTemplate(InventoryUtilityScenario.NpcId).GetPrice(1));
		var spawns = XDocument.Load(Path.Combine(RealStaticData.RepoRoot(), "game-server/data/static_data/spawns/Npcs/110010000_Sanctum.xml"));
		var spawn = Assert.Single(spawns.Descendants("spawn"), spawn => (int?)spawn.Attribute("npc_id") == InventoryUtilityScenario.NpcId);
		var spot = Assert.Single(spawn.Elements("spot"));
		Assert.Equal(InventoryUtilityScenario.Position.X, (float)spot.Attribute("x")!);
		Assert.Equal(InventoryUtilityScenario.Position.Y, (float)spot.Attribute("y")!);
		Assert.Equal(InventoryUtilityScenario.Position.Z, (float)spot.Attribute("z")!);
	}
}
