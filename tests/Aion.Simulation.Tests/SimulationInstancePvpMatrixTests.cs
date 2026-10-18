using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Autogroup;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Portal;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.Simulation.Tests;

/// <summary>
/// P11-01, scenario INSTANCES-PVP: the instance entry matrix of the auto-group instances, which a player enters through the
/// queue (<c>CM_AUTO_GROUP</c>, <c>AutoGroupService</c>) and not through a portal, played on the virtual clock from the process's
/// own epoch, Saturday 2026-10-17 19:55 UTC (<c>reset-INSTANCES-PVP</c>), with 24 ordinary Elyos and 24 ordinary Asmodian
/// Gladiators. Every row (<see cref="AutoInstanceRows"/>) is matched as Java matches it: a battleground (the three dredgions,
/// Kamar Battlefield, Engulfed Ophidan Bridge, Iron Wall Warfront, Idgel Dome; <c>AutoPvpInstance</c>) when both races fill their
/// side, an arena or a training ground (<c>AutoPvPFFAInstance</c>, <c>AutoHarmonyInstance</c>) when its places are full. Each
/// player registers alone (window 100, a new group): from the icon a battleground's opening registration gives
/// (<c>PeriodicInstanceManager</c>, window 104), from an arena's gate in Marchutan Priory (<c>PortalDialogAI</c>'s
/// <c>INSTANCE_PARTY_MATCH</c>), or, for a training ground, whose gate 4.8's data never spawns, with the registration that gate's
/// window sends. The match's entry window comes, every player enters (window 102: <c>AutoInstance.onPressEnter</c> counts the
/// entry, the ticket is taken), the handler's create path must log nothing, and every player leaves by
/// <c>CM_INSTANCE_LEAVE</c>, which each of these handlers implements (<c>BasicPvpInstance</c>, <c>DredgionInstance</c>,
/// <c>PvPArenaInstance</c>): the exit, and the instance destroyed when the last one is out
/// (<c>AutoGroupService.destroyIfPossible</c>). After as many matches as a battleground's cooltime row allows, the next
/// registration is refused (<c>STR_MSG_CANNOT_MAKE_INSTANCE_COOL_TIME</c>); after its reset (Sunday 09:00 for the daily rows, for
/// Kamar's Tuesday-Thursday-Saturday-Sunday and Engulfed Ophidan Bridge's Monday-Wednesday-Friday-Sunday) a match is made again.
/// The arenas and training grounds count nothing (their rows' maxcount is 0) and are matched twice in a row. Iron Wall Warfront
/// is matched, but Java 4.8 ports nobody into it (<see cref="PlayBrokenAutoEntryAsync"/>), so its limit and reset are not reached.
/// <para>
/// The registrations open on autogroup.properties' calendar where it falls in the run (the dredgions and Kamar Battlefield at
/// Saturday 20:00, Idgel Dome at 23:00, Iron Wall Warfront at Sunday 00:00, the dredgions and Engulfed Ophidan Bridge at Sunday
/// 12:00) and otherwise by the GM's <c>//instance open</c> (<c>PeriodicInstanceManager.openRegistration</c>, as the plan's phase
/// 11 allows): Engulfed Ophidan Bridge on Saturday, Kamar Battlefield and Idgel Dome on Sunday.
/// </para>
/// <para>
/// Director setup, recorded here as the plan asks: every subject is made a Gladiator and given each row's level (46 for
/// Baranath Dredgion and the arenas of discipline, chaos and harmony and their training grounds, 51 for Chantra Dredgion, 56 for
/// Terath Dredgion and the Arena of Glory, 61 for the battlegrounds of 61 to 65); the arena tickets are given (186000135,
/// 186000184, three 186000185 for each entry); the arena subjects are placed beside the gates by setup teleports, with the
/// monsters aggressive to them within 40 m of the gate deleted first; everyone is logged out from Sunday 00:00 to 11:58.
/// </para>
/// </summary>
public sealed partial class SimulationFastScenarioTests
{
	internal static readonly DateTimeOffset InstancesPvpEpoch = new(2026, 10, 17, 19, 55, 0, TimeSpan.Zero);

	private enum AutoInstanceKind
	{
		/// <summary>AutoPvpInstance: both races' sides full.</summary>
		Battleground,
		/// <summary>AutoPvPFFAInstance: every place full, each player alone.</summary>
		FreeForAll,
		/// <summary>AutoHarmonyInstance: two harmony groups of three.</summary>
		Harmony,
	}

	/// <summary>One auto-group instance handler: its map, its class, the queue's mask, the level, the kind, the gate in Marchutan
	/// Priory (0: a battleground's icon, or a training ground with no gate), and the ticket each entry takes.</summary>
	private sealed record AutoInstanceRow(int MapId, string Handler, int Mask, byte Level, AutoInstanceKind Kind, int Gate = 0, int Ticket = 0,
		int TicketCount = 0, bool EntryThrows = false)
	{
		public AutoGroupType Type => AutoGroupTypeExtensions.GetAGTByMaskId(Mask)!.Value;

		public InstanceCooltime Cooltime => DataManager.INSTANCE_COOLTIME_DATA.GetInstanceCooltimeByWorldId(MapId);

		/// <summary>The places of a match: both races' sides for a battleground (AutoPvpInstance.getMaxPlayers), the instance's
		/// member count otherwise.</summary>
		public int Places => Kind == AutoInstanceKind.Battleground
			? DataManager.INSTANCE_COOLTIME_DATA.GetMaxMemberCount(MapId, Race.ELYOS) + DataManager.INSTANCE_COOLTIME_DATA.GetMaxMemberCount(MapId, Race.ASMODIANS)
			: DataManager.INSTANCE_COOLTIME_DATA.GetMaxMemberCount(MapId, Race.ASMODIANS);

		/// <summary>AutoInstance.onPressEnter counts a battleground's entry; the arenas' onPressEnter does not call it.</summary>
		public bool Counted => Kind == AutoInstanceKind.Battleground && Cooltime.GetMaxCount() != 0;

		public override string ToString() => MapId + " " + Handler;
	}

	/// <summary>The auto-group instance handlers, in the order the scenario plays them.</summary>
	private static readonly AutoInstanceRow[] AutoInstanceRows =
	[
		new(300110000, "BaranathDredgionInstance", 1, 46, AutoInstanceKind.Battleground),
		new(300210000, "ChantraDredgionInstance", 2, 51, AutoInstanceKind.Battleground),
		new(300440000, "TerathDredgionInstance", 3, 56, AutoInstanceKind.Battleground),
		new(301120000, "KamarBattlefieldInstance", 107, 61, AutoInstanceKind.Battleground),
		new(300350000, "ArenaOfChaosInstance", 21, 46, AutoInstanceKind.FreeForAll, Gate: 730482, Ticket: 186000135, TicketCount: 1),
		new(300360000, "ArenaOfDisciplineInstance", 24, 46, AutoInstanceKind.FreeForAll, Gate: 730484, Ticket: 186000135, TicketCount: 1),
		new(300450000, "ArenaOfHarmonyInstance", 33, 46, AutoInstanceKind.Harmony, Gate: 730577, Ticket: 186000184, TicketCount: 1),
		new(300550000, "ArenaOfGloryInstance", 38, 56, AutoInstanceKind.FreeForAll, Gate: 730645, Ticket: 186000185, TicketCount: 3),
		new(300420000, "ChaosTrainingGroundsInstance", 27, 46, AutoInstanceKind.FreeForAll),
		new(300430000, "DisciplineTrainingGroundsInstance", 30, 46, AutoInstanceKind.FreeForAll),
		new(300570000, "HarmonyTrainingGroundsInstance", 101, 46, AutoInstanceKind.Harmony),
		new(301210000, "EngulfedOphidanBridgeInstance", 108, 61, AutoInstanceKind.Battleground),
		new(301310000, "IdgelDomeInstance", 111, 61, AutoInstanceKind.Battleground),
		// Java 4.8 cannot port anyone into Iron Wall Warfront: see PlayBrokenAutoEntryAsync.
		new(301220000, "IronWallWarfrontInstance", 109, 61, AutoInstanceKind.Battleground, EntryThrows: true),
	];

	private const int MarchutanPrioryMap = 120080000;

	private async Task RunInstancesPvpAsync(ScenarioDefinition scenario, bool includeHistory)
	{
		try
		{
			await PlayInstancesPvpAsync(scenario, includeHistory);
		}
		catch (Exception exception) when (exception is not SimulationLogPolicyException)
		{
			throw new InvalidOperationException("INSTANCES-PVP failed at " + SystemClock.UtcNow().ToString("O") + "; recent packets:" + Environment.NewLine +
				string.Join(Environment.NewLine, instanceCast.Where(actor => actor.Online).Take(8).Select(actor => actor.Name + " " + actor.Session.CurrentStep + " " +
					DescribeInstancePackets(actor.Session.PacketHistory.TakeLast(20)))), exception);
		}
	}

	private async Task PlayInstancesPvpAsync(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(150));
		CancellationToken token = timeout.Token;
		Assert.Equal(InstancesPvpEpoch, fixture.Epoch);
		DateTimeOffset saturday = new(2026, 10, 17, 0, 0, 0, TimeSpan.Zero);
		Assert.Equal(DayOfWeek.Saturday, saturday.DayOfWeek);
		// autogroup.properties as shipped (see ARENA): the queue on, the arenas' hours on, the registrations' crons.
		Assert.True(AutoGroupConfig.AUTO_GROUP_ENABLE && AutoGroupConfig.START_TIME_ENABLE);
		Assert.Equal(PvpInstanceMapIds.Order(), AutoInstanceRows.Select(row => row.MapId).Order());

		var cast = new List<InstanceCast>();
		var sessions = new List<SimulationL0Session>();
		try
		{
			for (int i = 0; i < 48; i++)
			{
				Race race = i < 24 ? Race.ELYOS : Race.ASMODIANS;
				string name = (race == Race.ELYOS ? "Elypvp" : "Asmpvp") + (char)('a' + i % 24);
				var session = new SimulationL0Session(fixture, policy, $"b{i + 1:00}", i + 1, name, race);
				sessions.Add(session);
				cast.Add(new InstanceCast(session, name, race));
			}
			instanceCast = cast.ToArray();
			InstanceCast[] elyos = instanceCast.Take(24).ToArray(), asmodians = instanceCast.Skip(24).ToArray();

			// s01, 19:55: login, class.
			foreach (InstanceCast actor in instanceCast)
			{
				actor.Session.BeginStep("s01", "login-create-enter");
				await actor.Session.LoginAndAuthenticateAsync(token);
				await actor.Session.CreateCharacterAsync(token);
				await actor.Session.EnterWorldAsync(token);
				await actor.Session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
				actor.Online = true;
				Player player = PlayerOf(actor);
				Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
				Assert.True(ClassChangeService.SetClass(player, PlayerClass.GLADIATOR, validate: true, updateDaevaStatus: true));
				player.GetCommonData().SetLevel(30);
			}
			await SyncInstanceCastAsync(token);

			string[] only = (Environment.GetEnvironmentVariable("AION_SIM_INSTANCE_ROWS") ?? "")
				.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			bool Selected(AutoInstanceRow row) => only.Length == 0 || only.Contains(row.MapId.ToString());
			var failures = new List<InstanceRowFailure>();
			AutoInstanceRow Row(int mapId) => AutoInstanceRows.Single(row => row.MapId == mapId);

			// Saturday 20:00: the dredgions' and Kamar Battlefield's registrations open by the cron; the arenas are open.
			await AdvanceInstanceWorldToAsync(saturday.AddHours(20).AddSeconds(5), token, TimeSpan.FromSeconds(1));
			foreach (int mask in new[] { 1, 2, 3, 107 })
				Assert.True(PeriodicInstanceManager.GetInstance().IsRegistrationOpen(mask), $"registration {mask} is not open at 20:00");
			foreach (AutoInstanceRow row in AutoInstanceRows.Take(11).Where(Selected))
				await PlayAutoRowStageAsync(scenario, row, elyos, asmodians, "saturday", failures, token, () => PlayAutoDayAsync(row, elyos, asmodians, token));
			// Engulfed Ophidan Bridge by the GM's //instance open eob.
			if (Selected(Row(301210000)))
			{
				Assert.True(PeriodicInstanceManager.GetInstance().OpenRegistration(SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_OPEN_IDLDF5_Under_01_War(), 108,
					AutoGroupConfig.ENGULFED_OPHIDAN_BRIDGE_REGISTRATION_PERIOD));
				await PlayAutoRowStageAsync(scenario, Row(301210000), elyos, asmodians, "saturday", failures, token,
					() => PlayAutoDayAsync(Row(301210000), elyos, asmodians, token));
			}
			// 23:00: Idgel Dome by the cron.
			await AdvanceInstanceWorldToAsync(saturday.AddHours(23).AddSeconds(5), token, TimeSpan.FromSeconds(60));
			Assert.True(PeriodicInstanceManager.GetInstance().IsRegistrationOpen(111));
			if (Selected(Row(301310000)))
				await PlayAutoRowStageAsync(scenario, Row(301310000), elyos, asmodians, "saturday", failures, token,
					() => PlayAutoDayAsync(Row(301310000), elyos, asmodians, token));
			// Sunday 00:00: Iron Wall Warfront by the cron.
			DateTimeOffset sunday = saturday.AddDays(1);
			await AdvanceInstanceWorldToAsync(sunday.AddSeconds(5), token, TimeSpan.FromSeconds(60));
			Assert.True(PeriodicInstanceManager.GetInstance().IsRegistrationOpen(109));
			if (Selected(Row(301220000)))
				await PlayAutoRowStageAsync(scenario, Row(301220000), elyos, asmodians, "sunday", failures, token,
					() => PlayAutoDayAsync(Row(301220000), elyos, asmodians, token));

			// Sunday 12:00: the daily resets and the Sunday ones have passed (09:00); the dredgions' and Engulfed Ophidan Bridge's
			// registrations open by the cron, Kamar Battlefield's and Idgel Dome's by the GM's //instance open. Everyone is logged out
			// in the night.
			foreach (InstanceCast actor in instanceCast.Where(actor => actor.Online))
			{
				await actor.Session.QuitAsync(token);
				actor.Online = false;
			}
			await AdvanceInstanceWorldToAsync(sunday.AddHours(11).AddMinutes(58), token, TimeSpan.FromMinutes(1));
			foreach (InstanceCast actor in instanceCast)
				await InstanceReloginAsync(actor, token);
			await AdvanceInstanceWorldToAsync(sunday.AddHours(12).AddSeconds(5), token, TimeSpan.FromSeconds(1));
			foreach (int mask in new[] { 1, 2, 3, 108 })
				Assert.True(PeriodicInstanceManager.GetInstance().IsRegistrationOpen(mask), $"registration {mask} is not open at Sunday 12:00");
			Assert.True(PeriodicInstanceManager.GetInstance().OpenRegistration(SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_OPEN_IDKamar(), 107,
				AutoGroupConfig.KAMAR_BATTLEFIELD_REGISTRATION_PERIOD));
			Assert.True(PeriodicInstanceManager.GetInstance().OpenRegistration(SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_OPEN_IDLDF5_Fortress_Re(), 111,
				AutoGroupConfig.IDGEL_DOME_REGISTRATION_PERIOD));
			foreach (AutoInstanceRow row in AutoInstanceRows.Where(row => row.Counted && !row.EntryThrows && Selected(row)))
			{
				Assert.True(ResetAfter(row.Cooltime, saturday.AddHours(21)) <= sunday.AddHours(9), row + " does not reset on Sunday");
				await PlayAutoRowStageAsync(scenario, row, elyos, asmodians, "reset", failures, token, () => PlayAutoResetAsync(row, elyos, asmodians, token));
			}

			foreach (InstanceRowFailure failure in failures)
				Console.WriteLine($"INSTANCES-PVP {failure.Row} {failure.Stage}: {failure.Error}");
			Assert.True(failures.Count == 0, $"{failures.Count} auto-group row stages failed: " +
				string.Join("; ", failures.Select(failure => $"{failure.Row} {failure.Stage}: {failure.Error.GetType().Name}: {FirstLine(failure.Error.Message)}")));
			foreach (InstanceCast actor in instanceCast.Where(actor => actor.Online))
				await actor.Session.QuitAsync(token);
			policy.AssertClean();
		}
		finally
		{
			foreach (SimulationL0Session session in sessions)
				await session.DisposeAsync();
		}
	}

	/// <summary>One stage of an auto-group row under its own log policy; a failure is recorded and the subjects brought back.</summary>
	private async Task PlayAutoRowStageAsync(ScenarioDefinition scenario, AutoInstanceRow row, InstanceCast[] elyos, InstanceCast[] asmodians,
		string stage, List<InstanceRowFailure> failures, CancellationToken token, Func<Task> play)
	{
		using var rowPolicy = NewPolicy(scenario.Id, includeHistory: false, new SimulationLogPolicyOptions { FailOnWarnings = true });
		ClearInstanceCastHistories();
		foreach (InstanceCast actor in instanceCast.Where(actor => actor.Online))
			actor.Session.BeginStep(row.MapId + "-" + stage, row.Handler);
		try
		{
			await play();
			rowPolicy.AssertClean();
			Console.WriteLine($"INSTANCES-PVP {row} {stage}: passed at {SystemClock.UtcNow():O}");
		}
		catch (Exception error)
		{
			failures.Add(new InstanceRowFailure(row.ToString(), stage, error));
			Console.WriteLine($"INSTANCES-PVP {row} {stage}: FAILED at {SystemClock.UtcNow():O}: {error}");
			Console.WriteLine("  recent packets: " + string.Join(Environment.NewLine + "  ", instanceCast.Where(actor => actor.Online).Take(6).Select(actor =>
				actor.Name + ": " + DescribeInstancePackets(actor.Session.PacketHistory.TakeLast(15)))));
			try { rowPolicy.AssertClean(); }
			catch (Exception logError) { Console.WriteLine("  log problems: " + logError.Message); }
			await RecoverAutoCastAsync(row, token);
		}
	}

	/// <summary>After a failed stage: every registration cancelled, everyone out of every instance and team, at his race's start.</summary>
	private async Task RecoverAutoCastAsync(AutoInstanceRow row, CancellationToken token)
	{
		foreach (InstanceCast actor in instanceCast)
		{
			try
			{
				if (!actor.Online)
					await InstanceReloginAsync(actor, token);
				Player player = PlayerOf(actor);
				AutoGroupService.GetInstance().CancelRegistration(player, row.Mask);
				if (player.IsInAlliance())
					Aion.GameServer.Model.Team.Alliance.PlayerAllianceService.RemovePlayer(player);
				else if (player.IsInGroup())
					Aion.GameServer.Model.Team.Group.PlayerGroupService.RemovePlayer(player);
				var start = DataManager.PLAYER_INITIAL_DATA.GetSpawnLocation(actor.Race);
				await TeleportForSetupAsync(actor.Session, player, start.GetMapId(), start.GetX(), start.GetY(), start.GetZ(), token);
			}
			catch (Exception error)
			{
				Console.WriteLine($"  recovery of {actor.Name} failed: {error.Message}");
			}
		}
		await AdvanceInstanceWorldAsync(TimeSpan.FromSeconds(12), token);
		await SyncInstanceCastAsync(token);
	}

	/// <summary>
	/// The first day: as many matches as the row counts (two for a row with no limit), each made, entered, left and its instance
	/// destroyed; then, for a counted row, every player's next registration refused.
	/// </summary>
	private async Task PlayAutoDayAsync(AutoInstanceRow row, InstanceCast[] elyos, InstanceCast[] asmodians, CancellationToken token)
	{
		InstanceCast[] players = AutoPlayersOf(row, elyos, asmodians);
		SetInstanceLevel(players, row.Level);
		if (row.EntryThrows)
		{
			await PlayBrokenAutoEntryAsync(row, players, token);
			return;
		}
		int matches = row.Counted ? row.Cooltime.GetMaxCount() : 2;
		for (int match = 1; match <= matches; match++)
		{
			WorldMapInstance instance = await PlayAutoMatchAsync(row, players, match, token);
			await LeaveAutoMatchAsync(row, players, instance, token);
		}
		if (!row.Counted)
			return;
		foreach (InstanceCast actor in players)
		{
			Player player = PlayerOf(actor);
			Assert.True(player.GetPortalCooldownList().IsPortalUseDisabled(row.MapId), $"{actor.Name} is not refused {row}");
			int mark = actor.Session.PacketHistory.Count;
			await actor.Session.SendPacketAsync(GameClientPackets.AutoGroup(row.Mask, 100, 0), token);
			await actor.Session.SynchronizeAsync(token);
			DecodedBotServerPacket[] since = actor.Session.PacketHistory.Skip(mark).ToArray();
			Assert.True(since.Count(packet => IsSystemMessage(packet, "STR_MSG_CANNOT_MAKE_INSTANCE_COOL_TIME")) == 1,
				$"{actor.Name}'s registration for {row} was not refused once; got {DescribeInstancePackets(since)}");
			Assert.False(AutoGroupService.GetInstance().IsSearching(player, row.Mask));
		}
	}

	/// <summary>After the row's reset: nobody is refused, and a match is made, entered and counted from one again.</summary>
	private async Task PlayAutoResetAsync(AutoInstanceRow row, InstanceCast[] elyos, InstanceCast[] asmodians, CancellationToken token)
	{
		InstanceCast[] players = AutoPlayersOf(row, elyos, asmodians);
		SetInstanceLevel(players, row.Level);
		foreach (InstanceCast actor in players)
			Assert.False(PlayerOf(actor).GetPortalCooldownList().IsPortalUseDisabled(row.MapId), $"{actor.Name} is still refused {row}");
		WorldMapInstance instance = await PlayAutoMatchAsync(row, players, 1, token);
		await LeaveAutoMatchAsync(row, players, instance, token);
	}

	/// <summary>Who plays a row: both races' sides for a battleground, the Asmodians otherwise (the arenas take either race).</summary>
	private static InstanceCast[] AutoPlayersOf(AutoInstanceRow row, InstanceCast[] elyos, InstanceCast[] asmodians)
	{
		if (row.Kind != AutoInstanceKind.Battleground)
			return asmodians.Take(row.Places).ToArray();
		int side = row.Places / 2;
		return elyos.Take(side).Concat(asmodians.Take(side)).ToArray();
	}

	/// <summary>
	/// One match: every player registers alone (from the icon, the gate or, for a training ground, at once), the last
	/// registration makes the match (AutoGroupService.createNewInstance: the entry window 4 to every player), and every player
	/// enters (window 102): the entered window 5, the instance's start position, the entry counted.
	/// </summary>
	private async Task<WorldMapInstance> PlayAutoMatchAsync(AutoInstanceRow row, InstanceCast[] players, int match, CancellationToken token)
	{
		WorldMapInstance instance = await MakeAutoMatchAsync(row, players, token);
		foreach (InstanceCast actor in players)
		{
			Player player = PlayerOf(actor);
			long tickets = row.Ticket == 0 ? 0 : player.GetInventory().GetItemCountByItemId(row.Ticket);
			int mark = actor.Session.PacketHistory.Count;
			await actor.Session.SendPacketAsync(GameClientPackets.AutoGroup(row.Mask, 102), token);
			await WaitForInstanceArrivalAsync(actor, row.MapId, mark, token);
			Assert.Same(instance, player.GetWorldMapInstance());
			Assert.Contains(AutoGroupWindows(actor.Session, mark), window => window.Mask == row.Mask && window.Window == 5);
			if (row.Ticket != 0)
				Assert.Equal(tickets - row.TicketCount, player.GetInventory().GetItemCountByItemId(row.Ticket));
			AssertInstanceEntryCounted(row.MapId, row.Cooltime, row.Counted, actor, player, match, mark);
		}
		Assert.Equal(players.Length, instance.GetPlayersInside().Count);
		return instance;
	}

	/// <summary>
	/// Every player registers alone (from the icon, the gate or, for a training ground, at once) and the last registration
	/// makes the match (AutoGroupService.createNewInstance): the instance of the row's handler, and the entry window 4 to every
	/// player.
	/// </summary>
	private async Task<WorldMapInstance> MakeAutoMatchAsync(AutoInstanceRow row, InstanceCast[] players, CancellationToken token)
	{
		Assert.Empty(InstancesOf(row.MapId));
		AutoGroup template = row.Type.GetTemplate();
		Npc? gate = null;
		if (row.Gate != 0)
		{
			WorldMapInstance priory = fixture.World.GetWorldMap(MarchutanPrioryMap).GetMainWorldMapInstance();
			gate = priory.GetNpcs(row.Gate).Where(npc => npc.IsSpawned()).OrderBy(npc => npc.GetObjectId()).First();
		}
		int[] marks = players.Select(actor => actor.Session.PacketHistory.Count).ToArray();
		for (int i = 0; i < players.Length; i++)
		{
			InstanceCast actor = players[i];
			Player player = PlayerOf(actor);
			if (row.Ticket != 0)
				Assert.Equal(0, ItemService.AddItem(player, row.Ticket, row.TicketCount));
			int mark = actor.Session.PacketHistory.Count;
			if (gate != null)
			{
				// PortalDialogAI: page 1011, INSTANCE_PARTY_MATCH, the entry request (window 0) of the arena the level takes.
				await PlaceBesideAsync(actor, player, gate, i, token);
				mark = actor.Session.PacketHistory.Count;
				await actor.Session.SendPacketAsync(actor.Session.Api.TalkTo(gate.GetObjectId()), token);
				await WaitForInstanceDialogAsync(actor, gate.GetObjectId(), 1011, token);
				await actor.Session.SendPacketAsync(actor.Session.Api.SelectDialog(gate.GetObjectId(), 77), token);
				await WaitForInstanceDialogAsync(actor, gate.GetObjectId(), 0, token);
				Assert.Contains(AutoGroupWindows(actor.Session, mark), window => window == new ArenaWindow(row.Mask, 0, row.MapId, template.GetL10nId(), template.GetTitleId(), 0, ""));
			}
			else if (row.Kind == AutoInstanceKind.Battleground)
			{
				// The icon of the opening registration (PeriodicInstanceManager.handleRequest): the entry request, window 0.
				await actor.Session.SendPacketAsync(GameClientPackets.AutoGroup(row.Mask, 104), token);
				await actor.Session.SynchronizeAsync(token);
				Assert.Contains(AutoGroupWindows(actor.Session, mark), window => window == new ArenaWindow(row.Mask, 0, row.MapId, template.GetL10nId(), template.GetTitleId(), 0, ""));
			}
			int registered = actor.Session.PacketHistory.Count;
			await actor.Session.SendPacketAsync(GameClientPackets.AutoGroup(row.Mask, 100, 0), token);
			await actor.Session.SynchronizeAsync(token);
			DecodedBotServerPacket[] since = actor.Session.PacketHistory.Skip(registered).ToArray();
			Assert.True(since.Any(packet => IsSystemMessage(packet, "STR_MSG_INSTANCE_REGISTER_SUCCESS")),
				$"{actor.Name}'s registration for {row} failed; got {DescribeInstancePackets(since)}");
		}
		await SyncInstanceCastAsync(token);
		for (int i = 0; i < players.Length; i++)
			Assert.True(AutoGroupWindows(players[i].Session, marks[i]).Any(window => window.Mask == row.Mask && window.Window == 4),
				$"{players[i].Name} got no entry window for {row}");
		WorldMapInstance instance = Assert.Single(InstancesOf(row.MapId));
		Assert.Equal(row.Handler, instance.GetInstanceHandler().GetType().Name);
		return instance;
	}

	/// <summary>
	/// Iron Wall Warfront as Java 4.8 has it: the match is made, but <c>IronWallWarfrontInstance</c> has no
	/// <c>portToStartPosition</c> (its <c>portToPosition(Player, WorldMapInstance)</c> is never called), so the first player's entry
	/// (window 102) reaches <c>AutoPvpInstance.onPressEnter</c>, which counts the entry and then calls
	/// <c>GeneralInstanceHandler.portToStartPosition</c>, which throws <c>UnsupportedOperationException</c>: the packet's error is
	/// logged (allowlisted for this one press), nobody moves and no entered window comes. Every player then declines the entry
	/// (window 103, <c>AutoGroupService.cancelEnter</c>) and the empty match's instance is destroyed. Nobody else presses.
	/// </summary>
	private async Task PlayBrokenAutoEntryAsync(AutoInstanceRow row, InstanceCast[] players, CancellationToken token)
	{
		WorldMapInstance instance = await MakeAutoMatchAsync(row, players, token);
		InstanceCast first = players[0];
		Player firstPlayer = PlayerOf(first);
		int worldBefore = firstPlayer.GetWorldId();
		int mark = first.Session.PacketHistory.Count;
		int logMark = fixture.LogCapture.Entries.Count;
		await first.Session.SendPacketAsync(GameClientPackets.AutoGroup(row.Mask, 102), token);
		await first.Session.SynchronizeAsync(token);
		var thrown = Assert.Single(fixture.LogCapture.Entries.Skip(logMark), entry => entry.Level >= Microsoft.Extensions.Logging.LogLevel.Error);
		Assert.IsType<NotSupportedException>(thrown.Exception);
		Assert.StartsWith("Error handling client packet", thrown.Message);
		Assert.Contains("GeneralInstanceHandler.PortToStartPosition", thrown.Exception!.StackTrace);
		Assert.Equal(worldBefore, firstPlayer.GetWorldId());
		Assert.DoesNotContain(first.Session.PacketHistory.Skip(mark), packet => packet.PacketType == typeof(SM_TELEPORT_LOC) || packet.PacketType == typeof(SM_PLAYER_SPAWN));
		Assert.DoesNotContain(AutoGroupWindows(first.Session, mark), window => window.Window == 5);
		// AutoInstance.onPressEnter counted the entry before the throw.
		AssertInstanceEntryCounted(row.MapId, row.Cooltime, row.Counted, first, firstPlayer, 1, mark);
		Assert.Empty(instance.GetPlayersInside());
		foreach (InstanceCast actor in players)
		{
			int cancelled = actor.Session.PacketHistory.Count;
			await actor.Session.SendPacketAsync(GameClientPackets.AutoGroup(row.Mask, 103), token);
			await actor.Session.SynchronizeAsync(token);
			Assert.Contains(AutoGroupWindows(actor.Session, cancelled), window => window.Mask == row.Mask && window.Window == 2);
		}
		Assert.Empty(InstancesOf(row.MapId));
		// The penalty of the declines (AutoGroupService.penalisePlayerAndScheduleRemoval) runs out in 10 s.
		await AdvanceInstanceWorldAsync(TimeSpan.FromSeconds(11), token, TimeSpan.FromSeconds(1));
		await SyncInstanceCastAsync(token);
	}

	/// <summary>
	/// Every player sends CM_INSTANCE_LEAVE: the handler's leaveInstance sends him to the exit (instance_exit.xml, or his bind
	/// point where the map has none), and the last one out has the match's instance destroyed (AutoGroupService.destroyIfPossible).
	/// </summary>
	private async Task LeaveAutoMatchAsync(AutoInstanceRow row, InstanceCast[] players, WorldMapInstance instance, CancellationToken token)
	{
		foreach (InstanceCast actor in players)
		{
			Player player = PlayerOf(actor);
			int mark = actor.Session.PacketHistory.Count;
			await actor.Session.SendPacketAsync(GameClientPackets.InstanceLeave(), token);
			InstanceExit? exit = DataManager.INSTANCE_EXIT_DATA.GetInstanceExit(row.MapId, actor.Race);
			if (exit != null)
			{
				await WaitForInstanceArrivalAsync(actor, exit.GetExitWorld(), mark, token);
				Assert.Equal((exit.GetExitWorld(), exit.GetX(), exit.GetY(), exit.GetZ()), (player.GetWorldId(), player.GetX(), player.GetY(), player.GetZ()));
			}
			else
			{
				// TeleportService.moveToInstanceExit: no exit for the map, so the bind point.
				var bind = player.GetBindPoint();
				int world = bind?.GetMapId() ?? DataManager.PLAYER_INITIAL_DATA.GetSpawnLocation(actor.Race).GetMapId();
				await WaitForInstanceArrivalAsync(actor, world, mark, token);
			}
			Assert.False(ReferenceEquals(instance, player.GetWorldMapInstance()));
			Assert.False(player.IsInTeam(), $"{actor.Name} is still in the match's team");
		}
		Assert.Empty(InstancesOf(row.MapId));
		await SyncInstanceCastAsync(token);
	}
}
