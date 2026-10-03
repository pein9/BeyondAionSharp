using System.Text.Json;
using Aion.Bots.Api;
using Aion.Bots.Dashboard;
using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>CG-03: probe-only equipment, actual reward offer/debits/equips and login persistence. Free account 222.</summary>
	[SkippableFact]
	public async Task CoinRewardShopRefusesInsufficientFundsAndPreservesChainAndStaffAcrossRelog()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int map = 220030000;
		using var policy = NewPolicy("CG03", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
		CancellationToken token = timeout.Token;
		string root = RealStaticData.RepoRoot();
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "cg03-equipment";
		string tracePath = Path.Combine(root, "run", $"{run}-cg03.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(tracePath, run, "b01", "sim-player-222", virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 222, "Asimcoinarmour", Race.ASMODIANS, trace, tracePath);
		var dashboard = new LiveBotDashboardState();
		await using var host = new LiveBotDashboardHost(run, ["CG-03"], dashboard,
			int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880"));
		session.Dashboard = dashboard;
		if (host.Enabled) Console.WriteLine($"CG-03 dashboard: {host.Url}");
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("cg");
		NaturalCoinGear gear = leg.CoinGear!;
		session.BeginStep("s00", "labelled-probe-loadout-and-heart-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		var player = fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(player, PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		player.GetCommonData().SetLevel(24);
		SkillLearnService.LearnNewSkills(player, 1, 24);
		player.GetInventory().IncreaseKinah(536193);
		foreach ((int item, long slot) in new (int, long)[]
		{
			(101501357, 3), (125004139, 4), (110551139, 8), (111101650, 16), (114501726, 32), (113100773, 4096),
			(122001664, 256), (122000871, 512), (123001109, 65536), (120001521, 64), (120001132, 128), (121000751, 1024),
		})
		{
			Assert.Equal(0, ItemService.AddItem(player, item, 1, allowInventoryOverflow: true));
			var owned = player.GetInventory().GetItems().Last(i => i.GetItemId() == item);
			Assert.NotNull(player.GetEquipment().EquipItem(owned.GetObjectId(), slot));
		}
		Assert.Equal(0, ItemService.AddItem(player, gear.CoinItemId, 3, allowInventoryOverflow: true));
		Assert.Equal(0, ItemService.AddItem(player, gear.SealedBundleId, 1, allowInventoryOverflow: true));
		var instance = fixture.World.GetWorldMap(map).GetMainWorldMapInstance();
		BotPosition setup = new(2654.192f, 1660.59f, 324.742f, 0);
		var aggressive = instance.GetNpcs().Where(n => !n.IsDead() &&
			NaturalHostility.IsAggressive(n.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
			MathF.Pow(n.GetX() - setup.X, 2) + MathF.Pow(n.GetY() - setup.Y, 2) <= 1600).ToArray();
		foreach (var npc in aggressive) fixture.World.Despawn(npc);
		Console.WriteLine($"CG-03 labelled setup cleared {aggressive.Length} aggressive neighbours on the upper shop platform.");
		session.Api.World.BeginWorldReload();
		await TeleportForSetupAsync(session, player, map, setup.X, setup.Y, setup.Z, token);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
		var services = new NaturalServiceSteps(session);
		int obelisk = await session.WaitForNpcAsync(700067, token);
		Assert.True((await services.BindAsync(obelisk, new(2656.192f, 1660.59f, 325.052f, 0), map, 813, 5, token)).IsDone);
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(instance.GetInstanceId(), Race.ASMODIANS);
		async Task<int> ApproachAsync(int npcId)
		{
			var npc = instance.GetNpcs(npcId).First(n => !n.IsDead());
			BotPosition at = new(npc.GetX(), npc.GetY(), npc.GetZ(), 0);
			if (NaturalFlightPolicy.Distance(session.CurrentPosition, at) > npc.GetObjectTemplate().GetTalkDistance() - 0.5f)
			{
				var route = geometry.GroundAround(map, at, [2f, 3f]).OrderBy(p => NaturalFlightPolicy.Distance(session.CurrentPosition, p))
					.Select(p => geometry.FindJourneyPath(map, session.CurrentPosition, p)).First(p => p.Count > 0);
				await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing).CreateGroundPlan(route,
					session.CurrentPosition, session.Api.World.MovementSpeed!.Value), token);
				await session.SynchronizeAsync(token);
			}
			return await session.WaitForNpcAsync(npcId, token);
		}
		var inventory = NaturalIshalgenInventoryPolicy.Load(root, session.Api.World.Inventory.Values.Select(i => i.ItemId)
			.Concat(gear.ProtectedItemIds).Append(186000007));
		var shop = new NaturalCoinGearSteps(session, fixture.DataManager.StaticData, gear, inventory);
		NaturalAltgardObservation Observe(NaturalCoinGearProgress? progress = null) => NaturalAltgardObservation.Observe(session.Api.World,
			session.CurrentPosition, freeCubeSlots: inventory.Decide(session.Api.World, coinGear: gear).FreeSlots, coinGearProgress: progress);
		long Coins() => session.Api.World.Inventory.Values.Where(i => i.ItemId == gear.CoinItemId).Sum(i => i.Count);
		int staff = session.Api.World.Inventory.Values.Single(i => i.ItemId == gear.StaffItemId).ObjectId;
		session.BeginStep("s01", "actual-batch-refusal-three-coins-for-four-coin-order");
		int vendor = await ApproachAsync(gear.VendorNpcId);
		BotTradeWindow offer = await shop.OpenShopAsync(vendor, token);
		NaturalJourneyItem[] beforeRefusal = Observe().Inventory!;
		long kinah = session.Api.World.Kinah;
		await session.SendPacketAsync(GameClientPackets.BuyItem(vendor, 15, gear.Purchases.Select(p => (p.ItemId, 1L)).ToArray()), token);
		await session.WaitForPacketAsync(typeof(SM_SYSTEM_MESSAGE), token,
			p => p.Get<string>("name") == "STR_MSG_NOT_ENOUGH_ABYSSPOINT");
		await session.SynchronizeAsync(token);
		Assert.Equal(3, Coins());
		Assert.Equal(kinah, session.Api.World.Kinah);
		Assert.Equal(beforeRefusal.OrderBy(i => i.ObjectId), Observe().Inventory!.OrderBy(i => i.ObjectId));
		Assert.All(gear.Purchases, p => Assert.DoesNotContain(session.Api.World.Inventory.Values, i => i.ItemId == p.ItemId));
		await session.SendPacketAsync(session.Api.CloseDialog(vendor), token);
		// Only this disposable probe gets gate/funds setup. CG-02 already proved the natural 6+16 hunt.
		session.BeginStep("s02", "labelled-reward-gate-real-five-coin-claim");
		Assert.Equal(0, ItemService.AddItem(player, gear.CoinItemId, 15, allowInventoryOverflow: true));
		int lateni = await ApproachAsync(203659);
		await session.StartQuestAsync(lateni, gear.QuestId, token);
		player.GetQuestStateList().GetQuestState(gear.QuestId).SetQuestVar(6 | (16 << 6));
		NaturalAltgardObservation beforeReward = Observe();
		await session.FinishQuestAsync(lateni, gear.QuestId, token);
		await session.SynchronizeAsync(token);
		NaturalCoinGearProgress progress = NaturalCoinGearProgress.Empty.ObserveReward(gear, beforeReward, Observe());
		Assert.Equal(23, Coins());
		foreach (NaturalCoinGearPurchase purchase in gear.Purchases)
		{
			session.BeginStep($"s03-{purchase.ItemId}", "exact-approved-coin-purchase-and-armour-equip");
			vendor = await ApproachAsync(gear.VendorNpcId);
			progress = await shop.PurchaseAsync(vendor, purchase.ItemId, progress, token);
			await shop.EquipAsync(purchase.ItemId, progress, token);
		}
		Assert.Equal("coin-gear-complete", NaturalCoinGearPolicy.Decide(gear, Observe(progress)).Action);
		Assert.Equal(19, Coins());
		Assert.Equal(kinah, session.Api.World.Kinah);
		NaturalInventoryPlan protectedPlan = inventory.Decide(session.Api.World, coinGear: gear);
		Assert.DoesNotContain(protectedPlan.Sales.Concat(protectedPlan.Equips), d => gear.ProtectedItemIds.Contains(d.ItemId) || d.ItemId is 186000006 or 186000007);
		var ishalgen = NaturalIshalgenContract.Load(Path.Combine(root, "parity-artifacts/e2e/natural-ishalgen-contract.json"));
		NaturalJourneyCheckpoint before = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
			session.ConnectionGeneration, ishalgen, session.CurrentPosition, coinGearProgress: progress);
		session.BeginStep("s04", "actual-endpoint-relog-no-weapon-swap");
		await session.QuitAsync(token);
		await session.WaitForReentryAsync(token);
		await session.ReloginExistingCharacterAsync(token);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		NaturalJourneyCheckpoint after = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
			session.ConnectionGeneration, ishalgen, session.CurrentPosition, coinGearProgress: progress);
		NaturalJourneyPersistence.Verify(before, after);
		Assert.Equal("coin-gear-complete", NaturalCoinGearPolicy.Decide(gear, Observe(progress)).Action);
		Assert.Equal(1, session.Api.World.CompletedQuests[gear.QuestId].CompleteCount);
		Assert.False(session.Api.World.IsDead);
		Assert.True(NaturalAltgardDecisionEngine.BoundAt(leg.Bind!, map, session.Api.World.ObeliskBindPoint));
		Assert.Equal((gear.StaffItemId, (ushort)3), (session.Api.World.Inventory[staff].ItemId, session.Api.World.Inventory[staff].EquipmentSlot));
		Assert.DoesNotContain(gear.ForbiddenStigmaSkillId, session.Api.World.Skills.Keys);
		await File.WriteAllTextAsync(Path.ChangeExtension(tracePath, ".summary.json"), JsonSerializer.Serialize(new
			{ account = 222, offer, refusedCoins = 3, partialItems = 0, progress, kinah, finalCoins = Coins(), staff, before, after }), token);
		Console.WriteLine("CG-03 PASS: whole-order refusal, three approved purchases, four-coin debit, five chain slots, unchanged staff and actual relog.");
		policy.AssertClean();
	}
}
