using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Services.Items;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// NA-08: the map-agnostic service steps against the real server — Doman's teleporter to Altgard (the fare as
	/// PricesService charges it), the Altgard Fortress bind, and trading at Nirmirn and Donabe, including a purchase the
	/// observed trade window must refuse. Focused setup (level, Kinah, a sellable item, the Pandaemonium start) is GM
	/// preparation like CAPITAL's; it is diagnostic coverage, not natural-play evidence.
	/// </summary>
	[SkippableFact]
	public async Task NaturalServiceStepsTeleportBindAndTradeOnTheBridgeMaps()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		// Only this scenario's own window: in the Fast tier the process history holds earlier scenarios' audit lines
		// (the Warrior combat scenario's skill 2864 timing audits failed NA-26's Docker Fast).
		using var policy = NewEconomyPolicy("NA08", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		CancellationToken token = timeout.Token;
		NaturalAscensionContract contract = NaturalAscensionContract.LoadDefault();
		await using var session = new SimulationL0Session(fixture, policy, "b01", 57, "Asimservice", Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		var player = fixture.World.GetPlayer(session.CharacterId);
		player.GetCommonData().SetLevel(10);
		Assert.Equal(0, ItemService.AddItem(player, BotWorldModel.KinahItemId, 20_000));
		Assert.Equal(0, ItemService.AddItem(player, 162000052, 3)); // something ordinary to sell
		NaturalAscensionStep doman = contract.Steps.Single(step => step.Key == "q2904-v0-doman");
		await TeleportForSetupAsync(session, player, doman.MapId, doman.Position[0], doman.Position[1] - 2, doman.Position[2], token);
		await session.SynchronizeAsync(token);
		var steps = new NaturalServiceSteps(session);

		session.BeginStep("s01", "doman-teleporter-to-altgard");
		int domanObject = await session.WaitForNpcAsync(contract.Teleporter.NpcId, token);
		BotPosition domanAt = session.Api.World.Objects[domanObject].Position;
		Assert.Equal("too-far", (await steps.TeleportAsync(domanObject, domanAt with { Y = domanAt.Y + 30 }, doman.TalkRange,
			contract.Teleporter.LocationId, contract.Teleporter.BasePrice, contract.Teleporter.Destination.MapId, token)).Outcome);
		NaturalServiceOutcome teleported = await steps.TeleportAsync(domanObject, domanAt, doman.TalkRange,
			contract.Teleporter.LocationId, contract.Teleporter.BasePrice, contract.Teleporter.Destination.MapId, token);
		Assert.True(teleported.IsDone, teleported.Reason);
		Assert.Equal(contract.Teleporter.Destination.MapId, player.GetWorldId());

		session.BeginStep("s02", "bind-at-altgard-fortress");
		BotPosition obelisk = new(contract.Bind.Position[0], contract.Bind.Position[1], contract.Bind.Position[2], 0);
		await session.MoveToPositionAsync(obelisk with { X = obelisk.X - 2 }, token);
		int obeliskObject = await session.WaitForNpcAsync(contract.Bind.NpcId, token);
		NaturalServiceOutcome bound = await steps.BindAsync(obeliskObject, obelisk, contract.Bind.MapId, contract.Bind.Price,
			contract.Bind.AcceptRange, token);
		Assert.True(bound.IsDone, bound.Reason);
		Assert.Equal(contract.Bind.MapId, player.GetBindPoint().GetMapId());
		Assert.Equal("already-bound", (await steps.BindAsync(obeliskObject, obelisk, contract.Bind.MapId, contract.Bind.Price,
			contract.Bind.AcceptRange, token)).Outcome);

		XElement goods = XDocument.Load(Path.Combine(RealStaticData.RepoRoot(), "game-server/data/static_data/goodslists/goodslists.xml")).Root!;
		IReadOnlyCollection<int> Items(int tab) => goods.Elements("list").Where(list => (int?)list.Attribute("id") == tab)
			.Elements("item").Select(item => (int)item.Attribute("id")!).ToArray();
		long BasePrice(int item) => fixture.DataManager.StaticData.ItemDataDh.GetItemTemplate(item).GetPrice();
		long Stock(int item) => session.Api.World.Inventory.Values.Where(entry => entry.ItemId == item).Sum(entry => entry.Count);

		session.BeginStep("s03", "sell-and-buy-potions-at-nirmirn");
		BotPosition nirmirn = new(1618.83f, 1916.97f, 262.936f, 0);
		await session.MoveToPositionAsync(nirmirn with { X = nirmirn.X - 2 }, token);
		int nirmirnObject = await session.WaitForNpcAsync(contract.Shop.PotionNpcId, token);
		BotInventoryItem junk = session.Api.World.Inventory.Values.Single(item => item.ItemId == 162000052);
		NaturalVendorResult potions = await steps.TradeAsync(nirmirnObject, [new(junk.ObjectId, junk.ItemId, junk.Count)],
			[new(162000053, 3), new(169300003, 2)], Items, BasePrice, token);
		Assert.Single(potions.Sold);
		Assert.Equal(0, Stock(162000052));
		Assert.Equal([new NaturalPurchase(162000053, 3)], potions.Bought);
		Assert.Equal("not-in-trade-list", Assert.Single(potions.Refused).Reason);
		Assert.Equal(3, Stock(162000053));

		session.BeginStep("s04", "buy-powder-at-donabe");
		BotPosition donabe = new(1758.07f, 1779.11f, 256.174f, 0);
		await session.MoveToPositionAsync(donabe with { X = donabe.X - 2 }, token);
		int donabeObject = await session.WaitForNpcAsync(contract.Shop.ReagentNpcId, token);
		NaturalVendorResult powder = await steps.TradeAsync(donabeObject, [], [new(169300003, 5)], Items, BasePrice, token);
		Assert.Equal([new NaturalPurchase(169300003, 5)], powder.Bought);
		Assert.Equal(5, Stock(169300003));
		policy.AssertClean();
	}
}
