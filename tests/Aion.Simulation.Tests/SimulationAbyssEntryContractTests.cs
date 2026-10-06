using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.Reflexes;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Instance;
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

	/// <summary>Garm's arena: Garm's SETPRO3 sends the player in (D35), the 240 s timer survives the world entry, and every attempt
	/// starts in a new instance with all twelve spirits (D36) and no kill counted (D34). Leaving at var 5 fails the attempt and destroys
	/// its instance at once. An instance left behind before any attempt (the entrance 700368 asks for no quest) is destroyed by
	/// Garm's SETPRO3. One visited at var 6, after a failure, is destroyed on leaving it (D36 resets a failed player's arena on the
	/// way out). Before D34 the entrance returned to the same instance, its dead spirits still dead, for ten minutes.</summary>
	[SkippableFact]
	public async Task AbyssEntryArenaKeepsItsTimerAndStartsEveryAttemptInANewInstance()
	{
		await RunCapitalProbeAsync("AX01B", 232, "Asimaxarena", async (probe, session, token) =>
		{
			Player server = probe.Server;
			await AxArenaStartAsync(probe, session, token);

			// Before Garm, at var 4: the entrance lets the player in. No timer starts, and a kill counts for nothing.
			(int earlyInstance, int earlyAlive) = await AxWalkIntoArenaAsync(probe, session, token);
			await AxKillSpiritAsync(session, server, token);
			await AxUseArenaPortalAsync(session, server, 730067, AxPandaemonium, token);
			int earlyVar = server.GetQuestStateList().GetQuestState(2947).GetQuestVars().GetQuestVars();
			// Outside an attempt nothing is reset: the instance stays registered, as in Java, until Garm starts the attempt.
			bool earlyKept = InstanceService.InstanceExists(AxArena, earlyInstance);

			(int firstInstance, int firstAlive) = await AxEnterArenaAsync(probe, session, again: false, token);
			bool earlyGone = !InstanceService.InstanceExists(AxArena, earlyInstance);
			var spirits = server.GetWorldMapInstance().GetNpcs().Where(npc => npc.GetNpcId() is 213583 or 213584).ToArray();
			foreach (var kind in spirits.GroupBy(npc => npc.GetNpcId()))
			{
				Npc one = kind.First();
				Console.WriteLine($"AX-01 spirit {kind.Key}: {kind.Count()} spawned, level {one.GetLevel()}, HP {one.GetLifeStats().GetMaxHp()}, " +
					$"attack {one.GetGameStats().GetMainHandPAttack().GetCurrent()}, magic attack {one.GetGameStats().GetMainHandMAttack().GetCurrent()}, " +
					$"aggro {one.GetObjectTemplate().GetAggroRange()} m, attack range {one.GetObjectTemplate().GetAttackRange()} m, tribe {one.GetTribe()}");
			}
			long xpBefore = server.GetCommonData().GetExp();
			await AxKillSpiritAsync(session, server, token);
			long spiritXp = server.GetCommonData().GetExp() - xpBefore;
			int counted = AxArenaKills(server);
			Assert.Equal(5, AxArenaVar(server));
			await AxLeaveArenaAsync(session, server, token);
			// D34: the failed attempt's instance is destroyed as soon as the player is out of it.
			bool firstGone = !InstanceService.InstanceExists(AxArena, firstInstance);
			Assert.Null(InstanceService.GetRegisteredInstance(AxArena, server.GetObjectId()));

			// At var 6, back in without Garm: a new instance, no timer, and again a kill that counts for nothing. Leaving destroys it (D36).
			(int idleInstance, int idleAlive) = await AxWalkIntoArenaAsync(probe, session, token);
			await AxKillSpiritAsync(session, server, token);
			await AxUseArenaPortalAsync(session, server, 730067, AxPandaemonium, token);
			int idleVar = server.GetQuestStateList().GetQuestState(2947).GetQuestVars().GetQuestVars();
			bool idleKept = InstanceService.InstanceExists(AxArena, idleInstance);

			(int secondInstance, int secondAlive) = await AxEnterArenaAsync(probe, session, again: true, token);
			bool idleGone = !InstanceService.InstanceExists(AxArena, idleInstance);
			int secondCounted = AxArenaKills(server);
			await AxLeaveArenaAsync(session, server, token);

			Console.WriteLine($"AX-01 arena: entered at var 4 before Garm: instance {earlyInstance} with {earlyAlive} spirits, one killed, var {earlyVar}, kept on leaving {earlyKept}; " +
				$"Garm's SETPRO3 destroyed it {earlyGone}: instance {firstInstance} with {firstAlive} spirits; one kill pays {spiritXp} XP and counts {counted}; " +
				$"left at var 5: var 6, instance {firstInstance} destroyed {firstGone}; entered at var 6 without Garm: instance {idleInstance} with {idleAlive} spirits, " +
				$"one killed, var {idleVar}, kept on leaving {idleKept}, gone before Garm's SETPRO3 {idleGone}: instance {secondInstance} with {secondAlive} spirits, " +
				$"{secondCounted} counted");
			Assert.Equal((12, 4, true, true), (earlyAlive, earlyVar, earlyKept, earlyGone));
			Assert.Equal(12, firstAlive);
			Assert.Equal(1, counted);
			Assert.True(firstGone, "the failed attempt's instance still exists");
			Assert.Equal((12, 6, false, true), (idleAlive, idleVar, idleKept, idleGone));
			Assert.Equal(4, new[] { earlyInstance, firstInstance, idleInstance, secondInstance }.Distinct().Count());
			Assert.Equal(12, secondAlive);
			Assert.Equal(0, secondCounted);
		});
	}

	/// <summary>Probe setup for Garm's arena: a level-25 Cleric with Q2946 complete and Q2947 at Kvasir's var 4.</summary>
	private static async Task AxArenaStartAsync(CapitalProbe probe, SimulationL0Session session, CancellationToken token)
	{
		AxLevel25(probe.Server);
		AxSetQuest(probe.Server, 2946, QuestStatus.COMPLETE, 0);
		AxSetQuest(probe.Server, 2947, QuestStatus.START, 4);
		await session.SynchronizeAsync(token);
	}

	private static int AxArenaVar(Player server) => server.GetQuestStateList().GetQuestState(2947).GetQuestVarById(0);

	private static int AxArenaKills(Player server) => server.GetQuestStateList().GetQuestState(2947).GetQuestVarById(4);

	/// <summary>Where Garm sends the player: the arena's own portal location (D35).</summary>
	private static readonly NaturalAscensionTeleport AxArenaEntry = new(AxArena, [276, 293, 163]);

	/// <summary>Garm's SETPRO3 (from var 4, or again from var 6). D35: he sends the player straight in, with no walk to the entrance.
	/// The 240 s timer has to start on entry and again at movie 167's end (hazard 5). Returns the instance entered and its living
	/// spirits.</summary>
	private static async Task<(int Instance, int Alive)> AxEnterArenaAsync(CapitalProbe probe, SimulationL0Session session, bool again, CancellationToken token)
	{
		Player server = probe.Server;
		NaturalAltgardStep garm = (again
			? AxStep("q2947-garm-again", 2947, 6, "START", 204089, ["USE_OBJECT", "SETPRO3"], [1779, 0], AxPandaemonium, next: 5)
			: AxStep("q2947-garm-start", 2947, 4, "START", 204089, ["QUEST_SELECT", "SETPRO3"], [1693, 0], AxPandaemonium, next: 5))
			with { Teleport = AxArenaEntry };
		await probe.SetupNearAsync(AxPandaemonium, 204089);
		int npc = await probe.WalkNpcAsync(204089);
		int history = session.PacketHistory.Count;
		Console.WriteLine("AX-01 " + await NaturalAltgardQuestSteps.TalkAsync(session, garm, npc, token));
		Assert.Equal((5, AxArena), (AxArenaVar(server), server.GetWorldId()));
		float miss = MathF.Sqrt(MathF.Pow(server.GetX() - 276, 2) + MathF.Pow(server.GetY() - 293, 2) + MathF.Pow(server.GetZ() - 163, 2));
		Assert.True(miss < 1.5f, $"Garm sent the player {miss:F1} m from the arena's portal location");
		Assert.Equal(server.GetInstanceId(), InstanceService.GetRegisteredInstance(AxArena, server.GetObjectId())?.GetInstanceId());
		DecodedBotServerPacket? timer = session.PacketHistory.Skip(history).LastOrDefault(packet => AxIsTimer(packet, 2947));
		Assert.NotNull(timer);
		Assert.Equal(240, timer!.Get<int>("timer"));
		// Hazard 8: Q1044's and Q2042's enter-world hooks run on this entry too. D33 keeps them off another quest's timer.
		Assert.True(server.GetController().HasTask(TaskId.QUEST_TIMER), "the arena timer did not survive the world entry");
		await NaturalMovieGate.FinishAsync(session, token);
		await session.SynchronizeAsync(token);
		Assert.True(server.GetController().HasTask(TaskId.QUEST_TIMER));
		Assert.Equal(2, session.PacketHistory.Skip(history).Count(packet => AxIsTimer(packet, 2947)));
		return (server.GetInstanceId(), AxLivingSpirits(server));
	}

	/// <summary>The walk from Garm to the entrance 700368 and its portal, which asks for no quest and which no attempt uses since
	/// D35. Outside an attempt no timer starts.</summary>
	private static async Task<(int Instance, int Alive)> AxWalkIntoArenaAsync(CapitalProbe probe, SimulationL0Session session, CancellationToken token)
	{
		Player server = probe.Server;
		await probe.SetupNearAsync(AxPandaemonium, 204089);
		await probe.WalkNpcAsync(700368);
		int history = session.PacketHistory.Count;
		await AxUseArenaPortalAsync(session, server, 700368, AxArena, token);
		Assert.DoesNotContain(session.PacketHistory.Skip(history), packet => AxIsTimer(packet, 2947));
		Assert.False(server.GetController().HasScheduledTask(TaskId.QUEST_TIMER), "a timer started outside an attempt");
		return (server.GetInstanceId(), AxLivingSpirits(server));
	}

	private static int AxLivingSpirits(Player server) =>
		server.GetWorldMapInstance().GetNpcs().Count(npc => !npc.IsDead() && npc.GetNpcId() is 213583 or 213584);

	/// <summary>Use one of the arena's two portal objects, the entrance 700368 or the exit 730067, and follow it to its map.</summary>
	private static async Task AxUseArenaPortalAsync(SimulationL0Session session, Player server, int npcId, int toMap, CancellationToken token)
	{
		Npc portal = server.GetWorldMapInstance().GetNpcs(npcId).First();
		Assert.True(NaturalFlightPolicy.Distance(session.CurrentPosition, new(portal.GetX(), portal.GetY(), portal.GetZ(), 0)) <= 6, $"portal {npcId} is out of reach");
		await NaturalDialogProtocol.OpenAsync(session, portal.GetObjectId(), token);
		DecodedBotServerPacket use = await session.WaitForPacketAsync(typeof(SM_USE_OBJECT), token,
			packet => packet.Get<int>("targetObjectId") == portal.GetObjectId() && packet.Get<byte>("actionType") == 1);
		session.Api.World.BeginWorldReload();
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(use.Get<int>("durationMs") + 1), token);
		await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token, packet => packet.Get<int>("worldId") == toMap);
		await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
		Assert.Equal(toMap, server.GetWorldId());
	}

	/// <summary>Walk out through the exit 730067 at var 5. Leaving is a failed attempt: the enter-world hook ends the timer and sets var 6.</summary>
	private static async Task AxLeaveArenaAsync(SimulationL0Session session, Player server, CancellationToken token)
	{
		await AxUseArenaPortalAsync(session, server, 730067, AxPandaemonium, token);
		Assert.Equal(6, AxArenaVar(server));
		Assert.False(server.GetController().HasTask(TaskId.QUEST_TIMER));
	}

	/// <summary>Probe setup: one living spirit dies to a full-HP hit credited to the player. XP and the quest counter need the player in
	/// range, so the probe is moved beside the spirit first, and back to where it stood afterwards (a setup move leaves it protected,
	/// so the group does not pull).</summary>
	private async Task<Npc> AxKillSpiritAsync(SimulationL0Session session, Player server, CancellationToken token, int npcId = 213584)
	{
		BotPosition back = session.CurrentPosition;
		async Task MoveForSetupAsync(float x, float y, float z)
		{
			session.Api.World.BeginWorldReload();
			await TeleportForSetupAsync(session, server, AxArena, x, y, z, token, targetInstanceId: server.GetInstanceId());
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
		}
		Npc victim = server.GetWorldMapInstance().GetNpcs(npcId).Where(npc => !npc.IsDead()).OrderBy(npc => npc.GetX()).First();
		await MoveForSetupAsync(victim.GetX() + 2, victim.GetY(), victim.GetZ() + 0.2f);
		victim.GetController().OnAttack(server, null!, SmAttackStatus.TYPE.REGULAR, victim.GetLifeStats().GetMaxHp(), true,
			SmAttackStatus.LOG.REGULAR, null, Aion.GameServer.SkillEngine.Model.HopType.DAMAGE);
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
		await session.SynchronizeAsync(token);
		Assert.True(victim.IsDead());
		// The tenth kill plays movie 168, whose end teleports the player out: there is no arena to move back in then.
		if (server.GetWorldId() == AxArena && AxArenaKills(server) != 10)
			await MoveForSetupAsync(back.X, back.Y, back.Z);
		return victim;
	}

	/// <summary>A controlled death through the real die hook, and the client's resurrection prompt.</summary>
	private static async Task<DecodedBotServerPacket> AxDieAsync(SimulationL0Session session, Player server, CancellationToken token)
	{
		Assert.True(server.GetController().Die(server));
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(501), token);
		DecodedBotServerPacket prompt = await session.WaitForPacketAsync(typeof(SM_DIE), token);
		await session.SynchronizeAsync(token);
		Assert.True(server.IsDead());
		return prompt;
	}

	/// <summary>Follow a teleport the server started by itself (the arena timer's end, movie 168's end, a bind revive) to its map;
	/// <paramref name="mapId"/> null is any map but the arena.</summary>
	private static async Task AxFollowTeleportAsync(SimulationL0Session session, CancellationToken token, int? mapId = AxPandaemonium)
	{
		session.Api.World.BeginWorldReload();
		await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token, packet => mapId == null ? packet.Get<int>("worldId") != AxArena : packet.Get<int>("worldId") == mapId);
		await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
	}

	/// <summary>How far the player stands from where Java's Q2947 teleports end: (1006.1, 1526, 222.2), beside Garm.</summary>
	private static float AxDistanceFromGarmTeleport(Player server) =>
		NaturalFlightPolicy.Distance(new(server.GetX(), server.GetY(), server.GetZ(), 0), new(1006.1f, 1526f, 222.2f, 0));

	/// <summary>
	/// AX-12b: the operator's rule, "always soul heal when we resurrect at an obelisk". A death takes XP, a third of it for good
	/// and the rest recoverable. The bind revive leaves soul sickness (skill 8291). Golenthor, 1.2 m from Morheim's obelisk,
	/// answers the RECOVERY dialog action with the priced question; accepted, the recoverable XP is back, the price Java's
	/// DialogService names is charged, and the sickness is gone. A second visit has nothing to recover and costs nothing.
	/// </summary>
	[SkippableFact]
	public async Task SoulHealerGivesBackTheRecoverableExperienceAfterAnObeliskRevive()
	{
		await RunCapitalProbeAsync("AX12B", 97, "Asimsoulheal", async (probe, session, token) =>
		{
			Player server = probe.Server;
			AxLevel25(server);
			server.GetInventory().IncreaseKinah(40_000);
			// A death takes from the XP earned in the level, so the probe has some.
			server.GetCommonData().SetExp(server.GetCommonData().GetExp() + 300_000);
			await session.SynchronizeAsync(token);
			var services = new NaturalServiceSteps(session);
			await probe.SetupNearAsync(AxMorheim, 700231);
			Npc obelisk = server.GetWorldMapInstance().GetNpcs(700231).First();
			NaturalServiceOutcome bind = await services.BindAsync(obelisk.GetObjectId(), new(obelisk.GetX(), obelisk.GetY(), obelisk.GetZ(), 0), AxMorheim, 2690, 6, token);
			Assert.True(bind.IsDone, bind.Reason);

			long xpAlive = server.GetCommonData().GetExp();
			DecodedBotServerPacket prompt = await AxDieAsync(session, server, token);
			Assert.False(prompt.Get<bool>("allowInstanceRevive"));
			long recoverable = server.GetCommonData().GetExpRecoverable(), xpDead = server.GetCommonData().GetExp();
			long lost = xpAlive - xpDead, forGood = lost - recoverable;
			Assert.True(recoverable > 0 && forGood > 0, $"the death took {lost} XP, {recoverable} of it recoverable");
			Assert.Equal(recoverable, session.Api.World.RecoverableExperience);

			// The obelisk is on the same map: the revive is a spawn there, a quarter of the HP, and soul sickness.
			session.Api.World.BeginWorldReload();
			await session.SendPacketAsync(session.Api.Revive(BotReviveType.Bind), token);
			await session.WaitForPacketAsync(typeof(SM_CHANNEL_INFO), token);
			await session.SynchronizeAsync(token);
			session.AcceptTeleportPosition();
			Assert.False(server.IsDead());
			Assert.True(server.GetEffectController().HasAbnormalEffect(8291), "the bind revive left no soul sickness");
			Assert.Equal(1, server.GetCommonData().GetDeathCount());

			Npc golenthor = server.GetWorldMapInstance().GetNpcs(204318).First();
			float fromObelisk = NaturalFlightPolicy.Distance(new(golenthor.GetX(), golenthor.GetY(), golenthor.GetZ(), 0), new(obelisk.GetX(), obelisk.GetY(), obelisk.GetZ(), 0));
			float fromRevive = NaturalFlightPolicy.Distance(new(golenthor.GetX(), golenthor.GetY(), golenthor.GetZ(), 0), session.CurrentPosition);
			Assert.Equal(NaturalServicePolicy.SoulHealerTitleId, golenthor.GetObjectTemplate().GetTitleId());
			long kinah = server.GetInventory().GetKinah();
			NaturalSoulHeal heal = await services.SoulHealAsync(golenthor.GetObjectId(), 204318, 0, token);
			long price = (int)(recoverable * (0.25 - (0.00000015 * recoverable)));
			Assert.Equal((recoverable, price), (heal.Recovered, heal.Price));
			Assert.Equal(price, kinah - server.GetInventory().GetKinah());
			Assert.Equal((0L, xpAlive - forGood), (server.GetCommonData().GetExpRecoverable(), server.GetCommonData().GetExp()));
			Assert.False(server.GetEffectController().HasAbnormalEffect(8291), "the soul healing left the soul sickness on");
			Assert.Equal(0, server.GetCommonData().GetDeathCount());

			// Nothing left to recover: no question and no charge.
			NaturalSoulHeal again = await services.SoulHealAsync(golenthor.GetObjectId(), 204318, 0, token);
			Assert.Equal((0L, 0L), (again.Recovered, again.Price));
			Assert.Equal(kinah - price, server.GetInventory().GetKinah());
			Console.WriteLine($"AX-12b soul heal: the death took {lost} XP, {forGood} for good and {recoverable} recoverable; bind revive with soul sickness; " +
				$"Golenthor {fromObelisk:F1} m from the obelisk and {fromRevive:F1} m from the revive point gave {heal.Recovered} XP back for {heal.Price} Kinah " +
				$"and took the sickness off; a second visit cost {again.Price}");
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

	/// <summary>AX-04: the inventory check the leg runs after every turn-in. The staff with the most magic boost is worn and no
	/// mace or shield goes on; the reward sacks and coin chests are opened; the manastone is discarded; the sealed bundle stays.</summary>
	[SkippableFact]
	public async Task AbyssEntryInventoryCheckWearsTheBestStaffOpensTheRewardsAndDiscardsTheManastone()
	{
		await RunCapitalProbeAsync("AX04", 95, "Asimaxinventory", async (probe, session, token) =>
		{
			Player server = probe.Server;
			AxLevel25(server);
			NaturalAbyssInventory rules = NaturalAltgardContract.LoadLeg(NaturalAbyssEntry.Leg).AbyssEntry!.Inventory;
			var items = fixture.DataManager.StaticData.ItemDataDh;
			var refused = new HashSet<int>();
			BotWorldModel world = session.Api.World;
			Task<IReadOnlyList<NaturalGearUpgrade>> EquipAsync(CancellationToken equipToken) => NaturalInventoryCheck.EquipAsync(session,
				world.Inventory.Values, id => NaturalInventoryCheck.Describe(items.GetItemTemplate(id), PlayerClass.CLERIC, Race.ASMODIANS),
				(long)Aion.GameServer.Model.Items.ItemSlot.MAIN_OFF_OR_SUB_OFF, refused, equipToken);
			async Task GiveAsync(params (int Id, long Count)[] given)
			{
				foreach ((int id, long count) in given) Assert.Equal(0, ItemService.AddItem(server, id, count, allowInventoryOverflow: true));
				await session.SynchronizeAsync(token);
			}
			int WornWeapon() => world.Inventory.Values.Single(item => ((item.Details.EquippedSlot ?? 0) & 1) != 0).ItemId;
			long Owned(int id) => world.Inventory.Values.Where(item => item.ItemId == id).Sum(item => item.Count);

			// The first staff the character owns replaces whatever is in its hand, though a staff is no higher in item level.
			int startWeapon = WornWeapon();
			await GiveAsync((101501357, 1));
			IReadOnlyList<NaturalGearUpgrade> first = await EquipAsync(token);
			Assert.Equal([101501357], first.Where(upgrade => upgrade.Slot == 1).Select(upgrade => upgrade.ItemId));
			Assert.Equal(101501357, WornWeapon());

			// The leg's rewards, and two things the staff rule must leave in the bag: a level-25 mace and a level-21 shield.
			await GiveAsync((101501224, 1), (100101199, 1), (115001119, 1), (188051192, 2), (188050878, 1), (188050873, 1), (167000465, 1), (188053787, 1));
			long coins = Owned(186000007);
			NaturalInventoryCheckResult result = await NaturalInventoryCheck.RunAsync(session, "probe", rules, EquipAsync, items.GetItemTemplate,
				() => (world.CubeExpansion?.Capacity ?? 27) - world.Inventory.Values.Count(item => (item.Details.EquippedSlot ?? 0) == 0 && item.ItemId != BotWorldModel.KinahItemId),
				token);

			Assert.Equal(101501224, WornWeapon());
			Assert.Equal([101501224], result.Worn.Select(upgrade => upgrade.ItemId));
			Assert.All(new[] { 100101199, 115001119, 101501357 }, id => Assert.Equal(0, world.Inventory.Values.Single(item => item.ItemId == id).Details.EquippedSlot ?? 0));
			Assert.Equal(4, result.Opened.Length);
			Assert.Empty(result.NotOpened);
			Assert.All(rules.Open, id => Assert.Equal(0, Owned(id)));
			long coinsGained = Owned(186000007) - coins;
			Assert.InRange(coinsGained, 2, 20);
			Assert.Equal([167000465], result.Discarded.Select(item => item.ItemId));
			Assert.Equal((0L, 1L), (Owned(167000465), Owned(188053787)));
			Console.WriteLine($"AX-04 inventory check: weapon {startWeapon} -> 101501357 -> {WornWeapon()}; opened " +
				string.Join("; ", result.Opened.Select(container => $"{container.ItemId} gave [{string.Join(", ", container.Gained.Select(item => $"{item.Key} x{item.Value}"))}]")) +
				$"; {coinsGained} Bronze Coins from the two chests; discarded {string.Join(", ", result.Discarded.Select(item => item.ItemId))}; " +
				$"bundle 188053787 kept; {result.FreeSlots} free slots");
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
