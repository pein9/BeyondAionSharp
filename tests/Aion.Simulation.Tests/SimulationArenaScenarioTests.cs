using System.Buffers.Binary;
using System.Text;
using Aion.Bots.Protocol;
using Aion.Bots.Reflexes;
using Aion.Bots.Scenarios;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Handlers.Instance;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Autogroup;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Model.Templates.Rewards;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.Simulation.Tests;

/// <summary>
/// P11-03, scenario ARENA: the PvP arenas of 4.8 and the auto-group queue (<c>CM_AUTO_GROUP</c>) played through on the virtual
/// clock from the process's own epoch, Saturday 2026-10-10 09:58 UTC (<c>reset-ARENA</c>), with six ordinary Asmodian
/// Gladiators: the Arena of Discipline (two players, and a lone one whose arena cannot start), the Arena of Harmony (two groups
/// of three), the Baranath Dredgion's periodic registration at noon (the queue only: six Asmodians cannot fill a dredgion) and
/// the Arena of Glory at 20:00 (four players and a quick entry into the running arena), every expected value from the Java
/// code: <c>AutoGroupService</c>, <c>AutoGroupUtility</c>,
/// <c>AutoPvPFFAInstance</c>, <c>AutoHarmonyInstance</c>, <c>AutoPvpInstance</c>, <c>LookingForParty</c>,
/// <c>PeriodicInstanceManager</c>, <c>PvPArenaService</c>, <c>CM_AUTO_GROUP</c>, <c>SM_AUTO_GROUP</c>, <c>PortalDialogAI</c>,
/// <c>DialogService</c> (MATCH_MAKER), <c>PvPArenaInstance</c>, <c>DisciplineTrainingGroundsInstance</c>,
/// <c>ArenaOfDisciplineInstance</c>, <c>HarmonyTrainingGroundsInstance</c>, <c>ArenaOfHarmonyInstance</c>, <c>ArenaOfGloryInstance</c>,
/// <c>PvPArenaScore</c>, <c>HarmonyArenaScore</c>, <c>PvPArenaPlayerReward</c>, <c>HarmonyGroupReward</c>,
/// <c>ArenaScoreWriter</c>, <c>HarmonyScoreWriter</c> and the instance positions of the three arenas.
/// <para>
/// Director setup, recorded here as the plan asks: every subject is made a level-46 Gladiator (the arenas of Marchutan Priory
/// take 46 to 50 at their first difficulty, Baranath Dredgion 46 to 50), one is made level 51 before noon for the second
/// dredgion and five are made level 56 before 20:00 for the Arena of Glory (56 to 60); the entry tickets are given (186000135
/// for the Arena of Discipline, 186000184 for the Arena of Harmony, three 186000185 for the Arena of Glory), with subjects
/// kept short of them for the refusals; the subjects are placed by setup teleports beside the arena gates of Marchutan
/// Priory and beside Rukbar, the dredgion's match maker in Reshanta, and, inside an arena, beside the opponent before a fight;
/// the monsters aggressive to the players are deleted from each arena once they are inside and after each round's ports;
/// the victim's health is lowered to <see cref="ArenaVictimHealth"/> before each blow of a fight (a level-56 player's natural
/// regeneration outpaces a starter weapon), so that real auto-attacks finish him.
/// </para>
/// </summary>
public sealed partial class SimulationFastScenarioTests
{
	internal static readonly DateTimeOffset ArenaEpoch = new(2026, 10, 10, 9, 58, 0, TimeSpan.Zero);
	private const int ArenaOfDiscipline = 300360000;
	private const int ArenaOfHarmony = 300450000;
	private const int DisciplineMask = 24; // ARENA_OF_DISCIPLINE_1, levels 46-50, difficulty 1
	private const int HarmonyMask = 33; // ARENA_OF_HARMONY_1, levels 46-50, difficulty 1
	private const int GloryMask = 38; // ARENA_OF_GLORY_1, levels 56-60
	private const int DisciplineGate = 730484; // Marchutan Priory
	private const int HarmonyGate = 730577;
	private const int GloryGate = 730645;
	private const int DisciplineTicket = 186000135;
	private const int HarmonyTicket = 186000184;
	private const int GloryTicket = 186000185;
	private const int ArenaOfGlory = 300550000;
	private const int CrucibleInsignia = 186000130;
	private const int CourageInsignia = 186000137;
	private const ushort InstancePartyMatch = 77; // DialogAction.INSTANCE_PARTY_MATCH
	private const int ArenaWindowRequest = 0, ArenaWindowWaiting = 1, ArenaWindowCancel = 2, ArenaWindowEnter = 4, ArenaWindowEntered = 5,
		ArenaWindowIcon = 6;

	/// <summary>The health the director leaves a victim with before each of an opponent's auto-attacks (one blow of a starter weapon at 46 or 56).</summary>
	private const int ArenaVictimHealth = 20;

	/// <summary>InstanceProgressionType ids as SM_INSTANCE_SCORE writes them.</summary>
	private const int Preparing = 1 << 20, StartProgress = 2 << 20, EndProgress = 3 << 20;

	/// <summary>DisciplineInstancePosition.port: [position - 1][zone - 1] = (x, y, z, heading).</summary>
	private static readonly (float X, float Y, float Z, sbyte H)[][] DisciplinePositions =
	[
		[(1841.294f, 1041.223f, 338.20056f, 15), (278.18478f, 1265.8389f, 263.1712f, 73), (709.78845f, 1766.1855f, 183.43953f, 60), (1817.1067f, 1737.4899f, 311.49692f, 1)],
		[(1869.4803f, 1041.8444f, 337.9918f, 43), (251.03516f, 1297.7039f, 248.11426f, 105), (693.93176f, 1761.0234f, 196.12753f, 21), (1851.6932f, 1765.4813f, 305.23187f, 90)],
		[(1869.0569f, 1069.1344f, 337.6657f, 71), (315.8269f, 1221.0648f, 263.4517f, 51), (686.09247f, 1756.8987f, 163.4386f, 25), (1851.7856f, 1709.3085f, 305.23566f, 31)],
		[(1841.7906f, 1069.6471f, 338.10706f, 107), (346.1267f, 1185.1802f, 244.43742f, 44), (693.11945f, 1771.6886f, 236.5583f, 17), (1887.0206f, 1737.6492f, 311.49692f, 62)],
	];

	/// <summary>HarmonyInstancePosition.port: [position - 1][zone - 1] = (x, y, z); every heading is 0.</summary>
	private static readonly (float X, float Y, float Z)[][] HarmonyPositions =
	[
		[(1851.0062f, 1702.5195f, 310.42856f), (446.29938f, 1805.3363f, 172.48746f), (523.44098f, 1175.9973f, 433.18271f), (484.3735f, 385.22284f, 186.00145f), (1553.1068f, 340.37198f, 200.51918f), (1858.165f, 1132.8064f, 217.73499f)],
		[(1854.8535f, 1702.7606f, 310.42856f), (441.7691f, 1780.3921f, 172.4404f), (536.21954f, 1152.8105f, 433.18625f), (503.5722f, 376.03281f, 196.5117f), (1575.3278f, 352.82626f, 203.68724f), (1818.9377f, 1187.3922f, 217.85521f)],
		[(1858.8142f, 1702.8604f, 310.42856f), (445.51358f, 1756.308f, 174.83492f), (529.83026f, 1123.1311f, 433.16101f), (513.78851f, 387.05893f, 202.90605f), (1572.9283f, 356.70764f, 204.03906f), (1860.4971f, 1200.7257f, 229.35692f)],
		[(1815.9524f, 1732.0352f, 310.44937f), (474.90109f, 1750.0125f, 172.47906f), (507.31659f, 1102.8146f, 433.29669f), (507.27774f, 407.05258f, 209.96556f), (1587.1409f, 354.17056f, 198.43542f), (1842.7477f, 1162.9127f, 217.70515f)],
		[(1815.9185f, 1736.5698f, 310.44937f), (495.06195f, 1747.3091f, 173.5493f), (506.33411f, 1131.1976f, 433.19296f), (482.01779f, 412.5983f, 218.26518f), (1588.2511f, 378.15894f, 200.05574f), (1830.0492f, 1203.9077f, 220.14816f)],
		[(1815.7126f, 1741.1333f, 310.44937f), (515.41498f, 1749.9364f, 171.58592f), (496.44434f, 1114.3512f, 433.28073f), (468.30084f, 398.0802f, 225.12308f), (1560.8124f, 389.32593f, 202.26889f), (1850.8292f, 1173.2179f, 230.76379f)],
		[(1853.0728f, 1772.45f, 310.44879f), (543.84558f, 1755.5155f, 173.66292f), (510.25061f, 1148.4823f, 433.18365f), (468.2298f, 384.94141f, 219.99139f), (1557.0649f, 389.01395f, 203.41882f), (1851.0189f, 1145.1492f, 231.58502f)],
		[(1849.2573f, 1772.3376f, 310.44879f), (549.66162f, 1780.8221f, 171.59732f), (497.4404f, 1160.4882f, 433.11658f), (480.7193f, 368.64465f, 216.83505f), (1553.0083f, 388.73923f, 203.36218f), (1828.4608f, 1130.5066f, 223.68491f)],
		[(1845.6466f, 1772.197f, 310.44879f), (544.16589f, 1806.5024f, 173.60339f), (486.33145f, 1173.7235f, 433.16763f), (499.29306f, 374.73746f, 210.01361f), (1558.7871f, 372.97946f, 203.56261f), (1832.5094f, 1148.7605f, 217.78876f)],
		[(1886.043f, 1733.7983f, 310.41644f), (514.97675f, 1811.9063f, 172.38895f), (473.27823f, 1153.5302f, 433.13321f), (499.47296f, 393.79526f, 204.07152f), (1539.7013f, 377.34818f, 204.68753f), (1814.7136f, 1149.2618f, 217.65062f)],
		[(1885.9547f, 1737.7559f, 310.41644f), (495.44571f, 1811.359f, 175.76955f), (474.12839f, 1123.361f, 441.54596f), (482.25574f, 391.00235f, 195.26796f), (1535.8186f, 375.17035f, 204.85748f), (1813.3f, 1176.4271f, 217.70609f)],
		[(1885.8027f, 1741.9368f, 310.41644f), (474.75629f, 1811.5469f, 173.62592f), (468.48843f, 1137.6251f, 436.42511f), (490.99121f, 394.31186f, 211.07214f), (1548.5005f, 366.883f, 198.40457f), (1847.0271f, 1149.4534f, 217.66801f)],
	];

	/// <summary>GloryInstancePosition.port: [position - 1][zone - 1] = (x, y, z); every heading is 0.</summary>
	private static readonly (float X, float Y, float Z)[][] GloryPositions =
	[
		[(1728.7698f, 1735.5115f, 270.35883f), (1848.7509f, 1122.0343f, 338.25327f), (1601.3693f, 262.18213f, 507.90225f), (695.56299f, 1830.2776f, 236.63037f), (466.32935f, 1137.7463f, 436.90363f), (491.25992f, 396.44275f, 210.79913f)],
		[(1782.6428f, 1749.6447f, 270.2099f), (1840.5874f, 1131.8651f, 337.86191f), (1601.5317f, 255.78668f, 508.06595f), (693.49695f, 1826.8826f, 236.33366f), (491.62064f, 1113.132f, 435.51141f), (481.56711f, 392.17386f, 195.64738f)],
		[(1779.3856f, 1763.3234f, 270.1344f), (1840.9456f, 1148.8275f, 338.19537f), (1651.7954f, 236.90672f, 514.25f), (681.13123f, 1813.1442f, 204.35895f), (487.12833f, 1174.527f, 432.93042f), (502.35532f, 383.3923f, 207.53601f)],
		[(1789.2378f, 1766.1699f, 270.11316f), (1849.8066f, 1158.5144f, 338.1109f), (1648.1063f, 233.50453f, 514.00116f), (713.56201f, 1829.1324f, 188.6698f), (492.18265f, 1146.7538f, 433.27365f), (470.37747f, 379.49329f, 219.26476f)],
		[(1813.1642f, 1704.0671f, 272.46228f), (1868.9424f, 1122.505f, 337.85699f), (1647.5758f, 237.77393f, 514.22351f), (695.18799f, 1816.3867f, 195.84592f), (504.25089f, 1130.025f, 433.1051f), (484.43649f, 382.09552f, 187.54836f)],
		[(1786.9512f, 1792.4897f, 272.69449f), (1877.2324f, 1130.4788f, 337.91608f), (1651.5988f, 279.14572f, 514.23566f), (711.16937f, 1822.9017f, 183.47443f), (509.46603f, 1169.5813f, 433.32385f), (508.7309f, 378.73807f, 199.08717f)],
		[(1783.3029f, 1686.1635f, 280.87762f), (1877.5284f, 1150.0294f, 338.69321f), (1648.1837f, 282.2934f, 514.32208f), (675.26306f, 1836.4603f, 171.69542f), (520.46014f, 1151.5432f, 432.93042f), (507.07452f, 406.64331f, 209.75935f)],
		[(1812.7941f, 1789.109f, 288.74359f), (1869.6437f, 1157.5081f, 338.42526f), (1647.8574f, 278.70276f, 514.76154f), (703.90625f, 1827.9193f, 163.59734f), (530.52057f, 1123.7206f, 433.0639f), (471.09128f, 400.79608f, 224.43683f)],
	];

	/// <summary>The scenario's subjects, every one drained after each step of world time (harness rule 7).</summary>
	private SimulationL0Session[] arenaCast = [];

	private async Task RunArenaAsync(ScenarioDefinition scenario, bool includeHistory)
	{
		var sessions = new List<SimulationL0Session>();
		try
		{
			await PlayArenaAsync(scenario, includeHistory, sessions);
		}
		catch (Exception exception) when (exception is not SimulationLogPolicyException)
		{
			// Harness rule 2: the whole exception and every bot's recent packets.
			throw new InvalidOperationException("ARENA failed at " + SystemClock.UtcNow().ToString("O") + "; recent packets:" + Environment.NewLine +
				string.Join(Environment.NewLine, sessions.Select(session => session.CurrentStep + " " + string.Join(" | ",
					session.PacketHistory.TakeLast(25).Select(packet => packet.PacketType.Name +
						(packet.PacketType == typeof(SM_SYSTEM_MESSAGE) ? "(" + (packet.Get<string?>("name") ?? packet.Get<int>("msgId").ToString()) + ")" : ""))))), exception);
		}
	}

	private async Task PlayArenaAsync(ScenarioDefinition scenario, bool includeHistory, List<SimulationL0Session> created)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(40));
		CancellationToken token = timeout.Token;
		Assert.Equal(ArenaEpoch, fixture.Epoch);
		DateTimeOffset day = new(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);
		Assert.Equal(DayOfWeek.Saturday, day.DayOfWeek);
		Assert.True(SystemClock.UtcNow() < day.AddHours(10));
		// autogroup.properties as shipped: the queue on, the arenas' hours on, the dredgion's registration at 0, 12 and 20 o'clock
		// for 60 minutes, no announcement of battleground registrations.
		Assert.True(AutoGroupConfig.AUTO_GROUP_ENABLE && AutoGroupConfig.START_TIME_ENABLE && !AutoGroupConfig.ANNOUNCE_BATTLEGROUND_REGISTRATIONS);
		Assert.Equal(60, AutoGroupConfig.DREDGION_REGISTRATION_PERIOD);
		Assert.Equal(["0 0 0,12,20 ? * *"], AutoGroupConfig.DREDGION_TIMES.Select(cron => cron.CronExpressionString));

		await using var first = new SimulationL0Session(fixture, policy, "b01", 161, "Asimarenaa", Race.ASMODIANS);
		await using var second = new SimulationL0Session(fixture, policy, "b02", 162, "Asimarenab", Race.ASMODIANS);
		await using var third = new SimulationL0Session(fixture, policy, "b03", 163, "Asimarenac", Race.ASMODIANS);
		await using var fourth = new SimulationL0Session(fixture, policy, "b04", 164, "Asimarenad", Race.ASMODIANS);
		await using var fifth = new SimulationL0Session(fixture, policy, "b05", 165, "Asimarenae", Race.ASMODIANS);
		await using var sixth = new SimulationL0Session(fixture, policy, "b06", 166, "Asimarenaf", Race.ASMODIANS);
		SimulationL0Session[] sessions = [first, second, third, fourth, fifth, sixth];
		created.AddRange(sessions);
		arenaCast = sessions;

		// s01: login, class and level, and the setup teleports beside the arena gates of Marchutan Priory.
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
			player.GetCommonData().SetLevel(46);
		}
		Player[] players = sessions.Select(session => fixture.World.GetPlayer(session.CharacterId)).ToArray();
		Player firstPlayer = players[0], secondPlayer = players[1], thirdPlayer = players[2], fourthPlayer = players[3], fifthPlayer = players[4];
		for (int i = 0; i < sessions.Length; i++)
			await TeleportBesideGateAsync(sessions[i], players[i], i == 5 ? GloryGate : DisciplineGate, i, token);
		await AdvanceSiegeWorldToAsync(sessions, SystemClock.UtcNow().AddSeconds(2), token);
		await SyncSiegeSessionsAsync(sessions, token);
		// The tickets of the Arena of Discipline: one each for the first four; the fifth has none.
		foreach (Player player in players.Take(4))
			Assert.Equal(0, ItemService.AddItem(player, DisciplineTicket, 1));
		Assert.Equal(0, fifthPlayer.GetInventory().GetItemCountByItemId(DisciplineTicket));
		await SyncSiegeSessionsAsync(sessions, token);

		// s02, 09:59: Saturday's arena hours begin at 10:00 (PvPArenaService.isPvPArenaAvailable), so the registration is refused
		// with the arena's closed time; a gate whose arena the level does not take offers no entry window at all.
		first.BeginStep("s02", "arena-closed-before-ten");
		await AdvanceSiegeWorldToAsync(sessions, day.AddHours(9).AddMinutes(59), token);
		await OpenEntryWindowAsync(first, DisciplineGate, DisciplineMask, ArenaOfDiscipline, 402046, 903479, token);
		int mark = first.PacketHistory.Count;
		await first.SendPacketAsync(GameClientPackets.AutoGroup(DisciplineMask, 100, 0), token);
		await first.SynchronizeAsync(token);
		AssertArenaRefusal(first, mark, "STR_MSG_INSTANCE_CLOSED_TIME", ArenaOfDiscipline.ToString());
		Assert.False(AutoGroupService.GetInstance().IsSearching(firstPlayer, DisciplineMask));
		sixth.BeginStep("s02", "glory-gate-offers-no-window");
		mark = sixth.PacketHistory.Count;
		int gloryGate = await sixth.WaitForNpcAsync(GloryGate, token);
		await sixth.SendPacketAsync(sixth.Api.TalkTo(gloryGate), token);
		await sixth.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == gloryGate && packet.Get<ushort>("dialogPageId") == 1011);
		await sixth.SendPacketAsync(sixth.Api.SelectDialog(gloryGate, InstancePartyMatch), token);
		await sixth.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == gloryGate && packet.Get<ushort>("dialogPageId") == 0);
		await sixth.SynchronizeAsync(token);
		// AutoGroupType.getAutoGroup(46, 730645): the Arena of Glory takes 56 to 60, so PortalDialogAI sends only the closing dialog.
		Assert.DoesNotContain(sixth.PacketHistory.Skip(mark), packet => packet.PacketType == typeof(SM_AUTO_GROUP));
		Assert.Null(AutoGroupTypeExtensions.GetAutoGroup(46, GloryGate));

		// s03, 10:00: the arena is open. The fifth, without a ticket, is refused; the first registers alone (NEW_GROUP_ENTRY),
		// and a second registration of his is refused as already registered.
		await AdvanceSiegeWorldToAsync(sessions, day.AddHours(10).AddSeconds(1), token);
		fifth.BeginStep("s03", "refused-without-a-ticket");
		await OpenEntryWindowAsync(fifth, DisciplineGate, DisciplineMask, ArenaOfDiscipline, 402046, 903479, token);
		mark = fifth.PacketHistory.Count;
		await fifth.SendPacketAsync(GameClientPackets.AutoGroup(DisciplineMask, 100, 0), token);
		await fifth.SynchronizeAsync(token);
		AssertArenaRefusal(fifth, mark, "STR_MSG_INSTANCE_CANT_ENTER_WITHOUT_ITEM");
		Assert.False(AutoGroupService.GetInstance().IsSearching(fifthPlayer, DisciplineMask));

		first.BeginStep("s03", "register-for-discipline");
		await OpenEntryWindowAsync(first, DisciplineGate, DisciplineMask, ArenaOfDiscipline, 402046, 903479, token);
		await RegisterAloneAsync(first, firstPlayer, DisciplineMask, ArenaOfDiscipline, token);
		mark = first.PacketHistory.Count;
		await first.SendPacketAsync(GameClientPackets.AutoGroup(DisciplineMask, 100, 0), token);
		await first.SynchronizeAsync(token);
		AssertArenaRefusal(first, mark, "STR_MSG_CANT_INSTANCE_ALREADY_REGISTERED", ArenaOfDiscipline.ToString());
		Assert.True(AutoGroupService.GetInstance().IsSearching(firstPlayer, DisciplineMask));

		// s04: the second registers; AutoPvPFFAInstance is READY with two (instance_cooltimes' two members of 300360000), so
		// AutoGroupService.createNewInstance makes the arena and sends both the entry window.
		second.BeginStep("s04", "register-and-match");
		int[] marks = Marks(sessions);
		HashSet<int> before = ArenaInstanceIds(ArenaOfDiscipline);
		await OpenEntryWindowAsync(second, DisciplineGate, DisciplineMask, ArenaOfDiscipline, 402046, 903479, token);
		await RegisterAloneAsync(second, secondPlayer, DisciplineMask, ArenaOfDiscipline, token);
		await SyncSiegeSessionsAsync(sessions, token);
		DateTimeOffset matchCreated = SystemClock.UtcNow();
		WorldMapInstance match = Assert.Single(ArenaInstances(ArenaOfDiscipline), instance => !before.Contains(instance.GetInstanceId()));
		foreach ((SimulationL0Session session, int index) in new[] { (first, 0), (second, 1) })
		{
			ArenaWindow enter = Assert.Single(AutoGroupWindows(session, marks[index]), window => window.Window == ArenaWindowEnter);
			Assert.Equal(new ArenaWindow(DisciplineMask, ArenaWindowEnter, ArenaOfDiscipline, 0, 0, 0, ""), enter);
		}
		Assert.False(AutoGroupService.GetInstance().IsSearching(firstPlayer, DisciplineMask));
		Assert.False(AutoGroupService.GetInstance().IsSearching(secondPlayer, DisciplineMask));
		ArenaOfDisciplineInstance matchHandler = Assert.IsType<ArenaOfDisciplineInstance>(match.GetInstanceHandler());
		PvPArenaScore matchScore = Assert.IsType<PvPArenaScore>(matchHandler.GetInstanceScore());
		Assert.Equal(2, match.GetMaxPlayers());
		Assert.True(matchScore.IsPreparing());
		Assert.Equal((1, 1, 50000, 10000), (matchScore.GetDifficultyId(), matchScore.GetRound(), matchScore.GetUpperScoreCap(), matchScore.GetLowerScoreCap()));
		Assert.Empty(matchScore.GetPlayerRewards());

		// s05: the third registers and cancels (window 101): AutoGroupService.cancelRegistration sends him the cancel window.
		third.BeginStep("s05", "register-and-cancel");
		await OpenEntryWindowAsync(third, DisciplineGate, DisciplineMask, ArenaOfDiscipline, 402046, 903479, token);
		await RegisterAloneAsync(third, thirdPlayer, DisciplineMask, ArenaOfDiscipline, token);
		mark = third.PacketHistory.Count;
		await third.SendPacketAsync(GameClientPackets.AutoGroup(DisciplineMask, 101), token);
		await third.SynchronizeAsync(token);
		Assert.Equal([new ArenaWindow(DisciplineMask, ArenaWindowCancel, ArenaOfDiscipline, 0, 0, 0, "")], AutoGroupWindows(third, mark));
		Assert.False(AutoGroupService.GetInstance().IsSearching(thirdPlayer, DisciplineMask));

		// s06: the third registers again and the fourth with him: a second arena; the fourth declines the entry (window 103),
		// the third enters alone.
		fourth.BeginStep("s06", "match-and-decline");
		third.BeginStep("s06", "match-and-enter-alone");
		before = ArenaInstanceIds(ArenaOfDiscipline);
		await RegisterAloneAsync(third, thirdPlayer, DisciplineMask, ArenaOfDiscipline, token);
		marks = Marks(sessions);
		await OpenEntryWindowAsync(fourth, DisciplineGate, DisciplineMask, ArenaOfDiscipline, 402046, 903479, token);
		await RegisterAloneAsync(fourth, fourthPlayer, DisciplineMask, ArenaOfDiscipline, token);
		await SyncSiegeSessionsAsync(sessions, token);
		DateTimeOffset loneCreated = SystemClock.UtcNow();
		WorldMapInstance lone = Assert.Single(ArenaInstances(ArenaOfDiscipline), instance => !before.Contains(instance.GetInstanceId()));
		Assert.Single(AutoGroupWindows(third, marks[2]), window => window.Window == ArenaWindowEnter);
		Assert.Single(AutoGroupWindows(fourth, marks[3]), window => window.Window == ArenaWindowEnter);
		mark = fourth.PacketHistory.Count;
		await fourth.SendPacketAsync(GameClientPackets.AutoGroup(DisciplineMask, 103), token);
		await fourth.SynchronizeAsync(token);
		Assert.Equal([new ArenaWindow(DisciplineMask, ArenaWindowCancel, ArenaOfDiscipline, 0, 0, 0, "")], AutoGroupWindows(fourth, mark));
		Assert.Equal(1, fourthPlayer.GetInventory().GetItemCountByItemId(DisciplineTicket));
		Assert.Equal(MarchutanPriory, fourthPlayer.GetWorldId());
		long loneEntered = await EnterArenaAsync(third, thirdPlayer, DisciplineMask, lone, token);
		PvPArenaScore loneScore = Assert.IsType<PvPArenaScore>(lone.GetInstanceHandler().GetInstanceScore());
		int loneZone = AssertArenaPosition(thirdPlayer, loneScore);
		AssertTimeBonus(loneScore.GetPlayerReward(third.CharacterId), 8100, loneEntered);
		await ClearArenaMonstersAsync([(third, thirdPlayer)], lone, token);

		// s07: the first two enter their arena; each pays a ticket and is ported to a free position of the arena's zone, and on
		// arrival (onEnterInstance) ported again.
		var entered = new Dictionary<int, long>();
		foreach ((SimulationL0Session session, Player player) in new[] { (first, firstPlayer), (second, secondPlayer) })
		{
			session.BeginStep("s07", "enter-the-arena");
			entered[player.GetObjectId()] = await EnterArenaAsync(session, player, DisciplineMask, match, token);
		}
		await SyncSiegeSessionsAsync(sessions, token);
		// PvPArenaInstance.onEnterInstance: pressEnter already registered the player's reward (portToPosition), so the arrival
		// takes the branch of a returning player and calls updateBonusTime with no logout time: the bonus loses the clock's
		// milliseconds (cut to an int) times bonus / 660000, as in Java (see the plan, P11-03).
		foreach (Player player in new[] { firstPlayer, secondPlayer })
			AssertTimeBonus(matchScore.GetPlayerReward(player.GetObjectId()), 8100, entered[player.GetObjectId()]);
		int zone = AssertArenaPosition(firstPlayer, matchScore);
		Assert.Equal(zone, AssertArenaPosition(secondPlayer, matchScore));
		Assert.NotEqual(matchScore.GetPlayerReward(first.CharacterId).GetPosition(), matchScore.GetPlayerReward(second.CharacterId).GetPosition());
		Assert.True(matchScore.IsPreparing());
		// staticdoor_templates.xml: a closed door at each of the sixteen starting points until the first round.
		int doors = 0;
		match.ForEachDoor(door => { doors++; Assert.False(door.IsOpen()); });
		Assert.Equal(16, doors);
		Assert.All(new[] { first, second }, session => Assert.Contains(session.PacketHistory, packet => packet.PacketType == typeof(SM_INSTANCE_SCORE)));
		ArenaScore entryScore = LastArenaScore(second);
		Assert.Equal((ArenaOfDiscipline, Preparing, 1, 50000, 3, 1), (entryScore.MapId, entryScore.Progression, entryScore.Round, entryScore.UpperCap, entryScore.Rounds, entryScore.RewardTable));
		AssertArenaRows(entryScore, matchScore, rewarded: false);
		await ClearArenaMonstersAsync([(first, firstPlayer), (second, secondPlayer)], match, token);

		await PlayDisciplineMatchAsync(first, firstPlayer, second, secondPlayer, third, thirdPlayer, match, matchScore, matchCreated, lone, loneScore, loneCreated, token);
		await PlayHarmonyAsync(players, token);
		await PlayDredgionQueueAsync(players, day, token);
		await PlayGloryAsync(players, day, token);
		policy.AssertClean();
	}

	/// <summary>
	/// The Arena of Discipline from the start of its first round to its end and the players' exit, with the lone third's arena,
	/// which cannot start (PvPArenaInstance.canStart: one player inside) and ends as an interrupted one.
	/// </summary>
	private async Task PlayDisciplineMatchAsync(SimulationL0Session first, Player firstPlayer, SimulationL0Session second, Player secondPlayer,
		SimulationL0Session third, Player thirdPlayer, WorldMapInstance match, PvPArenaScore matchScore, DateTimeOffset matchCreated,
		WorldMapInstance lone, PvPArenaScore loneScore, DateTimeOffset loneCreated, CancellationToken token)
	{
		SimulationL0Session[] sessions = arenaCast;
		// s08, creation + 120 s: START_DELAY ends; both are inside, so the doors open and the first round starts.
		first.BeginStep("s08", "first-round-starts");
		await AdvanceSiegeWorldToAsync(sessions, matchCreated.AddSeconds(120.5), token);
		await SyncSiegeSessionsAsync(sessions, token);
		Assert.True(matchScore.IsStartProgress());
		int openDoors = 0;
		match.ForEachDoor(door => openDoors += door.IsOpen() ? 1 : 0);
		Assert.Equal(16, openDoors);
		ArenaScore started = LastArenaScore(first);
		// PvPArenaScore.getTime: the round's 180 s from the end of the 120 s START_DELAY.
		Assert.Equal((StartProgress, 180000, 1), (started.Progression, started.Time, started.Round));

		// The lone arena: started 120 s after its creation, with one player inside it ends at once (isInterrupted).
		await AdvanceSiegeWorldToAsync(sessions, loneCreated.AddSeconds(120.5), token);
		await SyncSiegeSessionsAsync(sessions, token);
		Assert.True(loneScore.IsRewarded());

		// s09: the first kills the second with real auto-attacks.
		first.BeginStep("s09", "first-kills-second");
		int[] fightMark = Marks([first, second]);
		await FightInArenaAsync(first, firstPlayer, second, secondPlayer, match, token);
		await SyncSiegeSessionsAsync(sessions, token);
		Assert.True(secondPlayer.IsDead());
		Assert.Equal(1, matchScore.GetPlayerReward(first.CharacterId).GetPvPKills());
		Assert.Equal(13000 + 200, matchScore.GetPlayerReward(first.CharacterId).GetPoints());
		Assert.Equal(13000 - 100, matchScore.GetPlayerReward(second.CharacterId).GetPoints());
		Assert.Single(first.PacketHistory.Skip(fightMark[0]), packet => IsSystemMessage(packet, "STR_MSG_GET_SCORE_FOR_ENEMY") &&
			packet.Get<string[]>("params").SequenceEqual(["200"]));
		Assert.DoesNotContain(second.PacketHistory.Skip(fightMark[1]), packet => IsSystemMessage(packet, "STR_MSG_GET_SCORE_FOR_ENEMY"));
		// applyMoraleBoost: the victim's boost of morale (instance_bonusattr 8) for his rank's duration (14 s first, 16 s second).
		PvPArenaPlayerReward secondReward = matchScore.GetPlayerReward(second.CharacterId);
		Assert.True(secondReward.HasBoostMorale());
		Assert.InRange(secondReward.GetRemainingTime(), 1, matchScore.GetRank(secondReward) == 0 ? 14000 : 16000);
		AssertArenaRows(LastArenaScore(first), matchScore, rewarded: false);
		int firstZone = await ReviveInArenaAsync(second, secondPlayer, matchScore, token);

		// s10, creation + 300 s: the second round, in another zone (setRndZone takes a zone out of the list): the dead revived and
		// everyone ported a second later (changeZone).
		foreach (SimulationL0Session session in new[] { first, second }) session.BeginStep("s10", "second-round");
		await AdvanceSiegeWorldToAsync(sessions, matchCreated.AddSeconds(301.5), token);
		await SyncSiegeSessionsAsync(sessions, token);
		foreach (SimulationL0Session session in new[] { first, second }) session.AcceptTeleportPosition();
		Assert.Equal(2, matchScore.GetRound());
		int secondZone = AssertArenaPosition(firstPlayer, matchScore);
		Assert.Equal(secondZone, AssertArenaPosition(secondPlayer, matchScore));
		Assert.NotEqual(firstZone, secondZone);
		Assert.Equal(2, LastArenaScore(first).Round);

		// s11, creation + 480 s: the final round, with STR_MSG_INSTANCE_START_SCOREMOD and a third zone.
		int[] roundMarks = Marks(sessions);
		await AdvanceSiegeWorldToAsync(sessions, matchCreated.AddSeconds(481.5), token);
		await SyncSiegeSessionsAsync(sessions, token);
		foreach (SimulationL0Session session in new[] { first, second }) session.AcceptTeleportPosition();
		Assert.Equal(3, matchScore.GetRound());
		int thirdZone = AssertArenaPosition(firstPlayer, matchScore);
		Assert.Equal(thirdZone, AssertArenaPosition(secondPlayer, matchScore));
		Assert.DoesNotContain(thirdZone, new[] { firstZone, secondZone });
		Assert.Single(first.PacketHistory.Skip(roundMarks[0]), packet => IsSystemMessage(packet, "STR_MSG_INSTANCE_START_SCOREMOD"));
		Assert.Single(second.PacketHistory.Skip(roundMarks[1]), packet => IsSystemMessage(packet, "STR_MSG_INSTANCE_START_SCOREMOD"));

		// s12: the second kills the first in the final round. getRunnerUpScoreMod: a victim ranked above his killer is worth three
		// times the kill.
		second.BeginStep("s12", "second-kills-first-in-the-final-round");
		PvPArenaPlayerReward firstReward = matchScore.GetPlayerReward(first.CharacterId);
		float mod = matchScore.GetRank(firstReward) < matchScore.GetRank(secondReward) ? 3f : 1f;
		int killPoints = JavaMath.Round(200 * mod);
		int firstPoints = firstReward.GetPoints(), secondPoints = secondReward.GetPoints();
		fightMark = Marks([first, second]);
		await FightInArenaAsync(second, secondPlayer, first, firstPlayer, match, token);
		await SyncSiegeSessionsAsync(sessions, token);
		Assert.Equal(secondPoints + killPoints, secondReward.GetPoints());
		Assert.Equal(firstPoints - 100, firstReward.GetPoints());
		Assert.Equal(1, secondReward.GetPvPKills());
		Assert.Single(second.PacketHistory.Skip(fightMark[1]), packet => IsSystemMessage(packet, "STR_MSG_GET_SCORE_FOR_ENEMY") &&
			packet.Get<string[]>("params").SequenceEqual([killPoints.ToString()]));
		await ReviveInArenaAsync(first, firstPlayer, matchScore, token);

		// s13, creation + 660 s: the third round's end ends the arena (isFinalRound): END_PROGRESS, the rewards by rank and score,
		// each player's reward table, the AP and insignia.
		foreach (SimulationL0Session session in new[] { first, second }) session.BeginStep("s13", "rewards");
		int[] endMarks = Marks(sessions);
		var apBefore = new[] { firstPlayer, secondPlayer }.ToDictionary(player => player.GetObjectId(), player => player.GetAbyssRank().GetAp());
		var crucibleBefore = new[] { firstPlayer, secondPlayer }.ToDictionary(player => player.GetObjectId(), player => player.GetInventory().GetItemCountByItemId(CrucibleInsignia));
		var courageBefore = new[] { firstPlayer, secondPlayer }.ToDictionary(player => player.GetObjectId(), player => player.GetInventory().GetItemCountByItemId(CourageInsignia));
		await AdvanceSiegeWorldToAsync(sessions, matchCreated.AddSeconds(660.5), token);
		await SyncSiegeSessionsAsync(sessions, token);
		Assert.True(matchScore.IsRewarded());
		Assert.DoesNotContain(match.GetNpcs(), npc => npc.IsSpawned());
		int total = matchScore.GetTotalPoints();
		foreach ((SimulationL0Session session, Player player, int index) in new[] { (first, firstPlayer, 0), (second, secondPlayer, 1) })
		{
			PvPArenaPlayerReward reward = matchScore.GetPlayerReward(player.GetObjectId());
			int rank = matchScore.GetRank(reward);
			ExpectedArenaReward expected = ExpectedDisciplineReward(reward.GetScorePoints(), total, playerCount: 2, rank);
			AssertArenaReward(reward, expected);
			Assert.True(reward.IsRewarded());
			Assert.Equal(apBefore[player.GetObjectId()] + expected.Ap.GetTotalCount(), player.GetAbyssRank().GetAp());
			Assert.Equal(crucibleBefore[player.GetObjectId()] + expected.Crucible.GetTotalCount(), player.GetInventory().GetItemCountByItemId(CrucibleInsignia));
			Assert.Equal(courageBefore[player.GetObjectId()] + expected.Courage.GetTotalCount(), player.GetInventory().GetItemCountByItemId(CourageInsignia));
			// broadcastResults: each player's table with his own rewards, the rows with score points and time bonus.
			ArenaScore result = LastArenaScore(session);
			Assert.Equal((EndProgress, 0, 3, 1), (result.Progression, result.Time, result.Round, result.RewardTable));
			// DisciplineTrainingGroundsInstance.sendPacket sends to every player whoever it is called for, so each gets one table
			// for each player inside.
			Assert.Equal(2, session.PacketHistory.Skip(endMarks[index]).Count(packet => packet.PacketType == typeof(SM_INSTANCE_SCORE)
				&& ReadArenaScore(packet).Progression == EndProgress));
			AssertArenaRows(result, matchScore, rewarded: true);
			Assert.Equal(ArenaRewardBlock(expected, null, null), result.Rewards);
			Assert.Single(session.PacketHistory.Skip(endMarks[index]), packet => IsSystemMessage(packet, "STR_MSG_COMBAT_MY_ABYSS_POINT_GAIN") &&
				packet.Get<string[]>("params").SequenceEqual([expected.Ap.GetTotalCount().ToString()]));
		}

		// s14, end + 60 s: both are sent to the instance exit (instance_exit.xml: Marchutan Priory), and the arena is destroyed
		// when the last one leaves (AutoGroupService.destroyIfPossible).
		foreach (SimulationL0Session session in new[] { first, second }) session.BeginStep("s14", "leave-the-arena");
		await AdvanceSiegeWorldToAsync(sessions, matchCreated.AddSeconds(720.5), token);
		await SyncSiegeSessionsAsync(sessions, token);
		foreach ((SimulationL0Session session, Player player) in new[] { (first, firstPlayer), (second, secondPlayer) })
			AssertAtArenaExit(session, player);
		Assert.DoesNotContain(ArenaInstances(ArenaOfDiscipline), instance => instance.GetInstanceId() == match.GetInstanceId());
		Assert.False(AutoGroupService.GetInstance().IsInAutoInstance(firstPlayer));

		// The lone arena: its interrupted end rewarded the third as the runner-up of a full arena (rank getMaxPlayers() - 1), and a
		// minute later sent him out and was destroyed.
		third.BeginStep("s14", "lone-arena-ended");
		Assert.Equal(MarchutanPriory, thirdPlayer.GetWorldId());
		AssertAtArenaExit(third, thirdPlayer);
		Assert.DoesNotContain(ArenaInstances(ArenaOfDiscipline), instance => instance.GetInstanceId() == lone.GetInstanceId());
		ArenaScore loneResult = ReadArenaScore(third.PacketHistory.Last(packet => packet.PacketType == typeof(SM_INSTANCE_SCORE)
			&& ReadArenaScore(packet).Progression == EndProgress));
		ArenaRow loneRow = Assert.Single(loneResult.Rows);
		ExpectedArenaReward loneExpected = ExpectedDisciplineReward(loneRow.Points, loneRow.Points, playerCount: 1, rank: 1);
		Assert.Equal(ArenaRewardBlock(loneExpected, null, null), loneResult.Rewards);
		Assert.Equal(loneExpected.Ap.GetTotalCount(), thirdPlayer.GetAbyssRank().GetAp());
		Assert.Equal(loneExpected.Crucible.GetTotalCount(), thirdPlayer.GetInventory().GetItemCountByItemId(CrucibleInsignia));
		Assert.Equal(loneExpected.Courage.GetTotalCount(), thirdPlayer.GetInventory().GetItemCountByItemId(CourageInsignia));
	}

	/// <summary>
	/// The Arena of Harmony (300450000) at difficulty 1: two groups of three register as groups (GROUP_ENTRY), AutoHarmonyInstance
	/// puts each in a harmony group of its own, and inside the arena each harmony group becomes an AUTO_GROUP team whose
	/// HarmonyGroupReward holds the score; a kill credited to the team, the three rounds, the rewards by group rank split among
	/// the members (splitGroupRewards) and the exit.
	/// </summary>
	private async Task PlayHarmonyAsync(Player[] players, CancellationToken token)
	{
		SimulationL0Session[] sessions = arenaCast;
		(SimulationL0Session first, SimulationL0Session second, SimulationL0Session third, SimulationL0Session fourth, SimulationL0Session fifth,
			SimulationL0Session sixth) = (sessions[0], sessions[1], sessions[2], sessions[3], sessions[4], sessions[5]);
		Player firstPlayer = players[0], fourthPlayer = players[3], sixthPlayer = players[5];

		// s15: everyone beside the Harmony gate; two groups by their own invitations (the first leads the first three, the fourth
		// the other three); a Harmony ticket for the first five.
		for (int i = 0; i < sessions.Length; i++)
		{
			sessions[i].BeginStep("s15", "groups-for-harmony");
			await TeleportBesideGateAsync(sessions[i], players[i], HarmonyGate, i, token);
		}
		await MakeGroupAsync(first, firstPlayer, [(second, players[1]), (third, players[2])], token);
		await MakeGroupAsync(fourth, fourthPlayer, [(fifth, players[4]), (sixth, sixthPlayer)], token);
		foreach (Player player in players.Take(5))
			Assert.Equal(0, ItemService.AddItem(player, HarmonyTicket, 1));
		await SyncSiegeSessionsAsync(sessions, token);

		// s16: a member cannot register his group (STR_MSG_CANT_INSTANCE_NOT_LEADER); the fourth's group is refused because the
		// sixth has no ticket (AutoGroupUtility.checkGroupRequirements: the member hears why, the leader whom).
		fifth.BeginStep("s16", "member-cannot-register");
		await OpenEntryWindowAsync(fifth, HarmonyGate, HarmonyMask, ArenaOfHarmony, 403505, 904400, token);
		int mark = fifth.PacketHistory.Count;
		await fifth.SendPacketAsync(GameClientPackets.AutoGroup(HarmonyMask, 100, 2), token);
		await fifth.SynchronizeAsync(token);
		AssertArenaRefusal(fifth, mark, "STR_MSG_CANT_INSTANCE_NOT_LEADER");
		fourth.BeginStep("s16", "member-without-a-ticket");
		await OpenEntryWindowAsync(fourth, HarmonyGate, HarmonyMask, ArenaOfHarmony, 403505, 904400, token);
		int[] marks = Marks(sessions);
		await fourth.SendPacketAsync(GameClientPackets.AutoGroup(HarmonyMask, 100, 2), token);
		await SyncSiegeSessionsAsync(sessions, token);
		AssertArenaRefusal(fourth, marks[3], "STR_MSG_CANT_INSTANCE_ENTER_MEMBER", sixthPlayer.GetName());
		AssertArenaRefusal(sixth, marks[5], "STR_MSG_INSTANCE_CANT_ENTER_WITHOUT_ITEM");
		Assert.DoesNotContain(fifth.PacketHistory.Skip(marks[4]), packet => packet.PacketType == typeof(SM_SYSTEM_MESSAGE));
		Assert.All(players.Skip(3), player => Assert.False(AutoGroupService.GetInstance().IsSearching(player, HarmonyMask)));

		// s17: the first registers his group: each of the three gets the success and the waiting window with request type 2 and
		// the leader's name.
		first.BeginStep("s17", "register-the-first-group");
		await OpenEntryWindowAsync(first, HarmonyGate, HarmonyMask, ArenaOfHarmony, 403505, 904400, token);
		marks = Marks(sessions);
		await first.SendPacketAsync(GameClientPackets.AutoGroup(HarmonyMask, 100, 2), token);
		await SyncSiegeSessionsAsync(sessions, token);
		for (int i = 0; i < 3; i++)
		{
			Assert.Single(sessions[i].PacketHistory.Skip(marks[i]), packet => IsSystemMessage(packet, "STR_MSG_INSTANCE_REGISTER_SUCCESS"));
			Assert.Equal([new ArenaWindow(HarmonyMask, ArenaWindowWaiting, ArenaOfHarmony, 0, 0, 2, firstPlayer.GetName())], AutoGroupWindows(sessions[i], marks[i]));
			Assert.True(AutoGroupService.GetInstance().IsSearching(players[i], HarmonyMask));
		}

		// s18: the sixth gets his ticket and the fourth registers again: the second harmony group fills the arena (six,
		// instance_cooltimes' members of 300450000), and all six get the entry window.
		fourth.BeginStep("s18", "register-the-second-group");
		Assert.Equal(0, ItemService.AddItem(sixthPlayer, HarmonyTicket, 1));
		HashSet<int> before = ArenaInstanceIds(ArenaOfHarmony);
		marks = Marks(sessions);
		await fourth.SendPacketAsync(GameClientPackets.AutoGroup(HarmonyMask, 100, 2), token);
		await SyncSiegeSessionsAsync(sessions, token);
		DateTimeOffset created = SystemClock.UtcNow();
		WorldMapInstance arena = Assert.Single(ArenaInstances(ArenaOfHarmony), instance => !before.Contains(instance.GetInstanceId()));
		for (int i = 0; i < 6; i++)
		{
			ArenaWindow[] windows = AutoGroupWindows(sessions[i], marks[i]);
			Assert.Equal(new ArenaWindow(HarmonyMask, ArenaWindowEnter, ArenaOfHarmony, 0, 0, 0, ""), windows.Last());
			Assert.Equal(i < 3 ? 1 : 2, windows.Length); // the second group's waiting window comes first
			Assert.False(AutoGroupService.GetInstance().IsSearching(players[i], HarmonyMask));
		}
		ArenaOfHarmonyInstance handler = Assert.IsType<ArenaOfHarmonyInstance>(arena.GetInstanceHandler());
		HarmonyArenaScore score = Assert.IsType<HarmonyArenaScore>(handler.GetInstanceScore());
		Assert.Equal((6, 1, 50000, 7000), (arena.GetMaxPlayers(), score.GetDifficultyId(), score.GetUpperScoreCap(), score.GetLowerScoreCap()));

		// s19: all six enter, each leaving his group (AutoGroupService.pressEnter); the first of each harmony group to arrive makes
		// its AUTO_GROUP team and the group's reward (12000 bonus time, buff 7), the others join it.
		var entered = new Dictionary<int, long>();
		int[] entryMarks = Marks(sessions);
		for (int i = 0; i < 6; i++)
		{
			sessions[i].BeginStep("s19", "enter-the-arena");
			entered[players[i].GetObjectId()] = await EnterArenaAsync(sessions[i], players[i], HarmonyMask, arena, token);
		}
		await SyncSiegeSessionsAsync(sessions, token);
		Assert.Equal(2, score.GetGroups().Count);
		int zone = AssertArenaPosition(firstPlayer, score);
		for (int g = 0; g < 2; g++)
		{
			HarmonyGroupReward group = score.GetGroups()[g];
			Player[] members = players.Skip(3 * g).Take(3).ToArray();
			Player leader = members[0];
			Assert.Equal(g, group.GetOwnerId());
			Assert.Equal(members.Select(player => player.GetObjectId()), group.GetAssociatedPlayers().Select(agp => agp.ObjectId));
			Assert.Equal((13000, 12000), (group.GetPoints(), group.GetScorePoints() - group.GetPoints()));
			PlayerGroup team = Assert.IsType<PlayerGroup>(leader.GetPlayerGroup());
			Assert.Equal((TeamType.AUTO_GROUP, leader.GetObjectId(), 3), (team.GetTeamType(), team.GetLeaderObject().GetObjectId(), team.Size()));
			Assert.Equal(team.GetObjectId(), group.GetGrpObjectId());
			Assert.True(arena.IsRegistered(team.GetObjectId()));
			foreach (Player member in members)
			{
				Assert.Same(team, member.GetPlayerGroup());
				Assert.Same(group, score.GetGroupReward(member.GetObjectId()));
				Assert.Equal(zone, AssertArenaPosition(member, score));
				AssertTimeBonus(score.GetPlayerReward(member.GetObjectId()), 12000, entered[member.GetObjectId()]);
			}
		}
		(SimulationL0Session, Player)[] inside = sessions.Zip(players).ToArray();
		await ClearArenaMonstersAsync(inside, arena, token);

		// s20, creation + 120 s: both harmony groups are inside (HarmonyTrainingGroundsInstance.canStart), the first round starts.
		await AdvanceSiegeWorldToAsync(sessions, created.AddSeconds(120.5), token);
		await SyncSiegeSessionsAsync(sessions, token);
		Assert.True(score.IsStartProgress());

		// s21: the first kills the fourth. HarmonyTrainingGroundsInstance merges group damage, so the 800 points go to the reward
		// of the killer's team's leader (the first group's) and every member hears them; the fourth's group loses 150.
		first.BeginStep("s21", "first-kills-fourth");
		marks = Marks(sessions);
		await FightInArenaAsync(first, firstPlayer, fourth, fourthPlayer, arena, token);
		await SyncSiegeSessionsAsync(sessions, token);
		HarmonyGroupReward winners = score.GetGroups()[0], losers = score.GetGroups()[1];
		Assert.Equal((13800, 1), (winners.GetPoints(), winners.GetPvPKills()));
		Assert.True((12850, 0) == (losers.GetPoints(), losers.GetPvPKills()), $"losers {losers.GetPoints()}; deaths since the fight: " +
			string.Join(", ", sessions.Select((session, i) => session.PacketHistory.Skip(marks[i]).Count(packet => packet.PacketType == typeof(SM_DIE)))) +
			"; deaths since the entry: " + string.Join(", ", sessions.Select((session, i) => session.PacketHistory.Skip(entryMarks[i]).Count(packet => packet.PacketType == typeof(SM_DIE)))) +
			"; dead now: " + string.Join(", ", players.Select(player => player.IsDead() + "@" + player.GetX() + "/" + player.GetY() + "/" + player.GetZ())) +
			"; score messages: " + string.Join(" | ", sessions.Select((session, i) => string.Join(",", session.PacketHistory.Skip(marks[i])
				.Where(packet => packet.PacketType == typeof(SM_SYSTEM_MESSAGE) && (packet.Get<string?>("name") ?? "").Contains("SCORE"))
				.Select(packet => packet.Get<string?>("name") + ":" + string.Join("/", packet.Get<string[]>("params")))))));
		for (int i = 0; i < 6; i++)
		{
			int heard = sessions[i].PacketHistory.Skip(marks[i]).Count(packet => IsSystemMessage(packet, "STR_MSG_GET_SCORE_FOR_ENEMY")
				&& packet.Get<string[]>("params").SequenceEqual(["800"]));
			Assert.Equal(i < 3 ? 1 : 0, heard);
		}
		// applyMoraleBoost: the victim's own reward, ranked among the groups' score points (HarmonyArenaScore.getRank), 15 s.
		PvPArenaPlayerReward fourthReward = score.GetPlayerReward(fourthPlayer.GetObjectId());
		Assert.Equal(1, score.GetRank(fourthReward));
		Assert.InRange(fourthReward.GetRemainingTime(), 1, 15000);
		AssertHarmonyScoreBoard(first, score, arena);
		await ReviveInArenaAsync(fourth, fourthPlayer, score, token);
		await PlayRoundsAsync(inside, arena, score, created, token);

		// s22, creation + 660 s: the end; calculateRewards by group (rank 0 gets 90% of the rank share and a Lesser Arena Supply
		// Package), split among each group's three members (Math.round of a third).
		marks = Marks(sessions);
		var apBefore = players.ToDictionary(player => player.GetObjectId(), player => player.GetAbyssRank().GetAp());
		var courageBefore = players.ToDictionary(player => player.GetObjectId(), player => player.GetInventory().GetItemCountByItemId(CourageInsignia));
		foreach (SimulationL0Session session in sessions) session.BeginStep("s22", "harmony-rewards");
		await AdvanceSiegeWorldToAsync(sessions, created.AddSeconds(660.5), token);
		await SyncSiegeSessionsAsync(sessions, token);
		Assert.True(score.IsRewarded());
		int total = score.GetTotalPoints();
		Assert.Equal(winners.GetScorePoints() + losers.GetScorePoints(), total);
		for (int g = 0; g < 2; g++)
		{
			HarmonyGroupReward group = score.GetGroups()[g];
			int rank = score.GetRank(group);
			Assert.Equal(g, rank);
			ExpectedArenaReward expected = ExpectedArenaRewardOf((1200, 0, 0, 12), (200, 0, 0, 0), playerCount: 6, group.GetScorePoints(), total,
				rank == 0 ? 0.9f : 0.1f, 1f, new ArenaRewardItem(CourageInsignia, 0, 0, 0));
			AssertArenaReward(group, expected);
			RewardItem? item1 = rank == 0 ? new RewardItem(188052179, 1) : null;
			Assert.Equal(item1?.ToString(), group.GetRewardItem1()?.ToString());
			Assert.Null(group.GetRewardItem2());
			ExpectedArenaReward share = new(Third(expected.Ap), Third(expected.Gp), Third(expected.Crucible), Third(expected.Courage));
			for (int m = 0; m < 3; m++)
			{
				int index = 3 * g + m;
				Player member = players[index];
				PvPArenaPlayerReward reward = score.GetPlayerReward(member.GetObjectId());
				AssertArenaReward(reward, share);
				Assert.True(reward.IsRewarded());
				Assert.Equal(apBefore[member.GetObjectId()] + share.Ap.GetTotalCount(), member.GetAbyssRank().GetAp());
				Assert.Equal(courageBefore[member.GetObjectId()] + share.Courage.GetTotalCount(), member.GetInventory().GetItemCountByItemId(CourageInsignia));
				Assert.Equal(rank == 0 ? 1 : 0, member.GetInventory().GetItemCountByItemId(188052179));
				// HarmonyScoreWriter SHOW_REWARD, to each player alone: AP and GP (base, score, ranking), courage and crucible
				// insignia with their ids, the two items.
				DecodedBotServerPacket[] results = sessions[index].PacketHistory.Skip(marks[index])
					.Where(packet => packet.PacketType == typeof(SM_INSTANCE_SCORE) && HarmonyType(packet) == 5).ToArray();
				DecodedBotServerPacket result = Assert.Single(results);
				Assert.Equal(HarmonyRewardBlock(share, item1, null), HarmonyBody(result)[1..]);
				Assert.Equal(EndProgress, BinaryPrimitives.ReadInt32LittleEndian(Convert.FromHexString(result.Get<string>("bodyHex")).AsSpan(8)));
			}
		}

		// s23, end + 60 s: everyone at the exit, out of the AUTO_GROUP teams (AutoHarmonyInstance.onLeaveInstance), the arena gone.
		foreach (SimulationL0Session session in sessions) session.BeginStep("s23", "leave-harmony");
		await AdvanceSiegeWorldToAsync(sessions, created.AddSeconds(720.5), token);
		await SyncSiegeSessionsAsync(sessions, token);
		for (int i = 0; i < 6; i++)
		{
			AssertAtArenaExit(sessions[i], players[i]);
			Assert.False(players[i].IsInTeam());
		}
		Assert.DoesNotContain(ArenaInstances(ArenaOfHarmony), instance => instance.GetInstanceId() == arena.GetInstanceId());
	}

	/// <summary>PvPArenaInstance.calculateIndividualReward: each count times the member's rate (1.0) over the group's size, Math.round.</summary>
	private static ArenaRewardItem Third(ArenaRewardItem item) =>
		new(item.ItemId, JavaMath.Round(item.BaseCount * 1f / 3), JavaMath.Round(item.RankingCount * 1f / 3), JavaMath.Round(item.ScoreCount * 1f / 3));

	/// <summary>
	/// The Baranath Dredgion's periodic registration (PeriodicInstanceManager, autogroup.properties: 0, 12 and 20 o'clock, 60
	/// minutes): the noon opening's icon and message to the players of each dredgion's levels, the icon clicked, Rukbar's match
	/// maker dialog, registrations alone (quick and new) and as a group, a cancel and its penalty's icon, a member logging out
	/// and back in, and the 13:00 close sending every queued player the cancel window. Six Asmodians cannot fill a dredgion
	/// (six of each race), so no dredgion starts.
	/// </summary>
	private async Task PlayDredgionQueueAsync(Player[] players, DateTimeOffset day, CancellationToken token)
	{
		SimulationL0Session[] sessions = arenaCast;
		(SimulationL0Session first, SimulationL0Session second, SimulationL0Session third, SimulationL0Session fourth, SimulationL0Session fifth,
			SimulationL0Session sixth) = (sessions[0], sessions[1], sessions[2], sessions[3], sessions[4], sessions[5]);
		Player firstPlayer = players[0], secondPlayer = players[1], thirdPlayer = players[2], fifthPlayer = players[4], sixthPlayer = players[5];
		const int BaranathMask = 1, ChantraMask = 2, BaranathDredgion = 300110000, ChantraDredgion = 300210000, Rukbar = 279055;

		// s24, 11:58: the sixth is made level 51 (Chantra Dredgion takes 51 to 55); the second goes to Rukbar in Reshanta.
		sixth.BeginStep("s24", "before-noon");
		await AdvanceSiegeWorldToAsync(sessions, day.AddHours(11).AddMinutes(58), token);
		sixthPlayer.GetCommonData().SetLevel(51);
		second.BeginStep("s24", "go-to-rukbar");
		await TeleportForSetupAsync(second, secondPlayer, Reshanta, 1073.5f, 2955.12f, Ground(Reshanta, 1073.5f, 2955.12f, 1660, 1620), token);
		await SyncSiegeSessionsAsync(sessions, token);
		Assert.False(PeriodicInstanceManager.GetInstance().IsRegistrationOpen(BaranathMask));

		// s25, 12:00: the three dredgions' registrations open; each player gets the icon (window 6, open) and the opening message
		// of the dredgion his level takes, none of the others.
		int[] marks = Marks(sessions);
		int logMark = fixture.LogCapture.Entries.Count;
		foreach (SimulationL0Session session in sessions) session.BeginStep("s25", "registration-opens");
		await AdvanceSiegeWorldToAsync(sessions, day.AddHours(12).AddSeconds(1), token);
		await SyncSiegeSessionsAsync(sessions, token);
		Assert.True(PeriodicInstanceManager.GetInstance().IsRegistrationOpen(BaranathMask));
		Assert.True(PeriodicInstanceManager.GetInstance().IsRegistrationOpen(ChantraMask));
		Assert.True(PeriodicInstanceManager.GetInstance().IsRegistrationOpen(3));
		string[] log = fixture.LogCapture.Entries.Skip(logMark).Select(entry => entry.Message).ToArray();
		foreach (string type in new[] { "BARANATH_DREDGION", "CHANTRA_DREDGION", "TERATH_DREDGION" })
			Assert.Single(log, line => line == $"Scheduled registration closing for {type} in 60 minutes");
		for (int i = 0; i < 6; i++)
		{
			(int mask, int mapId, int name, int title, string message) = i == 5 ? (ChantraMask, ChantraDredgion, 401675, 401677, "STR_MSG_INSTANCE_OPEN_IDDREADGION_02")
				: (BaranathMask, BaranathDredgion, 401193, 401197, "STR_MSG_INSTANCE_OPEN_IDAB1_DREADGION");
			Assert.Equal([new ArenaWindow(mask, ArenaWindowIcon, mapId, name, title, 1, "")], AutoGroupWindows(sessions[i], marks[i]));
			Assert.Single(sessions[i].PacketHistory.Skip(marks[i]), packet => IsSystemMessage(packet, message));
			Assert.DoesNotContain(sessions[i].PacketHistory.Skip(marks[i]), packet => IsSystemMessage(packet, i == 5 ? "STR_MSG_INSTANCE_OPEN_IDAB1_DREADGION" : "STR_MSG_INSTANCE_OPEN_IDDREADGION_02"));
		}

		// s26: the first clicks the icon (window 104: PeriodicInstanceManager.handleRequest), the second asks Rukbar (MATCH_MAKER):
		// both get the entry request.
		first.BeginStep("s26", "click-the-icon");
		int mark = first.PacketHistory.Count;
		await first.SendPacketAsync(GameClientPackets.AutoGroup(BaranathMask, 104), token);
		await first.SynchronizeAsync(token);
		Assert.Equal([new ArenaWindow(BaranathMask, ArenaWindowRequest, BaranathDredgion, 401193, 401197, 0, "")], AutoGroupWindows(first, mark));
		second.BeginStep("s26", "ask-rukbar");
		int rukbar = await second.WaitForNpcAsync(Rukbar, token);
		mark = second.PacketHistory.Count;
		await second.SendPacketAsync(second.Api.TalkTo(rukbar), token);
		await second.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == rukbar);
		await second.SendPacketAsync(second.Api.SelectDialog(rukbar, 63), token); // DialogAction.MATCH_MAKER
		await second.SynchronizeAsync(token);
		Assert.Equal([new ArenaWindow(BaranathMask, ArenaWindowRequest, BaranathDredgion, 401193, 401197, 0, "")], AutoGroupWindows(second, mark));

		// s27: the first registers for a quick entry, the second as a new group, the third leads his group of three: for a
		// periodic instance sendSuccessfulRegistration first closes each player's icon.
		await RegisterForDredgionAsync(first, firstPlayer, [first], BaranathMask, 1, token);
		await RegisterForDredgionAsync(second, secondPlayer, [second], BaranathMask, 0, token);
		third.BeginStep("s27", "group-for-the-dredgion");
		await MakeGroupAsync(third, thirdPlayer, [(fourth, players[3]), (fifth, fifthPlayer)], token);
		await RegisterForDredgionAsync(third, thirdPlayer, [third, fourth, fifth], BaranathMask, 2, token);
		Assert.All(players.Take(5), player => Assert.True(AutoGroupService.GetInstance().IsSearching(player, BaranathMask)));
		Assert.False(AutoGroupService.GetInstance().IsSearching(sixthPlayer, ChantraMask));

		// s28: the second cancels; ten seconds later his penalty ends and the open registration's icon comes back.
		second.BeginStep("s28", "cancel-and-penalty");
		mark = second.PacketHistory.Count;
		await second.SendPacketAsync(GameClientPackets.AutoGroup(BaranathMask, 101), token);
		await second.SynchronizeAsync(token);
		Assert.Equal([new ArenaWindow(BaranathMask, ArenaWindowCancel, BaranathDredgion, 0, 0, 0, "")], AutoGroupWindows(second, mark));
		Assert.False(AutoGroupService.GetInstance().IsSearching(secondPlayer, BaranathMask));
		await AdvanceSiegeWorldToAsync(sessions, SystemClock.UtcNow().AddSeconds(9), token);
		await SyncSiegeSessionsAsync(sessions, token);
		Assert.Equal([new ArenaWindow(BaranathMask, ArenaWindowCancel, BaranathDredgion, 0, 0, 0, "")], AutoGroupWindows(second, mark));
		await AdvanceSiegeWorldToAsync(sessions, SystemClock.UtcNow().AddSeconds(1.5), token);
		await SyncSiegeSessionsAsync(sessions, token);
		Assert.Equal(new ArenaWindow(BaranathMask, ArenaWindowIcon, BaranathDredgion, 401193, 401197, 1, ""), AutoGroupWindows(second, mark).Last());

		// s29: the fifth, a member of the third's queued group, logs out (AutoGroupService.onLogout takes him out of it) and back
		// in, where the open registration's icon is sent at login.
		fifth.BeginStep("s29", "member-relogs");
		int fifthId = fifth.CharacterId;
		await fifth.QuitAsync(token);
		Assert.False(AutoGroupService.GetInstance().IsSearching(fifthPlayer, BaranathMask));
		Assert.True(AutoGroupService.GetInstance().IsSearching(thirdPlayer, BaranathMask));
		// The client waits ten seconds after a quit before it enters the world again (BotTimingContract).
		await AdvanceSiegeWorldToAsync(sessions.Where(session => session != fifth).ToArray(), SystemClock.UtcNow().AddSeconds(11), token);
		await fifth.LoginCharacterListAsync(token);
		fifth.SelectCharacter(fifthId, "Asimarenae");
		mark = fifth.PacketHistory.Count;
		await fifth.EnterWorldAsync(token);
		await fifth.SynchronizeAsync(token);
		fifthPlayer = fixture.World.GetPlayer(fifthId);
		Assert.Contains(new ArenaWindow(BaranathMask, ArenaWindowIcon, BaranathDredgion, 401193, 401197, 1, ""), AutoGroupWindows(fifth, mark));
		Assert.False(AutoGroupService.GetInstance().IsSearching(fifthPlayer, BaranathMask));

		// s30, 13:00: the registrations close: every player of each dredgion's levels gets its icon closed, and the queued ones
		// (the first, the third and the fourth) the cancel window (stopRegistrationsByMaskId).
		marks = Marks(sessions);
		foreach (SimulationL0Session session in sessions) session.BeginStep("s30", "registration-closes");
		await AdvanceSiegeWorldToAsync(sessions, day.AddHours(13).AddSeconds(1), token);
		await SyncSiegeSessionsAsync(sessions, token);
		Assert.False(PeriodicInstanceManager.GetInstance().IsRegistrationOpen(BaranathMask));
		for (int i = 0; i < 6; i++)
		{
			(int mask, int mapId, int name, int title) = i == 5 ? (ChantraMask, ChantraDredgion, 401675, 401677) : (BaranathMask, BaranathDredgion, 401193, 401197);
			ArenaWindow[] windows = AutoGroupWindows(sessions[i], marks[i]);
			bool queued = i is 0 or 2 or 3;
			ArenaWindow[] expected = queued
				? [new ArenaWindow(mask, ArenaWindowIcon, mapId, name, title, 0, ""), new ArenaWindow(mask, ArenaWindowCancel, mapId, 0, 0, 0, "")]
				: [new ArenaWindow(mask, ArenaWindowIcon, mapId, name, title, 0, "")];
			Assert.Equal(expected, windows);
			Assert.False(AutoGroupService.GetInstance().IsSearching(fixture.World.GetPlayer(sessions[i].CharacterId), mask));
		}
	}

	/// <summary>
	/// A registration for a periodic instance: for each member the icon closed (window 6), the success message and the waiting
	/// window with the request type and the registering leader's name.
	/// </summary>
	private async Task RegisterForDredgionAsync(SimulationL0Session leader, Player leaderPlayer, SimulationL0Session[] members, int mask, byte entryRequest,
		CancellationToken token)
	{
		leader.BeginStep("s27", "register-for-the-dredgion");
		int[] marks = Marks(members);
		await leader.SendPacketAsync(GameClientPackets.AutoGroup(mask, 100, entryRequest), token);
		await SyncSiegeSessionsAsync(members, token);
		for (int i = 0; i < members.Length; i++)
		{
			Assert.Equal([new ArenaWindow(mask, ArenaWindowIcon, 300110000, 401193, 401197, 0, ""), new ArenaWindow(mask, ArenaWindowWaiting, 300110000, 0, 0, entryRequest, leaderPlayer.GetName())],
				AutoGroupWindows(members[i], marks[i]));
			Assert.Single(members[i].PacketHistory.Skip(marks[i]), packet => IsSystemMessage(packet, "STR_MSG_INSTANCE_REGISTER_SUCCESS"));
		}
	}

	/// <summary>A group made by the leader's invitations and each member's answer (question 60000), as SIEGE makes its group.</summary>
	private static async Task MakeGroupAsync(SimulationL0Session leader, Player leaderPlayer, (SimulationL0Session Session, Player Player)[] members, CancellationToken token)
	{
		foreach ((SimulationL0Session member, Player memberPlayer) in members)
		{
			await leader.SendPacketAsync(leader.Api.InviteToGroup(memberPlayer.GetName()), token);
			await member.DrainServerPacketsAsync(token);
			await member.WaitForPacketAsync(typeof(SM_QUESTION_WINDOW), token, packet => packet.Get<int>("code") == 60000);
			await member.SendPacketAsync(member.Api.Answer(1), token);
			await leader.DrainServerPacketsAsync(token);
			await member.WaitForPacketAsync(typeof(SM_GROUP_INFO), token, packet => packet.Get<int>("leaderId") == leader.CharacterId);
			await leader.SynchronizeAsync(token);
			Assert.Same(leaderPlayer.GetCurrentTeam(), memberPlayer.GetCurrentTeam());
		}
		Assert.Equal(members.Length + 1, leaderPlayer.GetPlayerGroup().Size());
	}

	/// <summary>
	/// HarmonyScoreWriter UPDATE_INSTANCE_BUFFS_AND_SCORE (6), the last one the session got: the caps, 3 rounds, the round, and
	/// for each harmony group inside its rank, kills, points, team and members (compared by object id: the port orders the
	/// players inside its own way, see the plan).
	/// </summary>
	private static void AssertHarmonyScoreBoard(SimulationL0Session session, HarmonyArenaScore score, WorldMapInstance arena)
	{
		DecodedBotServerPacket packet = session.PacketHistory.Last(candidate => candidate.PacketType == typeof(SM_INSTANCE_SCORE) && HarmonyType(candidate) == 6);
		var body = new ArenaBodyReader(HarmonyBody(packet));
		Assert.Equal(6, body.C());
		Assert.Equal((7000, 50000, 3, 1, 0, 0, 0, score.GetRound()), (body.D(), body.D(), body.D(), body.D(), body.D(), body.D(), body.D(), body.D()));
		int groups = body.C();
		Assert.Equal(score.GetHarmonyGroupInside().Count, groups);
		for (int g = 0; g < groups; g++)
		{
			int rank = body.C(), kills = body.D(), points = body.D(), team = body.D();
			HarmonyGroupReward group = Assert.Single(score.GetGroups(), candidate => candidate.GetGrpObjectId() == team);
			Assert.Equal((score.GetRank(group), group.GetPvPKills(), group.GetPoints()), (rank, kills, points));
			int members = body.C();
			var seen = new List<int>();
			for (int m = 0; m < members; m++)
			{
				Assert.Equal(0, body.D());
				body.D(); // the member's boost of morale's remaining time when the packet was written
				Assert.Equal(0, body.D());
				Assert.Equal(g, body.C());
				Assert.Equal(m, body.C());
				Assert.Equal(0, body.H());
				string name = body.FixedS(52);
				int objectId = body.D();
				Assert.Equal(arena.GetPlayersInside().Single(player => player.GetObjectId() == objectId).GetName(), name);
				seen.Add(objectId);
			}
			Assert.Equal(score.GetPlayersInside(group).Select(player => player.GetObjectId()).Order(), seen.Order());
		}
		body.End();
	}

	/// <summary>HarmonyScoreWriter SHOW_REWARD after its type byte: AP and GP (base, score, ranking), courage and crucible insignia (id, base, score, ranking), item 1 and item 2.</summary>
	private static byte[] HarmonyRewardBlock(ExpectedArenaReward expected, RewardItem? item1, RewardItem? item2)
	{
		var block = new List<byte>();
		void D(int value) { var bytes = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(bytes, value); block.AddRange(bytes); }
		foreach (ArenaRewardItem item in new[] { expected.Ap, expected.Gp })
		{
			D(item.BaseCount); D(item.ScoreCount); D(item.RankingCount);
		}
		foreach (ArenaRewardItem item in new[] { expected.Courage, expected.Crucible })
		{
			D(item.ItemId); D(item.BaseCount); D(item.ScoreCount); D(item.RankingCount);
		}
		foreach (RewardItem? item in new[] { item1, item2 })
		{
			D(item?.GetId() ?? 0); D(item == null ? 0 : (int)item.GetCount());
		}
		return block.ToArray();
	}

	/// <summary>The body of a harmony SM_INSTANCE_SCORE after its map, time and progression: the type byte and its fields.</summary>
	private static byte[] HarmonyBody(DecodedBotServerPacket packet) => Convert.FromHexString(packet.Get<string>("bodyHex"))[12..];

	private static int HarmonyType(DecodedBotServerPacket packet) => HarmonyBody(packet)[0];

	/// <summary>
	/// The Arena of Glory (300550000), weekends 20:00 to 22:00, levels 56 to 60, three tickets: the closed hour's refusal, a
	/// player with too few tickets, a player in a group, four registrations that fill it, a quick entry that waits while it is
	/// full and is taken into the running arena when one of the four declines (checkQueueForQuickEntries), the fights, the
	/// four ranks' rewards and the exit.
	/// </summary>
	private async Task PlayGloryAsync(Player[] players, DateTimeOffset day, CancellationToken token)
	{
		SimulationL0Session[] sessions = arenaCast;
		(SimulationL0Session first, SimulationL0Session second, SimulationL0Session third, SimulationL0Session fourth, SimulationL0Session fifth,
			SimulationL0Session sixth) = (sessions[0], sessions[1], sessions[2], sessions[3], sessions[4], sessions[5]);
		players = sessions.Select(session => fixture.World.GetPlayer(session.CharacterId)).ToArray(); // the fifth logged in again
		Player firstPlayer = players[0], secondPlayer = players[1], thirdPlayer = players[2], fourthPlayer = players[3], fifthPlayer = players[4];
		const int TerathMask = 3, TerathDredgion = 300440000;

		// s31, 19:55: the first five are made level 56 and given Glory tickets (three each, two for the second); everyone
		// beside the Glory gate.
		foreach (SimulationL0Session session in sessions) session.BeginStep("s31", "glory-setup");
		await AdvanceSiegeWorldToAsync(sessions, day.AddHours(19).AddMinutes(55), token);
		for (int i = 0; i < 5; i++)
		{
			players[i].GetCommonData().SetLevel(56);
			Assert.Equal(0, ItemService.AddItem(players[i], GloryTicket, i == 1 ? 2 : 3));
			await TeleportBesideGateAsync(sessions[i], players[i], GloryGate, i, token);
		}
		await SyncSiegeSessionsAsync(sessions, token);

		// s32, 19:59: the Arena of Glory opens at 20:00 on weekends (PvPArenaService.isGloryArenaAvailable).
		first.BeginStep("s32", "glory-closed-before-eight");
		await AdvanceSiegeWorldToAsync(sessions, day.AddHours(19).AddMinutes(59), token);
		await OpenEntryWindowAsync(first, GloryGate, GloryMask, ArenaOfGlory, 403554, 903479, token);
		int mark = first.PacketHistory.Count;
		await first.SendPacketAsync(GameClientPackets.AutoGroup(GloryMask, 100, 0), token);
		await first.SynchronizeAsync(token);
		AssertArenaRefusal(first, mark, "STR_MSG_INSTANCE_CLOSED_TIME", ArenaOfGlory.ToString());

		// s33, 20:00: the dredgions' evening registration opens too, Terath Dredgion's for the five at 56. The second, with two
		// tickets, is refused (three needed); the fourth, still in the third's group, may not register alone.
		int[] marks = Marks(sessions);
		await AdvanceSiegeWorldToAsync(sessions, day.AddHours(20).AddSeconds(1), token);
		await SyncSiegeSessionsAsync(sessions, token);
		for (int i = 0; i < 5; i++)
		{
			Assert.Equal([new ArenaWindow(TerathMask, ArenaWindowIcon, TerathDredgion, 401836, 401837, 1, "")], AutoGroupWindows(sessions[i], marks[i]));
			Assert.Single(sessions[i].PacketHistory.Skip(marks[i]), packet => IsSystemMessage(packet, "STR_MSG_INSTANCE_OPEN_IDDREADGION_03"));
		}
		second.BeginStep("s33", "too-few-glory-tickets");
		await OpenEntryWindowAsync(second, GloryGate, GloryMask, ArenaOfGlory, 403554, 903479, token);
		mark = second.PacketHistory.Count;
		await second.SendPacketAsync(GameClientPackets.AutoGroup(GloryMask, 100, 0), token);
		await second.SynchronizeAsync(token);
		AssertArenaRefusal(second, mark, "STR_MSG_INSTANCE_CANT_ENTER_WITHOUT_ITEM");
		Assert.Equal(0, ItemService.AddItem(secondPlayer, GloryTicket, 1));
		fourth.BeginStep("s33", "in-a-group");
		Assert.True(fourthPlayer.IsInGroup());
		await OpenEntryWindowAsync(fourth, GloryGate, GloryMask, ArenaOfGlory, 403554, 903479, token);
		mark = fourth.PacketHistory.Count;
		await fourth.SendPacketAsync(GameClientPackets.AutoGroup(GloryMask, 100, 0), token);
		await fourth.SynchronizeAsync(token);
		AssertArenaRefusal(fourth, mark, "STR_MSG_CANT_INSTANCE_NOT_LEADER");
		foreach (SimulationL0Session session in new[] { third, fourth })
		{
			await session.SendPacketAsync(session.Api.LeaveGroup(), token);
			await SyncSiegeSessionsAsync(sessions, token);
		}
		Assert.All(new[] { thirdPlayer, fourthPlayer, fifthPlayer }, player => Assert.False(player.IsInTeam()));

		// s34: the first four register alone and fill the arena (four, instance_cooltimes' members of 300550000); the fifth asks
		// for a quick entry, which the full arena refuses, so he waits; the fourth declines, and the fifth is taken into the
		// running arena in his place.
		foreach (SimulationL0Session session in new[] { first, second, third })
		{
			session.BeginStep("s34", "register-for-glory");
			if (session != first)
				await OpenEntryWindowAsync(session, GloryGate, GloryMask, ArenaOfGlory, 403554, 903479, token);
			await RegisterAloneAsync(session, fixture.World.GetPlayer(session.CharacterId), GloryMask, ArenaOfGlory, token);
		}
		fourth.BeginStep("s34", "register-and-decline");
		HashSet<int> before = ArenaInstanceIds(ArenaOfGlory);
		marks = Marks(sessions);
		await RegisterAloneAsync(fourth, fourthPlayer, GloryMask, ArenaOfGlory, token);
		await SyncSiegeSessionsAsync(sessions, token);
		DateTimeOffset created = SystemClock.UtcNow();
		WorldMapInstance arena = Assert.Single(ArenaInstances(ArenaOfGlory), instance => !before.Contains(instance.GetInstanceId()));
		for (int i = 0; i < 4; i++)
			Assert.Equal(new ArenaWindow(GloryMask, ArenaWindowEnter, ArenaOfGlory, 0, 0, 0, ""), AutoGroupWindows(sessions[i], marks[i]).Last());
		ArenaOfGloryInstance handler = Assert.IsType<ArenaOfGloryInstance>(arena.GetInstanceHandler());
		PvPArenaScore score = Assert.IsType<PvPArenaScore>(handler.GetInstanceScore());
		Assert.Equal((4, 1), (arena.GetMaxPlayers(), score.GetDifficultyId()));
		fifth.BeginStep("s34", "quick-entry");
		await OpenEntryWindowAsync(fifth, GloryGate, GloryMask, ArenaOfGlory, 403554, 903479, token);
		mark = fifth.PacketHistory.Count;
		await fifth.SendPacketAsync(GameClientPackets.AutoGroup(GloryMask, 100, 1), token);
		await fifth.SynchronizeAsync(token);
		Assert.Equal([new ArenaWindow(GloryMask, ArenaWindowWaiting, ArenaOfGlory, 0, 0, 1, fifthPlayer.GetName())], AutoGroupWindows(fifth, mark));
		Assert.True(AutoGroupService.GetInstance().IsSearching(fifthPlayer, GloryMask));
		marks = Marks(sessions);
		await fourth.SendPacketAsync(GameClientPackets.AutoGroup(GloryMask, 103), token);
		await SyncSiegeSessionsAsync(sessions, token);
		Assert.Equal([new ArenaWindow(GloryMask, ArenaWindowCancel, ArenaOfGlory, 0, 0, 0, "")], AutoGroupWindows(fourth, marks[3]));
		Assert.Equal([new ArenaWindow(GloryMask, ArenaWindowEnter, ArenaOfGlory, 0, 0, 0, "")], AutoGroupWindows(fifth, marks[4]));
		Assert.False(AutoGroupService.GetInstance().IsSearching(fifthPlayer, GloryMask));
		Assert.Equal(3, fourthPlayer.GetInventory().GetItemCountByItemId(GloryTicket));

		// s35: the four enter, each paying three tickets.
		var entered = new Dictionary<int, long>();
		Player[] fighters = [firstPlayer, secondPlayer, thirdPlayer, fifthPlayer];
		SimulationL0Session[] fighterSessions = [first, second, third, fifth];
		for (int i = 0; i < 4; i++)
		{
			fighterSessions[i].BeginStep("s35", "enter-glory");
			entered[fighters[i].GetObjectId()] = await EnterArenaAsync(fighterSessions[i], fighters[i], GloryMask, arena, token);
		}
		await SyncSiegeSessionsAsync(sessions, token);
		int zone = AssertArenaPosition(secondPlayer, score);
		foreach (Player player in fighters)
		{
			Assert.Equal(zone, AssertArenaPosition(player, score));
			AssertTimeBonus(score.GetPlayerReward(player.GetObjectId()), 10800, entered[player.GetObjectId()]);
		}
		Assert.Equal(4, fighters.Select(player => score.GetPlayerReward(player.GetObjectId()).GetPosition()).Distinct().Count());
		(SimulationL0Session, Player)[] inside = fighterSessions.Zip(fighters).ToArray();
		await ClearArenaMonstersAsync(inside, arena, token);

		// s36, creation + 120 s: the first round; the first kills the second and the third the fifth (1000 points a kill, 200 lost).
		await AdvanceSiegeWorldToAsync(sessions, created.AddSeconds(120.5), token);
		await SyncSiegeSessionsAsync(sessions, token);
		Assert.True(score.IsStartProgress());
		first.BeginStep("s36", "glory-fights");
		await FightInArenaAsync(first, firstPlayer, second, secondPlayer, arena, token);
		await ReviveInArenaAsync(second, secondPlayer, score, token);
		third.BeginStep("s36", "glory-fights");
		await FightInArenaAsync(third, thirdPlayer, fifth, fifthPlayer, arena, token);
		await ReviveInArenaAsync(fifth, fifthPlayer, score, token);
		Assert.Equal([14000, 12800, 14000, 12800], fighters.Select(player => score.GetPlayerReward(player.GetObjectId()).GetPoints()));

		// The fifth logs out of the running arena (PvPArenaInstance.onPlayerLogout keeps the time) and back in:
		// InstanceService.onPlayerLogin puts him back into the arena he is registered in, and on arrival he is ported to a
		// position of the zone and loses the offline milliseconds times bonus / 660000 of his time bonus (updateBonusTime).
		fifth.BeginStep("s36", "relog-inside-glory");
		int fifthId = fifth.CharacterId;
		PvPArenaPlayerReward fifthReward = score.GetPlayerReward(fifthId);
		int bonusBefore = fifthReward.GetScorePoints() - fifthReward.GetPoints();
		await fifth.QuitAsync(token);
		long loggedOut = SystemClock.CurrentMillis();
		Assert.True(arena.IsRegistered(fifthId));
		await AdvanceSiegeWorldToAsync(sessions.Where(session => session != fifth).ToArray(), SystemClock.UtcNow().AddSeconds(11), token);
		await fifth.LoginCharacterListAsync(token);
		fifth.SelectCharacter(fifthId, "Asimarenae");
		int relogMark = fifth.PacketHistory.Count;
		await fifth.EnterWorldAsync(token);
		await fifth.SynchronizeAsync(token);
		fifth.AcceptTeleportPosition();
		long loggedIn = SystemClock.CurrentMillis();
		fifthPlayer = fixture.World.GetPlayer(fifthId);
		Assert.Equal((ArenaOfGlory, arena.GetInstanceId()), (fifthPlayer.GetWorldId(), fifthPlayer.GetInstanceId()));
		Assert.Same(fifthReward, score.GetPlayerReward(fifthId));
		Assert.Equal(bonusBefore - (int)(unchecked((int)(loggedIn - loggedOut)) * ((float)10800 / (float)660000)), fifthReward.GetScorePoints() - fifthReward.GetPoints());
		Assert.Equal(zone, AssertArenaPosition(fifthPlayer, score));
		Assert.Contains(fifth.PacketHistory.Skip(relogMark), packet => packet.PacketType == typeof(SM_INSTANCE_SCORE));
		fighters[3] = fifthPlayer;
		inside[3] = (fifth, fifthPlayer);
		await PlayRoundsAsync(inside, arena, score, created, token);

		// s37, creation + 660 s: the end. Equal scores share the higher rank (getRank counts those at or above), and
		// ArenaOfGloryInstance gives rank 1 its Mithril Medal and then, in the same branch, three Platinum Medals in its place
		// (rank 2's reward too).
		var apBefore = fighters.ToDictionary(player => player.GetObjectId(), player => player.GetAbyssRank().GetAp());
		var crucibleBefore = fighters.ToDictionary(player => player.GetObjectId(), player => player.GetInventory().GetItemCountByItemId(CrucibleInsignia));
		var courageBefore = fighters.ToDictionary(player => player.GetObjectId(), player => player.GetInventory().GetItemCountByItemId(CourageInsignia));
		foreach (SimulationL0Session session in fighterSessions) session.BeginStep("s37", "glory-rewards");
		await AdvanceSiegeWorldToAsync(sessions, created.AddSeconds(660.5), token);
		await SyncSiegeSessionsAsync(sessions, token);
		Assert.True(score.IsRewarded());
		int total = score.GetTotalPoints();
		for (int i = 0; i < 4; i++)
		{
			Player player = fighters[i];
			PvPArenaPlayerReward reward = score.GetPlayerReward(player.GetObjectId());
			int rank = score.GetRank(reward);
			// The first and the third tie at 14000 points; the fifth's relog cost him time bonus, so the second ranks above him.
			Assert.Equal(i switch { 0 or 2 => 1, 1 => 2, _ => 3 }, rank);
			ExpectedArenaReward expected = ExpectedArenaRewardOf((1100, 0, 655, 77), (0, 0, 0, 0), playerCount: 4, reward.GetScorePoints(), total,
				rank switch { 0 => 0.55f, 1 => 0.25f, 2 => 0.15f, _ => 0.05f }, 1f, new ArenaRewardItem(0, 0, 0, 0));
			AssertArenaReward(reward, expected);
			RewardItem item1 = rank is 1 or 2 ? new RewardItem(186000096, 3) : new RewardItem(162000077, 1);
			Assert.Equal(item1.ToString(), reward.GetRewardItem1()?.ToString());
			Assert.Null(reward.GetRewardItem2());
			Assert.Equal(apBefore[player.GetObjectId()] + expected.Ap.GetTotalCount(), player.GetAbyssRank().GetAp());
			Assert.Equal(crucibleBefore[player.GetObjectId()] + expected.Crucible.GetTotalCount(), player.GetInventory().GetItemCountByItemId(CrucibleInsignia));
			Assert.Equal(courageBefore[player.GetObjectId()] + expected.Courage.GetTotalCount(), player.GetInventory().GetItemCountByItemId(CourageInsignia));
			Assert.Equal(item1.GetCount(), player.GetInventory().GetItemCountByItemId(item1.GetId()));
			Assert.Equal(0, player.GetInventory().GetItemCountByItemId(186000147));
			// ArenaOfGloryInstance's packets carry no reward table flag; the owner's rewards are written all the same.
			ArenaScore result = LastArenaScore(fighterSessions[i]);
			Assert.Equal((EndProgress, 0, 0), (result.Progression, result.Time, result.RewardTable));
			AssertArenaRows(result, score, rewarded: true);
			Assert.Equal(ArenaRewardBlock(expected, item1, null), result.Rewards);
		}

		// s38, end + 60 s: the four at the exit, the arena gone.
		foreach (SimulationL0Session session in fighterSessions) session.BeginStep("s38", "leave-glory");
		await AdvanceSiegeWorldToAsync(sessions, created.AddSeconds(720.5), token);
		await SyncSiegeSessionsAsync(sessions, token);
		for (int i = 0; i < 4; i++)
			AssertAtArenaExit(fighterSessions[i], fighters[i]);
		Assert.DoesNotContain(ArenaInstances(ArenaOfGlory), instance => instance.GetInstanceId() == arena.GetInstanceId());
	}

	// ---------------------------------------------------------------------------------------------------------------------
	// Helpers

	// Marchutan Priory (120080000) is declared with the rift scenario.

	/// <summary>The spot of each subject in Marchutan Priory: 3 m from its gate (talk distance 7), spread so they do not stack.</summary>
	private async Task TeleportBesideGateAsync(SimulationL0Session session, Player player, int gate, int index, CancellationToken token)
	{
		(float x, float y) = gate switch
		{
			DisciplineGate => (559.5f - index * 0.5f, 340.0f),
			HarmonyGate => (538.0f + index * 0.5f, 316.5f),
			GloryGate => (560.5f - index * 0.5f, 316.5f),
			_ => throw new ArgumentOutOfRangeException(nameof(gate)),
		};
		await TeleportForSetupAsync(session, player, MarchutanPriory, x, y, Ground(MarchutanPriory, x, y, 100, 85), token);
	}

	/// <summary>
	/// Talks to an arena gate (PortalDialogAI: page 1011) and selects INSTANCE_PARTY_MATCH: SM_AUTO_GROUP's entry request
	/// (window 0) for the arena the player's level takes, and the closing dialog page 0.
	/// </summary>
	private static async Task OpenEntryWindowAsync(SimulationL0Session session, int gateNpcId, int mask, int mapId, int messageId, int titleId,
		CancellationToken token)
	{
		int gate = await session.WaitForNpcAsync(gateNpcId, token);
		int mark = session.PacketHistory.Count;
		await session.SendPacketAsync(session.Api.TalkTo(gate), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == gate && packet.Get<ushort>("dialogPageId") == 1011);
		await session.SendPacketAsync(session.Api.SelectDialog(gate, InstancePartyMatch), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == gate && packet.Get<ushort>("dialogPageId") == 0);
		Assert.Equal([new ArenaWindow(mask, ArenaWindowRequest, mapId, messageId, titleId, 0, "")], AutoGroupWindows(session, mark));
	}

	/// <summary>
	/// A registration of one player (NEW_GROUP_ENTRY): AutoGroupUtility.sendSuccessfulRegistration's success message and the
	/// waiting window with the request type and the leader's name.
	/// </summary>
	private static async Task RegisterAloneAsync(SimulationL0Session session, Player player, int mask, int mapId, CancellationToken token)
	{
		int mark = session.PacketHistory.Count;
		await session.SendPacketAsync(GameClientPackets.AutoGroup(mask, 100, 0), token);
		await session.SynchronizeAsync(token);
		DecodedBotServerPacket[] since = session.PacketHistory.Skip(mark).ToArray();
		int success = Array.FindIndex(since, packet => IsSystemMessage(packet, "STR_MSG_INSTANCE_REGISTER_SUCCESS"));
		Assert.True(success >= 0, $"{player.GetName()} got no registration success");
		ArenaWindow waiting = AutoGroupWindows(since.Skip(success)).First();
		Assert.Equal(new ArenaWindow(mask, ArenaWindowWaiting, mapId, 0, 0, 0, player.GetName()), waiting);
	}

	/// <summary>
	/// CM_AUTO_GROUP 102: AutoGroupService.pressEnter. The ticket is taken, the player is ported to the arena, answers the map
	/// load, and gets the entered window (5). Returns the clock at his arrival (no world time passes while packets are read).
	/// </summary>
	private async Task<long> EnterArenaAsync(SimulationL0Session session, Player player, int mask, WorldMapInstance arena, CancellationToken token)
	{
		int mark = session.PacketHistory.Count;
		(int ticket, int price) = arena.GetMapId() switch { ArenaOfDiscipline => (DisciplineTicket, 1), ArenaOfHarmony => (HarmonyTicket, 1), _ => (GloryTicket, 3) };
		long tickets = player.GetInventory().GetItemCountByItemId(ticket);
		await session.SendPacketAsync(GameClientPackets.AutoGroup(mask, 102), token);
		await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token, packet => packet.Get<int>("worldId") == arena.GetMapId());
		await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
		await session.SynchronizeAsync(token);
		session.AcceptTeleportPosition();
		Assert.Equal((arena.GetMapId(), arena.GetInstanceId()), (player.GetWorldId(), player.GetInstanceId()));
		Assert.Contains(new ArenaWindow(mask, ArenaWindowEntered, arena.GetMapId(), 0, 0, 0, ""), AutoGroupWindows(session, mark));
		Assert.Equal(tickets - price, player.GetInventory().GetItemCountByItemId(ticket));
		Assert.True(arena.IsRegistered(player.GetObjectId()));
		return SystemClock.CurrentMillis();
	}

	/// <summary>
	/// The player stands on the arena's instance position (DisciplineInstancePosition, HarmonyInstancePosition) for his reward's
	/// position in one of the arena's zones; returns that zone.
	/// </summary>
	private static int AssertArenaPosition(Player player, PvPArenaScore score)
	{
		PvPArenaPlayerReward reward = score.GetPlayerReward(player.GetObjectId());
		Assert.NotNull(reward);
		int position = reward.GetPosition();
		(float X, float Y, float Z, sbyte H)[][] table = player.GetWorldId() switch
		{
			ArenaOfDiscipline => DisciplinePositions,
			ArenaOfHarmony => HarmonyPositions.Select(row => row.Select(point => (point.X, point.Y, point.Z, (sbyte)0)).ToArray()).ToArray(),
			_ => GloryPositions.Select(row => row.Select(point => (point.X, point.Y, point.Z, (sbyte)0)).ToArray()).ToArray(),
		};
		Assert.InRange(position, 1, table.Length);
		(float X, float Y, float Z, sbyte H)[] points = table[position - 1];
		int zone = Array.FindIndex(points, point => point.X == player.GetX() && point.Y == player.GetY() && point.Z == player.GetZ()) + 1;
		Assert.True(zone > 0, $"{player.GetName()} stands at ({player.GetX()}, {player.GetY()}, {player.GetZ()}), no point of position {position}");
		Assert.Equal(points[zone - 1].H, (sbyte)player.GetHeading());
		return zone;
	}

	/// <summary>Real auto-attacks of the attacker on the victim, placed beside him and lowered to <see cref="ArenaVictimHealth"/>.</summary>
	private async Task FightInArenaAsync(SimulationL0Session attacker, Player attackerPlayer, SimulationL0Session victim, Player victimPlayer,
		WorldMapInstance arena, CancellationToken token)
	{
		// The first of four spots 1.5 m from the victim with ground under it from which the attacker sees him (GeoService.canSee).
		var geo = Aion.GameServer.World.Geo.GeoService.GetInstance();
		foreach ((float dx, float dy) in new[] { (1.5f, 0f), (-1.5f, 0f), (0f, 1.5f), (0f, -1.5f) })
		{
			float x = victimPlayer.GetX() + dx, y = victimPlayer.GetY() + dy;
			float z = geo.GetZ(arena.GetMapId(), x, y, victimPlayer.GetZ() + 2, victimPlayer.GetZ() - 2, arena.GetInstanceId());
			if (float.IsNaN(z))
				continue;
			await TeleportForSetupAsync(attacker, attackerPlayer, arena.GetMapId(), x, y, z, token, targetInstanceId: arena.GetInstanceId());
			await SyncSiegeSessionsAsync(arenaCast, token);
			if (geo.CanSee(attackerPlayer, victimPlayer))
				break;
		}
		Assert.True(geo.CanSee(attackerPlayer, victimPlayer), $"{attackerPlayer.GetName()} cannot see {victimPlayer.GetName()}");
		if (!attacker.Api.World.Objects.ContainsKey(victimPlayer.GetObjectId()))
			await attacker.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == victimPlayer.GetObjectId());
		Assert.True(attackerPlayer.IsEnemy(victimPlayer), $"{attackerPlayer.GetName()} cannot fight {victimPlayer.GetName()}");
		victimPlayer.GetLifeStats().SetCurrentHp(ArenaVictimHealth);
		await attacker.SendPacketAsync(attacker.Api.Target(victimPlayer.GetObjectId()), token);
		int swings = 0, fightMark = attacker.PacketHistory.Count;
		var health = new List<int>();
		for (int round = 0; round < 40 && !victimPlayer.IsDead(); round++)
		{
			int speed = attackerPlayer.GetGameStats().GetAttackSpeed().GetCurrent();
			if (!attackerPlayer.IsDead() && attacker.Api.Timing.TimeUntilAttack(speed) <= TimeSpan.Zero)
			{
				// A level-56 player's natural regeneration outpaces a starter weapon's blows, so the director lowers him again.
				if (victimPlayer.GetLifeStats().GetCurrentHp() > ArenaVictimHealth)
					victimPlayer.GetLifeStats().SetCurrentHp(ArenaVictimHealth);
				await attacker.SendPacketAsync(attacker.Api.Attack(victimPlayer.GetObjectId(), speed), token);
				swings++;
			}
			await AdvanceSiegeWorldToAsync(arenaCast, SystemClock.UtcNow().AddMilliseconds(500), token);
			health.Add(victimPlayer.GetLifeStats().GetCurrentHp());
		}
		Assert.True(victimPlayer.IsDead(), $"{victimPlayer.GetName()} survived {swings} swings with {victimPlayer.GetLifeStats().GetCurrentHp()} hp, " +
			$"{PositionUtil.GetDistance(attackerPlayer, victimPlayer):F2} m away, sees him {geo.CanSee(attackerPlayer, victimPlayer)}, attacks shown " +
			attacker.PacketHistory.Count(packet => packet.PacketType == typeof(SM_ATTACK) && packet.Get<int>("attackerObjId") == attackerPlayer.GetObjectId()) +
			"; victim hp " + string.Join(",", health) + "; attacker level " + attackerPlayer.GetLevel() + ", victim level " + victimPlayer.GetLevel() +
			", victim max hp " + victimPlayer.GetLifeStats().GetMaxHp() + "; packets since: " + string.Join(" ", attacker.PacketHistory.Skip(fightMark)
				.Where(packet => packet.PacketType != typeof(SM_AFTER_TIME_CHECK_4_7_5) && packet.PacketType != typeof(SM_TIME_CHECK)).Take(30)
				.Select(packet => packet.PacketType.Name + (packet.PacketType == typeof(SM_SYSTEM_MESSAGE) ? "(" + packet.Get<string?>("name") + ")" : ""))));
		// PlayerController.scheduleShowResurrectionOptions: SM_DIE comes half a second after the death.
		await AdvanceSiegeWorldToAsync(arenaCast, SystemClock.UtcNow().AddSeconds(1), token);
		await victim.SynchronizeAsync(token);
		Assert.Contains(victim.PacketHistory, packet => packet.PacketType == typeof(SM_DIE));
	}

	/// <summary>SM_DIE offers the instance revive (PvPArenaInstance has an instance handler); onReviveEvent revives in place and ports.</summary>
	private async Task<int> ReviveInArenaAsync(SimulationL0Session session, Player player, PvPArenaScore score, CancellationToken token)
	{
		DecodedBotServerPacket die = session.PacketHistory.Last(packet => packet.PacketType == typeof(SM_DIE));
		Assert.True(die.Get<bool>("allowInstanceRevive"));
		Assert.False(die.Get<bool>("allowReviveBySkill"));
		int mark = session.PacketHistory.Count;
		await session.SendPacketAsync(session.Api.Revive(BotReviveType.Instance), token);
		await session.SynchronizeAsync(token);
		session.AcceptTeleportPosition();
		Assert.False(player.IsDead());
		Assert.Equal(player.GetLifeStats().GetMaxHp(), player.GetLifeStats().GetCurrentHp());
		Assert.Contains(session.PacketHistory.Skip(mark), packet => IsSystemMessage(packet, "STR_REBIRTH_MASSAGE_ME"));
		return AssertArenaPosition(player, score);
	}

	/// <summary>
	/// Rounds two and three of an arena whose players the scenario does not check at every port: after each round's ports
	/// (changeZone, a second into the round) everyone is in the new zone, whose monsters the director clears.
	/// </summary>
	private async Task PlayRoundsAsync((SimulationL0Session Session, Player Player)[] inside, WorldMapInstance arena, PvPArenaScore score,
		DateTimeOffset created, CancellationToken token)
	{
		foreach ((double seconds, int round) in new[] { (301.5, 2), (481.5, 3) })
		{
			await AdvanceSiegeWorldToAsync(arenaCast, created.AddSeconds(seconds), token);
			await SyncSiegeSessionsAsync(arenaCast, token);
			Assert.Equal(round, score.GetRound());
			foreach ((SimulationL0Session session, _) in inside) session.AcceptTeleportPosition();
			int zone = AssertArenaPosition(inside[0].Player, score);
			Assert.All(inside, fighter => Assert.Equal(zone, AssertArenaPosition(fighter.Player, score)));
			await ClearArenaMonstersAsync(inside, arena, token);
		}
	}

	/// <summary>
	/// The arenas' fields hold aggressive monsters (a Lurking Fangwing stands two metres from one of the Arena of Harmony's
	/// starting points) that kill a level-46 player who waits beside them, or take part in a fight. Once the players are inside,
	/// and again after each round's ports, the director deletes every monster aggressive to them (CreatureType.AGGRESSIVE), so
	/// that only the scenario's fights kill and every kill's points are the fight's.
	/// </summary>
	private async Task ClearArenaMonstersAsync((SimulationL0Session Session, Player Player)[] fighters, WorldMapInstance arena, CancellationToken token)
	{
		foreach (Npc npc in arena.GetNpcs().Where(npc => npc.IsSpawned() && !npc.IsDead() && npc.GetTypeValue(fighters[0].Player) == CreatureType.AGGRESSIVE).ToList())
			npc.GetController().Delete();
		await SyncSiegeSessionsAsync(arenaCast, token);
	}

	/// <summary>The raw time bonus after PvPArenaPlayerReward.updateBonusTime with no logout time (score points less points).</summary>
	private static void AssertTimeBonus(PvPArenaPlayerReward reward, int bonusTime, long enteredAt)
	{
		float modifier = (float)bonusTime / (float)660000;
		int offlineTime = unchecked((int)(enteredAt - 0));
		int expected = bonusTime - (int)(offlineTime * modifier);
		Assert.Equal(expected, reward.GetScorePoints() - reward.GetPoints());
		Assert.Equal(Math.Max(expected, 0), reward.GetTimeBonus());
	}

	/// <summary>A player's or group's rewards as PvPArenaInstance.calculateRewards sets them.</summary>
	private sealed record ExpectedArenaReward(ArenaRewardItem Ap, ArenaRewardItem Gp, ArenaRewardItem Crucible, ArenaRewardItem Courage);

	/// <summary>
	/// PvPArenaInstance.calculateRewards for one reward: the per-player base values times the players, 70% shared by rank
	/// (<paramref name="rankRate"/>) and 30% by the share of the score points, each part cut to an int as Java's float math does.
	/// </summary>
	private static ExpectedArenaReward ExpectedArenaRewardOf((int Ap, int Gp, int Crucible, int Courage) perPlayer, (int Ap, int Gp, int Crucible, int Courage) baseRewards,
		int playerCount, int score, int totalPoints, float rankRate, float configRate, ArenaRewardItem defaultCourage)
	{
		int totalAp = perPlayer.Ap * playerCount, totalGp = perPlayer.Gp * playerCount;
		int totalCrucible = perPlayer.Crucible * playerCount, totalCourage = perPlayer.Courage * playerCount;
		int rankAp = (int)(totalAp * 0.7f), rankGp = (int)(totalGp * 0.7f), rankCrucible = (int)(totalCrucible * 0.7f), rankCourage = (int)(totalCourage * 0.7f);
		int scoreAp = (int)(totalAp * 0.3f), scoreGp = (int)(totalGp * 0.3f), scoreCrucible = (int)(totalCrucible * 0.3f), scoreCourage = (int)(totalCourage * 0.3f);
		float scoreRate = score / (float)totalPoints;
		var ap = new ArenaRewardItem(0, baseRewards.Ap, (int)(rankAp * rankRate * configRate), (int)(scoreAp * scoreRate * configRate));
		var gp = new ArenaRewardItem(0, baseRewards.Gp, (int)(rankGp * rankRate * 1f), (int)(scoreGp * scoreRate * 1f));
		var crucible = new ArenaRewardItem(CrucibleInsignia, baseRewards.Crucible, (int)(rankCrucible * rankRate * configRate), (int)(scoreCrucible * scoreRate * configRate));
		var courage = new ArenaRewardItem(CourageInsignia, baseRewards.Courage, (int)(rankCourage * rankRate * configRate), (int)(scoreCourage * scoreRate * configRate));
		return new ExpectedArenaReward(ap, gp, crucible.GetTotalCount() > 0 ? crucible : new ArenaRewardItem(0, 0, 0, 0),
			courage.GetTotalCount() > 0 ? courage : defaultCourage);
	}

	/// <summary>ArenaOfDisciplineInstance at difficulty 1: 700 AP, 310 crucible and 20 courage insignia a player; base 200 AP and 90 crucible insignia; 72% of the rank share to the first, 28% to the second; RatesConfig's discipline rate 1.0 for an ordinary account.</summary>
	private static ExpectedArenaReward ExpectedDisciplineReward(int score, int totalPoints, int playerCount, int rank) =>
		ExpectedArenaRewardOf((700, 0, 310, 20), (200, 0, 90, 0), playerCount, score, totalPoints, rank == 0 ? 0.72f : 0.28f, 1f, new ArenaRewardItem(0, 0, 0, 0));

	private static void AssertArenaReward(PvPArenaPlayerReward reward, ExpectedArenaReward expected)
	{
		Assert.Equal(expected.Ap, reward.GetAp());
		Assert.Equal(expected.Gp, reward.GetGp());
		Assert.Equal(expected.Crucible, reward.GetCrucibleInsignia());
		Assert.Equal(expected.Courage, reward.GetCourageInsignia());
	}

	/// <summary>ArenaScoreWriter.writeOwnerRewards: AP, GP, crucible and courage insignia (the item id only when it is set: base, score, ranking), then the two items.</summary>
	private static byte[] ArenaRewardBlock(ExpectedArenaReward expected, RewardItem? item1, RewardItem? item2)
	{
		var block = new List<byte>();
		void D(int value) { var bytes = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(bytes, value); block.AddRange(bytes); }
		foreach (ArenaRewardItem item in new[] { expected.Ap, expected.Gp, expected.Crucible, expected.Courage })
		{
			if (item.ItemId != 0)
				D(item.ItemId);
			D(item.BaseCount); D(item.ScoreCount); D(item.RankingCount);
		}
		foreach (RewardItem? item in new[] { item1, item2 })
		{
			D(item?.GetId() ?? 0); D(item == null ? 0 : (int)item.GetCount());
		}
		return block.ToArray();
	}

	/// <summary>PvPArenaInstance.leaveInstance: TeleportService.moveToInstanceExit, instance_exit.xml's Asmodian exit of the arenas.</summary>
	private static void AssertAtArenaExit(SimulationL0Session session, Player player)
	{
		Assert.Contains(session.PacketHistory, packet => packet.PacketType == typeof(SM_PLAYER_SPAWN) && packet.Get<int>("worldId") == MarchutanPriory);
		session.AcceptTeleportPosition();
		Assert.Equal((MarchutanPriory, 549.32f, 328.39505f, 93.49f), (player.GetWorldId(), player.GetX(), player.GetY(), player.GetZ()));
		Assert.False(player.IsDead());
	}

	private static void AssertArenaRefusal(SimulationL0Session session, int mark, string message, params string[] parameters)
	{
		DecodedBotServerPacket refusal = Assert.Single(session.PacketHistory.Skip(mark), packet => IsSystemMessage(packet, message));
		Assert.Equal(parameters, refusal.Get<string[]>("params"));
		Assert.DoesNotContain(session.PacketHistory.Skip(mark), packet => packet.PacketType == typeof(SM_AUTO_GROUP));
	}

	private HashSet<int> ArenaInstanceIds(int mapId) => ArenaInstances(mapId).Select(instance => instance.GetInstanceId()).ToHashSet();

	private List<WorldMapInstance> ArenaInstances(int mapId) => fixture.World.GetWorldMap(mapId).ToList();

	/// <summary>SM_AUTO_GROUP: D mask, C window, D map, three D by window, C 0, S name.</summary>
	private sealed record ArenaWindow(int Mask, int Window, int MapId, int A, int B, int C, string Name);

	private static ArenaWindow[] AutoGroupWindows(SimulationL0Session session, int mark) => AutoGroupWindows(session.PacketHistory.Skip(mark));

	private static ArenaWindow[] AutoGroupWindows(IEnumerable<DecodedBotServerPacket> packets) =>
		packets.Where(packet => packet.PacketType == typeof(SM_AUTO_GROUP)).Select(ReadAutoGroup).ToArray();

	private static ArenaWindow ReadAutoGroup(DecodedBotServerPacket packet)
	{
		var body = new ArenaBodyReader(Convert.FromHexString(packet.Get<string>("bodyHex")));
		int mask = body.D(), window = body.C(), mapId = body.D();
		int a = 0, b = 0, c = 0;
		if (window is 0 or 1 or 2 or 3 or 4 or 5 or 6 or 7 or 8)
			(a, b, c) = (body.D(), body.D(), body.D());
		Assert.Equal(0, body.C());
		var window1 = new ArenaWindow(mask, window, mapId, a, b, c, body.S());
		body.End();
		return window1;
	}

	/// <summary>
	/// SM_INSTANCE_SCORE with ArenaScoreWriter: D map, D time, D progression; twelve rows of 92 bytes (D owner, D kills, D points,
	/// D 0, C 0, C class, C 1, C rank, D buff time, D time bonus, D buff id, D 0, H participation, S54 name); the owner's
	/// rewards (72 bytes or fewer); D 0, D 0, D round, D upper cap, D 3, D reward table.
	/// </summary>
	private sealed record ArenaScore(int MapId, int Time, int Progression, ArenaRow[] Rows, byte[] Rewards, int Round, int UpperCap, int Rounds, int RewardTable);

	private sealed record ArenaRow(int OwnerId, int Kills, int Points, int ClassId, int Rank, int BuffTime, int TimeBonus, int BuffId, int Participation, string Name);

	private static ArenaScore LastArenaScore(SimulationL0Session session) =>
		ReadArenaScore(session.PacketHistory.Last(packet => packet.PacketType == typeof(SM_INSTANCE_SCORE)));

	private static ArenaScore ReadArenaScore(DecodedBotServerPacket packet)
	{
		byte[] raw = Convert.FromHexString(packet.Get<string>("bodyHex"));
		var body = new ArenaBodyReader(raw);
		int mapId = body.D(), time = body.D(), progression = body.D();
		var rows = new List<ArenaRow>();
		for (int i = 0; i < 12; i++)
		{
			int owner = body.D(), kills = body.D(), points = body.D();
			Assert.Equal(0, body.D());
			Assert.Equal(0, body.C());
			int classId = body.C();
			Assert.Equal(owner == 0 ? 0 : 1, body.C());
			int rank = body.C(), buffTime = body.D(), timeBonus = body.D(), buffId = body.D();
			Assert.Equal(0, body.D());
			int participation = body.H();
			string name = body.FixedS(54);
			if (owner != 0)
				rows.Add(new ArenaRow(owner, kills, points, classId, rank, buffTime, timeBonus, buffId, participation, name));
			else
				Assert.Equal(new ArenaRow(0, 0, 0, 0, 0, 0, 0, 0, 0, ""), new ArenaRow(owner, kills, points, classId, rank, buffTime, timeBonus, buffId, participation, name));
		}
		int rewardLength = raw.Length - body.Position - 24;
		byte[] rewards = body.B(rewardLength);
		Assert.Equal(0, body.D());
		Assert.Equal(0, body.D());
		var score = new ArenaScore(mapId, time, progression, rows.ToArray(), rewards, body.D(), body.D(), body.D(), body.D());
		body.End();
		return score;
	}

	/// <summary>
	/// The rows are the score's player rewards, compared by owner: Java iterates its ConcurrentHashMap and the port its
	/// ConcurrentDictionary, whose orders differ (see the plan, P11-03).
	/// </summary>
	private static void AssertArenaRows(ArenaScore packet, PvPArenaScore score, bool rewarded)
	{
		Assert.Equal(score.GetPlayerRewards().Select(reward => reward.GetOwnerId()).Order(), packet.Rows.Select(row => row.OwnerId).Order());
		foreach (ArenaRow row in packet.Rows)
		{
			PvPArenaPlayerReward reward = score.GetPlayerReward(row.OwnerId);
			// The boost of morale's remaining time was written when the packet was sent, up to two seconds before this check.
			int remaining = reward.GetRemainingTime();
			Assert.InRange(row.BuffTime, remaining, remaining == 0 && row.BuffTime > 2000 ? 0 : remaining + 2000);
			Assert.Equal(new ArenaRow(reward.GetOwnerId(), reward.GetPvPKills(), rewarded ? reward.GetScorePoints() : reward.GetPoints(),
				reward.GetPlayerClass()!.Value.GetClassId(), score.GetRank(reward), row.BuffTime, rewarded ? reward.GetTimeBonus() : 0,
				row.BuffTime > 0 ? score.GetBuffId() : 0, rewarded ? (short)(reward.GetParticipation() * 100) : 0, reward.GetPlayerName()), row);
		}
	}

	private struct ArenaBodyReader(byte[] body)
	{
		private int position;
		public readonly int Position => position;
		public int C() => body[position++];
		public int H() { int value = BinaryPrimitives.ReadInt16LittleEndian(body.AsSpan(position)); position += 2; return value; }
		public int D() { int value = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(position)); position += 4; return value; }
		public byte[] B(int count) { byte[] value = body.AsSpan(position, count).ToArray(); position += count; return value; }

		public string S()
		{
			var text = new StringBuilder();
			for (char c = (char)BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(position)); ; c = (char)BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(position)))
			{
				position += 2;
				if (c == '\0') return text.ToString();
				text.Append(c);
			}
		}

		public string FixedS(int size)
		{
			string text = Encoding.Unicode.GetString(body, position, size).TrimEnd('\0');
			position += size;
			return text;
		}

		public readonly void End() => Assert.Equal(body.Length, position);
	}
}
