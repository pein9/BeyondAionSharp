using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;

namespace Aion.Simulation.Tests;

/// <summary>
/// AX-01 (docs/natural-abyss-entry.md): what the server does on the Morheim arrival and Abyss-entry route, measured with
/// controlled level-25 Clerics on free accounts. Setup (level, quest state, positions, supplied items) is the probe's own;
/// every measured action is an ordinary client action. No natural character is involved.
/// </summary>
public sealed partial class SimulationFastScenarioTests
{
	private const int AxPandaemonium = 120010000, AxMorheim = 220020000, AxAltgard = 220030000, AxArena = 320090000;

	/// <summary>Flight time and speed at level 25, the flight-speed scroll, and what passing ring 1 does on Q2042's course.</summary>
	[SkippableFact]
	public async Task AbyssEntryFlightMeasuresTimeSpeedScrollAndTheFirstRing()
	{
		await RunCapitalProbeAsync("AX01A", 231, "Asimaxflight", async (probe, session, token) =>
		{
			Player server = probe.Server;
			AxLevel25(server);
			AxSetQuest(server, 2947, QuestStatus.COMPLETE, 0);
			AxSetQuest(server, 2042, QuestStatus.START, 1);
			Assert.Equal(0, ItemService.AddItem(server, 164000079, 1, allowInventoryOverflow: true));
			await probe.SetupNearAsync(AxMorheim, 204319);
			await session.SynchronizeAsync(token);
			int maxFp = session.Api.World.MaxFlightTime;
			Assert.Equal(server.GetLifeStats().GetMaxFp(), maxFp);

			// Base flight: take off, climb 20 m, come back down, land. Flight time is read on the server.
			BotPosition ground = session.CurrentPosition;
			BotPosition above = ground with { Z = ground.Z + 20 };
			float baseSpeed = await NaturalFlightProtocol.TakeOffAsync(session, token);
			long started = fixture.Clock.NowMillis;
			await NaturalFlightProtocol.FlyAsync(session, AxMorheim, ground, [above, ground with { Z = ground.Z + 0.5f }], baseSpeed, token);
			await session.SynchronizeAsync(token);
			int fpSpent = maxFp - server.GetLifeStats().GetCurrentFp();
			double flownSeconds = (fixture.Clock.NowMillis - started) / 1000.0;
			await NaturalFlightProtocol.LandAsync(session, token);
			await session.SynchronizeAsync(token);
			Assert.False(server.IsFlying());

			// The Greater Raging Wind Scroll (item level 30, no class restrict) is used on the ground at level 25.
			BotInventoryItem scroll = session.Api.World.Inventory.Values.Single(item => item.ItemId == 164000079);
			await session.SendPacketAsync(session.Api.UseItem(scroll.ObjectId, fixture.DataManager.StaticData.ItemDataDh.GetItemTemplate(164000079)), token);
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
			await session.SynchronizeAsync(token);
			bool scrollWorks = server.GetEffectController().HasAbnormalEffect(9961);
			bool scrollSpent = session.Api.World.Inventory.Values.All(item => item.ItemId != 164000079);

			// Restore flight time and wait out the take-off reuse before the course.
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(NaturalFlightPolicy.RestoreMillis(server.GetLifeStats().GetCurrentFp(), maxFp, maxFp) + 11_000), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(maxFp, server.GetLifeStats().GetCurrentFp());

			// Yornduf starts the course: movie 89, then SETPRO2 gives var 2 and the 70 s timer.
			int yornduf = server.GetWorldMapInstance().GetNpcs(204319).First().GetObjectId();
			int history = session.PacketHistory.Count;
			Console.WriteLine("AX-01 " + await NaturalAltgardQuestSteps.TalkAsync(session, AxStep("q2042-yornduf-start", 2042, 1, "START", 204319,
				["QUEST_SELECT", "SELECT2_1_1", "SETPRO2"], [1352, 1354, 0], AxMorheim, next: 2, movie: 89), yornduf, token));
			DecodedBotServerPacket? timer = session.PacketHistory.Skip(history).LastOrDefault(packet =>
				AxIsTimer(packet, 2042));
			Assert.NotNull(timer);
			Assert.True(server.GetController().HasTask(TaskId.QUEST_TIMER));

			// Fly through ring 1 and on for a few metres. Wings of Aether (skill 265) is the server's answer.
			BotPosition start = session.CurrentPosition;
			BotPosition ring1 = new(310.83936f, 2296.9807f, 470.03555f, 0);
			BotPosition beyond = new(ring1.X + (ring1.X - start.X) * 0.25f, ring1.Y + (ring1.Y - start.Y) * 0.25f, ring1.Z + (ring1.Z - start.Z) * 0.25f, 0);
			float scrollSpeed = await NaturalFlightProtocol.TakeOffAsync(session, token);
			int fpBeforeRing = server.GetLifeStats().GetCurrentFp();
			await NaturalFlightProtocol.FlyAsync(session, AxMorheim, start, [ring1], scrollSpeed, token);
			await session.SynchronizeAsync(token);
			int fpAtRing = server.GetLifeStats().GetCurrentFp();
			await NaturalFlightProtocol.FlyAsync(session, AxMorheim, ring1, [beyond], scrollSpeed, token);
			await session.SynchronizeAsync(token);
			int varAfter = server.GetQuestStateList().GetQuestState(2042).GetQuestVarById(0);
			bool boosted = server.GetEffectController().HasAbnormalEffect(265);
			float boostedSpeed = server.GetGameStats().GetMovementSpeedFloat();
			int fpAfterRing = server.GetLifeStats().GetCurrentFp();
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(6_500), token);
			await session.SynchronizeAsync(token);
			bool boostEnded = !server.GetEffectController().HasAbnormalEffect(265);
			float afterBoostSpeed = server.GetGameStats().GetMovementSpeedFloat();

			// Come down and land; then let the timer run out, which fails the course (var 9).
			await NaturalFlightProtocol.FlyAsync(session, AxMorheim, beyond, [start with { Z = start.Z + 0.5f }], afterBoostSpeed, token);
			await NaturalFlightProtocol.LandAsync(session, token);
			await session.AdvanceAsync(TimeSpan.FromSeconds(75), token);
			await session.SynchronizeAsync(token);
			int varExpired = server.GetQuestStateList().GetQuestState(2042).GetQuestVarById(0);

			Console.WriteLine($"AX-01 flight: level {server.GetLevel()}, max FP {maxFp}; base speed {baseSpeed} m/s; {fpSpent} FP in {flownSeconds:F1} s of flying; " +
				$"scroll 164000079 usable at 25: {scrollWorks} (spent {scrollSpent}), speed with it {scrollSpeed} m/s; " +
				$"Q2042 timer {timer!.Get<int>("timer")} s; ring 1: var 2 -> {varAfter}, Wings of Aether {boosted}, speed {boostedSpeed} m/s for 6 s (ended {boostEnded}, then {afterBoostSpeed}), " +
				$"FP {fpBeforeRing} at take-off, {fpAtRing} at the ring, {fpAfterRing} just past it; timer expiry: var {varExpired}");
			Assert.Equal(60, maxFp);
			Assert.True(scrollWorks && scrollSpent);
			Assert.Equal(70, timer.Get<int>("timer"));
			Assert.Equal(3, varAfter);
			Assert.True(boosted && boostEnded);
			// Winged Blessing I (skill 362, passive) gives the Cleric +33% of the base 9 m/s; the scroll adds 30%; Wings of Aether
			// asks for +200%, which StatCapUtil caps at 16 m/s for a player.
			Assert.Equal(11.97f, baseSpeed, 2);
			Assert.Equal(14.67f, scrollSpeed, 2);
			Assert.Equal(16f, boostedSpeed, 2);
			Assert.Equal(scrollSpeed, afterBoostSpeed, 2);
			Assert.Equal(9, varExpired);
		});
	}

	/// <summary>Garm's arena: the 240 s timer survives the world entry, and a second entry inside ten minutes returns to the same instance.</summary>
	[SkippableFact]
	public async Task AbyssEntryArenaKeepsItsTimerAndReusesItsInstanceForTenMinutes()
	{
		await RunCapitalProbeAsync("AX01B", 232, "Asimaxarena", async (probe, session, token) =>
		{
			Player server = probe.Server;
			AxLevel25(server);
			AxSetQuest(server, 2946, QuestStatus.COMPLETE, 0);
			AxSetQuest(server, 2947, QuestStatus.START, 4);
			await session.SynchronizeAsync(token);
			NaturalAltgardStep first = AxStep("q2947-garm-start", 2947, 4, "START", 204089, ["QUEST_SELECT", "SETPRO3"], [1693, 0], AxPandaemonium, next: 5);
			NaturalAltgardStep again = AxStep("q2947-garm-again", 2947, 6, "START", 204089, ["USE_OBJECT", "SETPRO3"], [1779, 0], AxPandaemonium, next: 5);

			async Task<(int Instance, int Alive)> EnterAsync(NaturalAltgardStep garm)
			{
				await probe.SetupNearAsync(AxPandaemonium, 204089);
				int npc = await probe.WalkNpcAsync(204089);
				Console.WriteLine("AX-01 " + await NaturalAltgardQuestSteps.TalkAsync(session, garm, npc, token));
				int entrance = await probe.WalkNpcAsync(700368);
				Assert.Equal(5, server.GetQuestStateList().GetQuestState(2947).GetQuestVarById(0));
				int history = session.PacketHistory.Count;
				await NaturalDialogProtocol.OpenAsync(session, entrance, token);
				DecodedBotServerPacket use = await session.WaitForPacketAsync(typeof(SM_USE_OBJECT), token,
					packet => packet.Get<int>("targetObjectId") == entrance && packet.Get<byte>("actionType") == 1);
				session.Api.World.BeginWorldReload();
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(use.Get<int>("durationMs") + 1), token);
				await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token, packet => packet.Get<int>("worldId") == AxArena);
				await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
				session.AcceptTeleportPosition();
				await session.SynchronizeAsync(token);
				Assert.Equal(AxArena, server.GetWorldId());
				DecodedBotServerPacket? timer = session.PacketHistory.Skip(history).LastOrDefault(packet =>
					AxIsTimer(packet, 2947));
				Assert.NotNull(timer);
				Assert.Equal(240, timer!.Get<int>("timer"));
				// Hazard 8: Q1044's and Q2042's enter-world hooks run on this entry too. D33 keeps them off another quest's timer.
				Assert.True(server.GetController().HasTask(TaskId.QUEST_TIMER), "the arena timer did not survive the world entry");
				await NaturalMovieGate.FinishAsync(session, token);
				await session.SynchronizeAsync(token);
				Assert.True(server.GetController().HasTask(TaskId.QUEST_TIMER));
				int alive = server.GetWorldMapInstance().GetNpcs().Count(npcIn => !npcIn.IsDead() && npcIn.GetNpcId() is 213583 or 213584);
				return (server.GetInstanceId(), alive);
			}

			async Task LeaveAsync()
			{
				Npc exit = server.GetWorldMapInstance().GetNpcs(730067).First();
				Assert.True(NaturalFlightPolicy.Distance(session.CurrentPosition, new(exit.GetX(), exit.GetY(), exit.GetZ(), 0)) <= 6, "the exit is out of reach");
				await NaturalDialogProtocol.OpenAsync(session, exit.GetObjectId(), token);
				DecodedBotServerPacket use = await session.WaitForPacketAsync(typeof(SM_USE_OBJECT), token,
					packet => packet.Get<int>("targetObjectId") == exit.GetObjectId() && packet.Get<byte>("actionType") == 1);
				session.Api.World.BeginWorldReload();
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(use.Get<int>("durationMs") + 1), token);
				await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token, packet => packet.Get<int>("worldId") == AxPandaemonium);
				await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
				session.AcceptTeleportPosition();
				await session.SynchronizeAsync(token);
				// Leaving at var 5 is a failed attempt: the enter-world hook sets var 6 and ends the timer.
				Assert.Equal(6, server.GetQuestStateList().GetQuestState(2947).GetQuestVarById(0));
				Assert.False(server.GetController().HasTask(TaskId.QUEST_TIMER));
			}

			(int firstInstance, int firstAlive) = await EnterAsync(first);
			var spirits = server.GetWorldMapInstance().GetNpcs().Where(npc => npc.GetNpcId() is 213583 or 213584).ToArray();
			foreach (var kind in spirits.GroupBy(npc => npc.GetNpcId()))
			{
				Npc one = kind.First();
				Console.WriteLine($"AX-01 spirit {kind.Key}: {kind.Count()} spawned, level {one.GetLevel()}, HP {one.GetLifeStats().GetMaxHp()}, " +
					$"attack {one.GetGameStats().GetMainHandPAttack().GetCurrent()}, magic attack {one.GetGameStats().GetMainHandMAttack().GetCurrent()}, " +
					$"aggro {one.GetObjectTemplate().GetAggroRange()} m, attack range {one.GetObjectTemplate().GetAttackRange()} m, tribe {one.GetTribe()}");
			}
			// The kill is credited from beside the spirit: XP and the quest counter need the player in range.
			BotPosition entry = session.CurrentPosition;
			async Task MoveForSetupAsync(float x, float y, float z)
			{
				session.Api.World.BeginWorldReload();
				await TeleportForSetupAsync(session, server, AxArena, x, y, z, token, targetInstanceId: server.GetInstanceId());
				session.AcceptTeleportPosition();
				await session.SynchronizeAsync(token);
			}
			Npc victim = spirits.Where(npc => npc.GetNpcId() == 213584).OrderBy(npc => npc.GetX()).First();
			await MoveForSetupAsync(victim.GetX() + 2, victim.GetY(), victim.GetZ() + 0.2f);
			long xpBefore = server.GetCommonData().GetExp();
			victim.GetController().OnAttack(server, null!, SmAttackStatus.TYPE.REGULAR, victim.GetLifeStats().GetMaxHp(), true,
				SmAttackStatus.LOG.REGULAR, null, Aion.GameServer.SkillEngine.Model.HopType.DAMAGE);
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
			await session.SynchronizeAsync(token);
			Assert.True(victim.IsDead());
			long spiritXp = server.GetCommonData().GetExp() - xpBefore;
			int counted = server.GetQuestStateList().GetQuestState(2947).GetQuestVarById(4);
			await MoveForSetupAsync(entry.X, entry.Y, entry.Z);
			Assert.Equal(5, server.GetQuestStateList().GetQuestState(2947).GetQuestVarById(0));
			await LeaveAsync();

			(int secondInstance, int secondAlive) = await EnterAsync(again);
			await LeaveAsync();

			// Solo instances are destroyed 600 s after the last player left, checked once a minute.
			await session.AdvanceAsync(TimeSpan.FromSeconds(661), token);
			await session.SynchronizeAsync(token);
			(int thirdInstance, int thirdAlive) = await EnterAsync(again);
			await LeaveAsync();

			Console.WriteLine($"AX-01 arena: first entry instance {firstInstance} with {firstAlive} spirits; one kill pays {spiritXp} XP and counts {counted}; " +
				$"re-entry at once: instance {secondInstance} with {secondAlive} spirits; after 661 s: instance {thirdInstance} with {thirdAlive} spirits");
			Assert.Equal(11, firstAlive);
			Assert.Equal(1, counted);
			Assert.Equal(firstInstance, secondInstance);
			Assert.Equal(10, secondAlive);
			Assert.NotEqual(firstInstance, thirdInstance);
			Assert.Equal(11, thirdAlive);
		});
	}

	/// <summary>Ukin's teleport starts Q24020 on arrival; the Morheim bind, Aegir's reward and the two return fares are as the data says.</summary>
	[SkippableFact]
	public async Task AbyssEntryMorheimArrivalStartsAegirsOrdersAndChargesTheShippedFares()
	{
		await RunCapitalProbeAsync("AX01C", 233, "Asimaxmorheim", async (probe, session, token) =>
		{
			Player server = probe.Server;
			AxLevel25(server);
			server.GetInventory().IncreaseKinah(40_000);
			await session.SynchronizeAsync(token);
			var services = new NaturalServiceSteps(session);

			async Task<NaturalServiceOutcome> TeleportAsync(int fromMap, int npcId, int location, long price, int toMap)
			{
				await probe.SetupNearAsync(fromMap, npcId);
				Npc npc = server.GetWorldMapInstance().GetNpcs(npcId).First();
				return await services.TeleportAsync(npc.GetObjectId(), new(npc.GetX(), npc.GetY(), npc.GetZ(), 0), 6, location, price, toMap, token);
			}

			Assert.Null(server.GetQuestStateList().GetQuestState(24020));
			NaturalServiceOutcome ukin = await TeleportAsync(AxAltgard, 203581, 10, 1700, AxMorheim);
			Assert.True(ukin.IsDone, ukin.Reason);
			BotPosition landing = session.CurrentPosition;
			Assert.True(NaturalAltgardQuestSteps.State(session.Api.World, 24020) is (3, 0), "Q24020 did not start on entering Morheim");

			await probe.SetupNearAsync(AxMorheim, 700231);
			Npc obelisk = server.GetWorldMapInstance().GetNpcs(700231).First();
			NaturalServiceOutcome bind = await services.BindAsync(obelisk.GetObjectId(), new(obelisk.GetX(), obelisk.GetY(), obelisk.GetZ(), 0), AxMorheim, 2690, 6, token);
			Assert.True(bind.IsDone, bind.Reason);

			long xp = server.GetCommonData().GetExp();
			HashSet<int> owned = session.Api.World.Inventory.Values.Select(item => item.ItemId).ToHashSet();
			await probe.SetupNearAsync(AxMorheim, 204301);
			int aegir = server.GetWorldMapInstance().GetNpcs(204301).First().GetObjectId();
			Console.WriteLine("AX-01 " + await NaturalAltgardQuestSteps.TalkAsync(session, AxStep("q24020-aegir", 24020, 0, "START", 204301,
				["QUEST_SELECT", "SELECT_QUEST_REWARD", "SELECTED_QUEST_REWARD4"], [1011, 5], AxMorheim), aegir, token));
			Assert.Contains(24020, session.Api.World.CompletedQuestIds);
			long paid = server.GetCommonData().GetExp() - xp;
			int[] rewards = session.Api.World.Inventory.Values.Select(item => item.ItemId).Where(id => !owned.Contains(id)).Distinct().ToArray();

			NaturalServiceOutcome orhe = await TeleportAsync(AxMorheim, 204399, 7, 1500, AxPandaemonium);
			Assert.True(orhe.IsDone, orhe.Reason);
			NaturalServiceOutcome doman = await TeleportAsync(AxPandaemonium, 204191, 10, 1500, AxMorheim);
			Assert.True(doman.IsDone, doman.Reason);

			Console.WriteLine($"AX-01 Morheim: {ukin.Reason} Landing {landing}. Q24020 started on arrival. {bind.Reason} " +
				$"Aegir pays {paid} XP and choice 4 gives [{string.Join(", ", rewards)}]. {orhe.Reason} {doman.Reason}");
			Assert.Equal(293759, paid);
			Assert.Equal([110551147], rewards);
		});
	}

	/// <summary>The four Abyss-entry missions pay what the handler's reward group selects, Q2947's group 1 included.</summary>
	[SkippableFact]
	public async Task AbyssEntryMissionsPayTheirSelectedRewardGroups()
	{
		await RunCapitalProbeAsync("AX01D", 234, "Asimaxreward", async (probe, session, token) =>
		{
			Player server = probe.Server;
			AxLevel25(server);
			await session.SynchronizeAsync(token);
			(int Quest, int Var, int Npc, int Map, int Page, int RewardPage, string Claim)[] turnIns =
			[
				(2945, 1, 204075, AxPandaemonium, 10002, 5, "SELECTED_QUEST_NOREWARD"),
				(2946, 3, 204053, AxPandaemonium, 10002, 5, "SELECTED_QUEST_NOREWARD"),
				(2947, 7, 204301, AxMorheim, 3739, 6, "SELECTED_QUEST_REWARD2"),
				(2042, 8, 204301, AxMorheim, 10002, 5, "SELECTED_QUEST_NOREWARD"),
			];
			var lines = new List<string>();
			foreach (var turnIn in turnIns)
			{
				AxSetQuest(server, turnIn.Quest, QuestStatus.REWARD, turnIn.Var);
				await session.SynchronizeAsync(token);
				long xp = server.GetCommonData().GetExp(), kinah = session.Api.World.Kinah;
				int ap = server.GetAbyssRank().GetAp();
				var before = session.Api.World.Inventory.Values.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
				await probe.SetupNearAsync(turnIn.Map, turnIn.Npc);
				int npc = server.GetWorldMapInstance().GetNpcs(turnIn.Npc).First().GetObjectId();
				await NaturalAltgardQuestSteps.TalkAsync(session, AxStep($"q{turnIn.Quest}-reward", turnIn.Quest, null, "REWARD", turnIn.Npc,
					["USE_OBJECT", "SELECT_QUEST_REWARD", turnIn.Claim], [turnIn.Page, turnIn.RewardPage], turnIn.Map), npc, token);
				Assert.Contains(turnIn.Quest, session.Api.World.CompletedQuestIds);
				var gained = session.Api.World.Inventory.Values.Where(item => item.ItemId != BotWorldModel.KinahItemId).GroupBy(item => item.ItemId)
					.Select(group => (Id: group.Key, Count: group.Sum(item => item.Count) - before.GetValueOrDefault(group.Key))).Where(item => item.Count > 0)
					.OrderBy(item => item.Id).Select(item => $"{item.Id} x{item.Count}");
				lines.Add($"Q{turnIn.Quest}: {server.GetCommonData().GetExp() - xp} XP, {session.Api.World.Kinah - kinah} Kinah, " +
					$"{server.GetAbyssRank().GetAp() - ap} AP, items [{string.Join(", ", gained)}]");
			}
			foreach (string line in lines) Console.WriteLine("AX-01 pays " + line);
			Assert.Equal("Q2945: 20110 XP, 0 Kinah, 250 AP, items [188051192 x2]", lines[0]);
			Assert.Equal("Q2946: 20110 XP, 0 Kinah, 250 AP, items [188050878 x1]", lines[1]);
			Assert.Equal("Q2947: 403012 XP, 4000 Kinah, 0 AP, items [101501224 x1, 167000465 x1]", lines[2]);
			Assert.Equal("Q2042: 301641 XP, 0 Kinah, 0 AP, items [162001057 x5, 164000079 x10, 188050873 x1]", lines[3]);
		});
	}

	private static bool AxIsTimer(DecodedBotServerPacket packet, int questId) =>
		packet.PacketType == typeof(SM_QUEST_ACTION) && packet.Fields.TryGetValue("action", out object? action) && action is byte and 4 &&
		packet.Get<int>("questId") == questId && packet.Get<int>("timer") > 0;

	private static void AxLevel25(Player server)
	{
		server.GetCommonData().SetLevel(25);
		SkillLearnService.LearnNewSkills(server, 11, 25);
	}

	/// <summary>Probe setup only: put a quest in the given state on the server and tell the client, as a login would.</summary>
	private static void AxSetQuest(Player server, int questId, QuestStatus status, int var)
	{
		QuestState? state = server.GetQuestStateList().GetQuestState(questId);
		bool added = state == null;
		if (state == null)
		{
			state = new QuestState(questId, status);
			Assert.True(server.GetQuestStateList().AddQuest(questId, state));
		}
		state.SetStatus(status);
		state.SetQuestVar(var);
		if (status == QuestStatus.COMPLETE)
			PacketSendUtility.SendPacket(server, new SM_QUEST_COMPLETED_LIST(0, [state]));
		else
			PacketSendUtility.SendPacket(server, new SM_QUEST_ACTION(added ? SM_QUEST_ACTION.ActionType.ADD : SM_QUEST_ACTION.ActionType.UPDATE, state));
	}

	private static NaturalAltgardStep AxStep(string key, int quest, int? var, string status, int npc, string[] actions, int[] pages, int map,
		int? next = null, int? movie = null) =>
		new(key, quest, var, status, npc, [], 5, actions, pages, movie, null, "abyss-entry", false, null, MapId: map, NextVar: next);
}
