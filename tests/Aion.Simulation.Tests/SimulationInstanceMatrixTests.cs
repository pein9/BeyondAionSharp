using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Portal;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.Simulation.Tests;

/// <summary>
/// P11-01, scenario INSTANCES: the instance entry matrix of the instances a player enters through a portal, played on the
/// virtual clock from the process's own epoch, Tuesday 2026-10-13 09:01 UTC (<c>reset-INSTANCES</c>), with six ordinary
/// Asmodian Gladiators and an Elyos one. For each instance handler (<see cref="InstanceRows"/>) the right team (alone, a
/// group of six, an alliance of six, a league of two alliances of three, by the cooltime row's member count as
/// <c>PortalService.port</c> reads it) enters through the instance's own entry in Java: the portal NPC's dialog
/// (<c>PortalDialogAI</c> and its subclasses, <c>PortalService.port</c>), its use (<c>PortalAI</c>), Beshmundir's walk
/// (<c>BeshmundirsWalkAI</c>), Epeios's quest dialog (<c>_1922DeliveronYourPromises</c>), and, for the handlers no player
/// reaches in Java 4.8, the GM's <c>//goto</c> (<c>GoTo.goTo</c>). The handler's create path must log nothing, the
/// handler must be the instance's (its class), the team and every member registered, and every entry counted as
/// <c>PortalService.transfer</c> and <c>InstanceCooltimeData.calculateInstanceEntranceCooltime</c> count it, with
/// <c>SM_INSTANCE_INFO</c> read field by field. Every member then sends <c>CM_INSTANCE_LEAVE</c>: Java hands it to the
/// handler's <c>leaveInstance</c>, which takes the player out for the handlers that implement it and does nothing for the
/// rest (<c>GeneralInstanceHandler.leaveInstance</c> is empty). Those leave as Java lets them: a group or an alliance by
/// leaving its team (<c>PlayerLeavedEvent</c>: <c>STR_MSG_LEAVE_INSTANCE_NOT_PARTY</c> and the exit 30 s later), a player
/// alone, a league or a GM's guest by logging out until the empty instance is destroyed and logging back in
/// (<c>InstanceService.onPlayerLogin</c> sends him to the exit). After as many entries as the cooltime row allows the next
/// one is refused (<c>STR_MSG_CANNOT_MAKE_INSTANCE_COOL_TIME</c>), and after the row's reset (Wednesday 09:00 for the daily
/// and most weekly rows, 11:00 for Stonespear Reach, which is closed before, Thursday 09:00 for the Eternal Bastion) the
/// entry succeeds again. A row with no limit (no entries counted, or a relative cooltime of zero) is entered twice in a row.
/// The same code plays INSTANCES-TOMB and INSTANCES-CIRCUS, each in a process of its own on a Tuesday of its 2024 event, for the
/// two instances whose entry NPC is an event spawn (<see cref="EventInstanceRows"/>).
/// <para>
/// Director setup, recorded here as the plan asks: every subject is made a Gladiator and given the row's level; the siege
/// location whose peace group holds an Abyss or Gelkmaros entry is given to the Asmodians by the GM capture
/// (<see cref="SimulationSiegeSetup.GiveLocation"/>) and given back after the row; the entry items a portal path or an entry
/// AI takes are given (the Eternal Bastion's 186000236, the Sealed Danuar Mysticarium's 185000223, Stonespear Reach's
/// key 185000230); the six Asmodians' legion, made with their own packets, is given legion dominion 1 for Stonespear Reach;
/// the Elyos is given Q1922 at step 4 for Epeios; the subjects are placed beside the entry NPC by setup teleports, with the
/// monsters aggressive to them within 40 m of the entry deleted first; the handlers no player reaches are entered by the GM's
/// <c>//goto</c>.
/// </para>
/// </summary>
public sealed partial class SimulationFastScenarioTests
{
	internal static readonly DateTimeOffset InstancesEpoch = new(2026, 10, 13, 9, 1, 0, TimeSpan.Zero);
	/// <summary>Tuesday of the Shugo Imperial Tomb event (2024-08-12 to 2024-08-25), after the day's 09:00 entry reset.</summary>
	internal static readonly DateTimeOffset InstancesTombEpoch = new(2024, 8, 13, 9, 1, 0, TimeSpan.Zero);
	/// <summary>Tuesday of the Nightmare Circus event (2024-10-28 to 2024-11-03), after the day's 09:00 entry reset.</summary>
	internal static readonly DateTimeOffset InstancesCircusEpoch = new(2024, 10, 29, 9, 1, 0, TimeSpan.Zero);

	/// <summary>How a player reaches the instance in Java 4.8.</summary>
	private enum InstanceWay
	{
		/// <summary>PortalDialogAI or a subclass: talk, then the portal path's dialog action.</summary>
		Dialog,
		/// <summary>PortalAI: talk, the use bar (the template's talk delay), then the portal_use path.</summary>
		Use,
		/// <summary>BeshmundirsWalkAI: the use bar, page 10, INSTANCE_ENTRY; the leader picks a path and answers its question.</summary>
		Beshmundir,
		/// <summary>_1922DeliveronYourPromises: Epeios's SETPRO3 makes a new arena and sends the player into it.</summary>
		Epeios,
		/// <summary>No player path in Java 4.8: the GM's //goto (InstanceService.getOrRegisterInstance, then a plain teleport).</summary>
		Goto,
	}

	/// <summary>What the handler's leaveInstance does with CM_INSTANCE_LEAVE.</summary>
	private enum InstancePacketLeave
	{
		/// <summary>GeneralInstanceHandler.leaveInstance is empty, or the handler leaves only at the end of its progress.</summary>
		None,
		/// <summary>TeleportService.moveToInstanceExit.</summary>
		Exit,
		/// <summary>InstanceService.destroyInstance: STR_MSG_LEAVE_INSTANCE_FORCE and the exit.</summary>
		Destroy,
		/// <summary>StonespearReachInstance: to the point of the legion's dominion in Enshar.</summary>
		Dominion,
	}

	private enum InstanceTeam { Solo, Group, Alliance, League }

	/// <summary>
	/// One instance handler: its map, its class, the entry and the NPC and map of the entry, the dialog action of the portal path,
	/// the level the director gives, what <c>CM_INSTANCE_LEAVE</c> does, and the setup the entry needs.
	/// </summary>
	private sealed record InstanceRow(int MapId, string Handler, InstanceWay Way, int NpcId, int NpcMap, ushort Action, byte Level,
		InstancePacketLeave PacketLeave, int SiegeId = 0, int ItemId = 0, int ItemCount = 0, bool Legion = false, Race Race = Race.ASMODIANS)
	{
		public InstanceCooltime Cooltime => DataManager.INSTANCE_COOLTIME_DATA.GetInstanceCooltimeByWorldId(MapId);

		/// <summary>The member count PortalService.port reads; the GM's //goto registers the player alone.</summary>
		public int MaxMembers => DataManager.INSTANCE_COOLTIME_DATA.GetMaxMemberCount(MapId, Race);

		public InstanceTeam Team => Way is InstanceWay.Goto or InstanceWay.Epeios ? InstanceTeam.Solo : MaxMembers switch
		{
			1 => InstanceTeam.Solo,
			3 or 6 => InstanceTeam.Group,
			<= 24 => InstanceTeam.Alliance,
			_ => InstanceTeam.League,
		};

		/// <summary>Whether an entry is counted (InstanceCooltimeData.calculateInstanceEntranceCooltime is above 0). The GM's
		/// //goto counts nothing.</summary>
		public bool Counted => Way != InstanceWay.Goto && Cooltime.GetMaxCount() != 0 &&
			!(Cooltime.GetCoolTimeType() == InstanceCoolTimeType.RELATIVE && Cooltime.GetEntCoolTime() == 0);

		public override string ToString() => MapId + " " + Handler;
	}

	/// <summary>
	/// The instance handlers a player enters through a portal or the GM's //goto, in the order the scenario plays them: the
	/// Abyss rows first, before the Tuesday 17:00 sieges of their fortresses (siege_schedule.xml); the dredgions, the PvP
	/// battlegrounds and the arenas are scenario INSTANCES-PVP's, Shugo Imperial Tomb's and the Nightmare Circus's the event
	/// scenarios'.
	/// </summary>
	private static readonly InstanceRow[] InstanceRows =
	[
		// Reshanta: the fortress artifacts' portals, in the Asmodian peace group of the fortress (siege_spawns).
		new(300050000, "AsteriaInstance", InstanceWay.Use, 700533, 400010000, 0, 40, InstancePacketLeave.None, SiegeId: 1251),
		new(300060000, "SulfurTreeNestInstance", InstanceWay.Use, 700529, 400010000, 0, 30, InstancePacketLeave.None, SiegeId: 1141),
		new(300070000, "RoahInstance", InstanceWay.Use, 700532, 400010000, 0, 40, InstancePacketLeave.None, SiegeId: 1211),
		new(300080000, "LeftWingChamberInstance", InstanceWay.Use, 700530, 400010000, 0, 30, InstancePacketLeave.None, SiegeId: 1131),
		new(300090000, "RightWingChamberInstance", InstanceWay.Use, 700531, 400010000, 0, 30, InstancePacketLeave.None, SiegeId: 1132),
		new(300120000, "KysisChamber", InstanceWay.Dialog, 730903, 400010000, DialogActionSetPro1, 40, InstancePacketLeave.None, SiegeId: 1231),
		new(300130000, "MirenChamber", InstanceWay.Dialog, 730904, 400010000, DialogActionSetPro1, 40, InstancePacketLeave.None, SiegeId: 1241),
		new(300140000, "KrotanChamber", InstanceWay.Dialog, 730902, 400010000, DialogActionSetPro1, 40, InstancePacketLeave.None, SiegeId: 1221),
		new(301280000, "KysisBarracks", InstanceWay.Dialog, 730903, 400010000, DialogActionSetPro2, 61, InstancePacketLeave.None, SiegeId: 1231),
		new(301290000, "MirenBarracks", InstanceWay.Dialog, 730904, 400010000, DialogActionSetPro2, 61, InstancePacketLeave.None, SiegeId: 1241),
		new(301300000, "KrotanBarracks", InstanceWay.Dialog, 730902, 400010000, DialogActionSetPro2, 61, InstancePacketLeave.None, SiegeId: 1221),
		new(300220000, "AbyssalSplinterInstance", InstanceWay.Dialog, 730356, 400010000, DialogActionSetPro1, 50, InstancePacketLeave.None, SiegeId: 1011),
		new(300600000, "UnstableSplinterpathInstance", InstanceWay.Dialog, 730366, 400010000, DialogActionSetPro1, 60, InstancePacketLeave.None, SiegeId: 1011),
		// The capitals, Marchutan Priory and the lands.
		new(300040000, "DarkPoetaInstance", InstanceWay.Dialog, 730185, 220040000, DialogActionSetPro1, 48, InstancePacketLeave.None),
		new(300100000, "SteelRakeInstance", InstanceWay.Dialog, 798379, 120010000, DialogActionSelect11, 40, InstancePacketLeave.None),
		new(300460000, "SteelRakeCabineInstance", InstanceWay.Dialog, 798463, 120010000, DialogActionSelect11, 40, InstancePacketLeave.None),
		new(320090000, "TrinielUndergroundArenaInstance", InstanceWay.Use, 700368, 120010000, 0, 26, InstancePacketLeave.None),
		new(300300000, "EmpyreanCrucibleInstance", InstanceWay.Dialog, 730431, 120080000, DialogActionSetPro1, 50, InstancePacketLeave.Exit),
		new(300320000, "CrucibleChallengeInstance", InstanceWay.Dialog, 730462, 120080000, DialogActionSetPro1, 50, InstancePacketLeave.Destroy),
		new(320100000, "FireTempleInstance", InstanceWay.Dialog, 730047, 220020000, DialogActionSetPro1, 27, InstancePacketLeave.None),
		new(300200000, "HaramelInstance", InstanceWay.Use, 730319, 220030000, 0, 16, InstancePacketLeave.None),
		new(320080000, "DraupnirCaveInstance", InstanceWay.Dialog, 700370, 220040000, DialogActionSetPro1, 39, InstancePacketLeave.None),
		new(300230000, "KromedesTrialInstance", InstanceWay.Dialog, 205234, 220040000, DialogActionSetPro1, 37, InstancePacketLeave.None),
		new(310110000, "TheobomosLabInstance", InstanceWay.Use, 730672, 220050000, 0, 46, InstancePacketLeave.None),
		new(320130000, "AdmaStrongholdInstance", InstanceWay.Use, 730151, 220050000, 0, 46, InstancePacketLeave.None),
		new(300160000, "LowerUdasTempleInstance", InstanceWay.Dialog, 730280, 220070000, DialogActionSetPro1, 51, InstancePacketLeave.None),
		new(300190000, "TalocsHollowInstance", InstanceWay.Dialog, 799325, 220070000, DialogActionSetPro1, 51, InstancePacketLeave.None),
		new(300250000, "EsoterraceInstance", InstanceWay.Dialog, 730409, 220070000, DialogActionSetPro1, 50, InstancePacketLeave.None, SiegeId: 3011),
		new(320150000, "PadmarashkasCaveInstance", InstanceWay.Dialog, 730486, 220070000, DialogActionSetPro1, 55, InstancePacketLeave.None),
		new(300170000, "BeshmundirInstance", InstanceWay.Beshmundir, 730231, 600010000, 0, 53, InstancePacketLeave.None),
		new(300700000, "TheHexwayInstance", InstanceWay.Use, 730731, 600010000, 0, 55, InstancePacketLeave.None),
		// Enshar, and Cygnea for the Asmodians' Anguished Dragon Lord's Refuge.
		new(300240000, "AturamSkyFortressInstance", InstanceWay.Dialog, 832994, 220080000, DialogActionSetPro1, 55, InstancePacketLeave.None),
		new(300280000, "RentusBaseInstance", InstanceWay.Dialog, 832992, 220080000, DialogActionSetPro1, 57, InstancePacketLeave.None),
		new(300620000, "OccupiedRentusBaseInstance", InstanceWay.Dialog, 832992, 220080000, DialogActionSetPro2, 65, InstancePacketLeave.None),
		new(300480000, "DanuarMysticariumInstance", InstanceWay.Dialog, 730722, 220080000, DialogActionSetPro1, 65, InstancePacketLeave.Exit,
			ItemId: 185000223, ItemCount: 1),
		new(300510000, "TiamatStrongHoldInstance", InstanceWay.Dialog, 832996, 220080000, DialogActionSetPro1, 57, InstancePacketLeave.None),
		new(300520000, "DragonLordsRefugeInstance", InstanceWay.Dialog, 832998, 220080000, DialogActionSetPro1, 60, InstancePacketLeave.None),
		new(300630000, "AnguishedDragonLordsRefugeInstance", InstanceWay.Dialog, 832997, 210070000, DialogActionSetPro1, 65, InstancePacketLeave.None),
		new(300590000, "OphidanBridgeInstance", InstanceWay.Dialog, 833002, 220080000, DialogActionSetPro1, 63, InstancePacketLeave.None),
		new(300610000, "RaksangRuinsInstance", InstanceWay.Dialog, 833000, 220080000, DialogActionSetPro1, 60, InstancePacketLeave.None),
		new(301140000, "SeizedDanuarSanctuaryInstance", InstanceWay.Dialog, 731549, 220080000, DialogActionSetPro2, 65, InstancePacketLeave.None),
		new(301380000, "DanuarSanctuaryInstance", InstanceWay.Dialog, 731549, 220080000, DialogActionSetPro1, 63, InstancePacketLeave.None),
		new(301390000, "DrakenspireDepthsInstance", InstanceWay.Dialog, 731547, 220080000, DialogActionSetPro1, 65, InstancePacketLeave.Exit),
		// Kaldor and Levinshor.
		new(300540000, "EternalBastionInstance", InstanceWay.Dialog, 833009, 600090000, DialogActionSetPro1, 65, InstancePacketLeave.None,
			ItemId: 186000236, ItemCount: 3),
		new(301110000, "DanuarReliquaryInstance", InstanceWay.Dialog, 804621, 600090000, DialogActionSetPro1, 65, InstancePacketLeave.None),
		new(301360000, "InfernalDanuarReliquaryInstance", InstanceWay.Dialog, 804621, 600090000, DialogActionSetPro2, 65, InstancePacketLeave.None),
		new(300800000, "InfinityShardInstance", InstanceWay.Dialog, 804623, 600100000, DialogActionSetPro1, 65, InstancePacketLeave.None),
		new(301130000, "SauroSupplyBaseInstance", InstanceWay.Dialog, 801966, 600100000, DialogActionSetPro1, 65, InstancePacketLeave.None),
		new(301230000, "IlluminaryObeliskInstance", InstanceWay.Dialog, 730883, 600100000, DialogActionSetPro1, 65, InstancePacketLeave.Exit),
		new(301370000, "InfernalIlluminaryObelisk", InstanceWay.Dialog, 730883, 600100000, DialogActionSetPro2, 65, InstancePacketLeave.Exit),
		new(301270000, "LinkgateFoundryInstance", InstanceWay.Dialog, 730973, 600100000, DialogActionSetPro1, 65, InstancePacketLeave.None),
		// Sanctum: the Elyos twin of the Asmodians' quest arena, entered from Epeios's quest dialog.
		new(310080000, "SanctumUndergroundArenaInstance", InstanceWay.Epeios, 203764, 110010000, DialogActionSetPro3, 26, InstancePacketLeave.None,
			Race: Race.ELYOS),
		// No player path in Java 4.8, only the GM's //goto: the legion barracks' entries are aggressive guards whose AI never
		// reads their portal path, the lucky instances have none, the vault's entry has the general AI.
		new(301240000, "LegionsKysisBarracks", InstanceWay.Goto, 0, 0, 0, 61, InstancePacketLeave.None),
		new(301250000, "LegionsMirenBarracks", InstanceWay.Goto, 0, 0, 0, 61, InstancePacketLeave.None),
		new(301260000, "LegionsKrotanBarracks", InstanceWay.Goto, 0, 0, 0, 61, InstancePacketLeave.None),
		new(301320000, "OphidanBridgeInstance_L", InstanceWay.Goto, 0, 0, 0, 63, InstancePacketLeave.None),
		new(301330000, "DanuarReliquaryInstance_L", InstanceWay.Goto, 0, 0, 0, 65, InstancePacketLeave.None),
		new(301400000, "TheShugoEmperorsVaultInstance", InstanceWay.Goto, 0, 0, 0, 40, InstancePacketLeave.Exit),
		// Stonespear Reach last: its entry is closed on Wednesdays from 08:00 to 10:59 (LegionDominionPortalAI).
		new(301500000, "StonespearReachInstance", InstanceWay.Dialog, 833025, 220080000, DialogActionSetPro1, 65, InstancePacketLeave.Dominion,
			ItemId: 185000230, ItemCount: 1, Legion: true),
	];

	private const ushort DialogActionSetPro1 = 10000, DialogActionSetPro2 = 10001, DialogActionSetPro3 = 10002, DialogActionSelect11 = 1012;
	private const ushort DialogActionInstanceEntry = 65, DialogActionSelectNone1 = 4763;

	/// <summary>The GM's //goto points (GoTo.java) of the handlers no player reaches.</summary>
	private static readonly Dictionary<int, (float X, float Y, float Z, byte H)> GotoPoints = new()
	{
		[301240000] = (528, 121, 176, 0),
		[301250000] = (528, 121, 176, 0),
		[301260000] = (528, 121, 176, 0),
		[301320000] = (750, 554, 574, 0),
		// GoTo.java gives Lucky Danuar Reliquary Lucky Ophidan Bridge's point (750, 554, 574), outside its 512 m map, which
		// World.createPosition refuses (Java's too); the director takes Danuar Reliquary's entry point (portal_loc 3011100), the
		// same map geometry (IDLDF5_Under_Rune).
		[301330000] = (255.7398f, 243.60954f, 241.90897f, 0),
		[301400000] = (542.9366f, 299.9885f, 401f, 22),
	};

	/// <summary>A subject of the matrix: its session, its name, whether it is in the world.</summary>
	private sealed class InstanceCast(SimulationL0Session session, string name, Race race)
	{
		public SimulationL0Session Session { get; } = session;
		public string Name { get; } = name;
		public Race Race { get; } = race;
		public int Id => Session.CharacterId;
		public bool Online { get; set; }
	}

	/// <summary>Every subject; the online ones are drained after each step of world time (harness rule 7).</summary>
	private InstanceCast[] instanceCast = [];

	private sealed record InstanceRowFailure(string Row, string Stage, Exception Error);

	private async Task RunInstancesAsync(ScenarioDefinition scenario, bool includeHistory)
	{
		try
		{
			await PlayInstancesAsync(scenario, includeHistory);
		}
		catch (Exception exception) when (exception is not SimulationLogPolicyException)
		{
			// Harness rule 2: the whole exception and every bot's recent packets.
			throw new InvalidOperationException("INSTANCES failed at " + SystemClock.UtcNow().ToString("O") + "; recent packets:" + Environment.NewLine +
				string.Join(Environment.NewLine, instanceCast.Select(actor => actor.Name + " " + actor.Session.CurrentStep + " " + string.Join(" | ",
					actor.Session.PacketHistory.TakeLast(25).Select(packet => packet.PacketType.Name +
						(packet.PacketType == typeof(SM_SYSTEM_MESSAGE) ? "(" + (packet.Get<string?>("name") ?? packet.Get<int>("msgId").ToString()) + ")" : ""))))), exception);
		}
	}

	private async Task PlayInstancesAsync(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(150));
		CancellationToken token = timeout.Token;
		// INSTANCES plays the instances a player enters through a portal at any time; an event scenario plays the instance whose
		// entry stands only while its event runs, in a process of its own inside the event.
		(DateTimeOffset epoch, InstanceRow[] scenarioRows, string? eventName) = scenario.Id switch
		{
			"INSTANCES" => (InstancesEpoch, InstanceRows, (string?)null),
			"INSTANCES-TOMB" => (InstancesTombEpoch, EventInstanceRows.Where(row => row.MapId == 300560000).ToArray(), "Shugo Imperial Tomb"),
			"INSTANCES-CIRCUS" => (InstancesCircusEpoch, EventInstanceRows.Where(row => row.MapId == 301200000).ToArray(), "Nightmare Circus"),
			_ => throw new ArgumentOutOfRangeException(nameof(scenario)),
		};
		Assert.Equal(epoch, fixture.Epoch);
		DateTimeOffset tuesday = new(epoch.Year, epoch.Month, epoch.Day, 0, 0, 0, TimeSpan.Zero);
		Assert.Equal(DayOfWeek.Tuesday, tuesday.DayOfWeek);
		if (eventName != null)
		{
			// The event's entry NPC stands from its start to its end (events/timed_events/retail_events.xml); the run ends in it.
			Assert.True(Aion.GameServer.Services.Event.EventService.GetInstance().IsEventActive(eventName), eventName + " is not running");
			Assert.True(DataManager.EVENT_DATA.GetEvents().Single(template => template.GetName() == eventName).GetEndDate() is DateTime end &&
				end > tuesday.AddDays(2).UtcDateTime, eventName + " ends before the run does");
		}
		Assert.True(SystemClock.UtcNow() > tuesday.AddHours(9));
		// instance.properties as shipped: no cooldown rate, the empty-instance checks' 600 s.
		Assert.Equal(1, InstanceConfig.INSTANCE_COOLDOWN_RATE);
		Assert.Equal(600, InstanceConfig.INSTANCE_DESTROY_DELAY_SECONDS);
		Assert.Equal(600, InstanceConfig.SOLO_INSTANCE_DESTROY_DELAY_SECONDS);
		// Every handler of the port is a row of this scenario, of INSTANCES-PVP or of an event scenario.
		Assert.Equal(InstanceHandlerMapIds().Order(), InstanceRows.Concat(EventInstanceRows).Select(row => row.MapId).Concat(PvpInstanceMapIds).Order());

		string[] names = ["Asiminsta", "Asiminstb", "Asiminstc", "Asiminstd", "Asiminste", "Asiminstf"];
		await using var a1 = new SimulationL0Session(fixture, policy, "b01", 49, names[0], Race.ASMODIANS);
		await using var a2 = new SimulationL0Session(fixture, policy, "b02", 50, names[1], Race.ASMODIANS);
		await using var a3 = new SimulationL0Session(fixture, policy, "b03", 62, names[2], Race.ASMODIANS);
		await using var a4 = new SimulationL0Session(fixture, policy, "b04", 63, names[3], Race.ASMODIANS);
		await using var a5 = new SimulationL0Session(fixture, policy, "b05", 92, names[4], Race.ASMODIANS);
		await using var a6 = new SimulationL0Session(fixture, policy, "b06", 112, names[5], Race.ASMODIANS);
		await using var e1 = new SimulationL0Session(fixture, policy, "b07", 203, "Elyinstg", Race.ELYOS);
		InstanceCast[] asmodians = [new(a1, names[0], Race.ASMODIANS), new(a2, names[1], Race.ASMODIANS), new(a3, names[2], Race.ASMODIANS),
			new(a4, names[3], Race.ASMODIANS), new(a5, names[4], Race.ASMODIANS), new(a6, names[5], Race.ASMODIANS)];
		InstanceCast elyos = new(e1, "Elyinstg", Race.ELYOS);
		instanceCast = [.. asmodians, elyos];

		// s01: login, class, and the Asmodians' legion by their own packets (S1's path).
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
		await MakeInstanceLegionAsync(asmodians, token);
		// Epeios's quest at the step that sends the player into the arena (Q1922 var 4).
		Player elyosPlayer = PlayerOf(elyos);
		var epeiosQuest = new QuestState(1922, QuestStatus.START);
		Assert.True(elyosPlayer.GetQuestStateList().AddQuest(1922, epeiosQuest));
		epeiosQuest.SetQuestVarById(0, 4);

		string[] only = (Environment.GetEnvironmentVariable("AION_SIM_INSTANCE_ROWS") ?? "")
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		InstanceRow[] rows = only.Length == 0 ? scenarioRows : scenarioRows.Where(row => only.Contains(row.MapId.ToString())).ToArray();
		var failures = new List<InstanceRowFailure>();

		// Tuesday: every row's entries up to its limit, and the refusal after it.
		foreach (InstanceRow row in rows)
			await PlayInstanceRowStageAsync(scenario, row, asmodians, elyos, "tuesday", failures, token,
				() => PlayInstanceDayAsync(row, asmodians, elyos, token));
		DateTimeOffset wednesday = tuesday.AddDays(1);
		Assert.True(SystemClock.UtcNow() < wednesday.AddHours(9), "Tuesday's rows ran past Wednesday's reset: " + SystemClock.UtcNow().ToString("O"));

		// Wednesday 09:00: the daily resets, the Wednesday ones and Monday-Wednesday-Friday-Sunday's (Engulfed Ophidan Bridge is
		// INSTANCES-PVP's); Stonespear Reach's entry opens at 11:00.
		await AdvanceInstanceWorldToAsync(wednesday.AddHours(9).AddSeconds(30), token, TimeSpan.FromSeconds(60));
		foreach (InstanceRow row in rows.Where(row => row.Counted && ResetAfter(row, tuesday.AddHours(10)) <= wednesday.AddHours(9)))
		{
			if (row.Legion)
				await AdvanceInstanceWorldToAsync(Max(SystemClock.UtcNow(), wednesday.AddHours(11)), token, TimeSpan.FromSeconds(10));
			await PlayInstanceRowStageAsync(scenario, row, asmodians, elyos, "reset", failures, token,
				() => PlayInstanceResetAsync(row, asmodians, elyos, token));
		}
		// Thursday 09:00: Tuesday-Thursday-Saturday-Sunday's weekly reset (the Eternal Bastion).
		InstanceRow[] thursdayRows = rows.Where(row => row.Counted && ResetAfter(row, tuesday.AddHours(10)) > wednesday.AddHours(9)).ToArray();
		if (thursdayRows.Length != 0)
		{
			await AdvanceInstanceWorldToAsync(wednesday.AddDays(1).AddHours(9).AddSeconds(30), token, TimeSpan.FromSeconds(60));
			foreach (InstanceRow row in thursdayRows)
				await PlayInstanceRowStageAsync(scenario, row, asmodians, elyos, "reset", failures, token,
					() => PlayInstanceResetAsync(row, asmodians, elyos, token));
		}

		foreach (InstanceRowFailure failure in failures)
			Console.WriteLine($"INSTANCES {failure.Row} {failure.Stage}: {failure.Error}");
		Assert.True(failures.Count == 0, $"{failures.Count} instance row stages failed: " +
			string.Join("; ", failures.Select(failure => $"{failure.Row} {failure.Stage}: {failure.Error.GetType().Name}: {FirstLine(failure.Error.Message)}")));
		foreach (InstanceCast actor in instanceCast.Where(actor => actor.Online))
			await actor.Session.QuitAsync(token);
		policy.AssertClean();
	}

	private static string FirstLine(string text) => text.Split('\n')[0];

	private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;

	/// <summary>
	/// One stage of a row under its own log policy, so that a problem names its handler. A failed stage is recorded and the
	/// subjects are brought back to a known state, so that one run shows every failing row; the scenario fails at its end.
	/// </summary>
	private async Task PlayInstanceRowStageAsync(ScenarioDefinition scenario, InstanceRow row, InstanceCast[] asmodians, InstanceCast elyos,
		string stage, List<InstanceRowFailure> failures, CancellationToken token, Func<Task> play)
	{
		// A row's own play fails on any warning too, not only on errors: "zero problems" for the handler's create path.
		using var rowPolicy = NewPolicy(scenario.Id, includeHistory: false, new SimulationLogPolicyOptions { FailOnWarnings = true });
		Action? restoreSiege = null;
		var details = new Dictionary<string, string>();
		ClearInstanceCastHistories();
		foreach (InstanceCast actor in instanceCast)
			actor.Session.BeginStep(row.MapId + "-" + stage, row.Handler);
		try
		{
			if (row.SiegeId != 0)
				SimulationSiegeSetup.GiveLocation(fixture.World, row.SiegeId, SiegeRace.ASMODIANS, restore => restoreSiege = restore, details, "siege");
			await play();
			rowPolicy.AssertClean();
			Console.WriteLine($"INSTANCES {row} {stage}: passed at {SystemClock.UtcNow():O}");
		}
		catch (Exception error)
		{
			failures.Add(new InstanceRowFailure(row.ToString(), stage, error));
			Console.WriteLine($"INSTANCES {row} {stage}: FAILED at {SystemClock.UtcNow():O}: {error}");
			Console.WriteLine("  recent packets: " + string.Join(Environment.NewLine + "  ", instanceCast.Where(actor => actor.Online).Select(actor =>
				actor.Name + ": " + string.Join(" | ", actor.Session.PacketHistory.TakeLast(15).Select(packet => packet.PacketType.Name +
					(packet.PacketType == typeof(SM_SYSTEM_MESSAGE) ? "(" + (packet.Get<string?>("name") ?? packet.Get<int>("msgId").ToString()) + ")" : ""))))));
			try { rowPolicy.AssertClean(); }
			catch (Exception logError) { Console.WriteLine("  log problems: " + logError.Message); }
			await RecoverInstanceCastAsync(token);
		}
		finally
		{
			restoreSiege?.Invoke();
		}
	}

	/// <summary>After a failed stage: everyone online, out of every team, out of every instance, back in Pandaemonium.</summary>
	private async Task RecoverInstanceCastAsync(CancellationToken token)
	{
		foreach (InstanceCast actor in instanceCast)
		{
			try
			{
				if (!actor.Online)
				{
					await InstanceReloginAsync(actor, token);
				}
				Player player = PlayerOf(actor);
				if (player.GetCurrentTeam() is { } team)
				{
					if (player.IsInLeague())
						Aion.GameServer.Model.Team.League.LeagueService.RemoveAlliance(player.GetPlayerAlliance());
					if (player.IsInAlliance())
						Aion.GameServer.Model.Team.Alliance.PlayerAllianceService.RemovePlayer(player);
					else if (player.IsInGroup())
						Aion.GameServer.Model.Team.Group.PlayerGroupService.RemovePlayer(player);
				}
				var start = DataManager.PLAYER_INITIAL_DATA.GetSpawnLocation(actor.Race);
				await TeleportForSetupAsync(actor.Session, player, start.GetMapId(), start.GetX(), start.GetY(), start.GetZ(), token);
			}
			catch (Exception error)
			{
				Console.WriteLine($"  recovery of {actor.Name} failed: {error.Message}");
			}
		}
		await AdvanceInstanceWorldAsync(TimeSpan.FromSeconds(2), token);
		await SyncInstanceCastAsync(token);
	}

	// ---------------------------------------------------------------------------------------------------------------------
	// A row's day and its reset

	/// <summary>
	/// Tuesday: as many entries as the row counts (two for a row with no limit, one for the GM's //goto), each in a new
	/// instance, each left and the instance destroyed; then, for a counted row, the refused entry.
	/// </summary>
	private async Task PlayInstanceDayAsync(InstanceRow row, InstanceCast[] asmodians, InstanceCast elyos, CancellationToken token)
	{
		InstanceCast[] party = PartyOf(row, asmodians, elyos);
		SetInstanceLevel(party, row.Level);
		InstanceCooltime cooltime = row.Cooltime;
		Assert.NotNull(cooltime);
		int entries = row.Way == InstanceWay.Goto ? 1 : row.Counted ? cooltime.GetMaxCount() : 2;
		for (int entry = 1; entry <= entries; entry++)
			await PlayInstanceCycleAsync(row, party, entry, token);
		if (row.Counted)
			await AssertInstanceRefusedAsync(row, party, token);
	}

	/// <summary>After the row's reset: the entry is no longer refused, and is counted from one again.</summary>
	private async Task PlayInstanceResetAsync(InstanceRow row, InstanceCast[] asmodians, InstanceCast elyos, CancellationToken token)
	{
		InstanceCast[] party = PartyOf(row, asmodians, elyos);
		SetInstanceLevel(party, row.Level);
		foreach (InstanceCast actor in party)
			Assert.False(PlayerOf(actor).GetPortalCooldownList().IsPortalUseDisabled(row.MapId), $"{actor.Name} is still refused {row}");
		await PlayInstanceCycleAsync(row, party, 1, token);
	}

	/// <summary>Who enters: the Elyos alone for the Elyos arena, the first Asmodian alone, a group as large as the instance takes
	/// (six, or three), every Asmodian in an alliance or a league.</summary>
	private static InstanceCast[] PartyOf(InstanceRow row, InstanceCast[] asmodians, InstanceCast elyos) =>
		row.Race == Race.ELYOS ? [elyos] : row.Team switch
		{
			InstanceTeam.Solo => [asmodians[0]],
			InstanceTeam.Group => asmodians.Take(row.MaxMembers).ToArray(),
			_ => asmodians,
		};

	private void SetInstanceLevel(InstanceCast[] party, byte level)
	{
		foreach (InstanceCast actor in party)
		{
			Player player = PlayerOf(actor);
			if (player.GetLevel() != level)
				player.GetCommonData().SetLevel(level);
			Assert.Equal((int)level, (int)player.GetLevel());
		}
	}

	/// <summary>One entry of the whole party, its leaving, and the instance's destruction.</summary>
	private async Task PlayInstanceCycleAsync(InstanceRow row, InstanceCast[] party, int entry, CancellationToken token)
	{
		await FormInstanceTeamAsync(row.Team, party, token);
		if (row.ItemId != 0)
			foreach (InstanceCast actor in row.Legion ? party.Take(1) : party)
				Assert.Equal(0, ItemService.AddItem(PlayerOf(actor), row.ItemId, row.ItemCount));
		WorldMapInstance instance = await EnterInstanceAsync(row, party, entry, token);
		await LeaveInstanceAsync(row, party, instance, token);
		await WaitForInstanceDestructionAsync(instance, token);
	}

	// ---------------------------------------------------------------------------------------------------------------------
	// Entering

	/// <summary>
	/// Every member of the party enters, the leader first (who makes the instance and registers the team), each through the
	/// row's entry; every entry lands in the same new instance, of the row's handler, with the party registered and the entry
	/// counted.
	/// </summary>
	private async Task<WorldMapInstance> EnterInstanceAsync(InstanceRow row, InstanceCast[] party, int entry, CancellationToken token)
	{
		Assert.Empty(InstancesOf(row.MapId));
		Npc? npc = row.Way == InstanceWay.Goto ? null : EntryNpc(row);
		WorldMapInstance? instance = null;
		for (int i = 0; i < party.Length; i++)
		{
			InstanceCast actor = party[i];
			Player player = PlayerOf(actor);
			int mark = actor.Session.PacketHistory.Count;
			if (npc == null)
			{
				// GoTo.goTo: the registered instance or a new one (InstanceService.getOrRegisterInstance), then a plain teleport.
				WorldMapInstance destination = InstanceService.GetOrRegisterInstance(row.MapId, player);
				var (x, y, z, h) = GotoPoints[row.MapId];
				TeleportService.TeleportTo(player, destination, x, y, z, h != 0 ? h : player.GetHeading(), TeleportAnimation.NONE);
				await actor.Session.DrainServerPacketsAsync(token);
			}
			else
			{
				await PlaceBesideAsync(actor, player, npc, i, token);
				mark = actor.Session.PacketHistory.Count;
				await UseInstanceEntryAsync(row, actor, npc, leader: i == 0, token);
			}
			await WaitForInstanceArrivalAsync(actor, row.MapId, mark, token);
			WorldMapInstance arrived = player.GetPosition().GetWorldMapInstance();
			Assert.Equal(row.MapId, arrived.GetMapId());
			if (instance == null)
				instance = arrived;
			else
				Assert.Same(instance, arrived);
			Assert.True(instance.IsRegistered(player.GetObjectId()), $"{actor.Name} is not registered with {row}");
			AssertInstanceEntryCounted(row, actor, player, entry, mark);
		}
		Assert.NotNull(instance);
		Assert.Single(InstancesOf(row.MapId));
		Assert.Equal(row.Handler, instance.GetInstanceHandler().GetType().Name);
		Assert.Equal(row.Way == InstanceWay.Epeios ? 1 : row.MaxMembers, instance.GetMaxPlayers());
		Assert.Equal(party.Length, instance.GetPlayersInside().Count);
		Player leaderPlayer = PlayerOf(party[0]);
		GeneralTeam? expectedTeam = row.Team switch
		{
			InstanceTeam.Group => leaderPlayer.GetPlayerGroup(),
			InstanceTeam.Alliance => leaderPlayer.GetPlayerAlliance(),
			InstanceTeam.League => leaderPlayer.GetPlayerAlliance().GetLeague(),
			_ => null,
		};
		Assert.Same(expectedTeam, instance.GetRegisteredTeam());
		return instance;
	}

	/// <summary>The entry of one member through the row's NPC, up to the point where the server teleports him.</summary>
	private async Task UseInstanceEntryAsync(InstanceRow row, InstanceCast actor, Npc npc, bool leader, CancellationToken token)
	{
		SimulationL0Session session = actor.Session;
		int target = npc.GetObjectId();
		switch (row.Way)
		{
			case InstanceWay.Dialog:
				await TalkToInstanceNpcAsync(actor, npc, token);
				await session.SendPacketAsync(session.Api.SelectDialog(target, row.Action), token);
				break;
			case InstanceWay.Use:
				// PortalAI: ActionItemNpcAI's use bar for the template's talk delay, then handleUseItemFinish ports.
				await session.SendPacketAsync(session.Api.TalkTo(target), token);
				int delay = await WaitForUseBarAsync(actor, npc, token);
				Assert.Equal(npc.GetObjectTemplate().GetTalkDelay() * 1000, delay);
				await AdvanceInstanceWorldAsync(TimeSpan.FromMilliseconds(delay + 100), token, TimeSpan.FromMilliseconds(delay + 100));
				break;
			case InstanceWay.Beshmundir:
				// BeshmundirsWalkAI: the use bar, the initial page 10, "I'm ready to enter" (INSTANCE_ENTRY); the leader picks the safer
				// path (SELECT_NONE_1) and accepts its question, whose answer moves him in; a member is moved in at once.
				await TalkToInstanceNpcAsync(actor, npc, token, expectedPage: 10);
				await session.SendPacketAsync(session.Api.SelectDialog(target, DialogActionInstanceEntry), token);
				if (leader)
				{
					await WaitForInstanceDialogAsync(actor, target, 4762, token);
					await session.SendPacketAsync(session.Api.SelectDialog(target, DialogActionSelectNone1), token);
					DecodedBotServerPacket question = await WaitForInstancePacketAsync(actor, typeof(SM_QUESTION_WINDOW),
						packet => packet.Get<int>("code") == SM_QUESTION_WINDOW.STR_INSTANCE_DUNGEON_WITH_DIFFICULTY_ENTER_CONFIRM, token);
					Assert.Equal(target, question.Get<int>("senderId"));
					await session.SendPacketAsync(session.Api.Answer(1), token);
				}
				break;
			case InstanceWay.Epeios:
				// _1922DeliveronYourPromises: Epeios's SETPRO3 at step 4 makes a new arena for the player and sends him in.
				await TalkToInstanceNpcAsync(actor, npc, token);
				await session.SendPacketAsync(session.Api.SelectDialog(target, row.Action, questId: 1922), token);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(row));
		}
	}

	/// <summary>
	/// CM_SHOW_DIALOG at an entry NPC: a dialog page, after the use bar when the NPC has a talk delay (PortalDialogAI hands
	/// a talk delay to ActionItemNpcAI and opens its dialog when the bar ends).
	/// </summary>
	private async Task<DecodedBotServerPacket> TalkToInstanceNpcAsync(InstanceCast actor, Npc npc, CancellationToken token, int? expectedPage = null)
	{
		SimulationL0Session session = actor.Session;
		int target = npc.GetObjectId();
		await session.SendPacketAsync(session.Api.TalkTo(target), token);
		DecodedBotServerPacket first = await WaitForInstancePacketAsync(actor, null, packet =>
			packet.PacketType == typeof(SM_DIALOG_WINDOW) && packet.Get<int>("targetObjectId") == target ||
			packet.PacketType == typeof(SM_USE_OBJECT) && packet.Get<int>("targetObjectId") == target && packet.Get<byte>("actionType") == 1, token);
		if (first.PacketType == typeof(SM_USE_OBJECT))
		{
			int delay = first.Get<int>("durationMs");
			await AdvanceInstanceWorldAsync(TimeSpan.FromMilliseconds(delay + 100), token, TimeSpan.FromMilliseconds(delay + 100));
			first = await WaitForInstanceDialogAsync(actor, target, expectedPage, token);
		}
		else if (expectedPage != null)
		{
			Assert.Equal(expectedPage.Value, first.Get<ushort>("dialogPageId"));
		}
		return first;
	}

	private async Task<int> WaitForUseBarAsync(InstanceCast actor, Npc npc, CancellationToken token)
	{
		DecodedBotServerPacket use = await WaitForInstancePacketAsync(actor, typeof(SM_USE_OBJECT),
			packet => packet.Get<int>("targetObjectId") == npc.GetObjectId() && packet.Get<byte>("actionType") == 1, token);
		return use.Get<int>("durationMs");
	}

	private Task<DecodedBotServerPacket> WaitForInstanceDialogAsync(InstanceCast actor, int target, int? page, CancellationToken token) =>
		WaitForInstancePacketAsync(actor, typeof(SM_DIALOG_WINDOW),
			packet => packet.Get<int>("targetObjectId") == target && (page == null || packet.Get<ushort>("dialogPageId") == page), token);

	/// <summary>
	/// A packet the server has already queued: no world time passes while a bot reads, so a packet that is not among the queued
	/// ones will not come (NR-46b), and the failure names what came instead.
	/// </summary>
	private static async Task<DecodedBotServerPacket> WaitForInstancePacketAsync(InstanceCast actor, Type? type, Func<DecodedBotServerPacket, bool> predicate,
		CancellationToken token)
	{
		int mark = actor.Session.PacketHistory.Count;
		DecodedBotServerPacket? packet = await actor.Session.WaitForPacketWithinAsync(type ?? typeof(object), TimeSpan.FromSeconds(30), token,
			candidate => (type == null || candidate.PacketType == type) && predicate(candidate));
		if (packet == null && type == null)
			packet = actor.Session.PacketHistory.Skip(mark).LastOrDefault(predicate);
		return packet ?? throw new InvalidOperationException($"{actor.Name} got no {type?.Name ?? "matching packet"}; got " +
			DescribeInstancePackets(actor.Session.PacketHistory.Skip(mark)));
	}

	private static string DescribeInstancePackets(IEnumerable<DecodedBotServerPacket> packets) => string.Join(", ", packets.Select(packet =>
		packet.PacketType.Name + (packet.PacketType == typeof(SM_SYSTEM_MESSAGE) ? "(" + (packet.Get<string?>("name") ?? packet.Get<int>("msgId").ToString()) + ")"
			: packet.PacketType == typeof(SM_DIALOG_WINDOW) ? "(" + packet.Get<ushort>("dialogPageId") + ")" : "")));

	/// <summary>The member arrives in <paramref name="mapId"/>: its spawn packet, its player info, then a time check.</summary>
	private static async Task WaitForInstanceArrivalAsync(InstanceCast actor, int mapId, int mark, CancellationToken token)
	{
		SimulationL0Session session = actor.Session;
		DecodedBotServerPacket? spawn = await session.WaitForPacketWithinAsync(typeof(SM_PLAYER_SPAWN), TimeSpan.FromSeconds(30), token,
			packet => packet.Get<int>("worldId") == mapId);
		if (spawn == null)
			throw new InvalidOperationException($"{actor.Name} did not arrive in {mapId}; got {DescribeInstancePackets(session.PacketHistory.Skip(mark))}");
		await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == actor.Id);
		await session.SynchronizeAsync(token);
		session.AcceptTeleportPosition();
	}

	/// <summary>
	/// The entry counted as PortalService.transfer (or AutoInstance.onPressEnter) counts it: the player's portal cooldown for the
	/// map has the entry count and the reset of InstanceCooltimeData.calculateInstanceEntranceCooltime, and
	/// PortalCooldownList.sendEntryInfo's SM_INSTANCE_INFO (update 2) carries them. A row that counts nothing stores nothing.
	/// </summary>
	private static void AssertInstanceEntryCounted(InstanceRow row, InstanceCast actor, Player player, int entry, int mark) =>
		AssertInstanceEntryCounted(row.MapId, row.Cooltime, row.Counted, actor, player, entry, mark);

	private static void AssertInstanceEntryCounted(int mapId, InstanceCooltime cooltime, bool counted, InstanceCast actor, Player player, int entry, int mark)
	{
		PortalCooldown cooldown = player.GetPortalCooldownList().GetPortalCooldown(mapId);
		DecodedBotServerPacket[] infos = actor.Session.PacketHistory.Skip(mark).Where(packet => packet.PacketType == typeof(SM_INSTANCE_INFO)
			&& packet.Get<List<BotInstanceEntry>>("entries").Any(item => item.PlayerId == actor.Id && item.CooldownId == cooltime.GetId())).ToArray();
		if (!counted)
		{
			Assert.Null(cooldown);
			Assert.Empty(infos);
			return;
		}
		Assert.NotNull(cooldown);
		long reset = ResetAfter(cooltime, SystemClock.UtcNow()).ToUnixTimeMilliseconds();
		Assert.Equal((entry, reset), (cooldown.GetEnterCount(), cooldown.GetReuseTime()));
		DecodedBotServerPacket info = Assert.Single(infos);
		Assert.Equal((byte)2, info.Get<byte>("updateType"));
		Assert.Equal(cooltime.GetId(), info.Get<int>("cooldownId"));
		BotInstanceEntry item = Assert.Single(info.Get<List<BotInstanceEntry>>("entries"));
		Assert.Equal(new BotInstanceEntry(actor.Id, cooltime.GetId(), (int)(reset - SystemClock.CurrentMillis()) / 1000, cooltime.GetMaxCount(), entry, true), item);
	}

	/// <summary>
	/// When the reset of an entry made at <paramref name="at"/> falls, by the cooltime row (the scenario's own calendar, UTC as
	/// the SIM runs): DAILY at the next ent_cool_time (hhmm), WEEKLY on the next listed day from there, RELATIVE after
	/// ent_cool_time minutes.
	/// </summary>
	private static DateTimeOffset ResetAfter(InstanceRow row, DateTimeOffset at) => ResetAfter(row.Cooltime, at);

	private static DateTimeOffset ResetAfter(InstanceCooltime cooltime, DateTimeOffset at)
	{
		at = at.ToUniversalTime();
		if (cooltime.GetCoolTimeType() == InstanceCoolTimeType.RELATIVE)
			return at.AddMinutes(cooltime.GetEntCoolTime());
		var reset = new DateTimeOffset(at.Year, at.Month, at.Day, cooltime.GetEntCoolTime() / 100, cooltime.GetEntCoolTime() % 100, 0, TimeSpan.Zero);
		if (at > reset)
			reset = reset.AddDays(1);
		if (cooltime.GetCoolTimeType() == InstanceCoolTimeType.WEEKLY)
		{
			int[] days = cooltime.GetTypeValue().Split(',').Select(day => Array.IndexOf(JavaDays, day) + 1).Order().ToArray();
			Assert.DoesNotContain(0, days);
			int today = Array.IndexOf(JavaDays, reset.DayOfWeek.ToString()[..3]) + 1;
			int next = days.Where(day => day >= today).DefaultIfEmpty(days[0] + 7).First();
			reset = reset.AddDays(next - today);
		}
		return reset;
	}

	private static readonly string[] JavaDays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

	// ---------------------------------------------------------------------------------------------------------------------
	// Leaving

	/// <summary>
	/// Every member sends CM_INSTANCE_LEAVE and gets what the handler's leaveInstance gives (the exit, the instance destroyed,
	/// the dominion's point, or nothing). Where it gives nothing, the party leaves as Java lets it: a group or an alliance leaves
	/// its team and is sent out 30 s later (PlayerLeavedEvent); a player alone or a league (whose leave sends nobody out)
	/// dissolves its teams, logs out until the empty instance is destroyed and logs back in at the exit
	/// (InstanceService.onPlayerLogin). A party still in a team when it is out leaves it, so that its instance is destroyed
	/// at the next check (EmptyInstanceCheckerTask: the registered team disbanded).
	/// </summary>
	private async Task LeaveInstanceAsync(InstanceRow row, InstanceCast[] party, WorldMapInstance instance, CancellationToken token)
	{
		foreach (InstanceCast actor in party)
		{
			Player player = PlayerOf(actor);
			if (!ReferenceEquals(instance, player.GetWorldMapInstance()))
				continue; // a destroyed instance has sent its whole party out
			int mark = actor.Session.PacketHistory.Count;
			await actor.Session.SendPacketAsync(GameClientPackets.InstanceLeave(), token);
			switch (row.PacketLeave)
			{
				case InstancePacketLeave.None:
					await actor.Session.SynchronizeAsync(token);
					Assert.DoesNotContain(actor.Session.PacketHistory.Skip(mark), packet => packet.PacketType == typeof(SM_TELEPORT_LOC) ||
						packet.PacketType == typeof(SM_PLAYER_SPAWN));
					Assert.Same(instance, player.GetWorldMapInstance());
					break;
				case InstancePacketLeave.Exit:
					await WaitForInstanceExitAsync(row, actor, player, mark, token);
					break;
				case InstancePacketLeave.Destroy:
					await WaitForInstanceExitAsync(row, actor, player, mark, token);
					Assert.Contains(actor.Session.PacketHistory.Skip(mark), packet => IsSystemMessage(packet, "STR_MSG_LEAVE_INSTANCE_FORCE"));
					Assert.Empty(InstancesOf(row.MapId));
					break;
				case InstancePacketLeave.Dominion:
					// StonespearReachInstance.leaveInstance: legion dominion 1's point in Enshar.
					await WaitForInstanceArrivalAsync(actor, 220080000, mark, token);
					Assert.Equal((220080000, 1770f, 2556f, 300f), (player.GetWorldId(), player.GetX(), player.GetY(), player.GetZ()));
					break;
			}
		}
		if (row.PacketLeave != InstancePacketLeave.None)
		{
			// Stonespear Reach's alliance stays together until its empty instance is destroyed, 600 s after the last player
			// left: StonespearReachInstance.onInstanceDestroy does not cancel the three-minute start timer of onInstanceCreate
			// (cancelAllTasks cancels the fail task and the stages' tasks only), so an instance destroyed in its first three
			// minutes, as a disbanded team's is, starts its stage after its destruction and spawns into nothing ("Error during
			// spawn", then an out-of-range point). Java-shared, kept, and not reached this way: the timer starts the empty
			// instance's stage, and its destruction cancels what the stage scheduled.
			if (row.PacketLeave == InstancePacketLeave.Dominion)
				await WaitForInstanceDestructionAsync(instance, token);
			await LeaveInstanceTeamAsync(row.Team, party, token);
			return;
		}
		if (row.Team is InstanceTeam.Group or InstanceTeam.Alliance)
		{
			int[] marks = party.Select(actor => actor.Session.PacketHistory.Count).ToArray();
			await LeaveInstanceTeamAsync(row.Team, party, token);
			for (int i = 0; i < party.Length; i++)
				Assert.Single(party[i].Session.PacketHistory.Skip(marks[i]), packet => IsSystemMessage(packet, "STR_MSG_LEAVE_INSTANCE_NOT_PARTY"));
			Assert.All(party, actor => Assert.Same(instance, PlayerOf(actor).GetWorldMapInstance()));
			await AdvanceInstanceWorldAsync(TimeSpan.FromSeconds(30), token, TimeSpan.FromSeconds(1));
			for (int i = 0; i < party.Length; i++)
				await WaitForInstanceExitAsync(row, party[i], PlayerOf(party[i]), marks[i], token);
			return;
		}
		await LeaveInstanceTeamAsync(row.Team, party, token);
		foreach (InstanceCast actor in party)
		{
			await actor.Session.QuitAsync(token);
			actor.Online = false;
		}
		await WaitForInstanceDestructionAsync(instance, token);
		foreach (InstanceCast actor in party)
		{
			int mark = actor.Session.PacketHistory.Count;
			await InstanceReloginAsync(actor, token);
			await AssertAtInstanceExitAsync(row, actor, PlayerOf(actor), mark, token);
		}
	}

	/// <summary>The member is sent to instance_exit.xml's point of the map for his race (TeleportService.moveToInstanceExit).</summary>
	private async Task WaitForInstanceExitAsync(InstanceRow row, InstanceCast actor, Player player, int mark, CancellationToken token)
	{
		InstanceExit exit = DataManager.INSTANCE_EXIT_DATA.GetInstanceExit(row.MapId, actor.Race);
		Assert.NotNull(exit);
		await WaitForInstanceArrivalAsync(actor, exit.GetExitWorld(), mark, token);
		await AssertAtInstanceExitAsync(row, actor, player, mark, token);
	}

	private Task AssertAtInstanceExitAsync(InstanceRow row, InstanceCast actor, Player player, int mark, CancellationToken token)
	{
		InstanceExit exit = DataManager.INSTANCE_EXIT_DATA.GetInstanceExit(row.MapId, actor.Race);
		Assert.NotNull(exit);
		Assert.True((exit.GetExitWorld(), exit.GetX(), exit.GetY(), exit.GetZ()) == (player.GetWorldId(), player.GetX(), player.GetY(), player.GetZ()),
			$"{actor.Name} left {row} for {player.GetWorldId()} ({player.GetX()}, {player.GetY()}, {player.GetZ()}), not its exit " +
			$"{exit.GetExitWorld()} ({exit.GetX()}, {exit.GetY()}, {exit.GetZ()}); got {DescribeInstancePackets(actor.Session.PacketHistory.Skip(mark))}");
		Assert.Equal(1, player.GetInstanceId());
		return Task.CompletedTask;
	}

	/// <summary>The empty instance's checker (every 60 s) destroys it: at once when its team disbanded, 600 s after the last
	/// player left otherwise.</summary>
	private async Task WaitForInstanceDestructionAsync(WorldMapInstance instance, CancellationToken token)
	{
		DateTimeOffset limit = SystemClock.UtcNow().AddMinutes(12);
		WorldMap map = fixture.World.GetWorldMap(instance.GetMapId());
		while (ReferenceEquals(map.GetWorldMapInstance(instance.GetInstanceId()), instance))
		{
			Assert.True(SystemClock.UtcNow() < limit, $"instance {instance.GetMapId()} [{instance.GetInstanceId()}] was not destroyed by {limit:O}");
			await AdvanceInstanceWorldAsync(TimeSpan.FromSeconds(5), token, TimeSpan.FromSeconds(5));
		}
	}

	// ---------------------------------------------------------------------------------------------------------------------
	// The refusal

	/// <summary>
	/// After the row's last counted entry, each member's next entry through the NPC is refused before any instance is made:
	/// PortalService.port (or the entry AI) sends STR_MSG_CANNOT_MAKE_INSTANCE_COOL_TIME and nobody moves. Beshmundir's walk
	/// takes a member in only behind his leader, so its leader alone is refused.
	/// </summary>
	private async Task AssertInstanceRefusedAsync(InstanceRow row, InstanceCast[] party, CancellationToken token)
	{
		await FormInstanceTeamAsync(row.Team, party, token);
		Npc npc = EntryNpc(row);
		InstanceCast[] attempting = row.Way == InstanceWay.Beshmundir ? party.Take(1).ToArray() : party;
		for (int i = 0; i < attempting.Length; i++)
		{
			InstanceCast actor = attempting[i];
			Player player = PlayerOf(actor);
			Assert.True(player.GetPortalCooldownList().IsPortalUseDisabled(row.MapId), $"{actor.Name} is not refused {row}");
			await PlaceBesideAsync(actor, player, npc, i, token);
			int mark = actor.Session.PacketHistory.Count;
			await UseInstanceEntryAsync(row, actor, npc, leader: i == 0, token);
			await actor.Session.SynchronizeAsync(token);
			DecodedBotServerPacket[] since = actor.Session.PacketHistory.Skip(mark).ToArray();
			Assert.True(since.Count(packet => IsSystemMessage(packet, "STR_MSG_CANNOT_MAKE_INSTANCE_COOL_TIME")) == 1,
				$"{actor.Name} was not refused {row} once; got {DescribeInstancePackets(since)}");
			Assert.DoesNotContain(since, packet => packet.PacketType == typeof(SM_TELEPORT_LOC) || packet.PacketType == typeof(SM_PLAYER_SPAWN));
			Assert.Equal(npc.GetWorldId(), player.GetWorldId());
			Assert.Empty(InstancesOf(row.MapId));
		}
		await LeaveInstanceTeamAsync(row.Team, party, token);
	}

	// ---------------------------------------------------------------------------------------------------------------------
	// Teams

	/// <summary>
	/// The party's team, made by the leader's invitations and each member's answer (CM_INVITE_TO_GROUP: 0 a group, question
	/// 60000; 12 an alliance, question 70000; a league of two alliances of three, the first's leader inviting the second's
	/// with 28, question 902249).
	/// </summary>
	private async Task FormInstanceTeamAsync(InstanceTeam team, InstanceCast[] party, CancellationToken token)
	{
		Assert.All(party, actor => Assert.Null(PlayerOf(actor).GetCurrentTeam()));
		switch (team)
		{
			case InstanceTeam.Solo:
				return;
			case InstanceTeam.Group:
				foreach (InstanceCast member in party.Skip(1))
					await InviteInstanceMemberAsync(party[0], member, 0, SM_QUESTION_WINDOW.STR_PARTY_DO_YOU_ACCEPT_INVITATION, token);
				Assert.Equal(party.Length, PlayerOf(party[0]).GetPlayerGroup().Size());
				break;
			case InstanceTeam.Alliance:
				foreach (InstanceCast member in party.Skip(1))
					await InviteInstanceMemberAsync(party[0], member, 12, SM_QUESTION_WINDOW.STR_PARTY_ALLIANCE_DO_YOU_ACCEPT_HIS_INVITATION, token);
				Assert.Equal(party.Length, PlayerOf(party[0]).GetPlayerAlliance().Size());
				break;
			case InstanceTeam.League:
				foreach (InstanceCast member in party.Skip(1).Take(2))
					await InviteInstanceMemberAsync(party[0], member, 12, SM_QUESTION_WINDOW.STR_PARTY_ALLIANCE_DO_YOU_ACCEPT_HIS_INVITATION, token);
				foreach (InstanceCast member in party.Skip(4))
					await InviteInstanceMemberAsync(party[3], member, 12, SM_QUESTION_WINDOW.STR_PARTY_ALLIANCE_DO_YOU_ACCEPT_HIS_INVITATION, token);
				await InviteInstanceMemberAsync(party[0], party[3], 28, SM_QUESTION_WINDOW.STR_MSGBOX_UNION_INVITE_ME, token);
				Assert.True(PlayerOf(party[0]).IsInLeague());
				Assert.Same(PlayerOf(party[0]).GetPlayerAlliance().GetLeague(), PlayerOf(party[5]).GetPlayerAlliance().GetLeague());
				break;
		}
		await SyncInstanceCastAsync(token);
		if (team == InstanceTeam.League)
			Assert.All(party, actor => Assert.Same(PlayerOf(party[0]).GetPlayerAlliance().GetLeague(), PlayerOf(actor).GetPlayerAlliance().GetLeague()));
		else
			Assert.All(party, actor => Assert.Same(PlayerOf(party[0]).GetCurrentTeam(), PlayerOf(actor).GetCurrentTeam()));
	}

	private async Task InviteInstanceMemberAsync(InstanceCast leader, InstanceCast member, byte inviteType, int question, CancellationToken token)
	{
		await leader.Session.SendPacketAsync(leader.Session.Api.InviteToGroup(member.Name, inviteType), token);
		await member.Session.DrainServerPacketsAsync(token);
		await WaitForInstancePacketAsync(member, typeof(SM_QUESTION_WINDOW), packet => packet.Get<int>("code") == question, token);
		await member.Session.SendPacketAsync(member.Session.Api.Answer(1), token);
		await leader.Session.DrainServerPacketsAsync(token);
	}

	/// <summary>
	/// The party leaves its team by its own packets (CM_PLAYER_STATUS_INFO): the second alliance leaves a league (29), then each
	/// member but the leader leaves the group (6, himself) or the alliance (14); the last one leaving disbands it.
	/// </summary>
	private async Task LeaveInstanceTeamAsync(InstanceTeam team, InstanceCast[] party, CancellationToken token)
	{
		if (team == InstanceTeam.Solo)
			return;
		if (team == InstanceTeam.League)
		{
			await party[3].Session.SendPacketAsync(GameClientPackets.TeamCommand(29), token);
			await SyncInstanceCastAsync(token);
			Assert.False(PlayerOf(party[0]).IsInLeague());
			foreach (InstanceCast member in party.Skip(1).Take(2).Concat(party.Skip(4)))
				await member.Session.SendPacketAsync(GameClientPackets.TeamCommand(14), token);
		}
		else
		{
			foreach (InstanceCast member in party.Skip(1))
				await member.Session.SendPacketAsync(GameClientPackets.TeamCommand(team == InstanceTeam.Group ? (byte)6 : (byte)14), token);
		}
		await SyncInstanceCastAsync(token);
		Assert.All(party, actor => Assert.Null(PlayerOf(actor).GetCurrentTeam()));
	}

	/// <summary>The Asmodians' legion by their own packets (S1's path): the first creates it, the others accept his invitation.</summary>
	private async Task MakeInstanceLegionAsync(InstanceCast[] asmodians, CancellationToken token)
	{
		InstanceCast general = asmodians[0];
		Assert.Equal(0, ItemService.AddItem(PlayerOf(general), BotWorldModel.KinahItemId, LegionConfig.LEGION_CREATE_REQUIRED_KINAH));
		await general.Session.SendPacketAsync(general.Session.Api.CreateLegion("Instancemarch"), token);
		await WaitForInstancePacketAsync(general, typeof(SM_SYSTEM_MESSAGE), packet => IsSystemMessage(packet, "STR_GUILD_CREATED"), token);
		foreach (InstanceCast member in asmodians.Skip(1))
		{
			await general.Session.SendPacketAsync(general.Session.Api.InviteToLegion(member.Name), token);
			await member.Session.DrainServerPacketsAsync(token);
			await WaitForInstancePacketAsync(member, typeof(SM_QUESTION_WINDOW), packet => packet.Get<int>("code") == 80001, token);
			await member.Session.SendPacketAsync(member.Session.Api.Answer(1), token);
			await general.Session.DrainServerPacketsAsync(token);
		}
		await SyncInstanceCastAsync(token);
		Legion legion = Assert.IsType<Legion>(PlayerOf(general).GetLegion());
		Assert.All(asmodians, actor => Assert.Same(legion, PlayerOf(actor).GetLegion()));
		// Director setup: the legion holds Stonespear Reach's dominion 1, which LegionDominionPortalAI asks of the captain's legion.
		legion.SetCurrentLegionDominion(1);
	}

	// ---------------------------------------------------------------------------------------------------------------------
	// World

	/// <summary>The row's entry NPC as it stands in its map (the main instance), the first by object id.</summary>
	private Npc EntryNpc(InstanceRow row)
	{
		WorldMapInstance main = fixture.World.GetWorldMap(row.NpcMap).GetMainWorldMapInstance();
		Npc? npc = main.GetNpcs(row.NpcId).Where(candidate => candidate.IsSpawned()).OrderBy(candidate => candidate.GetObjectId()).FirstOrDefault();
		Assert.True(npc != null, $"{row}: no spawned entry NPC {row.NpcId} in {row.NpcMap}");
		return npc;
	}

	/// <summary>A setup teleport beside the NPC, on a circle of 1.5 m around it, the member at its index; then the NPC is known.</summary>
	private async Task PlaceBesideAsync(InstanceCast actor, Player player, Npc npc, int index, CancellationToken token)
	{
		ClearAggressiveMonstersNear(npc, player);
		double angle = index * Math.PI / 3;
		float x = npc.GetX() + 1.5f * (float)Math.Cos(angle), y = npc.GetY() + 1.5f * (float)Math.Sin(angle);
		await TeleportForSetupAsync(actor.Session, player, npc.GetWorldId(), x, y, npc.GetZ(), token, npc.GetInstanceId());
		if (!actor.Session.Api.World.Objects.ContainsKey(npc.GetObjectId()))
			await WaitForInstancePacketAsync(actor, typeof(SM_NPC_INFO), packet => packet.Get<int>("objectId") == npc.GetObjectId(), token);
		Assert.True(PositionUtil.IsInTalkRange(player, npc), $"{actor.Name} is not in talk range of {npc.GetNpcId()}");
	}

	/// <summary>
	/// Director setup before a subject stands at an entry: every monster aggressive to him within 40 m of the entry NPC is deleted,
	/// so that an entry whose NPC has a use bar (ActionItemNpcAI, aborted by an attack) is not interrupted by the camp beside it
	/// (Adma Stronghold's and the Hexway's entries, the Eternal Bastion's in Kaldor).
	/// </summary>
	private static void ClearAggressiveMonstersNear(Npc entry, Player player)
	{
		foreach (Npc npc in entry.GetPosition().GetWorldMapInstance().GetNpcs().Where(npc => npc.IsSpawned() && !npc.IsDead() &&
			npc.GetTypeValue(player) == CreatureType.AGGRESSIVE && PositionUtil.IsInRange(npc, entry, 40)).ToList())
			npc.GetController().Delete();
	}

	/// <summary>The instances entries made in the map: the map's default instances (WorldMap's constructor, inaccessible, no
	/// player limit) are left out.</summary>
	/// <summary>Drops every subject's packet history between stages, where no mark into it is held: a day of a world's packets
	/// for dozens of bots would otherwise pile up in memory.</summary>
	private void ClearInstanceCastHistories()
	{
		foreach (InstanceCast actor in instanceCast)
		{
			actor.Session.PacketHistory.Clear();
			actor.Session.PacketObservations.Clear();
			actor.Session.PacketTypes.Clear();
		}
	}

	private List<WorldMapInstance> InstancesOf(int mapId) => fixture.World.GetWorldMap(mapId).Where(instance => instance.GetMaxPlayers() != 0).ToList();

	private Player PlayerOf(InstanceCast actor) => fixture.World.GetPlayer(actor.Id)
		?? throw new InvalidOperationException($"{actor.Name} is not in the world.");

	private async Task SyncInstanceCastAsync(CancellationToken token)
	{
		foreach (InstanceCast actor in instanceCast.Where(actor => actor.Online))
			await actor.Session.SynchronizeAsync(token);
	}

	/// <summary>Logs a known character back in: the character list, the same character, the world.</summary>
	private async Task InstanceReloginAsync(InstanceCast actor, CancellationToken token)
	{
		await actor.Session.LoginCharacterListAsync(token);
		actor.Session.SelectCharacter(actor.Id, actor.Name);
		await actor.Session.EnterWorldAsync(token);
		actor.Online = true;
		await actor.Session.SynchronizeAsync(token);
		actor.Session.AcceptTeleportPosition();
	}

	private Task AdvanceInstanceWorldAsync(TimeSpan duration, CancellationToken token, TimeSpan? step = null) =>
		AdvanceInstanceWorldToAsync(SystemClock.UtcNow() + duration, token, step);

	/// <summary>
	/// Moves the world's clock to <paramref name="target"/> in steps of at most <paramref name="step"/> (1 s by default),
	/// serializing every online bot's packets after each step (harness rule 7). With nobody online the clock is moved directly.
	/// </summary>
	private async Task AdvanceInstanceWorldToAsync(DateTimeOffset target, CancellationToken token, TimeSpan? step = null)
	{
		TimeSpan most = step ?? TimeSpan.FromSeconds(1);
		while (SystemClock.UtcNow() < target)
		{
			TimeSpan next = target - SystemClock.UtcNow();
			if (next > most)
				next = most;
			InstanceCast[] online = instanceCast.Where(actor => actor.Online).ToArray();
			if (online.Length == 0)
			{
				fixture.Clock.Advance(next);
				continue;
			}
			await online[0].Session.AdvanceAsync(next, token);
			foreach (InstanceCast actor in online.Skip(1))
				await actor.Session.DrainServerPacketsAsync(token);
		}
	}

	/// <summary>The maps of the port's instance handlers (their InstanceID), as InstanceEngine registers them.</summary>
	private static int[] InstanceHandlerMapIds() => typeof(GeneralInstanceHandler).Assembly.GetTypes()
		.Select(type => type.GetCustomAttributes(typeof(InstanceID), false).OfType<InstanceID>().FirstOrDefault())
		.Where(id => id != null).Select(id => id!.Value).ToArray();

	/// <summary>The auto-group instances (dredgions, battlegrounds, arenas and training grounds): scenario INSTANCES-PVP.</summary>
	private static readonly int[] PvpInstanceMapIds =
		[300110000, 300210000, 300440000, 301120000, 301210000, 301220000, 301310000, 300350000, 300360000, 300420000, 300430000, 300450000, 300550000, 300570000];

	/// <summary>
	/// The instances whose entry NPC is an event spawn (events/timed_events/retail_events.xml), each played by an event scenario in
	/// its own process inside the event: Shugo Imperial Tomb (2024-08-12 to 25; Indianerk in Pandaemonium, page 1012, the portal
	/// path's dialog 1015, a group of three) and the Nightmare Circus (2024-10-28 to 11-03; Saia in Pandaemonium).
	/// </summary>
	private static readonly InstanceRow[] EventInstanceRows =
	[
		new(300560000, "ShugoImperialTombInstance", InstanceWay.Dialog, 831117, 120010000, 1015, 45, InstancePacketLeave.Exit),
		new(301200000, "NightmareCircus", InstanceWay.Dialog, 831714, 120010000, DialogActionSetPro1, 30, InstancePacketLeave.None),
	];
}
