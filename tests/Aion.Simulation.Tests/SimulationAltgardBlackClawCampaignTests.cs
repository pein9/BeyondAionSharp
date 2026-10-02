using Aion.Bots.Navigation;
using Aion.Bots.Movement;
using Aion.Bots.Reflexes;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.Bots.Tracing;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>BC-04: three campaigns, gated Orb, real Bregirun mechanics, ordinary self revival and leave/re-entry. Free probe 215 only.</summary>
	[SkippableFact]
	public async Task BlackClawCampaignsCompleteThroughRealPortalsAndMovies()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, level = 22;
		using var policy = NewPolicy("BC04", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l10");
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? $"bc04-quests-s{fixture.Seed}";
		string tracePath = Path.Combine(Aion.GameServer.TestKit.RealStaticData.RepoRoot(), "run", $"{run}.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(tracePath, run, "b01", "sim-player-215",
			virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 215, "Asimclawcamp", Race.ASMODIANS, trace, tracePath);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player Server() => fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(Server(), PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		Server().GetCommonData().SetLevel(level);
		SkillLearnService.LearnNewSkills(Server(), 1, level);
		Server().GetInventory().IncreaseKinah(10000);
		foreach (int id in leg.Start.CompletedQuestIds.Where(id => Server().GetQuestStateList().GetQuestState(id) == null))
			Assert.True(Server().GetQuestStateList().AddQuest(id, new QuestState(id, QuestStatus.COMPLETE)));
		Aion.GameServer.World.WorldMapInstance Instance() => Server().GetPosition().GetWorldMapInstance();
		BotNavigationGeometry Geometry() => BotNavigationGeometry.ForServerWorld(Server().GetInstanceId(), Race.ASMODIANS);
		var runtime = new NaturalJourneyRuntime(Aion.GameServer.TestKit.RealStaticData.RepoRoot(), "SIM-bc04", fixture.Seed, fixture.DataManager.StaticData,
			() => fixture.Clock.NowMillis, fixture.Epoch, () => Geometry(), _ => Task.FromResult(false), policy.AssertClean,
			() => policy.SnapshotProblems(), null!, new Aion.Bots.Dashboard.LiveBotDashboardState());
		long ItemCount(int itemId) => session.Api.World.Inventory.Values.Where(owned => owned.ItemId == itemId).Sum(owned => owned.Count);
		(byte Status, int Var)? State(int id) => NaturalAltgardQuestSteps.State(session.Api.World, id);
		var unusable = new HashSet<int>();
		var dropped = new Dictionary<int, int>();
		int respawns = 0, ordinaryRespawns = 0, confirmedKills = 0;
		var log = new List<string>();

		// Java sends no SM_DELETE for what the player saw while it teleports (PlayerController.notSee), so the client drops its
		// view first, as it does for a natural teleport (AG-04).
		async Task TeleportNearAsync(BotPosition at, float[] radii, bool sighted = false, int skip = 0, int map = altgard)
		{
			var currentInstance = (Server().GetWorldId() == map ? Instance() : fixture.World.GetWorldMap(map).GetMainWorldMapInstance());
			BotNavigationGeometry currentGeometry = BotNavigationGeometry.ForServerWorld(currentInstance.GetInstanceId(), Race.ASMODIANS);
			BotPosition ground = currentGeometry.GroundAround(map, at, radii)
				.Where(point => !sighted || currentGeometry.HasLineOfSight(map, point with { Z = point.Z + 1.6f }, at with { Z = at.Z + 1 }))
				.Skip(skip).First();
			foreach (var npc in currentInstance.GetNpcs().Where(npc => !npc.IsDead() &&
				NaturalHostility.IsAggressive(npc.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
				MathF.Pow(npc.GetX() - ground.X, 2) + MathF.Pow(npc.GetY() - ground.Y, 2) <= 40 * 40).ToArray())
			{
				// Preserve the objective victim at a sighted stand-off; ordinary talks clear every hostile neighbour.
				if (sighted && MathF.Pow(npc.GetX() - at.X, 2) + MathF.Pow(npc.GetY() - at.Y, 2) < 1) continue;
				fixture.World.Despawn(npc);
			}
			session.Api.World.BeginWorldReload();
			await TeleportForSetupAsync(session, Server(), map, ground.X, ground.Y, ground.Z, token, currentInstance.GetInstanceId());
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
		}
		IEnumerable<Aion.GameServer.Model.GameObjects.Npc> Candidates(int[] kinds) => Instance().GetNpcs()
			.Where(npc => kinds.Contains(npc.GetNpcId()) && !npc.IsDead() && !unusable.Contains(npc.GetObjectId()))
			.OrderBy(npc => MathF.Pow(npc.GetX() - session.CurrentPosition.X, 2) + MathF.Pow(npc.GetY() - session.CurrentPosition.Y, 2));
		// The shared Fast world keeps what earlier probes despawned (AG-03 clears around Gerger), and a despawned monster never
		// returns: spawn a fresh one at the nearest shipped spot of its kind (GM setup on the probe world).
		Aion.GameServer.Model.GameObjects.Npc RespawnShipped(int[] kinds)
		{
			SpawnGroup group = fixture.DataManager.StaticData.SpawnsDh.GetSpawnsByWorldId(Instance().GetMapId())
				.First(candidate => kinds.Contains(candidate.GetNpcId()));
			var spot = group.GetSpawnTemplates().OrderBy(template =>
				MathF.Pow(template.GetX() - session.CurrentPosition.X, 2) + MathF.Pow(template.GetY() - session.CurrentPosition.Y, 2))
				.Skip(respawns++ % group.GetSpawnTemplates().Count).First();
			var spawned = Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(new SpawnTemplate(group, spot.GetX(), spot.GetY(), spot.GetZ(), spot.GetHeading(), 0, null, 0), Instance().GetInstanceId());
			Assert.True(spawned is Aion.GameServer.Model.GameObjects.Npc, $"No {group.GetNpcId()} could be spawned.");
			return (Aion.GameServer.Model.GameObjects.Npc)spawned!;
		}
		// One monster of the given kinds at 1 HP, shot down with Smite from a sighted stand-off; a kill counts only when the server
		// has it dead. Then its corpse is looted for the item, when one is wanted.
		async Task KillAndLootAsync(int[] kinds, int quest, int? item)
		{
			var reasons = new List<string>();
			for (int tries = 0; tries < 16; tries++)
			{
				var next = Candidates(kinds).FirstOrDefault();
				if (next == null && confirmedKills > 0)
				{
					await NpcObjectAsync(700065);
					await session.AdvanceAsync(TimeSpan.FromSeconds(301), token);
					await session.SynchronizeAsync(token);
					unusable.Clear();
					next = Candidates(kinds).FirstOrDefault();
					if (next != null) ordinaryRespawns++;
				}
				next ??= RespawnShipped(kinds);
				unusable.Add(next.GetObjectId());
				var at = new BotPosition(next.GetX(), next.GetY(), next.GetZ(), 0);
				if (!Geometry().GroundAround(Server().GetWorldId(), at, [12f, 14f, 10f, 16f])
					.Any(point => Geometry().HasLineOfSight(Server().GetWorldId(), point with { Z = point.Z + 1.6f }, at with { Z = at.Z + 1 })))
				{
					reasons.Add($"{next.GetObjectId()}: no sighted ground");
					continue;
				}
				// Aggressive neighbours are cleared, but not the kinds being hunted: a dense camp would otherwise be emptied of them.
				foreach (var npc in Instance().GetNpcs().Where(npc => !kinds.Contains(npc.GetNpcId()) && !npc.IsDead() &&
					NaturalHostility.IsAggressive(npc.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
					MathF.Pow(npc.GetX() - next.GetX(), 2) + MathF.Pow(npc.GetY() - next.GetY(), 2) <= 25 * 25).ToArray())
					fixture.World.Despawn(npc);
				// A spot the bot's geometry calls sighted can still be refused by the server (STR_SKILL_OBSTACLE): try up to three.
				for (int spot = 0; spot < 3 && !next.IsDead(); spot++)
				{
					await TeleportNearAsync(at, [12f, 14f, 10f, 16f], sighted: true, skip: spot, map: Server().GetWorldId());
					next.GetLifeStats().SetCurrentHp(1);
					await session.SynchronizeAsync(token);
					if (!session.Api.World.Objects.ContainsKey(next.GetObjectId())) break;
					await NaturalAirCombat.ShootDownAsync(session, next.GetObjectId(), quest,
						(origin, skill, skillLevel, aim) => runtime.CreateSpellCast(session.Api.World, origin, skill, skillLevel, aim), token, maximumCasts: 6);
					await session.SynchronizeAsync(token);
				}
				if (!session.Api.World.Objects.ContainsKey(next.GetObjectId()) && !next.IsDead())
				{
					reasons.Add($"{next.GetObjectId()}: not in the client's view");
					continue;
				}
				if (!next.IsDead())
				{
					reasons.Add($"{next.GetObjectId()}: alive at {next.GetLifeStats().GetCurrentHp()} HP, probe HP {Server().GetLifeStats().GetCurrentHp()}" +
						$"{(Server().IsDead() ? " (dead)" : "")}, last messages " + string.Join("/", session.PacketHistory.TakeLast(40)
						.Where(packet => packet.PacketType == typeof(Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE))
						.Select(packet => packet.Get<object>("name")).TakeLast(3)));
					continue;
				}
				confirmedKills++;
				if (item is int wanted)
				{
					await TeleportNearAsync(new BotPosition(next.GetX(), next.GetY(), next.GetZ(), 0), [2f, 3f, 4f], map: Server().GetWorldId());
					bool looted = await NaturalAltgardQuestSteps.LootItemAsync(session, next.GetObjectId(), wanted, token);
					if (looted) dropped[next.GetNpcId()] = dropped.GetValueOrDefault(next.GetNpcId()) + 1;
					log.Add($"{next.GetNpcId()} {(looted ? "dropped" : "did not drop")} {wanted}");
				}
				else log.Add($"{next.GetNpcId()} killed");
				return;
			}
			throw new InvalidDataException($"No {string.Join("/", kinds)} could be shot down: {string.Join("; ", reasons)}");
		}
		async Task<int> NpcObjectAsync(int npcId, int map = altgard, BotPosition? hint = null)
		{
			var currentInstance = (Server().GetWorldId() == map ? Instance() : fixture.World.GetWorldMap(map).GetMainWorldMapInstance());
			var npc = currentInstance.GetNpcs().Where(candidate => candidate.GetNpcId() == npcId && !candidate.IsDead())
				.OrderBy(candidate => hint is BotPosition at ? NaturalFlightPolicy.Distance(at,
					new BotPosition(candidate.GetX(), candidate.GetY(), candidate.GetZ(), 0)) : 0).First();
			// Grak, Gulkalla and Banatisai are guards. Java AbstractAI refuses talks during a fight or return; clear the hostile target
			// as part of controlled probe setup and let him return before positioning the probe for its dialog.
			for (int wait = 0; wait < 60 && npc.GetAi().GetState() is not (Aion.GameServer.Ai.AIState.IDLE or Aion.GameServer.Ai.AIState.WALKING); wait++)
			{
				if (npc.GetTarget() is Aion.GameServer.Model.GameObjects.Npc target && npc.IsEnemy(target))
					fixture.World.Despawn(target);
				await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
				await session.SynchronizeAsync(token);
			}
			await TeleportNearAsync(new BotPosition(npc.GetX(), npc.GetY(), npc.GetZ(), 0), [2f, 3f, 4f], map: map);
			return await session.WaitForNpcAsync(npcId, token);
		}

		async Task TalkAsync(string key)
		{
			NaturalAltgardStep step = leg.Steps.Single(s => s.Key == key);
			int npc = await NpcObjectAsync(step.NpcId);
			Console.WriteLine("BC-04 " + await NaturalAltgardQuestSteps.TalkAsync(session, step, npc, token));
		}
		async Task WalkTalkAsync(int npcId)
		{
			var npc = Instance().GetNpcs(npcId).Single(n => !n.IsDead());
			var target = new BotPosition(npc.GetX(), npc.GetY(), npc.GetZ(), 0);
			float range = npc.GetObjectTemplate().GetTalkDistance();
			var routes = Geometry().GroundAround(Server().GetWorldId(), target, [range - 1, range - 2, 2f])
				.Where(at => NaturalFlightPolicy.Distance(at, target) <= range - 0.5f)
				.OrderBy(at => NaturalFlightPolicy.Distance(session.CurrentPosition, at))
				.Select(at => Geometry().FindJourneyPath(Server().GetWorldId(), session.CurrentPosition, at)).ToArray();
			var route = routes.FirstOrDefault(r => r.Count > 0);
			Assert.True(route != null || NaturalFlightPolicy.Distance(session.CurrentPosition, target) <= range,
				$"No ordinary recovery route to {npcId} from {session.CurrentPosition} ({Aion.Bots.Navigation.NavMesh.BotNavMeshRouter.LastOutcome}).");
			if (route != null)
			{
				foreach (var hostile in Instance().GetNpcs().Where(n => !n.IsDead() && NaturalHostility.IsAggressive(n.GetObjectTemplate(),
					fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) && route.Any(at =>
					MathF.Pow(n.GetX() - at.X, 2) + MathF.Pow(n.GetY() - at.Y, 2) <= 900)).ToArray()) fixture.World.Despawn(hostile);
				await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing).CreateGroundPlan(route,
					session.CurrentPosition, session.Api.World.MovementSpeed!.Value), token);
				await session.SynchronizeAsync(token);
			}
			Console.WriteLine($"BC-04 ordinary route to {npcId}: {route?.Count ?? 0} points");
		}
		async Task PortalAsync(int npcId, int duration, int destination)
		{
			await WalkTalkAsync(npcId);
			int npc = await session.WaitForNpcAsync(npcId, token);
			await NaturalDialogProtocol.OpenAsync(session, npc, token);
			var use = await session.WaitForPacketAsync(typeof(SM_USE_OBJECT), token,
				p => p.Get<int>("targetObjectId") == npc && p.Get<byte>("actionType") == 1);
			Assert.Equal(duration, use.Get<int>("durationMs"));
			session.Api.World.BeginWorldReload();
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(duration + 1), token);
			await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, p => p.Get<int>("objectId") == session.CharacterId);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
			Assert.Equal(destination, Server().GetWorldId());
			Console.WriteLine($"BC-04 portal {npcId}: {duration} ms -> {destination}, Q24016 {State(24016)}");
		}
		Aion.GameServer.Utils.PacketSendUtility.SendPacket(Server(), new SM_QUEST_COMPLETED_LIST(0,
			leg.Start.CompletedQuestIds.Select(id => Server().GetQuestStateList().GetQuestState(id)).ToList()));
		foreach ((int id, QuestStatus status) in new[] { (2900, QuestStatus.START), (24014, QuestStatus.START),
			(24015, QuestStatus.START), (24016, QuestStatus.LOCKED) })
		{
			var state = Server().GetQuestStateList().GetQuestState(id);
			if (state == null) { state = new QuestState(id, status); Assert.True(Server().GetQuestStateList().AddQuest(id, state)); }
			state.SetStatus(status); state.SetQuestVar(0);
			Aion.GameServer.Utils.PacketSendUtility.SendPacket(Server(), new SM_QUEST_ACTION(SM_QUEST_ACTION.ActionType.ADD, state));
		}
		await session.SynchronizeAsync(token);
		int stone = await NpcObjectAsync(700065);
		Assert.True((await new NaturalServiceSteps(session).BindAsync(stone, session.Api.World.Objects[stone].Position,
			altgard, leg.Bind!.Price, leg.Bind.AcceptRange, token)).IsDone);
		await TalkAsync("q24014-v0-dellalont");
		await TalkAsync("q24014-v1-jolk");
		await KillAndLootAsync([210751], 0, 182215360);
		Assert.Equal(0, ItemCount(182215360)); // QuestService.isQuestDrop requires collecting_step 5.
		Assert.Equal((NaturalAltgardDecisionEngine.Start, 2), State(24014));
		for (int i = 0; i < 3; i++) await KillAndLootAsync([210562], 24014, null);
		Assert.Equal((NaturalAltgardDecisionEngine.Start, 5), State(24014));
		await KillAndLootAsync([210751, 216893], 0, 182215360);
		Assert.Equal(1, ItemCount(182215360));
		await TalkAsync("q24014-v5-jolk");
		Assert.Equal(0, ItemCount(182215360));
		await TalkAsync("q24014-reward-jolk");
		Assert.Equal(QuestStatus.LOCKED, Server().GetQuestStateList().GetQuestState(24016).GetStatus());
		await TalkAsync("q24015-v0-taora");
		NaturalAltgardZoneStep zone = Assert.Single(leg.ZoneStepList);
		await TeleportNearAsync(new(zone.Anchor![0], zone.Anchor[1], zone.Anchor[2], 0), [2f, 3f, 4f]);
		Assert.Equal((NaturalAltgardDecisionEngine.Start, 2), State(24015));
		for (int i = 0; i < 3; i++) await KillAndLootAsync([700099], 24015, null);
		Assert.Equal(QuestStatus.REWARD, Server().GetQuestStateList().GetQuestState(24015).GetStatus());
		await TalkAsync("q24015-reward-suthran");
		Assert.Equal((NaturalAltgardDecisionEngine.Start, 0), State(24016));
		Console.WriteLine("BC-04 Q24014/Q24015 completed; all five prerequisites naturally unlock Q24016");
		NaturalAltgardInstanceTrip trip = Assert.Single(leg.InstanceTripList);
		int suthran = await NpcObjectAsync(203557);
		await NaturalDialogProtocol.OpenAsync(session, suthran, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(suthran, DialogAction.QUEST_SELECT, questId: 24016), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, p => p.Get<ushort>("dialogPageId") == 1693);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(suthran, DialogAction.SELECT3_1_1, questId: 24016), token);
		await NaturalMovieGate.FinishAsync(session, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, p => p.Get<ushort>("dialogPageId") == 1695);
		session.Api.World.BeginWorldReload();
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(suthran, DialogAction.SETPRO1, questId: 24016), token);
		await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, p => p.Get<int>("objectId") == session.CharacterId);
		session.AcceptTeleportPosition(); await session.SynchronizeAsync(token);
		Assert.Equal((NaturalAltgardDecisionEngine.Start, 1), State(24016));
		await PortalAsync(trip.PortalNpcId, trip.UseMillis, trip.MapId);
		Assert.Equal((NaturalAltgardDecisionEngine.Start, 2), State(24016));
		await PortalAsync(trip.ExitNpcId, trip.ExitUseMillis, altgard);
		Assert.Equal((NaturalAltgardDecisionEngine.Start, 1), State(24016));
		await PortalAsync(trip.PortalNpcId, trip.UseMillis, trip.MapId);
		Assert.Equal((NaturalAltgardDecisionEngine.Start, 2), State(24016));
		// The level-22, auto-learned Cleric skill is an ordinary option; the instance does not offer instance revival.
		BotSkill rebirth = session.Api.World.Skills[4005];
		await session.SendPacketAsync(session.Api.Target(session.CharacterId), token);
		await session.SendPacketAsync(session.Api.Cast(new SpellCastData(4005, checked((byte)rebirth.Level), 0)
			{ TargetObjectId = session.CharacterId }), token);
		var cast = await BotCastProtocol.WaitForStartAsync((predicate, waitToken) => session.WaitForPacketAsync(
			p => predicate(p) || BotCastProtocol.IsStartRejection(p), waitToken), session.CharacterId, 4005, token);
		Assert.Equal(typeof(SM_CASTSPELL), cast.PacketType);
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(cast.Get<ushort>("castDuration") + 1), token);
		var effect = await BotCastProtocol.WaitForCompletionAsync(session.WaitForPacketAsync, session.CharacterId, 4005, token);
		Assert.Equal(typeof(SM_CASTSPELL_RESULT), effect.PacketType);
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(effect.Get<ushort>("hitTime") + 1), token);
		await session.SynchronizeAsync(token);
		Assert.True(NaturalAltgardQuestSteps.HasEffect(session.Api.World, 4005));
		Assert.True(Server().GetController().Die(Server())); // Controlled death, real die hook and client revival protocol.
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(501), token);
		var death = await session.WaitForPacketAsync(typeof(SM_DIE), token);
		Assert.True(death.Get<bool>("allowReviveBySkill"));
		Assert.False(death.Get<bool>("allowInstanceRevive"));
		Assert.Equal((NaturalAltgardDecisionEngine.Start, 1), State(24016));
		await session.SendPacketAsync(session.Api.Revive(BotReviveType.Rebirth), token);
		await session.SynchronizeAsync(token);
		Assert.False(Server().IsDead());
		Assert.Equal(trip.MapId, Server().GetWorldId());
		Assert.Equal((NaturalAltgardDecisionEngine.Start, 1), State(24016));
		await PortalAsync(trip.ExitNpcId, trip.ExitUseMillis, altgard);
		await PortalAsync(trip.PortalNpcId, trip.UseMillis, trip.MapId);
		Assert.Equal((NaturalAltgardDecisionEngine.Start, 2), State(24016));
		Console.WriteLine("BC-04 controlled death -> learned Hand of Reincarnation self revival -> ordinary exit/re-entry at var 2");
		await WalkTalkAsync(700140);
		int guardian = await session.WaitForNpcAsync(700140, token);
		Assert.True(await NaturalAltgardQuestSteps.UseObjectAsync(session, guardian, null, token));
		Assert.Equal((NaturalAltgardDecisionEngine.Start, 13), State(24016));
		Assert.Single(Instance().GetNpcs(210753), n => !n.IsDead());
		await KillAndLootAsync([210753], 24016, null);
		Assert.Equal((NaturalAltgardDecisionEngine.Start, 14), State(24016));
		await WalkTalkAsync(700141);
		int gate = await session.WaitForNpcAsync(700141, token);
		Assert.True(await NaturalAltgardQuestSteps.UseObjectAsync(session, gate, null, token));
		Assert.Equal(QuestStatus.REWARD, Server().GetQuestStateList().GetQuestState(24016).GetStatus());
		session.Api.World.BeginWorldReload();
		await NaturalMovieGate.FinishAsync(session, token);
		await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, p => p.Get<int>("objectId") == session.CharacterId);
		session.AcceptTeleportPosition(); await session.SynchronizeAsync(token);
		Assert.Equal(altgard, Server().GetWorldId());
		await TalkAsync("q24016-reward-suthran");
		foreach (int id in new[] { 24014, 24015, 24016 })
		{
			Assert.Equal(QuestStatus.COMPLETE, Server().GetQuestStateList().GetQuestState(id).GetStatus());
			Assert.Contains(id, session.Api.World.CompletedQuestIds);
		}
		Assert.Equal((NaturalAltgardDecisionEngine.Start, 0), State(2900));
		Console.WriteLine($"BC-04 all three campaigns complete; Orb consumed; {confirmedKills} controlled kills, " +
			$"{ordinaryRespawns} ordinary waits/{respawns} setup replenishments; one controlled death/self revival, ordinary leave/re-entry; Q2900 preserved");
		policy.AssertClean();
	}
}
