using System.Text.Json;
using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Dao;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.TestKit;
using Aion.GameServer.Utils;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	[SkippableFact]
	public async Task HaramelFreshClearProvesPackedKillsPostBossObjectsAndRecovery()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		using var policy = NewPolicy("HM05", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(25));
		CancellationToken token = timeout.Token;
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "hm05-recovery";
		string path = Path.Combine(RealStaticData.RepoRoot(), "run", $"{run}.hm05.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(path, run, "b01", "sim-player-226", virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 226, "Asimharpost", Race.ASMODIANS, trace, path);
		var dashboard = new LiveBotDashboardState();
		await using var host = new LiveBotDashboardHost(run, ["HM-05"], dashboard,
		int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880"));
		session.Dashboard = dashboard;
		if (host.Enabled) Console.WriteLine($"HM-05 dashboard: {host.Url}");
		var probe = new HaramelPostProbe(this, fixture, session, token, trace, dashboard);
		await probe.InitializeAsync();
		probe.Travel.PreserveNpcIds.Clear();
		probe.Quests.PreserveNpcIds.Clear();
		await probe.Travel.PortalAsync(730319, 300200000);
		probe.ObserveEntry(post: false, fresh: true);
		int firstCopy = probe.Server.GetInstanceId(), firstEntries = probe.Travel.EntriesUsed;
		Npc firstKilled = probe.Server.GetWorldMapInstance().GetNpcs(216897).Single();
		await probe.KillOriginalAsync(firstKilled, 0);
		await probe.ExitAsync();
		await probe.Travel.PortalAsync(730319, 300200000);
		Assert.Equal(firstCopy, probe.Server.GetInstanceId());
		Assert.Equal(firstEntries, probe.Travel.EntriesUsed);
		Assert.True(firstKilled.IsDead());
		Assert.DoesNotContain(probe.Server.GetWorldMapInstance().GetNpcs(216897), n => !n.IsDead());
		probe.ObserveEntry(post: false, fresh: false);
		await probe.ExitAsync();
		long expiry = probe.Progress.FreshEntryAfterMillis!.Value;
		await probe.WaitUntilAsync(expiry - probe.Rules.CleanupPeriodMillis - 2000);
		Assert.True(InstanceService.InstanceExists(300200000, firstCopy));
		await probe.WaitUntilAsync(expiry + 1);
		Assert.False(InstanceService.InstanceExists(300200000, firstCopy));
		foreach (int quest in new[] { 28504, 28505, 28510 }) await probe.Quests.AcceptAsync(quest);
		Assert.Equal(1, probe.Quests.Count(182212021));
		probe.UseProbeContract(probe.Rules.StaffObjectId);
		await probe.Travel.PortalAsync(730319, 300200000);
		Assert.Equal(firstEntries + 1, probe.Travel.EntriesUsed);
		probe.ObserveEntry(post: true, fresh: true);
		await probe.Quests.AcceptAsync(28511);
		Npc[] original = probe.Server.GetWorldMapInstance().GetNpcs().Where(n => probe.Rules.KillNpcIds.Contains(n.GetNpcId())).ToArray();
		Assert.Equal(74, original.Length);
		Assert.Equal(74, original.Select(n => n.GetObjectId()).Distinct().Count());
		Assert.All(original, n => Assert.False(n.IsDead()));
		// Protocol-only HP setup on the disposable probe's original sources. No replacements,
		// counter grants or natural-character edits. HM-04 separately proved normal HP/combat.
		foreach (Npc npc in original) npc.GetLifeStats().SetCurrentHp(1);
		var spawnLedger = original.Select(n => new { npc = n.GetNpcId(), obj = n.GetObjectId(), position = new BotPosition(n.GetX(), n.GetY(), n.GetZ(), 0) }).ToArray();
		Npc[] ordered = original.OrderBy(n => n.GetNpcId() == 216922 ? 1 : 0).ThenBy(n => n.GetZ()).ThenBy(n => n.GetNpcId()).ToArray();
		var boundaries = new List<object>();
		for (int index = 0; index < ordered.Length; index++)
		{
			Npc victim = ordered[index];
			int? item = victim.GetNpcId() switch { 216897 => 182212017, 216907 => 182212018, 216915 => 182212019, _ => null };
			await probe.KillOriginalAsync(victim, 28504, item);
			Assert.Equal(Math.Min(index + 1, 65), probe.PackedCount);
			if (index == 9)
			{
				int copy = probe.Server.GetInstanceId(), entries = probe.Travel.EntriesUsed;
				await probe.Travel.ReviveAtHeartAsync();
				probe.Progress = probe.Progress.ObserveRevive(probe.Now);
				Assert.Equal(10, probe.PackedCount);
				Assert.Equal(1, probe.Quests.Count(182212021));
				await probe.Travel.PillarAsync(upper: false);
				await probe.Travel.PortalAsync(730319, 300200000);
				Assert.Equal((copy, entries), (probe.Server.GetInstanceId(), probe.Travel.EntriesUsed));
				probe.ObserveEntry(post: true, fresh: false);
			}
			if (index + 1 is 63 or 64 or 65)
			{
				boundaries.Add(new { count = probe.PackedCount, var0 = probe.Quests.Counter(28504, 0), var1 = probe.Quests.Counter(28504, 1) });
				Assert.Equal(((index + 1) & 63, (index + 1) >> 6), (probe.Quests.Counter(28504, 0), probe.Quests.Counter(28504, 1)));
				if (index + 1 < 65)
				{
					await probe.ExitAsync();
					await probe.Quests.RefuseRewardAsync(28504);
					await probe.RelogWithProgressAsync(path + ".progress.json");
					Assert.Equal(index + 1, probe.PackedCount);
					await probe.Travel.PortalAsync(730319, 300200000);
					probe.ObserveEntry(post: true, fresh: false);
				}
			}
		}
		Assert.All(original, n => Assert.True(n.IsDead()));
		foreach (int item in new[] { 182212017, 182212018, 182212019 }) Assert.Equal(1, probe.Quests.Count(item));
		// A use before the three cart deaths cannot spend a processed-odella source.
		Npc processed = probe.Server.GetWorldMapInstance().GetNpcs(700953).First(n => !n.IsDead());
		await probe.Quests.SetupNearAsync(processed);
		await NaturalAltgardQuestSteps.UseObjectAsync(session, processed.GetObjectId(), null, token);
		Assert.Equal(0, probe.Quests.Counter(28510, 0));
		Assert.False(processed.IsDead());
		Npc[] carts = probe.Server.GetWorldMapInstance().GetNpcs(700950).Where(n => !n.IsDead()).ToArray();
		Assert.Equal(3, carts.Length);
		foreach (Npc cart in carts) await probe.KillOriginalAsync(cart, 28510);
		Assert.Equal(3, probe.Quests.Counter(28510, 0));
		foreach (Npc source in probe.Server.GetWorldMapInstance().GetNpcs(700953).Where(n => !n.IsDead()).ToArray())
		{
			await probe.Quests.SetupNearAsync(source);
			await NaturalAltgardQuestSteps.UseObjectAsync(session, source.GetObjectId(), null, token);
			await session.SynchronizeAsync(token);
		}
		Assert.Equal((QuestStatus.REWARD, 6), (probe.Server.GetQuestStateList().GetQuestState(28510).GetStatus(), probe.Quests.Counter(28510, 0)));
		await probe.PaySoupAsync();
		await probe.RelogWithProgressAsync(path + ".progress.json");
		Assert.Equal(0, probe.Quests.Count(182212022));
		Assert.Equal(0, probe.Quests.Counter(28511, 0));
		Assert.NotNull(probe.Progress.SoupPayment);
		probe.ObserveEntry(post: true, fresh: false);
		await probe.ReceiveSoupAsync();
		await probe.ExitAsync();
		await probe.Quests.ClaimAsync(28504);
		await probe.Quests.ClaimAsync(28505);
		Assert.Equal(1, probe.Quests.Count(113501720));
		await probe.Travel.PillarAsync(upper: true);
		await probe.Travel.FlyHubAsync(toHeart: false);
		await probe.ClaimCustomAsync(28510, 203560, selectReward: true);
		await probe.ClaimCustomAsync(28511, 798031, selectReward: false);
		await probe.Travel.BindAsync(700065, 451);
		await probe.RelogWithProgressAsync(path + ".progress.json");
		foreach (int quest in new[] { 28504, 28505, 28510, 28511 }) Assert.Equal(1, probe.Server.GetQuestStateList().GetQuestState(quest).GetCompleteCount());
		Assert.Equal(6, probe.Quests.Count(186000007)); // Q28509 belongs to the separate HM-03 proof.
		Assert.Equal(19, probe.Quests.Count(186000006));
		foreach (int item in new[] { 182212017, 182212018, 182212019, 182212021, 182212022, 182212023 }) Assert.Equal(0, probe.Quests.Count(item));
		Assert.Equal(1 + probe.CombatDeaths, probe.Progress.Revives);
		Assert.Equal(2, probe.Progress.Visits.Length);
		await File.WriteAllTextAsync(Path.ChangeExtension(path, ".summary.json"), JsonSerializer.Serialize(new
		{
			account = 226,
			firstCopy,
			firstEntries,
			expiry,
			spawnLedger,
			boundaries,
			probe.Kills,
			probe.Quests.SetupClears,
			probe.Quests.SetupRespawns,
			probe.CombatDeaths,
			probe.CombatAttempts,
			progress = probe.Progress,
			bronze = probe.Quests.Count(186000007),
			completed = new[] { 28504, 28505, 28510, 28511 },
			naturalCharacterChanged = false,
		}), token);
		Console.WriteLine("HM-05 PASS: natural empty expiry/fresh entry, all 74 original targets, packed 63/64/65/relog, four post quests, controlled death/native Heart recovery and retained budgets.");
		policy.AssertClean();
	}

	[SkippableFact]
	public async Task HaramelColdRestartPreservesPaidSoupAndRecoversThroughTheActualExit()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		string? phase = Environment.GetEnvironmentVariable("AION_SIM_HARAMEL_RESTART_PHASE");
		Skip.If(phase is null, "Use scripts/sim/probe-haramel-restart.ps1 for the two-process HM-05 probe.");
		Assert.Contains(phase, new[] { "prepare", "resume" });
		Assert.StartsWith("aion_gs_sim_ni08_", Environment.GetEnvironmentVariable("AION_SIM_NI08_DATABASE") ?? "");
		string receipt = Environment.GetEnvironmentVariable("AION_SIM_HARAMEL_RESTART_RECEIPT")!;
		using var policy = NewPolicy("HM05-cold", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "hm05-cold";
		string path = Path.Combine(RealStaticData.RepoRoot(), "run", $"{run}.hm05-cold.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(path, run, "b01", "sim-player-227", virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 227, "Asimharcold", Race.ASMODIANS, trace, path);
		var dashboard = new LiveBotDashboardState();
		await using var host = new LiveBotDashboardHost(run, ["HM-05 cold"], dashboard, int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880"));
		session.Dashboard = dashboard;
		if (host.Enabled) Console.WriteLine($"HM-05 cold dashboard: {host.Url}");
		var probe = new HaramelPostProbe(this, fixture, session, token, trace, dashboard);
		if (phase == "prepare")
		{
			await probe.InitializeAsync();
			probe.Travel.PreserveNpcIds.Clear();
			probe.Quests.PreserveNpcIds.Clear();
			foreach (int quest in new[] { 28504, 28505, 28510 }) await probe.Quests.AcceptAsync(quest);
			await probe.Travel.PortalAsync(730319, 300200000);
			probe.ObserveEntry(post: true, fresh: true);
			await probe.Quests.AcceptAsync(28511);
			await probe.KillOriginalAsync(probe.Server.GetWorldMapInstance().GetNpcs(216897).Single(), 28504, 182212017);
			await probe.PaySoupAsync();
			await probe.Travel.ReviveAtHeartAsync();
			probe.Progress = probe.Progress.ObserveRevive(probe.Now);
			await probe.Travel.PillarAsync(upper: false);
			await probe.Travel.PortalAsync(730319, 300200000);
			probe.ObserveEntry(post: true, fresh: false);
			probe.Progress.Write(receipt + ".progress.json");
			await session.QuitAsync(token);
			Assert.False(PlayerDAO.IsOnline(session.CharacterId));
			Assert.Equal(300200000, PlayerDAO.LoadPlayerCommonData(session.CharacterId)!.GetMapId());
			await File.WriteAllTextAsync(receipt, JsonSerializer.Serialize(new
			{
				CharacterId = session.CharacterId,
				ProcessId = Environment.ProcessId,
				MapId = 300200000,
				InstanceId = probe.Progress.CurrentVisit!.InstanceId,
				ElapsedMillis = fixture.Clock.NowMillis,
				StaffObjectId = probe.Rules.StaffObjectId,
				Entries = probe.Travel.EntriesUsed,
				PackedCount = probe.PackedCount
			}), token);
			Console.WriteLine("HM-05 cold prepare: actual partial kill/drop, cart work item, paid ginseng/no soup, one native revive and inside-instance quit saved.");
		}
		else
		{
			using JsonDocument saved = JsonDocument.Parse(File.ReadAllText(receipt));
			JsonElement row = saved.RootElement;
			Assert.NotEqual(row.GetProperty("ProcessId").GetInt32(), Environment.ProcessId);
			var list = await session.LoginCharacterListAsync(token);
			var character = Assert.Single(list.Get<List<IReadOnlyDictionary<string, object?>>>("characters"));
			Assert.Equal(row.GetProperty("CharacterId").GetInt32(), (int)character["objectId"]!);
			Assert.Equal(300200000, (int)character["mapId"]!);
			session.SelectCharacter((int)character["objectId"]!, "Asimharcold");
			await session.WaitForReentryAsync(token);
			await session.EnterWorldAsync(token);
			await session.SynchronizeAsync(token);
			Assert.Equal(220030000, probe.Server.GetWorldId());
			Assert.True(NaturalFlightPolicy.Distance(session.CurrentPosition, new(2907.624f, 1464.1887f, 252.59264f, 36)) < 1);
			probe.UseProbeContract(row.GetProperty("StaffObjectId").GetInt32());
			probe.Travel.PreserveNpcIds.Clear();
			probe.Quests.PreserveNpcIds.Clear();
			probe.Progress = NaturalHaramelProgress.Read(receipt + ".progress.json", session.CharacterId, probe.Now, session.Api.World, probe.Leg);
			Assert.Equal(1, probe.Progress.Revives);
			Assert.NotNull(probe.Progress.StallBudget);
			Assert.Equal(1, probe.PackedCount);
			Assert.Equal(1, probe.Quests.Count(182212017));
			Assert.Equal(1, probe.Quests.Count(182212021));
			Assert.Equal(0, probe.Quests.Count(182212022));
			Assert.NotNull(probe.Progress.SoupPayment);
			await probe.Travel.PortalAsync(730319, 300200000);
			Assert.Equal(row.GetProperty("Entries").GetInt32() + 1, probe.Travel.EntriesUsed);
			probe.ObserveEntry(post: true, fresh: true);
			await probe.KillOriginalAsync(probe.Server.GetWorldMapInstance().GetNpcs(216907).Single(), 28504, 182212018);
			Assert.Equal(2, probe.PackedCount);
			await probe.ReceiveSoupAsync();
			await probe.ExitAsync();
			await probe.Travel.PillarAsync(upper: true);
			await probe.Travel.FlyHubAsync(toHeart: false);
			await probe.ClaimCustomAsync(28511, 798031, selectReward: false);
			await probe.RelogWithProgressAsync(receipt + ".resumed-progress.json");
			Assert.Equal(2, probe.PackedCount);
			Assert.Equal(3, probe.Quests.Count(186000007));
			Assert.Equal(1, probe.Progress.Revives);
			Assert.Equal(2, probe.Progress.Visits.Length);
			await File.WriteAllTextAsync(Path.ChangeExtension(path, ".summary.json"), JsonSerializer.Serialize(new
			{
				savedProcess = row.GetProperty("ProcessId").GetInt32(),
				resumedProcess = Environment.ProcessId,
				fallback = session.PacketHistory.Where(p => p.PacketType == typeof(SM_PLAYER_SPAWN)).Select(p => p.Get<int>("worldId")).ToArray(),
				progress = probe.Progress,
				packed = probe.PackedCount,
				soupCompleted = true,
				naturalCharacterChanged = false
			}), token);
			Console.WriteLine("HM-05 cold resume PASS: actual configured Altgard exit, original counters/items/payment/budgets retained, fresh ordinary entry spends one, remaining soup delivery/hand-in/relog without another payment.");
		}
		policy.AssertClean();
	}

	private sealed class HaramelPostProbe(SimulationFastScenarioTests owner, SimulationWorldFixture fixture, SimulationL0Session session,
	CancellationToken token, BotActionTraceWriter trace, LiveBotDashboardState dashboard)
	{
		public HaramelTravelProbe Travel { get; } = new(owner, fixture, session, token);
		public HaramelQuestProbe Quests { get; } = new(owner, fixture, session, token);
		public Aion.GameServer.Model.GameObjects.Players.Player Server => fixture.World.GetPlayer(session.CharacterId);
		public NaturalAltgardContract Leg { get; private set; } = NaturalAltgardContract.LoadLeg("l12");
		public NaturalHaramel Rules => Leg.Haramel!;
		public NaturalHaramelProgress Progress { get; set; } = null!;
		public int Kills { get; private set; }
		public int CombatDeaths { get; private set; }
		public List<object> CombatAttempts { get; } = [];
		public long Now => fixture.Epoch.ToUnixTimeMilliseconds() + fixture.Clock.NowMillis;
		public int PackedCount => NaturalQuestProgress.KillCount(session.Api.World.Quests[28504], 0, 65);
		private BotNavigationGeometry Geometry() => BotNavigationGeometry.ForServerWorld(Server.GetInstanceId(), Race.ASMODIANS);
		public void UseProbeContract(int staff)
		{
			// Substitute only disposable identity/prerequisites; production validation stays unchanged.
			Leg = Leg with { Start = Leg.Start with { CompletedQuestIds = [28507] }, Haramel = Rules with { StaffObjectId = staff } };
			Travel.PreserveNpcIds.UnionWith(Rules.KillNpcIds.Concat(new[] { 700950 }));
			Quests.PreserveNpcIds.UnionWith(Rules.KillNpcIds.Concat(new[] { 700950 }));
		}
		public async Task InitializeAsync()
		{
			await Travel.InitializeAsync();
			foreach ((int item, long slot) in new (int, long)[]{(125004139,4),(110551139,8),(111501065,16),(114501726,32),(112501015,2048),(113501074,4096),
		(120001521,64),(120001132,128),(122001664,256),(122000871,512),(121000751,1024),(123001109,65536)})
			{
				Assert.Equal(0, ItemService.AddItem(Server, item, 1, allowInventoryOverflow: true));
				Assert.NotNull(Server.GetEquipment().EquipItem(Server.GetInventory().GetItems().Single(i => i.GetItemId() == item).GetObjectId(), slot));
			}
			foreach ((int item, long count) in new (int, long)[] { (111101650, 1), (188053787, 1), (186000006, 19) })
				Assert.Equal(0, ItemService.AddItem(Server, item, count, allowInventoryOverflow: true));
			foreach (NaturalHelpTopUp supply in NaturalHelpItemSupply.Plan(24, new Dictionary<int, long>()))
			{
				NaturalHelpItemSupply.RequireApproved(supply.ItemId, supply.Count);
				Assert.Equal(0, ItemService.AddItem(Server, supply.ItemId, supply.Count, allowInventoryOverflow: true));
			}
			QuestState? prerequisite = Server.GetQuestStateList().GetQuestState(28507);
			if (prerequisite == null) Assert.True(Server.GetQuestStateList().AddQuest(28507, new QuestState(28507, QuestStatus.COMPLETE)));
			else prerequisite.SetStatus(QuestStatus.COMPLETE);
			PacketSendUtility.SendPacket(Server, new SM_QUEST_COMPLETED_LIST(0, [Server.GetQuestStateList().GetQuestState(28507)]));
			Console.WriteLine("HM-05 labelled probe-only class/level/loadout and Q28507 prerequisite. Post-boss counters/items/rewards use real packets; no natural changes.");
			await Travel.SetupFortressAsync();
			await Travel.FlyHubAsync(toHeart: true);
			await Travel.BindAsync(700067, 813);
			await Travel.PillarAsync(upper: false);
			await session.SynchronizeAsync(token);
			UseProbeContract(session.Api.World.Inventory.Values.Single(i => i.ItemId == 101501357).ObjectId);
			Progress = NaturalHaramelProgress.Begin(session.CharacterId, Now, session.Api.World, Rules) with
			{ StallBudget = new("hm05-original-budget", TimeSpan.FromMilliseconds(fixture.Clock.NowMillis), TimeSpan.FromMilliseconds(fixture.Clock.NowMillis)) };
		}
		public void ObserveEntry(bool post, bool fresh)
		{
			Assert.Equal(Server.GetInstanceId(), session.Api.World.InstanceId);
			Progress = Progress.ObserveEntry(session.Api.World.InstanceId!.Value, Server.GetWorldMapInstance().GetNpcs(799522).Single().GetObjectId(),
				session.Api.World.InstanceEntries[(session.CharacterId, 46)], Now, post, fresh);
		}
		public async Task ExitAsync()
		{
			await Quests.SetupNearAsync(Server.GetWorldMapInstance().GetNpcs(730320).Single());
			await Travel.PortalAsync(730320, 220030000);
			Progress = Progress.ObserveExit(Now, Rules);
		}
		public async Task WaitUntilAsync(long deadline)
		{
			while (Now < deadline) await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Min(1000, deadline - Now)), token);
			await session.SynchronizeAsync(token);
		}
		public async Task RelogWithProgressAsync(string path)
		{
			Progress = Progress with { LastObservedAtMillis = Now };
			Progress.Write(path);
			var original = Progress;
			// Capture the latest ordinary movement endpoint for the fixture's relog assertion.
			// ExecuteMovementAsync updates CurrentPosition, but its earlier setup expectation stays.
			await session.MoveToPositionAsync(session.CurrentPosition, token);
			await session.QuitAsync(token);
			await session.WaitForReentryAsync(token);
			await session.ReloginAndVerifyPersistenceAsync(token);
			await session.EnterWorldAsync(token);
			await session.SynchronizeAsync(token);
			Progress = NaturalHaramelProgress.Read(path, session.CharacterId, Now, session.Api.World, Leg);
			Assert.Equal(original.StartedAtMillis, Progress.StartedAtMillis);
			Assert.Equal(original.Revives, Progress.Revives);
			Assert.Equal(original.StallBudget, Progress.StallBudget);
			Assert.Equal(original.SoupPayment, Progress.SoupPayment);
		}
		public async Task KillOriginalAsync(Npc npc, int quest, int? item = null)
		{
			Assert.False(npc.IsDead());
			for (int attempt = 0; attempt < 4 && !npc.IsDead(); attempt++)
			{
				Console.WriteLine($"HM-05 labelled HP=1 protocol source {npc.GetNpcId()}/{npc.GetObjectId()}, no respawn/counter write.");
				npc.GetLifeStats().SetCurrentHp(1);
				if (Server.GetWorldId() != 300200000)
				{
					int sickness = session.Api.World.VisibleEffects?.Where(e => e.SkillId == 8291).Select(e => e.RemainingMillis).DefaultIfEmpty(0).Max() ?? 0;
					if (sickness > 0) await session.AdvanceAsync(TimeSpan.FromMilliseconds(sickness + 100), token);
					await Travel.PillarAsync(upper: false);
					await Travel.PortalAsync(730319, 300200000);
					Assert.Equal(Progress.CurrentVisit!.InstanceId, Server.GetInstanceId());
					ObserveEntry(Progress.CurrentVisit.PostBossQuests, fresh: false);
				}
				// Ordinary rest/healing at the checked entry floor before each protocol cast.
				session.Api.World.BeginWorldReload();
				await owner.TeleportForSetupAsync(session, Server, 300200000, 172, 20, 144.22548f, token, Server.GetInstanceId());
				session.AcceptTeleportPosition();
				await session.SynchronizeAsync(token);
				TimeSpan remaining = new ushort[] { 4016, 4015, 4014, 4013, 4012 }.Where(id => session.Api.World.Skills.ContainsKey(id))
				.Select(id => session.Api.Timing.TimeUntilCast(id)).DefaultIfEmpty(TimeSpan.Zero).Max();
				if (remaining > TimeSpan.Zero) await session.AdvanceAsync(remaining + TimeSpan.FromMilliseconds(1), token);
				if (attempt > 0) await session.AdvanceAsync(TimeSpan.FromSeconds(15), token); // OD-14: let the patrol move before a fresh approach.
				var runtime = new NaturalJourneyRuntime(RealStaticData.RepoRoot(), "SIM-hm05", fixture.Seed, fixture.DataManager.StaticData,
				() => fixture.Clock.NowMillis, fixture.Epoch, Geometry, _ => Task.FromResult(false), () => { }, () => Array.Empty<object>(), trace, dashboard);
				NaturalCombatDiagnosticResult result = await new NaturalIshalgenJourney(session, runtime, new()).RunObservedCombatAsync(async _ =>
				{
					await Quests.SetupNearAsync(npc, combat: true, closeCombat: attempt > 0);
					return npc.GetObjectId();
				}, token);
				CombatAttempts.Add(new { npc = npc.GetNpcId(), obj = npc.GetObjectId(), attempt, result, at = new BotPosition(npc.GetX(), npc.GetY(), npc.GetZ(), 0) });
				Console.WriteLine($"HM-05 actual source attempt {npc.GetNpcId()}/{npc.GetObjectId()} {attempt}: {result}");
				CombatDeaths += result.Deaths;
				for (int death = 0; death < result.Deaths; death++) Progress = Progress.ObserveRevive(Now);
				await NaturalMovieGate.FinishAsync(session, token);
				await session.SynchronizeAsync(token);
			}
			Assert.True(npc.IsDead(), $"Original source {npc.GetNpcId()}/{npc.GetObjectId()} remains unresolved after four observed approaches.");
			Kills++;
			if (item is int drop)
			{
				await Quests.SetupNearAsync(npc);
				bool looted = await NaturalAltgardQuestSteps.LootItemAsync(session, npc.GetObjectId(), drop, token);
				await session.SynchronizeAsync(token);
				Console.WriteLine($"HM-05 actual overseer drop {drop}: looted={looted}, held={Quests.Count(drop)}");
			}
			Console.WriteLine($"HM-05 actual kill {npc.GetNpcId()}/{npc.GetObjectId()}, Q{quest}, packed={(session.Api.World.Quests.ContainsKey(28504) ? PackedCount : 0)}");
		}
		private async Task SelectAsync(int npc, int quest, int action)
		{
			// Java USE_OBJECT=-1 is produced by CM_SHOW_DIALOG, not CM_DIALOG_SELECT's ushort.
			if (action == DialogAction.USE_OBJECT) await NaturalDialogProtocol.OpenAsync(session, npc, token);
			else await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc, checked((ushort)action), questId: quest), token);
			await session.SynchronizeAsync(token);
		}
		public async Task PaySoupAsync()
		{
			int cauldron = await Quests.NpcAsync(730359);
			await SelectAsync(cauldron, 28511, DialogAction.USE_OBJECT);
			int start = session.PacketHistory.Count;
			await SelectAsync(cauldron, 28511, DialogAction.CHECK_USER_HAS_QUEST_ITEM);
			Assert.Contains(session.PacketHistory.Skip(start), p => p.PacketType == typeof(SM_DIALOG_WINDOW) && p.Get<ushort>("dialogPageId") == 10001);
			await session.SendPacketAsync(session.Api.CloseDialog(cauldron), token);
			await Quests.CollectAsync(28511, 700954, 182212022, 5);
			cauldron = await Quests.NpcAsync(730359);
			await SelectAsync(cauldron, 28511, DialogAction.USE_OBJECT);
			start = session.PacketHistory.Count;
			long before = Quests.Count(182212022);
			await SelectAsync(cauldron, 28511, DialogAction.CHECK_USER_HAS_QUEST_ITEM);
			Assert.Contains(session.PacketHistory.Skip(start), p => p.PacketType == typeof(SM_DIALOG_WINDOW) && p.Get<ushort>("dialogPageId") == 1352);
			Progress = Progress.ObserveSoupPayment(cauldron, before, Quests.Count(182212022), 1352, Now);
			Assert.Equal(0, Quests.Counter(28511, 0));
			Assert.Equal(0, Quests.Count(182212023));
			await session.SendPacketAsync(session.Api.CloseDialog(cauldron), token);
		}
		public async Task ReceiveSoupAsync()
		{
			Assert.NotNull(Progress.SoupPayment);
			Assert.Equal(0, Quests.Count(182212022));
			Assert.Equal(0, Quests.Count(182212023));
			int cauldron = await Quests.NpcAsync(730359);
			await SelectAsync(cauldron, 28511, DialogAction.USE_OBJECT);
			await SelectAsync(cauldron, 28511, DialogAction.SETPRO2);
			Assert.Equal(1, Quests.Count(182212023));
			Assert.Equal(QuestStatus.REWARD, Server.GetQuestStateList().GetQuestState(28511).GetStatus());
		}
		public async Task ClaimCustomAsync(int quest, int npcId, bool selectReward)
		{
			int npc = await Quests.NpcAsync(npcId);
			await SelectAsync(npc, quest, DialogAction.USE_OBJECT);
			if (selectReward) await SelectAsync(npc, quest, DialogAction.SELECT_QUEST_REWARD);
			await SelectAsync(npc, quest, DialogAction.SELECTED_QUEST_NOREWARD);
			await session.SendPacketAsync(session.Api.CloseDialog(npc), token);
			Assert.Equal(QuestStatus.COMPLETE, Server.GetQuestStateList().GetQuestState(quest).GetStatus());
		}
	}
}
