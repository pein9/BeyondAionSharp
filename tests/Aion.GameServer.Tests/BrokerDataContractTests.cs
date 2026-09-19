using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Items;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

public sealed class BrokerDataContractTests
{
	[Fact]
	public async Task E9UsesExistingTradeableOreAndASpawnedSanctumBroker()
	{
		var data = (await RealStaticData.LoadAsync()).StaticData;
		var ore = data.ItemDataDh.GetItemTemplate(BrokerScenario.ItemId);
		Assert.True(ore.IsStackable());
		Assert.Equal(ItemMask.TRADEABLE, ore.GetMask() & ItemMask.TRADEABLE);
		Assert.True(data.NpcDataDh.GetNpcTemplate(BrokerScenario.NpcId).SupportsAction(DialogAction.OPEN_VENDOR));
		var spawns = XDocument.Load(Path.Combine(RealStaticData.RepoRoot(), "game-server/data/static_data/spawns/Npcs/110010000_Sanctum.xml"));
		var spawn = Assert.Single(spawns.Descendants("spawn"), row => (int?)row.Attribute("npc_id") == BrokerScenario.NpcId);
		var spot = Assert.Single(spawn.Elements("spot"));
		Assert.Equal(BrokerScenario.Position.X, (float)spot.Attribute("x")!);
		Assert.Equal(BrokerScenario.Position.Y, (float)spot.Attribute("y")!);
		Assert.Equal(BrokerScenario.Position.Z, (float)spot.Attribute("z")!);
	}
}
