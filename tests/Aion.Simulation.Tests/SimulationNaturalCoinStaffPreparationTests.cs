using Aion.Bots.Dashboard;
using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Items;
using Aion.GameServer.TestKit;
using Aion.GameServer.Utils;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	[SkippableFact]
	public async Task CoinPreparationEquipsTheOwnedEarnedStaffBeforeItsNativeRewardReceipt()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		using var policy = NewPolicy("RC11CoinStaff", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
		CancellationToken token = timeout.Token;
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "rc11-coin-staff";
		string path = Path.Combine(RealStaticData.RepoRoot(), "run", $"{run}.coin-staff.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(path, run, "b01", "sim-player-230",
			virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 230, "Asimcoinstaff", Race.ASMODIANS, trace, path);
		var dashboard = new LiveBotDashboardState();
		await using var host = new LiveBotDashboardHost(run, ["RC-11"], dashboard,
			int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880"));
		session.Dashboard = dashboard;
		if (host.Enabled) Console.WriteLine($"RC-11 coin staff probe dashboard: {host.Url}");
		var probe = new DestinyProbe(this, fixture, session, token);
		await probe.InitializeAsync();
		await probe.SetupAtFortressAsync();
		NaturalCoinGear gear = NaturalAltgardContract.LoadLeg("cg").CoinGear!;
		foreach (int item in new[] { 101501355, gear.StaffItemId, gear.SealedBundleId })
			Assert.Equal(0, ItemService.AddItem(probe.Server, item, 1, allowInventoryOverflow: true));
		(int Item, ushort Slot)[] body = [(110551139, 8), (114501726, 32), (111101650, 16), (113100773, 4096),
			(112500097, 2048), (120000833, 64), (120001132, 128), (122001664, 256), (122000871, 512), (121000751, 1024), (125004139, 4)];
		foreach (var item in body)
			Assert.Equal(0, ItemService.AddItem(probe.Server, item.Item, 1, allowInventoryOverflow: true));
		Assert.Equal(0, ItemService.AddItem(probe.Server, 123001109, 1, allowInventoryOverflow: true));
		Assert.Equal(0, ItemService.AddItem(probe.Server, gear.CoinItemId, gear.IncomingCoins, allowInventoryOverflow: true));
		await session.SynchronizeAsync(token);
		foreach (var item in body)
		{
			int objectId = session.Api.World.Inventory.Values.Single(i => i.ItemId == item.Item).ObjectId;
			await session.SendPacketAsync(session.Api.Equip(0, item.Slot, objectId), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(item.Slot, session.Api.World.Inventory[objectId].EquipmentSlot);
		}
		BotInventoryItem old = session.Api.World.Inventory.Values.Single(i => i.ItemId == 101501355);
		BotInventoryItem earned = session.Api.World.Inventory.Values.Single(i => i.ItemId == gear.StaffItemId);
		await session.SendPacketAsync(session.Api.Equip(0, NaturalGearPolicy.MainHand, old.ObjectId), token);
		await session.SynchronizeAsync(token);
		Assert.Equal((ushort)3, session.Api.World.Inventory[old.ObjectId].EquipmentSlot);
		Assert.NotEqual((ushort)3, session.Api.World.Inventory[earned.ObjectId].EquipmentSlot);
		Console.WriteLine("RC-11 free probe 230: labelled level/loadout/funds; old level-16 staff equipped and earned level-21 staff owned in the cube, matching attempt 22.");
		long kinah = session.Api.World.Kinah;
		NaturalJourneyItem[] retained = await NaturalCoinGearSteps.PrepareRetainedLoadoutAsync(session, gear, token);
		Assert.Equal((ushort)3, session.Api.World.Inventory[earned.ObjectId].EquipmentSlot);
		Assert.NotEqual((ushort)3, session.Api.World.Inventory[old.ObjectId].EquipmentSlot);
		Assert.Contains(retained, i => i.ObjectId == earned.ObjectId && i.EquipmentSlot == 3);
		Assert.DoesNotContain(retained, i => i.ObjectId == old.ObjectId || i.EquipmentSlot is 16 or 2048 or 4096);
		Assert.Contains(retained, i => i.ItemId == 110551139 && i.EquipmentSlot == 8);
		Assert.Contains(retained, i => i.ItemId == 114501726 && i.EquipmentSlot == 32);
		await NaturalCoinGearSteps.EnsureRetainedStaffEquippedAsync(session, gear, token);
		BotPosition at = new(2663, 1663, 324.69f, 0);
		foreach (var npc in probe.Server.GetWorldMapInstance().GetNpcs().Where(n => !n.IsDead() &&
			NaturalHostility.IsAggressive(n.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
			NaturalFlightPolicy.Distance(at, new(n.GetX(), n.GetY(), n.GetZ(), 0)) < 60).ToArray())
			fixture.World.Despawn(npc);
		session.Api.World.BeginWorldReload();
		await TeleportForSetupAsync(session, probe.Server, 220030000, at.X, at.Y, at.Z, token);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
		int lateni = await session.WaitForNpcAsync(203659, token);
		await session.StartQuestAsync(lateni, gear.QuestId, token);
		// Only the disposable probe's kill counters are setup. The equip and reward use ordinary client packets.
		probe.Server.GetQuestStateList().GetQuestState(gear.QuestId).SetQuestVar(6 | (16 << 6));
		NaturalAltgardObservation before = NaturalAltgardObservation.Observe(session.Api.World, session.CurrentPosition);
		await session.FinishQuestAsync(lateni, gear.QuestId, token);
		await session.SynchronizeAsync(token);
		NaturalCoinGearProgress progress = NaturalCoinGearProgress.Empty.ObserveReward(gear, before,
			NaturalAltgardObservation.Observe(session.Api.World, session.CurrentPosition));
		Assert.Equal(earned.ObjectId, progress.StaffObjectId);
		Assert.Equal(new NaturalCoinRewardReceipt(0, 1, 18, 23), progress.Reward);
		Assert.Equal(kinah, session.Api.World.Kinah);
		var inventory = NaturalIshalgenInventoryPolicy.Load(RealStaticData.RepoRoot(),
			session.Api.World.Inventory.Values.Select(i => i.ItemId).Concat(gear.ProtectedItemIds));
		var shop = new NaturalCoinGearSteps(session, fixture.DataManager.StaticData, gear, inventory);
		var geometry = BotNavigationGeometry.ForServerWorld(probe.Server.GetInstanceId(), Race.ASMODIANS);
		foreach (NaturalCoinGearPurchase purchase in gear.Purchases)
		{
			var npc = probe.Server.GetWorldMapInstance().GetNpcs(gear.VendorNpcId).Single(n => !n.IsDead());
			BotPosition contact = new(npc.GetX(), npc.GetY(), npc.GetZ(), 0);
			if (NaturalFlightPolicy.Distance(session.CurrentPosition, contact) > npc.GetObjectTemplate().GetTalkDistance() - 0.5f)
			{
				var route = geometry.GroundAround(220030000, contact, [2f, 3f])
					.OrderBy(p => NaturalFlightPolicy.Distance(session.CurrentPosition, p))
					.Select(p => geometry.FindJourneyPath(220030000, session.CurrentPosition, p)).First(p => p.Count > 0);
				await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing).CreateGroundPlan(route,
					session.CurrentPosition, session.Api.World.MovementSpeed!.Value), token);
				await session.SynchronizeAsync(token);
			}
			int vendor = await session.WaitForNpcAsync(gear.VendorNpcId, token);
			progress = await shop.PurchaseAsync(vendor, purchase.ItemId, progress, token);
			await shop.EquipAsync(purchase.ItemId, progress, token);
		}
		NaturalCoinGearSteps.VerifyRetainedLoadout(session.Api.World, retained);
		// Labelled prerequisite only: this probe does not claim to play Destiny. The complete natural
		// journey already proved it before attempt 24's CG/Haramel handoff rejected the historical earring.
		QuestState destiny = probe.Server.GetQuestStateList().GetQuestState(2900);
		destiny.SetStatus(QuestStatus.COMPLETE); destiny.SetQuestVar(0); destiny.SetCompleteCount(1);
		PacketSendUtility.SendPacket(probe.Server, new SM_QUEST_COMPLETED_LIST(1, [destiny]));
		await session.SynchronizeAsync(token);
		NaturalAltgardContract historical = NaturalAltgardContract.LoadLeg("l12");
		NaturalAltgardContract BoundHaramel() => NaturalAltgardContinuation.BindIncoming(historical, session.Api.World.CompletedQuestIds,
			session.Api.World.Inventory.Values.Select(i => new NaturalJourneyItem(i.ObjectId, i.ItemId, i.Count, i.EquipmentSlot)).ToArray(),
			NaturalAltgardContinuation.EquippedItemIds(session.Api.World));
		NaturalAltgardContract bound = BoundHaramel();
		Assert.Contains(120000833, bound.Haramel!.ProtectedItemIds);
		Assert.DoesNotContain(120001521, bound.Haramel.ProtectedItemIds);
		NaturalHaramelProgress handoff = NaturalHaramelProgress.Begin(session.CharacterId, fixture.Clock.NowMillis, session.Api.World, bound.Haramel);
		string receipt = path + ".haramel.json";
		handoff.Write(receipt);
		await session.QuitAsync(token);
		await session.WaitForReentryAsync(token);
		await session.ReloginExistingCharacterAsync(token);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Assert.Equal((ushort)3, session.Api.World.Inventory[earned.ObjectId].EquipmentSlot);
		Assert.Equal(1, session.Api.World.CompletedQuestCounts[gear.QuestId]);
		Assert.Equal(19, session.Api.World.Inventory.Values.Where(i => i.ItemId == gear.CoinItemId).Sum(i => i.Count));
		Assert.Equal(kinah, session.Api.World.Kinah);
		NaturalCoinGearSteps.VerifyRetainedLoadout(session.Api.World, retained);
		Assert.Equal("coin-gear-complete", NaturalCoinGearPolicy.Decide(gear,
			NaturalAltgardObservation.Observe(session.Api.World, session.CurrentPosition, coinGearProgress: progress)).Action);
		NaturalHaramelProgress cold = NaturalHaramelProgress.Read(receipt, session.CharacterId, fixture.Clock.NowMillis,
			session.Api.World, BoundHaramel());
		Assert.Equal(handoff.IncomingEquipment, cold.IncomingEquipment);
		Assert.False(session.Api.World.IsDead);
		Console.WriteLine("RC-11 approved owned staff equipped before loadout freeze; native reward, three purchases/four-coin debit and retained equipment verification survive ordinary endpoint relog. No weapon purchase.");
		Console.WriteLine("RC-11 labelled Q2900 prerequisite and Deyla earring: shared CG/Haramel handoff and cold receipt preserve the actual incoming gear, without the absent historical earring.");
		policy.AssertClean();
	}
}
