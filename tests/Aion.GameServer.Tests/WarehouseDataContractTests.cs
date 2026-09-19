using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Items;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

public sealed class WarehouseDataContractTests
{
	[Fact]
	public async Task E8UsesExistingStorableItemsAndSanctumWarehouseWithTheQuotedExpansionPrice()
	{
		var data = (await RealStaticData.LoadAsync()).StaticData;
		var weapon = data.ItemDataDh.GetItemTemplate(WarehouseScenario.WeaponId);
		var ore = data.ItemDataDh.GetItemTemplate(WarehouseScenario.OreId);
		Assert.False(weapon.IsStackable());
		Assert.True(ore.IsStackable());
		Assert.Equal(ItemMask.STORABLE_IN_WH, weapon.GetMask() & ItemMask.STORABLE_IN_WH);
		Assert.Equal(ItemMask.STORABLE_IN_AWH, ore.GetMask() & ItemMask.STORABLE_IN_AWH);
		Assert.True(data.NpcDataDh.GetNpcTemplate(WarehouseScenario.NpcId).SupportsAction(DialogAction.DEPOSIT_CHAR_WAREHOUSE));
		// Expansion is a priced sub-action of the warehouse dialog, not a separate advertised function.
		Assert.Equal(1200, data.WarehouseExpandDataDh.GetWarehouseExpansionTemplate(WarehouseScenario.NpcId).GetPrice(1));
		var spawns = XDocument.Load(Path.Combine(RealStaticData.RepoRoot(), "game-server/data/static_data/spawns/Npcs/110010000_Sanctum.xml"));
		var spawn = Assert.Single(spawns.Descendants("spawn"), row => (int?)row.Attribute("npc_id") == WarehouseScenario.NpcId);
		var spot = Assert.Single(spawn.Elements("spot"));
		Assert.Equal(WarehouseScenario.Position.X, (float)spot.Attribute("x")!);
		Assert.Equal(WarehouseScenario.Position.Y, (float)spot.Attribute("y")!);
		Assert.Equal(WarehouseScenario.Position.Z, (float)spot.Attribute("z")!);
	}
}
