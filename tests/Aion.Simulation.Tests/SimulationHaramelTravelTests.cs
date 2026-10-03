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
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>HM-02: ordinary travel on unused access-0 account 223; administrative setup is labelled separately.</summary>
	[SkippableFact]
	public async Task HaramelTravelProvesFloorsLiftSourcesExitsAndHeartRecovery()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		using var policy = NewPolicy("HM02", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		string root = RealStaticData.RepoRoot();
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "hm02-travel";
		string path = Path.Combine(root, "run", $"{run}.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(path, run, "b01", "sim-player-223", virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 223, "Asimharpath", Race.ASMODIANS, trace, path);
		var dashboard = new LiveBotDashboardState();
		await using var host = new LiveBotDashboardHost(run, ["HM-02"], dashboard,
			int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880"));
		session.Dashboard = dashboard;
		if (host.Enabled) Console.WriteLine($"HM-02 dashboard: {host.Url}");
		var probe = new HaramelTravelProbe(this, fixture, session, token);
		await probe.InitializeAsync();
		await probe.SetupFortressAsync();
		await probe.BindAsync(700065, 451);
		await probe.WalkNpcAsync(203560);
		await probe.WalkNpcAsync(798031);
		await probe.FlyHubAsync(toHeart: true);
		await probe.BindAsync(700067, 813);
		await probe.WalkNpcAsync(203649);
		await probe.PillarAsync(upper: false);
		await probe.WalkNpcAsync(730306);
		await probe.WalkNpcAsync(730307);
		await probe.WalkNpcAsync(804605);
		await probe.PortalAsync(730319, 300200000);
		int firstCopy = probe.Server.GetInstanceId();
		int firstAnchor = await probe.WalkNpcAsync(799522);
		int firstEntries = probe.EntriesUsed;
		Assert.Equal(firstCopy, session.Api.World.InstanceId);
		Assert.Equal(1, firstEntries);
		// Prove the entrance exit and actual same-copy re-entry before the floor circuit.
		await probe.PortalAsync(730320, 220030000);
		await probe.PortalAsync(730319, 300200000);
		Assert.Equal(firstCopy, probe.Server.GetInstanceId());
		Assert.Equal(firstAnchor, await probe.WalkNpcAsync(799522));
		Assert.Equal(firstEntries, probe.EntriesUsed);
		await probe.VisitSourcesAsync([700951, 700833, 700950]);
		await probe.WalkNpcAsync(216897, 18);
		await probe.WalkNpcAsync(799523);
		// The tower box is isolated from Moofrenerk's floor; reach it after the ordinary lower-floor lift.
		await probe.VisitSourcesAsync([700953, 700954, 700834]);
		await probe.WalkNpcAsync(216907, 18);
		await probe.WalkNpcAsync(730359);
		await probe.WalkNpcAsync(799524);
		int lift = await probe.WalkNpcAsync(730321);
		session.BeginStep("lift", "ordinary-lift-dialog-10000");
		await NaturalDialogProtocol.OpenAsync(session, lift, token);
		session.Api.World.BeginWorldReload();
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(lift, 10000), token);
		await probe.AcceptTeleportAsync(300200000, changedMap: false);
		Assert.True(NaturalFlightPolicy.Distance(session.CurrentPosition, new(220, 213, 126.68472f, 0)) < 2);
		Assert.Equal(firstCopy, probe.Server.GetInstanceId());
		await probe.WalkNpcAsync(700853); // Lift is not the box platform: prove the remaining stairs.
		await probe.GlideTowerDownAsync(); // The lift is one-way; descend through the ordinary glide protocol.
		await probe.WalkNpcAsync(799524);
		await probe.RideOfficeElevatorAsync();
		await probe.WalkNpcAsync(216915, 18);
		int boss = await probe.WalkNpcAsync(216922, 8);
		await probe.SpawnBossExitForTravelAsync(boss);
		await probe.WalkNpcAsync(700832);
		await probe.PortalAsync(700852, 220030000);
		await probe.WalkNpcAsync(804605);
		await probe.ReviveAtHeartAsync();
		await probe.PillarAsync(upper: false);
		await probe.PortalAsync(730319, 300200000);
		Assert.Equal(firstCopy, probe.Server.GetInstanceId());
		Assert.Equal(firstEntries, probe.EntriesUsed);
		await probe.PortalAsync(730320, 220030000);
		await probe.PillarAsync(upper: true);
		await probe.FlyHubAsync(toHeart: false);
		await probe.BindAsync(700065, 451);
		Assert.False(probe.Server.IsDead());
		Assert.Equal(220030000, session.Api.World.MapId);
		Assert.True(NaturalFlightPolicy.Distance(session.CurrentPosition, new(1655.52f, 1825.29f, 254.408f, 0)) < 20);
		await File.WriteAllTextAsync(Path.ChangeExtension(path, ".summary.json"), JsonSerializer.Serialize(new
		{
			account = 223, firstCopy, firstAnchor, firstEntries, probe.Walks, probe.SourceVisits, probe.HubFlights,
			probe.PillarFlights, probe.TowerGlides, probe.OfficeElevators, probe.MissedBoardingWindows, probe.Cleared, controlledRecoveryDeaths = 1, controlledBossHp = 1,
			liftDialog = 10000, exits = new[] { 730320, 700852 }, endpoint = session.CurrentPosition,
			bind = session.Api.World.ObeliskBindPoint,
		}), token);
		Console.WriteLine($"HM-02 PASS: {probe.Walks} checked ground legs, {probe.SourceVisits} source spots, actual lift/two exits, {probe.HubFlights} hub flights and Heart revival.");
		policy.AssertClean();
	}

	private sealed class HaramelTravelProbe(SimulationFastScenarioTests owner, SimulationWorldFixture fixture, SimulationL0Session session, CancellationToken token)
	{
		public Player Server => fixture.World.GetPlayer(session.CharacterId);
		public int EntriesUsed => session.Api.World.InstanceEntries[(session.CharacterId, 46)].EntriesUsed;
		public int Walks { get; private set; }
		public int SourceVisits { get; private set; }
		public int HubFlights { get; private set; }
		public int PillarFlights { get; private set; }
		public int TowerGlides { get; private set; }
		public int OfficeElevators { get; private set; }
		public int MissedBoardingWindows { get; private set; }
		public int Cleared { get; private set; }
		public HashSet<int> PreserveNpcIds { get; } = [];
		private int sequence;
		private long? lastTakeoff;
		private readonly Dictionary<int, BotPosition> bossSites = [];
		private BotNavigationGeometry Geometry() => BotNavigationGeometry.ForServerWorld(Server.GetInstanceId(), Race.ASMODIANS);
		private static BotPosition At(Npc npc) => new(npc.GetX(), npc.GetY(), npc.GetZ(), 0);
		private static IEnumerable<BotPosition> InteractionGround(BotNavigationGeometry geometry, int map, BotPosition target, float range)
		{
			BotNavMesh mesh = geometry.NavMesh!.NavMeshes.Get(map)!;
			// The spawned portal blocks its own tiny island. Java dialog range accepts the adjacent floor;
			// it does not require a sight ray through the target's physical placeable model.
			foreach (float radius in new[] { MathF.Max(1, range - 1), MathF.Max(1, range - 2), 1f })
				for (int sector = 0; sector < 16; sector++)
				{
					float angle = sector * MathF.PI / 8;
					BotPosition sample = target with { X = target.X + radius * MathF.Cos(angle), Y = target.Y + radius * MathF.Sin(angle) };
					if (mesh.Snap(sample, BotNavQuery.Default with { SnapHorizontal = 1, SnapVertical = 3 }) is BotPosition ground &&
						NaturalFlightPolicy.Distance(ground, target) <= range - 0.5f) yield return ground;
				}
		}

		public async Task InitializeAsync()
		{
			session.BeginStep("s00", "labelled-probe-level-24-cleric-loadout");
			await session.LoginAndAuthenticateAsync(token);
			await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
			await session.EnterWorldAsync(token);
			await session.SynchronizeAsync(token);
			Assert.True(ClassChangeService.SetClass(Server, PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
			Server.GetCommonData().SetLevel(24);
			SkillLearnService.LearnNewSkills(Server, 1, 24);
			Server.GetInventory().IncreaseKinah(534815);
			Assert.Equal(0, ItemService.AddItem(Server, 101501357, 1, allowInventoryOverflow: true));
			var staff = Server.GetInventory().GetItems().Single(i => i.GetItemId() == 101501357);
			Assert.NotNull(Server.GetEquipment().EquipItem(staff.GetObjectId(), 3));
			Console.WriteLine("HM-02 labelled setup: probe-only class/level/Kinah/staff; no natural character or quest modification.");
		}

		public async Task SetupFortressAsync()
		{
			BotPosition at = new(1658.57f, 1818.39f, 253.72f, 0);
			ClearNeighbours(fixture.World.GetWorldMap(220030000).GetMainWorldMapInstance().GetNpcs(), [at]);
			session.Api.World.BeginWorldReload();
			await owner.TeleportForSetupAsync(session, Server, 220030000, at.X, at.Y, at.Z, token);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
		}

		private void ClearNeighbours(IEnumerable<Npc> npcs, IEnumerable<BotPosition> points)
		{
			BotPosition[] path = points.ToArray();
			Npc[] cleared = npcs.Where(n => n.GetNpcId() != 216922 && !PreserveNpcIds.Contains(n.GetNpcId()) && !n.IsDead() &&
				NaturalHostility.IsAggressive(n.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
				path.Any(p => MathF.Pow(n.GetX() - p.X, 2) + MathF.Pow(n.GetY() - p.Y, 2) <= 900)).ToArray();
			foreach (Npc npc in cleared) fixture.World.Despawn(npc);
			Cleared += cleared.Length;
			if (cleared.Length > 0) Console.WriteLine("HM-02 labelled route setup clears neighbours: " +
				string.Join(",", cleared.GroupBy(n => n.GetNpcId()).Select(g => $"{g.Key}x{g.Count()}")));
		}

		public async Task WalkAsync(string label, BotPosition target, float range = 0)
		{
			session.BeginStep($"s{++sequence:00}", label);
			int map = Server.GetWorldId();
			BotPosition from = session.CurrentPosition;
			BotNavigationGeometry geo = Geometry();
			BotTravelPlanner? planner = BotTravelPlanner.For(map, geo, fixture.DataManager.StaticData);
			IReadOnlyList<BotPosition> route = [];
			if (range > 0 && NaturalFlightPolicy.Distance(from, target) <= range - 0.5f) { }
			else
			{
				// Interaction routes can stop on the reachable side of a small isolated portal/platform island.
				// GroundAround deliberately filters to the target's island and is insufficient for that case.
				if (range is > 0 and <= 5) route = geo.NavMesh!.FindInteractionPath(map, from, target);
				IEnumerable<BotPosition> points = range > 0
					? (range <= 5 ? new[] { target }.Concat(InteractionGround(geo, map, target, range)) : geo.GroundAround(map, target, [range - 1, range - 2, 2f]))
						.Where(p => NaturalFlightPolicy.Distance(p, target) <= range - 0.5f && (range <= 5 || geo.HasLineOfSight(map, p, target)))
						.OrderBy(p => NaturalFlightPolicy.Distance(from, p))
					: [target];
				BotPosition[] approaches = points.Distinct().ToArray();
				foreach (BotPosition point in approaches)
				{
					if (route.Count > 0) break;
					token.ThrowIfCancellationRequested();
					// Try every checked mesh approach before spending a grid search on one awkward ring point.
					route = planner?.PlanJourney(map, from, point, 24, [])?.Route ?? geo.NavMesh!.FindPath(map, from, point);
					if (route.Count > 0) break;
				}
				if (route.Count == 0 && approaches.Length > 0) route = geo.FindJourneyPath(map, from, approaches[0]);
			}
			Assert.True(route.Count > 0 || NaturalFlightPolicy.Distance(from, target) <= (range > 0 ? range : 2),
				$"{label}: no checked route ({BotNavMeshRouter.LastOutcome}) {from} -> {target}, range {range}.");
			ClearNeighbours(Server.GetWorldMapInstance().GetNpcs(), route.Append(from).Append(target));
			if (route.Count > 0) await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing)
				.CreateGroundPlan(route, from, session.Api.World.MovementSpeed!.Value), token);
			await session.SynchronizeAsync(token);
			float miss = NaturalFlightPolicy.Distance(new(Server.GetX(), Server.GetY(), Server.GetZ(), 0), target);
			Assert.True(miss <= (range > 0 ? range : 5), $"{label}: server endpoint misses by {miss:F1} m.");
			Assert.False(Server.IsDead());
			Walks++;
			Console.WriteLine($"HM-02 {label}: {route.Count} checked points, {from} -> {session.CurrentPosition}, miss {miss:F1} m.");
		}

		public async Task<int> WalkNpcAsync(int npcId, float? standRange = null)
		{
			Npc? npc = Server.GetWorldMapInstance().GetNpcs(npcId).Where(n => !n.IsDead())
				.OrderBy(n => NaturalFlightPolicy.Distance(session.CurrentPosition, At(n))).FirstOrDefault();
			if (npc == null && standRange is float range && bossSites.TryGetValue(npcId, out BotPosition clearedSite))
			{
				await WalkAsync($"labelled-cleared-boss-site-{npcId}", clearedSite, range);
				return 0; // Site access only; no combat credit is claimed for a cleared neighbour.
			}
			Assert.NotNull(npc);
			int objectId = npc.GetObjectId();
			await WalkAsync($"npc-{npcId}-{objectId}", At(npc), standRange ?? MathF.Min(5, npc.GetObjectTemplate().GetTalkDistance()));
			if (standRange == null) return await session.WaitForNpcAsync(npcId, token);
			return objectId;
		}

		public async Task VisitSourcesAsync(int[] npcIds)
		{
			var remaining = Server.GetWorldMapInstance().GetNpcs().Where(n => npcIds.Contains(n.GetNpcId()) && !n.IsDead())
				.Select(n => (Id: n.GetNpcId(), Object: n.GetObjectId(), Position: At(n), Range: MathF.Min(5, n.GetObjectTemplate().GetTalkDistance()))).ToList();
			while (remaining.Count > 0)
			{
				var source = remaining.MinBy(s => NaturalFlightPolicy.Distance(session.CurrentPosition, s.Position));
				await WalkAsync($"source-{source.Id}-{source.Object}", source.Position, source.Range);
				remaining.Remove(source);
				SourceVisits++;
			}
		}

		public async Task BindAsync(int npcId, int price)
		{
			int npc = await WalkNpcAsync(npcId);
			NaturalServiceOutcome result = await new NaturalServiceSteps(session).BindAsync(npc, session.Api.World.Objects[npc].Position,
				220030000, price, 5, token);
			Assert.True(result.IsDone, result.Reason);
		}

		public async Task FlyHubAsync(bool toHeart)
		{
			NaturalAirlineRoute flight = NaturalAirlineRoutes.Load(RealStaticData.RepoRoot()).Single(r =>
				r.MapId == 220030000 && r.Route == (toHeart ? "df1a_altgardtoimpetosium" : "df1a_impetosiumtoaltgard"));
			int npc = await WalkNpcAsync(flight.NpcId);
			var result = await new NaturalServiceSteps(session).FlyAsync(npc, session.Api.World.Objects[npc].Position, 6, flight, token);
			Assert.True(result.IsDone, result.Reason);
			HubFlights++;
		}

		public async Task PillarAsync(bool upper)
		{
			NaturalAltgardPillarFlight pillar = NaturalAltgardContract.LoadLeg("cg").PillarFlight!;
			// The Haramel exit is east of the FLY volume. Walk to the shipped lower launch point first.
			if (upper)
			{
				BotPosition lower = new(pillar.Lower[0], pillar.Lower[1], pillar.Lower[2], 0);
				await WalkAsync("pillar-lower-launch", lower);
			}
			float[] endpoint = upper ? pillar.Upper : pillar.Lower;
			BotNavigationGeometry geo = Geometry();
			BotPosition destination = geo.StaticGroundAt(220030000, new(endpoint[0], endpoint[1], endpoint[2], 0))
				?? throw new InvalidDataException("The approved pillar landing lacks static ground.");
			BotPosition from = session.CurrentPosition;
			ClearNeighbours(Server.GetWorldMapInstance().GetNpcs(), [from, destination]);
			long wait = NaturalFlightPolicy.RestoreMillis(session.Api.World.CurrentFlightTime, session.Api.World.MaxFlightTime, session.Api.World.MaxFlightTime);
			if (lastTakeoff is long last) wait = Math.Max(wait, last + NaturalFlightPolicy.TakeoffReuseMillis - fixture.Clock.NowMillis);
			if (wait > 0) await session.AdvanceAsync(TimeSpan.FromMilliseconds(wait + 100), token);
			await session.SynchronizeAsync(token);
			NaturalFlightRoute route = new([], 0, "No checked pillar route.");
			foreach (float cruise in new[] { MathF.Max(from.Z, destination.Z) + 12, 360f, 400f, 430f })
			{
				route = NaturalFlightProtocol.Plan(geo, 220030000, from, destination, cruise);
				if (route.IsUsable) break;
			}
			Assert.True(route.IsUsable, route.Refusal);
			lastTakeoff = fixture.Clock.NowMillis;
			float speed = await NaturalFlightProtocol.TakeOffAsync(session, token);
			var zones = NaturalFlyZone.Load(Path.Combine(RealStaticData.RepoRoot(), "game-server/data/static_data/zones/zones_220030000.xml"));
			var allowed = NaturalFlightPolicy.CanFly(NaturalFlightProtocol.ToPlan(route, from, speed), session.Api.World.CurrentFlightTime, zones);
			Assert.True(allowed.Allowed, allowed.Reason);
			await NaturalFlightProtocol.FlyAsync(session, 220030000, from, route.Waypoints, speed, token);
			await NaturalFlightProtocol.LandAsync(session, token);
			await session.SynchronizeAsync(token);
			PillarFlights++;
		}

		public async Task AcceptTeleportAsync(int map, bool changedMap, int? historyStart = null)
		{
			int start = historyStart ?? session.PacketHistory.Count;
			Type entryPacket = changedMap ? typeof(SM_PLAYER_SPAWN) : typeof(SM_CHANNEL_INFO);
			if (!session.PacketHistory.Skip(start).Any(p => p.PacketType == entryPacket))
				await session.WaitForPacketAsync(entryPacket, token);
			if (!session.PacketHistory.Skip(start).Any(p => p.PacketType == typeof(SM_PLAYER_INFO) && p.Get<int>("objectId") == session.CharacterId))
				await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, p => p.Get<int>("objectId") == session.CharacterId);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
			Assert.Equal(map, Server.GetWorldId());
			Assert.Equal(map, session.Api.World.MapId);
		}

		public async Task GlideTowerDownAsync()
		{
			BotNavigationGeometry geometry = Geometry();
			BotPosition from = session.CurrentPosition;
			BotNavMesh mesh = geometry.NavMesh!.NavMeshes.Get(300200000)!;
			int lowerIsland = mesh.IslandOf(new(283.064f, 275.719f, 90.1018f, 0));
			BotPosition[] landings = mesh.Polygons().Where(p => p.Island == lowerIsland).Select(p =>
				new BotPosition(p.Ring.Average(v => v.X), p.Ring.Average(v => v.Y), p.Ring.Average(v => v.Z), 0)).ToArray();
			var candidates = new[] { from }.Concat(geometry.GroundAround(300200000, from, [2f, 4f, 8f, 12f]))
				.SelectMany(takeoff => landings.Select(landing => (Takeoff: takeoff, Landing: landing)))
				.Where(p => p.Takeoff.Z - p.Landing.Z is > 20 and < 60 &&
					MathF.Sqrt(MathF.Pow(p.Takeoff.X - p.Landing.X, 2) + MathF.Pow(p.Takeoff.Y - p.Landing.Y, 2)) >= p.Takeoff.Z - p.Landing.Z)
				.OrderBy(p => NaturalFlightPolicy.Distance(from, p.Takeoff) + NaturalFlightPolicy.Distance(p.Takeoff, p.Landing));
			(BotPosition Takeoff, BotPosition Launch, BotPosition Landing)? chosen = null;
			foreach (var candidate in candidates)
			{
				float horizontal = MathF.Sqrt(MathF.Pow(candidate.Landing.X - candidate.Takeoff.X, 2) + MathF.Pow(candidate.Landing.Y - candidate.Takeoff.Y, 2));
				BotPosition launch = candidate.Takeoff with { X = candidate.Takeoff.X + (candidate.Landing.X - candidate.Takeoff.X) / horizontal * 6,
					Y = candidate.Takeoff.Y + (candidate.Landing.Y - candidate.Takeoff.Y) / horizontal * 6, Z = candidate.Takeoff.Z + 1.5f };
				if (!NaturalFlightProtocol.IsClear(geometry, 300200000, candidate.Takeoff, launch) ||
					!NaturalFlightProtocol.IsClear(geometry, 300200000, launch, candidate.Landing)) continue;
				if (NaturalFlightPolicy.Distance(from, candidate.Takeoff) > 1 && geometry.NavMesh!.FindPath(300200000, from, candidate.Takeoff).Count == 0) continue;
				chosen = (candidate.Takeoff, launch, candidate.Landing); break;
			}
			Assert.True(chosen != null, "The one-way tower needs a checked glide descent to the lower floor.");
			var route = chosen ?? throw new InvalidOperationException("No checked tower glide.");
			await WalkAsync("tower-glide-takeoff", route.Takeoff);
			ClearNeighbours(Server.GetWorldMapInstance().GetNpcs(), [route.Takeoff, route.Landing]);
			float speed = session.Api.World.MovementSpeed!.Value;
			long need = (long)Math.Ceiling(NaturalFlightPolicy.Distance(route.Takeoff, route.Landing) / speed) + 5;
			Assert.True(need <= session.Api.World.MaxFlightTime, "The glide exceeds the observed flight-time capacity.");
			long wait = NaturalFlightPolicy.RestoreMillis(session.Api.World.CurrentFlightTime, checked((int)need), session.Api.World.MaxFlightTime);
			if (wait > 0) await session.AdvanceAsync(TimeSpan.FromMilliseconds(wait + 100), token);
			session.BeginStep("tower-glide", "ordinary-collision-checked-tower-descent");
			int before = session.Api.World.CurrentFlightTime;
			await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing)
				.CreateJumpPlan([route.Launch], route.Takeoff, speed), token);
			BotMovementPlan glide = new BotMover(session.Api.World, session.Api.Timing)
				.CreateGlidePlan([route.Landing], route.Launch, speed);
			// Java CM_MOVE(IMMEDIATE) stops gliding. Observe the accepted state before that landing frame.
			// Java CM_MOVE(GLIDE) starts gliding. START_GLIDE emotion is only animation and has no
			// CM_EMOTION handler; omit it rather than generating Java's known error in shared history.
			await session.ExecuteMovementAsync(glide with { Frames = glide.Frames.Skip(1).Take(glide.Frames.Count - 2).ToArray() }, token);
			await session.SynchronizeAsync(token);
			Assert.True(Server.IsInGlidingState());
			await session.ExecuteMovementAsync(glide with { Frames = [glide.Frames[^1]], Duration = TimeSpan.Zero, Distance = 0 }, token);
			await NaturalFlightProtocol.LandAsync(session, token);
			await session.SynchronizeAsync(token);
			Assert.False(Server.IsDead());
			Assert.False(Server.IsFlying());
			Assert.True(NaturalFlightPolicy.Distance(new(Server.GetX(), Server.GetY(), Server.GetZ(), 0), route.Landing) < 2);
			TowerGlides++;
			Console.WriteLine($"HM-02 checked tower glide {route.Takeoff} -> {route.Landing}; FP {before} -> {session.Api.World.CurrentFlightTime}, alive.");
		}

		public async Task RideOfficeElevatorAsync()
		{
			NaturalHaramelElevator elevator = NaturalHaramelElevator.Load(RealStaticData.RepoRoot());
			BotNavigationGeometry geometry = Geometry();
			// Stand beside the lower platform, then board during its actual 1.7-second bottom stop.
			long cycle = 0;
			bool boarded = false;
			for (int attempt = 0; attempt < 3 && !boarded; attempt++)
			{
				await WalkAsync("office-elevator-lower-pad", elevator.Bottom with { X = elevator.Bottom.X + 3.5f });
				cycle = elevator.NextBottomCycle(fixture.Clock.NowMillis);
				if (cycle > fixture.Clock.NowMillis)
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(cycle - fixture.Clock.NowMillis), token);
				await WalkAsync("office-elevator-board", elevator.Bottom);
				boarded = fixture.Clock.NowMillis - cycle < elevator.AscentStarts;
				if (!boarded) { MissedBoardingWindows++; Console.WriteLine("HM-02 outcome: missed elevator boarding window; wait for the next real cycle."); }
			}
			Assert.True(boarded, "Travel remains incomplete after three ordinary boarding attempts; retained misses are outcomes.");
			Assert.True(NaturalFlightProtocol.IsClear(geometry, 300200000, elevator.Bottom, elevator.Top), "Elevator shaft collision.");
			session.BeginStep("office-elevator-ride", "client-cga-tcb3-position-samples");
			await session.ExecuteMovementAsync(elevator.Ascent(cycle, fixture.Clock.NowMillis), token);
			await session.SynchronizeAsync(token);
			Assert.False(Server.IsDead());
			Assert.False(Server.IsFlying());
			Assert.True(NaturalFlightPolicy.Distance(new(Server.GetX(), Server.GetY(), Server.GetZ(), 0), elevator.Top) < 0.1f);
			BotPosition exit = elevator.Top with { X = elevator.Top.X - 3.75f };
			Assert.True(NaturalFlightProtocol.IsClear(geometry, 300200000, elevator.Top, exit), "Elevator upper disembark collision.");
			IReadOnlyList<BotPosition> connector = elevator.Disembark(geometry, exit);
			bool hop = connector.Count == 0;
			if (hop) connector = elevator.JumpDisembark(geometry, exit);
			Assert.NotEmpty(connector);
			session.BeginStep("office-elevator-disembark", hop ? "checked-hop-over-upper-landing-gap" : "checked-client-platform-floor-to-static-office-floor");
			var mover = new BotMover(session.Api.World, session.Api.Timing);
			await session.ExecuteMovementAsync(hop ? mover.CreateJumpPlan(connector, elevator.Top, session.Api.World.MovementSpeed!.Value)
				: mover.CreateGroundPlan(connector, elevator.Top, session.Api.World.MovementSpeed!.Value), token);
			await session.SynchronizeAsync(token);
			Assert.False(Server.IsDead());
			Walks++;
			Console.WriteLine($"HM-02 upper departure completed at animation phase {fixture.Clock.NowMillis - cycle} ms.");
			OfficeElevators++;
			Console.WriteLine($"HM-02 client elevator {elevator.Bottom} -> {elevator.Top}, cycle {cycle}, ordinary CM_MOVE, alive.");
		}

		public async Task PortalAsync(int npcId, int map)
		{
			int npc = await WalkNpcAsync(npcId);
			int start = session.PacketHistory.Count;
			Assert.True(await NaturalAltgardQuestSteps.UseObjectAsync(session, npc, null, token, reloadWorld: true));
			// Fade-out completion is client-acknowledged; the helper may have observed some entry packets already.
			await AcceptTeleportAsync(map, changedMap: true, start);
			if (map == 300200000)
				foreach (Npc boss in Server.GetWorldMapInstance().GetNpcs().Where(n => n.GetNpcId() is 216897 or 216907 or 216915 or 216922))
					bossSites.TryAdd(boss.GetNpcId(), At(boss));
			Console.WriteLine($"HM-02 ordinary portal {npcId}: map {map}, native instance {Server.GetInstanceId()}.");
		}

		public async Task SpawnBossExitForTravelAsync(int bossObject)
		{
			Npc boss = Server.GetWorldMapInstance().GetNpcs(216922).Single(n => n.GetObjectId() == bossObject);
			boss.GetLifeStats().SetCurrentHp(1);
			Console.WriteLine("HM-02 labelled setup: Hamerun HP=1 solely to expose class chest/exit for travel; normal combat proof is HM-04.");
			await session.SynchronizeAsync(token);
			ushort skill = new ushort[] { 4015, 4014, 4013, 4012 }.First(id => session.Api.World.Skills.ContainsKey(id));
			var runtime = new NaturalJourneyRuntime(RealStaticData.RepoRoot(), "SIM-hm02", fixture.Seed, fixture.DataManager.StaticData,
				() => fixture.Clock.NowMillis, fixture.Epoch, Geometry, _ => Task.FromResult(false), () => { }, () => Array.Empty<object>(), null!, new());
			SpellCastData cast = runtime.CreateSpellCast(session.Api.World, session.CurrentPosition, skill,
				checked((byte)session.Api.World.Skills[skill].Level), bossObject);
			await session.SendPacketAsync(session.Api.Target(bossObject), token);
			await session.SendPacketAsync(session.Api.Cast(cast), token);
			var started = await BotCastProtocol.WaitForStartAsync((predicate, waitToken) => session.WaitForPacketAsync(
				p => predicate(p) || BotCastProtocol.IsStartRejection(p), waitToken), session.CharacterId, skill, token);
			if (started.PacketType != typeof(SM_CASTSPELL)) Console.WriteLine("HM-02 exposing exit cast: " + JsonSerializer.Serialize(started.Fields));
			Assert.Equal(typeof(SM_CASTSPELL), started.PacketType);
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(started.Get<ushort>("castDuration") + 1), token);
			var result = await BotCastProtocol.WaitForCompletionAsync(session.WaitForPacketAsync, session.CharacterId, skill, token);
			await session.AdvanceAsync(BotCastProtocol.RecoveryDelay(result), token);
			await NaturalMovieGate.FinishAsync(session, token);
			await session.SynchronizeAsync(token);
			Assert.True(boss.IsDead());
			Assert.Single(Server.GetWorldMapInstance().GetNpcs(700832));
			Assert.Single(Server.GetWorldMapInstance().GetNpcs(700852));
		}

		public async Task ReviveAtHeartAsync()
		{
			Console.WriteLine("HM-02 labelled controlled death; real CM_REVIVE must recover at the retained Heart bind.");
			Assert.True(Server.GetController().Die(Server));
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(501), token);
			await session.WaitForPacketAsync(typeof(SM_DIE), token);
			session.Api.World.BeginWorldReload();
			await session.SendPacketAsync(session.Api.Revive(BotReviveType.Bind), token);
			await AcceptTeleportAsync(220030000, changedMap: false);
			Assert.False(Server.IsDead());
			Assert.True(NaturalFlightPolicy.Distance(session.CurrentPosition, new(2656.19f, 1660.59f, 325.052f, 0)) < 10);
			// Java PlayerController.updateSoulSickness applies 8291. Its speed penalty can make a safe
			// pillar flight exceed the reserve; wait for the observed ordinary expiry, never remove it.
			int sickness = session.Api.World.VisibleEffects?.Where(e => e.SkillId == 8291).Select(e => e.RemainingMillis).DefaultIfEmpty(0).Max() ?? 0;
			if (sickness > 0)
			{
				session.BeginStep("heart-recovery-wait", "observed-soul-sickness-expiry");
				Console.WriteLine($"HM-02 recovery outcome: wait {sickness} ms for observed soul sickness before the pillar flight.");
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(sickness + 100), token);
				await session.SynchronizeAsync(token);
				Assert.DoesNotContain(session.Api.World.VisibleEffects ?? [], e => e.SkillId == 8291);
			}
		}
	}
}
