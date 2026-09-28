using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

/// <summary>NA-08: binding, teleporting and trading decided from client-observed state on any map.</summary>
public sealed class NaturalServicePolicyTests
{
	private static readonly BotPosition Obelisk = new(1658.44f, 1815.30f, 254.10f, 0); // Altgard Fortress, 700065
	private static readonly BotPosition Doman = new(1682.45f, 1397.31f, 195.36f, 0);

	[Fact]
	public void BindIsReadyOnlyWithinFiveMetresOnTheSameMapWithTheFee()
	{
		BotPosition near = Obelisk with { X = Obelisk.X - 3 };
		Assert.True(NaturalServicePolicy.Bind(near, 220030000, Obelisk, 220030000, 5, 1000, 451, null).IsReady);
		Assert.Equal("too-far", NaturalServicePolicy.Bind(Obelisk with { X = Obelisk.X - 7 }, 220030000, Obelisk, 220030000, 5, 1000, 451, null).Outcome);
		Assert.Equal("not-enough-kinah", NaturalServicePolicy.Bind(near, 220030000, Obelisk, 220030000, 5, 450, 451, null).Outcome);
		Assert.Equal("wrong-map", NaturalServicePolicy.Bind(near, 120010000, Obelisk, 220030000, 5, 1000, 451, null).Outcome);
	}

	[Fact]
	public void BindingTheSameObeliskAgainIsSkipped()
	{
		var bound = new BotBindPoint(220030000, Obelisk with { X = Obelisk.X - 2 }, 0);
		Assert.Equal("already-bound", NaturalServicePolicy.Bind(Obelisk, 220030000, Obelisk, 220030000, 5, 1000, 451, bound).Outcome);
		// A bind at another obelisk (Aldelle, in Ishalgen) does not stop a new one.
		var aldelle = new BotBindPoint(220010000, new BotPosition(587.7f, 2467.1f, 278.8f, 0), 0);
		Assert.True(NaturalServicePolicy.Bind(Obelisk, 220030000, Obelisk, 220030000, 5, 1000, 451, aldelle).IsReady);
	}

	[Fact]
	public void TeleportNeedsTalkRangeTheObservedFareAndFeetOnTheGround()
	{
		BotPosition near = Doman with { Y = Doman.Y - 3 };
		Assert.True(NaturalServicePolicy.Teleport(near, Doman, 4, 1000, 706, flying: false).IsReady);
		Assert.Equal("too-far", NaturalServicePolicy.Teleport(Doman with { Y = Doman.Y - 5 }, Doman, 4, 1000, 706, false).Outcome);
		// The Doman fare goes through PricesService (706 observed in SIM for a 500 base), not the base price.
		Assert.Equal("not-enough-kinah", NaturalServicePolicy.Teleport(near, Doman, 4, 600, 706, false).Outcome);
		Assert.Equal("flying", NaturalServicePolicy.Teleport(near, Doman, 4, 1000, 706, true).Outcome);
	}

	[Fact]
	public void VendorBuysOnlyWhatTheObservedTabsOfferAndWhatKinahCovers()
	{
		var tabs = new Dictionary<int, int[]> { [275] = [162000052, 162000053], [399] = [169300003] };
		IReadOnlyCollection<int> Items(int tab) => tabs.TryGetValue(tab, out int[]? items) ? items : [];
		NaturalVendorPlan plan = NaturalServicePolicy.Vendor([275], Items,
			[new(162000053, 5), new(169300003, 30), new(0, 3)], _ => 450, kinah: 2000);
		// Four elixirs fit 2,000 Kinah; the rest is refused for Kinah; powder is not on this vendor's list.
		Assert.Equal([new NaturalPurchase(162000053, 4)], plan.Buys);
		Assert.Equal(1800, plan.Cost);
		Assert.Contains(plan.Refused, r => r.Purchase == new NaturalPurchase(162000053, 1) && r.Reason == "not-enough-kinah");
		Assert.Contains(plan.Refused, r => r.Purchase.ItemId == 169300003 && r.Reason == "not-in-trade-list");
		Assert.Equal("not-enough-kinah", Assert.Single(NaturalServicePolicy.Vendor([275], Items, [new(162000053, 1)], _ => 450, 100).Refused).Reason);
	}

	[Fact]
	public void TheIshalgenAndAltgardPotionListsBothResolveFromShippedGoods()
	{
		string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
		XElement goods = XDocument.Load(Path.Combine(root, "game-server/data/static_data/goodslists/goodslists.xml")).Root!;
		IReadOnlyCollection<int> Items(int tab) => goods.Elements("list").Where(list => (int?)list.Attribute("id") == tab)
			.Elements("item").Select(item => (int)item.Attribute("id")!).ToArray();
		Assert.Equal([new NaturalPurchase(162000052, 3)],
			NaturalServicePolicy.Vendor([264, 721], Items, [new(162000052, 3)], _ => 250, 10_000).Buys); // Ishalgen
		Assert.Equal([new NaturalPurchase(162000053, 3)],
			NaturalServicePolicy.Vendor([275], Items, [new(162000053, 3)], _ => 450, 10_000).Buys); // Altgard, Nirmirn
		Assert.Equal([new NaturalPurchase(169300003, 30)],
			NaturalServicePolicy.Vendor([399, 1505, 118], Items, [new(169300003, 30)], _ => 15, 10_000).Buys); // Donabe
	}
}
