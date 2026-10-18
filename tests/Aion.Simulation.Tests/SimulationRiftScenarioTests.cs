using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.GameServer.Controllers;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Rift;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Spawns.Vortexspawns;
using Aion.GameServer.Model.Templates.Worldraid;
using Aion.GameServer.Model.Vortex;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Rift;
using Aion.GameServer.Services.Vortex;
using Aion.GameServer.Utils;
using Aion.GameServer.Configs.Main;

namespace Aion.Simulation.Tests;

/// <summary>
/// P11-04, scenario RIFT: Java's schedules of rifts, the dimensional vortex and world raids played through on the virtual
/// clock from the process's own epoch, Saturday 2026-10-03 15:58 UTC (<c>reset-RIFT</c>): the hourly rift openings of
/// rift_schedule.xml (16:00, 17:00 and 18:00, each closed 59 minutes later) with a crossing each way between Cygnea and
/// Enshar and Enshar's Saturday 17:00 rift with guards, which is volatile; the Brusthonin invasion of
/// <c>gameserver.vortex.brusthonin.schedule</c> (Saturday 16:00, <c>gameserver.vortex.duration</c> 2): the vortex rift in
/// Kaisinel Academy, two Elyos invaders and their offence alliance, two Asmodian defenders (one inside the invasion zones
/// at the start, one arriving later) and their defence alliance, the level refusal, and the invasion's end when the
/// defenders destroy the rift generator; the world raids of world_raid_schedule.xml on the 3rd at 17:30 in Verteron and
/// Altgard (two of three locations each): the flag, the vortex, the markers, the boss at 18:00, one boss killed and the
/// others gone with their hour at 19:00; and, after a night with everyone logged out, the Theobomos invasion of Sunday
/// 16:00, which the Asmodians enter from Marchutan Priory and the Elyos defend, ended by its two-hour timer at 18:00.
/// Every expected value is Java's: <c>RiftService</c>, <c>RiftOpenRunnable</c>,
/// <c>RiftManager</c>, <c>RiftInformer</c>, <c>RVController</c>, <c>SM_RIFT_ANNOUNCE</c>, <c>VortexService</c>,
/// <c>Invasion</c>, <c>VortexLocation</c>, <c>WorldRaidService</c>, <c>WorldRaidRunnable</c>, <c>WorldRaid</c> and
/// <c>WorldRaidAI</c>.
/// <para>
/// Director setup, recorded here as the plan asks: every subject is made a Gladiator, the four who cross level 55 (the
/// rifts of Cygnea and Enshar take 50 to 65 or 55 to 65, the vortex 45 to 65) and the two others level 30 (one is refused
/// by the vortex for it); every subject is placed by setup teleports (the bases of Cygnea and Enshar, Kaisinel Academy
/// beside the vortex's spot, quiet ground of Wailing Cliffs, the invasion zone of Brusthonin's rift generator, the obelisks of
/// Verteron and Altgard, beside Marchutan Priory's vortex spot and on quiet ground of Theobomos' invasion zones; next to a
/// rift before using it, 30 m from the generator's camp and next to the boss before fighting them); the rift generator's
/// health is lowered to <see cref="RiftTargetHealth"/> and the Altgard boss's too, so that the bots' real auto-attacks
/// finish them.
/// </para>
/// </summary>
public sealed partial class SimulationFastScenarioTests
{
	internal static readonly DateTimeOffset RiftEpoch = new(2026, 10, 3, 15, 58, 0, TimeSpan.Zero);
	private const int Cygnea = 210070000;
	private const int Enshar = 220080000;
	private const int KaisinelAcademy = 110070000;
	private const int Brusthonin = 220050000;
	private const int Verteron = 210030000;
	// Altgard (220030000) is declared with the Cleric encounter tests.
	private const int BrusthoninInvasion = 1; // dimensional_vortex.xml: the Asmodians defend Brusthonin, the Elyos invade
	private const int TheobomosInvasion = 0; // and the Elyos defend Theobomos, the Asmodians invade
	private const int Theobomos = 210060000;
	private const int MarchutanPriory = 120080000;
	private const int TheobomosGenerator = 209487;
	private const int MarchutanVortexRift = 831143;
	private const int TheobomosVortexExit = 831144;
	private const int BrusthoninGenerator = 209486;
	private const int BrusthoninPeaceSurt = 205150; // the location's PEACE group
	private const int KaisinelVortexRift = 831141;
	private const int BrusthoninVortexExit = 831142;
	private const int VortexPassQuestion = 904304;
	private const int VortexDefendQuestion = 904306;
	private const int WorldRaidFlag = 832819;
	private const int WorldRaidVortex = 702550;
	private const int WorldRaidMarker = 702548;
	private const int WorldRaidAdventEffect = 702549;

	/// <summary>The health the director leaves the rift generator and the raid boss before the bots strike.</summary>
	private const int RiftTargetHealth = 4;

	/// <summary>rift_schedule.xml's hourly maps and the most rifts <c>RiftService.prepareRiftOpening</c> opens there at once.</summary>
	private static readonly (int World, int MaxCount, bool Always)[] HourlyRiftMaps =
	[
		(210020000, 4, false), (210040000, 4, false), (210050000, 3, false), (Cygnea, 4, true),
		(220020000, 4, false), (220040000, 4, false), (220070000, 3, false), (Enshar, 4, true),
	];

	private async Task RunRiftAsync(ScenarioDefinition scenario, bool includeHistory)
	{
		var sessions = new List<SimulationL0Session>();
		try
		{
			await PlayRiftAsync(scenario, includeHistory, sessions);
		}
		catch (Exception exception) when (exception is not SimulationLogPolicyException)
		{
			// Harness rule 2: the whole exception and every bot's recent packets.
			throw new InvalidOperationException("RIFT failed at " + SystemClock.UtcNow().ToString("O") + "; recent packets:" + Environment.NewLine +
				string.Join(Environment.NewLine, sessions.Select(session => session.CurrentStep + " " + string.Join(" | ",
					session.PacketHistory.TakeLast(25).Select(packet => packet.PacketType.Name +
						(packet.PacketType == typeof(SM_SYSTEM_MESSAGE) ? "(" + (packet.Get<string?>("name") ?? packet.Get<int>("msgId").ToString()) + ")" : ""))))), exception);
		}
	}

	private async Task PlayRiftAsync(ScenarioDefinition scenario, bool includeHistory, List<SimulationL0Session> created)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(30));
		CancellationToken token = timeout.Token;
		Assert.Equal(RiftEpoch, fixture.Epoch);
		DateTimeOffset day = new(2026, 10, 3, 0, 0, 0, TimeSpan.Zero);
		Assert.Equal(DayOfWeek.Saturday, day.DayOfWeek);
		DateTimeOffset at1600 = day.AddHours(16), at1700 = day.AddHours(17), at1800 = day.AddHours(18), at1900 = day.AddHours(19);
		Assert.True(SystemClock.UtcNow() < at1600);
		// The configuration Java reads: custom.properties' vortex duration is 2 hours, the rifts' 1.
		Assert.True(CustomConfig.RIFT_ENABLED && CustomConfig.VORTEX_ENABLED && EventsConfig.ENABLE_WORLDRAID && EventsConfig.WORLDRAID_ENABLE_SPAWNMSG);
		Assert.Equal(1, CustomConfig.RIFT_DURATION);
		Assert.Equal(2, CustomConfig.VORTEX_DURATION);

		await using var traveller = new SimulationL0Session(fixture, policy, "b01", 106, "Aesimrifta", Race.ELYOS);
		await using var invader = new SimulationL0Session(fixture, policy, "b02", 107, "Aesimriftb", Race.ELYOS);
		await using var crosser = new SimulationL0Session(fixture, policy, "b03", 108, "Asimriftc", Race.ASMODIANS);
		await using var defender = new SimulationL0Session(fixture, policy, "b04", 109, "Asimriftd", Race.ASMODIANS);
		await using var raider = new SimulationL0Session(fixture, policy, "b05", 110, "Asimrifte", Race.ASMODIANS);
		await using var watcher = new SimulationL0Session(fixture, policy, "b06", 111, "Aesimriftf", Race.ELYOS);
		SimulationL0Session[] sessions = [traveller, invader, crosser, defender, raider, watcher];
		created.AddRange(sessions);
		riftCast = sessions;

		// s01: login, class and level, and the setup teleports to where each subject waits for 16:00.
		foreach (SimulationL0Session session in sessions)
		{
			session.BeginStep("s01", "login-create-enter-and-setup");
			await session.LoginAndAuthenticateAsync(token);
			await session.CreateCharacterAsync(token);
			await session.EnterWorldAsync(token);
			await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
			Player player = fixture.World.GetPlayer(session.CharacterId);
			Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
			Assert.True(ClassChangeService.SetClass(player, PlayerClass.GLADIATOR, validate: true, updateDaevaStatus: true));
			player.GetCommonData().SetLevel(session == raider || session == watcher ? 30 : 55);
		}
		Player travellerPlayer = fixture.World.GetPlayer(traveller.CharacterId), invaderPlayer = fixture.World.GetPlayer(invader.CharacterId),
			crosserPlayer = fixture.World.GetPlayer(crosser.CharacterId), defenderPlayer = fixture.World.GetPlayer(defender.CharacterId),
			raiderPlayer = fixture.World.GetPlayer(raider.CharacterId), watcherPlayer = fixture.World.GetPlayer(watcher.CharacterId);
		VortexLocation vortex = VortexService.GetInstance().GetLocationByWorld(Brusthonin);
		Assert.Equal(BrusthoninInvasion, vortex.GetId());
		Assert.Equal((Race.ASMODIANS, Race.ELYOS, KaisinelAcademy, Brusthonin), (vortex.GetDefendersRace(), vortex.GetInvadersRace(), vortex.GetHomeWorldId(), vortex.GetInvasionWorldId()));
		// Inside WAILING_CLIFFS_220050000, the invasion zone of the rift generator, 57 m from any spawn of the map.
		var (cliffX, cliffY) = (2238.8f, 2924.3f);
		await TeleportForSetupAsync(traveller, travellerPlayer, Cygnea, 2945.5f, 852.5f, 572.5f, token);
		await TeleportForSetupAsync(invader, invaderPlayer, KaisinelAcademy, 455.5f, 236f, 127f, token);
		await TeleportForSetupAsync(crosser, crosserPlayer, Enshar, 430.5f, 2229.5f, 220f, token);
		await TeleportForSetupAsync(defender, defenderPlayer, Brusthonin, cliffX, cliffY, Ground(Brusthonin, cliffX, cliffY, 140, 40), token);
		await TeleportForSetupAsync(raider, raiderPlayer, Altgard, 1656f, 1817.5f, 253.8f, token);
		await TeleportForSetupAsync(watcher, watcherPlayer, KaisinelAcademy, 450f, 236f, 127f, token);
		await AdvanceSiegeWorldToAsync(sessions, SystemClock.UtcNow().AddSeconds(2), token);
		await SyncSiegeSessionsAsync(sessions, token);

		// Before 16:00: no rift is open, the vortex location holds its PEACE group, and the defender is one of its players.
		Assert.All(RiftService.GetInstance().GetRiftLocations().Values, location => Assert.False(location.IsOpened(), $"rift {location.GetId()} open before 16:00"));
		Assert.Empty(RiftManager.GetSpawnedRifts(KaisinelAcademy));
		Assert.False(vortex.IsActive());
		Assert.False(VortexService.GetInstance().IsInvasionInProgress(BrusthoninInvasion));
		Assert.Equal(VortexGroup(BrusthoninInvasion, VortexStateType.PEACE), vortex.GetSpawned().Select(npc => ((Npc)npc).GetNpcId()).Order());
		Assert.Contains(BrusthoninPeaceSurt, vortex.GetSpawned().Select(npc => ((Npc)npc).GetNpcId()));
		Assert.True(vortex.IsInsideLocation(defenderPlayer));
		Assert.Equal(new[] { defender.CharacterId }, vortex.GetPlayers().Keys.ToArray());

		// s02, 16:00: the hourly rifts of eight maps and the Brusthonin invasion.
		int[] marks = Marks(sessions);
		int logMark = fixture.LogCapture.Entries.Count;
		foreach (SimulationL0Session session in sessions) session.BeginStep("s02", "rifts-open-and-invasion-starts");
		await AdvanceSiegeWorldToAsync(sessions, at1600.AddSeconds(1), token);
		await SyncSiegeSessionsAsync(sessions, token);
		RiftLocation[] firstHour = AssertHourlyRifts(logMark);
		AssertRiftBatch(traveller, marks[0], Cygnea);
		AssertRiftBatch(crosser, marks[2], Enshar);

		Assert.True(VortexService.GetInstance().IsInvasionInProgress(BrusthoninInvasion));
		Assert.True(vortex.IsActive());
		Invasion invasion = Assert.IsType<Invasion>(vortex.GetActiveVortex());
		RVController gate = Assert.IsType<RVController>(vortex.GetVortexController());
		Assert.True(gate.IsMaster() && gate.IsVortex() && !gate.IsVolatile() && !gate.IsInvasion());
		Assert.Same(RiftEnum.KAISINEL_AM, gate.GetRiftTemplate());
		Npc gateNpc = gate.GetOwner();
		Assert.Equal((KaisinelVortexRift, KaisinelAcademy), (gateNpc.GetNpcId(), gateNpc.GetWorldId()));
		Npc gateExit = gate.GetSlave();
		Assert.Equal((BrusthoninVortexExit, Brusthonin), (gateExit.GetNpcId(), gateExit.GetWorldId()));
		Assert.Equal((24, 45, 65), (gate.GetMaxEntries()!.Value, gate.GetMinLevel()!.Value, gate.GetMaxLevel()!.Value));
		Assert.InRange(gate.GetRemainTime(), 2 * 3600 - 2, 2 * 3600);
		// Invasion.startInvasion: the PEACE group despawned, the vortex rift and the INVASION group spawned.
		Assert.Equal(VortexGroup(BrusthoninInvasion, VortexStateType.INVASION).Append(KaisinelVortexRift).Append(BrusthoninVortexExit).Order(),
			vortex.GetSpawned().Select(npc => ((Npc)npc).GetNpcId()).Order());
		Assert.All(vortex.GetSpawned(), npc => Assert.True(npc.IsSpawned()));
		Npc generator = (Npc)Assert.Single(vortex.GetSpawned(), npc => ((Npc)npc).GetNpcId() == BrusthoninGenerator);
		Assert.Contains("Rift opened: KAISINEL_AM, spawned 0 Npcs (guards=false).", LogMessages(logMark));
		// RiftInformer.sendRiftsInfo(home world): Kaisinel Academy's players see the vortex rift, Brusthonin's an empty announce.
		AssertRiftBatch(invader, marks[1], KaisinelAcademy);
		AssertRiftBatch(watcher, marks[5], KaisinelAcademy);
		AssertRiftBatch(defender, marks[3], Brusthonin);
		// updateAlliance: the defender inside the invasion zones is asked to defend.
		defender.BeginStep("s02", "accept-the-defence");
		DecodedBotServerPacket defend = Assert.Single(defender.PacketHistory.Skip(marks[3]), packet => IsQuestion(packet, VortexDefendQuestion));
		Assert.Equal((0, 0), (defend.Get<int>("senderId"), defend.Get<int>("rangeOrCooldownSeconds")));
		await defender.SendPacketAsync(defender.Api.Answer(1), token);
		await defender.SynchronizeAsync(token);
		Assert.Equal(new[] { defender.CharacterId }, invasion.GetDefenders().Keys.ToArray());
		Assert.False(defenderPlayer.IsInTeam());

		// s03: the traveller crosses from Cygnea into Enshar by a rift, and the crosser from Enshar into Cygnea.
		traveller.BeginStep("s03", "cross-into-enshar");
		await CrossByRiftAsync(traveller, travellerPlayer, Cygnea, Enshar, crosser, token);
		crosser.BeginStep("s03", "cross-into-cygnea");
		await CrossByRiftAsync(crosser, crosserPlayer, Enshar, Cygnea, traveller, token);

		// s04: the level-30 watcher is refused by the vortex rift (RVController.onAccept: below its level 45).
		watcher.BeginStep("s04", "vortex-refuses-level-30");
		int auditMark = fixture.LogCapture.Entries.Count;
		await AnswerRiftQuestionAsync(watcher, gateNpc, VortexPassQuestion, token);
		await AdvanceSiegeWorldToAsync(sessions, SystemClock.UtcNow().AddSeconds(1), token);
		await SyncSiegeSessionsAsync(sessions, token);
		Assert.Equal(KaisinelAcademy, watcherPlayer.GetWorldId());
		Assert.Equal(0, gate.GetUsedEntries());
		Assert.Empty(gate.GetPassedPlayers());
		Assert.Contains(fixture.LogCapture.Entries.Skip(auditMark), entry => entry.Category == "AUDIT_LOG"
			&& entry.Message == "Player [id=" + watcher.CharacterId + ", name=" + watcherPlayer.GetName() + "] tried to use rift outside level restriction");

		// s05: the two Elyos invade Brusthonin by the vortex rift; the second makes their offence alliance.
		invader.BeginStep("s05", "invade-by-the-vortex");
		await InvadeAsync(invader, invaderPlayer, gate, defender, 1, token);
		Assert.Equal(new[] { invader.CharacterId }, invasion.GetInvaders().Keys.ToArray());
		Assert.False(invaderPlayer.IsInTeam());
		traveller.BeginStep("s05", "invade-by-the-vortex");
		await TeleportForSetupAsync(traveller, travellerPlayer, KaisinelAcademy, 455.5f, 228f, 127f, token);
		await InvadeAsync(traveller, travellerPlayer, gate, defender, 2, token);
		Assert.Equal(new[] { invader.CharacterId, traveller.CharacterId }.Order(), invasion.GetInvaders().Keys.Order());
		PlayerAlliance offence = Assert.IsType<PlayerAlliance>(invaderPlayer.GetPlayerAlliance());
		Assert.Same(offence, travellerPlayer.GetPlayerAlliance());
		Assert.Equal(TeamType.ALLIANCE_OFFENCE, offence.GetTeamType());
		Assert.Equal(invader.CharacterId, offence.GetLeaderObject().GetObjectId());
		Assert.Equal(2, offence.Size());

		// s06: the crosser arrives inside the invasion zones while it runs, is asked to defend, and makes the defence alliance.
		crosser.BeginStep("s06", "join-the-defence");
		int crosserMark = crosser.PacketHistory.Count;
		await TeleportForSetupAsync(crosser, crosserPlayer, Brusthonin, cliffX - 3, cliffY, Ground(Brusthonin, cliffX - 3, cliffY, 140, 40), token);
		await AdvanceSiegeWorldToAsync(sessions, SystemClock.UtcNow().AddSeconds(1), token);
		await crosser.SynchronizeAsync(token);
		Assert.True(vortex.IsInsideLocation(crosserPlayer));
		DecodedBotServerPacket lateDefend = Assert.Single(crosser.PacketHistory.Skip(crosserMark), packet => IsQuestion(packet, VortexDefendQuestion));
		Assert.Equal(0, lateDefend.Get<int>("senderId"));
		await crosser.SendPacketAsync(crosser.Api.Answer(1), token);
		await SyncSiegeSessionsAsync(sessions, token);
		Assert.Equal(new[] { defender.CharacterId, crosser.CharacterId }.Order(), invasion.GetDefenders().Keys.Order());
		PlayerAlliance defence = Assert.IsType<PlayerAlliance>(defenderPlayer.GetPlayerAlliance());
		Assert.Same(defence, crosserPlayer.GetPlayerAlliance());
		Assert.Equal(TeamType.ALLIANCE_DEFENCE, defence.GetTeamType());
		Assert.Equal(defender.CharacterId, defence.GetLeaderObject().GetObjectId());

		// s07: the defenders destroy the rift generator; Invasion.stopInvasion sends the invaders home and restores the PEACE
		// group.
		foreach (SimulationL0Session session in new[] { crosser, defender }) session.BeginStep("s07", "destroy-the-rift-generator");
		// The defenders are placed 30 m south-east of the generator, 30 m from every NPC of the camp. A teleport despawns the
		// player (TeleportService.sendLoc), which leaves every zone, and VortexLocation.onLeaveZone gives a defender a 10-second
		// kick timer; back inside by then, it does nothing. They wait it out, and the striker walks the rest: had the invasion
		// ended first, the timer would dereference the ended invasion, in Java as here (see the plan, section 7).
		var (stageX, stageY) = (2256.7f, 2773.4f);
		await TeleportForSetupAsync(defender, defenderPlayer, Brusthonin, stageX, stageY, Ground(Brusthonin, stageX, stageY, 140, 40), token);
		await TeleportForSetupAsync(crosser, crosserPlayer, Brusthonin, stageX + 2, stageY, Ground(Brusthonin, stageX + 2, stageY, 140, 40), token);
		await AdvanceSiegeWorldToAsync(sessions, SystemClock.UtcNow().AddSeconds(10.5), token);
		await SyncSiegeSessionsAsync(sessions, token);
		Assert.Equal(new[] { defender.CharacterId, crosser.CharacterId }.Order(), invasion.GetDefenders().Keys.Order());
		Assert.True(vortex.IsInsideActiveVotrex(defenderPlayer) && vortex.IsInsideActiveVotrex(crosserPlayer));
		Assert.Same(defence, defenderPlayer.GetPlayerAlliance());
		Assert.Same(defence, crosserPlayer.GetPlayerAlliance());
		Assert.False(defenderPlayer.IsDead() || crosserPlayer.IsDead());
		// The striker walks to the generator's east side, where nothing stands between them (GeoService.canSee).
		float strikeX = generator.GetX() + 1.5f, strikeY = generator.GetY();
		await defender.MoveToPositionAsync(new Aion.Bots.World.BotPosition(strikeX, strikeY,
			Ground(Brusthonin, strikeX, strikeY, generator.GetZ() + 5, generator.GetZ() - 5), 30), token);
		await SyncSiegeSessionsAsync(sessions, token);
		Assert.True(Aion.GameServer.World.Geo.GeoService.GetInstance().CanSee(defenderPlayer, generator), "the striker cannot see the generator");
		generator.GetLifeStats().SetCurrentHp(RiftTargetHealth);
		marks = Marks(sessions);
		await StrikeUntilDeadAsync(sessions, generator, [(defender, defenderPlayer)], token);
		await AdvanceSiegeWorldToAsync(sessions, SystemClock.UtcNow().AddSeconds(2), token);
		await SyncSiegeSessionsAsync(sessions, token);
		AssertInvasionEnded(vortex, invasion, gate, [(traveller, travellerPlayer, marks[0]), (invader, invaderPlayer, marks[1])], offence, defence,
			[defenderPlayer, crosserPlayer]);
		// The vortex rift's despawn (RVController.onDespawn) is announced to Kaisinel Academy's players.
		Assert.Contains(RiftAnnounces(watcher, marks[5]), announce => announce == new RiftAnnounce(4, [], gateNpc.GetObjectId(), 0, 0, 0, 0, 0, 0, 0, 0, 0));
		Assert.Contains(RiftAnnounces(defender, marks[3]), announce => announce == new RiftAnnounce(4, [], gateExit.GetObjectId(), 0, 0, 0, 0, 0, 0, 0, 0, 0));

		// s08, 16:55: the watcher moves to Cygnea's base to see the first hour's rifts close and the next ones open, and the
		// crosser goes back to Enshar's base for the Saturday rift with guards.
		watcher.BeginStep("s08", "watch-cygnea");
		crosser.BeginStep("s08", "watch-enshar");
		await AdvanceSiegeWorldToAsync(sessions, day.AddHours(16).AddMinutes(55), token);
		Assert.All(firstHour, location => Assert.True(location.IsOpened(), $"rift {location.GetId()} closed before 16:59"));
		await TeleportForSetupAsync(watcher, watcherPlayer, Cygnea, 2945.5f, 857f, 572.5f, token);
		await TeleportForSetupAsync(crosser, crosserPlayer, Enshar, 430.5f, 2229.5f, 220f, token);
		await AdvanceSiegeWorldToAsync(sessions, SystemClock.UtcNow().AddSeconds(2), token);
		await SyncSiegeSessionsAsync(sessions, token);

		// s09, 16:59: RiftOpenRunnable's close task (duration * 3540 s) closes every rift of the first hour.
		marks = Marks(sessions);
		foreach (SimulationL0Session session in sessions) session.BeginStep("s09", "rifts-close");
		Dictionary<int, int> firstHourNpcs = RiftNpcWorlds(firstHour);
		await AdvanceSiegeWorldToAsync(sessions, day.AddHours(16).AddMinutes(59).AddSeconds(1), token);
		await SyncSiegeSessionsAsync(sessions, token);
		foreach (RiftLocation location in firstHour)
		{
			Assert.False(location.IsOpened(), $"rift {location.GetId()} still open at 16:59");
			Assert.False(RiftService.GetInstance().IsRiftOpened(location.GetId()));
			Assert.Empty(location.GetSpawned());
		}
		Assert.All(firstHourNpcs.Keys, objectId => Assert.Null(fixture.World.FindVisibleObject(objectId)));
		Assert.All(HourlyRiftMaps, map => Assert.Empty(RiftManager.GetSpawnedRifts(map.World)));
		AssertRiftDespawns(watcher, marks[5], firstHourNpcs, Cygnea);
		AssertRiftDespawns(crosser, marks[2], firstHourNpcs, Enshar);

		// s10, 17:00: the second hour's rifts, and Enshar's Saturday 17:00 opening with guards: one rift, volatile.
		marks = Marks(sessions);
		logMark = fixture.LogCapture.Entries.Count;
		foreach (SimulationL0Session session in sessions) session.BeginStep("s10", "rifts-open-with-guards");
		await AdvanceSiegeWorldToAsync(sessions, at1700.AddSeconds(1), token);
		await SyncSiegeSessionsAsync(sessions, token);
		RiftLocation guarded = Assert.Single(RiftService.GetInstance().GetRiftLocations().Values, location => location.IsOpened() && location.HasSpawns());
		Assert.Equal(Enshar, guarded.GetWorldId());
		Assert.InRange(guarded.GetId(), 2286, 2288);
		Assert.True(guarded.IsAutoCloseable());
		RiftEnum guardedEnum = RiftEnum.GetRift(guarded.GetId());
		Npc volatileRift = Assert.Single(RiftManager.GetSpawnedRifts(Enshar), npc => ((RVController)npc.GetController()).GetRiftTemplate() == guardedEnum
			&& ((RVController)npc.GetController()).IsMaster());
		Assert.True(((RVController)volatileRift.GetController()).IsVolatile());
		Assert.Equal(702723, volatileRift.GetNpcId());
		// RiftService.openRifts spawns the location's rift_spawn group (in Cygnea, at the exit's side) before the rift and its exit,
		// so RiftManager's line counts the guards.
		SpawnTemplate[] guardSpawns = (DataManager.SPAWNS_DATA.GetRiftSpawnsByLocId(guarded.GetId()) ?? []).SelectMany(group => group.GetSpawnTemplates()).ToArray();
		Assert.NotEmpty(guardSpawns);
		Assert.Equal(guardSpawns.Length + 2, guarded.GetSpawned().Count);
		Npc[] guards = guarded.GetSpawned().Keys.Select(objectId => Assert.IsAssignableFrom<Npc>(fixture.World.FindVisibleObject(objectId)))
			.Where(npc => npc.GetController() is not RVController).ToArray();
		Assert.Equal(guardSpawns.Select(spawn => spawn.GetNpcId()).Order(), guards.Select(npc => npc.GetNpcId()).Order());
		Assert.All(guards, npc => Assert.True(npc.IsSpawned() && npc.GetWorldId() == Cygnea));
		Assert.Contains($"Rift opened: {guardedEnum.Name()}, spawned {guardSpawns.Length} Npcs (guards=true).", LogMessages(logMark));
		RiftLocation[] secondHour = AssertHourlyRifts(logMark, guarded);
		AssertRiftBatch(crosser, marks[2], Enshar);
		AssertRiftBatch(watcher, marks[5], Cygnea);
		Assert.Equal(1, LastRiftBatch(RiftAnnounces(crosser, marks[2])).Counts[4]);

		// s11, 17:20: the watcher goes to Verteron for the world raids.
		watcher.BeginStep("s11", "watch-verteron");
		await AdvanceSiegeWorldToAsync(sessions, day.AddHours(17).AddMinutes(20), token);
		await TeleportForSetupAsync(watcher, watcherPlayer, Verteron, 1265.5f, 2174f, 151.4f, token);

		// s12, 17:30: the world raids of the 3rd, two locations each of Verteron and Altgard; only the first of a map speaks.
		marks = Marks(sessions);
		logMark = fixture.LogCapture.Entries.Count;
		foreach (SimulationL0Session session in sessions) session.BeginStep("s12", "world-raids-start");
		await AdvanceSiegeWorldToAsync(sessions, day.AddHours(17).AddMinutes(30).AddSeconds(1), token);
		await SyncSiegeSessionsAsync(sessions, token);
		List<WorldRaidLocation> raids = WorldRaidService.GetInstance().GetActiveWorldRaidLocations();
		Assert.Equal(4, raids.Count);
		WorldRaidLocation[] verteronRaids = raids.Where(raid => raid.GetMapId() == Verteron).OrderBy(raid => raid.GetLocationId()).ToArray();
		WorldRaidLocation[] altgardRaids = raids.Where(raid => raid.GetMapId() == Altgard).OrderBy(raid => raid.GetLocationId()).ToArray();
		Assert.Equal(2, verteronRaids.Length);
		Assert.Equal(2, altgardRaids.Length);
		Assert.All(verteronRaids, raid => Assert.InRange(raid.GetLocationId(), 1, 3));
		Assert.All(altgardRaids, raid => Assert.InRange(raid.GetLocationId(), 4, 6));
		string[] raidLog = LogMessages(logMark);
		Assert.Contains("Attempting to start world raid with ID: veteron_all and location pool: [1, 2, 3]", raidLog);
		Assert.Contains("Attempting to start world raid with ID: altgard_all and location pool: [4, 5, 6]", raidLog);
		int[] startOrder = raidLog.Where(line => line.StartsWith("Starting world raid for location: ", StringComparison.Ordinal))
			.Select(line => int.Parse(line["Starting world raid for location: ".Length..], System.Globalization.CultureInfo.InvariantCulture)).ToArray();
		Assert.Equal(raids.Select(raid => raid.GetLocationId()).Order(), startOrder.Order());
		foreach ((string id, WorldRaidLocation[] started) in new[] { ("veteron_all", verteronRaids), ("altgard_all", altgardRaids) })
			Assert.Contains($"Started scheduled world raid with ID {id} at the following raid locations: [" +
				string.Join(", ", startOrder.Where(location => started.Any(raid => raid.GetLocationId() == location))) + "]", raidLog);
		// WorldRaidService.startRaid: the second raid of a map within 30 s of the first does not broadcast.
		WorldRaidLocation altgardSpeaker = altgardRaids.Single(raid => raid.GetLocationId() == startOrder.First(location => location >= 4));
		WorldRaidLocation altgardSilent = altgardRaids.Single(raid => raid != altgardSpeaker);
		foreach (WorldRaidLocation raid in raids)
		{
			Npc flag = Assert.Single(RaidNpcs(raid, WorldRaidFlag));
			Assert.Equal((raid.GetX(), raid.GetY(), raid.GetZ(), (byte)0), (flag.GetX(), flag.GetY(), flag.GetZ(), flag.GetHeading()));
		}
		AssertRaidMessage(raider, marks[4], "STR_MSG_WORLDRAID_MESSAGE_01");
		AssertRaidMessage(watcher, marks[5], "STR_MSG_WORLDRAID_MESSAGE_01");
		Assert.DoesNotContain(traveller.PacketHistory.Skip(marks[0]), packet => IsSystemMessage(packet, "STR_MSG_WORLDRAID_MESSAGE_01"));

		// 17:40 the vortex (40 m above the location), 17:55 the markers, 17:59 the spawn message.
		raider.BeginStep("s13", "world-raid-preparation");
		marks = Marks(sessions);
		await AdvanceSiegeWorldToAsync(sessions, day.AddHours(17).AddMinutes(40).AddSeconds(1), token);
		await SyncSiegeSessionsAsync(sessions, token);
		foreach (WorldRaidLocation raid in raids)
		{
			Npc raidVortex = Assert.Single(RaidNpcs(raid, WorldRaidVortex));
			Assert.Equal((raid.GetX(), raid.GetY(), raid.GetZ() + 40f), (raidVortex.GetX(), raidVortex.GetY(), raidVortex.GetZ()));
			Assert.Empty(RaidNpcs(raid, WorldRaidMarker));
		}
		AssertRaidMessage(raider, marks[4], "STR_MSG_WORLDRAID_MESSAGE_02");
		AssertRaidMessage(watcher, marks[5], "STR_MSG_WORLDRAID_MESSAGE_02");

		// The second hour's rifts close at 17:59, the guarded one with them.
		marks = Marks(sessions);
		Dictionary<int, int> secondHourNpcs = RiftNpcWorlds(secondHour.Append(guarded));
		await AdvanceSiegeWorldToAsync(sessions, day.AddHours(17).AddMinutes(59).AddSeconds(1), token);
		await SyncSiegeSessionsAsync(sessions, token);
		Assert.All(secondHour.Append(guarded), location => Assert.False(location.IsOpened(), $"rift {location.GetId()} still open at 17:59"));
		Assert.False(volatileRift.IsSpawned());
		Assert.All(guards, npc => Assert.False(npc.IsSpawned(), $"rift guard {npc.GetNpcId()} still stands at 17:59"));
		AssertRiftDespawns(crosser, marks[2], secondHourNpcs, Enshar);
		foreach (WorldRaidLocation raid in raids)
			Assert.Equal(raid.GetLocationMarkers().Count, RaidNpcs(raid, WorldRaidMarker).Count);
		AssertRaidMessage(raider, marks[4], "STR_MSG_WORLDRAID_MESSAGE_03");
		AssertRaidMessage(raider, marks[4], "STR_MSG_WORLDRAID_INVADE_VRITRA");
		AssertRaidMessage(watcher, marks[5], "STR_MSG_WORLDRAID_MESSAGE_03");
		AssertRaidMessage(watcher, marks[5], "STR_MSG_WORLDRAID_INVADE_VRITRA");

		// s14, 18:00: the bosses (the vortex gone), the third hour's rifts; the invasion's own end timer finds nothing to stop.
		marks = Marks(sessions);
		logMark = fixture.LogCapture.Entries.Count;
		foreach (SimulationL0Session session in sessions) session.BeginStep("s14", "world-raid-bosses");
		await AdvanceSiegeWorldToAsync(sessions, at1800.AddSeconds(1), token);
		await SyncSiegeSessionsAsync(sessions, token);
		var bosses = new Dictionary<int, Npc>();
		foreach (WorldRaidLocation raid in raids)
		{
			Assert.Empty(RaidNpcs(raid, WorldRaidVortex));
			int bossId = Assert.Single(raid.GetNpcPool()).GetNpcId();
			Npc boss = Assert.Single(RaidNpcs(raid, bossId));
			Assert.Equal((raid.GetX(), raid.GetY(), raid.GetZ(), raid.GetH()), (boss.GetX(), boss.GetY(), boss.GetZ(), boss.GetHeading()));
			Assert.Equal("world_raid_aggressive", boss.GetAi().GetName());
			bosses[raid.GetLocationId()] = boss;
		}
		AssertRaidMessage(raider, marks[4], "STR_MSG_WORLDRAID_MESSAGE_04");
		AssertRaidMessage(watcher, marks[5], "STR_MSG_WORLDRAID_MESSAGE_04");
		Assert.False(VortexService.GetInstance().IsInvasionInProgress(BrusthoninInvasion));
		Assert.Equal(VortexGroup(BrusthoninInvasion, VortexStateType.PEACE), vortex.GetSpawned().Select(npc => ((Npc)npc).GetNpcId()).Order());
		AssertHourlyRifts(logMark);

		// s15: the raider kills the silent Altgard raid's boss; its death message goes to the whole map, and that raid ends.
		raider.BeginStep("s15", "kill-a-world-raid-boss");
		Npc altgardBoss = bosses[altgardSilent.GetLocationId()];
		await TeleportForSetupAsync(raider, raiderPlayer, Altgard, altgardBoss.GetX() - 1.5f, altgardBoss.GetY(), altgardBoss.GetZ(), token);
		altgardBoss.GetLifeStats().SetCurrentHp(RiftTargetHealth);
		marks = Marks(sessions);
		await StrikeUntilDeadAsync(sessions, altgardBoss, [(raider, raiderPlayer)], token);
		await AdvanceSiegeWorldToAsync(sessions, SystemClock.UtcNow().AddSeconds(1), token);
		await SyncSiegeSessionsAsync(sessions, token);
		Assert.False(WorldRaidService.GetInstance().IsWorldRaidInProgress(altgardSilent.GetLocationId()));
		Assert.True(WorldRaidService.GetInstance().IsWorldRaidInProgress(altgardSpeaker.GetLocationId()));
		Assert.Empty(RaidNpcs(altgardSilent, WorldRaidFlag));
		Assert.Empty(RaidNpcs(altgardSilent, WorldRaidMarker));
		// WorldRaidAI.handleAttack spawned its advent effect (702549) at the first blow; handleDied took it away.
		Assert.Contains(raider.PacketHistory.Skip(marks[4]), packet => packet.PacketType == typeof(SM_NPC_INFO) && packet.Get<int>("npcId") == WorldRaidAdventEffect);
		Assert.Empty(RaidNpcs(altgardSilent, WorldRaidAdventEffect));
		int deathMessage = Assert.Single(altgardSilent.GetNpcPool()).GetDeathMsgId()!.Value;
		Assert.Single(raider.PacketHistory.Skip(marks[4]), packet => packet.PacketType == typeof(SM_SYSTEM_MESSAGE) && packet.Get<int>("msgId") == deathMessage);
		Assert.DoesNotContain(watcher.PacketHistory.Skip(marks[5]), packet => packet.PacketType == typeof(SM_SYSTEM_MESSAGE) && packet.Get<int>("msgId") == deathMessage);

		// s16, 19:00: the other raids' hour ends (scheduleBossDespawn): flag, boss and markers gone, no message.
		marks = Marks(sessions);
		foreach (SimulationL0Session session in sessions) session.BeginStep("s16", "world-raids-end");
		await AdvanceSiegeWorldToAsync(sessions, at1900.AddSeconds(1), token);
		await SyncSiegeSessionsAsync(sessions, token);
		Assert.Empty(WorldRaidService.GetInstance().GetActiveWorldRaidLocations());
		foreach (WorldRaidLocation raid in raids.Where(raid => raid != altgardSilent))
		{
			Assert.False(bosses[raid.GetLocationId()].IsSpawned());
			Assert.Empty(RaidNpcs(raid, WorldRaidFlag));
			Assert.Empty(RaidNpcs(raid, WorldRaidMarker));
			Assert.Empty(RaidNpcs(raid, Assert.Single(raid.GetNpcPool()).GetNpcId()));
		}
		foreach (SimulationL0Session session in new[] { raider, watcher })
			Assert.DoesNotContain(session.PacketHistory.Skip(session == raider ? marks[4] : marks[5]), packet => packet.PacketType == typeof(SM_SYSTEM_MESSAGE)
				&& raids.Any(raid => raid.GetNpcPool().Any(npc => npc.GetDeathMsgId() == packet.Get<int>("msgId"))));

		await PlayTheobomosInvasionAsync(traveller, invader, crosser, defender, token);
		policy.AssertClean();
	}

	/// <summary>
	/// Sunday 2026-10-04, <c>gameserver.vortex.theobomos.schedule</c> (Sunday 16:00): the vortex of location 0 opens for two hours.
	/// The Asmodians invade Theobomos from Marchutan Priory and the Elyos defend it, and this time nobody destroys the generator:
	/// the end timer of <c>VortexService.startInvasion</c> stops the invasion at 18:00. Everyone logs out for the night before.
	/// </summary>
	private async Task PlayTheobomosInvasionAsync(SimulationL0Session traveller, SimulationL0Session invader, SimulationL0Session crosser,
		SimulationL0Session defender, CancellationToken token)
	{
		DateTimeOffset sunday = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);
		Assert.Equal(DayOfWeek.Sunday, sunday.DayOfWeek);
		foreach (SimulationL0Session session in riftCast)
		{
			session.BeginStep("s17", "log-out-for-the-night");
			await session.QuitAsync(token);
		}
		DateTimeOffset evening = sunday.AddHours(15).AddMinutes(50);
		while (SystemClock.UtcNow() < evening)
			fixture.Clock.Advance(evening - SystemClock.UtcNow() > TimeSpan.FromMinutes(10) ? TimeSpan.FromMinutes(10) : evening - SystemClock.UtcNow());

		// s18, 15:50: the two Elyos wait inside the invasion zones of Theobomos, the two Asmodians beside the vortex spot of
		// Marchutan Priory.
		SimulationL0Session[] cast = [traveller, invader, crosser, defender];
		foreach ((SimulationL0Session session, string name) in new[] { (traveller, "Aesimrifta"), (invader, "Aesimriftb"), (crosser, "Asimriftc"), (defender, "Asimriftd") })
		{
			session.BeginStep("s18", "log-in-for-the-theobomos-invasion");
			int id = session.CharacterId;
			await session.LoginCharacterListAsync(token);
			session.SelectCharacter(id, name);
			await session.EnterWorldAsync(token);
		}
		riftCast = cast;
		Player travellerPlayer = fixture.World.GetPlayer(traveller.CharacterId), invaderPlayer = fixture.World.GetPlayer(invader.CharacterId),
			crosserPlayer = fixture.World.GetPlayer(crosser.CharacterId), defenderPlayer = fixture.World.GetPlayer(defender.CharacterId);
		VortexLocation vortex = VortexService.GetInstance().GetLocationByWorld(Theobomos);
		Assert.Equal(TheobomosInvasion, vortex.GetId());
		Assert.Equal((Race.ELYOS, Race.ASMODIANS, MarchutanPriory, Theobomos), (vortex.GetDefendersRace(), vortex.GetInvadersRace(), vortex.GetHomeWorldId(), vortex.GetInvasionWorldId()));
		var (flameX, flameY) = (818.9f, 1998.7f); // inside FREGIONS_FLAME_210060000, the zone of the start point, 100 m from any spawn of the map
		await TeleportForSetupAsync(traveller, travellerPlayer, Theobomos, flameX, flameY, Ground(Theobomos, flameX, flameY, 130, 30), token);
		await TeleportForSetupAsync(invader, invaderPlayer, Theobomos, flameX + 3, flameY, Ground(Theobomos, flameX + 3, flameY, 130, 30), token);
		await TeleportForSetupAsync(crosser, crosserPlayer, MarchutanPriory, 560f, 211f, 93.5f, token);
		await TeleportForSetupAsync(defender, defenderPlayer, MarchutanPriory, 557f, 211f, 93.5f, token);
		await AdvanceSiegeWorldToAsync(cast, SystemClock.UtcNow().AddSeconds(2), token);
		await SyncSiegeSessionsAsync(cast, token);
		Assert.False(vortex.IsActive());
		Assert.Equal(VortexGroup(TheobomosInvasion, VortexStateType.PEACE), vortex.GetSpawned().Select(npc => ((Npc)npc).GetNpcId()).Order());
		Assert.Equal(new[] { traveller.CharacterId, invader.CharacterId }.Order(), vortex.GetPlayers().Keys.Order());

		// s19, 16:00: the Theobomos invasion starts; the two Elyos inside are asked to defend and make the defence alliance.
		int[] marks = Marks(cast);
		int logMark = fixture.LogCapture.Entries.Count;
		foreach (SimulationL0Session session in cast) session.BeginStep("s19", "theobomos-invasion-starts");
		await AdvanceSiegeWorldToAsync(cast, sunday.AddHours(16).AddSeconds(1), token);
		await SyncSiegeSessionsAsync(cast, token);
		Assert.True(VortexService.GetInstance().IsInvasionInProgress(TheobomosInvasion));
		Invasion invasion = Assert.IsType<Invasion>(vortex.GetActiveVortex());
		RVController gate = Assert.IsType<RVController>(vortex.GetVortexController());
		Assert.Same(RiftEnum.MARCHUTAN_AM, gate.GetRiftTemplate());
		Assert.Equal((MarchutanVortexRift, MarchutanPriory), (gate.GetOwner().GetNpcId(), gate.GetOwner().GetWorldId()));
		Assert.Equal((TheobomosVortexExit, Theobomos), (gate.GetSlave().GetNpcId(), gate.GetSlave().GetWorldId()));
		Assert.Equal(VortexGroup(TheobomosInvasion, VortexStateType.INVASION).Append(MarchutanVortexRift).Append(TheobomosVortexExit).Order(),
			vortex.GetSpawned().Select(npc => ((Npc)npc).GetNpcId()).Order());
		Assert.Single(vortex.GetSpawned(), npc => ((Npc)npc).GetNpcId() == TheobomosGenerator);
		Assert.Contains("Rift opened: MARCHUTAN_AM, spawned 0 Npcs (guards=false).", LogMessages(logMark));
		AssertRiftBatch(crosser, marks[2], MarchutanPriory);
		AssertRiftBatch(traveller, marks[0], Theobomos);
		foreach (SimulationL0Session elyos in new[] { traveller, invader })
		{
			elyos.BeginStep("s19", "accept-the-defence");
			Assert.Single(elyos.PacketHistory.Skip(elyos == traveller ? marks[0] : marks[1]), packet => IsQuestion(packet, VortexDefendQuestion));
			await elyos.SendPacketAsync(elyos.Api.Answer(1), token);
			await elyos.SynchronizeAsync(token);
		}
		Assert.Equal(new[] { traveller.CharacterId, invader.CharacterId }.Order(), invasion.GetDefenders().Keys.Order());
		PlayerAlliance defence = Assert.IsType<PlayerAlliance>(travellerPlayer.GetPlayerAlliance());
		Assert.Same(defence, invaderPlayer.GetPlayerAlliance());
		Assert.Equal((TeamType.ALLIANCE_DEFENCE, traveller.CharacterId), (defence.GetTeamType(), defence.GetLeaderObject().GetObjectId()));

		// s20: the two Asmodians invade Theobomos by the vortex rift of Marchutan Priory and make the offence alliance.
		crosser.BeginStep("s20", "invade-theobomos");
		await InvadeAsync(crosser, crosserPlayer, gate, traveller, 1, token);
		defender.BeginStep("s20", "invade-theobomos");
		await InvadeAsync(defender, defenderPlayer, gate, traveller, 2, token);
		Assert.Equal(new[] { crosser.CharacterId, defender.CharacterId }.Order(), invasion.GetInvaders().Keys.Order());
		PlayerAlliance offence = Assert.IsType<PlayerAlliance>(crosserPlayer.GetPlayerAlliance());
		Assert.Same(offence, defenderPlayer.GetPlayerAlliance());
		Assert.Equal((TeamType.ALLIANCE_OFFENCE, crosser.CharacterId), (offence.GetTeamType(), offence.GetLeaderObject().GetObjectId()));

		// s21, 18:00: the end timer stops the invasion (gameserver.vortex.duration 2); the invaders go home, the defenders stay.
		marks = Marks(cast);
		foreach (SimulationL0Session session in cast) session.BeginStep("s21", "theobomos-invasion-ends");
		await AdvanceSiegeWorldToAsync(cast, sunday.AddHours(18).AddSeconds(1), token);
		await SyncSiegeSessionsAsync(cast, token);
		AssertInvasionEnded(vortex, invasion, gate, [(crosser, crosserPlayer, marks[2]), (defender, defenderPlayer, marks[3])], offence, defence,
			[travellerPlayer, invaderPlayer]);
		Assert.Contains(RiftAnnounces(traveller, marks[0]), announce => announce == new RiftAnnounce(4, [], gate.GetSlave().GetObjectId(), 0, 0, 0, 0, 0, 0, 0, 0, 0));
	}

	/// <summary>
	/// Invasion.stopInvasion, by the generator's death or by the end timer: the location inactive with no controller, its PEACE
	/// group back and its vortex rift gone; kickPlayer tells each invader and sends him to the home point, the first one kicked
	/// leaving the offence alliance with 1401452, which disbands it, so the second hears nothing of it; the defenders and their
	/// alliance are not touched, as in Java.
	/// </summary>
	private static void AssertInvasionEnded(VortexLocation vortex, Invasion invasion, RVController gate,
		(SimulationL0Session Session, Player Player, int Mark)[] invaders, PlayerAlliance offence, PlayerAlliance defence, Player[] defenders)
	{
		Assert.False(VortexService.GetInstance().IsInvasionInProgress(vortex.GetId()));
		Assert.True(invasion.IsFinished());
		Assert.False(vortex.IsActive());
		Assert.Null(vortex.GetActiveVortex());
		Assert.Null(vortex.GetVortexController());
		Assert.Empty(invasion.GetInvaders());
		Assert.Equal(VortexGroup(vortex.GetId(), VortexStateType.PEACE), vortex.GetSpawned().Select(npc => ((Npc)npc).GetNpcId()).Order());
		Assert.False(gate.GetOwner().IsSpawned());
		Assert.False(gate.GetSlave().IsSpawned());
		Assert.Empty(RiftManager.GetSpawnedRifts(vortex.GetHomeWorldId()));
		Assert.Empty(gate.GetPassedPlayers());
		Assert.Equal(0, gate.GetUsedEntries());
		var home = vortex.GetHomePoint();
		int allianceNotices = 0;
		foreach ((SimulationL0Session session, Player player, int mark) in invaders)
		{
			DecodedBotServerPacket[] since = session.PacketHistory.Skip(mark).ToArray();
			allianceNotices += since.Count(packet => packet.PacketType == typeof(SM_SYSTEM_MESSAGE) && packet.Get<int>("msgId") == 1401452);
			Assert.Single(since, packet => IsSystemMessage(packet, "STR_MSG_INVADE_DIRECT_PORTAL_OUT_COMPULSION"));
			Assert.Contains(since, packet => packet.PacketType == typeof(SM_PLAYER_SPAWN) && packet.Get<int>("worldId") == vortex.GetHomeWorldId());
			Assert.Equal((vortex.GetHomeWorldId(), home.GetX(), home.GetY(), home.GetZ()), (player.GetWorldId(), player.GetX(), player.GetY(), player.GetZ()));
			Assert.False(player.IsInTeam());
			session.AcceptTeleportPosition();
		}
		Assert.Equal(1, allianceNotices);
		Assert.True(offence.IsDisbanded());
		Assert.False(defence.IsDisbanded());
		Assert.All(defenders, player => Assert.Same(defence, player.GetPlayerAlliance()));
		Assert.Equal(defenders.Select(player => player.GetObjectId()).Order(), invasion.GetDefenders().Keys.Order());
	}

	/// <summary>
	/// The rifts that opened since <paramref name="logMark"/> by rift_schedule.xml's hourly jobs (<c>prepareRiftOpening(world,
	/// false)</c>): in each map only locations without spawns that close by themselves, none to its maximum, at least one in
	/// Cygnea and Enshar, which skip the coin toss; each with its rift and exit in every instance and Java's log line.
	/// </summary>
	private RiftLocation[] AssertHourlyRifts(int logMark, params RiftLocation[] excluded)
	{
		RiftLocation[] opened = RiftService.GetInstance().GetRiftLocations().Values.Where(location => location.IsOpened() && !excluded.Contains(location)).ToArray();
		foreach ((int world, int maxCount, bool always) in HourlyRiftMaps)
		{
			RiftLocation[] open = opened.Where(location => location.GetWorldId() == world).ToArray();
			Assert.InRange(open.Length, always ? 1 : 0, maxCount);
			Assert.All(open, location => Assert.True(!location.HasSpawns() && location.IsAutoCloseable(), $"rift {location.GetId()} has spawns or stays open"));
		}
		Assert.All(opened, location => Assert.Contains(HourlyRiftMaps, map => map.World == location.GetWorldId()));
		string[] log = LogMessages(logMark);
		foreach (RiftLocation location in opened)
		{
			RiftEnum riftEnum = RiftEnum.GetRift(location.GetId());
			int instances = fixture.World.GetWorldMap(location.GetWorldId()).GetInstanceCount();
			Assert.Equal(2 * instances, location.GetSpawned().Count);
			Npc[] masters = RiftManager.GetSpawnedRifts(location.GetWorldId()).Where(npc => ((RVController)npc.GetController()).IsMaster()
				&& ((RVController)npc.GetController()).GetRiftTemplate() == riftEnum).ToArray();
			Assert.Equal(instances, masters.Length);
			Assert.All(masters, master =>
			{
				var controller = (RVController)master.GetController();
				Assert.False(controller.IsVortex() || controller.IsVolatile() || controller.IsInvasion());
				Assert.Equal((riftEnum.GetEntries(), riftEnum.GetMinLevel(), riftEnum.GetMaxLevel(), 0),
					(controller.GetMaxEntries()!.Value, controller.GetMinLevel()!.Value, controller.GetMaxLevel()!.Value, controller.GetUsedEntries()));
				Assert.Contains(location.GetSpawned().Keys, objectId => objectId == master.GetObjectId());
				Assert.Contains(location.GetSpawned().Keys, objectId => objectId == controller.GetSlave().GetObjectId());
				Assert.NotEqual(location.GetWorldId(), controller.GetSlave().GetWorldId());
			});
			Assert.Single(log, line => line == $"Rift opened: {riftEnum.Name()}, spawned {2 * (instances - 1)} Npcs (guards=false).");
		}
		Assert.Equal(opened.Length, log.Count(line => line.StartsWith("Rift opened: ", StringComparison.Ordinal) && line.EndsWith("(guards=false).", StringComparison.Ordinal)
			&& !line.StartsWith("Rift opened: KAISINEL_AM", StringComparison.Ordinal) && !line.StartsWith("Rift opened: MARCHUTAN_AM", StringComparison.Ordinal)));
		return opened;
	}

	/// <summary>
	/// The last announce batch a player in <paramref name="world"/> got since <paramref name="mark"/>
	/// (<c>RiftInformer.getPackets(world)</c>): the twelve counts, then for each master rift of the world its master (2) and
	/// slave (3) entries.
	/// </summary>
	private void AssertRiftBatch(SimulationL0Session session, int mark, int world)
	{
		// Silentera's state (1) and despawns (4) are sent on their own; a batch is the counts and the entries.
		RiftAnnounce[] announces = RiftAnnounces(session, mark).Where(announce => announce.Action is 0 or 2 or 3).ToArray();
		Npc[] masters = RiftManager.GetSpawnedRifts(world).Where(npc => ((RVController)npc.GetController()).IsMaster()).ToArray();
		int last = Array.FindLastIndex(announces, announce => announce.Action == 0);
		Assert.True(last >= 0, $"{session.CurrentStep}: no rift announce for {world}");
		int[] counts = new int[12];
		foreach (Npc master in masters)
		{
			var controller = (RVController)master.GetController();
			string name = controller.GetRiftTemplate().Name();
			if (name.StartsWith("KAISINEL", StringComparison.Ordinal) || name.StartsWith("MARCHUTAN", StringComparison.Ordinal))
				counts[1]++;
			else if (name.Contains("_VIL", StringComparison.Ordinal))
				continue;
			else
				counts[controller.IsVolatile() ? 4 : 0]++;
		}
		Assert.Equal(counts, announces[last].Counts);
		RiftAnnounce[] entries = announces.Skip(last + 1).Take(2 * masters.Length).ToArray();
		Assert.Equal(2 * masters.Length, entries.Length);
		for (int i = 0; i < masters.Length; i++)
		{
			var controller = (RVController)masters[i].GetController();
			int type = controller.IsVortex() ? 1 : controller.IsVolatile() ? 4 : controller.IsInvasion() ? 5 : 0;
			int remain = entries[2 * i].RemainTime;
			Assert.InRange(controller.GetRemainTime() - remain, -2, 0);
			Assert.Equal(new RiftAnnounce(2, [], masters[i].GetObjectId(), controller.GetMaxEntries()!.Value, remain, controller.GetMinLevel()!.Value,
				controller.GetMaxLevel()!.Value, masters[i].GetX(), masters[i].GetY(), masters[i].GetZ(), type, 1), entries[2 * i]);
			Assert.Equal(new RiftAnnounce(3, [], masters[i].GetObjectId(), controller.GetUsedEntries(), entries[2 * i + 1].RemainTime, 0, 0, 0, 0, 0, type, 0),
				entries[2 * i + 1]);
			Assert.InRange(controller.GetRemainTime() - entries[2 * i + 1].RemainTime, -2, 0);
		}
	}

	private static RiftAnnounce LastRiftBatch(RiftAnnounce[] announces) => announces.Last(announce => announce.Action == 0);

	/// <summary>RVController.onDespawn: a player in <paramref name="world"/> is told of every rift NPC of that world that closed.</summary>
	private void AssertRiftDespawns(SimulationL0Session session, int mark, Dictionary<int, int> closed, int world)
	{
		int[] despawned = RiftAnnounces(session, mark).Where(announce => announce.Action == 4).Select(announce => announce.ObjectId).Order().ToArray();
		int[] expected = closed.Where(npc => npc.Value == world).Select(npc => npc.Key).Order().ToArray();
		Assert.NotEmpty(expected);
		Assert.Equal(expected, despawned);
	}

	/// <summary>Each rift NPC of <paramref name="locations"/> and its world, taken while they stand.</summary>
	private Dictionary<int, int> RiftNpcWorlds(IEnumerable<RiftLocation> locations) => locations.SelectMany(location => location.GetSpawned().Keys)
		.Select(objectId => Assert.IsAssignableFrom<Npc>(fixture.World.FindVisibleObject(objectId))).Where(npc => npc.GetController() is RVController)
		.ToDictionary(npc => npc.GetObjectId(), npc => npc.GetWorldId());

	/// <summary>The scenario's subjects, every one drained after each step of world time (harness rule 7).</summary>
	private SimulationL0Session[] riftCast = [];

	/// <summary>Talks to a rift it stands beside and accepts its question (STR_ASK_PASS_BY_DIRECT_PORTAL, or the vortex's 904304).</summary>
	private static async Task AnswerRiftQuestionAsync(SimulationL0Session session, Npc rift, int question, CancellationToken token)
	{
		if (!session.Api.World.Objects.ContainsKey(rift.GetObjectId()))
			await session.WaitForPacketAsync(typeof(SM_NPC_INFO), token, packet => packet.Get<int>("objectId") == rift.GetObjectId());
		await session.SendPacketAsync(session.Api.TalkTo(rift.GetObjectId()), token);
		DecodedBotServerPacket ask = await session.WaitForPacketAsync(typeof(SM_QUESTION_WINDOW), token, packet => packet.Get<int>("code") == question);
		// RVController.onRequest: the rift's question has no sender and no range, the vortex's names the rift and 5 m.
		Assert.Equal(question == VortexPassQuestion ? (rift.GetObjectId(), 5) : (0, 0), (ask.Get<int>("senderId"), ask.Get<int>("rangeOrCooldownSeconds")));
		await session.SendPacketAsync(session.Api.Answer(1), token);
	}

	/// <summary>
	/// A crossing by one of <paramref name="from"/>'s open rifts (RVController's accept): the player lands at the rift's exit
	/// in <paramref name="to"/>, the rift counts the entry and every player of both worlds hears it (an observer in
	/// <paramref name="to"/>), and on arrival the player is told of <paramref name="to"/>'s rifts.
	/// </summary>
	private async Task CrossByRiftAsync(SimulationL0Session session, Player player, int from, int to, SimulationL0Session observer, CancellationToken token)
	{
		Npc rift = RiftManager.GetSpawnedRifts(from).First(npc => ((RVController)npc.GetController()).IsMaster());
		var controller = (RVController)rift.GetController();
		Npc exit = controller.GetSlave();
		Assert.Equal(to, exit.GetWorldId());
		Assert.InRange(player.GetLevel(), controller.GetMinLevel()!.Value, controller.GetMaxLevel()!.Value);
		await TeleportForSetupAsync(session, player, from, rift.GetX() + 2, rift.GetY(), rift.GetZ(), token);
		int observerMark = observer.PacketHistory.Count;
		await AnswerRiftQuestionAsync(session, rift, SM_QUESTION_WINDOW.STR_ASK_PASS_BY_DIRECT_PORTAL, token);
		await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token, packet => packet.Get<int>("worldId") == to);
		int arrival = session.PacketHistory.Count;
		await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
		await observer.SynchronizeAsync(token);
		SpawnTemplate landing = Assert.IsAssignableFrom<SpawnTemplate>(exit.GetSpawn());
		Assert.Equal((to, 1, landing.GetX(), landing.GetY(), landing.GetZ()), (player.GetWorldId(), player.GetInstanceId(), player.GetX(), player.GetY(), player.GetZ()));
		Assert.Equal(1, controller.GetUsedEntries());
		Assert.Contains(RiftAnnounces(observer, observerMark), announce => announce.Action == 3 && announce.ObjectId == rift.GetObjectId() && announce.Entries == 1);
		AssertRiftBatch(session, arrival - 1, to);
	}

	/// <summary>
	/// An entry by the vortex rift (RVController's vortex accept): the player lands at the invasion's start point, is told the
	/// portal is open, counts as passed and, inside the invasion zones, as an invader; the defender in Brusthonin hears the count.
	/// </summary>
	private async Task InvadeAsync(SimulationL0Session session, Player player, RVController gate, SimulationL0Session observer, int passed, CancellationToken token)
	{
		VortexLocation vortex = VortexService.GetInstance().GetLocationByRift(gate.GetOwner().GetNpcId());
		int observerMark = observer.PacketHistory.Count;
		int mark = session.PacketHistory.Count;
		await AnswerRiftQuestionAsync(session, gate.GetOwner(), VortexPassQuestion, token);
		await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token, packet => packet.Get<int>("worldId") == vortex.GetInvasionWorldId());
		await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
		session.AcceptTeleportPosition();
		await AdvanceSiegeWorldToAsync(riftCast, SystemClock.UtcNow().AddSeconds(1), token);
		await session.SynchronizeAsync(token);
		await observer.SynchronizeAsync(token);
		var start = vortex.GetStartPoint();
		Assert.Equal((start.GetMapId(), 1, start.GetX(), start.GetY(), start.GetZ()), (player.GetWorldId(), player.GetInstanceId(), player.GetX(), player.GetY(), player.GetZ()));
		Assert.Single(session.PacketHistory.Skip(mark), packet => IsSystemMessage(packet, "STR_MSG_INVADE_DIRECT_PORTAL_OPEN_NOTICE"));
		Assert.True(gate.GetPassedPlayers().ContainsKey(player.GetObjectId()));
		Assert.Equal(passed, gate.GetUsedEntries());
		Assert.True(vortex.IsInsideLocation(player));
		Assert.True(vortex.IsInvaderInside(player.GetObjectId()));
		Assert.Contains(RiftAnnounces(observer, observerMark), announce => announce.Action == 3 && announce.ObjectId == gate.GetOwner().GetObjectId()
			&& announce.Entries == passed && announce.Type == 1);
	}

	/// <summary>Real auto-attacks of <paramref name="attackers"/> on <paramref name="target"/> until it dies.</summary>
	private static async Task StrikeUntilDeadAsync(SimulationL0Session[] sessions, Npc target, (SimulationL0Session Session, Player Player)[] attackers, CancellationToken token)
	{
		int swings = 0;
		foreach ((SimulationL0Session session, _) in attackers)
		{
			if (!session.Api.World.Objects.ContainsKey(target.GetObjectId()))
				await session.WaitForPacketAsync(typeof(SM_NPC_INFO), token, packet => packet.Get<int>("objectId") == target.GetObjectId());
			await session.SendPacketAsync(session.Api.Target(target.GetObjectId()), token);
		}
		for (int round = 0; round < 40 && !target.IsDead(); round++)
		{
			foreach ((SimulationL0Session session, Player player) in attackers)
			{
				int speed = player.GetGameStats().GetAttackSpeed().GetCurrent();
				if (!target.IsDead() && !player.IsDead() && session.Api.Timing.TimeUntilAttack(speed) <= TimeSpan.Zero)
				{
					await session.SendPacketAsync(session.Api.Attack(target.GetObjectId(), speed), token);
					swings++;
				}
			}
			await AdvanceSiegeWorldToAsync(sessions, SystemClock.UtcNow().AddMilliseconds(500), token);
		}
		if (!target.IsDead())
			Assert.Fail($"{target.GetNpcId()} survived with {target.GetLifeStats().GetCurrentHp()} hp after {swings} swings; " + string.Join("; ", attackers.Select(attacker =>
				$"{attacker.Player.GetName()} {PositionUtil.GetDistance(attacker.Player, target):F2} m away, hp {attacker.Player.GetLifeStats().GetCurrentHp()}, " +
				$"enemy {attacker.Player.IsEnemy(target)}, sees it {Aion.GameServer.World.Geo.GeoService.GetInstance().CanSee(attacker.Player, target)}")));
		Assert.All(attackers, attacker => Assert.False(attacker.Player.IsDead(), $"{attacker.Player.GetName()} died fighting {target.GetNpcId()}"));
	}

	/// <summary>The NPC ids of an invasion location's group for <paramref name="state"/>, one per spot.</summary>
	private static IEnumerable<int> VortexGroup(int locationId, VortexStateType state) =>
		(DataManager.SPAWNS_DATA.GetVortexSpawnsByLocId(locationId) ?? []).SelectMany(group => group.GetSpawnTemplates())
			.OfType<VortexSpawnTemplate>().Where(spawn => spawn.GetStateType() == state).Select(spawn => spawn.GetNpcId()).Order();

	private List<Npc> RaidNpcs(WorldRaidLocation raid, int npcId) =>
		fixture.World.GetWorldMap(raid.GetMapId()).GetMainWorldMapInstance().GetNpcs(npcId)
			.Where(npc => npc.IsSpawned() && MathF.Abs(npc.GetX() - raid.GetX()) < 60 && MathF.Abs(npc.GetY() - raid.GetY()) < 60).ToList();

	/// <summary>A world raid message reaches a player of its map once, from the first raid of the map only.</summary>
	private static void AssertRaidMessage(SimulationL0Session session, int mark, string name) =>
		Assert.Single(session.PacketHistory.Skip(mark), packet => IsSystemMessage(packet, name));

	private static bool IsQuestion(DecodedBotServerPacket packet, int code) =>
		packet.PacketType == typeof(SM_QUESTION_WINDOW) && packet.Get<int>("code") == code;

	private string[] LogMessages(int mark) => fixture.LogCapture.Entries.Skip(mark).Select(entry => entry.Message).ToArray();

	private static float Ground(int world, float x, float y, float top, float bottom)
	{
		float z = Aion.GameServer.World.Geo.GeoService.GetInstance().GetZ(world, x, y, top, bottom, 1);
		Assert.False(float.IsNaN(z), $"no ground at {world} ({x}, {y}) between {bottom} and {top}");
		return z;
	}

	private static RiftAnnounce[] RiftAnnounces(SimulationL0Session session, int mark) =>
		session.PacketHistory.Skip(mark).Where(packet => packet.PacketType == typeof(SM_RIFT_ANNOUNCE)).Select(ReadRiftAnnounce).ToArray();

	/// <summary>
	/// SM_RIFT_ANNOUNCE: H following length, C action; 0 the twelve counts (D each), 1 Silentera (D Gelkmaros, D Inggison),
	/// 2 a master rift (D object, D max entries, D remaining seconds, D min level, D max level, F x, F y, F z, C type, C shown),
	/// 3 its entries (D object, D used entries, D remaining seconds, C type, C 0), 4 a despawn (D object).
	/// </summary>
	private static RiftAnnounce ReadRiftAnnounce(DecodedBotServerPacket packet)
	{
		byte[] raw = SiegeBody(packet);
		var body = new SiegeBodyReader(raw);
		int length = body.H();
		Assert.Equal(raw.Length - 2, length);
		int action = body.C();
		RiftAnnounce announce = action switch
		{
			0 => new RiftAnnounce(0, Enumerable.Range(0, (length - 1) / 4).Select(_ => body.D()).ToArray(), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
			1 => new RiftAnnounce(1, [body.D(), body.D()], 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
			2 => new RiftAnnounce(2, [], body.D(), body.D(), body.D(), body.D(), body.D(), body.F(), body.F(), body.F(), body.C(), body.C()),
			3 => new RiftAnnounce(3, [], body.D(), body.D(), body.D(), 0, 0, 0, 0, 0, body.C(), body.C()),
			4 => new RiftAnnounce(4, [], body.D(), 0, 0, 0, 0, 0, 0, 0, 0, 0),
			_ => throw new InvalidDataException($"SM_RIFT_ANNOUNCE action {action}"),
		};
		body.End();
		return announce;
	}

	/// <summary>One SM_RIFT_ANNOUNCE; <see cref="Counts"/> is empty (the same instance) except for actions 0 and 1.</summary>
	private sealed record RiftAnnounce(int Action, int[] Counts, int ObjectId, int Entries, int RemainTime, int MinLevel, int MaxLevel,
		float X, float Y, float Z, int Type, int Flag);
}
