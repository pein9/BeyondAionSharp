using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.TestKit;
using Aion.GameServer.Utils;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>ND-02: four maps, both Ishalgen flights, ordinary city trips and all five Q2900 teleports; free account 217.</summary>
	[SkippableFact]
	public async Task DestinyTravelWalksTheNornCircuitAndUsesEveryMapTransition()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		using var policy = NewPolicy("ND02", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "nd02-travel";
		string path = Path.Combine(RealStaticData.RepoRoot(), "run", $"{run}.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(path, run, "b01", "sim-player-217", virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 217, "Asimdestpath", Race.ASMODIANS, trace, path);
		var probe = new DestinyProbe(this, fixture, session, token);
		await probe.InitializeAsync();
		await probe.SetupAtFortressAsync();
		int obelisk = await probe.WalkNpcAsync(700065);
		Assert.True((await new NaturalServiceSteps(session).BindAsync(obelisk, session.Api.World.Objects[obelisk].Position,
			220030000, probe.Leg.Bind!.Price, 5, token)).IsDone);
		await probe.TransportAsync(120010000);
		foreach (string key in new[] { "heimdall", "munin" }) await probe.TalkAsync(key);
		await probe.FlyAsync(203545); // Anturoon -> Aldelle, rather than walking between hubs.
		foreach (string key in new[] { "urd", "verdandi", "enter" }) await probe.TalkAsync(key);
		Assert.Equal(320070000, probe.Server.GetWorldId());
		Assert.True(probe.Server.GetInstanceId() > 1);
		Assert.True(probe.Server.GetWorldMapInstance().IsRegistered(probe.Server.GetObjectId()));
		Assert.Equal(1, probe.Server.GetWorldMapInstance().GetRegisteredCount());
		await probe.WalkNpcAsync(204264);
		await probe.WalkAsync("instance-boss-ground", new(257.5f, 245, 125, 0));
		await probe.WalkAsync("instance-entry-ground", new(270.8424f, 249.1182f, 125.8369f, 0));
		await probe.WalkNpcAsync(204264);
		// Controlled state prepares only the transport trigger. ND-03 proves movie/stone/equip progression.
		await probe.SetupVarAsync(97);
		await probe.TalkAsync("spawn");
		var enemy = probe.Server.GetWorldMapInstance().GetNpcs(204263).Single(n => !n.IsDead());
		enemy.GetLifeStats().SetCurrentHp(1);
		await session.SynchronizeAsync(token);
		ushort skill = new ushort[] { 4015, 4014, 4013, 4012 }.First(id => session.Api.World.Skills.ContainsKey(id));
		SpellCastData cast = probe.Runtime.CreateSpellCast(session.Api.World, session.CurrentPosition, skill,
			checked((byte)session.Api.World.Skills[skill].Level), enemy.GetObjectId());
		await session.SendPacketAsync(session.Api.Target(enemy.GetObjectId()), token);
		session.Api.World.BeginWorldReload(); // The damage/kill event teleports immediately; invalidate before the cast.
		await session.SendPacketAsync(session.Api.Cast(cast), token);
		var started = await BotCastProtocol.WaitForStartAsync((predicate, waitToken) => session.WaitForPacketAsync(
			p => predicate(p) || BotCastProtocol.IsStartRejection(p), waitToken), session.CharacterId, skill, token);
		Assert.Equal(typeof(SM_CASTSPELL), started.PacketType);
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(started.Get<ushort>("castDuration") + 1), token);
		var result = await BotCastProtocol.WaitForCompletionAsync(session.WaitForPacketAsync, session.CharacterId, skill, token);
		await session.AdvanceAsync(BotCastProtocol.RecoveryDelay(result), token);
		await probe.AcceptTeleportAsync(220010000, true);
		Assert.True(enemy.IsDead());
		Assert.Equal(9, probe.Var);
		Assert.False(session.Api.World.Objects.ContainsKey(enemy.GetObjectId()));
		Console.WriteLine($"ND-02 kill teleport: map {probe.Server.GetWorldId()}, var {probe.Var}, old enemy absent");
		await probe.TalkAsync("skuld-return");
		await probe.TalkAsync("munin-return");
		await probe.WalkNpcAsync(204061);
		await probe.TransportAsync(220010000); // Actual Doman service requires the completed Ascension.
		await probe.FlyAsync(203513); // Aldelle -> Anturoon recovery.
		await probe.WalkNpcAsync(203546);
		await probe.WalkNpcAsync(203545);
		await probe.WalkNpcAsync(203550); // Reverse cemetery/prison road for a retained late var.
		await probe.ReturnAsync();
		await probe.TransportAsync(120010000);
		await probe.TransportAsync(220030000); // Actual Doman fallback if Return is on cooldown.
		await probe.WalkNpcAsync(700065);
		Assert.False(probe.Server.IsDead());
		Console.WriteLine($"ND-02 {probe.Walked} checked ground legs, two hub flights, four paid city trips, five quest teleports, learned Return; endpoint {session.CurrentPosition}");
		policy.AssertClean();
	}

	/// <summary>Shared controlled-probe transport helpers. Every administrative setup is explicitly logged; none enters the natural runner.</summary>
	private sealed class DestinyProbe(SimulationFastScenarioTests owner, SimulationWorldFixture fixture, SimulationL0Session session, CancellationToken token)
	{
		public NaturalAltgardContract Leg { get; } = NaturalAltgardContract.LoadLeg("l11");
		public Player Server => fixture.World.GetPlayer(session.CharacterId);
		public int Var => Leg.QuestVar(session.Api.World.Quests[2900]);
		public int Walked { get; private set; }
		private int sequence;
		private BotNavigationGeometry Geometry() => BotNavigationGeometry.ForServerWorld(Server.GetInstanceId(), Race.ASMODIANS);
		public NaturalJourneyRuntime Runtime => new(RealStaticData.RepoRoot(), "SIM-destiny-probe", fixture.Seed, fixture.DataManager.StaticData,
			() => fixture.Clock.NowMillis, fixture.Epoch, Geometry, _ => Task.FromResult(false), () => { }, () => Array.Empty<object>(), null!, new());

		public async Task InitializeAsync()
		{
			session.BeginStep("s00", "controlled-level-24-cleric-setup");
			await session.LoginAndAuthenticateAsync(token);
			await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
			await session.EnterWorldAsync(token);
			await session.SynchronizeAsync(token);
			Assert.True(ClassChangeService.SetClass(Server, PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
			Server.GetCommonData().SetLevel(24);
			SkillLearnService.LearnNewSkills(Server, 1, 24);
			Server.GetInventory().IncreaseKinah(10000);
			foreach (int id in Leg.Start.CompletedQuestIds.Where(id => Server.GetQuestStateList().GetQuestState(id) == null))
				Assert.True(Server.GetQuestStateList().AddQuest(id, new QuestState(id, QuestStatus.COMPLETE)));
			if (Server.GetQuestStateList().GetQuestState(2900) == null)
				Assert.True(Server.GetQuestStateList().AddQuest(2900, new QuestState(2900, QuestStatus.START)));
			await SetupVarAsync(0);
		}

		public async Task SetupVarAsync(int var)
		{
			QuestState quest = Server.GetQuestStateList().GetQuestState(2900);
			quest.SetStatus(QuestStatus.START); quest.SetQuestVar(var);
			PacketSendUtility.SendPacket(Server, new SM_QUEST_ACTION(SM_QUEST_ACTION.ActionType.ADD, quest));
			await session.SynchronizeAsync(token);
			Console.WriteLine($"ND controlled setup: Q2900 START/{var}; not quest progression evidence");
		}

		public async Task SetupAtFortressAsync()
		{
			BotPosition at = new(1658.57f, 1818.39f, 253.72f, 0);
			var instance = fixture.World.GetWorldMap(220030000).GetMainWorldMapInstance();
			ClearHostiles(instance.GetNpcs(), [at]);
			session.Api.World.BeginWorldReload();
			await owner.TeleportForSetupAsync(session, Server, 220030000, at.X, at.Y, at.Z, token);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
		}

		public async Task<int> WalkNpcAsync(int npcId)
		{
			var npc = Server.GetWorldMapInstance().GetNpcs(npcId).Where(n => !n.IsDead()).OrderBy(n =>
				NaturalFlightPolicy.Distance(session.CurrentPosition, new(n.GetX(), n.GetY(), n.GetZ(), 0))).First();
			await WalkAsync($"npc-{npcId}", new(npc.GetX(), npc.GetY(), npc.GetZ(), 0), npc.GetObjectTemplate().GetTalkDistance());
			return await session.WaitForNpcAsync(npcId, token);
		}

		public async Task WalkAsync(string name, BotPosition to, float range = 0)
		{
			session.BeginStep($"s{++sequence:00}", name);
			int map = Server.GetWorldId();
			BotPosition from = session.CurrentPosition;
			BotNavigationGeometry geometry = Geometry();
			IReadOnlyList<BotPosition> route = [];
			if (range > 0)
			{
				foreach (BotPosition at in geometry.GroundAround(map, to, [Math.Max(1, range - 1), Math.Max(1, range - 2), 2f])
					.Where(at => NaturalFlightPolicy.Distance(at, to) <= range - 0.5f).OrderBy(at => NaturalFlightPolicy.Distance(from, at)))
				{
					route = geometry.FindJourneyPath(map, from, at);
					if (route.Count > 0) break;
				}
			}
			else route = geometry.FindJourneyPath(map, from, to);
			Assert.True(route.Count > 0 || range > 0 && NaturalFlightPolicy.Distance(from, to) <= range,
				$"{name}: no checked route ({BotNavMeshRouter.LastOutcome}) {from} -> {to}");
			ClearHostiles(Server.GetWorldMapInstance().GetNpcs(), route.Append(from).Append(to));
			if (route.Count > 0) await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing)
				.CreateGroundPlan(route, from, session.Api.World.MovementSpeed!.Value), token);
			await session.SynchronizeAsync(token);
			float miss = NaturalFlightPolicy.Distance(new(Server.GetX(), Server.GetY(), Server.GetZ(), 0), to);
			Assert.True(miss <= (range > 0 ? range : 5), $"{name}: miss {miss:F1} m");
			Assert.False(Server.IsDead());
			Walked++;
			Console.WriteLine($"ND route {name}: map {map}, {route.Count} points, miss {miss:F1} m");
		}

		private void ClearHostiles(IEnumerable<Aion.GameServer.Model.GameObjects.Npc> npcs, IEnumerable<BotPosition> points)
		{
			BotPosition[] route = points.ToArray();
			var cleared = npcs.Where(n => !n.IsDead() && NaturalHostility.IsAggressive(n.GetObjectTemplate(),
				fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) && route.Any(at =>
					MathF.Pow(n.GetX() - at.X, 2) + MathF.Pow(n.GetY() - at.Y, 2) <= 900)).ToArray();
			foreach (var npc in cleared) fixture.World.Despawn(npc);
			if (cleared.Length > 0) Console.WriteLine("ND route setup clears aggressive neighbours: " +
				string.Join(",", cleared.GroupBy(n => n.GetNpcId()).Select(g => $"{g.Key}x{g.Count()}")));
		}

		public async Task AcceptTeleportAsync(int map, bool changedMap)
		{
			await session.WaitForPacketAsync(changedMap ? typeof(SM_PLAYER_SPAWN) : typeof(SM_CHANNEL_INFO), token);
			await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, p => p.Get<int>("objectId") == session.CharacterId);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
			Assert.Equal(map, Server.GetWorldId());
			Assert.Equal(map, session.Api.World.MapId);
		}

		public async Task TalkAsync(string key)
		{
			NaturalAltgardStep step = Leg.Steps.Single(s => s.Key == "q2900-" + key);
			int npc = await WalkNpcAsync(step.NpcId);
			Assert.Equal(step.Var, Var);
			await NaturalDialogProtocol.OpenAsync(session, npc, token);
			await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, p => p.Get<int>("targetObjectId") == npc);
			for (int i = 0; i < step.Actions.Length; i++)
			{
				bool teleports = i == step.Actions.Length - 1 && step.Teleport != null;
				bool otherMap = teleports && step.Teleport!.MapId != Server.GetWorldId();
				if (teleports) session.Api.World.BeginWorldReload();
				await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc,
					checked((ushort)NaturalAscensionContract.DialogActionId(step.Actions[i])), questId: 2900), token);
				if (i < step.Pages.Length) await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
					p => p.Get<int>("targetObjectId") == npc && p.Get<ushort>("dialogPageId") == step.Pages[i]);
				if (step.MovieId != null && i == step.Actions.Length - 1) await NaturalMovieGate.FinishAsync(session, token);
				if (teleports) await AcceptTeleportAsync(step.Teleport!.MapId, otherMap);
			}
			await session.SynchronizeAsync(token);
			Assert.Equal(step.NextVar, Var);
			await session.SendPacketAsync(session.Api.CloseDialog(npc), token);
			Console.WriteLine($"ND {step.Key}: {step.Var} -> {Var}, map {Server.GetWorldId()}");
		}

		public async Task TransportAsync(int destination)
		{
			NaturalAltgardMapTrip trip = Leg.MapTripList.Single(t => t.FromMapId == Server.GetWorldId() && t.MapId == destination);
			int npc = await WalkNpcAsync(trip.TeleporterNpcId);
			var result = await new NaturalServiceSteps(session).TeleportAsync(npc, session.Api.World.Objects[npc].Position,
				trip.TalkRange, trip.LocationId, trip.Fare, trip.MapId, token);
			Assert.True(result.IsDone, result.Reason);
			Console.WriteLine($"ND {result.Reason}");
		}

		public async Task FlyAsync(int npcId)
		{
			NaturalAirlineRoute flight = NaturalAirlineRoutes.Load(RealStaticData.RepoRoot()).Single(r => r.MapId == 220010000 && r.NpcId == npcId);
			int npc = await WalkNpcAsync(npcId);
			var result = await new NaturalServiceSteps(session).FlyAsync(npc, session.Api.World.Objects[npc].Position, 4, flight, token);
			Assert.True(result.IsDone, result.Reason);
			Console.WriteLine($"ND {result.Reason}; {flight.Seconds} s hub flight");
		}

		public async Task ReturnAsync()
		{
			BotSkill learned = session.Api.World.Skills[243];
			await session.SendPacketAsync(session.Api.Target(session.CharacterId), token);
			await session.SendPacketAsync(session.Api.Cast(new SpellCastData(243, checked((byte)learned.Level), 0) { TargetObjectId = session.CharacterId }), token);
			var started = await BotCastProtocol.WaitForStartAsync((predicate, waitToken) => session.WaitForPacketAsync(
				p => predicate(p) || BotCastProtocol.IsStartRejection(p), waitToken), session.CharacterId, 243, token);
			Assert.Equal(typeof(SM_CASTSPELL), started.PacketType);
			session.Api.World.BeginWorldReload();
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(started.Get<ushort>("castDuration") + 1), token);
			var result = await BotCastProtocol.WaitForCompletionAsync(session.WaitForPacketAsync, session.CharacterId, 243, token);
			await session.AdvanceAsync(BotCastProtocol.RecoveryDelay(result), token);
			await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, p => p.Get<int>("objectId") == session.CharacterId);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
			Assert.Equal(220030000, Server.GetWorldId());
			Console.WriteLine($"ND learned Return: fortress {session.CurrentPosition}");
		}
	}
}
