using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// AK-06: Stop 5 and the east, the Leg 5 template quests (Java MonsterHunt, ItemCollecting and ReportTo), from their plans:
	/// <list type="bullet">
	/// <item>at Kaibech's campsite, Mantigar's Q2233 then Q2234 (it needs Q2233), and Q2235;</item>
	/// <item>at Vovetirn, Q2241's five Glowing Mushrooms (80%) from the outlaws, then Q2242 taken and held for Gemyu;</item>
	/// <item>at Brodir, Sumarhon's camp: Q24230 (nine kills) and Q24231 (eight swords).</item>
	/// </list>
	/// GM setup on the probe only: its class, level and skills, the Leg 5 start's completed quests, setup teleports, and each
	/// target set to 1 HP with its aggressive neighbours cleared (the fights are AK-07's).
	/// </summary>
	[SkippableFact]
	public async Task EastAndSumarhonQuestsPlayThroughTheirPlans()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000;
		using var policy = NewPolicy("AK06", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l5");
		IReadOnlyDictionary<int, QuestRunPlan> plans = NaturalAltgardContract.LoadPlans("l5");
		IReadOnlyDictionary<int, NaturalTemplateObjective> objectives = NaturalTemplateObjective.From(plans);
		await using var session = new SimulationL0Session(fixture, policy, "b01", 76, "Asimeast", Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player Server() => fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(Server(), PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		Server().GetCommonData().SetLevel(19);
		SkillLearnService.LearnNewSkills(Server(), 1, 19);
		foreach (int id in leg.Start.CompletedQuestIds.Where(id => Server().GetQuestStateList().GetQuestState(id) == null))
			Assert.True(Server().GetQuestStateList().AddQuest(id, new QuestState(id, QuestStatus.COMPLETE)));
		var instance = fixture.World.GetWorldMap(altgard).GetMainWorldMapInstance();
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(instance.GetInstanceId(), Race.ASMODIANS);
		var runtime = new NaturalJourneyRuntime(Aion.GameServer.TestKit.RealStaticData.RepoRoot(), "SIM-ak06", fixture.Seed, fixture.DataManager.StaticData,
			() => fixture.Clock.NowMillis, fixture.Epoch, () => geometry, _ => Task.FromResult(false), policy.AssertClean,
			() => policy.SnapshotProblems(), null!, new Aion.Bots.Dashboard.LiveBotDashboardState());
		long ItemCount(int itemId) => session.Api.World.Inventory.Values.Where(owned => owned.ItemId == itemId).Sum(owned => owned.Count);
		(byte Status, int Var)? State(int id) => NaturalAltgardQuestSteps.State(session.Api.World, id);
		var unusable = new HashSet<int>();
		int respawns = 0;
		var log = new List<string>();

		async Task TeleportNearAsync(BotPosition at, float[] radii, bool sighted = false, int skip = 0)
		{
			BotPosition ground = geometry.GroundAround(altgard, at, radii)
				.Where(point => !sighted || geometry.HasLineOfSight(altgard, point with { Z = point.Z + 1.6f }, at with { Z = at.Z + 1 }))
				.Skip(skip).First();
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
		// The shared Fast world keeps what earlier probes despawned (AB-06 clears Sumarhon's camp), and a despawned monster
		// never returns: spawn a fresh one at the nearest shipped spot of its kind (GM setup on the probe world).
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
			await TeleportNearAsync(new BotPosition(npc.GetX(), npc.GetY(), npc.GetZ(), 0), [2f, 3f, 4f]);
			return await session.WaitForNpcAsync(npcId, token);
		}
		async Task AcceptAsync(int quest)
		{
			session.BeginStep($"s-accept-{quest}", $"accept-{quest}");
			await session.StartQuestAsync(await NpcObjectAsync(plans[quest].StartNpcs.First().Id), quest, token);
			Assert.Equal(QuestStatus.START, Server().GetQuestStateList().GetQuestState(quest).GetStatus());
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
			int npc = await NpcObjectAsync(plans[quest].EndNpcs.First().Id);
			await NaturalDialogProtocol.OpenAsync(session, npc, token);
			await session.WaitForPacketAsync(typeof(Aion.GameServer.Network.Aion.ServerPackets.SM_DIALOG_WINDOW), token);
			await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc, DialogAction.QUEST_SELECT, questId: quest), token);
			await session.WaitForPacketAsync(typeof(Aion.GameServer.Network.Aion.ServerPackets.SM_DIALOG_WINDOW), token);
			if (State(quest)?.Status == NaturalAltgardDecisionEngine.Start)
			{
				// ItemCollecting checks the items at the end NPC; an old-style MonsterHunt is still at START with its counter full,
				// and SELECT_QUEST_REWARD moves it to REWARD (Java MonsterHunt.onDialogEvent). Either then offers the reward.
				int check = plans[quest].Template == "item_collecting" ? DialogAction.CHECK_USER_HAS_QUEST_ITEM : DialogAction.SELECT_QUEST_REWARD;
				await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc, checked((ushort)check), questId: quest), token);
				await session.WaitForPacketAsync(typeof(Aion.GameServer.Network.Aion.ServerPackets.SM_DIALOG_WINDOW), token);
			}
			await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc, DialogAction.SELECTED_QUEST_NOREWARD, questId: quest), token);
			await session.SynchronizeAsync(token);
			await session.SendPacketAsync(session.Api.CloseDialog(npc), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(QuestStatus.COMPLETE, Server().GetQuestStateList().GetQuestState(quest).GetStatus());
			if (objectives[quest].ItemId is int item) Assert.Equal(0, ItemCount(item));
		}

		// Kaibech's campsite (Stop 5).
		await AcceptAsync(2233);
		await AcceptAsync(2235);
		await WorkAsync(2233, 8);
		await WorkAsync(2235, 8);
		await ClaimAsync(2233);
		await ClaimAsync(2235);
		await AcceptAsync(2234);
		await WorkAsync(2234, 10);
		await ClaimAsync(2234);

		// Vovetirn and the outlaws; Q2242 is taken and held for Gemyu in Gerger (AK-Q2).
		await AcceptAsync(2241);
		await WorkAsync(2241, 14);
		await ClaimAsync(2241);
		await AcceptAsync(2242);
		NaturalAltgardHeld gesture = leg.HeldList.Single(held => held.QuestId == 2242);
		Assert.Equal(gesture.EndNpcId, plans[2242].EndNpcs.Single().Id);
		int workItem = fixture.DataManager.StaticData.Quests.GetQuestById(2242).GetQuestWorkItems().GetQuestWorkItem().Single().GetItemId();
		Assert.Equal(1, ItemCount(workItem));
		log.Add($"Q2242 held at {State(2242)} with {workItem}");

		// Sumarhon's camp, worked out of Brodir.
		await AcceptAsync(24230);
		await AcceptAsync(24231);
		await WorkAsync(24230, 14);
		await WorkAsync(24231, 14);
		await ClaimAsync(24230);
		await ClaimAsync(24231);

		Console.WriteLine($"AK-06 {string.Join("; ", log)}");
		policy.AssertClean();
	}
}
