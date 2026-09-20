using System.Xml.Linq;
using Aion.Bots.Navigation;
using Aion.Bots.Movement;
using Aion.Bots.World;
using Aion.Bots.Scenarios;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class SoakPvpCampTests
{
	[Theory]
	[InlineData(Race.ELYOS)]
	[InlineData(Race.ASMODIANS)]
	public void AlreadyHomeDoesNotMoveOrSnapGroundRounding(Race race)
	{
		var geometry = new BotNavigationGeometry(_ => throw new InvalidOperationException("No walk requested."), 1,
			Aion.GameServer.GeoEngine.Collision.IgnoreProperties.Of(race));
		var home = SoakPvpCamp.Home(race);
		foreach (float z in new[] { home.Z, MathF.Round(home.Z, 2), MathF.BitIncrement(home.Z), MathF.BitDecrement(home.Z) })
		{
			var start = home with { Z = z };
			var path = SoakPvpCamp.Walk(geometry, start, home);
			Assert.Empty(path);
			Assert.Empty(new BotMover(new BotWorldModel()).CreateGroundPlan(path, start, 6).Frames);
		}
		Assert.IsType<InvalidOperationException>(Assert.Throws<InvalidDataException>(() => SoakPvpCamp.Walk(geometry, home, home with { X = home.X + (race == Race.ELYOS ? 1 : -1) })).InnerException);
	}

	[Theory]
	[InlineData(Race.ELYOS)]
	[InlineData(Race.ASMODIANS)]
	public void CampUsesOrdinaryRaceMediumKiskWithFiniteSupplyAndNativeLimits(Race race)
	{
		var items = XElement.Load(Path.Combine(Root(), "game-server/data/static_data/items/item_templates.xml"));
		var item = items.Elements("item_template").Single(node => (int?)node.Attribute("id") == SoakPvpCamp.Item(race));
		Assert.Equal(race.ToString(), (string?)item.Attribute("race"));
		Assert.Equal(10000, (int?)item.Attribute("casting_delay"));
		Assert.Equal(1800000, (int?)item.Element("uselimits")!.Attribute("usedelay"));
		Assert.Equal(50, (int?)item.Element("uselimits")!.Attribute("usedelayid"));
		Assert.Equal(SoakPvpCamp.Npc(race), (int?)item.Element("actions")!.Element("toypetspawn")!.Attribute("npcid"));
		var npcs = XElement.Load(Path.Combine(Root(), "game-server/data/static_data/npcs/npc_templates.xml"));
		var npc = npcs.Elements("npc_template").Single(node => (int?)node.Attribute("npc_id") == SoakPvpCamp.Npc(race));
		Assert.Equal("kisk", (string?)npc.Attribute("ai"));
		Assert.Equal(24, (int?)npc.Element("kisk_stats")!.Attribute("members"));
		Assert.Equal(SoakPvpCamp.MaxResurrects, (int?)npc.Element("kisk_stats")!.Attribute("resurrects"));
		Assert.Equal(4, SoakPvpCamp.InitialKisks);
		Assert.Throws<ArgumentOutOfRangeException>(() => SoakPvpCamp.Home(Race.NONE));
	}

	[SkippableFact]
	public async Task ActualCampHasCheckedGroundReturnPathsForBothRaces()
	{
		Skip.IfNot(Environment.GetEnvironmentVariable("AION_SOAK_NAV_INTEGRATION") == "1", "Set AION_SOAK_NAV_INTEGRATION=1 for actual Reshanta camp geometry.");
		var assets = await BotNavigationAssets.LoadAsync(Root(), Path.Combine(Root(), "run/soak-navigation-test-cache"), CancellationToken.None);
		foreach (var race in new[] { Race.ELYOS, Race.ASMODIANS })
		{
			var nav = assets.PvpCampRoute(race);
			var home = SoakPvpCamp.Home(race);
			var encounter = SoakPvpCamp.Encounter(nav.Geometry, race);
			if (race == Race.ASMODIANS)
			{
				_ = SoakPvpCamp.Walk(nav.Geometry, home with { Z = BitConverter.Int32BitsToSingle(0x44c2b471), Heading = 0 }, home);
				_ = SoakPvpCamp.Walk(nav.Geometry, home with { Z = 1557.64f }, new BotPosition(3190, 2480, 1558.3164f, 60));
			}
			Assert.NotEqual(home.X, encounter.X);
			// Exercise the exact runtime sequence, including persisted coordinate rounding and
			// ground-normalized origins. A broad local A* search may touch unrelated siege nodes.
			var current = home with { Z = MathF.Round(home.Z, 2) };
			for (int round = 0; round < 20; round++)
				foreach (var destination in new[] { home, encounter, home })
				{
					var path = SoakPvpCamp.Walk(nav.Geometry, current, destination);
					if (current.X != destination.X) Assert.NotEmpty(path);
					var movement = new BotMover(new BotWorldModel()).CreateGroundPlan(path, current, 6);
					if (movement.Frames.Count > 0) current = movement.Frames[^1].Position;
				}
			Assert.Throws<InvalidDataException>(() => SoakPvpCamp.Walk(nav.Geometry, current, home with { Y = home.Y + 2 }));
			Assert.Equal(SoakPvpCamp.Item(race), assets.KiskTemplate(race).GetTemplateId());
		}
	}
	private static string Root() => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
}
