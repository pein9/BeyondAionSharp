using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// AB-04 (docs/natural-altgard-leveling.md): the two timed quests on the live SIM server, with a level 16 probe Cleric.
	/// <list type="number">
	/// <item>Q2288: a logout while its timer runs abandons it;</item>
	/// <item>Q2288: letting the 600 s run out abandons it;</item>
	/// <item>Q2288: three mosbear kills inside the timer, then Shania's reward (the Crystal Ring);</item>
	/// <item>Q2230: letting the 1,800 s run out, then the check takes the tusks (page 3057) and SETPRO1 starts a new chance;</item>
	/// <item>Q2230: ten tusks inside the new timer, then the reward.</item>
	/// </list>
	/// GM setup on the probe only: the level, and each target mosbear set to 1 HP with its aggressive neighbours despawned (the
	/// fights are AB-07's). Shania walks a route, so each talk starts beside where the server has her.
	/// </summary>
	[SkippableFact]
	public async Task AltgardTimedMosbearQuestsAbandonExpireRenewAndComplete()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, level = 16, money = 2288, wager = 2230, shania = 203621;
		using var policy = NewPolicy("AB04", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l4");
		NaturalAltgardTimer moneyTimer = leg.TimerList.Single(timer => timer.QuestId == money);
		NaturalAltgardTimer wagerTimer = leg.TimerList.Single(timer => timer.QuestId == wager);
		int tusk = Assert.Single(wagerTimer.LostItemIds);
		NaturalAltgardStep Step(string key) => leg.Steps.Single(step => step.Key == key);
		await using var session = new SimulationL0Session(fixture, policy, "b01", 146, "Asimtimed", Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player Server() => fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(Server(), PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		Server().GetCommonData().SetLevel(level);
		SkillLearnService.LearnNewSkills(Server(), 1, level);
		var instance = fixture.World.GetWorldMap(altgard).GetMainWorldMapInstance();
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(instance.GetInstanceId(), Race.ASMODIANS);
		(byte Status, int Var)? State(int quest) => NaturalAltgardQuestSteps.State(session.Api.World, quest);
		long ItemCount(int itemId) => session.Api.World.Inventory.Values.Where(owned => owned.ItemId == itemId).Sum(owned => owned.Count);
		NaturalAltgardHunt moneyHunt = leg.HuntList.Single(hunt => hunt.QuestId == money);
		int[] tuskSources = leg.CollectionList.Single(collection => collection.QuestId == wager).Items.Single().SourceNpcIds;
		var unseen = new HashSet<int>();
		var runtime = new NaturalJourneyRuntime(Aion.GameServer.TestKit.RealStaticData.RepoRoot(), "SIM-ab04", fixture.Seed, fixture.DataManager.StaticData,
			() => fixture.Clock.NowMillis, fixture.Epoch, () => geometry, _ => Task.FromResult(false), policy.AssertClean,
			() => policy.SnapshotProblems(), null!, new Aion.Bots.Dashboard.LiveBotDashboardState());

		async Task TeleportBesideAsync(float x, float y, float z)
		{
			BotPosition ground = geometry.GroundAround(altgard, new BotPosition(x, y, z, 0), [2f, 3f, 5f]).First();
			await TeleportForSetupAsync(session, Server(), altgard, ground.X, ground.Y, ground.Z, token);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
		}
		async Task<string> TalkToShaniaAsync(string key)
		{
			var npc = instance.GetNpcs().First(candidate => candidate.GetNpcId() == shania);
			await TeleportBesideAsync(npc.GetX(), npc.GetY(), npc.GetZ());
			session.BeginStep($"s-{key}", key);
			int mark = session.PacketHistory.Count;
			try { return await NaturalAltgardQuestSteps.TalkAsync(session, Step(key), await session.WaitForNpcAsync(shania, token), token); }
			catch (TimeoutException)
			{
				Console.WriteLine($"AB-04 {key} pages seen: " + string.Join(",", session.PacketHistory.Skip(mark)
					.Where(packet => packet.PacketType == typeof(SM_DIALOG_WINDOW))
					.Select(packet => $"{packet.Get<ushort>("dialogPageId")}/q{packet.Get<int>("questId")}")) +
					$"; quest {State(wager)}; timer task {Server().GetController().HasTask(Aion.GameServer.Model.TaskId.QUEST_TIMER)}; tusks {ItemCount(tusk)}");
				throw;
			}
		}
		int? TimerSeen(int quest) => session.Api.World.Quests.TryGetValue(quest, out BotQuestState? state) ? state.TimerSeconds : null;
		async Task WalkToAsync(BotPosition target, float within)
		{
			if (NaturalGuardedTalkPolicy.Distance(session.CurrentPosition, target) <= within) return;
			BotPosition goal = geometry.GroundAround(altgard, target, [within * 0.6f, within * 0.8f, 2f, 3f])
				.OrderBy(point => NaturalGuardedTalkPolicy.Distance(point, session.CurrentPosition)).FirstOrDefault();
			if (goal == default) goal = target;
			IReadOnlyList<BotPosition> route = geometry.FindJourneyPath(altgard, session.CurrentPosition, goal);
			if (route.Count == 0) route = geometry.FindInteractionPath(altgard, session.CurrentPosition, target);
			Assert.True(route.Count > 0, $"no route to {target}");
			float speed = session.Api.World.MovementSpeed ?? throw new InvalidOperationException("No movement speed.");
			await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing).CreateGroundPlan(route, session.CurrentPosition, speed), token);
			await session.SynchronizeAsync(token);
		}
		// One mosbear of the given kinds, set to 1 HP with its aggressive neighbours cleared, shot down with Smite. Returns its object.
		async Task<int> KillOneAsync(int[] kinds)
		{
			// In run-fast the world is shared: earlier tests leave the nearest mosbears dead or despawned, so a walk to a farther
			// one is a try too (AB-10). Each failed try says why.
			var why = new List<string>();
			for (int tries = 0; tries < 40; tries++)
			{
				BotKnownObject? target = session.Api.World.Objects.Values
					.Where(known => known.Kind == BotKnownObjectKind.Npc && known.TemplateId is int id && kinds.Contains(id) && !known.IsCorpse &&
						!unseen.Contains(known.ObjectId) && instance.GetNpcs().Any(npc => npc.GetObjectId() == known.ObjectId && !npc.IsDead()))
					.OrderBy(known => NaturalGuardedTalkPolicy.Distance(known.Position, session.CurrentPosition)).FirstOrDefault();
				if (target == null)
				{
					var next = instance.GetNpcs().Where(npc => kinds.Contains(npc.GetNpcId()) && !npc.IsDead() && !unseen.Contains(npc.GetObjectId()))
						.OrderBy(npc => MathF.Pow(npc.GetX() - session.CurrentPosition.X, 2) + MathF.Pow(npc.GetY() - session.CurrentPosition.Y, 2)).First();
					BotPosition near = geometry.GroundAround(altgard, new BotPosition(next.GetX(), next.GetY(), next.GetZ(), 0), [10f, 14f, 6f])
						.FirstOrDefault(point => geometry.HasLineOfSight(altgard, point, new BotPosition(next.GetX(), next.GetY(), next.GetZ() + 1, 0)));
					if (near == default) { unseen.Add(next.GetObjectId()); why.Add($"{next.GetNpcId()}/{next.GetObjectId()}: no ground in sight"); continue; }
					why.Add($"walk toward {next.GetNpcId()}/{next.GetObjectId()}");
					// Walk, never teleport: a setup teleport that respawns the player sends CM_LEVEL_READY, whose quest hooks
					// (Q1044, Q2042) end any running quest timer (the AB-Q6 defect).
					await WalkToAsync(near, 3f);
					continue;
				}
				var victim = instance.GetNpcs().Single(npc => npc.GetObjectId() == target.ObjectId);
				foreach (var npc in instance.GetNpcs().Where(npc => npc.GetObjectId() != target.ObjectId && !npc.IsDead() &&
					NaturalHostility.IsAggressive(npc.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
					MathF.Pow(npc.GetX() - victim.GetX(), 2) + MathF.Pow(npc.GetY() - victim.GetY(), 2) <= 25 * 25).ToArray())
					fixture.World.Despawn(npc);
				victim.GetLifeStats().SetCurrentHp(1);
				foreach (float reach in new[] { 18f, 8f, 3f })
				{
					BotPosition now = session.Api.World.Objects.TryGetValue(target.ObjectId, out BotKnownObject? seen) ? seen.Position : target.Position;
					await WalkToAsync(now, reach);
					if (await NaturalAirCombat.ShootDownAsync(session, target.ObjectId, 0,
						(origin, skill, skillLevel, aim) => runtime.CreateSpellCast(session.Api.World, origin, skill, skillLevel, aim), token, maximumCasts: 10))
						return target.ObjectId;
				}
				unseen.Add(target.ObjectId);
				why.Add($"{target.TemplateId}/{target.ObjectId}: not shot down (dead {Server().IsDead()}, HP {session.Api.World.CurrentHp}, " +
					$"MP {session.Api.World.CurrentMp}, {NaturalGuardedTalkPolicy.Distance(target.Position, session.CurrentPosition):F0} m)");
			}
			throw new InvalidDataException($"No mosbear could be shot down: {string.Join("; ", why)}.");
		}

		// 1. Q2288: the timer starts with SETPRO1; a logout abandons the quest.
		Console.WriteLine($"AB-04 {await TalkToShaniaAsync("q2288-offer-shania")}");
		Console.WriteLine($"AB-04 {await TalkToShaniaAsync(moneyTimer.StartStep)}");
		Assert.Equal(((byte)3, moneyHunt.FromVar), State(money));
		Console.WriteLine($"AB-04 Q2288 timer seen by the client: {TimerSeen(money)} s");
		Assert.Equal(moneyTimer.Seconds, TimerSeen(money));
		await session.QuitAsync(token);
		await session.WaitForReentryAsync(token);
		await session.ReloginExistingCharacterAsync(token);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Assert.Null(State(money));
		Assert.Null(Server().GetQuestStateList().GetQuestState(money) is { } afterLogout && afterLogout.GetStatus() == QuestStatus.START ? afterLogout : null);
		Console.WriteLine("AB-04 Q2288 abandoned by the logout");

		// 2. Q2288 again: the 600 s run out and the server abandons it.
		Console.WriteLine($"AB-04 {await TalkToShaniaAsync("q2288-offer-shania")}");
		Console.WriteLine($"AB-04 {await TalkToShaniaAsync(moneyTimer.StartStep)}");
		long started = fixture.Clock.NowMillis;
		await session.AdvanceAsync(TimeSpan.FromSeconds(moneyTimer.Seconds + 5), token);
		await session.SynchronizeAsync(token);
		Assert.Null(State(money));
		Console.WriteLine($"AB-04 Q2288 abandoned at its timer's end, {(fixture.Clock.NowMillis - started) / 1000} game s after SETPRO1");

		// 3. Q2288 a third time: three kills inside the timer, then the reward.
		Console.WriteLine($"AB-04 {await TalkToShaniaAsync("q2288-offer-shania")}");
		Console.WriteLine($"AB-04 {await TalkToShaniaAsync(moneyTimer.StartStep)}");
		started = fixture.Clock.NowMillis;
		while (State(money) is (3, int var) && var < moneyHunt.ToVar)
		{
			await KillOneAsync(moneyHunt.NpcIds);
			await session.SynchronizeAsync(token);
			Console.WriteLine($"AB-04 Q2288 kill: var {State(money)?.Var}");
		}
		Assert.Equal(((byte)3, moneyHunt.ToVar), State(money));
		Console.WriteLine($"AB-04 {await TalkToShaniaAsync("q2288-v4-shania")}; {(fixture.Clock.NowMillis - started) / 1000} game s after SETPRO1");
		Assert.Contains(money, session.Api.World.CompletedQuestIds);
		NaturalAltgardRewardChoice ring = leg.RewardChoiceList.Single(choice => choice.QuestId == money);
		Assert.Contains(session.Api.World.Inventory.Values, owned => owned.ItemId == ring.ItemId);

		// 4. Q2230: accepting starts 1,800 s; let it run out with a few tusks, then the check takes them and a new chance starts.
		Console.WriteLine($"AB-04 {await TalkToShaniaAsync(wagerTimer.StartStep)}");
		Assert.Equal(wagerTimer.Seconds, TimerSeen(wager));
		while (ItemCount(tusk) < 2)
		{
			int killed = await KillOneAsync(tuskSources);
			await WalkToAsync(session.Api.World.Objects.TryGetValue(killed, out BotKnownObject? corpse) ? corpse.Position : session.CurrentPosition, 2f);
			await NaturalAltgardQuestSteps.LootItemAsync(session, killed, tusk, token);
		}
		long tusksBefore = ItemCount(tusk);
		// Wait out the timer in the village, not on the grounds: there the mosbears killed above respawn beside the idle probe and
		// kill it (AB-10's run-fast). Walk, never teleport: a respawning teleport ends the timer (AB-Q6).
		var shaniaBeforeWait = instance.GetNpcs().First(candidate => candidate.GetNpcId() == shania);
		await WalkToAsync(new BotPosition(shaniaBeforeWait.GetX(), shaniaBeforeWait.GetY(), shaniaBeforeWait.GetZ(), 0), 5f);
		await session.AdvanceAsync(TimeSpan.FromSeconds(wagerTimer.Seconds + 5), token);
		await session.SynchronizeAsync(token);
		Assert.False(Server().IsDead(), "The probe died while Q2230's timer ran out.");
		Assert.Equal(((byte)3, 0), State(wager));
		var shaniaNpc = instance.GetNpcs().First(candidate => candidate.GetNpcId() == shania);
		await TeleportBesideAsync(shaniaNpc.GetX(), shaniaNpc.GetY(), shaniaNpc.GetZ());
		session.BeginStep("s-q2230-expired-check", "q2230-expired-check-and-new-chance");
		int shaniaObject = await session.WaitForNpcAsync(shania, token);
		await NaturalDialogProtocol.OpenAsync(session, shaniaObject, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == shaniaObject);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(shaniaObject, DialogAction.QUEST_SELECT, questId: wager), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == shaniaObject &&
			packet.Get<ushort>("dialogPageId") == 2375);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(shaniaObject, DialogAction.CHECK_USER_HAS_QUEST_ITEM, questId: wager), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == shaniaObject &&
			packet.Get<ushort>("dialogPageId") == wagerTimer.ExpiredPage);
		await session.SynchronizeAsync(token);
		Assert.Equal(0, ItemCount(tusk));
		ushort newChance = checked((ushort)NaturalAscensionContract.DialogActionId(wagerTimer.NewChanceAction!));
		int timerPackets = session.PacketHistory.Count;
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(shaniaObject, newChance, questId: wager), token);
		await session.SynchronizeAsync(token);
		await session.SendPacketAsync(session.Api.CloseDialog(shaniaObject), token);
		Assert.True(Server().GetController().HasTask(Aion.GameServer.Model.TaskId.QUEST_TIMER), "SETPRO1 started no new timer.");
		Console.WriteLine($"AB-04 Q2230 expired with {tusksBefore} tusks: the check took them (page {wagerTimer.ExpiredPage}); SETPRO1 started a new timer " +
			$"(client timer {TimerSeen(wager)} s, {session.PacketHistory.Skip(timerPackets).Count(packet => packet.PacketType == typeof(SM_QUEST_ACTION))} quest packets)");

		// 5. Q2230: ten tusks inside the new timer, then the reward.
		Assert.False(Server().IsDead(), "The probe died before Q2230's second timer.");
		started = fixture.Clock.NowMillis;
		int kills = 0;
		while (ItemCount(tusk) < 10)
		{
			Assert.True(++kills <= 30, $"{ItemCount(tusk)} tusks after {kills - 1} kills");
			int killed = await KillOneAsync(tuskSources);
			await WalkToAsync(session.Api.World.Objects.TryGetValue(killed, out BotKnownObject? corpse) ? corpse.Position : session.CurrentPosition, 2f);
			await NaturalAltgardQuestSteps.LootItemAsync(session, killed, tusk, token);
		}
		Console.WriteLine($"AB-04 Q2230: 10 tusks from {kills} kills in {(fixture.Clock.NowMillis - started) / 1000} game s");
		// Walk to Shania with the timer running (a teleport could end it, AB-Q6).
		var shaniaNow = instance.GetNpcs().First(candidate => candidate.GetNpcId() == shania);
		await WalkToAsync(new BotPosition(shaniaNow.GetX(), shaniaNow.GetY(), shaniaNow.GetZ(), 0), 3f);
		Assert.True(Server().GetController().HasTask(Aion.GameServer.Model.TaskId.QUEST_TIMER), "The new-chance timer did not survive the hunt.");
		session.BeginStep("s-q2230-v0-shania", "q2230-v0-shania");
		int shaniaSeen = await session.WaitForNpcAsync(shania, token);
		for (int attempt = 1; ; attempt++)
		{
			try { Console.WriteLine($"AB-04 {await NaturalAltgardQuestSteps.TalkAsync(session, Step("q2230-v0-shania"), shaniaSeen, token)}"); break; }
			catch (NaturalDialogTooFarException) when (attempt < 6)
			{
				// Shania walks her route: follow her to where the server has her now (the client's report lags a walker).
				await session.SynchronizeAsync(token);
				var walking = instance.GetNpcs().FirstOrDefault(candidate => candidate.GetObjectId() == shaniaSeen);
				await WalkToAsync(walking != null ? new BotPosition(walking.GetX(), walking.GetY(), walking.GetZ(), 0) : session.Api.World.Objects[shaniaSeen].Position, 2f);
			}
		}
		Assert.Contains(wager, session.Api.World.CompletedQuestIds);
		Assert.Equal(0, ItemCount(tusk));
		Assert.False(Server().IsDead());
		policy.AssertClean();
	}
}
