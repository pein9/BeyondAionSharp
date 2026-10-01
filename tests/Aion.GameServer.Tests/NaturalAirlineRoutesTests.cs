using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

/// <summary>The maintainer's 2026-10-01 note: the natural bot flies between hubs. The client routes are generated
/// (tools/client-extract/extract_flight_routes.py); these pin them to the server's FLIGHT locations and the choice of a flight.</summary>
public sealed class NaturalAirlineRoutesTests
{
	private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
	private static readonly IReadOnlyList<NaturalAirlineRoute> Routes = NaturalAirlineRoutes.Load(Root);
	private const int Altgard = 220030000;

	[Fact]
	public void EveryRouteIsAServerFlightLocationOfItsTeleporterAndLeavesFromItsPad()
	{
		XElement teleporters = XDocument.Load(Path.Combine(Root, "game-server/data/static_data/npc_teleporter.xml")).Root!;
		Assert.True(Routes.Count >= 100);
		foreach (NaturalAirlineRoute route in Routes)
		{
			XElement location = teleporters.Elements("teleporter_template")
				.Where(template => ((string?)template.Attribute("npc_ids"))?.Split(' ').Contains($"{route.NpcId}") == true)
				.Descendants("telelocation").Single(loc => (int)loc.Attribute("loc_id")! == route.LocationId);
			Assert.Equal(("FLIGHT", route.TeleportId, route.Price),
				((string)location.Attribute("type")!, (int)location.Attribute("teleportid")!, (long)location.Attribute("price")!));
			Assert.True(route.Keys.Length >= 2 && route.Keys[0].T == 0 && Math.Abs(route.Keys[^1].T - route.Seconds) < 0.01f, route.Route);
		}
		// Altgard's eight: the fortress (203561) to four hubs and each of them back.
		Assert.Equal(8, Routes.Count(route => route.MapId == Altgard));
		Assert.Equal([20, 32, 121, 122], Routes.Where(route => route.NpcId == 203561).Select(route => route.LocationId).Order());
	}

	[Fact]
	public void FromBasfeltToTheFortressTheBotFliesAndAShortTripItWalks()
	{
		var nokir = new BotPosition(1779.88f, 690.477f, 264.309f, 0);
		var fortressTeleporter = new BotPosition(1754.08f, 1805.14f, 255.917f, 0);
		NaturalAirlineRoute flight = Assert.IsType<NaturalAirlineRoute>(NaturalAirlineRoutes.Toward(Routes, Altgard, nokir, fortressTeleporter));
		Assert.Equal((203683, 19, 16001, 400L, "df1a_sub_basfelt"), (flight.NpcId, flight.LocationId, flight.TeleportId, flight.Price, flight.Route));
		Assert.True(MathF.Sqrt(MathF.Pow(flight.Landing.X - fortressTeleporter.X, 2) + MathF.Pow(flight.Landing.Y - fortressTeleporter.Y, 2)) < 5);
		// MuMu Village is 600 m from Nokir with no transporter landing near it: walk.
		Assert.Null(NaturalAirlineRoutes.Toward(Routes, Altgard, nokir, new BotPosition(1500, 270, 293, 0)));
	}

	[Fact]
	public void TheFlightFollowsTheRouteKeysInTheirTime()
	{
		NaturalAirlineRoute basfelt = Routes.Single(route => route.NpcId == 203683 && route.LocationId == 19);
		var plan = NaturalAirlineRoutes.Plan(basfelt);
		Assert.Equal(TimeSpan.FromSeconds(basfelt.Seconds), plan.Duration);
		Assert.Equal(TimeSpan.FromSeconds(basfelt.Seconds), TimeSpan.FromTicks(plan.Frames.Sum(frame => frame.DelayBefore.Ticks)));
		Assert.Equal(basfelt.Landing, plan.Frames[^1].Position);
		// Each key is passed through: the route climbs to 324 m on the way, well above a straight line.
		NaturalAirlineKey high = basfelt.Keys.MaxBy(key => key.Z)!;
		Assert.Contains(plan.Frames, frame => MathF.Abs(frame.Position.Z - high.Z) < 3);
		Assert.True(plan.Distance > MathF.Sqrt(MathF.Pow(basfelt.Landing.X - basfelt.Departure.X, 2) + MathF.Pow(basfelt.Landing.Y - basfelt.Departure.Y, 2)));
	}
}
