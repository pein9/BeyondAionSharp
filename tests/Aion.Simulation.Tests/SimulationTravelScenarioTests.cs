using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Templates.Flypath;
using Aion.GameServer.Model.Templates.Teleport;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Trade;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Time.Gametime;
using Aion.GameServer.World;

namespace Aion.Simulation.Tests;

/// <summary>
/// P11-07, scenario TRAVEL: flight transporters, teleporters and day/night spawns, played by an ordinary Elyos and an ordinary
/// Asmodian in a process of its own (<c>reset-TRAVEL</c>, the shared epoch), every expected value from the Java code:
/// <c>DialogService</c> (AIRLINE_SERVICE), <c>TeleportService</c>, <c>CM_TELEPORT_SELECT</c>, <c>SM_TELEPORT_MAP</c>,
/// <c>SM_TELEPORT_LOC</c>, <c>CM_TELEPORT_ANIMATION_DONE</c>, <c>CM_MOVE_IN_AIR</c>, <c>CM_EMOTION</c> (LAND_FLYTELEPORT),
/// <c>PlayerController.onFlyTeleportEnd</c>, <c>PricesService.getPriceForService</c>, <c>GameTime</c>,
/// <c>TemporarySpawnEngine</c>, <c>TemporarySpawn</c>, <c>SpawnEngine</c> and <c>RespawnService</c>.
/// <list type="bullet">
/// <item>Each starter village's teleporter (Daines 203194 in Poeta, Osmar 203679 in Ishalgen) shows a player who is not yet a
/// Daeva its NO_RIGHT page instead of the airline map.</item>
/// <item>Each starter village's flight transporter refuses a player without the fare, a route it does not fly (with Java's
/// audit line) and a player out of talk range, then flies the route: the fare, START_FLYTELEPORT with the route's teleport
/// id, the FLYING state and the flight path, the client's CM_MOVE_IN_AIR along the fly path for its time, and the landing
/// with LAND_FLYTELEPORT (state, flight path cleared). The other village's transporter flies the player back.</item>
/// <item>Its capital (Sanctum, Pandaemonium) needs the Ascension quest: a CM_TELEPORT_SELECT for it, which checks no Daeva
/// status, is refused for the quest before the fare. Made a Daeva, the player gets the teleporter's map and is teleported to
/// the second zone (Verteron, Altgard) with the fare and JUMP_IN; the Asmodian then flies on from Altgard Fortress to
/// Trader's Berth.</item>
/// <item>Across a night and a day of the game clock: Stone (203121, 04:00 to 21:00) beside Verteron Citadel's landing and
/// Disguised Germir (798041, the same hours) in Trader's Berth appear to the player who stands there at 04:00 and disappear
/// at 21:00, and stay the same objects in between; Verteron's two Enintes, one spot with a night monster (210743, 21:00 to
/// 04:00) and a day NPC (203161), change places, and the night one killed at 03:30 has its pending respawn cancelled at
/// 04:00 (<c>deleteIfAliveOrCancelRespawn</c>) and comes back only at 21:00; Morheim's Igraine and Virashak, whose spots
/// carry their own hours under their group's empty temporary spawn, move to their day spots at 05:00 and back at 21:00.</item>
/// </list>
/// <para>
/// Director setup, recorded here as the plan asks: each subject is placed by a setup teleport between its village's
/// transporter and teleporter, has the 1,000 Kinah every new character gets taken away before the fare refusal and is given
/// 10,000 after it, and is made a level-10 Gladiator (ClassChangeService with the Daeva status, which completes the
/// Ascension quest) before its second visit to the teleporter. The night Eninte is
/// killed by the GM's <c>//kill</c> (<c>Kill.kill</c>: <c>onAttack</c> with its maximum HP, the Elyos the attacker), so
/// that its respawn is pending when its hours end.
/// </para>
/// </summary>
public sealed partial class SimulationFastScenarioTests
{
	private const int Poeta = 210010000, Ishalgen = 220010000, Morheim = 220020000;
	private const int Daines = 203194, Kustanon = 203070, Aero = 203083; // Akarios Village's teleporter, transporters of Akarios and Melponeh's Campsite
	private const int Osmar = 203679, Sheofin = 203513, Garhara = 203545; // Ishalgen's teleporter, transporters of Aldelle Village and Anturoon Crossing
	private const int Lenat = 203561; // Altgard Fortress's transporter
	private const int Stone = 203121, DisguisedGermir = 798041, DayEninte = 203161, NightEninte = 210743, Igraine = 204347, Virashak = 204348;
	private const int TravelKinah = 10_000;

	/// <summary>Each race's route through the scenario: where it starts, its village's teleporter and transporters, the
	/// routes, and the second zone's teleport location.</summary>
	private sealed record TravelPlan(string Name, Race Race, int Village, float StandX, float StandY, float StandZ, int Teleporter,
		int OutTransporter, int OutLocation, int WrongLocation, int BackTransporter, int BackLocation, int CapitalLocation,
		int RequiredQuest, int ZoneLocation);

	private static readonly TravelPlan ElyosTravel = new("Aesimtravel", Race.ELYOS, Poeta, 805.6f, 1242.6f, 118.99f, Daines,
		Kustanon, 13, 12, Aero, 12, 2, 1006, 4);
	private static readonly TravelPlan AsmodianTravel = new("Asimtravel", Race.ASMODIANS, Ishalgen, 526.9f, 2449.6f, 281.6f, Osmar,
		Sheofin, 18, 17, Garhara, 17, 7, 2008, 9);

	private async Task RunTravelAsync(ScenarioDefinition scenario, bool includeHistory)
	{
		var sessions = new List<SimulationL0Session>();
		try
		{
			await PlayTravelAsync(scenario, includeHistory, sessions);
		}
		catch (Exception exception) when (exception is not SimulationLogPolicyException)
		{
			// Harness rule 2: the whole exception and every bot's recent packets.
			throw new InvalidOperationException("TRAVEL failed at game time " + GameTimeService.GetInstance().GetGameTime().GetTime() + "; recent packets:" +
				Environment.NewLine + string.Join(Environment.NewLine, sessions.Select(session => session.CurrentStep + " " + string.Join(" | ",
					session.PacketHistory.TakeLast(25).Select(packet => packet.PacketType.Name +
						(packet.PacketType == typeof(SM_SYSTEM_MESSAGE) ? "(" + (packet.Get<string?>("name") ?? packet.Get<int>("msgId").ToString()) + ")" : ""))))), exception);
		}
	}

	private async Task PlayTravelAsync(ScenarioDefinition scenario, bool includeHistory, List<SimulationL0Session> created)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory, new SimulationLogPolicyOptions { FailOnWarnings = true, FailOnProtocolWarnings = true });
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(30));
		CancellationToken token = timeout.Token;
		GameTime clock = GameTimeService.GetInstance().GetGameTime();
		int startedAt = clock.GetTime();
		// Flight transporters now validate their endpoint and elapsed duration (Java 332d54885).
		// FlyTransporterAsync advances the shared clock through the full route before landing.
		int auditMark = fixture.LogCapture.Entries.Count;

		await using var elyos = new SimulationL0Session(fixture, policy, "b01", 108, ElyosTravel.Name, Race.ELYOS);
		await using var asmodian = new SimulationL0Session(fixture, policy, "b02", 109, AsmodianTravel.Name, Race.ASMODIANS);
		created.AddRange([elyos, asmodian]);

		Player elyosPlayer = await TravelVillageAsync(elyos, ElyosTravel, [], token);
		Player asmodianPlayer = await TravelVillageAsync(asmodian, AsmodianTravel, [elyos], token);
		SimulationL0Session[] online = [elyos, asmodian];
		Assert.Equal(2, fixture.LogCapture.Entries.Skip(auditMark).Count(entry => entry.Category == "AUDIT_LOG"));

		// s09: the Asmodian flies on from Altgard Fortress, where his teleport put him beside Lenat, to Trader's Berth.
		asmodian.BeginStep("s09", "fly-altgard-fortress-to-traders-berth");
		await FlyTransporterAsync(asmodian, asmodianPlayer, Lenat, 20, token);
		await elyos.DrainServerPacketsAsync(token);

		// s10: each takes his place for the night and the day. Stone stands 30 m from Verteron Citadel's teleport point (in
		// sight, 95 m: VisibleObject.getVisibleDistance); Germir is 118 m from Trader's Berth's landing, so the Asmodian walks
		// 40 m towards him, 78 m from him and 33 m from the nearest aggressive monster (a blackened angolem, aggro range 13).
		elyos.BeginStep("s10", "wait-beside-verteron-citadel");
		asmodian.BeginStep("s10", "walk-towards-germir");
		Assert.Equal(Verteron, DataManager.TELELOCATION_DATA.GetTelelocationTemplate(ElyosTravel.ZoneLocation).GetMapId());
		Assert.Equal(Verteron, elyosPlayer.GetWorldId());
		Assert.Equal(Altgard, DataManager.TELELOCATION_DATA.GetTelelocationTemplate(AsmodianTravel.ZoneLocation).GetMapId());
		float waitX = 2650.1f, waitY = 1001.0f;
		await asmodian.MoveToPositionAsync(new BotPosition(waitX, waitY, Ground(Altgard, waitX, waitY, 330, 290), 0), token);
		await SyncSiegeSessionsAsync(online, token);
		Assert.InRange(PositionUtil.GetDistance(asmodianPlayer, 2588, 953, 306), 70, 90);

		// s11: the night. Whatever the game hour now, the next 03:30 finds the night spawns up: the night Eninte, and Igraine
		// and Virashak at their night spots (spawned at boot by isInSpawnTime, or at the 21:00 before).
		foreach (SimulationL0Session session in online) session.BeginStep("s11", "the-night-before-0400");
		await AdvanceTravelToGameTimeAsync(online, 3, 30, token);
		WorldMapInstance verteron = fixture.World.GetWorldMap(Verteron).GetMainWorldMapInstance();
		WorldMapInstance altgard = fixture.World.GetWorldMap(Altgard).GetMainWorldMapInstance();
		WorldMapInstance morheim = fixture.World.GetWorldMap(Morheim).GetMainWorldMapInstance();
		Assert.Empty(LiveNpcs(verteron, Stone));
		Assert.Empty(LiveNpcs(altgard, DisguisedGermir));
		Assert.Empty(LiveNpcs(verteron, DayEninte));
		Npc nightEninte = Assert.Single(LiveNpcs(verteron, NightEninte));
		AssertAt(nightEninte, 1977.17f, 2632.55f, 301.336f);
		AssertMorheimSpots(morheim, night: true);
		Assert.DoesNotContain(elyos.Api.World.Objects.Values, known => known.TemplateId == Stone);
		Assert.DoesNotContain(asmodian.Api.World.Objects.Values, known => known.TemplateId == DisguisedGermir);

		// The GM's //kill: Kill.kill hits it for its maximum HP. NpcController.onDie schedules its respawn (295 s) at once.
		nightEninte.GetController().OnAttack(elyosPlayer, null!, SmAttackStatus.TYPE.REGULAR, nightEninte.GetLifeStats().GetMaxHp(), true,
			SmAttackStatus.LOG.REGULAR, null, Aion.GameServer.SkillEngine.Model.HopType.DAMAGE);
		Assert.True(nightEninte.IsDead());
		Assert.True(RespawnService.HasRespawnTask(nightEninte));
		int respawnTime = Assert.IsType<Aion.GameServer.Model.Templates.Spawns.SpawnTemplate>(nightEninte.GetSpawn()).GetRespawnTime();
		Assert.Equal(295, respawnTime);
		long respawnDue = SystemClock.UtcNow().AddSeconds(respawnTime).ToUnixTimeMilliseconds();
		await AdvanceTravelToGameTimeAsync(online, 3, 59, token);
		bool corpseLies = fixture.World.FindVisibleObject(nightEninte.GetObjectId()) == nightEninte;

		// s12, 04:00: the day spawns come, the night ones go. TemporarySpawnEngine.despawn: the dead Eninte cannot be deleted,
		// so its respawn is cancelled and its corpse, if it still lies, is left to its decay; spawn: Stone, Germir and the day
		// Eninte, each sent to the players who see it.
		int[] marks = Marks(online);
		foreach (SimulationL0Session session in online) session.BeginStep("s12", "0400-day-spawns");
		await AdvanceTravelToGameTimeAsync(online, 4, 0, token);
		Assert.False(RespawnService.HasRespawnTask(nightEninte));
		Assert.Equal(corpseLies, fixture.World.FindVisibleObject(nightEninte.GetObjectId()) == nightEninte);
		Assert.Empty(LiveNpcs(verteron, NightEninte));
		AssertAt(Assert.Single(LiveNpcs(verteron, DayEninte)), 1977.17f, 2632.55f, 301.336f);
		Npc stone = Assert.Single(LiveNpcs(verteron, Stone));
		AssertAt(stone, 1670.8358f, 1496.6965f, 119.193f);
		Npc germir = Assert.Single(LiveNpcs(altgard, DisguisedGermir));
		AssertAt(germir, 2588f, 953f, 306f);
		AssertAppears(elyos, marks[0], stone);
		AssertAppears(asmodian, marks[1], germir);
		AssertMorheimSpots(morheim, night: true);

		// s13, 05:00: Igraine and Virashak go to their day spots. The night Eninte's respawn would have been due by now.
		foreach (SimulationL0Session session in online) session.BeginStep("s13", "0500-morheim-day-spots");
		await AdvanceTravelToGameTimeAsync(online, 5, 0, token);
		Assert.True(SystemClock.UtcNow().ToUnixTimeMilliseconds() > respawnDue);
		Assert.Empty(LiveNpcs(verteron, NightEninte));
		AssertMorheimSpots(morheim, night: false);

		// s14, the day: no other hour changes them. At 21:00 the day spawns go (SM_DELETE, FADE_OUT) and the night ones come.
		foreach (SimulationL0Session session in online) session.BeginStep("s14", "2100-night-spawns");
		await AdvanceTravelToGameTimeAsync(online, 20, 59, token);
		Assert.Same(stone, Assert.Single(LiveNpcs(verteron, Stone)));
		Assert.Same(germir, Assert.Single(LiveNpcs(altgard, DisguisedGermir)));
		Npc dayEninte = Assert.Single(LiveNpcs(verteron, DayEninte));
		Assert.Empty(LiveNpcs(verteron, NightEninte));
		Assert.DoesNotContain(elyos.PacketHistory.Skip(marks[0]), packet => packet.PacketType == typeof(SM_DELETE) && packet.Get<int>("objectId") == stone.GetObjectId());
		Assert.DoesNotContain(asmodian.PacketHistory.Skip(marks[1]), packet => packet.PacketType == typeof(SM_DELETE) && packet.Get<int>("objectId") == germir.GetObjectId());
		marks = Marks(online);
		await AdvanceTravelToGameTimeAsync(online, 21, 0, token);
		Assert.Empty(LiveNpcs(verteron, Stone));
		Assert.Empty(LiveNpcs(altgard, DisguisedGermir));
		Assert.False(stone.IsSpawned() || germir.IsSpawned() || dayEninte.IsSpawned());
		Assert.Empty(LiveNpcs(verteron, DayEninte));
		Npc nextNight = Assert.Single(LiveNpcs(verteron, NightEninte));
		Assert.NotEqual(nightEninte.GetObjectId(), nextNight.GetObjectId());
		AssertAt(nextNight, 1977.17f, 2632.55f, 301.336f);
		AssertMorheimSpots(morheim, night: true);
		AssertDisappears(elyos, marks[0], stone);
		AssertDisappears(asmodian, marks[1], germir);

		// s15: both log out where they stand, and the database has them there.
		foreach (SimulationL0Session session in online)
		{
			session.BeginStep("s15", "quit-and-check-the-saved-position");
			await session.QuitAsync(token);
			await session.VerifyOfflineAsync(token);
		}
		Console.WriteLine($"TRAVEL game time {startedAt / 60 % 24:00}:{startedAt % 60:00} at the start, {clock.GetTime() - startedAt} game minutes played; " +
			$"the night Eninte's corpse {(corpseLies ? "still lay" : "had decayed")} at 04:00");
		policy.AssertClean();
	}

	/// <summary>s01-s08 for one race: the starter village's teleporter and transporters, then the teleport to the second zone.</summary>
	private async Task<Player> TravelVillageAsync(SimulationL0Session session, TravelPlan plan, SimulationL0Session[] others, CancellationToken token)
	{
		// s01: an ordinary level-1 character, and the setup teleport between the village's transporter and teleporter.
		session.BeginStep("s01", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token);
		await session.EnterWorldAsync(token);
		await session.WaitForPrologueAsync(token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
		Assert.False(player.GetCommonData().IsDaeva());
		// player_initial_data.xml gives every new character 1,000 Kinah (item 182400001); the director takes them for the fare refusal.
		Assert.Equal(1000, player.GetInventory().GetKinah());
		player.GetInventory().DecreaseKinah(1000);
		await TeleportForSetupAsync(session, player, plan.Village, plan.StandX, plan.StandY, Ground(plan.Village, plan.StandX, plan.StandY, plan.StandZ + 5, plan.StandZ - 5), token);
		await session.SynchronizeAsync(token);
		BotPosition stand = session.CurrentPosition;

		// s02: DialogService, AIRLINE_SERVICE: the two starter teleporters send a player who is not a Daeva the NO_RIGHT page.
		session.BeginStep("s02", "teleporter-refuses-a-non-daeva");
		int teleporter = await session.WaitForNpcAsync(plan.Teleporter, token);
		await OpenTravelDialogAsync(session, teleporter, token);
		await session.SendPacketAsync(session.Api.SelectDialog(teleporter, DialogAction.AIRLINE_SERVICE), token);
		DecodedBotServerPacket noRight = await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == teleporter);
		Assert.Equal(DialogPage.NO_RIGHT.Id(), noRight.Get<ushort>("dialogPageId"));
		Assert.Equal(27, DialogPage.NO_RIGHT.Id());
		// CM_TELEPORT_SELECT itself checks no Daeva status, only TeleportService.teleport's conditions in their order: the capital's
		// required quest (the Ascension) comes before the fare, so a client that sends it without Kinah is told about the quest.
		TeleporterTemplate teleporterTemplate = DataManager.TELEPORTER_DATA.GetTeleporterTemplateByNpcId(plan.Teleporter);
		Assert.Equal(plan.RequiredQuest, teleporterTemplate.GetTeleLocIdData().GetTeleportLocation(plan.CapitalLocation).GetRequiredQuest());
		int mark = session.PacketHistory.Count;
		await session.SendPacketAsync(session.Api.Teleport(teleporter, plan.CapitalLocation), token);
		await session.WaitForPacketAsync(typeof(SM_SYSTEM_MESSAGE), token, packet => packet.Get<string?>("name") == "STR_CANNOT_MOVE_TO_AIRPORT_NEED_FINISH_QUEST");
		await session.SynchronizeAsync(token);
		Assert.Equal(0, player.GetInventory().GetKinah());
		Assert.DoesNotContain(session.PacketHistory.Skip(mark), packet => packet.PacketType == typeof(SM_TELEPORT_LOC) ||
			packet.PacketType == typeof(SM_SYSTEM_MESSAGE) && packet.Get<string?>("name") == "STR_MSG_NOT_ENOUGH_KINA");

		// s03: the transporter's refusals. No Kinah: STR_MSG_NOT_ENOUGH_KINA with the fare. A location the transporter does not
		// fly: the audit line and STR_CANNOT_MOVE_TO_AIRPORT_NO_ROUTE. Out of talk range (talk distance + 1): FAR_FROM_NPC.
		session.BeginStep("s03", "transporter-refusals");
		TeleporterTemplate outTemplate = DataManager.TELEPORTER_DATA.GetTeleporterTemplateByNpcId(plan.OutTransporter);
		TeleportLocation outRoute = outTemplate.GetTeleLocIdData().GetTeleportLocation(plan.OutLocation);
		Assert.Null(outTemplate.GetTeleLocIdData().GetTeleportLocation(plan.WrongLocation));
		Assert.NotNull(DataManager.TELELOCATION_DATA.GetTelelocationTemplate(plan.WrongLocation));
		int transporter = await session.WaitForNpcAsync(plan.OutTransporter, token);
		Npc transporterNpc = Assert.IsType<Npc>(fixture.World.FindVisibleObject(transporter), exactMatch: false);
		await OpenAirlineMapAsync(session, transporter, outTemplate.GetTeleportId(), token);
		long fare = PricesService.GetPriceForService(outRoute.GetPrice(), plan.Race);
		mark = session.PacketHistory.Count;
		await session.SendPacketAsync(session.Api.Teleport(transporter, plan.OutLocation), token);
		DecodedBotServerPacket poor = await session.WaitForPacketAsync(typeof(SM_SYSTEM_MESSAGE), token, packet => packet.Get<string?>("name") == "STR_MSG_NOT_ENOUGH_KINA");
		Assert.Equal([fare.ToString(System.Globalization.CultureInfo.InvariantCulture)], poor.Get<string[]>("params"));
		int auditMark = fixture.LogCapture.Entries.Count;
		await session.SendPacketAsync(session.Api.Teleport(transporter, plan.WrongLocation), token);
		await session.WaitForPacketAsync(typeof(SM_SYSTEM_MESSAGE), token, packet => packet.Get<string?>("name") == "STR_CANNOT_MOVE_TO_AIRPORT_NO_ROUTE");
		await session.SynchronizeAsync(token);
		WorldPosition at = player.GetPosition(), npcAt = transporterNpc.GetPosition();
		Assert.Equal($"Player [id={player.GetObjectId()}, name={plan.Name}] tried to teleport to invalid locId {plan.WrongLocation} via " +
			$"Npc [templateId={plan.OutTransporter}, objectId={transporter}, name={transporterNpc.GetObjectTemplate().GetName()}, position={JavaPosition(npcAt)}] at {JavaPosition(at)}",
			Assert.Single(fixture.LogCapture.Entries.Skip(auditMark), entry => entry.Category == "AUDIT_LOG").Message);
		player.GetInventory().IncreaseKinah(TravelKinah);
		await session.SynchronizeAsync(token);
		Assert.Equal(TravelKinah, session.Api.World.Kinah);
		BotPosition far = stand with { Y = stand.Y + 8 };
		far = far with { Z = Ground(plan.Village, far.X, far.Y, stand.Z + 5, stand.Z - 5) };
		await session.MoveToPositionAsync(far, token);
		Assert.False(PositionUtil.IsInTalkRange(player, transporterNpc));
		await session.SendPacketAsync(session.Api.Teleport(transporter, plan.OutLocation), token);
		await session.WaitForPacketAsync(typeof(SM_SYSTEM_MESSAGE), token, packet => packet.Get<string?>("name") == "STR_CANNOT_MOVE_TO_AIRPORT_FAR_FROM_NPC");
		await session.MoveToPositionAsync(stand, token);
		await session.SynchronizeAsync(token);
		Assert.Equal(TravelKinah, player.GetInventory().GetKinah());
		Assert.DoesNotContain(session.PacketHistory.Skip(mark), packet => packet.PacketType == typeof(SM_EMOTION) && packet.Get<byte>("emotionType") == (byte)EmotionType.START_FLYTELEPORT);

		// s04 and s05: the flight out and the flight back with the other village's transporter.
		session.BeginStep("s04", "fly-out");
		await FlyTransporterAsync(session, player, plan.OutTransporter, plan.OutLocation, token);
		foreach (SimulationL0Session other in others) await other.DrainServerPacketsAsync(token);
		session.BeginStep("s05", "fly-back");
		Npc back = Assert.Single(fixture.World.GetWorldMap(plan.Village).GetMainWorldMapInstance().GetNpcs(plan.BackTransporter), npc => !npc.IsDead());
		if (!PositionUtil.IsInRange(player, back, 2.5f))
		{
			// Garhara stands 5 m from Anturoon Crossing's landing: the client walks up to him.
			float dx = player.GetX() - back.GetX(), dy = player.GetY() - back.GetY(), length = MathF.Sqrt(dx * dx + dy * dy);
			float x = back.GetX() + dx / length * 1.5f, y = back.GetY() + dy / length * 1.5f;
			await session.MoveToPositionAsync(new BotPosition(x, y, Ground(plan.Village, x, y, back.GetZ() + 5, back.GetZ() - 5), 0), token);
		}
		await FlyTransporterAsync(session, player, plan.BackTransporter, plan.BackLocation, token);
		foreach (SimulationL0Session other in others) await other.DrainServerPacketsAsync(token);
		await session.MoveToPositionAsync(stand, token);

		// s06: the director makes him a level-10 Gladiator. ClassChangeService.setClass with the Daeva status completes the
		// Ascension quest (completeAscensionQuest) and then updates the status, as Java's does.
		session.BeginStep("s06", "setup-daeva");
		Assert.True(ClassChangeService.SetClass(player, PlayerClass.GLADIATOR, validate: true, updateDaevaStatus: true));
		player.GetCommonData().SetLevel(10);
		Assert.True(player.GetCommonData().IsDaeva());
		Assert.True(player.IsCompleteQuest(plan.RequiredQuest));
		await session.SynchronizeAsync(token);

		// s07: a Daeva gets the teleporter's map (TeleportService.showMap).
		session.BeginStep("s07", "teleporter-map");
		await OpenAirlineMapAsync(session, teleporter, teleporterTemplate.GetTeleportId(), token);
		long kinah = player.GetInventory().GetKinah();

		// s08: the second zone. TeleportService.sendLoc: SM_TELEPORT_LOC (JUMP_IN, the map as its own instance id, the
		// telelocation's point and heading), the spawn on CM_TELEPORT_ANIMATION_DONE, the main instance of another map.
		session.BeginStep("s08", "teleport-to-the-second-zone");
		TeleportLocation zoneRoute = teleporterTemplate.GetTeleLocIdData().GetTeleportLocation(plan.ZoneLocation);
		Assert.Equal(TeleportType.REGULAR, zoneRoute.GetType_());
		Assert.Equal(0, zoneRoute.GetRequiredQuest());
		TelelocationTemplate zone = DataManager.TELELOCATION_DATA.GetTelelocationTemplate(plan.ZoneLocation);
		Npc teleporterNpc = Assert.IsType<Npc>(fixture.World.FindVisibleObject(teleporter), exactMatch: false);
		fare = PricesService.GetPriceForService(zoneRoute.GetPrice(), plan.Race);
		mark = session.PacketHistory.Count;
		await session.SendPacketAsync(session.Api.Teleport(teleporter, plan.ZoneLocation), token);
		DecodedBotServerPacket port = await session.WaitForPacketAsync(typeof(SM_TELEPORT_LOC), token);
		Assert.Equal(teleporterNpc.GetSpawn()!.GetStaticId() != 0 ? (byte)4 : (byte)3, port.Get<byte>("portAnimation"));
		Assert.Equal((zone.GetMapId(), zone.GetMapId()), (port.Get<int>("mapId"), port.Get<int>("destinationId")));
		Assert.Equal((zone.GetX(), zone.GetY(), zone.GetZ(), (byte)zone.GetHeading()),
			(port.Get<float>("x"), port.Get<float>("y"), port.Get<float>("z"), port.Get<byte>("heading")));
		await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
		await session.SynchronizeAsync(token);
		session.AcceptTeleportPosition();
		Assert.Equal((zone.GetMapId(), 1), (player.GetWorldId(), player.GetInstanceId()));
		Assert.Equal((zone.GetX(), zone.GetY(), zone.GetZ(), (byte)zone.GetHeading()), (player.GetX(), player.GetY(), player.GetZ(), player.GetHeading()));
		Assert.Equal(zone.GetMapId(), session.Api.World.MapId);
		Assert.Equal(kinah - fare, player.GetInventory().GetKinah());
		Assert.Equal(kinah - fare, session.Api.World.Kinah);
		foreach (SimulationL0Session other in others) await other.DrainServerPacketsAsync(token);
		return player;
	}

	/// <summary>A talk to an airline NPC: DialogPage.getStartPageId gives a functional NPC (func_dialogs 44) page 10.</summary>
	private static async Task OpenTravelDialogAsync(SimulationL0Session session, int npc, CancellationToken token)
	{
		await session.SendPacketAsync(session.Api.TalkTo(npc), token);
		DecodedBotServerPacket page = await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == npc);
		Assert.Equal(10, page.Get<ushort>("dialogPageId"));
	}

	/// <summary>The talk and AIRLINE_SERVICE: TeleportService.showMap's SM_TELEPORT_MAP (D the NPC, H the teleporter's teleport id).</summary>
	private static async Task OpenAirlineMapAsync(SimulationL0Session session, int npc, int teleportId, CancellationToken token)
	{
		await OpenTravelDialogAsync(session, npc, token);
		await session.SendPacketAsync(session.Api.SelectDialog(npc, DialogAction.AIRLINE_SERVICE), token);
		DecodedBotServerPacket map = await session.WaitForPacketAsync(typeof(SM_TELEPORT_MAP), token);
		var body = new SiegeBodyReader(SiegeBody(map));
		Assert.Equal((npc, teleportId), (body.D(), body.H()));
		body.End();
	}

	/// <summary>
	/// A flight transporter's route as the client flies it: the airline map, CM_TELEPORT_SELECT, the fare and Java's take-off
	/// (TeleportService.teleport: FLYING set, ACTIVE unset, the FLIGHT_TRANSPORTER path with the route's teleport id, SM_EMOTION
	/// START_FLYTELEPORT to the player too), CM_MOVE_IN_AIR along the fly path for its time (each one's distance kept by the
	/// path), and LAND_FLYTELEPORT (PlayerController.onFlyTeleportEnd: FLYING unset, ACTIVE set, the path cleared, and
	/// CM_EMOTION's SM_EMOTION back to the player).
	/// </summary>
	private async Task FlyTransporterAsync(SimulationL0Session session, Player player, int transporterNpcId, int locationId, CancellationToken token)
	{
		TeleporterTemplate template = DataManager.TELEPORTER_DATA.GetTeleporterTemplateByNpcId(transporterNpcId);
		TeleportLocation route = template.GetTeleLocIdData().GetTeleportLocation(locationId);
		Assert.Equal(TeleportType.FLIGHT, route.GetType_());
		// The wire's flight id is the client's path id * 1000 + 1.
		Assert.Equal(1, route.GetTeleportId() % 1000);
		FlyPathEntry path = DataManager.FLY_PATH.GetPathTemplate(route.GetTeleportId() / 1000);
		Assert.Equal((player.GetWorldId(), player.GetWorldId()), (path.GetStartWorldId(), path.GetEndWorldId()));
		Assert.Equal(DataManager.TELELOCATION_DATA.GetTelelocationTemplate(locationId).GetMapId(), path.GetEndWorldId());
		int transporter = await session.WaitForNpcAsync(transporterNpcId, token);
		Assert.True(PositionUtil.IsInTalkRange(player, Assert.IsType<Npc>(fixture.World.FindVisibleObject(transporter), exactMatch: false)));
		await OpenAirlineMapAsync(session, transporter, template.GetTeleportId(), token);
		long kinah = player.GetInventory().GetKinah(), fare = PricesService.GetPriceForService(route.GetPrice(), player.GetRace());
		Assert.True(fare > 0 && kinah >= fare);
		int stateBefore = player.GetState();
		Assert.True((stateBefore & (int)CreatureState.ACTIVE) != 0 && (stateBefore & (int)CreatureState.FLYING) == 0);

		await session.SendPacketAsync(session.Api.Teleport(transporter, locationId), token);
		DecodedBotServerPacket takeoff = await session.WaitForPacketAsync(typeof(SM_EMOTION), token, packet =>
			packet.Get<int>("senderObjectId") == session.CharacterId && packet.Get<byte>("emotionType") == (byte)EmotionType.START_FLYTELEPORT);
		Assert.Equal(route.GetTeleportId(), takeoff.Get<int>("teleportId"));
		int flying = (stateBefore | (int)CreatureState.FLYING) & ~(int)CreatureState.ACTIVE;
		Assert.Equal(flying, takeoff.Get<ushort>("state"));
		Assert.Equal(flying, player.GetState());
		Assert.True(player.IsUsingFlightTransporterOrWindstream());
		var flightPath = player.GetFlightPath();
		Assert.Equal((Aion.GameServer.Model.Templates.Flypath.FlightPath.Type.FLIGHT_TRANSPORTER, route.GetTeleportId(), 0),
			(flightPath.GetType_(), flightPath.GetId(), flightPath.GetDistance()));
		Assert.Equal(kinah - fare, player.GetInventory().GetKinah());

		var end = new BotPosition(path.GetEndX(), path.GetEndY(), path.GetEndZ(), 0);
		BotPosition from = session.CurrentPosition;
		float distance = MathF.Sqrt(MathF.Pow(end.X - from.X, 2) + MathF.Pow(end.Y - from.Y, 2) + MathF.Pow(end.Z - from.Z, 2));
		await session.ExecuteMovementAsync(CapitalAscensionScenario.CreateQuestFlight(from, end, path.GetEndWorldId(), TimeSpan.FromMilliseconds(path.GetTimeInMs())), token);
		await session.SynchronizeAsync(token);
		Assert.True(player.IsUsingFlightTransporterOrWindstream());
		Assert.Equal((int)MathF.Round(distance), player.GetFlightPath().GetDistance());
		Assert.InRange(PositionUtil.GetDistance(player, end.X, end.Y, end.Z), 0, 0.05);

		await session.SendPacketAsync(GameClientPackets.Emotion((byte)EmotionType.LAND_FLYTELEPORT), token);
		DecodedBotServerPacket landing = await session.WaitForPacketAsync(typeof(SM_EMOTION), token, packet =>
			packet.Get<int>("senderObjectId") == session.CharacterId && packet.Get<byte>("emotionType") == (byte)EmotionType.LAND_FLYTELEPORT);
		int landed = (flying & ~(int)CreatureState.FLYING) | (int)CreatureState.ACTIVE;
		Assert.Equal(landed, landing.Get<ushort>("state"));
		Assert.Equal(landed, player.GetState());
		Assert.Null(player.GetFlightPath());
		Assert.False(player.IsUsingFlightTransporterOrWindstream() || player.IsInFlyingState());
		await session.SynchronizeAsync(token);
		Assert.Equal(kinah - fare, session.Api.World.Kinah);
		// The client stands where it landed; the session keeps that as the position the database will have.
		await session.MoveToPositionAsync(end, token);
	}

	/// <summary>
	/// Moves the world's game clock (a game minute every 5 s, GameTimeService.startClock) to the next <paramref name="hour"/>:
	/// <paramref name="minute"/>, serializing every bot's packets after each step (harness rule 7). Each step stops short of
	/// the target's tick, the last ones are a second long, so the clock never passes it.
	/// </summary>
	private static async Task AdvanceTravelToGameTimeAsync(SimulationL0Session[] online, int hour, int minute, CancellationToken token)
	{
		GameTime clock = GameTimeService.GetInstance().GetGameTime();
		int target = hour * 60 + minute;
		bool moved = false;
		while (true)
		{
			int remaining = ((target - clock.GetTime() % 1440) + 1440) % 1440;
			if (remaining == 0 && moved)
				break;
			if (remaining == 0)
				remaining = 1440;
			TimeSpan step = remaining > 6 ? TimeSpan.FromSeconds(Math.Min(30, (remaining - 1) * 5)) : TimeSpan.FromSeconds(1);
			await online[0].AdvanceAsync(step, token);
			foreach (SimulationL0Session session in online.Skip(1))
				await session.DrainServerPacketsAsync(token);
			moved = true;
		}
		await SyncSiegeSessionsAsync(online, token);
		Assert.Equal((hour, minute), (clock.GetHour(), clock.GetMinute()));
	}

	private static Npc[] LiveNpcs(WorldMapInstance instance, int npcId) =>
		instance.GetNpcs(npcId).Where(npc => npc.IsSpawned() && !npc.IsDead()).ToArray();

	private static void AssertAt(Npc npc, float x, float y, float z) =>
		Assert.InRange(PositionUtil.GetDistance(npc, x, y, z), 0, 0.01);

	/// <summary>Igraine and Virashak: spot-level hours (05:00-21:00 and 21:00-05:00) under the group's empty temporary_spawn.</summary>
	private static void AssertMorheimSpots(WorldMapInstance morheim, bool night)
	{
		AssertAt(Assert.Single(LiveNpcs(morheim, Igraine)), night ? 2479.46f : 2522.29f, night ? 1801.02f : 1703.93f, night ? 95.6547f : 92.0406f);
		AssertAt(Assert.Single(LiveNpcs(morheim, Virashak)), night ? 2488.72f : 2493f, night ? 1795.96f : 1713f, night ? 95.2742f : 90.1875f);
	}

	/// <summary>The hour's spawn reaches the player who sees its spot: one SM_NPC_INFO for the new object.</summary>
	private static void AssertAppears(SimulationL0Session session, int mark, Npc npc)
	{
		DecodedBotServerPacket info = Assert.Single(session.PacketHistory.Skip(mark), packet =>
			packet.PacketType == typeof(SM_NPC_INFO) && packet.Get<int>("npcId") == npc.GetNpcId());
		Assert.Equal(npc.GetObjectId(), info.Get<int>("objectId"));
		Assert.Contains(session.Api.World.Objects.Values, known => known.ObjectId == npc.GetObjectId());
	}

	/// <summary>World.removeObject despawns with ObjectDeleteAnimation.FADE_OUT (1).</summary>
	private static void AssertDisappears(SimulationL0Session session, int mark, Npc npc)
	{
		DecodedBotServerPacket delete = Assert.Single(session.PacketHistory.Skip(mark), packet =>
			packet.PacketType == typeof(SM_DELETE) && packet.Get<int>("objectId") == npc.GetObjectId());
		Assert.Equal((byte)1, delete.Get<byte>("animationId"));
		Assert.DoesNotContain(session.Api.World.Objects.Values, known => known.ObjectId == npc.GetObjectId());
	}

	/// <summary>WorldPosition.toString as Java prints it.</summary>
	private static string JavaPosition(WorldPosition position) =>
		"WorldPosition [mapId=" + position.GetMapId() + ", x=" + JavaString.ValueOf(position.GetX()) + ", y=" + JavaString.ValueOf(position.GetY()) +
		", z=" + JavaString.ValueOf(position.GetZ()) + ", heading=" + JavaString.ValueOf(position.GetHeading()) + ", isSpawned=" +
		JavaString.ValueOf(position.IsSpawned()) + "]";
}
