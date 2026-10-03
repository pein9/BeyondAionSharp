using System.Text.Json;
using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.Reflexes;
using Aion.Bots.Scenarios;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>HM-03: first-clear protocols on unused access-0 account 224; setup never completes a quest.</summary>
	[SkippableFact]
	public async Task HaramelFirstClearQuestsProveMovieCollectionsCountersGatesAndRewards()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		using var policy = NewPolicy("HM03", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		string root = RealStaticData.RepoRoot();
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "hm03-quests";
		string path = Path.Combine(root, "run", $"{run}.hm03.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(path, run, "b01", "sim-player-224", virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 224, "Asimharquest", Race.ASMODIANS,
			trace, path, new BotMoviePolicy(BotMovieMode.Watch));
		var dashboard = new LiveBotDashboardState();
		await using var host = new LiveBotDashboardHost(run, ["HM-03"], dashboard,
			int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880"));
		session.Dashboard = dashboard;
		if (host.Enabled) Console.WriteLine($"HM-03 dashboard: {host.Url}");
		var travel = new HaramelTravelProbe(this, fixture, session, token);
		await travel.InitializeAsync();
		// Watch policy also holds the new character's introductory movie; finish it before any route.
		await NaturalMovieGate.FinishAsync(session, token);
		await session.SynchronizeAsync(token);
		await travel.SetupFortressAsync();
		var probe = new HaramelQuestProbe(this, fixture, session, token);
		int morn = await probe.NpcAsync(203560);
		probe.Server.GetCommonData().SetLevel(15);
		await probe.RefuseStartAsync(28500, morn, "level-15");
		probe.Server.GetCommonData().SetLevel(24);
		await probe.PlayTalkAsync("q28500-accept", morn);
		Assert.Equal(0, probe.Count(182212012)); // Java declares this work item but does not grant it.
		await travel.FlyHubAsync(toHeart: true);
		await travel.BindAsync(700067, 813);
		await probe.PlayTalkAsync("q28500-gulkalla", await probe.NpcAsync(203649));
		await probe.PlayObjectAsync("q28500-piece", await probe.NpcAsync(730306));
		await probe.PlayObjectAsync("q28500-pile", await probe.NpcAsync(730307));
		Assert.Equal((byte)3, session.Api.World.Quests[28500].Status);
		Assert.Equal(3, probe.Server.GetQuestStateList().GetQuestState(28500).GetQuestVarById(0));
		Assert.Equal(QuestStatus.START, probe.Server.GetQuestStateList().GetQuestState(28500).GetStatus());
		Assert.Equal(217, session.Api.Reflexes.PendingMovie!.MovieId);
		session.BeginStep("q28500-movie", "real-movie-end-required");
		await NaturalHaramelQuestSteps.FinishLeadInMovieAsync(session, token);
		await probe.RefuseStartAsync(28504, await probe.NpcAsync(804605), "before-Q28507");
		await probe.RefuseStartAsync(28505, await probe.NpcAsync(804605), "before-Q28507");
		await probe.RefuseStartAsync(28510, await probe.NpcAsync(804605), "before-Q28507");
		await probe.NpcAsync(730319);
		await travel.PortalAsync(730319, 300200000);
		int firstCopy = probe.Server.GetInstanceId();
		int firstEntries = travel.EntriesUsed;
		int moor = await probe.NpcAsync(799522);
		await probe.PlayTalkAsync("q28500-reward", moor);
		Assert.Equal(1, probe.Count(112501641));
		await probe.RefuseStartAsync(28511, moor, "before-Q28507");
		await probe.AcceptAsync(28501);
		await probe.AcceptAsync(28508);
		await probe.RefuseStartAsync(28506, await probe.NpcAsync(799523), "before-Q28501");
		await probe.RefuseStartAsync(28507, await probe.NpcAsync(799524), "before-Q28506");
		await probe.CollectAsync(28501, 700833, 182212013, 5);
		Assert.Equal(0, probe.Count(182212014));
		await probe.RefuseRewardAsync(28501);
		Assert.Equal(5, probe.Count(182212013));
		await probe.CollectAsync(28501, 700951, 182212014, 5);
		await probe.ClaimAsync(28501);
		for (int i = 0; i < 2; i++) await probe.KillAsync(216898, 28508);
		Assert.Equal(2, probe.Counter(28508, 0));
		Assert.Equal(0, probe.Counter(28508, 1));
		Assert.Equal(0, probe.Counter(28508, 2));
		await probe.RefuseRewardAsync(28508);
		for (int i = 0; i < 3; i++) await probe.KillAsync(216901, 28508);
		Assert.Equal(2, probe.Counter(28508, 0));
		Assert.Equal(3, probe.Counter(28508, 1));
		Assert.Equal(0, probe.Counter(28508, 2));
		await probe.RefuseRewardAsync(28508);
		for (int i = 0; i < 3; i++) await probe.KillAsync(216902, 28508);
		Assert.Equal(new[] { 2, 3, 3 }, Enumerable.Range(0, 3).Select(v => probe.Counter(28508, v)).ToArray());
		await probe.ClaimAsync(28508);
		await probe.AcceptAsync(28503);
		await probe.AcceptAsync(28509);
		await probe.AcceptAsync(28506);
		await probe.RefuseStartAsync(28507, await probe.NpcAsync(799524), "Q28506-active");
		await probe.ClaimAsync(28506);
		await probe.AcceptAsync(28507);
		await probe.CollectAsync(28503, 700834, 182212016, 5);
		await probe.RefuseTowerChestAsync();
		await probe.KillAsync(217025, 28509, 185000103);
		Assert.Equal(1, probe.Count(185000103));
		await probe.RefuseTowerChestAsync();
		Assert.Equal(1, probe.Count(185000103)); // Missing oil must not consume the key.
		for (int i = 0; i < 3; i++) await probe.KillAsync(217108, 28509, 185000107);
		Assert.Equal(3, probe.Count(185000107));
		await probe.CollectAsync(28509, 700853, 182212020, 1);
		Assert.Equal(0, probe.Count(185000103));
		Assert.Equal(0, probe.Count(185000107));
		await probe.ClaimAsync(28509);
		int boss = await probe.NpcAsync(216922, combat: true);
		await travel.SpawnBossExitForTravelAsync(boss); // Labelled HP=1 protocol credit; HM-04 proves actual combat.
		Assert.Equal(1, probe.Counter(28507, 0));
		await probe.NpcAsync(700852);
		await travel.PortalAsync(700852, 220030000);
		await probe.ClaimAsync(28503);
		await probe.ClaimAsync(28507);
		Assert.Equal(1, probe.Count(123001440));
		foreach (int quest in new[] { 28504, 28505, 28510 }) await probe.AcceptAsync(quest);
		Assert.Equal(1, probe.Count(182212021));
		await probe.NpcAsync(730319);
		await travel.PortalAsync(730319, 300200000);
		Assert.Equal(firstCopy, probe.Server.GetInstanceId());
		Assert.Equal(firstEntries, travel.EntriesUsed);
		await probe.AcceptAsync(28511);
		int[] completed = [28500, 28501, 28503, 28506, 28507, 28508, 28509];
		foreach (int quest in completed) Assert.True(session.Api.World.CompletedQuestIds.Contains(quest));
		foreach (int item in new[] { 182212012, 182212013, 182212014, 182212016, 182212020 }) Assert.Equal(0, probe.Count(item));
		Assert.Equal(1, probe.Count(186000007));
		Assert.Single(session.Api.World.Inventory.Values, i => i.ItemId == 101501357 && i.Details.EquippedSlot == 3);
		Assert.DoesNotContain(session.Api.World.Skills.Keys, id => id == 11504);
		await session.QuitAsync(token);
		await session.WaitForReentryAsync(token);
		await session.ReloginAndVerifyPersistenceAsync(token);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		foreach (int quest in completed)
		{
			Assert.True(session.Api.World.CompletedQuestIds.Contains(quest));
			Assert.Equal(1, probe.Server.GetQuestStateList().GetQuestState(quest).GetCompleteCount());
		}
		Assert.Equal(1, probe.Count(112501641));
		Assert.Equal(1, probe.Count(123001440));
		Assert.Equal(1, probe.Count(186000007));
		Assert.Equal(1, probe.Count(182212021));
		Assert.Equal(firstEntries, travel.EntriesUsed);
		await File.WriteAllTextAsync(Path.ChangeExtension(path, ".summary.json"), JsonSerializer.Serialize(new
		{
			account = 224, firstCopy, firstEntries, completed, probe.RefusedStarts, probe.RefusedRewards,
			probe.CollectionUses, probe.Kills, probe.SetupClears, probe.SetupRespawns, probe.TowerChestRefusals, probe.Log,
			movie = 217, rewards = new[] { 112501641, 123001440 }, bronze = probe.Count(186000007),
			postQuests = new[] { 28504, 28505, 28510, 28511 }, naturalCharacterChanged = false,
		}), token);
		Console.WriteLine($"HM-03 PASS: seven first completions, {probe.RefusedStarts} real gate refusals, {probe.RefusedRewards} incomplete reward refusals, selected chain/belt, cleanup and relog.");
		policy.AssertClean();
	}

	private sealed class HaramelQuestProbe(SimulationFastScenarioTests owner, SimulationWorldFixture fixture,
		SimulationL0Session session, CancellationToken token)
	{
		private readonly NaturalAltgardContract contract = NaturalAltgardContract.LoadLeg("l12");
		private readonly IReadOnlyDictionary<int, QuestRunPlan> plans = NaturalAltgardContract.LoadPlans("l12");
		public Aion.GameServer.Model.GameObjects.Players.Player Server => fixture.World.GetPlayer(session.CharacterId);
		public int RefusedStarts { get; private set; }
		public int RefusedRewards { get; private set; }
		public int CollectionUses { get; private set; }
		public int Kills { get; private set; }
		public int SetupClears { get; private set; }
		public int SetupRespawns { get; private set; }
		public int TowerChestRefusals { get; private set; }
		public List<string> Log { get; } = [];
		public long Count(int item) => session.Api.World.Inventory.Values.Where(i => i.ItemId == item).Sum(i => i.Count);
		public int Counter(int quest, int variable) => NaturalQuestProgress.KillCount(session.Api.World.Quests[quest], variable, 63);
		private BotNavigationGeometry Geometry() => BotNavigationGeometry.ForServerWorld(Server.GetInstanceId(), Race.ASMODIANS);
		private static BotPosition At(Npc npc) => new(npc.GetX(), npc.GetY(), npc.GetZ(), 0);

		private async Task SetupNearAsync(Npc npc, bool combat = false)
		{
			BotNavigationGeometry geo = Geometry();
			BotPosition target = At(npc);
			BotPosition ground = geo.GroundAround(Server.GetWorldId(), target, combat ? [7f, 6f, 8f] : [2f, 3f, 1f])
				.First(p => !combat || geo.HasLineOfSight(Server.GetWorldId(), p with { Z = p.Z + 1.6f }, target with { Z = target.Z + 1 }));
			Npc[] neighbours = Server.GetWorldMapInstance().GetNpcs().Where(n => n != npc && n.GetNpcId() != 216922 && !n.IsDead() &&
				NaturalHostility.IsAggressive(n.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
				MathF.Pow(n.GetX() - ground.X, 2) + MathF.Pow(n.GetY() - ground.Y, 2) <= 900).ToArray();
			foreach (Npc neighbour in neighbours) fixture.World.Despawn(neighbour);
			SetupClears += neighbours.Length;
			Console.WriteLine($"HM-03 labelled setup: teleport near {npc.GetNpcId()} in native copy {Server.GetInstanceId()}, clear {neighbours.Length} aggressive neighbours; no quest-state writes.");
			session.Api.World.BeginWorldReload();
			await owner.TeleportForSetupAsync(session, Server, Server.GetWorldId(), ground.X, ground.Y, ground.Z, token, Server.GetInstanceId());
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
		}

		public async Task<int> NpcAsync(int npcId, bool combat = false)
		{
			Npc npc = Server.GetWorldMapInstance().GetNpcs(npcId).First(n => !n.IsDead());
			await SetupNearAsync(npc, combat);
			return await session.WaitForNpcAsync(npcId, token);
		}
		private async Task SelectAsync(int npc, int quest, int action, bool expectRejection = false)
		{
			BotClientPacket choice = expectRejection
				? session.Api.SelectDialogExpectRejection(npc, checked((ushort)action), questId: quest)
				: session.Api.SelectDialog(npc, checked((ushort)action), questId: quest);
			await NaturalDialogProtocol.SelectAsync(session, choice, token);
			await session.SynchronizeAsync(token);
		}
		public async Task RefuseStartAsync(int quest, int npc, string reason)
		{
			session.BeginStep($"gate-{quest}-{reason}", "real-ineligible-accept-refusal");
			Assert.False(session.Api.World.Quests.GetValueOrDefault(quest)?.Status is 3 or 4);
			await NaturalDialogProtocol.OpenAsync(session, npc, token);
			await session.SynchronizeAsync(token);
			await SelectAsync(npc, quest, DialogAction.QUEST_SELECT);
			await SelectAsync(npc, quest, DialogAction.QUEST_ACCEPT);
			Assert.False(Server.GetQuestStateList().GetQuestState(quest)?.GetStatus() is QuestStatus.START or QuestStatus.REWARD);
			Assert.False(session.Api.World.Quests.GetValueOrDefault(quest)?.Status is 3 or 4);
			await session.SendPacketAsync(session.Api.CloseDialog(npc), token);
			RefusedStarts++;
			Log.Add($"Q{quest} refused {reason}");
		}
		public async Task PlayTalkAsync(string key, int npc)
		{
			session.BeginStep(key, key);
			Log.Add(await NaturalAltgardQuestSteps.TalkAsync(session, contract.Steps.Single(s => s.Key == key), npc, token));
		}
		public async Task PlayObjectAsync(string key, int npc)
		{
			session.BeginStep(key, "timed-object-dialog");
			await NaturalHaramelQuestSteps.UseLeadInObjectAsync(session, contract.Steps.Single(s => s.Key == key), npc, token);
			Log.Add(key);
		}
		public async Task AcceptAsync(int quest)
		{
			session.BeginStep($"accept-{quest}", "eligible-first-accept");
			int npc = await NpcAsync(contract.Quests.Single(q => q.Id == quest).StartNpcId
				?? throw new InvalidDataException($"Q{quest} has no start NPC."));
			await session.StartQuestAsync(npc, quest, token);
			await session.SynchronizeAsync(token);
			Assert.Equal(QuestStatus.START, Server.GetQuestStateList().GetQuestState(quest).GetStatus());
			Log.Add($"Q{quest} accepted");
		}
		public async Task RefuseRewardAsync(int quest)
		{
			session.BeginStep($"incomplete-{quest}", "real-incomplete-objective-refusal");
			int npc = await NpcAsync(plans[quest].EndNpcs.First().Id);
			await NaturalDialogProtocol.OpenAsync(session, npc, token);
			await SelectAsync(npc, quest, DialogAction.QUEST_SELECT);
			bool collecting = plans[quest].Template == "item_collecting";
			await SelectAsync(npc, quest, collecting ? DialogAction.CHECK_USER_HAS_QUEST_ITEM : DialogAction.SELECT_QUEST_REWARD,
				expectRejection: !collecting);
			if (!collecting) Assert.Equal(new PendingQuestDialogAction(npc, DialogAction.SELECT_QUEST_REWARD, quest),
				session.Api.QuestDialogEchoes.ConsumeExpectedRejection());
			Assert.Equal(QuestStatus.START, Server.GetQuestStateList().GetQuestState(quest).GetStatus());
			Assert.False(session.Api.World.CompletedQuestIds.Contains(quest));
			await session.SendPacketAsync(session.Api.CloseDialog(npc), token);
			RefusedRewards++;
			Log.Add($"Q{quest} refused incomplete reward");
		}
		public async Task ClaimAsync(int quest)
		{
			session.BeginStep($"claim-{quest}", "actual-first-reward");
			int npc = await NpcAsync(plans[quest].EndNpcs.First().Id);
			await NaturalDialogProtocol.OpenAsync(session, npc, token);
			await SelectAsync(npc, quest, DialogAction.QUEST_SELECT);
			await SelectAsync(npc, quest, plans[quest].Template == "item_collecting" ? DialogAction.CHECK_USER_HAS_QUEST_ITEM : DialogAction.SELECT_QUEST_REWARD);
			Assert.Equal(QuestStatus.REWARD, Server.GetQuestStateList().GetQuestState(quest).GetStatus());
			NaturalAltgardRewardChoice? choice = contract.RewardChoiceList.FirstOrDefault(c => c.QuestId == quest);
			await SelectAsync(npc, quest, choice == null ? DialogAction.SELECTED_QUEST_NOREWARD : NaturalAscensionContract.DialogActionId(choice.Action));
			await session.SendPacketAsync(session.Api.CloseDialog(npc), token);
			Assert.Equal(QuestStatus.COMPLETE, Server.GetQuestStateList().GetQuestState(quest).GetStatus());
			foreach (NaturalTemplateCollect collect in NaturalTemplateObjective.From(plans)[quest].Collections ?? []) Assert.Equal(0, Count(collect.ItemId));
			Log.Add($"Q{quest} complete");
		}
		public async Task CollectAsync(int quest, int npcId, int itemId, int required)
		{
			for (int attempt = 0; Count(itemId) < required && attempt < required + 4; attempt++)
			{
				session.BeginStep($"collect-{quest}-{itemId}-{attempt}", "real-use-bar-and-loot");
				Npc npc = Server.GetWorldMapInstance().GetNpcs(npcId).First(n => !n.IsDead());
				await SetupNearAsync(npc);
				bool obtained = await NaturalAltgardQuestSteps.UseObjectAsync(session, npc.GetObjectId(), itemId, token);
				await session.SynchronizeAsync(token);
				CollectionUses++;
				Log.Add($"Q{quest} {npcId}: {(obtained ? "looted" : "missed")} {itemId}, held {Count(itemId)}");
			}
			Assert.Equal(required, Count(itemId));
		}
		public async Task RefuseTowerChestAsync()
		{
			session.BeginStep("tower-chest-incomplete-keys", "real-key-and-oil-refusal");
			int box = await NpcAsync(700853);
			int start = session.PacketHistory.Count;
			await NaturalAltgardQuestSteps.UseObjectAsync(session, box, null, token);
			Assert.Contains(session.PacketHistory.Skip(start), p => p.PacketType == typeof(SM_SYSTEM_MESSAGE) && p.Get<int>("msgId") == 1111301);
			Assert.False(Server.GetWorldMapInstance().GetNpcs(700853).Single().IsDead());
			Assert.Equal(0, Count(182212020));
			TowerChestRefusals++;
			Log.Add("Tower chest refused incomplete Rusty Key/Lubricating Oil set without consumption");
		}
		public async Task KillAsync(int npcId, int quest, int? itemId = null)
		{
			session.BeginStep($"kill-{quest}-{npcId}-{Kills}", "labelled-HP1-protocol-credit");
			Npc? npc = Server.GetWorldMapInstance().GetNpcs(npcId).FirstOrDefault(n => !n.IsDead());
			if (npc == null)
			{
				SpawnGroup group = fixture.DataManager.StaticData.SpawnsDh.GetSpawnsByWorldId(300200000).First(g => g.GetNpcId() == npcId);
				var spot = group.GetSpawnTemplates().First();
				npc = (Npc)Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(new SpawnTemplate(new SpawnGroup(300200000, npcId, 0, null),
					spot.GetX(), spot.GetY(), spot.GetZ(), spot.GetHeading(), 0, null, 0), Server.GetInstanceId());
				SetupRespawns++;
				Console.WriteLine($"HM-03 labelled setup: respawn {npcId} at its shipped spot after neighbour clearing.");
			}
			await SetupNearAsync(npc, combat: true);
			npc.GetLifeStats().SetCurrentHp(1);
			var runtime = new NaturalJourneyRuntime(RealStaticData.RepoRoot(), "SIM-hm03", fixture.Seed, fixture.DataManager.StaticData,
				() => fixture.Clock.NowMillis, fixture.Epoch, Geometry, _ => Task.FromResult(false), () => { }, () => Array.Empty<object>(), null!, new());
			await NaturalAirCombat.ShootDownAsync(session, npc.GetObjectId(), quest,
				(origin, skill, level, aim) => runtime.CreateSpellCast(session.Api.World, origin, skill, level, aim), token, maximumCasts: 6);
			await session.SynchronizeAsync(token);
			Assert.True(npc.IsDead());
			Kills++;
			if (itemId is int item)
			{
				await SetupNearAsync(npc);
				bool looted = await NaturalAltgardQuestSteps.LootItemAsync(session, npc.GetObjectId(), item, token);
				await session.SynchronizeAsync(token);
				Log.Add($"{npcId}: {(looted ? "looted" : "missed")} actual key supply {item}, held {Count(item)}");
			}
			Log.Add($"Q{quest} real death {npcId}, counters " + string.Join("/", Enumerable.Range(0, 3).Select(v => Counter(quest, v))));
		}
	}
}
