using System.Buffers.Binary;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.Commons.Database;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.Siege;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Siegelocation;
using Aion.GameServer.Model.Templates.Spawns.Siegespawns;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Services.Siege;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Extensions;

namespace Aion.Simulation.Tests;

/// <summary>
/// P11-05, scenario SIEGE: Java's siege calendar plays a Thursday's 17:00 slot of the Reshanta fortresses (1131, 1132, 1141,
/// 1211, 1251) and its 19:00 slot of the Gelkmaros fortresses (3011, 3021) through, on the virtual clock, from the
/// process's own epoch (Thursday 2026-09-17 15:58 UTC): the hourly status broadcasts, the 16:55 preparation with its
/// occupy-count reset, the siege start and the clearing of the fortress, a capture by an Asmodian group in a legion
/// (1132, and 3011 whose grades pay kinah and GP and whose legion GP is 1000), sieges the Balaur defend to their end, a
/// Balaur assault on a player-held fortress (1141) and the Silentera outpost after the Gelkmaros sieges. Every expected
/// value is Java's: <c>SiegeService.initSieges</c> (the 16:55 preparation, the hourly status broadcast),
/// <c>startPreparations</c>/<c>resetSiegeLocation</c> (the occupy-count reset), <c>FortressSiege</c> (start, capture,
/// end), <c>FortressLocation.clearLocation</c>, <c>AbstractSiegeProtectorAI.stopSiege</c>, <c>Siege.sendRewardsToParticipants</c>,
/// <c>FortressSiege.distributeLegionRewards</c> and <c>BalaurAssaultService</c>/<c>FortressAssault</c>.
/// <para>
/// Director setup, recorded here as the plan asks: the two Asmodian attackers are made level-50 Gladiators and the Elyos
/// observer a level-30 Gladiator; the attackers' group and legion are made by their own packets (invite and answer, the
/// legion's CM_LEGION create with the fee given by the director); Siel's Western Fortress (1131) is given to the Elyos by
/// the GM capture (<c>//siege capture</c>) with an occupy count of 2, as two defended sieges would leave it, and Sulfur
/// Fortress (1141) to the Asmodians the same way; bots are placed by setup teleports; the bosses' health is lowered
/// (<see cref="SiegeBossHealth"/>, and 2 for 3011's level-65 boss) so that the two Gladiators kill them with real
/// auto-attacks, which do 1 a swing against a fortress boss's defence; and the Balaur assault on 1141 is the GM's
/// <c>//siege assault 1141 60</c> unless Java's own roll at the siege start (3% at that influence) scheduled one.
/// </para>
/// </summary>
public sealed partial class SimulationFastScenarioTests
{
	internal static readonly DateTimeOffset SiegeEpoch = new(2026, 9, 17, 15, 58, 0, TimeSpan.Zero);
	private const int SiegeResetFortress = 1131; // Siel's Western Fortress
	private const int SiegeCapturedFortress = 1132; // Siel's Eastern Fortress
	private const int SiegeAssaultedFortress = 1141; // Sulfur Fortress
	private const int Ulsaruk = 263311;
	private const int Reshanta = 400010000;
	private static readonly int[] ThursdayFortresses = [1131, 1132, 1141, 1211, 1251];
	private const int VorgaltemCitadel = 3011;
	private const int CrimsonTemple = 3021;
	private const int SilenteraOutpost = 3111; // depends on 3011 and 3021
	private static readonly int[] GelkmarosFortresses = [VorgaltemCitadel, CrimsonTemple];

	private async Task RunSiegeAsync(ScenarioDefinition scenario, bool includeHistory)
	{
		var sessions = new List<SimulationL0Session>();
		bool logSiege = LoggingConfig.LOG_SIEGE;
		try
		{
			// gameserver.log.siege: the lines Java's siege code writes only when it is on are played and checked too.
			LoggingConfig.LOG_SIEGE = true;
			await PlaySiegeAsync(scenario, includeHistory, sessions);
		}
		catch (Exception exception) when (exception is not SimulationLogPolicyException)
		{
			// Harness rule 2: the whole exception and every bot's recent packets.
			throw new InvalidOperationException("SIEGE failed at " + SystemClock.UtcNow().ToString("O") + "; recent packets:" + Environment.NewLine +
				string.Join(Environment.NewLine, sessions.Select(session => session.CurrentStep + " " + string.Join(" | ",
					session.PacketHistory.TakeLast(25).Select(packet => packet.PacketType.Name +
						(packet.PacketType == typeof(SM_SYSTEM_MESSAGE) ? "(" + packet.Get<string?>("name") + ")" : ""))))), exception);
		}
		finally
		{
			LoggingConfig.LOG_SIEGE = logSiege;
		}
	}

	private async Task PlaySiegeAsync(ScenarioDefinition scenario, bool includeHistory, List<SimulationL0Session> created)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(25));
		CancellationToken token = timeout.Token;
		Assert.Equal(SiegeEpoch, fixture.Epoch);
		DateTimeOffset day = new(2026, 9, 17, 0, 0, 0, TimeSpan.Zero);
		Assert.Equal(DayOfWeek.Thursday, day.DayOfWeek);
		DateTimeOffset hourly = day.AddHours(16), preparation = day.AddHours(16).AddMinutes(55), start = day.AddHours(17), end = day.AddHours(18);
		Assert.True(SystemClock.UtcNow() < hourly, $"the scenario starts at {SystemClock.UtcNow():O}, after the 16:00 broadcast");
		int firstLogEntry = fixture.LogCapture.Entries.Count;

		await using var leader = new SimulationL0Session(fixture, policy, "b01", 125, "Assimsiegea", Race.ASMODIANS);
		await using var member = new SimulationL0Session(fixture, policy, "b02", 126, "Assimsiegeb", Race.ASMODIANS);
		await using var observer = new SimulationL0Session(fixture, policy, "b03", 127, "Aesimsiegec", Race.ELYOS);
		SimulationL0Session[] sessions = [leader, member, observer];
		created.AddRange(sessions);
		foreach (SimulationL0Session session in sessions)
		{
			session.BeginStep("s01", "login-create-enter");
			await session.LoginAndAuthenticateAsync(token);
			await session.CreateCharacterAsync(token);
			await session.EnterWorldAsync(token);
			await session.WaitForPrologueAsync(token);
			Player player = fixture.World.GetPlayer(session.CharacterId);
			Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
			Assert.True(ClassChangeService.SetClass(player, PlayerClass.GLADIATOR, validate: true, updateDaevaStatus: true));
			player.GetCommonData().SetLevel(session == observer ? 30 : 50);
			// SiegeService.onPlayerLogin: SM_INFLUENCE_RATIO, the whole SM_SIEGE_LOCATION_INFO, SM_AFTER_SIEGE_LOCINFO_475 and
			// SM_RIFT_ANNOUNCE.
			SiegeLocationEntry[] all = ReadSiegeLocationInfo(Assert.Single(session.PacketHistory, packet => packet.PacketType == typeof(SM_SIEGE_LOCATION_INFO)), 0);
			Assert.Equal(SiegeService.GetInstance().GetSiegeLocations().Keys.Order(), all.Select(entry => entry.LocationId).Order());
			Assert.All(all, entry => Assert.Equal(SiegeService.GetInstance().GetSiegeLocation(entry.LocationId).GetRace().GetRaceId(), entry.Race));
			foreach (Type loginPacket in new[] { typeof(SM_INFLUENCE_RATIO), typeof(SM_AFTER_SIEGE_LOCINFO_475), typeof(SM_RIFT_ANNOUNCE) })
				Assert.Contains(session.PacketHistory, packet => packet.PacketType == loginPacket);
		}
		Player leaderPlayer = fixture.World.GetPlayer(leader.CharacterId);
		Player memberPlayer = fixture.World.GetPlayer(member.CharacterId);
		Player observerPlayer = fixture.World.GetPlayer(observer.CharacterId);

		// The attackers' group and legion, by their own packets (S1's path).
		leader.BeginStep("s02", "group-and-legion");
		member.BeginStep("s02", "group-and-legion");
		await leader.SendPacketAsync(leader.Api.InviteToGroup(memberPlayer.GetName()), token);
		await member.DrainServerPacketsAsync(token);
		await member.WaitForPacketAsync(typeof(SM_QUESTION_WINDOW), token, packet => packet.Get<int>("code") == 60000);
		await member.SendPacketAsync(member.Api.Answer(1), token);
		await leader.DrainServerPacketsAsync(token);
		await leader.WaitForPacketAsync(typeof(SM_GROUP_INFO), token, packet => packet.Get<int>("leaderId") == leader.CharacterId);
		await member.WaitForPacketAsync(typeof(SM_GROUP_INFO), token, packet => packet.Get<int>("leaderId") == leader.CharacterId);
		Assert.Same(leaderPlayer.GetCurrentTeam(), memberPlayer.GetCurrentTeam());
		Assert.Equal(0, ItemService.AddItem(leaderPlayer, BotWorldModel.KinahItemId, LegionConfig.LEGION_CREATE_REQUIRED_KINAH));
		const string legionName = "Siegebrigade";
		await leader.SendPacketAsync(leader.Api.CreateLegion(legionName), token);
		await leader.WaitForPacketAsync(typeof(SM_SYSTEM_MESSAGE), token, packet => packet.Get<string?>("name") == "STR_GUILD_CREATED");
		await leader.SendPacketAsync(leader.Api.InviteToLegion(memberPlayer.GetName()), token);
		await member.DrainServerPacketsAsync(token);
		await member.WaitForPacketAsync(typeof(SM_QUESTION_WINDOW), token, packet => packet.Get<int>("code") == 80001);
		await member.SendPacketAsync(member.Api.Answer(1), token);
		await member.WaitForPacketAsync(typeof(SM_LEGION_ADD_MEMBER), token, packet => packet.Get<int>("objectId") == member.CharacterId);
		Legion legion = Assert.IsType<Legion>(leaderPlayer.GetLegion());
		Assert.Same(legion, memberPlayer.GetLegion());
		Assert.Equal(leader.CharacterId, legion.GetBrigadeGeneral().GetObjectId());

		// The two fortresses the slot needs in player hands, by the GM capture; every other location stays as the new
		// database leaves it (Balaur).
		SiegeService sieges = SiegeService.GetInstance();
		FortressLocation reset = sieges.GetFortress(SiegeResetFortress), captured = sieges.GetFortress(SiegeCapturedFortress),
			assaulted = sieges.GetFortress(SiegeAssaultedFortress);
		foreach (int id in ThursdayFortresses)
		{
			Assert.False(sieges.IsSiegeInProgress(id));
			Assert.Equal(SiegeRace.BALAUR, sieges.GetFortress(id).GetRace());
			AssertSiegeGroup(id, SiegeRace.BALAUR, SiegeModType.PEACE);
		}
		sieges.CaptureSiege(SiegeRace.ELYOS, 0, SiegeResetFortress);
		reset.SetOccupiedCount(reset.GetTemplate().GetMaxOccupyCount());
		Assert.Equal(2, reset.GetOccupiedCount());
		Assert.True(SiegeDAO.UpdateSiegeLocation(reset));
		sieges.CaptureSiege(SiegeRace.ASMODIANS, 0, SiegeAssaultedFortress);
		AssertSiegeGroup(SiegeResetFortress, SiegeRace.ELYOS, SiegeModType.PEACE);
		AssertSiegeGroup(SiegeAssaultedFortress, SiegeRace.ASMODIANS, SiegeModType.PEACE);
		AssertSiegeRow(SiegeResetFortress, "ELYOS", 0, 2, 0);
		AssertSiegeRow(SiegeAssaultedFortress, "ASMODIANS", 0, 0, 0);
		await SyncSiegeSessionsAsync(sessions, token);

		// 16:00, the hourly status broadcast (initSieges' cron job): the next state of every fortress whose preparation comes
		// before the next broadcast is vulnerable, and every player is told.
		int[] marks = Marks(sessions);
		foreach (SimulationL0Session session in sessions) session.BeginStep("s03", "hourly-fortress-status");
		await AdvanceSiegeWorldToAsync(sessions, hourly.AddSeconds(1), token);
		await SyncSiegeSessionsAsync(sessions, token);
		int[] fortressIds = sieges.GetFortresses().Keys.ToArray();
		foreach (int id in fortressIds)
			Assert.Equal(ThursdayFortresses.Contains(id) ? SiegeLocation.STATE_VULNERABLE : SiegeLocation.STATE_INVULNERABLE,
				sieges.GetFortress(id).GetNextState());
		for (int i = 0; i < sessions.Length; i++)
		{
			FortressStatus status = AssertHourlyBroadcast(sessions[i], marks[i], fortressIds);
			Assert.InRange(status.SecondsUntilNextState, 3599, 3600);
			Assert.Equal(3 / 100f, status.ElyosRate);
			Assert.Equal(3 / 100f, status.AsmodianRate);
			Assert.Equal(94 / 100f, status.BalaurRate);
			Assert.Equal([3f, 3f, 48f], status.Worlds[Reshanta]);
			Assert.Equal(fortressIds.ToDictionary(id => id, id => ThursdayFortresses.Contains(id) ? 1 : 0), status.NextStates);
		}

		// 16:55, the preparation (SiegeStartRunnable -> checkSiegeStart -> startPreparations): the siege start is set five
		// minutes ahead, and 1131, held by the Elyos at its maximum occupy count, goes back to the Balaur.
		marks = Marks(sessions);
		foreach (SimulationL0Session session in sessions) session.BeginStep("s04", "preparation-and-occupy-reset");
		await AdvanceSiegeWorldToAsync(sessions, preparation.AddSeconds(1), token);
		await SyncSiegeSessionsAsync(sessions, token);
		Assert.Equal(SiegeRace.BALAUR, reset.GetRace());
		Assert.Equal(0, reset.GetLegionId());
		Assert.Equal(0, reset.GetOccupiedCount());
		Assert.False(reset.IsVulnerable());
		AssertSiegeGroup(SiegeResetFortress, SiegeRace.BALAUR, SiegeModType.PEACE);
		AssertSiegeRow(SiegeResetFortress, "BALAUR", 0, 0, 0);
		Assert.Equal(SiegeRace.ASMODIANS, assaulted.GetRace());
		AssertSiegeGroup(SiegeAssaultedFortress, SiegeRace.ASMODIANS, SiegeModType.PEACE);
		foreach (int id in ThursdayFortresses)
			Assert.False(sieges.IsSiegeInProgress(id), $"siege {id} started at the preparation");
		string resetName = reset.GetTemplate().GetL10n()!;
		for (int i = 0; i < sessions.Length; i++)
		{
			DecodedBotServerPacket[] since = sessions[i].PacketHistory.Skip(marks[i]).ToArray();
			DecodedBotServerPacket message = Assert.Single(since, packet => packet.PacketType == typeof(SM_SYSTEM_MESSAGE)
				&& packet.Get<string?>("name") == "STR_ABYSS_WIN_CASTLE");
			Assert.Equal([SiegeRace.BALAUR.GetL10n()!, resetName], message.Get<string[]>("params"));
			Assert.DoesNotContain(since, packet => packet.PacketType == typeof(SM_SYSTEM_MESSAGE) && packet.Get<string?>("name") == "STR_ABYSS_GUILD_CASTLE_TAKEN");
			SiegeLocationEntry[] infos = since.Where(packet => packet.PacketType == typeof(SM_SIEGE_LOCATION_INFO))
				.Select(ReadSiegeLocationUpdate).ToArray();
			Assert.Equal(2, infos.Length); // resetSiegeLocation's own and broadcastUpdate's
			Assert.All(infos, info => Assert.Equal(new SiegeLocationEntry(SiegeResetFortress, 0, SiegeRace.BALAUR.GetRaceId(), 0, 0, 1, 0), info));
			InfluenceRatio ratio = ReadInfluenceRatio(Assert.Single(since, packet => packet.PacketType == typeof(SM_INFLUENCE_RATIO)));
			Assert.Equal(0f, ratio.ElyosRate);
			Assert.Equal(3 / 100f, ratio.AsmodianRate);
			Assert.Equal(97 / 100f, ratio.BalaurRate);
		}

		// 16:58: the Elyos observer stands on the ground of 1132 with a personal kisk, out of reach of its Balaur guards.
		observer.BeginStep("s05", "kisk-inside-the-fortress");
		await AdvanceSiegeWorldToAsync(sessions, day.AddHours(16).AddMinutes(58), token);
		int reshantaMark = observer.PacketHistory.Count;
		await TeleportForSetupAsync(observer, observerPlayer, Reshanta, 2460f, 3040f, 1575.61f, token);
		await observer.SynchronizeAsync(token);
		// SiegeService.onEnterSiegeWorld: the shields of every location of the map, and its artifacts' states.
		DecodedBotServerPacket[] arrival = observer.PacketHistory.Skip(reshantaMark).ToArray();
		var shields = ReadShieldEffect(Assert.Single(arrival, packet => packet.PacketType == typeof(SM_SHIELD_EFFECT)));
		Assert.Equal(SiegeService.GetInstance().GetSiegeLocations(Reshanta).Keys.Order(), shields.Keys.Order());
		Assert.All(shields, shield => Assert.Equal(SiegeService.GetInstance().GetSiegeLocation(shield.Key).IsUnderShield(), shield.Value));
		int[] artifactIds = ReadArtifactInfo(Assert.Single(arrival, packet => packet.PacketType == typeof(SM_ABYSS_ARTIFACT_INFO3)));
		Assert.Equal(SiegeService.GetInstance().GetArtifacts().Values.Where(artifact => artifact.GetWorldId() == Reshanta)
			.Select(artifact => artifact.GetLocationId() * 10 + 1).Order(), artifactIds.Order());
		Assert.Equal(0, ItemService.AddItem(observerPlayer, 184000017, 1));
		Item kiskItem = Assert.IsType<Item>(observerPlayer.GetInventory().GetFirstItemByItemId(184000017));
		await observer.SendPacketAsync(observer.Api.UseItem(kiskItem.GetObjectId(), kiskItem.GetItemTemplate()), token);
		await observer.WaitForPacketAsync(typeof(SM_ITEM_USAGE_ANIMATION), token);
		await AdvanceSiegeWorldToAsync(sessions, SystemClock.UtcNow().AddMilliseconds(10_001), token);
		Kisk kisk = Assert.IsType<Kisk>(observerPlayer.GetKisk());
		Assert.True(kisk.IsActive());
		Assert.True(captured.IsInsideLocation(observerPlayer));
		Assert.True(captured.IsInsideLocation(kisk));
		var inside = new List<Creature>();
		captured.ForEachCreature(inside.Add);
		Assert.Contains(observerPlayer, inside);
		Assert.Contains(kisk, inside);

		// 17:00, the siege start (startSiege -> FortressSiege.onSiegeStart) of the five, and the hourly broadcast again.
		marks = Marks(sessions);
		foreach (SimulationL0Session session in sessions) session.BeginStep("s06", "siege-start");
		await AdvanceSiegeWorldToAsync(sessions, start.AddSeconds(1), token);
		foreach (int id in ThursdayFortresses)
		{
			FortressLocation fortress = sieges.GetFortress(id);
			Assert.True(sieges.IsSiegeInProgress(id), $"siege {id} did not start at 17:00");
			FortressSiege siege = Assert.IsType<FortressSiege>(sieges.GetSiege(id));
			Assert.True(siege.IsStarted());
			Assert.False(siege.IsFinished());
			Assert.Equal(start.ToUnixTimeMilliseconds(), siege.GetStartTime());
			Assert.True(fortress.IsVulnerable());
			Assert.Equal(SiegeLocation.STATE_INVULNERABLE, fortress.GetNextState()); // the 17:00 broadcast: next siege on Saturday
			AssertSiegeGroup(id, fortress.GetRace(), SiegeModType.SIEGE);
			SiegeNpc boss = siege.GetBoss();
			Assert.NotNull(boss);
			Assert.Equal(AbyssNpcType.BOSS, boss.GetObjectTemplate().GetAbyssNpcType());
			Assert.Single(fixture.World.GetLocalSiegeNpcs(id), npc => npc.GetObjectTemplate().GetAbyssNpcType() == AbyssNpcType.BOSS);
		}
		Assert.Equal(Ulsaruk, sieges.GetSiege(SiegeCapturedFortress).GetBoss().GetNpcId());
		Assert.Equal(SiegeRace.BALAUR, sieges.GetFortress(SiegeResetFortress).GetRace());
		// clearLocation: the Elyos kisk dies and the Elyos player goes to his bind point (none yet: the race's start).
		var bind = DataManager.PLAYER_INITIAL_DATA.GetSpawnLocation(Race.ELYOS);
		Assert.Null(observerPlayer.GetBindPoint());
		await observer.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token, packet => packet.Get<int>("worldId") == bind.GetMapId());
		await observer.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == observer.CharacterId);
		observer.AcceptTeleportPosition();
		Assert.True(kisk.IsDead() || !kisk.IsSpawned(), "the Elyos kisk survived the clearing of 1132");
		Assert.Null(observerPlayer.GetKisk());
		Assert.Equal(bind.GetMapId(), observerPlayer.GetWorldId());
		Assert.False(captured.IsInsideLocation(observerPlayer));
		await SyncSiegeSessionsAsync(sessions, token);
		for (int i = 0; i < sessions.Length; i++)
		{
			DecodedBotServerPacket[] since = sessions[i].PacketHistory.Skip(marks[i]).ToArray();
			var states = since.Where(packet => packet.PacketType == typeof(SM_SIEGE_LOCATION_STATE)).Select(ReadSiegeLocationState).ToArray();
			Assert.Equal(ThursdayFortresses.Order(), states.Select(state => state.LocationId).Order());
			Assert.All(states, state => Assert.Equal(1, state.State));
			FortressStatus status = AssertHourlyBroadcast(sessions[i], marks[i], fortressIds);
			Assert.All(status.NextStates.Values, state => Assert.Equal(0, state));
		}

		// The Balaur assault on the Asmodians' 1141: Java's roll at the start, else the GM's //siege assault 1141 60.
		leader.BeginStep("s07", "balaur-assault-on-1141");
		FortressAssault? assault = BalaurAssaultService.GetInstance().GetFortressAssaultBySiegeId(SiegeAssaultedFortress);
		bool naturalAssault = assault != null;
		if (!naturalAssault)
		{
			Assert.True(BalaurAssaultService.GetInstance().StartAssault(SiegeAssaultedFortress, 60));
			assault = BalaurAssaultService.GetInstance().GetFortressAssaultBySiegeId(SiegeAssaultedFortress);
		}
		Assert.NotNull(assault);
		FortressSiege assaultSiege = Assert.IsType<FortressSiege>(sieges.GetSiege(SiegeAssaultedFortress));
		int[] assaultMarks = Marks(sessions);
		Assert.False(BalaurAssaultService.GetInstance().StartAssault(SiegeAssaultedFortress, 60)); // "already started"
		foreach (int id in ThursdayFortresses.Where(id => id != SiegeAssaultedFortress))
			Assert.Null(BalaurAssaultService.GetInstance().GetFortressAssaultBySiegeId(id)); // Balaur-held: no assault

		// The capture of 1132: the Asmodian group kills Ulsaruk inside the fortress.
		FortressSiege captureSiege = Assert.IsType<FortressSiege>(sieges.GetSiege(SiegeCapturedFortress));
		SiegeNpc ulsaruk = captureSiege.GetBoss();
		long warehouseBefore = legion.GetLegionWarehouse().GetKinah();
		int lettersBeforeLeader = leaderPlayer.GetMailbox().Size(), lettersBeforeMember = memberPlayer.GetMailbox().Size();
		int apBeforeLeader = leaderPlayer.GetAbyssRank().GetAp(), apBeforeMember = memberPlayer.GetAbyssRank().GetAp();
		foreach (SimulationL0Session attacker in new[] { leader, member })
			attacker.BeginStep("s08", "kill-ulsaruk");
		await TeleportForSetupAsync(leader, leaderPlayer, Reshanta, ulsaruk.GetX() - 1, ulsaruk.GetY(), ulsaruk.GetZ(), token);
		await TeleportForSetupAsync(member, memberPlayer, Reshanta, ulsaruk.GetX() + 1, ulsaruk.GetY(), ulsaruk.GetZ(), token);
		Assert.True(captured.IsInsideLocation(leaderPlayer));
		Assert.True(captured.IsInsideLocation(memberPlayer));
		ulsaruk.GetLifeStats().SetCurrentHp(SiegeBossHealth);
		marks = Marks(sessions);
		var swings = new Dictionary<int, int> { [leader.CharacterId] = 0, [member.CharacterId] = 0 };
		var fightLog = new List<string> { $"start boss {ulsaruk.GetLifeStats().GetCurrentHp()}/{ulsaruk.GetLifeStats().GetMaxHp()}" };
		foreach (SimulationL0Session attacker in new[] { leader, member })
			await attacker.SendPacketAsync(attacker.Api.Target(ulsaruk.GetObjectId()), token);
		for (int round = 0; round < 60 && !ulsaruk.IsDead(); round++)
		{
			foreach ((SimulationL0Session attacker, Player player) in new[] { (leader, leaderPlayer), (member, memberPlayer) })
			{
				if (ulsaruk.IsDead() || player.IsDead())
					continue;
				int speed = player.GetGameStats().GetAttackSpeed().GetCurrent();
				if (attacker.Api.Timing.TimeUntilAttack(speed) > TimeSpan.Zero)
					continue;
				await attacker.SendPacketAsync(attacker.Api.Attack(ulsaruk.GetObjectId(), speed), token);
				swings[attacker.CharacterId]++;
			}
			if (leaderPlayer.IsDead() && memberPlayer.IsDead())
				break;
			await AdvanceSiegeWorldToAsync(sessions, SystemClock.UtcNow().AddMilliseconds(500), token);
			fightLog.Add($"{SystemClock.UtcNow():ss.fff} boss {ulsaruk.GetLifeStats().GetCurrentHp()} {ulsaruk.GetAi().GetState()} leader {leaderPlayer.GetLifeStats().GetCurrentHp()} member {memberPlayer.GetLifeStats().GetCurrentHp()} aggro [" +
				string.Join(",", ulsaruk.GetAggroList().Stream().Select(info => info.GetAttacker().GetName() + ":" + info.GetDamage())) + "]");
		}
		await SyncSiegeSessionsAsync(sessions, token);
		// The fight as the attackers saw it, for a failure: who hit whom for how much (SM_ATTACK, SmAttackStatus) and the casts.
		string Fight() => $"swings {string.Join(",", swings.Values)}, attackers hp {leaderPlayer.GetLifeStats().GetCurrentHp()}/{leaderPlayer.GetLifeStats().GetMaxHp()} " +
			$"{memberPlayer.GetLifeStats().GetCurrentHp()}/{memberPlayer.GetLifeStats().GetMaxHp()}, at {SystemClock.UtcNow():O}; " + string.Join("; ", fightLog) +
			"; the member saw " + string.Join(Environment.NewLine, member.PacketHistory.Skip(marks[1]).Where(packet => packet.PacketType != typeof(SM_NPC_INFO)
				&& packet.PacketType != typeof(SM_MOVE) && packet.PacketType != typeof(SM_DELETE) && packet.PacketType != typeof(SM_ABNORMAL_EFFECT))
				.TakeLast(40).Select(packet => packet.PacketType.Name + " " + System.Text.Json.JsonSerializer.Serialize(packet.Fields)));
		if (!ulsaruk.IsDead())
			Assert.Fail($"Ulsaruk survived: hp {ulsaruk.GetLifeStats().GetCurrentHp()}/{ulsaruk.GetLifeStats().GetMaxHp()}, {Fight()}");
		if (leaderPlayer.IsDead() || memberPlayer.IsDead())
			Assert.Fail("an attacker died in the fight: " + Fight());
		await AdvanceSiegeWorldToAsync(sessions, SystemClock.UtcNow().AddSeconds(1), token);
		await SyncSiegeSessionsAsync(sessions, token);

		// What Java's capture leaves.
		Assert.False(sieges.IsSiegeInProgress(SiegeCapturedFortress));
		Assert.True(captureSiege.IsFinished());
		Assert.True(captureSiege.IsBossKilled());
		Assert.Equal(SiegeRace.ASMODIANS, captured.GetRace());
		Assert.Equal(legion.GetLegionId(), captured.GetLegionId());
		Assert.Equal(1, captured.GetOccupiedCount());
		Assert.Equal(-1, captured.GetFactionBalance());
		Assert.False(captured.IsVulnerable());
		Assert.False(captured.IsUnderShield());
		ArtifactLocation artifact = Assert.IsType<ArtifactLocation>(sieges.GetFortressArtifact(SiegeCapturedFortress));
		Assert.Equal(SiegeRace.ASMODIANS, artifact.GetRace());
		Assert.Equal(legion.GetLegionId(), artifact.GetLegionId());
		AssertSiegeGroup(SiegeCapturedFortress, SiegeRace.ASMODIANS, SiegeModType.PEACE);
		AssertSiegeRow(SiegeCapturedFortress, "ASMODIANS", legion.GetLegionId(), 1, -1);
		string capturedName = captured.GetTemplate().GetL10n()!;
		const int fortressBuff = 12148; // FortressSiege.applyWorldBuffs, 1132
		string buffName = DataManager.SKILL_DATA.GetSkillTemplate(fortressBuff).GetL10n()!;
		for (int i = 0; i < sessions.Length; i++)
		{
			Player player = fixture.World.GetPlayer(sessions[i].CharacterId);
			DecodedBotServerPacket[] since = sessions[i].PacketHistory.Skip(marks[i]).ToArray();
			DecodedBotServerPacket winner = Assert.Single(since, packet => packet.PacketType == typeof(SM_SYSTEM_MESSAGE)
				&& packet.Get<string?>("name") == "STR_ABYSS_GUILD_WIN_CASTLE");
			Assert.Equal([legionName, capturedName], winner.Get<string[]>("params"));
			bool asmodian = player.GetRace() == Race.ASMODIANS;
			var buffMessages = since.Where(packet => packet.PacketType == typeof(SM_SYSTEM_MESSAGE) && packet.Get<string?>("name") == "STR_CASTLE_WIN_BUFF_ON").ToArray();
			Assert.Equal(asmodian ? 1 : 0, buffMessages.Length);
			if (asmodian)
				Assert.Equal([SiegeRace.ASMODIANS.GetL10n()!, capturedName, buffName], buffMessages[0].Get<string[]>("params"));
			Assert.True(asmodian == player.GetEffectController().HasAbnormalEffect(fortressBuff),
				$"{player.GetName()}: buff {fortressBuff} {player.GetEffectController().HasAbnormalEffect(fortressBuff)}, dead {player.IsDead()}, hp {player.GetLifeStats().GetCurrentHp()}, " +
				$"effects [{string.Join(",", player.GetEffectController().GetAbnormalEffects().Select(effect => effect.GetSkillId()))}]");
			SiegeLocationEntry info = Assert.Single(since.Where(packet => packet.PacketType == typeof(SM_SIEGE_LOCATION_INFO)).Select(ReadSiegeLocationUpdate));
			Assert.Equal(new SiegeLocationEntry(SiegeCapturedFortress, legion.GetLegionId(), SiegeRace.ASMODIANS.GetRaceId(), 0, asmodian ? 1 : 0, 0, 1), info);
			InfluenceRatio ratio = ReadInfluenceRatio(Assert.Single(since, packet => packet.PacketType == typeof(SM_INFLUENCE_RATIO)));
			Assert.Equal(0f, ratio.ElyosRate);
			Assert.Equal(6 / 100f, ratio.AsmodianRate);
			Assert.Equal(94 / 100f, ratio.BalaurRate);
		}
		// Rewards (Siege.sendRewardsToParticipants, FortressSiege.distributeLegionRewards): both attackers made AP inside the
		// vulnerable fortress, so both rank in the first grade (top 12): its item by mail, 1132 has no kinah grades and no GP.
		Assert.True(leaderPlayer.GetAbyssRank().GetAp() > apBeforeLeader);
		Assert.True(memberPlayer.GetAbyssRank().GetAp() > apBeforeMember);
		SiegeRewardMail[] firstGrade = [new(186000096, 3)];
		Assert.Equal(firstGrade, NewSiegeMail(memberPlayer, lettersBeforeMember));
		SiegeRewardMail[] legionMail = captured.GetLegionRewards().Where(reward => reward.GetItemId() != 182400001)
			.Select(reward => new SiegeRewardMail(reward.GetItemId(), reward.GetItemCount())).ToArray();
		Assert.Equal(5, legionMail.Length);
		Assert.Equal(firstGrade.Concat(legionMail).OrderBy(mail => mail.ItemId).ThenBy(mail => mail.Count),
			NewSiegeMail(leaderPlayer, lettersBeforeLeader).OrderBy(mail => mail.ItemId).ThenBy(mail => mail.Count));
		Assert.Equal(warehouseBefore + 5_000_000, legion.GetLegionWarehouse().GetKinah());
		LegionHistoryEntry occupation = Assert.Single(legion.GetHistory(LegionHistoryAction.Type.REWARD));
		Assert.Equal(LegionHistoryAction.OCCUPATION, occupation.Action);
		Assert.Equal("5000000", occupation.Name);
		Assert.Equal("1132", occupation.Description);

		member.BeginStep("s09", "repair-a-gate-of-1141");
		Assert.True(assaultSiege.GetSiegeLocation().IsVulnerable());
		// GateRepairAI (BA-005): a defender of the owning race talks to a gate stone, accepts both questions and pays the
		// fortress's repair fee; the stone's door gets round(max hp * gameserver.siege.door.repair.heal.percent) back, and the
		// stone is on cooldown for the data's cd. The door's damage is the director's.
		DoorRepairData repairData = Assert.IsType<DoorRepairData>(sieges.GetDoorRepairData(SiegeAssaultedFortress));
		SiegeNpc gateStone = Assert.Single(fixture.World.GetLocalSiegeNpcs(SiegeAssaultedFortress), npc => npc.GetSpawn().GetStaticId() == 204);
		Assert.Equal(SiegeModType.SIEGE, gateStone.GetSpawn().GetSiegeModType());
		DoorRepairStone repairStone = repairData.GetRepairStone(204);
		Creature gate = Assert.IsAssignableFrom<Creature>(gateStone.GetPosition().GetWorldMapInstance().GetObjectByStaticId(repairStone.GetDoorId()));
		int gateMaxHp = gate.GetLifeStats().GetMaxHp();
		int heal = (int)Math.Floor(gateMaxHp * SiegeConfig.DOOR_REPAIR_HEAL_PERCENT + 0.5);
		Assert.True(heal > 0);
		gate.GetLifeStats().SetCurrentHp(gateMaxHp - 2 * heal);
		Assert.Equal(0, ItemService.AddItem(memberPlayer, repairData.GetItemId(), repairData.GetCount()));
		await TeleportForSetupAsync(member, memberPlayer, Reshanta, gateStone.GetX() - 1, gateStone.GetY(), gateStone.GetZ(), token);
		DecodedBotServerPacket popup = await AskGateRepairAsync(member, gateStone, token);
		Assert.Equal(SiegeAssaultedFortress, popup.Get<int>("senderId"));
		Assert.Equal(0, popup.Get<int>("rangeOrCooldownSeconds"));
		DecodedBotServerPacket repaired = await member.WaitForPacketAsync(typeof(SM_SYSTEM_MESSAGE), token,
			packet => IsSystemMessage(packet, "STR_MSG_REPAIR_ABYSS_DOOR") || IsSystemMessage(packet, "STR_CANNOT_USE_DOOR_REPAIR_NOT_ENOUGH_FEE"));
		Assert.Equal("STR_MSG_REPAIR_ABYSS_DOOR", repaired.Get<string?>("name"));
		Assert.Equal([memberPlayer.GetName(), heal.ToString(System.Globalization.CultureInfo.InvariantCulture)], repaired.Get<string[]>("params"));
		Assert.Equal(gateMaxHp - heal, gate.GetLifeStats().GetCurrentHp());
		Assert.Equal(0, memberPlayer.GetInventory().GetItemCountByItemId(repairData.GetItemId()));
		await member.SendPacketAsync(member.Api.CloseDialog(gateStone.GetObjectId()), token);
		// The same stone again within its cooldown: the first question shows the cooldown, and the repair is refused.
		await AdvanceSiegeWorldToAsync(sessions, SystemClock.UtcNow().AddSeconds(2), token);
		popup = await AskGateRepairAsync(member, gateStone, token);
		Assert.InRange(popup.Get<int>("rangeOrCooldownSeconds"), 1, repairData.GetCd() / 1000);
		await member.WaitForPacketAsync(typeof(SM_SYSTEM_MESSAGE), token, packet => IsSystemMessage(packet, "STR_CANNOT_USE_DOOR_REPAIR_OUT_OF_COOLTIME"));
		Assert.Equal(gateMaxHp - heal, gate.GetLifeStats().GetCurrentHp());
		await member.SendPacketAsync(member.Api.CloseDialog(gateStone.GetObjectId()), token);

		// The observer of the assault stands inside 1141, away from its defenders, and sees the first wave land
		// (FortressAssault: the dredgion after the assault's delay, the first wave Rnd(170, 180) s later at this difficulty).
		member.BeginStep("s10", "watch-the-assault");
		await TeleportForSetupAsync(member, memberPlayer, Reshanta, 1306f, 1290f, 1579.02f, token);
		Assert.True(assaulted.IsInsideLocation(memberPlayer));
		int warpMark = member.PacketHistory.Count;
		while (!member.PacketHistory.Skip(warpMark).Any(packet => IsSystemMessage(packet, "STR_ABYSS_WARP_DRAGON")))
		{
			Assert.True(SystemClock.UtcNow() < start.AddMinutes(20), "the assault's first wave did not land by 17:20");
			Assert.True(assaultSiege.GetSiegeLocation().IsVulnerable(), "1141 fell before the assault's first wave");
			await AdvanceSiegeWorldToAsync(sessions, SystemClock.UtcNow().AddSeconds(1), token);
			await member.SynchronizeAsync(token);
		}
		// Waves 1 and 10: a TELEPORT assaulter by each siege NPC of 1141 that is neither legendary nor the artifact, for 40
		// rolls in 100, hating its NPC. 1141's assault data has one TELEPORT assaulter.
		int teleporter = Assert.Single(assaulted.GetTemplate().GetAssaultData().GetProcessedAssaulters()[AssaulterType.TELEPORT]).GetNpcId();
		SiegeNpc[] localNpcs = fixture.World.GetLocalSiegeNpcs(SiegeAssaultedFortress).ToArray();
		SiegeNpc[] assaulters = localNpcs.Where(npc => npc.GetSpawn().GetSiegeModType() == SiegeModType.ASSAULT).ToArray();
		int eligible = localNpcs.Count(npc => npc.GetSpawn().GetSiegeModType() == SiegeModType.SIEGE
			&& npc.GetRating() != NpcRating.LEGENDARY && npc.GetAbyssNpcType() != AbyssNpcType.ARTIFACT);
		Assert.InRange(assaulters.Length, 1, eligible + assaulters.Length);
		Assert.All(assaulters, npc =>
		{
			Assert.Equal(teleporter, npc.GetNpcId());
			Assert.Equal(SiegeRace.BALAUR, npc.GetSpawn().GetSiegeRace());
		});
		await SyncSiegeSessionsAsync(sessions, token);
		for (int i = 0; i < sessions.Length; i++)
		{
			// BalaurAssaultService.spawnDredgion: every player in the world sees the carrier.
			DecodedBotServerPacket[] since = sessions[i].PacketHistory.Skip(assaultMarks[i]).ToArray();
			Assert.Single(since, packet => packet.PacketType == typeof(SM_NPC_ASSEMBLER));
			Assert.Single(since, packet => IsSystemMessage(packet, "STR_ABYSS_CARRIER_SPAWN"));
			Assert.Equal(sessions[i] == member ? 1 : 0, since.Count(packet => IsSystemMessage(packet, "STR_ABYSS_WARP_DRAGON")));
		}
		marks = Marks(sessions);
		int[] lettersAfterCapture = sessions.Select(session => fixture.World.GetPlayer(session.CharacterId).GetMailbox().Size()).ToArray();

		// 18:00: the sieges that are still running end (siege_duration 3600).
		foreach (SimulationL0Session session in sessions) session.BeginStep("s11", "siege-end");
		await AdvanceSiegeWorldToAsync(sessions, end.AddSeconds(1), token);
		await SyncSiegeSessionsAsync(sessions, token);
		foreach (int id in ThursdayFortresses)
			Assert.False(sieges.IsSiegeInProgress(id), $"siege {id} still runs at 18:00");
		// Nobody took 1131, 1211 and 1251: onDefended keeps the Balaur (no occupy count for them, no world buff for any
		// player), the balance stays 0, and the Balaur peace groups come back.
		foreach (int id in new[] { SiegeResetFortress, 1211, 1251 })
		{
			FortressLocation fortress = sieges.GetFortress(id);
			Assert.Equal(SiegeRace.BALAUR, fortress.GetRace());
			Assert.False(fortress.IsVulnerable());
			Assert.Equal(0, fortress.GetOccupiedCount());
			AssertSiegeGroup(id, SiegeRace.BALAUR, SiegeModType.PEACE);
			AssertSiegeRow(id, "BALAUR", 0, 0, 0);
		}
		// 1141: the assault ended with its siege (BalaurAssaultService.onSiegeFinish); either the assaulters killed the
		// Asmodian boss and the Balaur took the fortress, or the Asmodians held it.
		Assert.True(assaultSiege.IsFinished());
		Assert.Null(BalaurAssaultService.GetInstance().GetFortressAssaultBySiegeId(SiegeAssaultedFortress));
		bool balaurCapture = assaultSiege.IsBossKilled();
		string assaultedName = assaulted.GetTemplate().GetL10n()!;
		Assert.False(assaulted.IsVulnerable());
		Assert.Equal(0, assaulted.GetLegionId());
		if (balaurCapture)
		{
			Assert.Equal(SiegeRace.BALAUR, assaulted.GetRace());
			Assert.Equal(0, assaulted.GetOccupiedCount());
			Assert.Equal(SiegeRace.BALAUR, sieges.GetFortressArtifact(SiegeAssaultedFortress).GetRace());
			AssertSiegeGroup(SiegeAssaultedFortress, SiegeRace.BALAUR, SiegeModType.PEACE);
			AssertSiegeRow(SiegeAssaultedFortress, "BALAUR", 0, 0, 0);
		}
		else
		{
			Assert.Equal(SiegeRace.ASMODIANS, assaulted.GetRace());
			Assert.Equal(1, assaulted.GetOccupiedCount());
			AssertSiegeGroup(SiegeAssaultedFortress, SiegeRace.ASMODIANS, SiegeModType.PEACE);
			AssertSiegeRow(SiegeAssaultedFortress, "ASMODIANS", 0, 1, -1);
		}
		for (int i = 0; i < sessions.Length; i++)
		{
			Player player = fixture.World.GetPlayer(sessions[i].CharacterId);
			bool asmodian = player.GetRace() == Race.ASMODIANS;
			DecodedBotServerPacket[] since = sessions[i].PacketHistory.Skip(marks[i]).ToArray();
			var states = since.Where(packet => packet.PacketType == typeof(SM_SIEGE_LOCATION_STATE)).Select(ReadSiegeLocationState).ToArray();
			// onDefended broadcasts the state; a capture broadcasts the location instead.
			foreach (int id in new[] { SiegeResetFortress, 1211, 1251 })
				Assert.Contains(new SiegeLocationState(id, 0), states);
			if (balaurCapture)
			{
				// onCapture: the old owners hear STR_ABYSS_CASTLE_TAKEN, everyone else the winner's STR_ABYSS_WIN_CASTLE;
				// the assault's end tells the players inside that the boss fell (STR_ABYSS_DRAGON_BOSS_KILLED).
				DecodedBotServerPacket taken = Assert.Single(since, packet => IsSystemMessage(packet, asmodian ? "STR_ABYSS_CASTLE_TAKEN" : "STR_ABYSS_WIN_CASTLE"));
				Assert.Equal([(asmodian ? SiegeRace.ASMODIANS : SiegeRace.BALAUR).GetL10n()!, assaultedName], taken.Get<string[]>("params"));
				Assert.Equal(sessions[i] == member ? 1 : 0, since.Count(packet => IsSystemMessage(packet, "STR_ABYSS_DRAGON_BOSS_KILLED")));
				Assert.DoesNotContain(since, packet => IsSystemMessage(packet, "STR_CASTLE_WIN_BUFF_ON"));
				Assert.Contains(since.Where(packet => packet.PacketType == typeof(SM_SIEGE_LOCATION_INFO)).Select(ReadSiegeLocationUpdate),
					info => info == new SiegeLocationEntry(SiegeAssaultedFortress, 0, SiegeRace.BALAUR.GetRaceId(), 0, 0, 0, 0));
				// gameserver.siege.reward.balaur.victory is off: a Balaur victory or defence rewards nobody.
				Assert.Equal(lettersAfterCapture[i], player.GetMailbox().Size());
			}
			else
			{
				Assert.Contains(new SiegeLocationState(SiegeAssaultedFortress, 0), states);
				Assert.Equal(asmodian ? 1 : 0, since.Count(packet => IsSystemMessage(packet, "STR_CASTLE_DEFENCE_WIN_BUFF_ON")));
			}
		}

		// The 18:00 broadcast: the next state of Vorgaltem Citadel and Crimson Temple (Thursday 19:00) is vulnerable.
		for (int i = 0; i < sessions.Length; i++)
		{
			FortressStatus status = AssertHourlyBroadcast(sessions[i], marks[i], fortressIds);
			Assert.Equal(fortressIds.ToDictionary(id => id, id => GelkmarosFortresses.Contains(id) ? 1 : 0), status.NextStates);
		}

		// 19:00, the Gelkmaros fortresses: the Asmodian group takes Vorgaltem Citadel (3011), whose grades pay kinah and GP and
		// whose legion GP is 1000, while Crimson Temple (3021) runs its 2400 seconds and stays Balaur.
		foreach (SimulationL0Session session in sessions) session.BeginStep("s12", "gelkmaros-preparation-and-start");
		await AdvanceSiegeWorldToAsync(sessions, end.AddMinutes(55).AddSeconds(1), token);
		foreach (int id in GelkmarosFortresses)
		{
			Assert.False(sieges.IsSiegeInProgress(id));
			Assert.Equal(SiegeRace.BALAUR, sieges.GetFortress(id).GetRace()); // no occupy reset for the Balaur
		}
		await AdvanceSiegeWorldToAsync(sessions, end.AddHours(1).AddSeconds(1), token);
		foreach (int id in GelkmarosFortresses)
		{
			Assert.True(sieges.IsSiegeInProgress(id), $"siege {id} did not start at 19:00");
			AssertSiegeGroup(id, SiegeRace.BALAUR, SiegeModType.SIEGE);
		}
		FortressLocation citadel = sieges.GetFortress(VorgaltemCitadel);
		FortressSiege citadelSiege = Assert.IsType<FortressSiege>(sieges.GetSiege(VorgaltemCitadel));
		SiegeNpc citadelBoss = citadelSiege.GetBoss();
		Assert.Equal(65, citadelBoss.GetLevel());
		OutpostLocation outpost = sieges.GetOutpost(SilenteraOutpost);
		Assert.Equal(SiegeRace.ASMODIANS, outpost.GetRace());
		Assert.False(sieges.IsSiegeInProgress(SilenteraOutpost));
		int[] outpostNpcsBefore = fixture.World.GetLocalSiegeNpcs(SilenteraOutpost).Select(npc => npc.GetObjectId()).Order().ToArray();
		AssertSiegeGroup(SilenteraOutpost, SiegeRace.ASMODIANS, SiegeModType.PEACE);

		foreach (SimulationL0Session attacker in new[] { leader, member })
			attacker.BeginStep("s13", "take-vorgaltem-citadel");
		foreach ((SimulationL0Session attacker, Player player, float offset) in new[] { (leader, leaderPlayer, -1f), (member, memberPlayer, 1f) })
		{
			if (player.IsDead())
			{
				// A death in 1141's assault: the obelisk revive (bind point), as a player would.
				await attacker.SendPacketAsync(attacker.Api.Revive(), token);
				await attacker.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == attacker.CharacterId);
				attacker.AcceptTeleportPosition();
			}
			await TeleportForSetupAsync(attacker, player, citadel.GetWorldId(), citadelBoss.GetX() + offset, citadelBoss.GetY(), citadelBoss.GetZ(), token);
			Assert.False(player.IsDead());
			Assert.True(citadel.IsInsideLocation(player));
		}
		marks = Marks(sessions);
		long warehouseBeforeCitadel = legion.GetLegionWarehouse().GetKinah();
		int lettersLeader = leaderPlayer.GetMailbox().Size(), lettersMember = memberPlayer.GetMailbox().Size();
		int gpLeader = leaderPlayer.GetAbyssRank().GetCurrentGP(), gpMember = memberPlayer.GetAbyssRank().GetCurrentGP();
		// One round of the two attackers' swings (1 each against his defence): the level-65 boss falls before he answers.
		citadelBoss.GetLifeStats().SetCurrentHp(2);
		foreach ((SimulationL0Session attacker, Player player) in new[] { (leader, leaderPlayer), (member, memberPlayer) })
		{
			await attacker.SendPacketAsync(attacker.Api.Target(citadelBoss.GetObjectId()), token);
			await attacker.SendPacketAsync(attacker.Api.Attack(citadelBoss.GetObjectId(), player.GetGameStats().GetAttackSpeed().GetCurrent()), token);
		}
		for (int round = 0; round < 20 && !citadelBoss.IsDead(); round++)
		{
			await AdvanceSiegeWorldToAsync(sessions, SystemClock.UtcNow().AddMilliseconds(500), token);
			foreach ((SimulationL0Session attacker, Player player) in new[] { (leader, leaderPlayer), (member, memberPlayer) })
			{
				int speed = player.GetGameStats().GetAttackSpeed().GetCurrent();
				if (!citadelBoss.IsDead() && !player.IsDead() && attacker.Api.Timing.TimeUntilAttack(speed) <= TimeSpan.Zero)
					await attacker.SendPacketAsync(attacker.Api.Attack(citadelBoss.GetObjectId(), speed), token);
			}
		}
		Assert.True(citadelBoss.IsDead(), $"the citadel's boss survived with {citadelBoss.GetLifeStats().GetCurrentHp()} hp");
		Assert.False(leaderPlayer.IsDead() || memberPlayer.IsDead(), "an attacker died at the citadel");
		await AdvanceSiegeWorldToAsync(sessions, SystemClock.UtcNow().AddSeconds(1), token);
		await SyncSiegeSessionsAsync(sessions, token);

		Assert.True(citadelSiege.IsFinished() && citadelSiege.IsBossKilled());
		Assert.Equal(SiegeRace.ASMODIANS, citadel.GetRace());
		Assert.Equal(legion.GetLegionId(), citadel.GetLegionId());
		Assert.Equal(1, citadel.GetOccupiedCount());
		AssertSiegeGroup(VorgaltemCitadel, SiegeRace.ASMODIANS, SiegeModType.PEACE);
		AssertSiegeRow(VorgaltemCitadel, "ASMODIANS", legion.GetLegionId(), 1, -1);
		// The first grade (top 5): its item and kinah grade 50000000 / 5 by mail, and 200 GP; the legion's 1000 GP split over
		// its two members with AP, capped at gameserver.siege.legion.gp.cap_per_member (200); the legion's kinah reward in its
		// warehouse and its three item rewards by mail to the brigade general.
		SiegeReward firstCitadelGrade = citadel.GetRewards()[0];
		Assert.Equal(5, firstCitadelGrade.GetTop());
		long gradeKinah = (long)Math.Floor(citadel.GetTemplate().GetKinahRewardByRewardLevel(0) / (float)firstCitadelGrade.GetTop() + 0.5f);
		Assert.Equal(10_000_000, gradeKinah);
		int legionGp = Math.Min((int)Math.Floor(citadel.GetLegionGp() / 2f + 0.5f), SiegeConfig.LEGION_GP_CAP_PER_MEMBER);
		Assert.Equal(200, legionGp);
		Assert.Equal(gpLeader + firstCitadelGrade.GetGpForWin() + legionGp, leaderPlayer.GetAbyssRank().GetCurrentGP());
		Assert.Equal(gpMember + firstCitadelGrade.GetGpForWin() + legionGp, memberPlayer.GetAbyssRank().GetCurrentGP());
		var gradeMail = new SiegeKinahMail(firstCitadelGrade.GetItemId(), firstCitadelGrade.GetItemCount(), gradeKinah);
		Assert.Equal([gradeMail], NewSiegeKinahMail(memberPlayer, lettersMember));
		SiegeKinahMail[] citadelLegionMail = citadel.GetLegionRewards().Where(reward => reward.GetItemId() != 182400001)
			.Select(reward => new SiegeKinahMail(reward.GetItemId(), reward.GetItemCount(), 0)).ToArray();
		Assert.Equal(3, citadelLegionMail.Length);
		Assert.Equal(citadelLegionMail.Append(gradeMail).OrderBy(mail => mail.ItemId),
			NewSiegeKinahMail(leaderPlayer, lettersLeader).OrderBy(mail => mail.ItemId));
		long legionKinah = citadel.GetLegionRewards().Single(reward => reward.GetItemId() == 182400001).GetItemCount();
		Assert.Equal(warehouseBeforeCitadel + legionKinah, legion.GetLegionWarehouse().GetKinah());
		Assert.Equal([("5000000", "1132"), ("10000000", "3011")],
			legion.GetHistory(LegionHistoryAction.Type.REWARD).Select(entry => (entry.Name, entry.Description)).OrderBy(entry => entry.Description));
		for (int i = 0; i < sessions.Length; i++)
		{
			DecodedBotServerPacket[] since = sessions[i].PacketHistory.Skip(marks[i]).ToArray();
			DecodedBotServerPacket winner = Assert.Single(since, packet => IsSystemMessage(packet, "STR_ABYSS_GUILD_WIN_CASTLE"));
			Assert.Equal([legionName, citadel.GetTemplate().GetL10n()!], winner.Get<string[]>("params"));
			bool asmodian = fixture.World.GetPlayer(sessions[i].CharacterId).GetRace() == Race.ASMODIANS;
			Assert.Equal(asmodian ? 2 : 0, since.Count(packet => IsSystemMessage(packet, "STR_MSG_GLORY_POINT_GAIN")));
		}
		// The outposts: Crimson Temple, which Silentera's eastern outpost also depends on, is still vulnerable, so nothing changes.
		Assert.Equal(outpostNpcsBefore, fixture.World.GetLocalSiegeNpcs(SilenteraOutpost).Select(npc => npc.GetObjectId()).Order().ToArray());

		// 19:40: Crimson Temple runs out its 2400 seconds and stays Balaur; now neither fortress of the outpost is vulnerable
		// and they are not both the outpost's siege race (Elyos for 3111), so the outpost's own peace group is spawned
		// again and every player hears the Silentera state.
		marks = Marks(sessions);
		foreach (SimulationL0Session session in sessions) session.BeginStep("s14", "crimson-temple-end");
		await AdvanceSiegeWorldToAsync(sessions, end.AddHours(1).AddMinutes(40).AddSeconds(1), token);
		await SyncSiegeSessionsAsync(sessions, token);
		Assert.False(sieges.IsSiegeInProgress(CrimsonTemple));
		Assert.Equal(SiegeRace.BALAUR, sieges.GetFortress(CrimsonTemple).GetRace());
		AssertSiegeGroup(CrimsonTemple, SiegeRace.BALAUR, SiegeModType.PEACE);
		AssertSiegeRow(CrimsonTemple, "BALAUR", 0, 0, 0);
		Assert.False(sieges.IsSiegeInProgress(SilenteraOutpost));
		AssertSiegeGroup(SilenteraOutpost, SiegeRace.ASMODIANS, SiegeModType.PEACE);
		Assert.Empty(fixture.World.GetLocalSiegeNpcs(SilenteraOutpost).Select(npc => npc.GetObjectId()).Intersect(outpostNpcsBefore));
		for (int i = 0; i < sessions.Length; i++)
		{
			DecodedBotServerPacket[] since = sessions[i].PacketHistory.Skip(marks[i]).ToArray();
			Assert.Single(since, packet => packet.PacketType == typeof(SM_RIFT_ANNOUNCE));
			Assert.Contains(new SiegeLocationState(CrimsonTemple, 0), since.Where(packet => packet.PacketType == typeof(SM_SIEGE_LOCATION_STATE)).Select(ReadSiegeLocationState));
		}

		// The siege log (SIEGE_LOG, Java's siege.log): Java's text of the lines these sieges write, gameserver.log.siege on.
		string[] siegeLog = fixture.LogCapture.Entries.Skip(firstLogEntry).Where(entry => entry.Category == "SIEGE_LOG"
			&& entry.Level == Microsoft.Extensions.Logging.LogLevel.Information).Select(entry => entry.Message).ToArray();
		string legionText = "Legion [id=" + legion.GetLegionId() + ", name=" + legionName + "]";
		string attackers = "[" + string.Join(", ", new[] { leader.CharacterId, member.CharacterId }.Order()) + "]";
		foreach (int id in ThursdayFortresses)
			Assert.Contains($"FortressSiege [locationId={id}]: Siege started. Race: {(id == SiegeAssaultedFortress ? "ASMODIANS" : "BALAUR")}, legion ID: 0", siegeLog);
		if (!naturalAssault)
			Assert.Contains("Scheduled assault of FortressSiege [locationId=1141] in 60 seconds", siegeLog);
		Assert.Contains("Initialized fortress assault on [locationID=1141] with [difficulty=0.34333333] [factionBalance=1.0] [influence=0.03] [difficultyMultiplier=1.0]", siegeLog);
		Assert.Contains($"Gate Repair Stone with staticId: 204 siege: 1141 activated by Player [id={member.CharacterId}, name={memberPlayer.GetName()}] (race: ASMODIANS) to heal door with staticId: {repairStone.GetDoorId()} by {heal}", siegeLog);
		Assert.Contains($"FortressSiege [locationId=1132]: Siege finished. Old race: BALAUR, legion ID: 0 -> New race: ASMODIANS, legion ID: {legion.GetLegionId()}", siegeLog);
		Assert.Contains($"FortressSiege [locationId=1132]: Added 5000000 kinah to the legion warehouse and sent 5 legion rewards to brigade general {leaderPlayer.GetName()} of {legionText} (see sysmail.log)", siegeLog);
		foreach (int id in new[] { SiegeResetFortress, 1211, 1251 })
			Assert.Contains($"FortressSiege [locationId={id}]: Siege finished. No winner found. Race: BALAUR, legion ID: 0", siegeLog);
		Assert.Contains(balaurCapture ? "FortressSiege [locationId=1141]: Siege finished. Old race: ASMODIANS, legion ID: 0 -> New race: BALAUR, legion ID: 0"
			: "FortressSiege [locationId=1141]: Siege finished. No winner found. Race: ASMODIANS, legion ID: 0", siegeLog);
		Assert.Contains($"FortressSiege [locationId=3011]: Siege finished. Old race: BALAUR, legion ID: 0 -> New race: ASMODIANS, legion ID: {legion.GetLegionId()}", siegeLog);
		// The two attackers made the same AP, so the order inside the brackets is the counters' and the sets' own.
		string[] gpLines = siegeLog.Where(line => line.StartsWith("FortressSiege [locationId=3011]: Distributed ", StringComparison.Ordinal)).ToArray();
		Assert.Equal(2, gpLines.Length);
		Assert.Single(gpLines, line => SameIds(line, "FortressSiege [locationId=3011]: Distributed 200 winner GP each, to the following players (rank 1): ", attackers));
		Assert.Single(gpLines, line => SameIds(line, $"FortressSiege [locationId=3011]: Distributed 200 GP each, to the following members of {legionText}: ", attackers));
		Assert.Contains($"FortressSiege [locationId=3011]: Added 10000000 kinah to the legion warehouse and sent 3 legion rewards to brigade general {leaderPlayer.GetName()} of {legionText} (see sysmail.log)", siegeLog);
		Assert.Contains("FortressSiege [locationId=3021]: Siege finished. No winner found. Race: BALAUR, legion ID: 0", siegeLog);
		policy.AssertClean();
	}

	/// <summary>Talks to a gate stone and accepts GateRepairAI's two questions; returns the first.</summary>
	private static async Task<DecodedBotServerPacket> AskGateRepairAsync(SimulationL0Session session, SiegeNpc gateStone, CancellationToken token)
	{
		await session.SendPacketAsync(session.Api.TalkTo(gateStone.GetObjectId()), token);
		DecodedBotServerPacket popup = await session.WaitForPacketAsync(typeof(SM_QUESTION_WINDOW), token,
			packet => packet.Get<int>("code") == SM_QUESTION_WINDOW.STR_ASK_DOOR_REPAIR_POPUPDIALOG);
		await session.SendPacketAsync(GameClientPackets.QuestionResponse(SM_QUESTION_WINDOW.STR_ASK_DOOR_REPAIR_POPUPDIALOG, 1, popup.Get<int>("senderId")), token);
		DecodedBotServerPacket fee = await session.WaitForPacketAsync(typeof(SM_QUESTION_WINDOW), token,
			packet => packet.Get<int>("code") == SM_QUESTION_WINDOW.STR_ASK_DOOR_REPAIR_DO_YOU_ACCEPT_REPAIR);
		Assert.Equal(gateStone.GetObjectId(), fee.Get<int>("senderId"));
		await session.SendPacketAsync(GameClientPackets.QuestionResponse(SM_QUESTION_WINDOW.STR_ASK_DOOR_REPAIR_DO_YOU_ACCEPT_REPAIR, 1, fee.Get<int>("senderId")), token);
		return popup;
	}

	/// <summary>True when <paramref name="line"/> is <paramref name="prefix"/> and a bracketed list of the same ids as <paramref name="ids"/>.</summary>
	private static bool SameIds(string line, string prefix, string ids) =>
		line.StartsWith(prefix, StringComparison.Ordinal) && line[prefix.Length..].StartsWith('[') && line.EndsWith(']')
		&& line[(prefix.Length + 1)..^1].Split(", ").Order().SequenceEqual(ids[1..^1].Split(", ").Order());

	private static bool IsSystemMessage(DecodedBotServerPacket packet, string name) =>
		packet.PacketType == typeof(SM_SYSTEM_MESSAGE) && packet.Get<string?>("name") == name;

	/// <summary>
	/// Ulsaruk's health when the attackers start. Two level-50 Gladiators with their starter weapons hit him for 1 a swing
	/// (his defence), so he falls in the second round. At any health they cannot take in a few seconds his blows killed
	/// one attacker first, and his area attack (17552, five times its damage on NPCs by FortressProtectorNpcAI) struck the
	/// two lead scouts beside him, who struck back and killed him: Java's TribeRelationService.checkSiegeRelation makes
	/// every siege NPC whose base tribe is GENERAL_DRAGON hostile to every creature, so the target filter and the scouts'
	/// aggro lists take each other as enemies, exactly as in Java.
	/// </summary>
	private const int SiegeBossHealth = 4;

	private static int[] Marks(IReadOnlyList<SimulationL0Session> sessions) => sessions.Select(session => session.PacketHistory.Count).ToArray();

	private static async Task SyncSiegeSessionsAsync(IEnumerable<SimulationL0Session> sessions, CancellationToken token)
	{
		foreach (SimulationL0Session session in sessions)
			await session.SynchronizeAsync(token);
	}

	/// <summary>
	/// Moves the world's clock to <paramref name="target"/> a second at a time, serializing every bot's packets after each
	/// second: several packets read the siege state while they are written (harness rule 7).
	/// </summary>
	private static async Task AdvanceSiegeWorldToAsync(IReadOnlyList<SimulationL0Session> sessions, DateTimeOffset target, CancellationToken token)
	{
		while (SystemClock.UtcNow() < target)
		{
			TimeSpan step = target - SystemClock.UtcNow();
			if (step > TimeSpan.FromSeconds(1))
				step = TimeSpan.FromSeconds(1);
			await sessions[0].AdvanceAsync(step, token);
			foreach (SimulationL0Session session in sessions.Skip(1))
				await session.DrainServerPacketsAsync(token);
		}
	}

	private void AssertSiegeGroup(int locationId, SiegeRace race, SiegeModType mod)
	{
		int expected = (DataManager.SPAWNS_DATA.GetSiegeSpawnsByLocId(locationId) ?? []).SelectMany(group => group.GetSpawnTemplates())
			.OfType<SiegeSpawnTemplate>().Count(spawn => spawn.GetSiegeRace() == race && spawn.GetSiegeModType() == mod);
		Assert.True(expected > 0, $"location {locationId} has no {race} {mod} group");
		ICollection<SiegeNpc> npcs = fixture.World.GetLocalSiegeNpcs(locationId);
		var wrong = npcs.Where(npc => npc.GetSpawn().GetSiegeRace() != race || npc.GetSpawn().GetSiegeModType() != mod).ToArray();
		Assert.True(npcs.Count == expected && wrong.Length == 0,
			$"location {locationId} holds {npcs.Count} siege NPCs, its {race} {mod} group has {expected}; others: " +
			string.Join(", ", wrong.Take(10).Select(npc => $"{npc.GetNpcId()} {npc.GetSpawn().GetSiegeRace()} {npc.GetSpawn().GetSiegeModType()}")));
		Assert.All(npcs, npc => Assert.True(npc.IsSpawned()));
	}

	private static void AssertSiegeRow(int locationId, string race, int legionId, int occupyCount, int factionBalance)
	{
		using var connection = DatabaseFactory.GetConnection();
		connection.Open();
		using var command = connection.CreateCommand();
		command.CommandText = "SELECT race, legion_id, occupy_count, faction_balance FROM siege_locations WHERE id = @id";
		command.Parameters.AddWithValue("@id", locationId);
		using var reader = command.ExecuteReader();
		Assert.True(reader.Read(), $"siege_locations has no row {locationId}");
		Assert.Equal((race, legionId, occupyCount, factionBalance),
			(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3)));
		Assert.False(reader.Read());
	}

	private static SiegeKinahMail[] NewSiegeKinahMail(Player player, int before)
	{
		Letter[] letters = player.GetMailbox().GetLetters().OrderBy(letter => letter.GetObjectId()).Skip(before).ToArray();
		Assert.All(letters, letter => Assert.Equal("$$ABYSS_REWARD_MAIL", letter.GetSenderName()));
		return letters.Select(letter => new SiegeKinahMail(letter.GetAttachedItem().GetItemId(), letter.GetAttachedItem().GetItemCount(), letter.GetAttachedKinah())).ToArray();
	}

	private static SiegeRewardMail[] NewSiegeMail(Player player, int before)
	{
		Letter[] letters = player.GetMailbox().GetLetters().OrderBy(letter => letter.GetObjectId()).Skip(before).ToArray();
		Assert.All(letters, letter =>
		{
			Assert.Equal("$$ABYSS_REWARD_MAIL", letter.GetSenderName());
			Assert.Equal(0, letter.GetAttachedKinah());
			Assert.NotNull(letter.GetAttachedItem());
		});
		return letters.Select(letter => new SiegeRewardMail(letter.GetAttachedItem().GetItemId(), letter.GetAttachedItem().GetItemCount())).ToArray();
	}

	/// <summary>
	/// The hourly job of initSieges: for each fortress SM_FORTRESS_INFO(id, false), then SM_FORTRESS_STATUS, then for each
	/// fortress SM_FORTRESS_INFO(id, true).
	/// </summary>
	private static FortressStatus AssertHourlyBroadcast(SimulationL0Session session, int mark, int[] fortressIds)
	{
		DecodedBotServerPacket[] since = session.PacketHistory.Skip(mark).ToArray();
		int status = Array.FindIndex(since, packet => packet.PacketType == typeof(SM_FORTRESS_STATUS));
		Assert.True(status >= fortressIds.Length, "no hourly SM_FORTRESS_STATUS after its SM_FORTRESS_INFO run");
		Assert.Equal(-1, Array.FindIndex(since, status + 1, packet => packet.PacketType == typeof(SM_FORTRESS_STATUS)));
		DecodedBotServerPacket[] packets = since[(status - fortressIds.Length)..(status + fortressIds.Length + 1)];
		Assert.All(packets.Where((_, index) => index != fortressIds.Length), packet => Assert.Equal(typeof(SM_FORTRESS_INFO), packet.PacketType));
		var before = packets.Take(fortressIds.Length).Select(ReadFortressInfo).ToArray();
		var after = packets.Skip(fortressIds.Length + 1).Select(ReadFortressInfo).ToArray();
		Assert.Equal(fortressIds, before.Select(info => info.LocationId));
		Assert.All(before, info => Assert.False(info.TeleportStatus));
		Assert.Equal(fortressIds, after.Select(info => info.LocationId));
		Assert.All(after, info => Assert.True(info.TeleportStatus));
		return ReadFortressStatus(packets[fortressIds.Length]);
	}

	private sealed record SiegeRewardMail(int ItemId, long Count);
	private sealed record SiegeKinahMail(int ItemId, long Count, long Kinah);
	private sealed record SiegeLocationState(int LocationId, int State);
	private sealed record FortressInfo(int LocationId, bool TeleportStatus);
	private sealed record SiegeLocationEntry(int LocationId, int LegionId, int Race, int Vulnerable, int CanTeleport, int NextState, int OccupiedCount);
	private sealed record InfluenceRatio(int SecondsUntilNextState, float ElyosRate, float AsmodianRate, float BalaurRate, Dictionary<int, float[]> Worlds);
	private sealed record FortressStatus(int SecondsUntilNextState, float ElyosRate, float AsmodianRate, float BalaurRate,
		Dictionary<int, float[]> Worlds, Dictionary<int, int> NextStates);

	// The bot keeps these packets as raw bodies (they are not in its decoder inventory); their layouts are Java's writeImpl.
	private static byte[] SiegeBody(DecodedBotServerPacket packet) => Convert.FromHexString(packet.Get<string>("bodyHex"));

	// SM_SIEGE_LOCATION_STATE: D locationId, C state.
	private static SiegeLocationState ReadSiegeLocationState(DecodedBotServerPacket packet)
	{
		var body = new SiegeBodyReader(SiegeBody(packet));
		var state = new SiegeLocationState(body.D(), body.C());
		body.End();
		return state;
	}

	// SM_FORTRESS_INFO: D locationId, C teleportStatus.
	private static FortressInfo ReadFortressInfo(DecodedBotServerPacket packet)
	{
		var body = new SiegeBodyReader(SiegeBody(packet));
		var info = new FortressInfo(body.D(), body.C() != 0);
		body.End();
		return info;
	}

	// SM_SIEGE_LOCATION_INFO(loc): C 1, H 1, then the location: D id, D legion, C emblem, C emblem type, H 0, C C C C colours,
	// C race, C vulnerable (2 or 0), C canTeleport, C next state, H 0, H 0, D timer, D server id, D 0, D occupied count.
	private static SiegeLocationEntry ReadSiegeLocationUpdate(DecodedBotServerPacket packet) => Assert.Single(ReadSiegeLocationInfo(packet, 1));

	// SM_SIEGE_LOCATION_INFO: C info type, H count, then the locations as in ReadSiegeLocationUpdate.
	private static SiegeLocationEntry[] ReadSiegeLocationInfo(DecodedBotServerPacket packet, int infoType)
	{
		var body = new SiegeBodyReader(SiegeBody(packet));
		Assert.Equal(infoType, body.C());
		int count = body.H();
		var entries = new SiegeLocationEntry[count];
		for (int i = 0; i < count; i++)
		{
			int id = body.D(), legionId = body.D();
			body.Skip(1 + 1 + 2 + 4);
			int race = body.C(), vulnerable = body.C(), canTeleport = body.C(), nextState = body.C();
			body.Skip(2 + 2 + 4 + 4 + 4);
			entries[i] = new SiegeLocationEntry(id, legionId, race, vulnerable, canTeleport, nextState, body.D());
		}
		body.End();
		return entries;
	}

	// SM_SHIELD_EFFECT: H count, each D location, C under shield.
	private static Dictionary<int, bool> ReadShieldEffect(DecodedBotServerPacket packet)
	{
		var body = new SiegeBodyReader(SiegeBody(packet));
		int count = body.H();
		var shields = new Dictionary<int, bool>();
		for (int i = 0; i < count; i++)
			shields.Add(body.D(), body.C() != 0);
		body.End();
		return shields;
	}

	// SM_ABYSS_ARTIFACT_INFO3: H count, each D location * 10 + 1, C status, D 0.
	private static int[] ReadArtifactInfo(DecodedBotServerPacket packet)
	{
		var body = new SiegeBodyReader(SiegeBody(packet));
		int count = body.H();
		var ids = new int[count];
		for (int i = 0; i < count; i++)
		{
			ids[i] = body.D();
			body.C();
			Assert.Equal(0, body.D());
		}
		body.End();
		return ids;
	}

	// SM_INFLUENCE_RATIO: D seconds, F F F, H worlds, each D world, F F F.
	private static InfluenceRatio ReadInfluenceRatio(DecodedBotServerPacket packet)
	{
		var body = new SiegeBodyReader(SiegeBody(packet));
		var ratio = new InfluenceRatio(body.D(), body.F(), body.F(), body.F(), ReadWorlds(ref body));
		body.End();
		return ratio;
	}

	// SM_FORTRESS_STATUS: C 1, D seconds, F F F, H worlds (D, F F F), H fortresses (D id, C next state).
	private static FortressStatus ReadFortressStatus(DecodedBotServerPacket packet)
	{
		var body = new SiegeBodyReader(SiegeBody(packet));
		Assert.Equal(1, body.C());
		int seconds = body.D();
		float elyos = body.F(), asmodians = body.F(), balaur = body.F();
		Dictionary<int, float[]> worlds = ReadWorlds(ref body);
		int count = body.H();
		var states = new Dictionary<int, int>();
		for (int i = 0; i < count; i++)
			states.Add(body.D(), body.C());
		body.End();
		return new FortressStatus(seconds, elyos, asmodians, balaur, worlds, states);
	}

	private static Dictionary<int, float[]> ReadWorlds(ref SiegeBodyReader body)
	{
		int count = body.H();
		var worlds = new Dictionary<int, float[]>();
		for (int i = 0; i < count; i++)
			worlds.Add(body.D(), [body.F(), body.F(), body.F()]);
		return worlds;
	}

	private struct SiegeBodyReader(byte[] body)
	{
		private int position;
		public int C() => body[position++];
		public int H() { int value = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(position)); position += 2; return value; }
		public int D() { int value = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(position)); position += 4; return value; }
		public float F() { float value = BinaryPrimitives.ReadSingleLittleEndian(body.AsSpan(position)); position += 4; return value; }
		public void Skip(int count) => position += count;
		public readonly void End() => Assert.Equal(body.Length, position);
	}
}
