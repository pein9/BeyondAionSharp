using Aion.Bots.Navigation;
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
	/// <summary>BC-03: six Black Claw templates, independent counters, ordinary source respawn and real Vidar delivery/Return. Free probe 214 only.</summary>
	[SkippableFact]
	public async Task BlackClawTemplatesRequireEveryCounterAndDeliverTheWorkItemToVidar()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, level = 22;
		using var policy = NewPolicy("BC03", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l10");
		IReadOnlyDictionary<int, QuestRunPlan> plans = NaturalAltgardContract.LoadPlans("l10");
		IReadOnlyDictionary<int, NaturalTemplateObjective> objectives = NaturalTemplateObjective.From(plans);
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? $"bc03-quests-s{fixture.Seed}";
		string tracePath = Path.Combine(Aion.GameServer.TestKit.RealStaticData.RepoRoot(), "run", $"{run}.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(tracePath, run, "b01", "sim-player-214",
			virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 214, "Asimclawquest", Race.ASMODIANS, trace, tracePath);
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
		var instance = fixture.World.GetWorldMap(altgard).GetMainWorldMapInstance();
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(instance.GetInstanceId(), Race.ASMODIANS);
		var runtime = new NaturalJourneyRuntime(Aion.GameServer.TestKit.RealStaticData.RepoRoot(), "SIM-bc03", fixture.Seed, fixture.DataManager.StaticData,
			() => fixture.Clock.NowMillis, fixture.Epoch, () => geometry, _ => Task.FromResult(false), policy.AssertClean,
			() => policy.SnapshotProblems(), null!, new Aion.Bots.Dashboard.LiveBotDashboardState());
		long ItemCount(int itemId) => session.Api.World.Inventory.Values.Where(owned => owned.ItemId == itemId).Sum(owned => owned.Count);
		(byte Status, int Var)? State(int id) => NaturalAltgardQuestSteps.State(session.Api.World, id);
		int? WorkItem(int quest) => fixture.DataManager.StaticData.Quests.GetQuestById(quest).GetQuestWorkItems()?.GetQuestWorkItem()
			.SingleOrDefault()?.GetItemId();
		var unusable = new HashSet<int>();
		var dropped = new Dictionary<int, int>();
		int respawns = 0, ordinaryRespawns = 0, confirmedKills = 0;
		Aion.GameServer.Model.GameObjects.Npc? firstHunter = null;
		var log = new List<string>();

		// Java sends no SM_DELETE for what the player saw while it teleports (PlayerController.notSee), so the client drops its
		// view first, as it does for a natural teleport (AG-04).
		async Task TeleportNearAsync(BotPosition at, float[] radii, bool sighted = false, int skip = 0, int map = altgard)
		{
			var currentInstance = fixture.World.GetWorldMap(map).GetMainWorldMapInstance();
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
			await TeleportForSetupAsync(session, Server(), map, ground.X, ground.Y, ground.Z, token);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
		}
		// The kill targets or drop sources of a quest's objective step (not its report NPC).
		int[] Kinds(int quest) => plans[quest].Steps.Where(step => step.Kind is "kill" or "collect").SelectMany(step =>
			step.Sources.Select(source => source.NpcId ?? 0).Concat(step.Npcs.Select(npc => npc.Id)))
			.Where(id => id > 0).Distinct().ToArray();
		IEnumerable<Aion.GameServer.Model.GameObjects.Npc> Candidates(int[] kinds) => instance.GetNpcs()
			.Where(npc => kinds.Contains(npc.GetNpcId()) && !npc.IsDead() && !unusable.Contains(npc.GetObjectId()))
			.OrderBy(npc => MathF.Pow(npc.GetX() - session.CurrentPosition.X, 2) + MathF.Pow(npc.GetY() - session.CurrentPosition.Y, 2));
		// The shared Fast world keeps what earlier probes despawned (AG-03 clears around Gerger), and a despawned monster never
		// returns: spawn a fresh one at the nearest shipped spot of its kind (GM setup on the probe world).
		Aion.GameServer.Model.GameObjects.Npc RespawnShipped(int[] kinds)
		{
			SpawnGroup group = fixture.DataManager.StaticData.SpawnsDh.GetSpawnsByWorldId(instance.GetMapId())
				.First(candidate => kinds.Contains(candidate.GetNpcId()));
			var spot = group.GetSpawnTemplates().OrderBy(template =>
				MathF.Pow(template.GetX() - session.CurrentPosition.X, 2) + MathF.Pow(template.GetY() - session.CurrentPosition.Y, 2))
				.Skip(respawns++ % group.GetSpawnTemplates().Count).First();
			var spawned = Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(new SpawnTemplate(group, spot.GetX(), spot.GetY(), spot.GetZ(), spot.GetHeading(), 0, null, 0), instance.GetInstanceId());
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
				if (!geometry.GroundAround(altgard, at, [12f, 14f, 10f, 16f])
					.Any(point => geometry.HasLineOfSight(altgard, point with { Z = point.Z + 1.6f }, at with { Z = at.Z + 1 })))
				{
					reasons.Add($"{next.GetObjectId()}: no sighted ground");
					continue;
				}
				// Aggressive neighbours are cleared, but not the kinds being hunted: a dense camp would otherwise be emptied of them.
				foreach (var npc in instance.GetNpcs().Where(npc => !kinds.Contains(npc.GetNpcId()) && !npc.IsDead() &&
					NaturalHostility.IsAggressive(npc.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
					MathF.Pow(npc.GetX() - next.GetX(), 2) + MathF.Pow(npc.GetY() - next.GetY(), 2) <= 25 * 25).ToArray())
					fixture.World.Despawn(npc);
				// A spot the bot's geometry calls sighted can still be refused by the server (STR_SKILL_OBSTACLE): try up to three.
				for (int spot = 0; spot < 3 && !next.IsDead(); spot++)
				{
					await TeleportNearAsync(at, [12f, 14f, 10f, 16f], sighted: true, skip: spot);
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
				if (kinds.Contains(210551) || kinds.Contains(210552)) firstHunter ??= next;
				if (item is int wanted)
				{
					await TeleportNearAsync(new BotPosition(next.GetX(), next.GetY(), next.GetZ(), 0), [2f, 3f, 4f]);
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
			var currentInstance = fixture.World.GetWorldMap(map).GetMainWorldMapInstance();
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
		async Task AcceptAsync(int quest)
		{
			session.BeginStep($"s-accept-{quest}", $"accept-{quest}");
			await session.StartQuestAsync(await NpcObjectAsync(plans[quest].StartNpcs.First().Id), quest, token);
			Assert.Equal(QuestStatus.START, Server().GetQuestStateList().GetQuestState(quest).GetStatus());
			if (WorkItem(quest) is int work) Assert.Equal(1, ItemCount(work));
		}
		int Counter(int quest, int slot) => (session.Api.World.Quests[quest].StepAndFlags >> (slot * 6)) & 0x3F;
		async Task RejectPartialAsync()
		{
			int npc = await NpcObjectAsync(203558);
			await NaturalDialogProtocol.OpenAsync(session, npc, token);
			await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
			await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc, DialogAction.QUEST_SELECT, questId: 2281), token);
			await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
			int before = session.Api.World.Quests[2281].StepAndFlags;
			await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialogExpectRejection(npc, DialogAction.SELECT_QUEST_REWARD, questId: 2281), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(new PendingQuestDialogAction(npc, DialogAction.SELECT_QUEST_REWARD, 2281),
				session.Api.QuestDialogEchoes.ConsumeExpectedRejection());
			Assert.Equal(before, session.Api.World.Quests[2281].StepAndFlags);
			Assert.Equal(QuestStatus.START, Server().GetQuestStateList().GetQuestState(2281).GetStatus());
			Assert.False(objectives[2281].IsDone(session.Api.World.Quests[2281], new Dictionary<int, long>()));
			await session.SendPacketAsync(session.Api.CloseDialog(npc), token);
			Console.WriteLine($"BC-03 Q2281 partial hand-in refused at ({Counter(2281, 0)}, {Counter(2281, 1)}, {Counter(2281, 2)})");
		}
		async Task WorkAsync(int quest, int budget)
		{
			session.BeginStep($"s-work-{quest}", $"work-{quest}");
			NaturalTemplateObjective objective = objectives[quest];
			int kills = 0;
			if (objective.ItemId is int item)
			{
				while (ItemCount(item) < objective.ItemCount)
				{
					Assert.True(++kills <= budget, $"Q{quest} still lacks its items after {kills - 1} kills.");
					await KillAndLootAsync(Kinds(quest), 0, item);
					if (quest == 2277 && kills == 1)
					{
						var hunter = Assert.IsType<Aion.GameServer.Model.GameObjects.Npc>(firstHunter);
						var spawn = Assert.IsType<SpawnTemplate>(hunter.GetSpawn());
						int wait = spawn.GetRespawnTime();
						Assert.InRange(wait, 295, 300);
						await NpcObjectAsync(700065);
						await session.AdvanceAsync(TimeSpan.FromSeconds(wait + 1), token);
						await session.SynchronizeAsync(token);
						Assert.Contains(instance.GetNpcs(), npc => npc.GetNpcId() == hunter.GetNpcId() && !npc.IsDead() &&
							npc.GetSpawn() is SpawnTemplate respawn && MathF.Abs(respawn.GetX() - spawn.GetX()) < 0.1f &&
							MathF.Abs(respawn.GetY() - spawn.GetY()) < 0.1f);
						unusable.Clear();
						ordinaryRespawns++;
						Console.WriteLine($"BC-03 hunter {hunter.GetNpcId()} respawned from its shipped spot after {wait + 1} seconds");
					}
				}
			}
			else foreach (QuestRunStep step in plans[quest].Steps.Where(s => s.Kind == "kill"))
			{
				int slot = step.Data.GetProperty("var").GetInt32();
				while (Counter(quest, slot) < step.Count)
				{
					Assert.True(++kills <= budget, $"Q{quest} counter {slot} still lacks kills.");
					await KillAndLootAsync(step.Npcs.Select(n => n.Id).ToArray(), quest, null);
				}
				Assert.Equal(step.Count, Server().GetQuestStateList().GetQuestState(quest).GetQuestVarById(slot));
				Console.WriteLine($"BC-03 Q{quest} counter {slot} = {Counter(quest, slot)}");
				if (quest == 2281 && slot < 2) await RejectPartialAsync();
			}
			Assert.True(objective.IsDone(session.Api.World.Quests[quest],
				objective.ItemId is int held ? new Dictionary<int, long> { [held] = ItemCount(held) } : new Dictionary<int, long>()));
			log.Add($"Q{quest}: {kills} kills, objectives full");
		}

		async Task ClaimAsync(int quest)
		{
			session.BeginStep($"s-claim-{quest}", $"claim-{quest}");
			QuestRunNpc recipient = plans[quest].EndNpcs.First();
			QuestRunPosition at = recipient.Positions.First(position => !position.ConditionalEvent);
			if (at.MapId != altgard) Assert.Equal(at.MapId, session.Api.World.MapId);
			int npc = await NpcObjectAsync(recipient.Id, at.MapId, new BotPosition(at.X, at.Y, at.Z, 0));
			using var dialogWait = CancellationTokenSource.CreateLinkedTokenSource(token);
			dialogWait.CancelAfter(TimeSpan.FromSeconds(20));
			CancellationToken claimToken = dialogWait.Token;
			await NaturalDialogProtocol.OpenAsync(session, npc, claimToken);
			await session.WaitForPacketAsync(typeof(Aion.GameServer.Network.Aion.ServerPackets.SM_DIALOG_WINDOW), claimToken);
			await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc, DialogAction.QUEST_SELECT, questId: quest), claimToken);
			await session.WaitForPacketAsync(typeof(Aion.GameServer.Network.Aion.ServerPackets.SM_DIALOG_WINDOW), claimToken);
			if (State(quest)?.Status == NaturalAltgardDecisionEngine.Start)
			{
				// ItemCollecting checks the items at the end NPC; an old-style MonsterHunt is still at START with its counter full,
				// and a ReportTo is at START; SELECT_QUEST_REWARD moves either to REWARD (Java MonsterHunt and ReportTo
				// onDialogEvent). Each then offers the reward.
				int check = plans[quest].Template == "item_collecting" ? DialogAction.CHECK_USER_HAS_QUEST_ITEM : DialogAction.SELECT_QUEST_REWARD;
				await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc, checked((ushort)check), questId: quest), claimToken);
				await session.WaitForPacketAsync(typeof(Aion.GameServer.Network.Aion.ServerPackets.SM_DIALOG_WINDOW), claimToken);
			}
			await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc, DialogAction.SELECTED_QUEST_NOREWARD, questId: quest), claimToken);
			await session.SynchronizeAsync(token);
			await session.SendPacketAsync(session.Api.CloseDialog(npc), claimToken);
			await session.SynchronizeAsync(token);
			Assert.Equal(QuestStatus.COMPLETE, Server().GetQuestStateList().GetQuestState(quest).GetStatus());
			if (objectives[quest].ItemId is int item) Assert.Equal(0, ItemCount(item));
			if (WorkItem(quest) is int work) Assert.Equal(0, ItemCount(work));
			log.Add($"Q{quest} complete");
			Console.WriteLine($"BC-03 Q{quest} complete on {at.MapId}; work and objective items consumed");
		}
		Aion.GameServer.Utils.PacketSendUtility.SendPacket(Server(), new SM_QUEST_COMPLETED_LIST(0,
			leg.Start.CompletedQuestIds.Select(id => Server().GetQuestStateList().GetQuestState(id)).ToList()));
		foreach ((int id, QuestStatus status) in new[] { (2900, QuestStatus.START), (24014, QuestStatus.START),
			(24015, QuestStatus.START), (24016, QuestStatus.LOCKED) })
		{
			var state = Server().GetQuestStateList().GetQuestState(id);
			if (state == null) { state = new QuestState(id, status); Assert.True(Server().GetQuestStateList().AddQuest(id, state)); }
			state.SetStatus(status);
			state.SetQuestVar(0);
			Aion.GameServer.Utils.PacketSendUtility.SendPacket(Server(), new SM_QUEST_ACTION(SM_QUEST_ACTION.ActionType.ADD, state));
		}
		await session.SynchronizeAsync(token);
		int stone = await NpcObjectAsync(leg.Bind!.NpcId);
		Assert.True((await new NaturalServiceSteps(session).BindAsync(stone, session.Api.World.Objects[stone].Position,
			altgard, leg.Bind.Price, leg.Bind.AcceptRange, token)).IsDone);
		foreach (int quest in new[] { 2273, 2277, 2280, 2281, 2282 })
		{
			await AcceptAsync(quest);
			await WorkAsync(quest, 40);
			await ClaimAsync(quest);
		}
		await AcceptAsync(2283);
		Assert.Equal(1, ItemCount(182203255));
		NaturalAltgardMapTrip trip = Assert.Single(leg.MapTripList);
		int teleporter = await NpcObjectAsync(trip.TeleporterNpcId);
		NaturalServiceOutcome travelled = await new NaturalServiceSteps(session).TeleportAsync(teleporter,
			session.Api.World.Objects[teleporter].Position, trip.TalkRange, trip.LocationId, trip.Fare, trip.MapId, token);
		Assert.True(travelled.IsDone, travelled.Reason);
		log.Add(travelled.Reason);
		await ClaimAsync(2283);

		BotSkill learned = session.Api.World.Skills[243];
		await session.SendPacketAsync(session.Api.Target(session.CharacterId), token);
		await session.SendPacketAsync(session.Api.Cast(new SpellCastData(243, checked((byte)learned.Level), 0)
			{ TargetObjectId = session.CharacterId }), token);
		DecodedBotServerPacket started = await BotCastProtocol.WaitForStartAsync(
			(predicate, waitToken) => session.WaitForPacketAsync(packet => predicate(packet) || BotCastProtocol.IsStartRejection(packet), waitToken),
			session.CharacterId, 243, token);
		Assert.Equal(typeof(SM_CASTSPELL), started.PacketType);
		session.Api.World.BeginWorldReload();
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(started.Get<ushort>("castDuration") + 1), token);
		DecodedBotServerPacket returned = await BotCastProtocol.WaitForCompletionAsync(session.WaitForPacketAsync, session.CharacterId, 243, token);
		Assert.Equal(typeof(SM_CASTSPELL_RESULT), returned.PacketType);
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(returned.Get<ushort>("hitTime") + 1), token);
		await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
		Assert.Equal(altgard, session.Api.World.MapId);
		Assert.False(Server().IsDead());
		Assert.True(NaturalFlightPolicy.Distance(new BotPosition(Server().GetX(), Server().GetY(), Server().GetZ(), 0),
			new BotPosition(leg.Hub.Anchor[0], leg.Hub.Anchor[1], leg.Hub.Anchor[2], 0)) <= 60);
		log.Add("Learned Return to the fortress bind");
		foreach (int id in plans.Keys)
		{
			Assert.Equal(QuestStatus.COMPLETE, Server().GetQuestStateList().GetQuestState(id).GetStatus());
			Assert.Contains(id, session.Api.World.CompletedQuestIds);
		}
		foreach ((int id, byte status) in new[] { (2900, (byte)3), (24014, (byte)3), (24015, (byte)3), (24016, (byte)6) })
			Assert.Equal((status, 0), State(id));
		Assert.Equal(0, ItemCount(182203255));
		Assert.True(ordinaryRespawns > 0);
		Console.WriteLine($"BC-03 {string.Join("; ", log)}; six hand-ins/work items consumed; {confirmedKills} confirmed kills, " +
			$"{ordinaryRespawns} ordinary source waits, {respawns} controlled replenishments; campaigns preserved");
		policy.AssertClean();
	}
}
