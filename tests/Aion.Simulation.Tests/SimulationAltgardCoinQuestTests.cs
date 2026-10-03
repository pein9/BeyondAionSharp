using System.Text.Json;
using Aion.Bots.Api;
using Aion.Bots.Dashboard;
using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.Protocol;
using Aion.Bots.Reflexes;
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
	/// <summary>CG-02: free account 221, normal level-24 combat, hub/pillar flights, repeat reset and Heart recovery.</summary>
	[SkippableFact]
	public async Task CoinQuestFlightsNormalSpiritCombatRewardRepeatResetAndHeartRecovery()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int map = 220030000, questId = 2293;
		using var policy = NewPolicy("CG02", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		string root = RealStaticData.RepoRoot();
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "cg02-quest";
		string tracePath = Path.Combine(root, "run", $"{run}-cg02.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(tracePath, run, "b01", "sim-player-221", virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 221, "Asimcoinquest", Race.ASMODIANS, trace, tracePath);
		var dashboard = new LiveBotDashboardState();
		await using var dashboardHost = new LiveBotDashboardHost(run, ["CG-02"], dashboard,
			int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880"));
		session.Dashboard = dashboard;
		if (dashboardHost.Enabled) Console.WriteLine($"CG-02 dashboard: {dashboardHost.Url}");
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("cg");
		NaturalCoinGear gear = leg.CoinGear!;
		var plans = NaturalAltgardContract.LoadPlans("cg");
		var objective = NaturalTemplateObjective.From(plans)[questId];
		session.BeginStep("s00", "controlled-level-24-probe-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Aion.GameServer.Model.GameObjects.Players.Player Player() => fixture.World.GetPlayer(session.CharacterId);
		var player = Player();
		Assert.True(ClassChangeService.SetClass(player, PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		player.GetCommonData().SetLevel(24);
		SkillLearnService.LearnNewSkills(player, 1, 24);
		player.GetInventory().IncreaseKinah(536193);
		// Controlled probe loadout mirrors the actual incoming snapshot; no natural-character grant or purchase.
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
		Assert.Equal(0, ItemService.AddItem(player, gear.CoinItemId, 18, allowInventoryOverflow: true));
		Assert.Equal(0, ItemService.AddItem(player, gear.SealedBundleId, 1, allowInventoryOverflow: true));
		foreach (NaturalHelpTopUp supply in NaturalHelpItemSupply.Plan(24, new Dictionary<int, long>()))
		{
			NaturalHelpItemSupply.RequireApproved(supply.ItemId, supply.Count);
			Assert.Equal(0, ItemService.AddItem(player, supply.ItemId, supply.Count, allowInventoryOverflow: true));
		}
		var instance = fixture.World.GetWorldMap(map).GetMainWorldMapInstance();
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(instance.GetInstanceId(), Race.ASMODIANS);
		BotTravelPlanner planner = BotTravelPlanner.For(map, geometry, fixture.DataManager.StaticData)!;
		var runtime = new NaturalJourneyRuntime(root, "SIM-cg02", fixture.Seed, fixture.DataManager.StaticData,
			() => fixture.Clock.NowMillis, fixture.Epoch, () => geometry, _ => Task.FromResult(false), policy.AssertClean,
			() => policy.SnapshotProblems(), trace, dashboard);
		var fights = new List<NaturalCombatDiagnosticResult>();
		int sequence = 0, freeFlights = 0, killedAtNormalHp = 0, cleared = 0;
		long? lastTakeoff = null;
		long Coins() => session.Api.World.Inventory.Values.Where(i => i.ItemId == gear.CoinItemId).Sum(i => i.Count);
		int Packed() => session.Api.World.Quests[questId].StepAndFlags;
		int Counter(int variable) => (Packed() >> (variable * 6)) & 63;
		BotPosition At(Aion.GameServer.Model.GameObjects.Npc npc) => new(npc.GetX(), npc.GetY(), npc.GetZ(), 0);
		void ClearNeighbours(IEnumerable<BotPosition> points, bool preserveSpirits = true)
		{
			BotPosition[] route = points.ToArray();
			var neighbours = instance.GetNpcs().Where(n => !n.IsDead() &&
				(!preserveSpirits || n.GetNpcId() is not (210576 or 210578 or 210523)) &&
				NaturalHostility.IsAggressive(n.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
				route.Any(p => MathF.Pow(n.GetX() - p.X, 2) + MathF.Pow(n.GetY() - p.Y, 2) <= 900)).ToArray();
			foreach (var npc in neighbours) fixture.World.Despawn(npc);
			cleared += neighbours.Length;
			if (neighbours.Length > 0) Console.WriteLine($"CG-02 labelled neighbour setup: {neighbours.Length} aggressive NPCs cleared, quest spirits preserved={preserveSpirits}.");
		}
		BotPosition Ground(BotPosition at) => geometry.GroundAround(map, at, [2f, 3f, 5f, 8f]).First();
		BotPosition fortress = new(1658.57f, 1818.39f, 253.72f, 0);
		ClearNeighbours([fortress]);
		session.Api.World.BeginWorldReload();
		await TeleportForSetupAsync(session, player, map, fortress.X, fortress.Y, fortress.Z, token);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
		Assert.DoesNotContain(11504, session.Api.World.Skills.Keys);
		Assert.Equal(24, session.Api.World.Level);
		int staffObject = session.Api.World.Inventory.Values.Single(i => i.ItemId == gear.StaffItemId).ObjectId;

		async Task WalkAsync(string label, BotPosition to)
		{
			session.BeginStep($"s{++sequence:00}", label);
			BotPosition from = session.CurrentPosition;
			IReadOnlyList<BotPosition> route = planner.PlanJourney(map, from, to, 24, [])?.Route ?? geometry.FindJourneyPath(map, from, to);
			Assert.True(route.Count > 0 || NaturalFlightPolicy.Distance(from, to) <= 2, $"{label}: no checked route ({BotNavMeshRouter.LastOutcome}) {from} -> {to}");
			ClearNeighbours(route.Append(from).Append(to));
			if (route.Count > 0) await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing)
				.CreateGroundPlan(route, from, session.Api.World.MovementSpeed!.Value), token);
			await session.SynchronizeAsync(token);
			Assert.True(NaturalFlightPolicy.Distance(AtPosition(), to) <= 5, $"{label}: server endpoint misses {to}");
			Console.WriteLine($"CG-02 {label}: {route.Count} checked ground points, endpoint {session.CurrentPosition}.");
		}
		BotPosition AtPosition() => new(player.GetX(), player.GetY(), player.GetZ(), 0);
		async Task<int> WalkNpcAsync(int npcId, float? serviceRange = null)
		{
			var npc = instance.GetNpcs(npcId).First(n => !n.IsDead());
			float range = MathF.Min(npc.GetObjectTemplate().GetTalkDistance(), serviceRange ?? float.MaxValue);
			// The upper platform has small geometry seams. Already being within real talk range
			// is sufficient; otherwise select a checked reachable point inside that range.
			if (NaturalFlightPolicy.Distance(session.CurrentPosition, At(npc)) > range - 0.5f)
			{
				BotPosition[] approaches = geometry.GroundAround(map, At(npc), [MathF.Max(1, range - 1), 2f, 3f])
					.Where(p => NaturalFlightPolicy.Distance(p, At(npc)) <= range - 0.5f)
					.OrderBy(p => NaturalFlightPolicy.Distance(session.CurrentPosition, p)).ToArray();
				BotPosition approach = approaches.First(p => geometry.FindJourneyPath(map, session.CurrentPosition, p).Count > 0);
				await WalkAsync($"npc-{npcId}", approach);
			}
			return await session.WaitForNpcAsync(npcId, token);
		}
		async Task PillarAsync(string label, BotPosition destination)
		{
			session.BeginStep($"s{++sequence:00}", label);
			BotPosition from = session.CurrentPosition;
			ClearNeighbours([from, destination]);
			long wait = NaturalFlightPolicy.RestoreMillis(session.Api.World.CurrentFlightTime, session.Api.World.MaxFlightTime, session.Api.World.MaxFlightTime);
			if (lastTakeoff is long last) wait = Math.Max(wait, last + NaturalFlightPolicy.TakeoffReuseMillis - fixture.Clock.NowMillis);
			if (wait > 0) await session.AdvanceAsync(TimeSpan.FromMilliseconds(wait + 100), token);
			await session.SynchronizeAsync(token);
			NaturalFlightRoute route = new([], 0, "No checked pillar route.");
			foreach (float cruise in new[] { MathF.Max(from.Z, destination.Z) + 12, 360f, 400f, 430f })
			{
				route = NaturalFlightProtocol.Plan(geometry, map, from, destination, cruise);
				if (route.IsUsable) break;
			}
			Assert.True(route.IsUsable, route.Refusal);
			lastTakeoff = fixture.Clock.NowMillis;
			float speed = await NaturalFlightProtocol.TakeOffAsync(session, token);
			var zones = NaturalFlyZone.Load(Path.Combine(root, "game-server/data/static_data/zones/zones_220030000.xml"));
			NaturalFlightDecision allowed = NaturalFlightPolicy.CanFly(NaturalFlightProtocol.ToPlan(route, from, speed), session.Api.World.CurrentFlightTime, zones);
			Assert.True(allowed.Allowed, allowed.Reason);
			await NaturalFlightProtocol.FlyAsync(session, map, from, route.Waypoints, speed, token);
			await NaturalFlightProtocol.LandAsync(session, token);
			await session.SynchronizeAsync(token);
			Assert.False(player.IsFlying());
			freeFlights++;
			Console.WriteLine($"CG-02 {label}: {route.Meters:F1} m free flight, FP={session.Api.World.CurrentFlightTime}.");
		}
		BotPosition upper = Ground(new(leg.PillarFlight!.Upper[0], leg.PillarFlight.Upper[1], leg.PillarFlight.Upper[2], 0));
		BotPosition lower = Ground(new(leg.PillarFlight.Lower[0], leg.PillarFlight.Lower[1], leg.PillarFlight.Lower[2], 0));
		NaturalAirlineJourney journey = Assert.IsType<NaturalAirlineJourney>(NaturalAirlineRoutes.Journey(NaturalAirlineRoutes.Load(root), map, session.CurrentPosition, upper));
		NaturalAirlineRoute flight = Assert.Single(journey.Flights);
		await WalkAsync("fortress-flight-pad", Ground(flight.Departure));
		int pad = await session.WaitForNpcAsync(flight.NpcId, token);
		Assert.Equal("df1a_altgardtoimpetosium", flight.Route);
		var flown = await new NaturalServiceSteps(session).FlyAsync(pad, session.Api.World.Objects[pad].Position, 6, flight, token);
		Assert.True(flown.IsDone, flown.Reason);
		Console.WriteLine($"CG-02 hub flight: {flown.Reason}");
		int obelisk = await WalkNpcAsync(700067, 5);
		var bound = await new NaturalServiceSteps(session).BindAsync(obelisk, session.Api.World.Objects[obelisk].Position, map, leg.Bind!.Price, 5, token);
		Assert.True(bound.IsDone, bound.Reason);
		int lateni = await WalkNpcAsync(203659);
		await session.StartQuestAsync(lateni, questId, token);
		Assert.Equal<(byte, int)?>((3, 0), NaturalAltgardQuestSteps.State(session.Api.World, questId));
		await PillarAsync("upper-to-lower-ground", lower);
		var rejected = new HashSet<int>();
		for (int attempt = 0; attempt < 60 && !objective.IsDone(session.Api.World.Quests[questId], new Dictionary<int, long>()); attempt++)
		{
			if (session.CurrentPosition.Z > 300) await PillarAsync("ordinary-post-death-descent", lower);
			int variable = Counter(0) < 6 ? 0 : 1;
			int[] kinds = variable == 0 ? [210576] : [210578, 210523];
			var candidates = instance.GetNpcs().Where(n => kinds.Contains(n.GetNpcId()) && !n.IsDead() && !rejected.Contains(n.GetObjectId()))
				.OrderBy(n => NaturalFlightPolicy.Distance(session.CurrentPosition, At(n))).ToArray();
			Aion.GameServer.Model.GameObjects.Npc? victim = null;
			BotPosition stand = default;
			foreach (var candidate in candidates)
			{
				BotPosition target = At(candidate);
				var points = geometry.GroundAround(map, target, [12f, 14f, 16f]).Where(p =>
					geometry.OnSameIsland(map, session.CurrentPosition, p) && geometry.HasLineOfSight(map, p with { Z = p.Z + 1.6f }, target with { Z = target.Z + 1 })).ToArray();
				if (points.Length == 0) { rejected.Add(candidate.GetObjectId()); continue; }
				victim = candidate; stand = points.OrderBy(p => NaturalFlightPolicy.Distance(session.CurrentPosition, p)).First(); break;
			}
			if (victim == null)
			{
				Console.WriteLine("CG-02 ordinary 295-second spirit respawn wait.");
				await session.AdvanceAsync(TimeSpan.FromSeconds(296), token); rejected.Clear(); await session.SynchronizeAsync(token); continue;
			}
			await WalkAsync($"counter-{variable}-spirit-{victim.GetObjectId()}", stand);
			await session.SynchronizeAsync(token);
			if (victim.IsDead()) continue;
			Assert.True(session.Api.World.Objects.ContainsKey(victim.GetObjectId()), "The actual spirit must be client-observed.");
			int hp = victim.GetLifeStats().GetMaxHp();
			Assert.True(hp > 1);
			int before = Counter(variable);
			NaturalCombatDiagnosticResult result = await new NaturalIshalgenJourney(session, runtime, new()).RunObservedCombatAsync(_ => Task.FromResult(victim.GetObjectId()), token);
			fights.Add(result);
			await session.SynchronizeAsync(token);
			if (result.Killed) { killedAtNormalHp++; Assert.True(victim.IsDead()); Assert.True(Counter(variable) > before); }
			else rejected.Add(victim.GetObjectId());
			Console.WriteLine($"CG-02 spirit {victim.GetNpcId()} max HP {hp}, result {JsonSerializer.Serialize(result)}, counters {Counter(0)}/6 {Counter(1)}/16.");
		}
		Assert.Equal((6, 16), (Counter(0), Counter(1)));
		await WalkAsync("circuit-to-lower-pillar", lower);
		await PillarAsync("lower-to-upper-vendors", upper);
		lateni = await WalkNpcAsync(203659);
		await NaturalDialogProtocol.OpenAsync(session, lateni, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(lateni, DialogAction.QUEST_SELECT, questId: questId), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(lateni, DialogAction.SELECT_QUEST_REWARD, questId: questId), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		long beforeReward = Coins();
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(lateni, DialogAction.SELECTED_QUEST_NOREWARD, questId: questId), token);
		await session.SynchronizeAsync(token);
		Assert.Equal((18L, 23L), (beforeReward, Coins()));
		Assert.Equal(1, session.Api.World.CompletedQuestCounts[questId]);
		// Actual login is the wire-count authority; compare it with the observed completion transition.
		await session.SendPacketAsync(session.Api.CloseDialog(lateni), token);
		await session.QuitAsync(token);
		await session.WaitForReentryAsync(token);
		await session.ReloginExistingCharacterAsync(token);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		player = Player();
		Assert.Equal(1, session.Api.World.CompletedQuests[questId].CompleteCount);
		Assert.False(session.Api.World.CompletedQuests[questId].NonRepeatable);
		lateni = await WalkNpcAsync(203659);
		await session.StartQuestAsync(lateni, questId, token);
		Assert.Equal<(byte, int)?>((3, 0), NaturalAltgardQuestSteps.State(session.Api.World, questId));
		Assert.Equal(1, player.GetQuestStateList().GetQuestState(questId).GetCompleteCount());
		Assert.Equal(23, Coins());
		await PillarAsync("recovery-descent", lower);
		// A controlled death exercises the real CM_REVIVE path; it is an outcome, not an objective failure.
		Assert.True(player.GetController().Die(player));
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(501), token);
		await session.WaitForPacketAsync(typeof(SM_DIE), token);
		session.Api.World.BeginWorldReload();
		await session.SendPacketAsync(session.Api.Revive(BotReviveType.Bind), token);
		await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, p => p.Get<int>("objectId") == session.CharacterId);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
		Assert.False(player.IsDead());
		Assert.True(NaturalAltgardDecisionEngine.BoundAt(leg.Bind, map, session.Api.World.ObeliskBindPoint));
		Assert.True(NaturalFlightPolicy.Distance(session.CurrentPosition, new(leg.Bind.Position[0], leg.Bind.Position[1], leg.Bind.Position[2], 0)) <= 10);
		Assert.Equal<(byte, int)?>((3, 0), NaturalAltgardQuestSteps.State(session.Api.World, questId));
		Assert.Equal(23, Coins());
		Assert.Equal((gear.StaffItemId, (ushort)3), (session.Api.World.Inventory[staffObject].ItemId, session.Api.World.Inventory[staffObject].EquipmentSlot));
		Assert.DoesNotContain(11504, session.Api.World.Skills.Keys);
		var evidence = new { account = 221, level = 24, flight = flown, bind = bound, freeFlights, groundLegs = sequence,
			killedAtNormalHp, fights, normalDeaths = fights.Sum(f => f.Deaths), controlledRecoveryDeaths = 1, cleared,
			completedCounters = new[] { 6, 16 }, completeCount = 1, resetCounters = new[] { Counter(0), Counter(1) }, coins = Coins(),
			staffObject, endpoint = session.CurrentPosition, bindPoint = session.Api.World.ObeliskBindPoint };
		await File.WriteAllTextAsync(Path.ChangeExtension(tracePath, ".summary.json"), JsonSerializer.Serialize(evidence), token);
		Console.WriteLine($"CG-02 PASS: normal counters 6+16, five-coin reward, unlimited repeat reset, {freeFlights} pillar flights, real Heart bind revival.");
		policy.AssertClean();
	}
}
