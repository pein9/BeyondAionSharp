using System.Text.Json;
using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
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
	/// <summary>ND-05: free account 220 uses normal journey combat, actual campaign spawn and unmodified Hellion.</summary>
	[SkippableFact]
	public async Task DestinyHellionFightsAtNormalHpAndKillTeleportClearsItsCorpse()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		using var policy = NewPolicy("ND05", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		string root = RealStaticData.RepoRoot();
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "nd05-combat";
		string path = Path.Combine(root, "run", $"{run}-nd05.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(path, run, "b01", "sim-player-220", virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 220, "Asimdestfight", Race.ASMODIANS, trace, path);
		var dashboard = new LiveBotDashboardState();
		await using var dashboardHost = new LiveBotDashboardHost(run, ["ND-05"], dashboard,
			int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880"));
		session.Dashboard = dashboard;
		if (dashboardHost.Enabled) Console.WriteLine($"ND-05 dashboard: {dashboardHost.Url}");
		var probe = new DestinyProbe(this, fixture, session, token);
		await probe.InitializeAsync(100000);
		// Controlled probe gear mirrors altgard-l10's seven worn items. Nothing is bought or supplied to the natural character.
		foreach ((int itemId, long slot) in new (int, long)[]
		{
			(122000869, 256), (113100773, 4096), (114100795, 32), (110101250, 8),
			(121000749, 1024), (111100763, 16), (101500498, 3),
		})
		{
			Assert.Equal(0, ItemService.AddItem(probe.Server, itemId, 1, allowInventoryOverflow: true));
			var owned = probe.Server.GetInventory().GetItems().Last(item => item.GetItemId() == itemId);
			Assert.NotNull(probe.Server.GetEquipment().EquipItem(owned.GetObjectId(), slot));
		}
		foreach (NaturalHelpTopUp supply in NaturalHelpItemSupply.Plan(24, new Dictionary<int, long>()))
		{
			NaturalHelpItemSupply.RequireApproved(supply.ItemId, supply.Count);
			Assert.Equal(0, ItemService.AddItem(probe.Server, supply.ItemId, supply.Count, allowInventoryOverflow: true));
		}
		await session.SynchronizeAsync(token);
		HashSet<int> learned = session.Api.World.Skills.Keys.ToHashSet();
		Assert.Contains(4204, learned); Assert.Contains(3951, learned); Assert.Contains(1842, learned);
		await probe.SetupAtFortressAsync();
		int obelisk = await probe.WalkNpcAsync(700065);
		Assert.True((await new NaturalServiceSteps(session).BindAsync(obelisk, session.Api.World.Objects[obelisk].Position,
			220030000, probe.Leg.Bind!.Price, 5, token)).IsDone);
		await probe.SetupVarAsync(4); // Only the prerequisite is prepared; entry, movie, equipment, spawn and fight are real.
		var runtime = new NaturalJourneyRuntime(root, "SIM-nd05", fixture.Seed, fixture.DataManager.StaticData,
			() => fixture.Clock.NowMillis, fixture.Epoch,
			() => BotNavigationGeometry.ForServerWorld(probe.Server.GetInstanceId(), Race.ASMODIANS),
			_ => Task.FromResult(false), policy.AssertClean, () => policy.SnapshotProblems(), trace, dashboard);
		var results = new List<NaturalCombatDiagnosticResult>();
		int enemyObjectId = 0, killTeleportIndex = -1, corpseSweeps = 0;
		for (int attempt = 0; attempt < 20; attempt++)
		{
			if (probe.Server.GetWorldId() == 220030000) await RecoverDestinyProbeAsync(probe);
			foreach (string key in new[] { "movie", "stone" }) await probe.TalkAsync(key);
			int skuld = await probe.WalkNpcAsync(204264);
			await NaturalAltgardQuestSteps.EquipDestinyStigmaAsync(session, probe.Leg.Destiny!, skuld, token);
			Assert.Contains(11504, session.Api.World.Skills.Keys);
			NaturalCombatDiagnosticResult result = await new NaturalIshalgenJourney(session, runtime, new()).RunObservedCombatAsync(async _ =>
			{
				await probe.TalkAsync("spawn");
				var enemy = probe.Server.GetWorldMapInstance().GetNpcs(204263).Single(npc => !npc.IsDead());
				Assert.Equal(3169, enemy.GetLifeStats().GetMaxHp());
				Assert.Equal(3169, enemy.GetLifeStats().GetCurrentHp());
				enemyObjectId = await session.WaitForNpcAsync(204263, token);
				return enemyObjectId;
			}, token, _ => { corpseSweeps++; return Task.CompletedTask; });
			results.Add(result);
			Console.WriteLine($"ND-05 normal fight {attempt + 1}: {JsonSerializer.Serialize(result)}, map {probe.Server.GetWorldId()}, var {probe.Var}");
			if (result.Killed) break;
			if (probe.Server.GetWorldId() != 220030000) await probe.ReturnAsync();
			AssertDestinyCleanup(probe, session);
		}
		await File.WriteAllTextAsync(Path.ChangeExtension(path, ".summary.json"), JsonSerializer.Serialize(results), token);
		Assert.Contains(results, result => result.Killed);
		Assert.True(results.Last().ElapsedMillis < 300000, "Kill must precede the actual five-minute despawn.");
		Assert.Equal((220010000, 9), (probe.Server.GetWorldId(), probe.Var));
		AssertDestinyCleanup(probe, session);
		Assert.All(learned, id => Assert.Contains(id, session.Api.World.Skills.Keys));
		Assert.Equal(0, corpseSweeps);
		await File.WriteAllTextAsync(Path.ChangeExtension(path, ".packets.json"), JsonSerializer.Serialize(
			session.PacketHistory.TakeLast(100).Select(packet => new { type = packet.PacketType.Name, fields = packet.Fields })), token);
		Assert.Contains(session.PacketHistory, packet => packet.PacketType == typeof(SmAttackStatus) &&
			packet.Get<int>("objectId") == session.CharacterId && packet.Get<byte>("typeId") == 5 && packet.Get<int>("writtenValue") > 1);
		Assert.DoesNotContain(enemyObjectId, session.Api.World.Objects.Keys);
		Assert.DoesNotContain(enemyObjectId, session.Api.World.LootStatuses.Keys);
		killTeleportIndex = session.PacketHistory.ToList().FindLastIndex(packet => packet.PacketType == typeof(SM_TELEPORT_LOC) && packet.Get<int>("mapId") == 220010000);
		Assert.True(killTeleportIndex >= 0);
		Assert.DoesNotContain(session.PacketHistory.Skip(killTeleportIndex), packet => packet.PacketType == typeof(SM_LOOT_ITEMLIST) && packet.Get<int>("targetObjectId") == enemyObjectId);
		await probe.TalkAsync("skuld-return"); // Reacquire the outdoor NPC; no old-instance corpse action.
		Assert.Equal(10, probe.Var);
		policy.AssertClean();
	}
}
