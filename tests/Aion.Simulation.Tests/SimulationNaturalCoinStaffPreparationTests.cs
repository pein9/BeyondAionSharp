using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Services.Items;
using Aion.GameServer.TestKit;

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
		Assert.Equal(0, ItemService.AddItem(probe.Server, gear.CoinItemId, gear.IncomingCoins, allowInventoryOverflow: true));
		await session.SynchronizeAsync(token);
		BotInventoryItem old = session.Api.World.Inventory.Values.Single(i => i.ItemId == 101501355);
		BotInventoryItem earned = session.Api.World.Inventory.Values.Single(i => i.ItemId == gear.StaffItemId);
		await session.SendPacketAsync(session.Api.Equip(0, NaturalGearPolicy.MainHand, old.ObjectId), token);
		await session.SynchronizeAsync(token);
		Assert.Equal((ushort)3, session.Api.World.Inventory[old.ObjectId].EquipmentSlot);
		Assert.NotEqual((ushort)3, session.Api.World.Inventory[earned.ObjectId].EquipmentSlot);
		Console.WriteLine("RC-11 free probe 230: labelled level/loadout/funds; old level-16 staff equipped and earned level-21 staff owned in the cube, matching attempt 22.");
		long kinah = session.Api.World.Kinah;
		await NaturalCoinGearSteps.EnsureRetainedStaffEquippedAsync(session, gear, token);
		Assert.Equal((ushort)3, session.Api.World.Inventory[earned.ObjectId].EquipmentSlot);
		Assert.NotEqual((ushort)3, session.Api.World.Inventory[old.ObjectId].EquipmentSlot);
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
		await session.QuitAsync(token);
		await session.WaitForReentryAsync(token);
		await session.ReloginExistingCharacterAsync(token);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Assert.Equal((ushort)3, session.Api.World.Inventory[earned.ObjectId].EquipmentSlot);
		Assert.Equal(1, session.Api.World.CompletedQuestCounts[gear.QuestId]);
		Assert.Equal(23, session.Api.World.Inventory.Values.Where(i => i.ItemId == gear.CoinItemId).Sum(i => i.Count));
		Assert.False(session.Api.World.IsDead);
		Console.WriteLine("RC-11 approved owned staff equipped normally; native one-completion/five-coin receipt and same equipped object survive ordinary relog, without a weapon purchase.");
		policy.AssertClean();
	}
}
