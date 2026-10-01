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
	/// AK-05: MuMu Village and Manumumu, the Leg 5 template quests (Java MonsterHunt and ItemCollecting), from their compiled plans:
	/// <list type="bullet">
	/// <item>one batch for Q24232 (nine looklooks or lookouts) and Q2238 (five MuMu Belts from the same kinds);</item>
	/// <item>Q2236's five hairpins from herb gatherers and workers;</item>
	/// <item>Q2237's three fertilizer sacks (quest_use_item objects: a use, then a loot);</item>
	/// <item>Chieftain Manumumu killed, which fills Q24233's counter (an old-style MonsterHunt stays at START until its end NPC);
	/// it is held for Suthran.</item>
	/// </list>
	/// The four other quests are claimed at their end NPCs. GM setup on the probe only: its class, level and skills, Q24112
	/// complete (the prerequisite), setup teleports, and each target set to 1 HP with its aggressive neighbours cleared (the
	/// fights are AK-07's).
	/// </summary>
	[SkippableFact]
	public async Task MumuVillageQuestsAndManumumuPlayThroughTheirPlans()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, anmurnerk = 832822, brodir = 832821, gefion = 203616;
		using var policy = NewPolicy("AK05", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		IReadOnlyDictionary<int, QuestRunPlan> plans = NaturalAltgardContract.LoadPlans("l5");
		IReadOnlyDictionary<int, NaturalTemplateObjective> objectives = NaturalTemplateObjective.From(plans);
		await using var session = new SimulationL0Session(fixture, policy, "b01", 75, "Asimmumu", Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player Server() => fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(Server(), PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		Server().GetCommonData().SetLevel(19);
		SkillLearnService.LearnNewSkills(Server(), 1, 19);
		Assert.True(Server().GetQuestStateList().AddQuest(24112, new QuestState(24112, QuestStatus.COMPLETE)));
		var instance = fixture.World.GetWorldMap(altgard).GetMainWorldMapInstance();
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(instance.GetInstanceId(), Race.ASMODIANS);
		var runtime = new NaturalJourneyRuntime(Aion.GameServer.TestKit.RealStaticData.RepoRoot(), "SIM-ak05", fixture.Seed, fixture.DataManager.StaticData,
			() => fixture.Clock.NowMillis, fixture.Epoch, () => geometry, _ => Task.FromResult(false), policy.AssertClean,
			() => policy.SnapshotProblems(), null!, new Aion.Bots.Dashboard.LiveBotDashboardState());
		long ItemCount(int itemId) => session.Api.World.Inventory.Values.Where(owned => owned.ItemId == itemId).Sum(owned => owned.Count);
		(byte Status, int Var)? State(int id) => NaturalAltgardQuestSteps.State(session.Api.World, id);
		var unusable = new HashSet<int>();
		var log = new List<string>();

		async Task TeleportNearAsync(BotPosition at, float[] radii, bool sighted = false)
		{
			BotPosition ground = geometry.GroundAround(altgard, at, radii)
				.First(point => !sighted || geometry.HasLineOfSight(altgard, point with { Z = point.Z + 1.6f }, at with { Z = at.Z + 1 }));
			await TeleportForSetupAsync(session, Server(), altgard, ground.X, ground.Y, ground.Z, token);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
		}
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
				MathF.Pow(template.GetX() - session.CurrentPosition.X, 2) + MathF.Pow(template.GetY() - session.CurrentPosition.Y, 2)).First();
			var spawned = Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(new SpawnTemplate(new SpawnGroup(instance.GetMapId(),
				group.GetNpcId(), 0, null), spot.GetX(), spot.GetY(), spot.GetZ(), spot.GetHeading(), 0, null, 0), instance.GetInstanceId());
			Assert.True(spawned is Aion.GameServer.Model.GameObjects.Npc, $"No {group.GetNpcId()} could be spawned.");
			return (Aion.GameServer.Model.GameObjects.Npc)spawned!;
		}
		Aion.GameServer.Model.GameObjects.Npc Nearest(int[] kinds) => Candidates(kinds).FirstOrDefault() ?? RespawnShipped(kinds);
		// Aggressive neighbours are cleared, except Manumumu: he is unique (one spawn, 1,800 s respawn) and stands in the village.
		void ClearAround(Aion.GameServer.Model.GameObjects.Npc target)
		{
			foreach (var npc in instance.GetNpcs().Where(npc => npc.GetObjectId() != target.GetObjectId() && !npc.IsDead() &&
				!Kinds(24233).Contains(npc.GetNpcId()) &&
				NaturalHostility.IsAggressive(npc.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
				MathF.Pow(npc.GetX() - target.GetX(), 2) + MathF.Pow(npc.GetY() - target.GetY(), 2) <= 25 * 25).ToArray())
				fixture.World.Despawn(npc);
		}
		// One monster of the given kinds at 1 HP, shot down with Smite from a sighted stand-off; a kill counts only when the server
		// has it dead (the shoot-down's own evidence is the client's). Then its corpse is looted for the item, when one is wanted.
		async Task<int> KillAndLootAsync(int[] kinds, int quest, int? item)
		{
			for (int tries = 0; tries < 16; tries++)
			{
				var next = Nearest(kinds);
				unusable.Add(next.GetObjectId());
				var at = new BotPosition(next.GetX(), next.GetY(), next.GetZ(), 0);
				if (!geometry.GroundAround(altgard, at, [12f, 14f, 10f, 16f])
					.Any(point => geometry.HasLineOfSight(altgard, point with { Z = point.Z + 1.6f }, at with { Z = at.Z + 1 })))
					continue;
				ClearAround(next);
				await TeleportNearAsync(at, [12f, 14f, 10f, 16f], sighted: true);
				next.GetLifeStats().SetCurrentHp(1);
				await session.SynchronizeAsync(token);
				if (!session.Api.World.Objects.ContainsKey(next.GetObjectId())) continue;
				await NaturalAirCombat.ShootDownAsync(session, next.GetObjectId(), quest,
					(origin, skill, skillLevel, aim) => runtime.CreateSpellCast(session.Api.World, origin, skill, skillLevel, aim), token, maximumCasts: 6);
				await session.SynchronizeAsync(token);
				if (!next.IsDead()) continue;
				if (item is int wanted)
				{
					await TeleportNearAsync(new BotPosition(next.GetX(), next.GetY(), next.GetZ(), 0), [2f, 3f, 4f]);
					bool looted = await NaturalAltgardQuestSteps.LootItemAsync(session, next.GetObjectId(), wanted, token);
					log.Add($"{next.GetNpcId()} {(looted ? "dropped" : "did not drop")} {wanted}");
				}
				else log.Add($"{next.GetNpcId()} killed");
				return next.GetNpcId();
			}
			throw new InvalidDataException($"No {string.Join("/", kinds)} could be shot down.");
		}
		// The kill targets or drop sources of a quest's objective step (not its report NPC).
		int[] Kinds(int quest) => plans[quest].Steps.Where(step => step.Kind is "kill" or "collect").SelectMany(step =>
			step.Sources.Select(source => source.NpcId ?? 0).Concat(step.Npcs.Select(npc => npc.Id)))
			.Where(id => id > 0).Distinct().ToArray();
		async Task<int> NpcObjectAsync(int npcId)
		{
			var npc = instance.GetNpcs().First(candidate => candidate.GetNpcId() == npcId);
			await TeleportNearAsync(new BotPosition(npc.GetX(), npc.GetY(), npc.GetZ(), 0), [2f, 3f, 4f]);
			return await session.WaitForNpcAsync(npcId, token);
		}
		async Task ClaimAsync(int npcId, int quest)
		{
			session.BeginStep($"s-claim-{quest}", $"claim-{quest}-at-{npcId}");
			int npc = await NpcObjectAsync(npcId);
			await NaturalDialogProtocol.OpenAsync(session, npc, token);
			await session.WaitForPacketAsync(typeof(Aion.GameServer.Network.Aion.ServerPackets.SM_DIALOG_WINDOW), token);
			await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc, DialogAction.QUEST_SELECT, questId: quest), token);
			await session.WaitForPacketAsync(typeof(Aion.GameServer.Network.Aion.ServerPackets.SM_DIALOG_WINDOW), token);
			if (State(quest)?.Status == NaturalAltgardDecisionEngine.Start)
			{
				// ItemCollecting checks the items at the end NPC; an old-style MonsterHunt (no reward flag) is still at START with its
				// counter full, and SELECT_QUEST_REWARD moves it to REWARD (Java MonsterHunt.onDialogEvent). Either then offers the reward.
				int check = plans[quest].Template == "item_collecting" ? DialogAction.CHECK_USER_HAS_QUEST_ITEM : DialogAction.SELECT_QUEST_REWARD;
				await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc, checked((ushort)check), questId: quest), token);
				await session.WaitForPacketAsync(typeof(Aion.GameServer.Network.Aion.ServerPackets.SM_DIALOG_WINDOW), token);
			}
			await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc, DialogAction.SELECTED_QUEST_NOREWARD, questId: quest), token);
			await session.SynchronizeAsync(token);
			await session.SendPacketAsync(session.Api.CloseDialog(npc), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(QuestStatus.COMPLETE, Server().GetQuestStateList().GetQuestState(quest).GetStatus());
		}

		session.BeginStep("s01", "accept-at-basfelt");
		foreach (int quest in new[] { 24232, 2236, 2237 })
			await session.StartQuestAsync(await NpcObjectAsync(anmurnerk), quest, token);
		foreach (int quest in new[] { 2238, 24233 })
			await session.StartQuestAsync(await NpcObjectAsync(brodir), quest, token);
		foreach (int quest in new[] { 24232, 2236, 2237, 2238, 24233 })
			Assert.Equal(QuestStatus.START, Server().GetQuestStateList().GetQuestState(quest).GetStatus());

		// Q24232 and Q2238 together: every looklook or lookout counts for one and drops a belt for the other.
		session.BeginStep("s02", "looklook-batch");
		NaturalTemplateObjective hunt = objectives[24232], belts = objectives[2238];
		Assert.Equal(Kinds(24232).Order(), Kinds(2238).Where(id => id < 700000).Order());
		int batchKills = 0;
		bool HuntDone() => hunt.IsDone(session.Api.World.Quests.GetValueOrDefault(24232), new Dictionary<int, long>());
		while (!HuntDone() || ItemCount(belts.ItemId!.Value) < belts.ItemCount)
		{
			Assert.True(++batchKills <= 16, $"Q24232 at {State(24232)}, {ItemCount(belts.ItemId!.Value)} belts after {batchKills - 1} kills");
			await KillAndLootAsync(Kinds(24232), HuntDone() ? 0 : 24232, ItemCount(belts.ItemId!.Value) < belts.ItemCount ? belts.ItemId : null);
		}
		log.Add($"batch: {batchKills} kills, Q24232 at {State(24232)}, {ItemCount(belts.ItemId!.Value)} belts");

		session.BeginStep("s03", "hairpins");
		NaturalTemplateObjective hairpins = objectives[2236];
		int hairpinKills = 0;
		while (ItemCount(hairpins.ItemId!.Value) < hairpins.ItemCount)
		{
			Assert.True(++hairpinKills <= 10, $"{ItemCount(hairpins.ItemId!.Value)} hairpins after {hairpinKills - 1} kills");
			await KillAndLootAsync(Kinds(2236), 0, hairpins.ItemId);
		}
		log.Add($"hairpins: {hairpinKills} kills");

		session.BeginStep("s04", "fertilizer-sacks");
		NaturalTemplateObjective sacks = objectives[2237];
		int uses = 0;
		while (ItemCount(sacks.ItemId!.Value) < sacks.ItemCount)
		{
			Assert.True(++uses <= 8, $"{ItemCount(sacks.ItemId!.Value)} sacks after {uses - 1} uses");
			var sack = Nearest(Kinds(2237));
			unusable.Add(sack.GetObjectId());
			ClearAround(sack);
			await TeleportNearAsync(new BotPosition(sack.GetX(), sack.GetY(), sack.GetZ(), 0), [2f, 3f, 4f]);
			if (!session.Api.World.Objects.ContainsKey(sack.GetObjectId())) continue;
			bool looted = await NaturalAltgardQuestSteps.UseObjectAsync(session, sack.GetObjectId(), sacks.ItemId, token);
			log.Add($"sack {sack.GetObjectId()}: looted {looted}");
		}

		session.BeginStep("s05", "manumumu");
		int manumumu = Kinds(24233).Single();
		await KillAndLootAsync([manumumu], 24233, null);
		// Q24233 has no reward flag and is not data-driven: Java MonsterHunt.onKillEvent fills the counter and leaves it at START.
		log.Add($"Q24233 at {State(24233)}");
		Assert.Equal(QuestStatus.START, Server().GetQuestStateList().GetQuestState(24233).GetStatus());
		Assert.Equal(1, Server().GetQuestStateList().GetQuestState(24233).GetQuestVarById(0));
		Assert.True(objectives[24233].IsDone(session.Api.World.Quests.GetValueOrDefault(24233), new Dictionary<int, long>()));

		await ClaimAsync(anmurnerk, 24232);
		await ClaimAsync(anmurnerk, 2236);
		await ClaimAsync(anmurnerk, 2237);
		await ClaimAsync(gefion, 2238);
		foreach (int item in new[] { belts.ItemId!.Value, hairpins.ItemId!.Value, sacks.ItemId!.Value })
			Assert.Equal(0, ItemCount(item));
		// Q24233 is held, its kill made, for Suthran in the fortress (AK-Q2).
		Assert.Equal(QuestStatus.START, Server().GetQuestStateList().GetQuestState(24233).GetStatus());
		Console.WriteLine($"AK-05 {string.Join("; ", log)}");
		policy.AssertClean();
	}
}
