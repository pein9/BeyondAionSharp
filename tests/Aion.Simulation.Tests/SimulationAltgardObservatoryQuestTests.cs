using Aion.Bots.Navigation;
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
	/// <summary>AO-02: both Observatory scripts and all five templates, Operation Order use/loot, held states and hub flights. GM setup on free account 208 only.</summary>
	[SkippableFact]
	public async Task ObservatoryQuestsAndOperationOrderPlayThroughTheirContracts()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, level = 21;
		using var policy = NewPolicy("AO02", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l8");
		IReadOnlyDictionary<int, QuestRunPlan> plans = NaturalAltgardContract.LoadPlans("l8");
		IReadOnlyDictionary<int, NaturalTemplateObjective> objectives = NaturalTemplateObjective.From(plans);
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? $"ao02-quests-s{fixture.Seed}";
		string tracePath = Path.Combine(Aion.GameServer.TestKit.RealStaticData.RepoRoot(), "run", $"{run}.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(tracePath, run, "b01", "sim-player-208",
			virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 208, "Asimobsquest", Race.ASMODIANS, trace, tracePath);
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
		var runtime = new NaturalJourneyRuntime(Aion.GameServer.TestKit.RealStaticData.RepoRoot(), "SIM-ao02", fixture.Seed, fixture.DataManager.StaticData,
			() => fixture.Clock.NowMillis, fixture.Epoch, () => geometry, _ => Task.FromResult(false), policy.AssertClean,
			() => policy.SnapshotProblems(), null!, new Aion.Bots.Dashboard.LiveBotDashboardState());
		long ItemCount(int itemId) => session.Api.World.Inventory.Values.Where(owned => owned.ItemId == itemId).Sum(owned => owned.Count);
		(byte Status, int Var)? State(int id) => NaturalAltgardQuestSteps.State(session.Api.World, id);
		int? WorkItem(int quest) => fixture.DataManager.StaticData.Quests.GetQuestById(quest).GetQuestWorkItems()?.GetQuestWorkItem()
			.SingleOrDefault()?.GetItemId();
		var unusable = new HashSet<int>();
		var dropped = new Dictionary<int, int>();
		int respawns = 0;
		var log = new List<string>();

		// Java sends no SM_DELETE for what the player saw while it teleports (PlayerController.notSee), so the client drops its
		// view first, as it does for a natural teleport (AG-04).
		async Task TeleportNearAsync(BotPosition at, float[] radii, bool sighted = false, int skip = 0)
		{
			BotPosition ground = geometry.GroundAround(altgard, at, radii)
				.Where(point => !sighted || geometry.HasLineOfSight(altgard, point with { Z = point.Z + 1.6f }, at with { Z = at.Z + 1 }))
				.Skip(skip).First();
			foreach (var npc in instance.GetNpcs().Where(npc => !npc.IsDead() &&
				NaturalHostility.IsAggressive(npc.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
				MathF.Pow(npc.GetX() - ground.X, 2) + MathF.Pow(npc.GetY() - ground.Y, 2) <= 40 * 40).ToArray())
			{
				// Preserve the objective victim at a sighted stand-off; ordinary talks clear every hostile neighbour.
				if (sighted && MathF.Pow(npc.GetX() - at.X, 2) + MathF.Pow(npc.GetY() - at.Y, 2) < 1) continue;
				fixture.World.Despawn(npc);
			}
			session.Api.World.BeginWorldReload();
			await TeleportForSetupAsync(session, Server(), altgard, ground.X, ground.Y, ground.Z, token);
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
			var spawned = Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(new SpawnTemplate(new SpawnGroup(instance.GetMapId(),
				group.GetNpcId(), 0, null), spot.GetX(), spot.GetY(), spot.GetZ(), spot.GetHeading(), 0, null, 0), instance.GetInstanceId());
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
				var next = Candidates(kinds).FirstOrDefault() ?? RespawnShipped(kinds);
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
		async Task<int> NpcObjectAsync(int npcId)
		{
			var npc = instance.GetNpcs().First(candidate => candidate.GetNpcId() == npcId);
			// Neifenmer is a guard. Java AbstractAI refuses talks during a fight or return; clear his hostile target
			// as part of controlled probe setup and let him return before positioning the probe for its dialog.
			for (int wait = 0; wait < 60 && npc.GetAi().GetState() is not (Aion.GameServer.Ai.AIState.IDLE or Aion.GameServer.Ai.AIState.WALKING); wait++)
			{
				if (npc.GetTarget() is Aion.GameServer.Model.GameObjects.Npc target && npc.IsEnemy(target))
					fixture.World.Despawn(target);
				await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
				await session.SynchronizeAsync(token);
			}
			await TeleportNearAsync(new BotPosition(npc.GetX(), npc.GetY(), npc.GetZ(), 0), [2f, 3f, 4f]);
			return await session.WaitForNpcAsync(npcId, token);
		}
		async Task AcceptAsync(int quest)
		{
			session.BeginStep($"s-accept-{quest}", $"accept-{quest}");
			await session.StartQuestAsync(await NpcObjectAsync(plans[quest].StartNpcs.First().Id), quest, token);
			Assert.Equal(QuestStatus.START, Server().GetQuestStateList().GetQuestState(quest).GetStatus());
			if (WorkItem(quest) is int work) Assert.Equal(1, ItemCount(work));
		}
		// The objective from the plan: kills until the counter is full, or kills and loots until the items are held.
		async Task WorkAsync(int quest, int budget)
		{
			session.BeginStep($"s-work-{quest}", $"work-{quest}");
			NaturalTemplateObjective objective = objectives[quest];
			bool Done() => objective.IsDone(session.Api.World.Quests.GetValueOrDefault(quest),
				objective.ItemId is int item ? new Dictionary<int, long> { [item] = ItemCount(item) } : new Dictionary<int, long>());
			int kills = 0;
			while (!Done())
			{
				Assert.True(++kills <= budget, $"Q{quest} at {State(quest)} after {kills - 1} kills");
				await KillAndLootAsync(Kinds(quest), objective.ItemId is null ? quest : 0, objective.ItemId);
			}
			log.Add($"Q{quest}: {kills} kills, at {State(quest)}");
		}
		async Task ClaimAsync(int quest)
		{
			session.BeginStep($"s-claim-{quest}", $"claim-{quest}");
			long collectedBefore = objectives[quest].ItemId is int collected ? ItemCount(collected) : 0;
			int npc = await NpcObjectAsync(plans[quest].EndNpcs.First().Id);
			await NaturalDialogProtocol.OpenAsync(session, npc, token);
			await session.WaitForPacketAsync(typeof(Aion.GameServer.Network.Aion.ServerPackets.SM_DIALOG_WINDOW), token);
			await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc, DialogAction.QUEST_SELECT, questId: quest), token);
			await session.WaitForPacketAsync(typeof(Aion.GameServer.Network.Aion.ServerPackets.SM_DIALOG_WINDOW), token);
			if (State(quest)?.Status == NaturalAltgardDecisionEngine.Start)
			{
				// ItemCollecting checks the items at the end NPC; an old-style MonsterHunt is still at START with its counter full,
				// and a ReportTo is at START; SELECT_QUEST_REWARD moves either to REWARD (Java MonsterHunt and ReportTo
				// onDialogEvent). Each then offers the reward.
				int check = plans[quest].Template == "item_collecting" ? DialogAction.CHECK_USER_HAS_QUEST_ITEM : DialogAction.SELECT_QUEST_REWARD;
				await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc, checked((ushort)check), questId: quest), token);
				await session.WaitForPacketAsync(typeof(Aion.GameServer.Network.Aion.ServerPackets.SM_DIALOG_WINDOW), token);
			}
			await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc, DialogAction.SELECTED_QUEST_NOREWARD, questId: quest), token);
			await session.SynchronizeAsync(token);
			await session.SendPacketAsync(session.Api.CloseDialog(npc), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(QuestStatus.COMPLETE, Server().GetQuestStateList().GetQuestState(quest).GetStatus());
			if (objectives[quest].ItemId is int item)
				Assert.Equal(collectedBefore - objectives[quest].ItemCount, ItemCount(item));
			if (WorkItem(quest) is int work) Assert.Equal(0, ItemCount(work));
			log.Add($"Q{quest} complete");
		}
		async Task FlyAsync(int npcId, int destinationNpc)
		{
			int pad = await NpcObjectAsync(npcId);
			var destination = instance.GetNpcs().First(npc => npc.GetNpcId() == destinationNpc);
			NaturalAirlineJourney journey = Assert.IsType<NaturalAirlineJourney>(NaturalAirlineRoutes.Journey(
				NaturalAirlineRoutes.Load(Aion.GameServer.TestKit.RealStaticData.RepoRoot()), altgard, session.CurrentPosition,
				new BotPosition(destination.GetX(), destination.GetY(), destination.GetZ(), 0)));
			NaturalAirlineRoute flight = Assert.Single(journey.Flights);
			Assert.Equal(npcId, flight.NpcId);
			NaturalServiceOutcome result = await new NaturalServiceSteps(session).FlyAsync(pad,
				session.Api.World.Objects[pad].Position, 6, flight, token);
			Assert.True(result.IsDone, result.Reason);
			log.Add(result.Reason);
		}

		// Publish the completed prerequisites and the held/campaign states from altgard-l7 to the probe's client.
		Aion.GameServer.Utils.PacketSendUtility.SendPacket(Server(), new SM_QUEST_COMPLETED_LIST(0,
			leg.Start.CompletedQuestIds.Select(id => Server().GetQuestStateList().GetQuestState(id)).ToList()));
		foreach ((int id, QuestStatus status, int variable) in new[]
		{
			(2146, QuestStatus.START, 0), (24115, QuestStatus.START, 3), (2900, QuestStatus.START, 0),
			(24014, QuestStatus.START, 0), (24015, QuestStatus.START, 0), (24016, QuestStatus.LOCKED, 0),
		})
		{
			var state = Server().GetQuestStateList().GetQuestState(id);
			if (state == null)
			{
				state = new QuestState(id, status);
				Assert.True(Server().GetQuestStateList().AddQuest(id, state));
			}
			state.SetStatus(status);
			state.SetQuestVar(variable);
			Aion.GameServer.Utils.PacketSendUtility.SendPacket(Server(), new SM_QUEST_ACTION(SM_QUEST_ACTION.ActionType.ADD, state));
		}
		Assert.Equal(0, Aion.GameServer.Services.Items.ItemService.AddItem(Server(), 182215477, 1));
		await session.SynchronizeAsync(token);

		async Task PlayAsync(string key)
		{
			NaturalAltgardStep step = leg.Steps.Single(candidate => candidate.Key == key);
			session.BeginStep(key, key);
			int npc = await NpcObjectAsync(step.NpcId);
			session.TraceDiagnostic("probe-talk", new Dictionary<string, object?>
			{
				["client"] = session.CurrentPosition, ["server"] = new[] { Server().GetX(), Server().GetY(), Server().GetZ() },
				["npc"] = npc, ["known"] = session.Api.World.Objects[npc].Position,
				["serverKnowsNpc"] = Server().GetKnownList().GetObject(npc) != null,
				["trading"] = Server().IsTrading(),
			});
			using var dialogWait = CancellationTokenSource.CreateLinkedTokenSource(token);
			dialogWait.CancelAfter(TimeSpan.FromSeconds(20));
			log.Add(await NaturalAltgardQuestSteps.TalkAsync(session, step, npc, dialogWait.Token));
		}
		await PlayAsync("q2266-offer-valurion");
		await FlyAsync(203561, 700822);
		await PlayAsync("q2266-v0-neifenmer");
		await PlayAsync("q2266-v1-aurtri");
		foreach (int quest in new[] { 2267, 2268 })
		{
			await AcceptAsync(quest);
			await WorkAsync(quest, 24);
			await ClaimAsync(quest);
		}
		await AcceptAsync(2269);
		int order = await NpcObjectAsync(700010);
		int beforeUse = session.PacketHistory.Count;
		Assert.True(await NaturalAltgardQuestSteps.UseObjectAsync(session, order, 182203245, token));
		Assert.Equal(3000, session.PacketHistory.Skip(beforeUse).First(packet => packet.PacketType == typeof(SM_USE_OBJECT)
			&& packet.Get<int>("targetObjectId") == order && packet.Get<byte>("actionType") != 2).Get<int>("durationMs"));
		Assert.Equal(1, ItemCount(182203245));
		log.Add("Q2269: Operation Order used for 3000 ms and looted");
		await ClaimAsync(2269);
		foreach (int quest in new[] { 2270, 24113 })
		{
			await AcceptAsync(quest);
			await WorkAsync(quest, 24);
			if (quest == 2270)
			{
				// RC-11's full run naturally looted four insignia. Labelled probe supply exercises that surplus.
				Assert.Equal(3, ItemCount(182203246));
				Assert.Equal(0, Aion.GameServer.Services.Items.ItemService.AddItem(Server(), 182203246, 1));
				await session.SynchronizeAsync(token);
				Assert.Equal(4, ItemCount(182203246));
				session.TraceDiagnostic("probe-surplus-insignia", new Dictionary<string, object?>
				{
					["quest"] = 2270, ["item"] = 182203246, ["supplied"] = 1, ["incoming"] = 4,
				});
			}
			await ClaimAsync(quest);
		}
		await PlayAsync("q2271-offer-neifenmer");
		await PlayAsync("q2271-v0-aurtri");
		await FlyAsync(205259, 203557);
		await PlayAsync("q2271-v1-suthran");
		await FlyAsync(203561, 700822);
		foreach (int id in leg.Endpoint.CompletedQuestIds)
		{
			Assert.Equal(QuestStatus.COMPLETE, Server().GetQuestStateList().GetQuestState(id).GetStatus());
			Assert.Contains(id, session.Api.World.CompletedQuestIds);
		}
		foreach ((int id, byte status, int variable) in new[]
		{
			(2146, (byte)3, 0), (24115, (byte)3, 3), (2900, (byte)3, 0),
			(24014, (byte)3, 0), (24015, (byte)3, 0), (24016, (byte)6, 0),
		}) Assert.Equal((status, variable), State(id));
		Assert.Equal(1, ItemCount(182215477));
		Assert.Equal(1, ItemCount(182203246));
		int insigniaObject = session.Api.World.Inventory.Values.Single(i => i.ItemId == 182203246).ObjectId;
		await session.QuitAsync(token);
		await session.WaitForReentryAsync(token);
		await session.ReloginExistingCharacterAsync(token);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Assert.Equal(1, session.Api.World.Inventory[insigniaObject].Count);
		Assert.Equal(1, session.Api.World.CompletedQuestCounts[2270]);
		Console.WriteLine("RC-11 Q2270: native hand-in consumed three of four insignia; one surplus and one completion survive relog.");
		Console.WriteLine($"AO-02 {string.Join("; ", log)}; held quests and campaign states preserved");
		policy.AssertClean();
	}
}
