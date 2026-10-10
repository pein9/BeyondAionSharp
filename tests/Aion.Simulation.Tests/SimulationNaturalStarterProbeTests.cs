using System.Text.Json;
using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private sealed record StarterProbeRow(string Name, NaturalClassLine Line, int Account, string CharacterName);

	// CP-42, CP-43. A later item adds its row here; each row plays on one of the two probe accounts, so one process
	// runs at most two rows.
	private static readonly StarterProbeRow[] StarterProbeRows =
	[
		new("warrior-rest", NaturalClassLine.Warrior, ProbeAccountA, "Asimrestwar"),
		new("warrior-1", NaturalClassLine.Warrior, ProbeAccountA, "Asimonewar"),
		new("warrior-7", NaturalClassLine.Warrior, ProbeAccountB, "Asimsevenwar"),
		new("mage-1", NaturalClassLine.Mage, ProbeAccountA, "Asimonemage"),
		new("mage-5", NaturalClassLine.Mage, ProbeAccountB, "Asimfivemage"),
		new("artist-1", NaturalClassLine.Artist, ProbeAccountA, "Asimoneart"),
		new("artist-5", NaturalClassLine.Artist, ProbeAccountB, "Asimfiveart"),
		new("engineer-1", NaturalClassLine.Engineer, ProbeAccountA, "Asimoneengi"),
		new("engineer-5", NaturalClassLine.Engineer, ProbeAccountB, "Asimfiveengi"),
		new("scout-1", NaturalClassLine.Scout, ProbeAccountA, "Asimonescout"),
		new("scout-7", NaturalClassLine.Scout, ProbeAccountB, "Asimsevenscout"),
		new("warrior-pack", NaturalClassLine.Warrior, ProbeAccountA, "Asimpackwar"),
		new("scout-two", NaturalClassLine.Scout, ProbeAccountA, "Asimtwodagger"),
		new("priest-1", NaturalClassLine.PriestCleric, ProbeAccountA, "Asimonepriest"),
		new("priest-7", NaturalClassLine.PriestCleric, ProbeAccountB, "Asimsevpriest"),
		new("cleric-10", NaturalClassLine.PriestCleric, ProbeAccountA, "Asimtencleric"),
		new("cleric-16", NaturalClassLine.PriestCleric, ProbeAccountB, "Asimsixcleric"),
		new("cleric-20", NaturalClassLine.PriestCleric, ProbeAccountA, "Asimtwecleric"),
		new("cleric-25", NaturalClassLine.PriestCleric, ProbeAccountB, "Asimtfcleric"),
		new("templar-10", NaturalClassLine.WarriorTemplar, ProbeAccountA, "Asimtentemp"),
		new("templar-16", NaturalClassLine.WarriorTemplar, ProbeAccountB, "Asimsixtemp"),
		new("templar-20", NaturalClassLine.WarriorTemplar, ProbeAccountA, "Asimtwetemp"),
		new("templar-25", NaturalClassLine.WarriorTemplar, ProbeAccountB, "Asimtftemp"),
		new("sorcerer-10", NaturalClassLine.MageSorcerer, ProbeAccountA, "Asimtensorc"),
		new("sorcerer-16", NaturalClassLine.MageSorcerer, ProbeAccountB, "Asimsixsorc"),
		new("sorcerer-20", NaturalClassLine.MageSorcerer, ProbeAccountA, "Asimtwesorc"),
		new("sorcerer-25", NaturalClassLine.MageSorcerer, ProbeAccountB, "Asimtfsorc"),
		new("chanter-10", NaturalClassLine.PriestChanter, ProbeAccountA, "Asimtenchan"),
		new("chanter-16", NaturalClassLine.PriestChanter, ProbeAccountB, "Asimsixchan"),
		new("chanter-20", NaturalClassLine.PriestChanter, ProbeAccountA, "Asimtwechan"),
		new("chanter-25", NaturalClassLine.PriestChanter, ProbeAccountB, "Asimtfchan"),
		new("chanter-mantras", NaturalClassLine.PriestChanter, ProbeAccountA, "Asimmantra"),
		new("gladiator-aerial", NaturalClassLine.WarriorGladiator, ProbeAccountB, "Asimaerial"),
		new("assassin-runes", NaturalClassLine.ScoutAssassin, ProbeAccountA, "Asimrunes"),
		new("spirit-master-spirit", NaturalClassLine.MageSpiritMaster, ProbeAccountB, "Asimsummon"),
		new("spirit-master-walk", NaturalClassLine.MageSpiritMaster, ProbeAccountA, "Asimwalk"),
		new("spirit-master-fight", NaturalClassLine.MageSpiritMaster, ProbeAccountB, "Asimfight"),
		new("spirit-master-orders", NaturalClassLine.MageSpiritMaster, ProbeAccountA, "Asimorders"),
		new("spirit-master-pack", NaturalClassLine.MageSpiritMaster, ProbeAccountB, "Asimpack"),
		new("spirit-master-place", NaturalClassLine.MageSpiritMaster, ProbeAccountA, "Asimplace"),
		new("gunner-chain", NaturalClassLine.EngineerGunner, ProbeAccountB, "Asimchain"),
		new("gunner-reload", NaturalClassLine.EngineerGunner, ProbeAccountA, "Asimreload"),
		new("rider-mech", NaturalClassLine.EngineerRider, ProbeAccountB, "Asimmech"),
		new("sorcerer-blast", NaturalClassLine.MageSorcerer, ProbeAccountA, "Asimblast"),
	];

	public static TheoryData<string> StarterProbeRowNames => new(StarterProbeRows.Select(row => row.Name));

	/// <summary>One trace record of a probe step.</summary>
	private sealed record StarterTraceRecord(string Direction, string Packet, TimeSpan VirtualTime, JsonElement Fields);

	/// <summary>
	/// CP-42: what every row of the starter field probe starts from. Prepared here, and nowhere in a journey: a character
	/// of the row's class line created by packets on a probe account, with no kit supplied, and a journey runtime over its
	/// session. The director's setup acts are a level change, an HP cut and a placement; a row says which it used.
	/// </summary>
	private sealed class StarterProbe(SimulationWorldFixture fixture, SimulationL0Session session, Player player, NaturalJourneyRuntime runtime,
		NaturalClassLine line, string tracePath, Func<float, float, float, Task> placeAsync,
		Func<int, float, float, float, Task<BotNavigationGeometry>> moveAsync, CancellationToken token)
	{
		public SimulationL0Session Session => session;
		public Player Server => player;
		public BotWorldModel World => session.Api.World;
		public NaturalIshalgenJourney Journey => new(session, runtime, new NaturalJourneyOptions(ClassLine: line));
		public long NowMillis => fixture.Clock.NowMillis;

		/// <summary>The director sets the character's level; the client sees the level-up as it would any other.</summary>
		public async Task SetLevelAsync(int level)
		{
			player.GetCommonData().SetLevel(level);
			await session.SynchronizeAsync(token);
			Assert.Equal(level, session.Api.World.Level);
		}

		/// <summary>The director cuts the character's HP to a percentage of its maximum; the client sees the HP update.</summary>
		public async Task CutHpAsync(int percent)
		{
			player.GetLifeStats().SetCurrentHpPercent(percent);
			await session.SynchronizeAsync(token);
			Assert.True(session.Api.World.CurrentHp * 100 <= session.Api.World.MaxHp * percent, $"HP was not cut to {percent}%.");
		}

		/// <summary>NR-51: the director cuts the character's MP to a percentage of its maximum; the client sees the MP update.</summary>
		public async Task CutMpAsync(int percent)
		{
			player.GetLifeStats().SetCurrentMpPercent(percent);
			await session.SynchronizeAsync(token);
			Assert.True(session.Api.World.CurrentMp * 100 <= session.Api.World.MaxMp * percent, $"MP was not cut to {percent}%.");
		}

		/// <summary>The director places the character on its map; the client takes the position the teleport gave it.</summary>
		public async Task PlaceAsync(float x, float y, float z)
		{
			await placeAsync(x, y, z);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
		}

		public long Owned(int itemId) => session.Api.World.Inventory.Values.Where(item => item.ItemId == itemId).Sum(item => item.Count);

		/// <summary>NR-71: the skills of a class's catalog on the row's line, which is every skill its table, its buff check and
		/// its rest may cast.</summary>
		public int[] CatalogOf(PlayerClass playerClass) =>
			NaturalClassProfiles.For(playerClass.GetClassId(), line, runtime.Data).Skills.Select(skill => (int)skill.Id).ToArray();

		/// <summary>NR-16: the director makes the character its second class at a level, with the skills of every level up
		/// to it. The client sees the class with its next SM_PLAYER_INFO, which the move to the row's map sends.</summary>
		public async Task BecomeAsync(PlayerClass second, int level)
		{
			Assert.True(ClassChangeService.SetClass(player, second, validate: false, updateDaevaStatus: true));
			player.GetCommonData().SetLevel(level);
			SkillLearnService.LearnNewSkills(player, 1, level);
			await session.SynchronizeAsync(token);
			Assert.Equal(level, session.Api.World.Level);
		}

		/// <summary>NR-16: the director places the character on another map, on the ground at a spot; the row's geometry
		/// is that map's from then on.</summary>
		public Task<BotNavigationGeometry> MoveToMapAsync(int map, float x, float y, float z) => moveAsync(map, x, y, z);

		/// <summary>NR-16: the director sets the character's DP; the client sees the DP update.</summary>
		public async Task SetDpAsync(int dp)
		{
			player.GetCommonData().SetDp(dp);
			await session.SynchronizeAsync(token);
		}


		/// <summary>The trace records of one step: client packets by name, and diagnostics by kind.</summary>
		public IReadOnlyList<StarterTraceRecord> TraceOf(string step)
		{
			var records = new List<StarterTraceRecord>();
			using var stream = new FileStream(tracePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			using var reader = new StreamReader(stream);
			while (reader.ReadLine() is { } line)
			{
				using JsonDocument record = JsonDocument.Parse(line);
				JsonElement root = record.RootElement;
				if (root.TryGetProperty("step", out JsonElement at) && at.GetString() == step)
					records.Add(new(root.GetProperty("dir").GetString() ?? "", root.GetProperty("packet").GetString() ?? "",
						TimeSpan.Parse(root.GetProperty("vt").GetString() ?? "00:00:00"),
						root.TryGetProperty("fields", out JsonElement fields) ? fields.Clone() : default));
			}
			return records;
		}

		/// <summary>The character's completed casts in one step, in order, from the server's cast results.</summary>
		public IReadOnlyList<(int SkillId, TimeSpan At)> CastsOf(string step) => TraceOf(step)
			.Where(record => record is { Direction: "<", Packet: "SM_CASTSPELL_RESULT" } &&
				record.Fields.GetProperty("effectorId").GetInt32() == session.CharacterId)
			.Select(record => (record.Fields.GetProperty("skillId").GetInt32(), record.VirtualTime)).ToArray();

		/// <summary>What the fight loop decided in one step, by action.</summary>
		public IReadOnlyDictionary<string, int> DecisionsOf(string step) => TraceOf(step)
			.Where(record => record is { Direction: "action", Packet: "combat-decision" })
			.GroupBy(record => record.Fields.GetProperty("action").GetString() ?? "")
			.ToDictionary(group => group.Key, group => group.Count());
	}

	/// <summary>
	/// CP-42, CP-43: the starter field probe. Row warrior-rest: the potion-and-sit rest. Rows warrior-1 and warrior-7:
	/// the Warrior's first fights, with the table policy, the chain and swing code and the walk to weapon reach.
	/// </summary>
	[SkippableTheory]
	[MemberData(nameof(StarterProbeRowNames))]
	public async Task NaturalStarterFieldProbe(string rowName)
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		Skip.IfNot(ProbeRowNamed(rowName), $"Row {rowName} is not named in CP_PROBE_ROWS.");
		StarterProbeRow row = StarterProbeRows.Single(candidate => candidate.Name == rowName);
		string id = "CP-probe-" + row.Name;
		using var policy = NewEconomyPolicy(id, includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(8));
		CancellationToken token = timeout.Token;
		string root = RealStaticData.RepoRoot();
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "cp-starter-probe";
		string tracePath = Path.Combine(root, "run", $"{run}-{row.Name}.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(tracePath, run, "b01", $"sim-player-{row.Account}",
			virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", row.Account, row.CharacterName, Race.ASMODIANS, trace, tracePath);
		var dashboard = new LiveBotDashboardState();
		// NR-45: probe rows of several classes are played side by side; a row beside others takes the next free port.
		await using var dashboardHost = LiveBotDashboardHost.OpenFirstFree(run, [id], dashboard,
			int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880"));
		session.Dashboard = dashboard;
		if (dashboardHost.Enabled) Console.WriteLine($"{id} dashboard: {dashboardHost.Url}");
		session.BeginStep("s00", "login-create-enter");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, row.Line.Starter);
		await session.EnterWorldAsync(token);
		await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
		Assert.Equal(row.Line.Starter, player.GetPlayerClass());
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(player.GetInstanceId(), Race.ASMODIANS);
		var runtime = new NaturalJourneyRuntime(root, "SIM-" + id, fixture.Seed, fixture.DataManager.StaticData,
			() => fixture.Clock.NowMillis, fixture.Epoch, () => geometry, _ => Task.FromResult(false), policy.AssertClean,
			() => policy.SnapshotProblems(), trace, dashboard);
		var probe = new StarterProbe(fixture, session, player, runtime, row.Line, tracePath,
			(x, y, z) => TeleportForSetupAsync(session, player, player.GetWorldId(), x, y, z, token),
			async (map, x, y, z) =>
			{
				// The runtime reads this variable, so the journey gets the new map's geometry.
				geometry = BotNavigationGeometry.ForServerWorld(fixture.World.GetWorldMap(map).GetMainWorldMapInstance().GetInstanceId(), Race.ASMODIANS);
				BotPosition ground = geometry.SnapToGround(map, new BotPosition(x, y, z + 1, 0))
					?? throw new InvalidDataException($"No ground at {x}/{y}/{z} on map {map}.");
				session.Api.World.BeginWorldReload();
				await TeleportForSetupAsync(session, player, map, ground.X, ground.Y, ground.Z, token);
				session.AcceptTeleportPosition();
				await session.SynchronizeAsync(token);
				Assert.Equal(row.Line.Second, player.GetPlayerClass());
				return geometry;
			}, token);
		switch (row.Name)
		{
			case "warrior-rest": await WarriorRestRowAsync(probe, id, token); break;
			case "warrior-1": await WarriorLevelOneRowAsync(probe, id, geometry, token); break;
			case "warrior-7": await WarriorLevelSevenRowAsync(probe, id, geometry, token); break;
			case "mage-1": await CasterLevelOneRowAsync(probe, id, geometry, "Mage", "Flame Bolt", 1282, token); break;
			case "mage-5": await MageLevelFiveRowAsync(probe, id, geometry, token); break;
			case "artist-1": await CasterLevelOneRowAsync(probe, id, geometry, "Artist", "Pulse", 4408, token); break;
			case "artist-5": await ArtistLevelFiveRowAsync(probe, id, geometry, token); break;
			case "engineer-1": await CasterLevelOneRowAsync(probe, id, geometry, "Engineer", "Direct Shot", 2219, token); break;
			case "engineer-5": await EngineerLevelFiveRowAsync(probe, id, geometry, token); break;
			case "scout-1": await ScoutLevelOneRowAsync(probe, id, geometry, token); break;
			case "scout-7": await ScoutLevelSevenRowAsync(probe, id, geometry, token); break;
			case "warrior-pack": await WarriorPackRowAsync(probe, id, geometry, token); break;
			case "scout-two": await ScoutTwoDaggersRowAsync(probe, id, geometry, token); break;
			case "priest-1": await PriestLevelOneRowAsync(probe, id, geometry, token); break;
			case "priest-7": await PriestLevelSevenRowAsync(probe, id, geometry, token); break;
			case "cleric-10": await ClericLevelTenRowAsync(probe, id, token); break;
			case "cleric-16": await ClericLevelSixteenRowAsync(probe, id, token); break;
			case "cleric-20": await ClericLevelTwentyRowAsync(probe, id, token); break;
			case "cleric-25": await ClericLevelTwentyFiveRowAsync(probe, id, token); break;
			case "templar-10": await TemplarLevelTenRowAsync(probe, id, token); break;
			case "templar-16": await TemplarLevelSixteenRowAsync(probe, id, token); break;
			case "templar-20": await TemplarLevelTwentyRowAsync(probe, id, token); break;
			case "templar-25": await TemplarLevelTwentyFiveRowAsync(probe, id, token); break;
			case "sorcerer-10": await SorcererLevelTenRowAsync(probe, id, token); break;
			case "sorcerer-16": await SorcererLevelSixteenRowAsync(probe, id, token); break;
			case "sorcerer-20": await SorcererLevelTwentyRowAsync(probe, id, token); break;
			case "sorcerer-25": await SorcererLevelTwentyFiveRowAsync(probe, id, token); break;
			case "chanter-10": await ChanterLevelTenRowAsync(probe, id, token); break;
			case "chanter-16": await ChanterLevelSixteenRowAsync(probe, id, token); break;
			case "chanter-20": await ChanterLevelTwentyRowAsync(probe, id, token); break;
			case "chanter-25": await ChanterLevelTwentyFiveRowAsync(probe, id, token); break;
			case "chanter-mantras": await ChanterMantrasRowAsync(probe, id, token); break;
			case "gladiator-aerial": await GladiatorAerialRowAsync(probe, id, token); break;
			case "assassin-runes": await AssassinRunesRowAsync(probe, id, token); break;
			case "spirit-master-spirit": await SpiritMasterSpiritRowAsync(probe, id, token); break;
			case "spirit-master-walk": await SpiritMasterWalkRowAsync(probe, id, token); break;
			case "spirit-master-fight": await SpiritMasterFightRowAsync(probe, id, token); break;
			case "spirit-master-orders": await SpiritMasterOrdersRowAsync(probe, id, token); break;
			case "spirit-master-pack": await SpiritMasterPackRowAsync(probe, id, token); break;
			case "spirit-master-place": await SpiritMasterPlaceRowAsync(probe, id, token); break;
			case "gunner-chain": await GunnerChainRowAsync(probe, id, token); break;
			case "gunner-reload": await GunnerReloadRowAsync(probe, id, token); break;
			case "rider-mech": await RiderMechRowAsync(probe, id, token); break;
			case "sorcerer-blast": await SorcererBlastRowAsync(probe, id, token); break;
			default: throw new InvalidOperationException($"Row {row.Name} has no body.");
		}
		policy.AssertClean();
	}

	private static async Task WarriorRestRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int potion = NaturalIshalgenPotionPolicy.StarterLifePotionId;
		Assert.Equal(1, probe.World.Level);
		long owned = probe.Owned(potion);
		Assert.True(owned > 0, "The starter owns no Minor Life Potion.");
		Assert.Equal(0, probe.Owned(NaturalIshalgenPotionPolicy.MajorLifePotionId)); // no kit is supplied to a probe

		// Prepared by the director: half HP. The rest must drink one potion and end at the 90% target.
		probe.Session.BeginStep("s01", "director-halves-hp-then-first-rest");
		await probe.CutHpAsync(50);
		long firstStarted = probe.NowMillis;
		await probe.Journey.RunObservedRestAsync(token);
		long firstTook = probe.NowMillis - firstStarted;
		var first = probe.TraceOf("s01");
		Assert.Equal(owned - 1, probe.Owned(potion));
		Assert.Equal(1, first.Count(record => record is { Direction: "action", Packet: "rest-life-potion" }));
		Assert.True(probe.World.CurrentHp * 100 >= probe.World.MaxHp * 90, $"The first rest ended at {probe.World.CurrentHp}/{probe.World.MaxHp}.");
		Assert.False(probe.Server.IsDead());

		// Prepared by the director: half HP again, while the potion's 30 s delay still runs. No potion is ready, so the
		// rest must sit to the target.
		Assert.True(firstTook < 30_000, $"The first rest took {firstTook} ms; the potion's delay has run out.");
		probe.Session.BeginStep("s02", "director-halves-hp-then-second-rest");
		await probe.CutHpAsync(50);
		long secondStarted = probe.NowMillis;
		await probe.Journey.RunObservedRestAsync(token);
		long secondTook = probe.NowMillis - secondStarted;
		var second = probe.TraceOf("s02");
		Assert.Equal(owned - 1, probe.Owned(potion));
		Assert.DoesNotContain(second, record => record is { Direction: "action", Packet: "rest-life-potion" });
		int sits = second.Count(record => record is { Direction: "action", Packet: "rest-sit-for-health" });
		Assert.True(sits >= 1, "The second rest did not sit for health.");
		Assert.True(probe.World.CurrentHp * 100 >= probe.World.MaxHp * 90, $"The second rest ended at {probe.World.CurrentHp}/{probe.World.MaxHp}.");
		Assert.False(probe.Server.IsDead());
		Console.WriteLine($"{id}: level {probe.World.Level} Warrior, max HP {probe.World.MaxHp}. First rest: one Minor Life Potion " +
			$"({owned} to {probe.Owned(potion)}), {first.Count(record => record is { Direction: "action", Packet: "rest-sit-for-health" })} sits for health, {firstTook} ms. " +
			$"Second rest, {secondStarted - firstStarted} ms after the first began: no potion, {sits} sits for health, {secondTook} ms. " +
			$"HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}.");
	}

	/// <summary>A place on checked ground about <paramref name="away"/> metres from a monster's shipped spot.</summary>
	private static BotPosition GroundNear(BotNavigationGeometry geometry, int map, BotPosition spot, float away) =>
		geometry.GroundAround(map, spot, [away]).OrderBy(point => point.X).ThenBy(point => point.Y).First();

	/// <summary>The nearest living monster of a template the client sees.</summary>
	private Npc NearestLiving(StarterProbe probe, int templateId) => probe.World.Objects.Values
		.Where(known => known.TemplateId == templateId)
		.Select(known => fixture.World.FindVisibleObject(known.ObjectId)).OfType<Npc>().Where(npc => !npc.IsDead())
		.OrderBy(npc => MathF.Abs(npc.GetX() - probe.Session.CurrentPosition.X) + MathF.Abs(npc.GetY() - probe.Session.CurrentPosition.Y))
		.First();

	/// <summary>
	/// CP-43, row warrior-1. Prepared by the director: the level-1 Warrior is placed 15 m from a Sprigg Worker's shipped
	/// spot. From there every act is the journey's: three Sprigg Workers (210363, 143 HP) killed one after another through
	/// RunObservedCombatAsync, with its ordinary rest before each, Ferocious Strike and weapon swings.
	/// </summary>
	private async Task WarriorLevelOneRowAsync(StarterProbe probe, string id, BotNavigationGeometry geometry, CancellationToken token)
	{
		const int map = 220010000, sprigg = 210363, strike = 2864;
		Assert.Equal(1, probe.World.Level);
		probe.Session.BeginStep("s01", "director-places-by-sprigg-workers");
		BotPosition spot = GroundNear(geometry, map, new BotPosition(145.856f, 2560.26f, 307.891f, 0), 15);
		await probe.PlaceAsync(spot.X, spot.Y, spot.Z);
		var lines = new List<string>();
		int strikesCast = 0;
		for (int kill = 1; kill <= 3; kill++)
		{
			string step = "s0" + (kill + 1);
			probe.Session.BeginStep(step, $"kill-sprigg-worker-{kill}");
			Npc target = NearestLiving(probe, sprigg);
			int hp = probe.World.CurrentHp;
			NaturalCombatDiagnosticResult result = await probe.Journey.RunObservedCombatAsync(_ => Task.FromResult(target.GetObjectId()), token);
			Assert.True(result.Killed, $"Sprigg Worker {kill} was not killed: {result}.");
			Assert.True(target.IsDead());
			Assert.Equal(0, result.Deaths);
			var casts = probe.CastsOf(step);
			IReadOnlyDictionary<string, int> decisions = probe.DecisionsOf(step);
			// Ferocious Strike has a 10 s cooldown, so a kill that begins inside it is made with the weapon alone.
			Assert.All(casts, cast => Assert.Equal(strike, cast.SkillId));
			strikesCast += casts.Count;
			Assert.True(decisions.GetValueOrDefault("attack") > 0, $"Kill {kill} swung no weapon: {string.Join(", ", decisions)}.");
			Assert.True(decisions.Values.Sum() < 200, $"Kill {kill} took {decisions.Values.Sum()} decisions.");
			lines.Add($"kill {kill}: {result.ElapsedMillis} ms, HP {hp} to {probe.World.CurrentHp}/{probe.World.MaxHp}, " +
				$"{casts.Count} Ferocious Strike, decisions {string.Join(" ", decisions.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key} {entry.Value}"))}");
		}
		Assert.False(probe.Server.IsDead());
		Assert.True(strikesCast > 0, "No Ferocious Strike was cast in three kills.");
		Console.WriteLine($"{id}: level {probe.World.Level} Warrior, three Sprigg Workers. " + string.Join("; ", lines) + ".");
	}

	/// <summary>
	/// CP-43, row warrior-7. Prepared by the director: level 7, a place 18 m from a Fanged Karnif's shipped spot, and half
	/// HP at the moment the fight begins. From there every act is the journey's: Ferocious Strike, Robust Blow and Rage in
	/// that order on one Fanged Karnif (210389, 478 HP, level 6), Rage inside Robust Blow's 3 s.
	/// </summary>
	private async Task WarriorLevelSevenRowAsync(StarterProbe probe, string id, BotNavigationGeometry geometry, CancellationToken token)
	{
		const int map = 220010000, karnif = 210389, rage = 2903;
		int[] strikes = [2864, 2865], robusts = [2877, 2878];
		probe.Session.BeginStep("s01", "director-sets-level-seven-and-places-by-a-karnif");
		await probe.SetLevelAsync(7);
		Assert.All(new[] { 2865, 2877, rage }, skill => Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 7."));
		BotPosition spot = GroundNear(geometry, map, new BotPosition(884.051f, 1652f, 272.994f, 0), 18);
		await probe.PlaceAsync(spot.X, spot.Y, spot.Z);
		probe.Session.BeginStep("s02", "kill-fanged-karnif-from-half-hp");
		Npc target = NearestLiving(probe, karnif);
		NaturalCombatDiagnosticResult result = await probe.Journey.RunObservedCombatAsync(async _ =>
		{
			// Prepared by the director, after the journey's own rest: half HP as the fight begins.
			await probe.CutHpAsync(50);
			return target.GetObjectId();
		}, token);
		var casts = probe.CastsOf("s02").ToList();
		IReadOnlyDictionary<string, int> decisions = probe.DecisionsOf("s02");
		string order = string.Join(" ", casts.Select(cast => $"{cast.SkillId}@{cast.At.TotalSeconds:F1}"));
		int first = casts.FindIndex(cast => strikes.Contains(cast.SkillId));
		Assert.True(first >= 0, $"No Ferocious Strike was cast: {order}.");
		int second = casts.FindIndex(first + 1, cast => robusts.Contains(cast.SkillId));
		Assert.True(second == first + 1, $"Robust Blow did not follow Ferocious Strike at once: {order}.");
		int third = casts.FindIndex(second + 1, cast => cast.SkillId == rage);
		Assert.True(third == second + 1, $"Rage did not follow Robust Blow at once: {order}.");
		TimeSpan gap = casts[third].At - casts[second].At;
		Assert.True(gap <= TimeSpan.FromSeconds(3), $"Rage came {gap.TotalMilliseconds:F0} ms after Robust Blow: {order}.");
		Assert.True(result.Killed, $"The Fanged Karnif was not killed: {result}; casts {order}.");
		Assert.True(target.IsDead());
		Assert.Equal(0, result.Deaths);
		Console.WriteLine($"{id}: level {probe.World.Level} Warrior, max HP {probe.World.MaxHp}, one Fanged Karnif in {result.ElapsedMillis} ms. " +
			$"Casts {order}; Rage {gap.TotalMilliseconds:F0} ms after Robust Blow. " +
			$"Decisions {string.Join(" ", decisions.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key} {entry.Value}"))}. " +
			$"HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}.");
	}

	/// <summary>
	/// CP-46 to CP-48, rows mage-1, artist-1 and engineer-1. Prepared by the director: the level-1 caster is placed 18 m from a
	/// Sprigg Worker's shipped spot, and its HP is halved before the second and before the third kill. Every other act is
	/// the journey's: three Sprigg Workers (210363, 143 HP) killed through RunObservedCombatAsync with the class's first
	/// attack from range (the Mage's Flame Bolt 1282, the Artist's Pulse 4408, the Engineer's Direct Shot 2219) and with
	/// no cast refused for distance. Each kill begins with the journey's
	/// ordinary rest: the first forced rest drinks a Minor Life Potion; the second comes while the potion's 30 s delay
	/// still runs, so it sits to the HP target and drinks nothing.
	/// </summary>
	private async Task CasterLevelOneRowAsync(StarterProbe probe, string id, BotNavigationGeometry geometry, string className,
		string attackName, int attack, CancellationToken token)
	{
		const int map = 220010000, sprigg = 210363, potion = NaturalIshalgenPotionPolicy.StarterLifePotionId;
		Assert.Equal(1, probe.World.Level);
		long owned = probe.Owned(potion);
		probe.Session.BeginStep("s01", "director-places-by-sprigg-workers");
		BotPosition spot = GroundNear(geometry, map, new BotPosition(145.856f, 2560.26f, 307.891f, 0), 18);
		await probe.PlaceAsync(spot.X, spot.Y, spot.Z);
		var lines = new List<string>();
		long potionAt = 0;
		int restPotions = 0;
		for (int kill = 1; kill <= 3; kill++)
		{
			string step = "s0" + (kill + 1);
			probe.Session.BeginStep(step, $"kill-sprigg-worker-{kill}");
			// Prepared by the director: half HP before the second and the third kill, so that their rests are forced.
			if (kill > 1) await probe.CutHpAsync(50);
			long cutAt = probe.NowMillis;
			Npc target = NearestLiving(probe, sprigg);
			NaturalCombatDiagnosticResult result = await probe.Journey.RunObservedCombatAsync(_ => Task.FromResult(target.GetObjectId()), token);
			Assert.True(result.Killed, $"Sprigg Worker {kill} was not killed: {result}.");
			Assert.True(target.IsDead());
			Assert.Equal(0, result.Deaths);
			IReadOnlyList<StarterTraceRecord> records = probe.TraceOf(step);
			var casts = probe.CastsOf(step);
			IReadOnlyDictionary<string, int> decisions = probe.DecisionsOf(step);
			Assert.NotEmpty(casts);
			Assert.All(casts, cast => Assert.Equal(attack, cast.SkillId));
			// From range: the first cast is decided with the target outside melee reach.
			StarterTraceRecord first = records.First(record => record is { Direction: "action", Packet: "combat-decision" } &&
				record.Fields.GetProperty("action").GetString() == "cast-target");
			double firedFrom = first.Fields.GetProperty("targetDistance").GetDouble();
			Assert.True(firedFrom > 5, $"Kill {kill} began at {firedFrom:F1} m.");
			Assert.DoesNotContain(records, record => record is { Direction: "action", Packet: "combat-range-rejected" });
			int potions = records.Count(record => record is { Direction: "action", Packet: "rest-life-potion" });
			int sits = records.Count(record => record is { Direction: "action", Packet: "rest-sit-for-health" });
			restPotions += potions;
			if (kill == 2)
			{
				Assert.Equal(1, potions);
				potionAt = cutAt;
			}
			if (kill == 3)
			{
				Assert.True(cutAt - potionAt < 30_000, $"The third kill's rest began {cutAt - potionAt} ms after the potion; its delay has run out.");
				Assert.Equal(0, potions);
				Assert.True(sits >= 1, "The second forced rest did not sit for health.");
			}
			lines.Add($"kill {kill}: {result.ElapsedMillis} ms, first cast from {firedFrom:F1} m, {casts.Count} {attackName}, rest potions {potions} sits {sits}, " +
				$"decisions {string.Join(" ", decisions.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key} {entry.Value}"))}, " +
				$"HP {probe.World.CurrentHp}/{probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}");
		}
		// A potion drunk in a fight is the ladder's and is reported, not asked for: the two rests drink one between them.
		Assert.Equal(1, restPotions);
		Assert.False(probe.Server.IsDead());
		Console.WriteLine($"{id}: level {probe.World.Level} {className}, max HP {probe.World.MaxHp}, three Sprigg Workers. " +
			string.Join("; ", lines) + $". Life potions {owned} to {probe.Owned(potion)}.");
	}

	/// <summary>
	/// CP-46, row mage-5. Prepared by the director: level 5 and a place 20 m from a Fanged Karnif's shipped spot. Every
	/// other act is the journey's: Flame Bolt and then Blaze, at once, on one Fanged Karnif (210389, 478 HP, level 6). A
	/// death or a retreat is a recorded outcome; the row asks for the pair and for no cast that never started.
	/// </summary>
	private async Task MageLevelFiveRowAsync(StarterProbe probe, string id, BotNavigationGeometry geometry, CancellationToken token)
	{
		const int map = 220010000, karnif = 210389, bolt = 1282, blaze = 1403;
		probe.Session.BeginStep("s01", "director-sets-level-five-and-places-by-a-karnif");
		await probe.SetLevelAsync(5);
		Assert.All(new[] { bolt, blaze, 1363, 1447 }, skill => Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 5."));
		BotPosition spot = GroundNear(geometry, map, new BotPosition(884.051f, 1652f, 272.994f, 0), 20);
		await probe.PlaceAsync(spot.X, spot.Y, spot.Z);
		probe.Session.BeginStep("s02", "fight-a-fanged-karnif");
		Npc target = NearestLiving(probe, karnif);
		NaturalCombatDiagnosticResult result = await probe.Journey.RunObservedCombatAsync(_ => Task.FromResult(target.GetObjectId()), token);
		var casts = probe.CastsOf("s02").ToList();
		IReadOnlyDictionary<string, int> decisions = probe.DecisionsOf("s02");
		string order = string.Join(" ", casts.Select(cast => $"{cast.SkillId}@{cast.At.TotalSeconds:F1}"));
		int pair = Enumerable.Range(0, Math.Max(0, casts.Count - 1)).FirstOrDefault(index => casts[index].SkillId == bolt && casts[index + 1].SkillId == blaze, -1);
		Assert.True(pair >= 0, $"Blaze did not follow Flame Bolt at once: {order}.");
		TimeSpan gap = casts[pair + 1].At - casts[pair].At;
		Assert.True(gap <= TimeSpan.FromSeconds(3), $"Blaze came {gap.TotalMilliseconds:F0} ms after Flame Bolt: {order}.");
		Console.WriteLine($"{id}: level {probe.World.Level} Mage, max HP {probe.World.MaxHp}, one Fanged Karnif: {result}. " +
			$"Casts {order}; Blaze {gap.TotalMilliseconds:F0} ms after Flame Bolt. " +
			$"Decisions {string.Join(" ", decisions.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key} {entry.Value}"))}. " +
			$"HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}.");
	}

	/// <summary>
	/// CP-47, row artist-5. Prepared by the director: level 5, a place 20 m from a Fanged Karnif's shipped spot, and HP
	/// halved. Every other act is the journey's. The rest before the pull heals with Soothing Melody 4339, because the
	/// heal is now in the skill list: it drinks no potion and does not sit for health. Then one Fanged Karnif (210389,
	/// 478 HP, level 6) is fought with Song of Ice and Pulse.
	/// </summary>
	private async Task ArtistLevelFiveRowAsync(StarterProbe probe, string id, BotNavigationGeometry geometry, CancellationToken token)
	{
		const int map = 220010000, karnif = 210389, pulse = 4408, ice = 4221, melody = 4339;
		probe.Session.BeginStep("s01", "director-sets-level-five-places-by-a-karnif-and-halves-hp");
		await probe.SetLevelAsync(5);
		Assert.All(new[] { pulse, ice, melody }, skill => Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 5."));
		BotPosition spot = GroundNear(geometry, map, new BotPosition(884.051f, 1652f, 272.994f, 0), 20);
		await probe.PlaceAsync(spot.X, spot.Y, spot.Z);
		await probe.CutHpAsync(50);
		int cutHp = probe.World.CurrentHp;
		probe.Session.BeginStep("s02", "heal-and-fight-a-fanged-karnif");
		Npc target = NearestLiving(probe, karnif);
		NaturalCombatDiagnosticResult result = await probe.Journey.RunObservedCombatAsync(_ => Task.FromResult(target.GetObjectId()), token);
		IReadOnlyList<StarterTraceRecord> records = probe.TraceOf("s02");
		var casts = probe.CastsOf("s02").ToList();
		IReadOnlyDictionary<string, int> decisions = probe.DecisionsOf("s02");
		string order = string.Join(" ", casts.Select(cast => $"{cast.SkillId}@{cast.At.TotalSeconds:F1}"));
		// The rest: Soothing Melody before any attack, and neither step of the potion plan.
		StarterTraceRecord[] heals = records.Where(record => record is { Direction: "action", Packet: "between-fights-heal" }).ToArray();
		Assert.NotEmpty(heals);
		Assert.All(heals, heal => Assert.Equal(melody, heal.Fields.GetProperty("skillId").GetInt32()));
		Assert.NotEmpty(casts);
		Assert.Equal(melody, casts[0].SkillId);
		Assert.DoesNotContain(records, record => record is { Direction: "action", Packet: "rest-life-potion" });
		Assert.DoesNotContain(records, record => record is { Direction: "action", Packet: "rest-sit-for-health" });
		// The pull is decided at the HP the heal left.
		JsonElement pull = records.First(record => record is { Direction: "action", Packet: "combat-decision" }).Fields.GetProperty("observedState");
		int healedTo = pull.GetProperty("Hp").GetInt32(), maxHp = pull.GetProperty("MaxHp").GetInt32();
		Assert.True(healedTo * 100 >= maxHp * 90, $"The rest ended at {healedTo}/{maxHp} HP.");
		Assert.True(result.Killed, $"The Fanged Karnif was not killed: {result}. Casts {order}.");
		Assert.True(target.IsDead());
		Assert.Contains(casts, cast => cast.SkillId == ice);
		Assert.Contains(casts, cast => cast.SkillId == pulse);
		Console.WriteLine($"{id}: level {probe.World.Level} Artist, max HP {probe.World.MaxHp}, cut to {cutHp}, healed to {healedTo} by " +
			$"{heals.Length} Soothing Melody in the rest; one Fanged Karnif: {result}. Casts {order}. " +
			$"Decisions {string.Join(" ", decisions.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key} {entry.Value}"))}. " +
			$"HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}.");
	}

	/// <summary>
	/// CP-48, row engineer-5. Prepared by the director: level 5 and a place 18 m from a Vengeful Ghost's shipped spot.
	/// Every other act is the journey's: Gunshot and then Rapidfire twice, each inside 2 s of the step before it and with
	/// no other cast between, on one Vengeful Ghost (210593, 719 HP, level 8). A death or a retreat is a recorded outcome;
	/// a cast that never starts throws in the fight loop and fails the row.
	/// </summary>
	private async Task EngineerLevelFiveRowAsync(StarterProbe probe, string id, BotNavigationGeometry geometry, CancellationToken token)
	{
		const int map = 220010000, ghost = 210593, direct = 2219, gunshot = 1957, rapid = 2142;
		probe.Session.BeginStep("s01", "director-sets-level-five-and-places-by-a-ghost");
		await probe.SetLevelAsync(5);
		Assert.All(new[] { direct, gunshot, rapid }, skill => Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 5."));
		BotPosition spot = GroundNear(geometry, map, new BotPosition(540.176f, 1865.25f, 293.628f, 0), 18);
		await probe.PlaceAsync(spot.X, spot.Y, spot.Z);
		probe.Session.BeginStep("s02", "fight-a-vengeful-ghost");
		Npc target = NearestLiving(probe, ghost);
		NaturalCombatDiagnosticResult result = await probe.Journey.RunObservedCombatAsync(_ => Task.FromResult(target.GetObjectId()), token);
		IReadOnlyList<StarterTraceRecord> records = probe.TraceOf("s02");
		var casts = probe.CastsOf("s02").ToList();
		IReadOnlyDictionary<string, int> decisions = probe.DecisionsOf("s02");
		string order = string.Join(" ", casts.Select(cast => $"{cast.SkillId}@{cast.At.TotalSeconds:F1}"));
		int chain = Enumerable.Range(0, Math.Max(0, casts.Count - 2)).FirstOrDefault(index =>
			casts[index].SkillId == gunshot && casts[index + 1].SkillId == rapid && casts[index + 2].SkillId == rapid, -1);
		Assert.True(chain >= 0, $"Gunshot was not followed by Rapidfire twice: {order}.");
		TimeSpan first = casts[chain + 1].At - casts[chain].At, second = casts[chain + 2].At - casts[chain + 1].At;
		Assert.True(first <= TimeSpan.FromSeconds(2) && second <= TimeSpan.FromSeconds(2),
			$"Rapidfire came {first.TotalMilliseconds:F0} and {second.TotalMilliseconds:F0} ms after the step before it: {order}.");
		int refusals = records.Count(record => record is { Direction: "action", Packet: "combat-range-rejected" });
		Console.WriteLine($"{id}: level {probe.World.Level} Engineer, max HP {probe.World.MaxHp}, one Vengeful Ghost: {result}. " +
			$"Casts {order}; Rapidfire {first.TotalMilliseconds:F0} ms after Gunshot and again {second.TotalMilliseconds:F0} ms later. " +
			$"Decisions {string.Join(" ", decisions.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key} {entry.Value}"))}. " +
			$"Distance refusals {refusals}. HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}.");
	}

	/// <summary>
	/// CP-49, row scout-1. Prepared by the director: the level-1 Scout is placed 15 m from a Sprigg Worker's shipped spot,
	/// and its HP is halved before the second and before the third kill. Every other act is the journey's: three Sprigg
	/// Workers (210363, 143 HP) killed through RunObservedCombatAsync, each after a walk to the dagger's reach, with Swift
	/// Edge and dagger swings. Each kill begins with the journey's ordinary rest: the first forced rest drinks a Minor
	/// Life Potion; the second comes while the potion's 30 s delay still runs, so it sits to the HP target and drinks
	/// nothing.
	/// </summary>
	private async Task ScoutLevelOneRowAsync(StarterProbe probe, string id, BotNavigationGeometry geometry, CancellationToken token)
	{
		const int map = 220010000, sprigg = 210363, edge = 3182, potion = NaturalIshalgenPotionPolicy.StarterLifePotionId;
		Assert.Equal(1, probe.World.Level);
		long owned = probe.Owned(potion);
		probe.Session.BeginStep("s01", "director-places-by-sprigg-workers");
		BotPosition spot = GroundNear(geometry, map, new BotPosition(145.856f, 2560.26f, 307.891f, 0), 15);
		await probe.PlaceAsync(spot.X, spot.Y, spot.Z);
		var lines = new List<string>();
		int edges = 0, restPotions = 0;
		for (int kill = 1; kill <= 3; kill++)
		{
			string step = "s0" + (kill + 1);
			probe.Session.BeginStep(step, $"kill-sprigg-worker-{kill}");
			// Prepared by the director: half HP before the second and the third kill, so that their rests are forced.
			if (kill > 1) await probe.CutHpAsync(50);
			Npc target = NearestLiving(probe, sprigg);
			NaturalCombatDiagnosticResult result = await probe.Journey.RunObservedCombatAsync(_ => Task.FromResult(target.GetObjectId()), token);
			Assert.True(result.Killed, $"Sprigg Worker {kill} was not killed: {result}.");
			Assert.True(target.IsDead());
			Assert.Equal(0, result.Deaths);
			IReadOnlyList<StarterTraceRecord> records = probe.TraceOf(step);
			var casts = probe.CastsOf(step);
			IReadOnlyDictionary<string, int> decisions = probe.DecisionsOf(step);
			// Swift Edge has a 7 s cooldown, so a kill that begins inside it is made with the dagger alone.
			Assert.All(casts, cast => Assert.Equal(edge, cast.SkillId));
			edges += casts.Count;
			Assert.True(decisions.GetValueOrDefault("attack") > 0, $"Kill {kill} swung no weapon: {string.Join(", ", decisions)}.");
			Assert.True(decisions.Values.Sum() < 200, $"Kill {kill} took {decisions.Values.Sum()} decisions.");
			int potions = records.Count(record => record is { Direction: "action", Packet: "rest-life-potion" });
			StarterTraceRecord[] sits = records.Where(record => record is { Direction: "action", Packet: "rest-sit-for-health" }).ToArray();
			restPotions += potions;
			if (kill == 2) Assert.Equal(1, potions);
			if (kill == 3)
			{
				Assert.Equal(0, potions);
				Assert.NotEmpty(sits);
				// The sit says why it sat: the potion's delay was still running.
				Assert.True(sits[0].Fields.GetProperty("lifePotionReadyInMillis").GetInt64() > 0, "The second forced rest sat with the potion ready.");
			}
			lines.Add($"kill {kill}: {result.ElapsedMillis} ms, {casts.Count} Swift Edge, rest potions {potions} sits {sits.Length}, " +
				$"decisions {string.Join(" ", decisions.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key} {entry.Value}"))}, " +
				$"HP {probe.World.CurrentHp}/{probe.World.MaxHp}");
		}
		Assert.Equal(1, restPotions);
		Assert.True(edges > 0, "No Swift Edge was cast in three kills.");
		Assert.False(probe.Server.IsDead());
		Console.WriteLine($"{id}: level {probe.World.Level} Scout, max HP {probe.World.MaxHp}, three Sprigg Workers. " +
			string.Join("; ", lines) + $". Life potions {owned} to {probe.Owned(potion)}.");
	}

	/// <summary>
	/// CP-49, row scout-7. Prepared by the director: level 7 and a place 18 m from a Fanged Karnif's shipped spot. Every
	/// other act is the journey's: Swift Edge and then Soul Slash, at once and inside its 3 s, on one Fanged Karnif
	/// (210389, 478 HP, level 6). A death or a retreat is a recorded outcome; a cast that never starts throws in the fight
	/// loop and fails the row.
	/// </summary>
	private async Task ScoutLevelSevenRowAsync(StarterProbe probe, string id, BotNavigationGeometry geometry, CancellationToken token)
	{
		const int map = 220010000, karnif = 210389, slash = 3223;
		int[] edges = [3182, 3183];
		probe.Session.BeginStep("s01", "director-sets-level-seven-and-places-by-a-karnif");
		await probe.SetLevelAsync(7);
		Assert.All(new[] { 3183, slash }, skill => Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 7."));
		BotPosition spot = GroundNear(geometry, map, new BotPosition(884.051f, 1652f, 272.994f, 0), 18);
		await probe.PlaceAsync(spot.X, spot.Y, spot.Z);
		probe.Session.BeginStep("s02", "fight-a-fanged-karnif");
		Npc target = NearestLiving(probe, karnif);
		NaturalCombatDiagnosticResult result = await probe.Journey.RunObservedCombatAsync(_ => Task.FromResult(target.GetObjectId()), token);
		var casts = probe.CastsOf("s02").ToList();
		IReadOnlyDictionary<string, int> decisions = probe.DecisionsOf("s02");
		string order = string.Join(" ", casts.Select(cast => $"{cast.SkillId}@{cast.At.TotalSeconds:F1}"));
		int first = casts.FindIndex(cast => edges.Contains(cast.SkillId));
		Assert.True(first >= 0, $"No Swift Edge was cast: {order}.");
		Assert.True(first + 1 < casts.Count && casts[first + 1].SkillId == slash, $"Soul Slash did not follow Swift Edge at once: {order}.");
		TimeSpan gap = casts[first + 1].At - casts[first].At;
		Assert.True(gap <= TimeSpan.FromSeconds(3), $"Soul Slash came {gap.TotalMilliseconds:F0} ms after Swift Edge: {order}.");
		Console.WriteLine($"{id}: level {probe.World.Level} Scout, max HP {probe.World.MaxHp}, one Fanged Karnif: {result}. " +
			$"Casts {order}; Soul Slash {gap.TotalMilliseconds:F0} ms after Swift Edge. " +
			$"Decisions {string.Join(" ", decisions.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key} {entry.Value}"))}. " +
			$"HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}.");
	}

	/// <summary>
	/// CP-56a, row warrior-pack. Prepared by the director: level 9 and a place 20 m east of two Eyvindr Sailors that stand
	/// half a metre apart (210738 at 1165.3/1861.3, level 6, and 210737 beside it, level 5). Every other act is the
	/// journey's: the Warrior takes the 210738 as its target. The hazard-checked walk to it is refused, because it stands
	/// inside its neighbour's circle; the walk-in then accepts that neighbour as the target's pack and walks in. A kill, a
	/// retreat and a death are all recorded outcomes; a blocked approach throws in the fight loop and fails the row.
	/// </summary>
	private async Task WarriorPackRowAsync(StarterProbe probe, string id, BotNavigationGeometry geometry, CancellationToken token)
	{
		const int map = 220010000, sailor = 210738, mate = 210737;
		var pair = new BotPosition(1165.33f, 1861.31f, 251.419f, 0);
		probe.Session.BeginStep("s01", "director-sets-level-nine-and-places-by-two-sailors");
		await probe.SetLevelAsync(9);
		// The eastmost ground 20 m from the pair: clear of the other Eyvindr spots, the nearest of which is 17 m on.
		BotPosition spot = geometry.GroundAround(map, pair, [20]).OrderByDescending(point => point.X).ThenBy(point => point.Y).First();
		await probe.PlaceAsync(spot.X, spot.Y, spot.Z);
		probe.Session.BeginStep("s02", "walk-in-on-a-sailor-and-its-pack");
		Npc target = NearestLiving(probe, sailor);
		Npc neighbour = NearestLiving(probe, mate, target);
		Assert.True(MathF.Abs(target.GetX() - pair.X) < 3 && MathF.Abs(target.GetY() - pair.Y) < 3, $"The target stands at {target.GetX()}/{target.GetY()}.");
		// The journey walks its quest legs with aggro circles as hazards; the probe asks for the same.
		NaturalCombatDiagnosticResult result = await probe.Journey.RunObservedCombatAsync(_ => Task.FromResult(target.GetObjectId()), token,
			avoidHostileAggro: true);
		IReadOnlyList<StarterTraceRecord> records = probe.TraceOf("s02");
		IReadOnlyDictionary<string, int> decisions = probe.DecisionsOf("s02");
		StarterTraceRecord[] accepts = records.Where(record => record is { Direction: "action", Packet: "walk-in-accepts-pack" }).ToArray();
		Assert.NotEmpty(accepts);
		JsonElement first = accepts[0].Fields;
		Assert.True(first.GetProperty("accepted").GetBoolean(), "The pack was not accepted.");
		int[] pack = first.GetProperty("pack").EnumerateArray().Select(member => member.GetInt32()).ToArray();
		Assert.Equal([neighbour.GetObjectId()], pack);
		Assert.True(decisions.GetValueOrDefault("attack") + decisions.GetValueOrDefault("cast-target") > 0, "The fight was not fought.");
		Console.WriteLine($"{id}: level {probe.World.Level} Warrior, max HP {probe.World.MaxHp}, placed {MathF.Sqrt(MathF.Pow(spot.X - pair.X, 2) + MathF.Pow(spot.Y - pair.Y, 2)):F1} m from the pair. " +
			$"Walk-in accepted the pack {string.Join(",", pack)} {accepts.Length} time(s) after: {first.GetProperty("refused").GetString()} " +
			$"Result {result}. Target dead {target.IsDead()}, neighbour dead {neighbour.IsDead()}. " +
			$"Decisions {string.Join(" ", decisions.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key} {entry.Value}"))}. " +
			$"HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}.");
	}

	/// <summary>
	/// NR-04, row scout-two. Prepared by the director: level 5, at which the Scout learns the dual-wield skill 55, two
	/// daggers in the bag (Raider's 100200125 and Ulgorn's 100200604, the rewards of Q2100 and Q2002) beside the Training
	/// Dagger it holds, and a place 18 m from a Fanged Karnif's shipped spot. Every other act is the journey's: its
	/// equipment check puts the best dagger in the main hand and the next in the off hand by packets, and the fight is
	/// fought with both. A swing the server refuses for coming too early shows as a swing sent with no attack carried out.
	/// </summary>
	private async Task ScoutTwoDaggersRowAsync(StarterProbe probe, string id, BotNavigationGeometry geometry, CancellationToken token)
	{
		const int map = 220010000, karnif = 210389, training = 100200112, raiders = 100200125, ulgorns = 100200604;
		probe.Session.BeginStep("s01", "director-sets-level-five-gives-two-daggers-and-places-by-a-karnif");
		await probe.SetLevelAsync(5);
		Assert.True(probe.World.Skills.Keys.Any(NaturalGearPolicy.DualWieldSkillIds.Contains), "No dual-wield skill was learned by level 5.");
		foreach (int dagger in new[] { raiders, ulgorns })
			Assert.Equal(0, Aion.GameServer.Services.Items.ItemService.AddItem(probe.Server, dagger, 1, allowInventoryOverflow: true));
		await probe.Session.SynchronizeAsync(token);
		BotPosition spot = GroundNear(geometry, map, new BotPosition(884.051f, 1652f, 272.994f, 0), 18);
		await probe.PlaceAsync(spot.X, spot.Y, spot.Z);
		int Held(long slot) => probe.World.Inventory.Values.SingleOrDefault(item => item.Details.EquippedSlot == slot)?.ItemId ?? 0;
		Assert.Equal((training, 0), (Held(1), Held(2)));

		probe.Session.BeginStep("s02", "equipment-check");
		IReadOnlyList<NaturalGearUpgrade> worn = await probe.Journey.RunObservedEquipmentCheckAsync(token);
		string asked = string.Join(", ", worn.Select(upgrade => $"{upgrade.ItemId} to slot {upgrade.Slot}"));
		// The client's view and the server's: the better dagger in the main hand, the other in the off hand.
		Assert.Equal((ulgorns, raiders), (Held(1), Held(2)));
		Assert.Equal(ulgorns, probe.Server.GetEquipment().GetMainHandWeapon()?.GetItemId());
		Assert.Equal(raiders, probe.Server.GetEquipment().GetOffHandWeapon()?.GetItemId());
		Assert.Equal(1, probe.Owned(training));
		int serverSwingMillis = probe.Server.GetGameStats().GetAttackSpeed().GetCurrent();

		probe.Session.BeginStep("s03", "fight-a-fanged-karnif-with-two-daggers");
		Npc target = NearestLiving(probe, karnif);
		NaturalCombatDiagnosticResult result = await probe.Journey.RunObservedCombatAsync(_ => Task.FromResult(target.GetObjectId()), token);
		IReadOnlyList<StarterTraceRecord> records = probe.TraceOf("s03");
		IReadOnlyDictionary<string, int> decisions = probe.DecisionsOf("s03");
		int sent = records.Count(record => record is { Direction: ">", Packet: "CM_ATTACK" });
		int carriedOut = records.Count(record => record is { Direction: "<", Packet: "SM_ATTACK" } &&
			record.Fields.GetProperty("attackerObjId").GetInt32() == probe.Session.CharacterId);
		Assert.True(sent > 0, "The fight had no swing.");
		Assert.Equal(sent, carriedOut);
		Assert.Equal((ulgorns, raiders), (Held(1), Held(2)));
		Console.WriteLine($"{id}: level {probe.World.Level} Scout, max HP {probe.World.MaxHp}. The equipment check asked for {asked}; " +
			$"main hand {Held(1)}, off hand {Held(2)}, Training Daggers in the bag {probe.Owned(training)}; the server's swing time is {serverSwingMillis} ms. " +
			$"One Fanged Karnif: {result}. Swings sent {sent}, carried out {carriedOut}. " +
			$"Decisions {string.Join(" ", decisions.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key} {entry.Value}"))}. " +
			$"HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}.");
	}

	/// <summary>The fight decisions of one step, in order.</summary>
	private static StarterTraceRecord[] DecidedIn(IReadOnlyList<StarterTraceRecord> records) =>
		records.Where(record => record is { Direction: "action", Packet: "combat-decision" }).ToArray();

	private static bool Decided(StarterTraceRecord record, string action, params int[] skills) =>
		record.Fields.GetProperty("action").GetString() == action &&
		record.Fields.GetProperty("skillId") is { ValueKind: JsonValueKind.Number } skill && skills.Contains(skill.GetInt32());

	/// <summary>
	/// NR-14, row priest-1. Prepared by the director: the level-1 Priest is placed 18 m from a Sprigg Worker's shipped
	/// spot, and its HP is halved before the second and before the third kill. Every other act is the journey's, by the
	/// Priest's rule table: three Sprigg Workers (210363, 143 HP) killed through RunObservedCombatAsync with Smite from
	/// range as the pull. Each forced rest heals with Healing Light and drinks no potion.
	/// </summary>
	private async Task PriestLevelOneRowAsync(StarterProbe probe, string id, BotNavigationGeometry geometry, CancellationToken token)
	{
		const int map = 220010000, sprigg = 210363, smite = 4012, heal = 1838;
		Assert.Equal(1, probe.World.Level);
		probe.Session.BeginStep("s01", "director-places-by-sprigg-workers");
		BotPosition spot = GroundNear(geometry, map, new BotPosition(145.856f, 2560.26f, 307.891f, 0), 18);
		await probe.PlaceAsync(spot.X, spot.Y, spot.Z);
		var lines = new List<string>();
		for (int kill = 1; kill <= 3; kill++)
		{
			string step = "s0" + (kill + 1);
			probe.Session.BeginStep(step, $"kill-sprigg-worker-{kill}");
			// Prepared by the director: half HP before the second and the third kill, so that their rests are forced.
			if (kill > 1) await probe.CutHpAsync(50);
			Npc target = NearestLiving(probe, sprigg);
			NaturalCombatDiagnosticResult result = await probe.Journey.RunObservedCombatAsync(_ => Task.FromResult(target.GetObjectId()), token);
			Assert.True(result.Killed, $"Sprigg Worker {kill} was not killed: {result}.");
			Assert.True(target.IsDead());
			Assert.Equal(0, result.Deaths);
			IReadOnlyList<StarterTraceRecord> records = probe.TraceOf(step);
			var casts = probe.CastsOf(step);
			IReadOnlyDictionary<string, int> decisions = probe.DecisionsOf(step);
			StarterTraceRecord[] decided = DecidedIn(records);
			Assert.All(decided, record => Assert.StartsWith("natural-priest-v1:", record.Fields.GetProperty("policyVersion").GetString()));
			Assert.All(casts, cast => Assert.Contains(cast.SkillId, new[] { smite, heal }));
			// From range: the first attack is Smite, decided with the target outside melee reach.
			StarterTraceRecord first = decided.First(record => record.Fields.GetProperty("action").GetString() == "cast-target");
			Assert.Equal(smite, first.Fields.GetProperty("skillId").GetInt32());
			double firedFrom = first.Fields.GetProperty("targetDistance").GetDouble();
			Assert.True(firedFrom > 5, $"Kill {kill} began at {firedFrom:F1} m.");
			Assert.DoesNotContain(records, record => record is { Direction: "action", Packet: "combat-range-rejected" });
			StarterTraceRecord[] restHeals = records.Where(record => record is { Direction: "action", Packet: "between-fights-heal" }).ToArray();
			if (kill > 1)
			{
				Assert.NotEmpty(restHeals);
				Assert.All(restHeals, record => Assert.Equal(heal, record.Fields.GetProperty("skillId").GetInt32()));
				Assert.DoesNotContain(records, record => record is { Direction: "action", Packet: "rest-life-potion" });
				JsonElement pull = decided[0].Fields.GetProperty("observedState");
				Assert.True(pull.GetProperty("Hp").GetInt32() * 100 >= pull.GetProperty("MaxHp").GetInt32() * 90,
					$"Kill {kill} began at {pull.GetProperty("Hp").GetInt32()}/{pull.GetProperty("MaxHp").GetInt32()} HP.");
			}
			lines.Add($"kill {kill}: {result.ElapsedMillis} ms, Smite first from {firedFrom:F1} m, {casts.Count(cast => cast.SkillId == smite)} Smite, " +
				$"{restHeals.Length} Healing Light in the rest and {decided.Count(record => Decided(record, "cast-self", heal))} in the fight, " +
				$"decisions {string.Join(" ", decisions.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key} {entry.Value}"))}, " +
				$"HP {probe.World.CurrentHp}/{probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}");
		}
		Assert.False(probe.Server.IsDead());
		Console.WriteLine($"{id}: level {probe.World.Level} Priest, max HP {probe.World.MaxHp}, three Sprigg Workers. " + string.Join("; ", lines) + ".");
	}

	/// <summary>
	/// NR-14, row priest-7. Prepared by the director: level 7, a place 20 m from a Vengeful Ghost's shipped spot, and 40%
	/// HP at the moment the fight begins, so that the life potion's first tick leaves the Priest below the heal's 55%.
	/// Every other act is the journey's, by the Priest's rule table, on one Vengeful Ghost (210593, 719 HP, level 8):
	/// Healing Light in the fight, Smite from range as the pull, and with the monster on the Priest, Infernal Blaze (the
	/// stun) and Hallowed Strike (the slow). A Fanged Karnif (478 HP) died to two Smites and the Blaze before the Strike.
	/// </summary>
	private async Task PriestLevelSevenRowAsync(StarterProbe probe, string id, BotNavigationGeometry geometry, CancellationToken token)
	{
		const int map = 220010000, ghost = 210593, blaze = 1814, strike = 1614, blessing = 1684;
		int[] heals = [1838, 1839], smites = [4012, 4013];
		probe.Session.BeginStep("s01", "director-sets-level-seven-and-places-by-a-ghost");
		await probe.SetLevelAsync(7);
		Assert.All(new[] { 1839, 4013, blaze, strike, blessing }, skill => Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 7."));
		BotPosition spot = GroundNear(geometry, map, new BotPosition(540.176f, 1865.25f, 293.628f, 0), 20);
		await probe.PlaceAsync(spot.X, spot.Y, spot.Z);
		probe.Session.BeginStep("s02", "fight-a-vengeful-ghost-from-two-fifths-hp");
		Npc target = NearestLiving(probe, ghost);
		NaturalCombatDiagnosticResult result = await probe.Journey.RunObservedCombatAsync(async _ =>
		{
			// Prepared by the director, after the journey's own rest: 40% HP as the fight begins.
			await probe.CutHpAsync(40);
			return target.GetObjectId();
		}, token);
		IReadOnlyList<StarterTraceRecord> records = probe.TraceOf("s02");
		var casts = probe.CastsOf("s02").ToList();
		IReadOnlyDictionary<string, int> decisions = probe.DecisionsOf("s02");
		StarterTraceRecord[] decided = DecidedIn(records);
		string order = string.Join(" ", casts.Select(cast => $"{cast.SkillId}@{cast.At.TotalSeconds:F1}"));
		Assert.All(decided, record => Assert.StartsWith("natural-priest-v1:", record.Fields.GetProperty("policyVersion").GetString()));
		// The heal, in the fight: decided by the ladder at or below its percentage.
		StarterTraceRecord[] healed = decided.Where(record => Decided(record, "cast-self", heals)).ToArray();
		Assert.True(healed.Length > 0, $"No Healing Light was decided in the fight: {order}.");
		JsonElement firstHeal = healed[0].Fields.GetProperty("observedState");
		int healedAt = firstHeal.GetProperty("Hp").GetInt32() * 100 / firstHeal.GetProperty("MaxHp").GetInt32();
		Assert.True(healedAt <= 70, $"The first fight heal was decided at {healedAt}% HP.");
		Assert.Contains(casts, cast => heals.Contains(cast.SkillId));
		// The pull: Smite from range.
		StarterTraceRecord first = decided.First(record => record.Fields.GetProperty("action").GetString() == "cast-target");
		Assert.Contains(first.Fields.GetProperty("skillId").GetInt32(), smites);
		double firedFrom = first.Fields.GetProperty("targetDistance").GetDouble();
		Assert.True(firedFrom > 5, $"The fight began at {firedFrom:F1} m.");
		// The stun and the slow: both completed, and both decided with the monster on the Priest.
		Assert.True(casts.Any(cast => cast.SkillId == blaze), $"Infernal Blaze was not cast: {order}.");
		Assert.True(casts.Any(cast => cast.SkillId == strike), $"Hallowed Strike was not cast: {order}.");
		Assert.All(decided.Where(record => Decided(record, "cast-target", blaze, strike)),
			record => Assert.True(record.Fields.GetProperty("targetAdjacent").GetBoolean()));
		Assert.True(result.Killed, $"The Vengeful Ghost was not killed: {result}; casts {order}.");
		Assert.True(target.IsDead());
		Assert.Equal(0, result.Deaths);
		int potions = records.Count(record => record is { Direction: "action", Packet: "combat-hot-potion" });
		Console.WriteLine($"{id}: level {probe.World.Level} Priest, max HP {probe.World.MaxHp}, one Vengeful Ghost in {result.ElapsedMillis} ms. " +
			$"Casts {order}. First fight heal decided at {healedAt}% HP; Smite first from {firedFrom:F1} m; life potions in the fight {potions}. " +
			$"Decisions {string.Join(" ", decisions.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key} {entry.Value}"))}. " +
			$"HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}.");
	}

	private const int ClericProbeMap = 220030000;
	private const string ClericTable = "natural-cleric-v1:";

	/// <summary>One fight of a Cleric row, with what the trace says of it. NR-51: and of any row that fights by a rule table.</summary>
	private sealed record ClericFight(NaturalCombatDiagnosticResult Result, IReadOnlyList<StarterTraceRecord> Records, StarterTraceRecord[] Decided,
		List<(int SkillId, TimeSpan At)> Casts, IReadOnlyDictionary<string, int> Decisions)
	{
		public string Order => string.Join(" ", Casts.Select(cast => $"{cast.SkillId}@{cast.At.TotalSeconds:F1}"));
		public string Counts => string.Join(" ", Decisions.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key} {entry.Value}"));

		/// <summary>The first place where the casts follow one another in this order, or -1.</summary>
		public int Run(params int[][] steps) => Enumerable.Range(0, Math.Max(0, Casts.Count - steps.Length + 1))
			.FirstOrDefault(start => steps.Select((step, offset) => step.Contains(Casts[start + offset].SkillId)).All(found => found), -1);
	}

	/// <summary>Fights one monster through the journey's fight, in its own step, and reads the step's trace. Every decision
	/// must be the Cleric table's.</summary>
	private static Task<ClericFight> ClericFightAsync(StarterProbe probe, string step, string name, Func<Task<int>> target, CancellationToken token) =>
		TableFightAsync(probe, ClericTable, step, name, target, token);

	/// <summary>NR-51: one fight through the journey's fight, in its own step. Every decision must be the named table's.</summary>
	/// <param name="afterKill">NR-110i: run after a fight that ends in a kill, as the journey's loot sweep is.</param>
	private static async Task<ClericFight> TableFightAsync(StarterProbe probe, string table, string step, string name, Func<Task<int>> target,
		CancellationToken token, Func<CancellationToken, Task>? afterKill = null)
	{
		probe.Session.BeginStep(step, name);
		NaturalCombatDiagnosticResult result = await probe.Journey.RunObservedCombatAsync(_ => target(), token, afterKill);
		IReadOnlyList<StarterTraceRecord> records = probe.TraceOf(step);
		StarterTraceRecord[] decided = DecidedIn(records);
		Assert.NotEmpty(decided);
		Assert.All(decided, record => Assert.StartsWith(table, record.Fields.GetProperty("policyVersion").GetString()));
		return new(result, records, decided, probe.CastsOf(step).ToList(), probe.DecisionsOf(step));
	}

	/// <summary>Prepared by the director: monsters of a kind spawned 4 m from the character and set on it. They are
	/// removed again by <see cref="RemoveSetOn"/>.</summary>
	private async Task<Npc[]> SpawnSetOnAsync(StarterProbe probe, BotNavigationGeometry geometry, int template, int count, CancellationToken token)
	{
		BotPosition[] around = geometry.GroundAround(ClericProbeMap, probe.Session.CurrentPosition, [4f]).OrderBy(point => point.X).ThenBy(point => point.Y).ToArray();
		Assert.True(around.Length >= count, $"Only {around.Length} places 4 m from the character.");
		Npc[] set = Enumerable.Range(0, count).Select(index => around[count == 1 ? 0 : index * (around.Length - 1) / (count - 1)]).Select(point => Assert.IsType<Npc>(
			Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(new Aion.GameServer.Model.Templates.Spawns.SpawnTemplate(
				new Aion.GameServer.Model.Templates.Spawns.SpawnGroup(ClericProbeMap, template, 0, null), point.X, point.Y, point.Z, 0, 0, null, 0),
				probe.Server.GetInstanceId()), exactMatch: false)).ToArray();
		foreach (Npc member in set) member.GetAggroList().AddHate(probe.Server, 1);
		await probe.Session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
		await probe.Session.SynchronizeAsync(token);
		return set;
	}

	private void RemoveSetOn(IEnumerable<Npc> set)
	{
		foreach (Npc member in set.Where(member => member.IsSpawned())) fixture.World.Despawn(member);
	}

	/// <summary>
	/// NR-16, row cleric-10. Prepared by the director: the Priest is made a level-10 Cleric with the skills of every level
	/// up to it and is placed in Altgard at a pull spot of the recorded leg, by the ice crasaurs (210415, level 11). The
	/// first fight is the journey's alone, by the Cleric's rule table: Smite and then Flashbolt at once, inside its 3 s.
	/// Before the second begins the director spawns one more ice crasaur 4 m away and sets it on the Cleric, so that the
	/// Cleric is being hit whether or not the first crasaur reached it: Light of Rejuvenation goes up while it is hit,
	/// and is not cast again while it lasts.
	/// </summary>
	private async Task ClericLevelTenRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int crasaur = 210415, flashbolt = 4025, rejuvenation = 3939;
		int[] smites = [4012, 4013];
		probe.Session.BeginStep("s01", "director-makes-a-level-ten-cleric-in-altgard");
		await probe.BecomeAsync(PlayerClass.CLERIC, 10);
		Assert.All(new[] { flashbolt, rejuvenation, 4061, 4083, 4127, 3922 }, skill => Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 10."));
		BotNavigationGeometry geometry = await probe.MoveToMapAsync(ClericProbeMap, 1473.2f, 1765.2f, 247.5f);
		Npc target = NearestLiving(probe, crasaur);
		ClericFight fight = await ClericFightAsync(probe, "s02", "fight-an-ice-crasaur", () => Task.FromResult(target.GetObjectId()), token);
		int pair = fight.Run(smites, [flashbolt]);
		Assert.True(pair >= 0, $"Flashbolt did not follow Smite at once: {fight.Order}.");
		TimeSpan gap = fight.Casts[pair + 1].At - fight.Casts[pair].At;
		Assert.True(gap <= TimeSpan.FromSeconds(3), $"Flashbolt came {gap.TotalMilliseconds:F0} ms after Smite: {fight.Order}.");
		Assert.True(fight.Result.Killed, $"The ice crasaur was not killed: {fight.Result}; casts {fight.Order}.");
		Assert.Equal(0, fight.Result.Deaths);

		Npc[] set = [];
		ClericFight hit = await ClericFightAsync(probe, "s03", "fight-an-ice-crasaur-that-is-on-the-cleric", async () =>
		{
			// Prepared by the director, after the journey's own rest: one ice crasaur 4 m away, set on the Cleric.
			set = await SpawnSetOnAsync(probe, geometry, crasaur, 1, token);
			return set[0].GetObjectId();
		}, token);
		RemoveSetOn(set);
		// It lasts 30 s, so the second fight casts it only when the first did not, or when it has run out: in the two
		// fights together it goes up at least once, each time under attack and never while it is seen on the Cleric.
		StarterTraceRecord[] kept = fight.Decided.Concat(hit.Decided).Where(record => Decided(record, "cast-self", rejuvenation)).ToArray();
		Assert.True(kept.Length > 0, $"Light of Rejuvenation was not cast: {fight.Order}, then {hit.Order}; decisions {fight.Counts}, then {hit.Counts}.");
		Assert.All(kept, record => Assert.True(record.Fields.GetProperty("observedState").GetProperty("Aggro").GetBoolean()));
		Assert.All(kept, record => Assert.False(record.Fields.GetProperty("observedState").GetProperty("HasRejuvenation").GetBoolean()));
		Assert.Contains(fight.Casts.Concat(hit.Casts), cast => cast.SkillId == rejuvenation);
		Assert.Contains(hit.Decided, record => record.Fields.GetProperty("observedState").GetProperty("Aggro").GetBoolean());
		Assert.Equal(0, hit.Result.Deaths);
		Console.WriteLine($"{id}: level {probe.World.Level} Cleric, max HP {probe.World.MaxHp}. First ice crasaur: {fight.Result}; casts {fight.Order}; " +
			$"Flashbolt {gap.TotalMilliseconds:F0} ms after Smite; decisions {fight.Counts}. Second, spawned 4 m away and set on the Cleric: {hit.Result}; " +
			$"casts {hit.Order}; decisions {hit.Counts}. Light of Rejuvenation decided {kept.Length} time(s) in the two fights, under attack and not yet on the Cleric each time. " +
			$"HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}.");
	}

	/// <summary>
	/// NR-16, row cleric-16. Prepared by the director: a level-16 Cleric at a pull spot of the recorded leg, by the tusked
	/// mosbears (210437, level 14). Every other act is the journey's: the Holy Servant is summoned on a target that is
	/// above 50% HP, and Smite and Flashbolt go together. A kill, a retreat and a death are recorded outcomes: the spot
	/// brings two more monsters, and with three on it the Cleric leaves.
	/// </summary>
	private async Task ClericLevelSixteenRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int mosbear = 210437, servant = 4106;
		int[] smites = [4014, 4015], flashbolts = [4025, 4026];
		probe.Session.BeginStep("s01", "director-makes-a-level-sixteen-cleric-in-altgard");
		await probe.BecomeAsync(PlayerClass.CLERIC, 16);
		Assert.All(new[] { servant, 4015, 4026, 1841 }, skill => Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 16."));
		await probe.MoveToMapAsync(ClericProbeMap, 1427.2f, 789.9f, 249.9f);
		Npc target = NearestLiving(probe, mosbear);
		ClericFight fight = await ClericFightAsync(probe, "s02", "fight-a-tusked-mosbear", () => Task.FromResult(target.GetObjectId()), token);
		StarterTraceRecord[] summoned = fight.Decided.Where(record => Decided(record, "cast-target", servant)).ToArray();
		Assert.True(summoned.Length > 0, $"The Holy Servant was not summoned: {fight.Order}; decisions {fight.Counts}.");
		int?[] targetHp = summoned.Select(record => record.Fields.GetProperty("observedState").GetProperty("TargetHpPercent") is { ValueKind: JsonValueKind.Number } hp
			? hp.GetInt32() : (int?)null).ToArray();
		Assert.All(targetHp, hp => Assert.True(hp is null or > 50, $"The servant was decided with the target at {hp}% HP."));
		Assert.Contains(fight.Casts, cast => cast.SkillId == servant);
		int pair = fight.Run(smites, flashbolts);
		Assert.True(pair >= 0, $"Flashbolt did not follow Smite at once: {fight.Order}.");
		Console.WriteLine($"{id}: level {probe.World.Level} Cleric, max HP {probe.World.MaxHp}, one tusked mosbear: {fight.Result}. Casts {fight.Order}; " +
			$"the servant decided with the target at {string.Join(", ", targetHp.Select(hp => hp?.ToString() ?? "unseen"))}% HP. " +
			$"Decisions {fight.Counts}. HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}.");
	}

	/// <summary>
	/// NR-16, row cleric-20. Prepared by the director: a level-20 Cleric at a pull spot of the recorded leg, by the starved
	/// mosbears (210564, level 13). Before the first fight begins the director gives 2,000 DP and cuts HP to 30%; before
	/// the second, with the DP spent, it cuts HP to 50%. Every other act is the journey's, by the ladder: Salvation in the
	/// emergency, and Healing Grace, or Healing Light after a cancelled Grace, at or below the heal percentage.
	/// </summary>
	private async Task ClericLevelTwentyRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int mosbear = 210564, salvation = 3922, grace = 4203;
		int[] heals = [1840, 1841];
		probe.Session.BeginStep("s01", "director-makes-a-level-twenty-cleric-in-altgard");
		await probe.BecomeAsync(PlayerClass.CLERIC, 20);
		Assert.All(new[] { salvation, grace, 1841, 4027 }, skill => Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 20."));
		await probe.MoveToMapAsync(ClericProbeMap, 1867.3f, 456.0f, 270.2f);
		Npc first = NearestLiving(probe, mosbear);
		ClericFight emergency = await ClericFightAsync(probe, "s02", "fight-a-starved-mosbear-from-three-tenths-hp-with-dp", async () =>
		{
			// Prepared by the director, after the journey's own rest: 2,000 DP and 30% HP as the fight begins.
			await probe.SetDpAsync(2000);
			await probe.CutHpAsync(30);
			return first.GetObjectId();
		}, token);
		StarterTraceRecord[] saved = emergency.Decided.Where(record => Decided(record, "cast-self", salvation)).ToArray();
		Assert.True(saved.Length > 0, $"Salvation was not cast: {emergency.Order}; decisions {emergency.Counts}.");
		JsonElement at = saved[0].Fields.GetProperty("observedState");
		Assert.True(at.GetProperty("InEmergency").GetBoolean(), "Salvation was decided outside an emergency.");
		Assert.True(at.GetProperty("Dp").GetInt32() >= 2000, $"Salvation was decided with {at.GetProperty("Dp").GetInt32()} DP.");
		Assert.Contains(emergency.Casts, cast => cast.SkillId == salvation);
		int savedAt = at.GetProperty("Hp").GetInt32() * 100 / at.GetProperty("MaxHp").GetInt32();
		Assert.Equal(0, emergency.Result.Deaths);
		Assert.True(probe.World.CurrentDp < 2000, $"The DP was not spent: {probe.World.CurrentDp}.");

		Npc second = NearestLiving(probe, mosbear);
		ClericFight hurt = await ClericFightAsync(probe, "s03", "fight-a-starved-mosbear-from-half-hp", async () =>
		{
			// Prepared by the director, after the journey's own rest: half HP as the fight begins. The DP is spent.
			await probe.CutHpAsync(50);
			return second.GetObjectId();
		}, token);
		StarterTraceRecord[] healed = hurt.Decided.Where(record => Decided(record, "cast-self", [grace, .. heals])).ToArray();
		Assert.True(healed.Length > 0, $"Neither Healing Grace nor Healing Light was decided: {hurt.Order}; decisions {hurt.Counts}.");
		Assert.DoesNotContain(hurt.Decided, record => Decided(record, "cast-self", salvation));
		JsonElement healState = healed[0].Fields.GetProperty("observedState");
		int healedAt = healState.GetProperty("Hp").GetInt32() * 100 / healState.GetProperty("MaxHp").GetInt32();
		Assert.True(healedAt <= 70, $"The first heal of the second fight was decided at {healedAt}% HP.");
		Assert.Equal(0, hurt.Result.Deaths);
		Console.WriteLine($"{id}: level {probe.World.Level} Cleric, max HP {probe.World.MaxHp}. First starved mosbear: {emergency.Result}; Salvation decided at {savedAt}% HP " +
			$"in an emergency with {at.GetProperty("Dp").GetInt32()} DP; casts {emergency.Order}; decisions {emergency.Counts}. " +
			$"Second: {hurt.Result}; skill {healed[0].Fields.GetProperty("skillId").GetInt32()} decided at {healedAt}% HP; casts {hurt.Order}; decisions {hurt.Counts}. " +
			$"HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}, DP {probe.World.CurrentDp}.");
	}

	/// <summary>
	/// NR-16, row cleric-25. Prepared by the director: a level-25 Cleric at a pull spot of the recorded leg, by the starved
	/// mosbears (210564, level 13). The first fights are the journey's alone, one mosbear after another until Divine Spark
	/// follows Flashbolt: the server opens that step one time in ten (Java Skill.java 629-640 rolls the template's
	/// chain_skill_prob, 10 for Flashbolt), so a single fight seldom shows it. Then, before one more fight begins, the
	/// director spawns three more starved mosbears 4 m from the Cleric and sets them on it; one of them is the journey's
	/// target. With three attackers the journey casts Root on its target and leaves.
	/// </summary>
	private async Task ClericLevelTwentyFiveRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int mosbear = 210564, flashbolt = 4028, spark = 4037, root = 4127, mostFights = 60;
		int[] smites = [4016];
		probe.Session.BeginStep("s01", "director-makes-a-level-twenty-five-cleric-in-altgard");
		await probe.BecomeAsync(PlayerClass.CLERIC, 25);
		Assert.All(new[] { 4016, flashbolt, spark, root, 3951, 4204 }, skill => Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 25."));
		BotNavigationGeometry geometry = await probe.MoveToMapAsync(ClericProbeMap, 1867.3f, 456.0f, 270.2f);
		ClericFight? chain = null;
		int fights = 0, kills = 0, flashbolts = 0, pairs = 0;
		while (chain == null && fights < mostFights)
		{
			fights++;
			Npc next = NearestLiving(probe, mosbear);
			ClericFight fight = await ClericFightAsync(probe, $"s02-{fights:D2}", $"fight-starved-mosbear-{fights}", () => Task.FromResult(next.GetObjectId()), token);
			Assert.Equal(0, fight.Result.Deaths);
			kills += fight.Result.Killed ? 1 : 0;
			flashbolts += fight.Casts.Count(cast => cast.SkillId == flashbolt);
			pairs += fight.Run(smites, [flashbolt]) >= 0 ? 1 : 0;
			// Divine Spark is cast whenever Flashbolt opened it, and at no other time.
			Assert.All(Enumerable.Range(0, fight.Casts.Count).Where(index => fight.Casts[index].SkillId == spark),
				index => Assert.True(index > 0 && fight.Casts[index - 1].SkillId == flashbolt, $"Divine Spark did not follow Flashbolt: {fight.Order}."));
			if (fight.Run(smites, [flashbolt], [spark]) >= 0) chain = fight;
		}
		Assert.True(chain != null, $"Divine Spark never followed Flashbolt in {fights} fights with {flashbolts} Flashbolts.");
		int run = chain.Run(smites, [flashbolt], [spark]);
		TimeSpan second = chain.Casts[run + 1].At - chain.Casts[run].At, third = chain.Casts[run + 2].At - chain.Casts[run + 1].At;
		Assert.True(second <= TimeSpan.FromSeconds(3) && third <= TimeSpan.FromSeconds(3),
			$"Flashbolt came {second.TotalMilliseconds:F0} ms after Smite and Divine Spark {third.TotalMilliseconds:F0} ms after Flashbolt: {chain.Order}.");

		Npc[] pack = [];
		ClericFight swarm = await ClericFightAsync(probe, "s03", "fight-a-pack-of-three-starved-mosbears", async () =>
		{
			// Prepared by the director, after the journey's own rest: three starved mosbears 4 m away, set on the Cleric.
			pack = await SpawnSetOnAsync(probe, geometry, mosbear, 3, token);
			return pack[0].GetObjectId();
		}, token);
		RemoveSetOn(pack);
		int rooted = Array.FindIndex(swarm.Decided, record => Decided(record, "cast-target", root));
		Assert.True(rooted >= 0, $"Root was not cast: {swarm.Order}; decisions {swarm.Counts}.");
		string? reason = swarm.Decided[rooted].Fields.GetProperty("reason").GetString();
		Assert.StartsWith("Hold the target before retreating", reason);
		Assert.Contains(swarm.Decided.Skip(rooted + 1), record => record.Fields.GetProperty("action").GetString() == "retreat");
		Assert.Contains(swarm.Casts, cast => cast.SkillId == root);
		Console.WriteLine($"{id}: level {probe.World.Level} Cleric, max HP {probe.World.MaxHp}. {fights} fights with starved mosbears, {kills} kills: Flashbolt followed Smite at once in {pairs}, " +
			$"was cast {flashbolts} times and opened Divine Spark in fight {fights}: casts {chain.Order}; Flashbolt {second.TotalMilliseconds:F0} ms after Smite, " +
			$"Divine Spark {third.TotalMilliseconds:F0} ms after Flashbolt; decisions {chain.Counts}. " +
			$"Then one more, against {pack.Length} starved mosbears the director spawned 4 m away and set on the Cleric: " +
			$"{swarm.Result}; Root decided with {swarm.Decided[rooted].Fields.GetProperty("observedState").GetProperty("NearbyAggressors").GetInt32()} attackers: {reason} " +
			$"Casts {swarm.Order}; decisions {swarm.Counts}. HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}, dead {probe.Server.IsDead()}.");
	}

	/// <summary>NR-70a: the journey's buff check before a pull in its own step, with the skills it cast.</summary>
	private static async Task<(IReadOnlyList<StarterTraceRecord> Records, int[] Casts)> BuffCheckStepAsync(StarterProbe probe, string step, string name,
		CancellationToken token)
	{
		probe.Session.BeginStep(step, name);
		await probe.Journey.RunObservedBuffCheckAsync(token);
		await probe.Session.SynchronizeAsync(token);
		return (probe.TraceOf(step), probe.CastsOf(step).Select(cast => cast.SkillId).ToArray());
	}

	/// <summary>
	/// NR-70a, row chanter-mantras. Prepared by the director: the Priest is made a level-22 Chanter with the skills of
	/// every level up to it and is placed in Altgard. The journey's buff check before a pull turns on the three mantras its
	/// profile keeps, and the server says so of each. A second check casts none. Then the director ends every effect, as a
	/// death does, and the next check turns the three on again. Last the director makes it level 23, where Shield Mantra's
	/// next rank is learned: the check casts that rank, and the server ends the older one.
	/// </summary>
	private async Task ChanterMantrasRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int celerity = 1809, shieldOne = 1657, shieldTwo = 1658, shieldThree = 1659, revival = 1746, wind = 1648;
		int[] mantras = [celerity, shieldOne, shieldTwo, shieldThree, revival, wind];
		static string Said(IReadOnlyList<StarterTraceRecord> records) => string.Join(" ", records
			.Where(record => record is { Direction: "<", Packet: "SM_SKILL_ACTIVATION" })
			.Select(record => $"{record.Fields.GetProperty("skillId").GetInt32()}:{(record.Fields.GetProperty("active").GetBoolean() ? "on" : "off")}"));
		string On() => string.Join(" ", probe.World.ActiveToggles.Order());
		void AssertOnAtTheServer(params int[] skills) => Assert.All(mantras, skill =>
			Assert.Equal(skills.Contains(skill), probe.Server.GetEffectController().FindBySkillId(skill) != null));

		probe.Session.BeginStep("s01", "director-makes-a-level-twenty-two-chanter-in-altgard");
		await probe.BecomeAsync(PlayerClass.CHANTER, 22);
		Assert.All(new[] { celerity, shieldOne, shieldTwo, revival }, skill => Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 22."));
		await probe.MoveToMapAsync(ClericProbeMap, 1473.2f, 1765.2f, 247.5f);
		Assert.Empty(probe.World.ActiveToggles);
		float? speedBefore = probe.World.MovementSpeed;

		(IReadOnlyList<StarterTraceRecord> first, int[] firstCasts) = await BuffCheckStepAsync(probe, "s02", "buff-check-turns-the-mantras-on", token);
		Assert.Equal([shieldTwo, revival, celerity], probe.World.ActiveToggles.Order());
		AssertOnAtTheServer(celerity, shieldTwo, revival);
		Assert.Equal([celerity, shieldTwo, revival], firstCasts.Where(mantras.Contains));

		(IReadOnlyList<StarterTraceRecord> second, int[] secondCasts) = await BuffCheckStepAsync(probe, "s03", "buff-check-again-casts-no-mantra", token);
		Assert.DoesNotContain(secondCasts, mantras.Contains);
		Assert.Equal("", Said(second));
		Assert.Equal([shieldTwo, revival, celerity], probe.World.ActiveToggles.Order());
		float? speedOn = probe.World.MovementSpeed;
		Assert.True(speedOn > speedBefore, $"Celerity Mantra did not raise the speed the client moves at: {speedBefore} before, {speedOn} with it.");

		// Prepared by the director: every effect is ended, which is what the server does at a death.
		probe.Session.BeginStep("s04", "director-ends-every-effect-as-a-death-does");
		probe.Server.GetEffectController().RemoveAllEffects();
		await probe.Session.SynchronizeAsync(token);
		IReadOnlyList<StarterTraceRecord> ended = probe.TraceOf("s04");
		Assert.Empty(probe.World.ActiveToggles);
		AssertOnAtTheServer();

		(IReadOnlyList<StarterTraceRecord> third, int[] thirdCasts) = await BuffCheckStepAsync(probe, "s05", "buff-check-turns-the-mantras-on-again", token);
		Assert.Equal([shieldTwo, revival, celerity], probe.World.ActiveToggles.Order());
		AssertOnAtTheServer(celerity, shieldTwo, revival);
		Assert.Equal([celerity, shieldTwo, revival], thirdCasts.Where(mantras.Contains));

		// Prepared by the director: one level more, where the next rank of Shield Mantra is learned.
		probe.Session.BeginStep("s06", "director-makes-it-level-twenty-three");
		await probe.SetLevelAsync(23);
		Assert.True(probe.World.Skills.ContainsKey(shieldThree), "Shield Mantra's third rank was not learned at level 23.");
		(IReadOnlyList<StarterTraceRecord> fourth, int[] fourthCasts) = await BuffCheckStepAsync(probe, "s07", "buff-check-casts-the-new-rank", token);
		Assert.Equal([shieldThree, revival, celerity], probe.World.ActiveToggles.Order());
		AssertOnAtTheServer(celerity, shieldThree, revival);
		Assert.Equal([shieldThree], fourthCasts.Where(mantras.Contains));
		Console.WriteLine($"{id}: level {probe.World.Level} Chanter, max HP {probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}. " +
			$"First buff check: casts {string.Join(" ", firstCasts)}; the server said {Said(first)}; speed {speedBefore} before, {speedOn} with Celerity Mantra. " +
			$"Second check: casts {string.Join(" ", secondCasts)}; the server said nothing of a toggle. " +
			$"Every effect ended by the director: the server said {Said(ended)}. Third check: casts {string.Join(" ", thirdCasts)}; the server said {Said(third)}. " +
			$"At level 23: casts {string.Join(" ", fourthCasts)}; the server said {Said(fourth)}. On at the end: {On()}.");
	}

	/// <summary>
	/// NR-110a, row spirit-master-spirit. Prepared by the director: the Mage is made a level-10 Spirit Master with the
	/// skills of every level up to it and is placed in Altgard. The journey's buff check summons the Fire Spirit, the one
	/// it has at level 10, and the server says so. A second check summons nothing. The director makes it level 16, where
	/// the Earth Spirit is learned: the check still summons nothing, for a spirit is out. Then the director releases the
	/// spirit, as the server does when one dies; once the summon's 5 s are over, the next check summons the Earth Spirit,
	/// the best it has.
	/// </summary>
	private async Task SpiritMasterSpiritRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int fire = 3707, earth = 3645, fireNpc = 833344, earthNpc = 833288;
		int[] summons = [3707, 3709, 3711, 3713, 3645, 3647, 3649, 3685, 3687, 3689, 3665, 3667];
		static string Said(IReadOnlyList<StarterTraceRecord> records) => string.Join(" ", records
			.Where(record => record is { Direction: "<", Packet: "SM_SUMMON_PANEL" or "SM_SUMMON_PANEL_REMOVE" or "SM_SUMMON_OWNER_REMOVE" })
			.Select(record => record.Packet));
		int? Seen() => probe.World.Summon is { } spirit ? probe.World.Objects.GetValueOrDefault(spirit.ObjectId)?.TemplateId : null;
		string Out() => probe.World.Summon is { } spirit ? $"{Seen()} with {spirit.CurrentHp}/{spirit.MaxHp} HP" : "none";

		probe.Session.BeginStep("s01", "director-makes-a-level-ten-spirit-master-in-altgard");
		await probe.BecomeAsync(PlayerClass.SPIRIT_MASTER, 10);
		Assert.True(probe.World.Skills.ContainsKey(fire), "Summon: Fire Spirit was not learned by level 10.");
		await probe.MoveToMapAsync(ClericProbeMap, 1473.2f, 1765.2f, 247.5f);
		Assert.Null(probe.World.Summon);
		Assert.Null(probe.Server.GetSummon());

		(IReadOnlyList<StarterTraceRecord> first, int[] firstCasts) = await BuffCheckStepAsync(probe, "s02", "buff-check-summons-the-fire-spirit", token);
		await probe.Session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
		await probe.Session.SynchronizeAsync(token);
		Assert.Equal([fire], firstCasts.Where(summons.Contains));
		Assert.Equal(fireNpc, probe.Server.GetSummon()?.GetNpcId());
		Assert.Equal(probe.Server.GetSummon()!.GetObjectId(), probe.World.Summon?.ObjectId);
		Assert.Equal(fireNpc, Seen());
		string fireOut = Out();

		(IReadOnlyList<StarterTraceRecord> second, int[] secondCasts) = await BuffCheckStepAsync(probe, "s03", "buff-check-again-summons-nothing", token);
		Assert.DoesNotContain(secondCasts, summons.Contains);
		Assert.Equal("", Said(second));

		// Prepared by the director: level 16, where the Earth Spirit is learned.
		probe.Session.BeginStep("s04", "director-makes-it-level-sixteen");
		await probe.SetLevelAsync(16);
		Assert.True(probe.World.Skills.ContainsKey(earth), "Summon: Earth Spirit was not learned at level 16.");
		(_, int[] thirdCasts) = await BuffCheckStepAsync(probe, "s05", "buff-check-with-a-spirit-out-summons-nothing", token);
		Assert.DoesNotContain(thirdCasts, summons.Contains);
		Assert.Equal(fireNpc, probe.Server.GetSummon()?.GetNpcId());

		// Prepared by the director: the spirit is released at once, which is what the server does when one dies.
		probe.Session.BeginStep("s06", "director-releases-the-spirit");
		Aion.GameServer.Services.Summons.SummonsService.Release(probe.Server.GetSummon(), Aion.GameServer.Model.Summons.UnsummonType.UNSPECIFIED);
		await probe.Session.SynchronizeAsync(token);
		IReadOnlyList<StarterTraceRecord> released = probe.TraceOf("s06");
		Assert.Null(probe.World.Summon);
		Assert.Null(probe.Server.GetSummon());
		await probe.Session.AdvanceAsync(TimeSpan.FromSeconds(6), token);

		(IReadOnlyList<StarterTraceRecord> fourth, int[] fourthCasts) = await BuffCheckStepAsync(probe, "s07", "buff-check-summons-the-earth-spirit", token);
		await probe.Session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
		await probe.Session.SynchronizeAsync(token);
		Assert.Equal([earth], fourthCasts.Where(summons.Contains));
		Assert.Equal(earthNpc, probe.Server.GetSummon()?.GetNpcId());
		Assert.Equal(earthNpc, Seen());
		Console.WriteLine($"{id}: level {probe.World.Level} Spirit Master, max HP {probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}. " +
			$"First buff check at level 10: casts {string.Join(" ", firstCasts)}; the server said {Said(first)}; spirit out: {fireOut}. " +
			$"Second check: casts {string.Join(" ", secondCasts)}; the server said nothing of a spirit. " +
			$"At level 16 with the Fire Spirit out: casts {string.Join(" ", thirdCasts)}. Released by the director: the server said {Said(released)}. " +
			$"After 6 s: casts {string.Join(" ", fourthCasts)}; the server said {Said(fourth)}; spirit out: {Out()}.");
	}

	/// <summary>
	/// NR-110b, row spirit-master-walk. Prepared by the director: the Mage is made a level-26 Spirit Master, which the
	/// monsters of Altgard leave alone, and is placed where the Cleric's level-16 row fights. The journey's buff check
	/// summons the Earth Spirit. Then the journey walks, by its own route, towards where the Cleric's level-25 row fights,
	/// more than 500 m away. The probe's approach ends at the first segment a monster has made unsafe, so the row asks for
	/// it again, up to twelve times; each plans a new route from where the bot stands. The same spirit is at its side at
	/// the end, by the server's own positions: the bot reported its steps as a client does, and the server moved it by
	/// nothing else.
	/// </summary>
	private async Task SpiritMasterWalkRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int earth = 3649, earthNpc = 833292;
		probe.Session.BeginStep("s01", "director-makes-a-level-twenty-six-spirit-master-in-altgard");
		await probe.BecomeAsync(PlayerClass.SPIRIT_MASTER, 26);
		Assert.True(probe.World.Skills.ContainsKey(earth), "Summon: Earth Spirit III was not learned by level 26.");
		await probe.MoveToMapAsync(ClericProbeMap, 1427.2f, 789.9f, 249.9f);

		(_, int[] buffCasts) = await BuffCheckStepAsync(probe, "s02", "buff-check-summons-the-earth-spirit", token);
		await probe.Session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
		await probe.Session.SynchronizeAsync(token);
		Assert.Contains(earth, buffCasts);
		Summon spirit = probe.Server.GetSummon() ?? throw new InvalidDataException("No spirit was summoned.");
		Assert.Equal(earthNpc, spirit.GetNpcId());
		int spiritId = spirit.GetObjectId();
		float Gap() => MathF.Sqrt(MathF.Pow(spirit.GetX() - probe.Server.GetX(), 2) + MathF.Pow(spirit.GetY() - probe.Server.GetY(), 2) +
			MathF.Pow(spirit.GetZ() - probe.Server.GetZ(), 2));
		float gapBefore = Gap();
		BotPosition from = probe.Session.CurrentPosition;
		(float X, float Y, float Z) spiritFrom = (spirit.GetX(), spirit.GetY(), spirit.GetZ());

		probe.Session.BeginStep("s03", "walk-to-the-starved-mosbears");
		bool reached = false;
		int approaches = 0;
		float farthest = 0;
		for (; approaches < 12 && !reached; approaches++)
		{
			reached = await probe.Journey.RunObservedZoneApproachAsync(new BotPosition(1867.3f, 456.0f, 270.2f, 0), [], token);
			await probe.Session.SynchronizeAsync(token);
			farthest = MathF.Max(farthest, Gap());
		}
		IReadOnlyList<StarterTraceRecord> walk = probe.TraceOf("s03");
		int ownSteps = walk.Count(record => record is { Direction: ">", Packet: "CM_MOVE" });
		int spiritSteps = walk.Count(record => record is { Direction: ">", Packet: "CM_SUMMON_MOVE" });
		BotPosition to = probe.Session.CurrentPosition;
		float straight = MathF.Sqrt(MathF.Pow(to.X - from.X, 2) + MathF.Pow(to.Y - from.Y, 2));
		float spiritStraight = MathF.Sqrt(MathF.Pow(spirit.GetX() - spiritFrom.X, 2) + MathF.Pow(spirit.GetY() - spiritFrom.Y, 2));
		Assert.True(straight >= 200, $"The walk was {straight:F0} m long after {approaches} approaches: at {to}, goal {(reached ? "reached" : "not reached")}.");
		Assert.Equal(spiritId, probe.Server.GetSummon()?.GetObjectId());
		Assert.Equal(spiritId, probe.World.Summon?.ObjectId);
		Assert.True(Gap() <= 6, $"The spirit stands {Gap():F1} m from its master at the end.");
		Assert.DoesNotContain(probe.CastsOf("s03"), cast => cast.SkillId == earth);
		Console.WriteLine($"{id}: level {probe.World.Level} Spirit Master. Buff check: casts {string.Join(" ", buffCasts)}; spirit {spirit.GetNpcId()} out, " +
			$"{gapBefore:F1} m from its master. Walked {straight:F0} m in a straight line in {approaches} approaches, goal {(reached ? "reached" : "not reached")}, " +
			$"with {ownSteps} steps of its own and {spiritSteps} of the spirit's; the spirit moved {spiritStraight:F0} m, stood at most {farthest:F1} m from its " +
			$"master between approaches and stands {Gap():F1} m from it at the end, by the server's positions. The same spirit, not summoned again.");
	}

	private const string SpiritMasterTable = "natural-spirit-master-v1:";

	/// <summary>
	/// NR-110c, row spirit-master-fight. Prepared by the director: the Mage is made a level-16 Spirit Master with the
	/// skills of every level up to it and is placed in Altgard by the starved mosbears (210564, level 13), where the
	/// Cleric's level-25 row fights. A mosbear there stands clear of every other monster, so the spirit goes first
	/// (NR-110f; the row was played at a tusked mosbear among its family before that item). The journey's buff check
	/// summons the Earth Spirit. Then the journey fights one mosbear by its table. The spirit is sent first, runs to the
	/// mosbear and strikes it; the bot holds its own first attack until it sees that hit. The mosbear strikes the spirit.
	/// At the end the spirit is called back. Then the journey walks on, 60 m or more by its own routes, and the spirit,
	/// which stood where it fought, is at its side again.
	/// </summary>
	private async Task SpiritMasterFightRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int earth = 3645, earthNpc = 833288, mosbear = 210564;
		probe.Session.BeginStep("s01", "director-makes-a-level-sixteen-spirit-master-in-altgard");
		await probe.BecomeAsync(PlayerClass.SPIRIT_MASTER, 16);
		Assert.True(probe.World.Skills.ContainsKey(earth), "Summon: Earth Spirit was not learned by level 16.");
		await probe.MoveToMapAsync(ClericProbeMap, 1867.3f, 456.0f, 270.2f);

		(_, int[] buffCasts) = await BuffCheckStepAsync(probe, "s02", "buff-check-summons-the-earth-spirit", token);
		await probe.Session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
		await probe.Session.SynchronizeAsync(token);
		Assert.Contains(earth, buffCasts);
		Summon spirit = probe.Server.GetSummon() ?? throw new InvalidDataException("No spirit was summoned.");
		Assert.Equal(earthNpc, spirit.GetNpcId());
		int spiritId = spirit.GetObjectId(), self = probe.Session.CharacterId, spiritHp = spirit.GetLifeStats().GetCurrentHp();

		Npc prey = NearestLiving(probe, mosbear);
		int preyId = prey.GetObjectId();
		ClericFight fight = await TableFightAsync(probe, SpiritMasterTable, "s03", "fight-a-starved-mosbear-with-the-spirit", () => Task.FromResult(preyId), token);
		AssertNoRefusedCastRepeats(fight);
		int Sent(string packet) => fight.Records.Count(record => record.Direction == ">" && record.Packet == packet);
		StarterTraceRecord[] attacks = fight.Records.Where(record => record is { Direction: "<", Packet: "SM_ATTACK" }).ToArray();
		int Struck(int by, int whom) => attacks.Count(record => record.Fields.GetProperty("attackerObjId").GetInt32() == by &&
			record.Fields.GetProperty("targetObjId").GetInt32() == whom);
		StarterTraceRecord[] Said(string action) => fight.Records.Where(record => record.Direction == "action" && record.Packet == action).ToArray();
		int orders = Sent("CM_SUMMON_COMMAND"), steps = Sent("CM_SUMMON_MOVE"), swings = Sent("CM_SUMMON_ATTACK");
		int spiritHits = Struck(spiritId, preyId), onSpirit = Struck(preyId, spiritId), onMaster = Struck(preyId, self);
		StarterTraceRecord sent = Assert.Single(Said("combat-spirit-sent"));
		StarterTraceRecord opening = Assert.Single(Said("combat-spirit-opening"));
		Assert.Equal(preyId, sent.Fields.GetProperty("targetObjectId").GetInt32());
		// The spirit was sent before the bot's first cast at the mosbear was decided to be made.
		TimeSpan firstCast = fight.Casts.Count > 0 ? fight.Casts[0].At : TimeSpan.MaxValue;
		Assert.True(sent.VirtualTime < firstCast, $"The spirit was sent at {sent.VirtualTime.TotalSeconds:F1} s and the first cast ended at {firstCast.TotalSeconds:F1} s.");
		Assert.True(steps > 0, "The spirit was not walked to its target.");
		Assert.True(swings > 0 && spiritHits > 0, $"The spirit swung {swings} times and the server showed {spiritHits} of its attacks.");
		Assert.True(onSpirit > 0, $"The mosbear never struck the spirit: {onMaster} attacks on its master. Opening: {opening.Fields}.");
		Assert.True(fight.Result.Killed || fight.Result.Retreats > 0, $"The fight ended with no kill and no retreat: {Outcome(fight)}.");
		if (fight.Result.Deaths == 0 && probe.Server.GetSummon() != null)
		{
			Assert.Equal(2, orders);
			Assert.Single(Said("combat-spirit-called-back"));
			Assert.Equal(Aion.GameServer.Model.Summons.SummonMode.GUARD, spirit.GetMode());
		}
		string afterFight = probe.Server.GetSummon() == null ? "gone" : $"{spirit.GetLifeStats().GetCurrentHp()} HP, mode {spirit.GetMode()}";
		string botAfterFight = $"{probe.World.CurrentHp}/{probe.World.MaxHp}";

		// The walk back: the spirit stands where it fought. The bot walks on and the spirit closes the gap at its own speed.
		float Gap() => MathF.Sqrt(MathF.Pow(spirit.GetX() - probe.Server.GetX(), 2) + MathF.Pow(spirit.GetY() - probe.Server.GetY(), 2) +
			MathF.Pow(spirit.GetZ() - probe.Server.GetZ(), 2));
		float gapAfterFight = Gap(), walked = 0;
		BotPosition from = probe.Session.CurrentPosition;
		probe.Session.BeginStep("s04", "walk-on-with-the-spirit");
		int approaches = 0;
		for (; approaches < 8 && walked < 60 && !probe.World.IsDead; approaches++)
		{
			await probe.Journey.RunObservedZoneApproachAsync(new BotPosition(1427.2f, 789.9f, 249.9f, 0), [], token);
			await probe.Session.SynchronizeAsync(token);
			walked = MathF.Sqrt(MathF.Pow(probe.Session.CurrentPosition.X - from.X, 2) + MathF.Pow(probe.Session.CurrentPosition.Y - from.Y, 2));
		}
		Assert.True(walked >= 60, $"The walk after the fight was {walked:F0} m long after {approaches} approaches.");
		Assert.Equal(spiritId, probe.Server.GetSummon()?.GetObjectId());
		Assert.Equal(spiritId, probe.World.Summon?.ObjectId);
		Assert.True(Gap() <= 6, $"The spirit stands {Gap():F1} m from its master after a walk of {walked:F0} m; it stood {gapAfterFight:F1} m away after the fight.");
		int walkSteps = probe.TraceOf("s04").Count(record => record is { Direction: ">", Packet: "CM_SUMMON_MOVE" });
		Console.WriteLine($"{id}: level {probe.World.Level} Spirit Master, max HP {probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}. " +
			$"Buff check: casts {string.Join(" ", buffCasts)}; spirit {spirit.GetNpcId()} out with {spiritHp} HP. One starved mosbear: {Outcome(fight)}. " +
			$"The spirit: sent from {sent.Fields.GetProperty("distance").GetDouble():F1} m at {sent.Fields.GetProperty("speed")} m/s; the opening ended by " +
			$"{opening.Fields.GetProperty("ended").GetString()} after {opening.Fields.GetProperty("millis").GetInt64()} ms; {orders} orders, {steps} steps, {swings} swings sent; " +
			$"the server showed {spiritHits} attacks of the spirit on the mosbear, {onSpirit} of the mosbear on the spirit and {onMaster} on its master. " +
			$"After the fight the spirit has {afterFight} and stands {gapAfterFight:F1} m from its master; the bot has {botAfterFight} HP. " +
			$"The walk on: {walked:F0} m in {approaches} approaches with {walkSteps} steps of the spirit; it stands {Gap():F1} m from its master at the end, " +
			$"by the server's positions.");
	}

	/// <summary>
	/// NR-110d, row spirit-master-orders. Prepared by the director: the Mage is made a level-22 Spirit Master with the
	/// skills of every level up to it and is placed in Altgard by the starved mosbears (210564, level 13), where the
	/// Cleric's level-25 row fights. The journey's buff check summons the Earth Spirit. Three fights by the table follow,
	/// and before each the director prepares one thing: the spirit's HP cut to 30%; the same again, while Spirit Wrath
	/// Position cools down; 2,000 DP. Every other act is the journey's. The spirit casts a skill of its own only when
	/// the bot has answered the server's SM_SUMMON_USESKILL, so each cast of the spirit shows an order carried out.
	/// NR-110i: in the second fight the spirit's swing kills the mosbear in the turn in which the bot casts, and the
	/// server refuses that cast on a dead target. Every fight ends as a kill, and the step after a kill runs each time.
	/// </summary>
	private async Task SpiritMasterOrdersRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int earth = 3647, earthNpc = 833290, mosbear = 210564;
		const int disturbance = 3837, spiritErosion = 3643, wrath = 3852, replenish = 3631, armor = 3857;
		// What the level-21 Earth Spirit casts for each order.
		const int earthDisturbance = 22198, earthErosion = 22468, earthWrath = 22513;
		probe.Session.BeginStep("s01", "director-makes-a-level-twenty-two-spirit-master-in-altgard");
		await probe.BecomeAsync(PlayerClass.SPIRIT_MASTER, 22);
		Assert.All(new[] { earth, disturbance, spiritErosion, wrath, replenish, armor },
			skill => Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 22."));
		await probe.MoveToMapAsync(ClericProbeMap, 1867.3f, 456.0f, 270.2f);

		(_, int[] buffCasts) = await BuffCheckStepAsync(probe, "s02", "buff-check-summons-the-earth-spirit", token);
		await probe.Session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
		await probe.Session.SynchronizeAsync(token);
		Assert.Contains(earth, buffCasts);
		Summon spirit = probe.Server.GetSummon() ?? throw new InvalidDataException("No spirit was summoned.");
		Assert.Equal(earthNpc, spirit.GetNpcId());
		int spiritId = spirit.GetObjectId(), self = probe.Session.CharacterId;
		Assert.Equal(100, probe.World.Summon?.HpPercent);

		// The director's cut, told to the client as the server tells every loss of HP.
		async Task CutSpiritAsync(int percent)
		{
			var stats = spirit.GetLifeStats();
			int cut = stats.GetCurrentHp() - (int)((long)stats.GetMaxHp() * percent / 100);
			if (cut > 0) stats.ReduceHp(SmAttackStatus.TYPE.REGULAR, cut, 0, SmAttackStatus.LOG.REGULAR, spirit);
			await probe.Session.SynchronizeAsync(token);
			Assert.True(probe.World.Summon?.HpPercent <= percent, $"The client sees the spirit at {probe.World.Summon?.HpPercent}% HP after a cut to {percent}%.");
		}
		int afterKills = 0;
		Task<ClericFight> FightAsync(string step, string name) =>
			TableFightAsync(probe, SpiritMasterTable, step, name, () => Task.FromResult(NearestLiving(probe, mosbear).GetObjectId()), token,
				_ => { afterKills++; return Task.CompletedTask; });
		int[] SpiritCasts(ClericFight fight) => fight.Records.Where(record => record is { Direction: "<", Packet: "SM_CASTSPELL_RESULT" } &&
			record.Fields.GetProperty("effectorId").GetInt32() == spiritId).Select(record => record.Fields.GetProperty("skillId").GetInt32()).ToArray();
		int Count(ClericFight fight, string direction, string packet) => fight.Records.Count(record => record.Direction == direction && record.Packet == packet);
		string Told(ClericFight fight) => $"{Count(fight, "<", "SM_SUMMON_USESKILL")} asked, {Count(fight, ">", "CM_SUMMON_CASTSPELL")} answered, " +
			$"the spirit cast {string.Join(" ", SpiritCasts(fight))}";
		void EveryOrderAnswered(ClericFight fight)
		{
			AssertNoRefusedCastRepeats(fight);
			Assert.Equal(0, fight.Result.Deaths);
			// NR-110i: a kill is the bot's own whoever of the two struck last.
			Assert.True(fight.Result.Killed, $"The fight did not end as a kill: {Outcome(fight)}.");
			Assert.Equal(0, Count(fight, "action", "combat-target-taken"));
			Assert.Equal(Count(fight, "<", "SM_SUMMON_USESKILL"), Count(fight, ">", "CM_SUMMON_CASTSPELL"));
			Assert.Equal(Count(fight, "<", "SM_SUMMON_USESKILL"), fight.Casts.Count(cast => cast.SkillId is disturbance or spiritErosion or wrath));
		}

		// The first fight: the spirit is hurt, and the order it carries out on itself heals it.
		await CutSpiritAsync(30);
		int hpBefore = spirit.GetLifeStats().GetCurrentHp();
		ClericFight first = await FightAsync("s03", "fight-with-the-spirit-at-thirty-percent");
		EveryOrderAnswered(first);
		Assert.Contains(first.Casts, cast => cast.SkillId == wrath);
		Assert.Contains(earthWrath, SpiritCasts(first));
		// The order's heal is waited for: the heal that is paid with the bot's own HP does not follow it at once.
		Assert.DoesNotContain(first.Casts, cast => cast.SkillId == replenish);
		Assert.DoesNotContain(first.Casts, cast => cast.SkillId == armor);
		int hpAfterWrath = spirit.GetLifeStats().GetCurrentHp();
		Assert.True(hpAfterWrath > hpBefore, $"The spirit had {hpBefore} HP before the fight and {hpAfterWrath} after it.");
		string firstLine = $"{Outcome(first)}; {Told(first)}; the spirit's HP {hpBefore} to {hpAfterWrath} of {spirit.GetLifeStats().GetMaxHp()}";

		// The second fight: Spirit Wrath Position cools down, and the heal paid with the bot's own HP is cast.
		await CutSpiritAsync(30);
		hpBefore = spirit.GetLifeStats().GetCurrentHp();
		ClericFight second = await FightAsync("s04", "fight-with-the-spirit-at-thirty-percent-again");
		EveryOrderAnswered(second);
		Assert.DoesNotContain(second.Casts, cast => cast.SkillId == wrath);
		Assert.Contains(second.Casts, cast => cast.SkillId == replenish);
		StarterTraceRecord healed = second.Records.First(record => record is { Direction: "<", Packet: "SM_CASTSPELL_RESULT" } &&
			record.Fields.GetProperty("effectorId").GetInt32() == self && record.Fields.GetProperty("skillId").GetInt32() == replenish);
		Assert.Equal(spiritId, healed.Fields.GetProperty("targetId").GetInt32());
		StarterTraceRecord paid = second.Records.First(record => record is { Direction: "<", Packet: "SmAttackStatus" } &&
			record.Fields.GetProperty("objectId").GetInt32() == self && record.Fields.GetProperty("typeId").GetInt32() == 4);
		// NR-110i: the spirit's swing killed the mosbear in the turn of the bot's last cast, which the server refused.
		StarterTraceRecord fell = Assert.Single(second.Records, record => record is { Direction: "action", Packet: "combat-target-fell-before-cast" });
		Assert.True(fell.Fields.GetProperty("experience").GetInt64() > 0, "The kill's experience had not reached the bot.");
		string secondLine = $"{Outcome(second)}; {Told(second)}; the heal cost the bot {Math.Abs(paid.Fields.GetProperty("writtenValue").GetInt32())} HP; " +
			$"the last cast, {fell.Fields.GetProperty("skillId").GetInt32()}, was refused on the mosbear the spirit had just killed, with " +
			$"{fell.Fields.GetProperty("experience").GetInt64()} experience in hand; " +
			$"the spirit's HP {hpBefore} to {spirit.GetLifeStats().GetCurrentHp()} of {spirit.GetLifeStats().GetMaxHp()}";

		// The third fight: with 2,000 DP the spirit is armed.
		await probe.SetDpAsync(2000);
		ClericFight third = await FightAsync("s05", "fight-with-two-thousand-dp");
		EveryOrderAnswered(third);
		Assert.Contains(third.Casts, cast => cast.SkillId == armor);
		StarterTraceRecord armed = third.Records.First(record => record is { Direction: "<", Packet: "SM_CASTSPELL_RESULT" } &&
			record.Fields.GetProperty("effectorId").GetInt32() == self && record.Fields.GetProperty("skillId").GetInt32() == armor);
		Assert.Equal(spiritId, armed.Fields.GetProperty("targetId").GetInt32());
		Assert.True(probe.World.CurrentDp < 2000, $"The bot has {probe.World.CurrentDp} DP after Divine Spirit Armor.");

		// Over the three fights each order was given, and the spirit cast what each asks of it.
		ClericFight[] fights = [first, second, third];
		int[] ordered = fights.SelectMany(fight => fight.Casts).Select(cast => cast.SkillId).Where(skill => skill is disturbance or spiritErosion or wrath).ToArray();
		int[] carried = fights.SelectMany(SpiritCasts).ToArray();
		Assert.Contains(disturbance, ordered);
		Assert.Contains(spiritErosion, ordered);
		Assert.Contains(earthDisturbance, carried);
		Assert.Contains(earthErosion, carried);
		Assert.Equal(spiritId, probe.Server.GetSummon()?.GetObjectId());
		Assert.Equal(3, afterKills);
		Console.WriteLine($"{id}: level {probe.World.Level} Spirit Master, max HP {probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}. " +
			$"Buff check: casts {string.Join(" ", buffCasts)}; spirit {spirit.GetNpcId()} out. Three kills, and the step after a kill ran {afterKills} times. " +
			$"Spirit at 30%: {firstLine}. Spirit at 30% again: {secondLine}. With 2,000 DP: {Outcome(third)}; {Told(third)}; DP {probe.World.CurrentDp} after. " +
			$"Orders given {string.Join(" ", ordered)}; the spirit cast {string.Join(" ", carried)}. The same spirit at the end, at {probe.World.Summon?.HpPercent}% HP by the client.");
	}

	/// <summary>
	/// NR-110e, row spirit-master-pack. Prepared by the director as row spirit-master-fight: a level-16 Spirit Master by
	/// the starved mosbears (210564) in Altgard, with the Earth Spirit out by the journey's buff check. The director then
	/// spawns one more starved mosbear 4 m from the spirit and sets it on the spirit alone. The journey fights the
	/// nearest mosbear by its table. A creature that strikes the spirit is counted among the fight's attackers although
	/// it has not struck the bot, and when the table leaves, it leaves that creature too. (NR-110f: the row was played
	/// at a tusked mosbear among its family before that item, which holds the spirit back there.)
	/// </summary>
	private async Task SpiritMasterPackRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int earth = 3645, mosbear = 210564;
		probe.Session.BeginStep("s01", "director-makes-a-level-sixteen-spirit-master-in-altgard");
		await probe.BecomeAsync(PlayerClass.SPIRIT_MASTER, 16);
		BotNavigationGeometry geometry = await probe.MoveToMapAsync(ClericProbeMap, 1867.3f, 456.0f, 270.2f);

		(_, int[] buffCasts) = await BuffCheckStepAsync(probe, "s02", "buff-check-summons-the-earth-spirit", token);
		await probe.Session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
		await probe.Session.SynchronizeAsync(token);
		Assert.Contains(earth, buffCasts);
		Summon spirit = probe.Server.GetSummon() ?? throw new InvalidDataException("No spirit was summoned.");
		int spiritId = spirit.GetObjectId(), self = probe.Session.CharacterId;

		int preyId = NearestLiving(probe, mosbear).GetObjectId();
		// The director's second monster: spawned 4 m from the spirit, on the side away from the bot, and set on the spirit.
		BotPosition here = probe.Session.CurrentPosition;
		BotPosition beside = geometry.GroundAround(ClericProbeMap, new BotPosition(spirit.GetX(), spirit.GetY(), spirit.GetZ(), 0), [4f])
			.OrderByDescending(point => MathF.Pow(point.X - here.X, 2) + MathF.Pow(point.Y - here.Y, 2)).First();
		Npc second = Assert.IsType<Npc>(Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(new Aion.GameServer.Model.Templates.Spawns.SpawnTemplate(
			new Aion.GameServer.Model.Templates.Spawns.SpawnGroup(ClericProbeMap, mosbear, 0, null), beside.X, beside.Y, beside.Z, 0, 0, null, 0),
			probe.Server.GetInstanceId()), exactMatch: false);
		second.GetAggroList().AddHate(spirit, 1);
		await probe.Session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
		await probe.Session.SynchronizeAsync(token);
		ClericFight fight = await TableFightAsync(probe, SpiritMasterTable, "s03", "fight-a-mosbear-with-another-on-the-spirit", () => Task.FromResult(preyId), token);
		AssertNoRefusedCastRepeats(fight);
		// Who struck whom, and when, by the server's SM_ATTACK.
		(TimeSpan At, int By, int Whom)[] strikes = fight.Records.Where(record => record is { Direction: "<", Packet: "SM_ATTACK" })
			.Select(record => (record.VirtualTime, record.Fields.GetProperty("attackerObjId").GetInt32(), record.Fields.GetProperty("targetObjId").GetInt32()))
			.Where(strike => strike.Item3 == self || strike.Item3 == spiritId).ToArray();
		int[] onBot = strikes.Where(strike => strike.Whom == self).Select(strike => strike.By).Distinct().ToArray();
		int[] onSpiritOnly = strikes.Where(strike => strike.Whom == spiritId).Select(strike => strike.By).Distinct().Except(onBot).ToArray();
		Assert.NotEmpty(onSpiritOnly);
		// A decision that counts more attackers than had struck the bot by then counted one that struck the spirit.
		StarterTraceRecord counted = fight.Decided.FirstOrDefault(record => record.Fields.GetProperty("observedAttackers").GetInt32() >
			strikes.Where(strike => strike.Whom == self && strike.At <= record.VirtualTime).Select(strike => strike.By).Distinct().Count())
			?? throw new InvalidDataException($"No decision counted a creature that struck the spirit alone: {Outcome(fight)}.");
		int most = fight.Decided.Max(record => record.Fields.GetProperty("observedAttackers").GetInt32());
		Assert.True(fight.Result.Killed || fight.Result.Retreats > 0, $"The fight ended with no kill and no retreat: {Outcome(fight)}.");
		string left = "no retreat";
		if (fight.Result.Retreats > 0)
		{
			StarterTraceRecord route = fight.Records.First(record => record is { Direction: "action", Packet: "combat-retreat-route" });
			int[] fled = route.Fields.GetProperty("observedAttackers").EnumerateArray().Select(value => value.GetInt32()).ToArray();
			Assert.Contains(fled, onSpiritOnly.Contains);
			left = $"it left {fled.Length} creature(s), {fled.Count(onSpiritOnly.Contains)} of them on the spirit alone";
		}
		string spiritEnd = probe.Server.GetSummon() == null ? "the spirit is gone"
			: $"the spirit has {spirit.GetLifeStats().GetCurrentHp()} HP, mode {spirit.GetMode()}, " +
				$"{MathF.Sqrt(MathF.Pow(spirit.GetX() - probe.Server.GetX(), 2) + MathF.Pow(spirit.GetY() - probe.Server.GetY(), 2)):F1} m from its master";
		RemoveSetOn([second]);
		Console.WriteLine($"{id}: level {probe.World.Level} Spirit Master, max HP {probe.World.MaxHp}. One starved mosbear, with another set on the spirit: {Outcome(fight)}. " +
			$"Struck the bot: {onBot.Length} creature(s); struck the spirit and never the bot: {onSpiritOnly.Length}. The first decision that counted one " +
			$"of those: at {counted.VirtualTime.TotalSeconds:F1} s, {counted.Fields.GetProperty("observedAttackers").GetInt32()} attackers, action " +
			$"{counted.Fields.GetProperty("action").GetString()}; the most counted: {most}. Retreats {fight.Result.Retreats}: {left}. " +
			$"At the end {spiritEnd}; the bot has {probe.World.CurrentHp}/{probe.World.MaxHp} HP.");
	}

	/// <summary>
	/// NR-110f, row spirit-master-place. Prepared by the director: the Mage is made a level-16 Spirit Master and is placed
	/// in Altgard by the starved mosbears (210564, level 13), where the Cleric's level-25 row fights. They stand alone
	/// there, and their tribe helps no one. So before the second fight the director spawns one more starved mosbear 5 m
	/// beside the next one, on the side away from the bot: a neighbour whose circle holds whoever stands at that
	/// mosbear, and that does not come when the mosbear is hit from afar. The journey's buff check summons the Earth
	/// Spirit. Two fights by the table follow. At the mosbear that stands clear the spirit goes first. At the mosbear
	/// with the neighbour the bot pulls, and the spirit is sent once the mosbear is within 8 m of its master.
	/// </summary>
	private async Task SpiritMasterPlaceRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int earth = 3645, mosbear = 210564;
		probe.Session.BeginStep("s01", "director-makes-a-level-sixteen-spirit-master-in-altgard");
		await probe.BecomeAsync(PlayerClass.SPIRIT_MASTER, 16);
		BotNavigationGeometry geometry = await probe.MoveToMapAsync(ClericProbeMap, 1867.3f, 456.0f, 270.2f);

		(_, int[] buffCasts) = await BuffCheckStepAsync(probe, "s02", "buff-check-summons-the-earth-spirit", token);
		await probe.Session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
		await probe.Session.SynchronizeAsync(token);
		Assert.Contains(earth, buffCasts);
		Summon spirit = probe.Server.GetSummon() ?? throw new InvalidDataException("No spirit was summoned.");
		int spiritId = spirit.GetObjectId(), self = probe.Session.CharacterId;
		StarterTraceRecord[] Said(ClericFight fight, string action) =>
			fight.Records.Where(record => record.Direction == "action" && record.Packet == action).ToArray();
		int[] Strikers(ClericFight fight, int whom) => fight.Records.Where(record => record is { Direction: "<", Packet: "SM_ATTACK" } &&
			record.Fields.GetProperty("targetObjId").GetInt32() == whom).Select(record => record.Fields.GetProperty("attackerObjId").GetInt32()).Distinct().ToArray();
		int Hits(ClericFight fight, int by, int whom) => fight.Records.Count(record => record is { Direction: "<", Packet: "SM_ATTACK" } &&
			record.Fields.GetProperty("attackerObjId").GetInt32() == by && record.Fields.GetProperty("targetObjId").GetInt32() == whom);

		// The first fight: nothing joins a fight at this mosbear's place, and the spirit goes first.
		int clearId = NearestLiving(probe, mosbear).GetObjectId();
		Assert.Empty(probe.Journey.ObservedPackOf(clearId));
		ClericFight first = await TableFightAsync(probe, SpiritMasterTable, "s03", "fight-a-mosbear-that-stands-clear", () => Task.FromResult(clearId), token);
		AssertNoRefusedCastRepeats(first);
		Assert.True(first.Result.Killed, $"The first fight did not end as a kill: {Outcome(first)}.");
		Assert.Empty(Said(first, "combat-spirit-held-back"));
		StarterTraceRecord wentFirst = Assert.Single(Said(first, "combat-spirit-sent"));
		Assert.Single(Said(first, "combat-spirit-opening"));
		Assert.True(first.Casts.Count > 0 && wentFirst.VirtualTime < first.Casts[0].At, "The bot cast before its spirit was sent at a target that stands clear.");
		Assert.All(Strikers(first, spiritId), striker => Assert.Equal(clearId, striker));
		Assert.All(Strikers(first, self), striker => Assert.Equal(clearId, striker));

		// The director's neighbour, 5 m beside the next mosbear on the side away from the bot.
		probe.Session.BeginStep("s04", "director-spawns-a-neighbour-beside-the-next-mosbear");
		Npc crowded = NearestLiving(probe, mosbear);
		int crowdedId = crowded.GetObjectId();
		BotPosition here = probe.Session.CurrentPosition;
		BotPosition beside = geometry.GroundAround(ClericProbeMap, new BotPosition(crowded.GetX(), crowded.GetY(), crowded.GetZ(), 0), [5f])
			.OrderByDescending(point => MathF.Pow(point.X - here.X, 2) + MathF.Pow(point.Y - here.Y, 2)).First();
		Npc neighbour = Assert.IsType<Npc>(Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(new Aion.GameServer.Model.Templates.Spawns.SpawnTemplate(
			new Aion.GameServer.Model.Templates.Spawns.SpawnGroup(ClericProbeMap, mosbear, 0, null), beside.X, beside.Y, beside.Z, 0, 0, null, 0),
			probe.Server.GetInstanceId()), exactMatch: false);
		int neighbourId = neighbour.GetObjectId();
		await probe.Session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
		await probe.Session.SynchronizeAsync(token);
		Assert.True(probe.World.Objects.ContainsKey(neighbourId), "The client does not see the neighbour.");
		int[] pack = probe.Journey.ObservedPackOf(crowdedId).ToArray();
		Assert.Equal([neighbourId], pack);

		// The second fight: the bot pulls, and the spirit meets the mosbear near its master.
		ClericFight second = await TableFightAsync(probe, SpiritMasterTable, "s05", "fight-the-mosbear-with-a-neighbour", () => Task.FromResult(crowdedId), token);
		AssertNoRefusedCastRepeats(second);
		StarterTraceRecord held = Assert.Single(Said(second, "combat-spirit-held-back"));
		Assert.Contains(neighbourId, held.Fields.GetProperty("pack").EnumerateArray().Select(value => value.GetInt32()));
		Assert.Empty(Said(second, "combat-spirit-opening"));
		Assert.True(second.Result.Killed || second.Result.Retreats > 0, $"The second fight ended with no kill and no retreat: {Outcome(second)}.");
		// The neighbour was drawn by no one.
		Assert.Equal(0, Hits(second, neighbourId, spiritId) + Hits(second, neighbourId, self));
		Assert.False(neighbour.IsDead(), "The neighbour is dead.");
		StarterTraceRecord met = Assert.Single(Said(second, "combat-spirit-sent"));
		double metAt = met.Fields.GetProperty("targetFromMaster").GetDouble();
		Assert.True(metAt <= held.Fields.GetProperty("meetMetres").GetDouble(), $"The spirit was sent with the mosbear {metAt:F1} m from its master.");
		Assert.True(second.Casts.Count > 0 && second.Casts[0].At < met.VirtualTime, "The spirit was sent before the bot's own pull.");
		Assert.True(Hits(second, spiritId, crowdedId) > 0, "The spirit never struck the mosbear it met.");
		RemoveSetOn([neighbour]);
		Console.WriteLine($"{id}: level {probe.World.Level} Spirit Master, max HP {probe.World.MaxHp}. Buff check: casts {string.Join(" ", buffCasts)}. " +
			$"A mosbear that stands clear: {Outcome(first)}; the spirit sent from {wentFirst.Fields.GetProperty("distance").GetDouble():F1} m " +
			$"{(first.Casts[0].At - wentFirst.VirtualTime).TotalSeconds:F1} s before the bot's first cast; it struck the mosbear {Hits(first, spiritId, clearId)} times. " +
			$"A mosbear with a neighbour 5 m beside it: pack {pack.Length}; {Outcome(second)}; the spirit held back with the mosbear " +
			$"{held.Fields.GetProperty("targetDistance").GetDouble():F1} m from its master and sent {(met.VirtualTime - second.Casts[0].At).TotalSeconds:F1} s after the bot's " +
			$"first cast, with the mosbear {metAt:F1} m from its master; it struck the mosbear {Hits(second, spiritId, crowdedId)} times, and the mosbear struck the " +
			$"spirit {Hits(second, crowdedId, spiritId)} and its master {Hits(second, crowdedId, self)} times. The neighbour struck no one and lives. " +
			$"The bot has {probe.World.CurrentHp}/{probe.World.MaxHp} HP.");
	}

	private const string GunnerTable = "natural-gunner-v1:";

	/// <summary>
	/// NR-120a, row gunner-chain. Prepared by the director: the Engineer is made a level-16 Gunner with the skills of
	/// every level up to it, is given the ceremony's pistol (101800506) beside the one it was created with, and is placed
	/// in Altgard by the starved mosbears (210564, level 13), where the Cleric's level-25 row fights. The journey's
	/// equipment check takes a pistol into each hand. Then it fights one mosbear after another by its table until its
	/// first chain is cast whole: Gunshot, Rapidfire twice, Automatic Fire twice. The server takes Automatic Fire only
	/// after two Rapidfires (chain/precount 2), and the table decides it at no other time.
	/// </summary>
	private async Task GunnerChainRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int mosbear = 210564, karmic = 101800506, mostFights = 12;
		// Gunshot, Rapidfire and Automatic Fire in their ranks of level 16.
		int[] gunshot = [1959], rapid = [2144], auto = [2132];
		probe.Session.BeginStep("s01", "director-makes-a-level-sixteen-gunner-with-a-second-pistol");
		await probe.BecomeAsync(PlayerClass.GUNNER, 16);
		Assert.All(gunshot.Concat(rapid).Concat(auto), skill => Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 16."));
		Assert.True(probe.World.Skills.Keys.Any(NaturalGearPolicy.DualWieldSkillIds.Contains), "No dual-wield skill was learned by level 16.");
		await GiveAsync(probe, token, (karmic, 1));
		await probe.MoveToMapAsync(ClericProbeMap, 1867.3f, 456.0f, 270.2f);

		probe.Session.BeginStep("s02", "equipment-check-takes-a-pistol-into-each-hand");
		await probe.Journey.RunObservedEquipmentCheckAsync(token);
		(int main, int off) = (Held(probe, 1), Held(probe, 2));
		Assert.True(main == karmic && off != 0, $"The hands hold {main} and {off}.");
		Assert.Equal(off, probe.Server.GetEquipment().GetOffHandWeapon()?.GetItemId());

		ClericFight? shown = null;
		int fights = 0, kills = 0, autos = 0;
		while (shown == null && fights < mostFights)
		{
			fights++;
			Npc next = NearestLiving(probe, mosbear);
			ClericFight fight = await TableFightAsync(probe, GunnerTable, $"s03-{fights:D2}", $"fight-starved-mosbear-{fights}", () => Task.FromResult(next.GetObjectId()), token);
			Assert.Equal(0, fight.Result.Deaths);
			AssertNoRefusedCastRepeats(fight);
			kills += fight.Result.Killed ? 1 : 0;
			autos += fight.Casts.Count(cast => auto.Contains(cast.SkillId));
			// Automatic Fire is decided only when two Rapidfires were cast since the Gunshot that opened the chain.
			foreach (StarterTraceRecord decided in fight.Decided.Where(record => Decided(record, "cast-target", auto)))
			{
				var before = fight.Casts.Where(cast => cast.At <= decided.VirtualTime).ToList();
				int opened = before.FindLastIndex(cast => gunshot.Contains(cast.SkillId));
				int rapids = opened < 0 ? 0 : before.Skip(opened + 1).Count(cast => rapid.Contains(cast.SkillId));
				Assert.True(rapids >= 2, $"Automatic Fire was decided at {decided.VirtualTime.TotalSeconds:F1} s after {rapids} Rapidfire(s): {fight.Order}.");
			}
			// Every Automatic Fire that was decided was carried out: the server refused none.
			Assert.Equal(fight.Decided.Count(record => Decided(record, "cast-target", auto)), fight.Casts.Count(cast => auto.Contains(cast.SkillId)));
			if (fight.Run(gunshot, rapid, rapid, auto, auto) >= 0) shown = fight;
		}
		Assert.True(shown != null, $"The whole chain was never cast in {fights} fights with {autos} Automatic Fire(s).");
		int at = shown.Run(gunshot, rapid, rapid, auto, auto);
		TimeSpan span = shown.Casts[at + 4].At - shown.Casts[at].At;
		Console.WriteLine($"{id}: level {probe.World.Level} Gunner, max HP {probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}, pistols {main} and {off}. " +
			$"{fights} fight(s), {kills} kill(s), {autos} Automatic Fire(s), each decided after two Rapidfires and none refused. " +
			$"The fight that showed the whole chain: {Outcome(shown)}; Gunshot, Rapidfire twice and Automatic Fire twice in {span.TotalMilliseconds:F0} ms. " +
			$"HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}.");
	}

	/// <summary>
	/// NR-120b, row gunner-reload. Prepared by the director: the Engineer is made a level-10 Gunner with the skills of
	/// every level up to it, is given the ceremony's pistol (101800506) beside the one it was created with, and is placed
	/// in Altgard by the ice crasaurs (210415, level 11), where the Cleric's level-10 row fights. The journey's equipment
	/// check takes a pistol into each hand. Then it fights one crasaur after another by its table until Reload is cast
	/// and Gunshot follows it. Gunshot is ready 16 s after it was cast; Reload ends that cooldown at once, the server says
	/// so, and the table opens the chain again.
	/// </summary>
	private async Task GunnerReloadRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int crasaur = 210415, karmic = 101800506, gunshot = 1958, reload = 2053, openers = 1802, mostFights = 12;
		probe.Session.BeginStep("s01", "director-makes-a-level-ten-gunner-with-a-second-pistol");
		await probe.BecomeAsync(PlayerClass.GUNNER, 10);
		Assert.All(new[] { gunshot, reload }, skill => Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 10."));
		await GiveAsync(probe, token, (karmic, 1));
		await probe.MoveToMapAsync(ClericProbeMap, 1473.2f, 1765.2f, 247.5f);

		probe.Session.BeginStep("s02", "equipment-check-takes-a-pistol-into-each-hand");
		await probe.Journey.RunObservedEquipmentCheckAsync(token);
		(int main, int off) = (Held(probe, 1), Held(probe, 2));
		Assert.True(main == karmic && off != 0, $"The hands hold {main} and {off}.");

		ClericFight? shown = null;
		TimeSpan between = default;
		int fights = 0, kills = 0, deaths = 0, reloads = 0;
		// A cooldown runs on between fights, so the casts of every fight so far are read together.
		var casts = new List<(int SkillId, TimeSpan At)>();
		while (shown == null && fights < mostFights)
		{
			fights++;
			Npc next = NearestLiving(probe, crasaur);
			ClericFight fight = await TableFightAsync(probe, GunnerTable, $"s03-{fights:D2}", $"fight-ice-crasaur-{fights}", () => Task.FromResult(next.GetObjectId()), token);
			AssertNoRefusedCastRepeats(fight);
			kills += fight.Result.Killed ? 1 : 0;
			deaths += fight.Result.Deaths;
			int first = casts.Count;
			casts.AddRange(fight.Casts);
			reloads += fight.Casts.Count(cast => cast.SkillId == reload);
			for (int index = first; index < casts.Count; index++)
			{
				if (casts[index].SkillId != reload) continue;
				// Reload is cast only while Gunshot's cooldown runs: a Gunshot in the 12 s before it.
				int before = casts.FindLastIndex(index, cast => cast.SkillId == gunshot);
				Assert.True(before >= 0 && casts[index].At - casts[before].At < TimeSpan.FromSeconds(12),
					$"Reload was cast at {casts[index].At.TotalSeconds:F1} s with no Gunshot in the 12 s before it: " +
					string.Join(" ", casts.Select(cast => $"{cast.SkillId}@{cast.At.TotalSeconds:F1}")));
				// The server said the cooldown was over, and the fight read it.
				Assert.Contains(fight.Records, record => record is { Direction: "action", Packet: "combat-cooldown-ended" } &&
					record.VirtualTime >= casts[index].At && record.Fields.GetProperty("cooldownIds").EnumerateArray().Any(value => value.GetInt32() == openers));
				int after = casts.FindIndex(index, cast => cast.SkillId == gunshot);
				if (after < 0 || shown != null) continue;
				between = casts[after].At - casts[before].At;
				Assert.True(between < TimeSpan.FromSeconds(16), $"The second Gunshot came {between.TotalSeconds:F1} s after the first: {fight.Order}.");
				shown = fight;
			}
		}
		Assert.True(shown != null, $"Reload was never followed by Gunshot in {fights} fights with {reloads} Reload(s).");
		Console.WriteLine($"{id}: level {probe.World.Level} Gunner, max HP {probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}, pistols {main} and {off}. " +
			$"{fights} fight(s), {kills} kill(s), {deaths} death(s), {reloads} Reload(s), each with Gunshot cooling down and each answered by the server. " +
			$"The fight that showed it: {Outcome(shown)}; the second Gunshot {between.TotalSeconds:F1} s after the first, where its cooldown is 16 s. " +
			$"HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}.");
	}

	private const string RiderTable = "natural-rider-v1:";

	/// <summary>
	/// NR-130a, row rider-mech. Prepared by the director: the Engineer is made a level-16 Rider with the skills of every
	/// level up to it, is given the ceremony's cipher-blade (102100489), and is placed in Altgard by the starved mosbears
	/// (210564, level 13), where the Cleric's level-25 row fights. The journey's equipment check takes the blade and its
	/// buff check boards the mech, which the server says to everyone who sees it. Then it fights one mosbear after another
	/// by its table, from the mech, until Battery has followed Bludgeon. The director gives it the level-16 coin blade:
	/// the equipment check takes that, and the server ends the mech, for the weapon left the hand. The rest before the
	/// next fight boards again. In the last fight the director ends the mech as the fight begins: the table decides no
	/// skill that needs it and the cipher-blade does the work, until a buff check has boarded once more.
	/// </summary>
	private async Task RiderMechRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int mosbear = 210564, karmic = 102100489, rankNine = 102100606, embark = 2768, mostFights = 8;
		// The ranks of level 16: the mech's skills in the table.
		int[] bludgeon = [2691], battery = [2554], cinder = [2808], whispers = [4648], rocket = [2543], overdrive = [2795];
		int[] mech = [.. bludgeon, .. battery, .. cinder, .. whispers, .. rocket, .. overdrive];
		int self = probe.Session.CharacterId;
		string Said(IReadOnlyList<StarterTraceRecord> records) => string.Join(" ", records
			.Where(record => record is { Direction: "<", Packet: "SM_RIDE_ROBOT" } && record.Fields.GetProperty("objectId").GetInt32() == self)
			.Select(record => record.Fields.GetProperty("robotId").GetInt32().ToString()));
		int Blade() => probe.World.Inventory.Values.SingleOrDefault(item => item.Details.EquippedSlot is 1 or 3)?.ItemId ?? 0;

		probe.Session.BeginStep("s01", "director-makes-a-level-sixteen-rider-with-a-cipher-blade");
		await probe.BecomeAsync(PlayerClass.RIDER, 16);
		Assert.All(mech.Append(embark), skill => Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 16."));
		await GiveAsync(probe, token, (karmic, 1));
		await probe.MoveToMapAsync(ClericProbeMap, 1867.3f, 456.0f, 270.2f);
		Assert.Equal(0, probe.World.RobotId);

		probe.Session.BeginStep("s02", "equipment-check-takes-the-cipher-blade");
		await probe.Journey.RunObservedEquipmentCheckAsync(token);
		Assert.Equal(karmic, Blade());

		(IReadOnlyList<StarterTraceRecord> boarded, int[] boardCasts) = await BuffCheckStepAsync(probe, "s03", "buff-check-boards-the-mech", token);
		int robot = probe.World.RobotId;
		Assert.Contains(embark, boardCasts);
		Assert.True(robot != 0 && probe.Server.GetRobotId() == robot && probe.Server.IsInRobotMode(),
			$"After the buff check the client knows mech {robot} and the server {probe.Server.GetRobotId()}.");
		Assert.Contains(embark, probe.World.ActiveToggles);

		ClericFight? shown = null;
		int fights = 0, kills = 0, fromMech = 0;
		double firedFrom = 0;
		while (shown == null && fights < mostFights)
		{
			fights++;
			Npc next = NearestLiving(probe, mosbear);
			ClericFight fight = await TableFightAsync(probe, RiderTable, $"s04-{fights:D2}", $"fight-from-the-mech-{fights}", () => Task.FromResult(next.GetObjectId()), token);
			Assert.Equal(0, fight.Result.Deaths);
			AssertNoRefusedCastRepeats(fight);
			Assert.All(fight.Casts, cast => Assert.Contains(cast.SkillId, mech));
			Assert.Equal(robot, probe.World.RobotId);
			kills += fight.Result.Killed ? 1 : 0;
			fromMech += fight.Casts.Count;
			if (fights == 1)
			{
				// From range: with every cooldown clear, the first cast is Cinder Cannon, decided outside the reach of the mech's arms.
				StarterTraceRecord first = fight.Decided.First(record => record.Fields.GetProperty("action").GetString() == "cast-target");
				firedFrom = first.Fields.GetProperty("targetDistance").GetDouble();
				Assert.True(Decided(first, "cast-target", cinder) && firedFrom > 6, $"The first cast was decided at {firedFrom:F1} m: {fight.Order}.");
			}
			if (fight.Run(bludgeon, battery) >= 0) shown = fight;
		}
		Assert.True(shown != null, $"Battery never followed Bludgeon in {fights} fights with {fromMech} casts from the mech.");
		int at = shown.Run(bludgeon, battery);
		TimeSpan gap = shown.Casts[at + 1].At - shown.Casts[at].At;
		Assert.True(gap <= TimeSpan.FromMilliseconds(3500), $"Battery came {gap.TotalMilliseconds:F0} ms after Bludgeon: {shown.Order}.");

		// Prepared by the director: a better blade in the bag. The check that takes it is the journey's, and the end of the
		// mech is the server's (Java RideRobotEffect.startEffect 28-36: the weapon left the hand).
		probe.Session.BeginStep("s05", "director-gives-a-better-blade-and-the-equipment-check-takes-it");
		await GiveAsync(probe, token, (rankNine, 1));
		await probe.Journey.RunObservedEquipmentCheckAsync(token);
		await probe.Session.SynchronizeAsync(token);
		IReadOnlyList<StarterTraceRecord> swapped = probe.TraceOf("s05");
		Assert.Equal(rankNine, Blade());
		Assert.True(probe.World.RobotId == 0 && !probe.Server.IsInRobotMode(), $"After the new blade the client knows mech {probe.World.RobotId} and the server {probe.Server.GetRobotId()}.");
		Assert.DoesNotContain(embark, probe.World.ActiveToggles);
		Assert.Equal("0", Said(swapped));

		Npc second = NearestLiving(probe, mosbear);
		ClericFight again = await TableFightAsync(probe, RiderTable, "s06", "rest-boards-again-and-fight-from-the-mech", () => Task.FromResult(second.GetObjectId()), token);
		AssertNoRefusedCastRepeats(again);
		// The trace's own order: the rest's buff check boards, and the fight decides after it.
		List<StarterTraceRecord> inOrder = again.Records.ToList();
		int reboard = inOrder.FindIndex(record => record is { Direction: "action", Packet: "toggle-embark" });
		Assert.True(reboard >= 0 && inOrder[reboard].Fields.GetProperty("on").GetBoolean(), "The rest's buff check did not board the mech.");
		Assert.True(reboard < inOrder.FindIndex(record => record is { Direction: "action", Packet: "combat-decision" }), "A fight decision came before the mech was boarded.");
		Assert.Contains(again.Casts, cast => mech.Contains(cast.SkillId));
		int robotAgain = probe.World.RobotId;
		Assert.True(robotAgain != 0 && probe.Server.GetRobotId() == robotAgain, $"After the rest the client knows mech {robotAgain} and the server {probe.Server.GetRobotId()}.");

		Npc third = NearestLiving(probe, mosbear);
		ClericFight afoot = await TableFightAsync(probe, RiderTable, "s07", "director-ends-the-mech-as-the-fight-begins", async () =>
		{
			// Prepared by the director: after the rest and its buff check the mech is ended, as a death ends it.
			probe.Server.GetEffectController().RemoveEffect(embark);
			await probe.Session.SynchronizeAsync(token);
			Assert.Equal(0, probe.World.RobotId);
			return third.GetObjectId();
		}, token);
		AssertNoRefusedCastRepeats(afoot);
		// No skill that needs the mech is decided until a buff check has boarded again, which this fight has none of
		// unless it retreated and rested.
		TimeSpan boardedAt = afoot.Records.FirstOrDefault(record => record is { Direction: "action", Packet: "toggle-embark" })?.VirtualTime ?? TimeSpan.MaxValue;
		StarterTraceRecord[] onFoot = afoot.Decided.Where(record => record.VirtualTime < boardedAt).ToArray();
		Assert.NotEmpty(onFoot);
		Assert.DoesNotContain(onFoot, record => Decided(record, "cast-target", mech) || Decided(record, "cast-self", mech));
		Assert.Contains(onFoot, record => record.Fields.GetProperty("action").GetString() == "attack");
		string refusal = CandidateAtStart(afoot, cinder[0]);
		Assert.Contains("needs a mech", refusal);
		int swings = onFoot.Count(record => record.Fields.GetProperty("action").GetString() == "attack");

		(_, int[] lastCasts) = await BuffCheckStepAsync(probe, "s08", "buff-check-boards-once-more", token);
		Assert.True(probe.World.RobotId != 0 && probe.Server.IsInRobotMode(), $"At the end the client knows mech {probe.World.RobotId} and the server {probe.Server.GetRobotId()}.");
		Console.WriteLine($"{id}: level {probe.World.Level} Rider, max HP {probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}. " +
			$"Buff check: casts {string.Join(" ", boardCasts)}; the server said mech {Said(boarded)}. " +
			$"{fights} fight(s) from the mech, {kills} kill(s), {fromMech} casts, every one a skill that needs the mech; the first from {firedFrom:F1} m. " +
			$"The fight that showed the chain: {Outcome(shown)}; Battery {gap.TotalMilliseconds:F0} ms after Bludgeon. " +
			$"With blade {rankNine} taken by the equipment check the server said mech {Said(swapped)}. " +
			$"The next fight, after the rest boarded again (the server said mech {Said(again.Records)}): {Outcome(again)}. " +
			$"With the mech ended by the director (the server said mech {Said(afoot.Records)}): {Outcome(afoot)}; {swings} swing(s) decided and no skill that needs the mech; " +
			$"Cinder Cannon at its first decision: {refusal} " +
			$"Last buff check: casts {string.Join(" ", lastCasts)}; mech {probe.World.RobotId}. HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}.");
	}

	private const string AssassinTable = "natural-assassin-v1:";

	/// <summary>
	/// NR-90a, row assassin-runes. Prepared by the director: the Scout is made a level-25 Assassin with the skills of
	/// every level up to it, keeps the dagger it was created with, so that a fight lasts, and is placed in Altgard by the
	/// starved mosbears (210564, level 13), where the Cleric's row fights. The journey's buff check puts Apply Deadly
	/// Poison on its dagger. Then it fights one mosbear after another until Pain Rune is cast: the table casts it once
	/// the target is seen with three runes and at no other time, and each decision says how many it saw.
	/// </summary>
	private async Task AssassinRunesRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int mosbear = 210564, poison = 3481, pain = 3376, binding = 3406, mostFights = 40;
		// Rune Slash, Fang Strike and Rune Carve in their ranks of level 25.
		int[] carving = [3283, 3418, 3387];
		probe.Session.BeginStep("s01", "director-makes-a-level-twenty-five-assassin-in-altgard");
		await probe.BecomeAsync(PlayerClass.ASSASSIN, 25);
		Assert.All(carving.Append(pain).Append(binding).Append(poison), skill => Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 25."));
		await probe.MoveToMapAsync(ClericProbeMap, 1867.3f, 456.0f, 270.2f);
		int weapon = Held(probe, 1);

		(_, int[] buffCasts) = await BuffCheckStepAsync(probe, "s02", "buff-check-puts-the-poison-on", token);
		Assert.Contains(poison, buffCasts);

		static string Runes(StarterTraceRecord decision, string role) => decision.Fields.GetProperty("checks").EnumerateArray()
			.Where(check => check.GetProperty("Rule").GetString() == "runes-" + role)
			.Select(check => $"{check.GetProperty("Verdict").GetString()}: {check.GetProperty("Reason").GetString()}").FirstOrDefault() ?? "not asked";
		ClericFight? shown = null;
		int fights = 0, kills = 0, carved = 0;
		while (shown == null && fights < mostFights)
		{
			fights++;
			Npc next = NearestLiving(probe, mosbear);
			ClericFight fight = await TableFightAsync(probe, AssassinTable, $"s03-{fights:D2}", $"fight-starved-mosbear-{fights}", () => Task.FromResult(next.GetObjectId()), token);
			Assert.Equal(0, fight.Result.Deaths);
			AssertNoRefusedCastRepeats(fight);
			kills += fight.Result.Killed ? 1 : 0;
			carved += fight.Casts.Count(cast => carving.Contains(cast.SkillId));
			// A burst is decided only on a target seen with three runes or more.
			Assert.All(fight.Decided.Where(record => Decided(record, "cast-target", pain)), record => Assert.StartsWith("pass:", Runes(record, "pain")));
			Assert.All(fight.Decided.Where(record => Decided(record, "cast-target", binding)), record => Assert.StartsWith("pass:", Runes(record, "binding")));
			if (fight.Casts.Any(cast => cast.SkillId == pain)) shown = fight;
		}
		Assert.True(shown != null, $"Pain Rune was never cast in {fights} fights with {carved} carving casts.");
		int at = shown.Casts.FindIndex(cast => cast.SkillId == pain);
		int before = shown.Casts.Take(at).Count(cast => carving.Contains(cast.SkillId));
		Assert.True(before >= 3, $"Pain Rune was cast after {before} carving casts: {shown.Order}.");
		StarterTraceRecord decided = shown.Decided.First(record => Decided(record, "cast-target", pain));
		string[] counts = shown.Decided.Select(record => Runes(record, "pain")).Where(text => text != "not asked")
			.Select(text => text.Split("seen with ")[1][..1]).ToArray();
		Console.WriteLine($"{id}: level {probe.World.Level} Assassin, max HP {probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}, weapon {weapon}. " +
			$"Buff check: casts {string.Join(" ", buffCasts)}. {fights} fight(s), {kills} kill(s), {carved} carving cast(s). " +
			$"The fight that showed it: {Outcome(shown)}; runes seen at each decision {string.Join("", counts)}; " +
			$"Pain Rune after {before} carving casts, decided with: {Runes(decided, "pain")} HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}.");
	}

	private const string GladiatorTable = "natural-gladiator-v1:";

	/// <summary>
	/// NR-80a, row gladiator-aerial. Prepared by the director: the Warrior is made a level-25 Gladiator with the skills of
	/// every level up to it, keeps the sword it was created with, so that a fight lasts, and is placed in Altgard by the
	/// starved mosbears (210564, level 13), where the Cleric's row fights. The journey's buff check turns Slaughter on.
	/// Then it fights one mosbear after another until Crashing Blow follows Aerial Lockdown: the lift lasts 2 s, a monster
	/// may resist it, and Aerial Lockdown is ready once in 3 min. Crashing Blow is cast at no other time. A cast the
	/// server completed is one it accepted, and it accepts Crashing Blow only on a target that is in the air.
	/// </summary>
	private async Task GladiatorAerialRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int mosbear = 210564, aerial = 545, crashing = 508, slaughter = 697, mostFights = 40;
		probe.Session.BeginStep("s01", "director-makes-a-level-twenty-five-gladiator-in-altgard");
		await probe.BecomeAsync(PlayerClass.GLADIATOR, 25);
		Assert.All(new[] { aerial, crashing, slaughter, 2868, 2881, 740, 626 }, skill => Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 25."));
		await probe.MoveToMapAsync(ClericProbeMap, 1867.3f, 456.0f, 270.2f);
		int weapon = Held(probe, 1);

		(_, int[] buffCasts) = await BuffCheckStepAsync(probe, "s02", "buff-check-turns-slaughter-on", token);
		Assert.Contains(slaughter, buffCasts);
		Assert.Contains(slaughter, probe.World.ActiveToggles);

		ClericFight? shown = null;
		int fights = 0, kills = 0, lifts = 0;
		while (shown == null && fights < mostFights)
		{
			fights++;
			Npc next = NearestLiving(probe, mosbear);
			ClericFight fight = await TableFightAsync(probe, GladiatorTable, $"s03-{fights:D2}", $"fight-starved-mosbear-{fights}", () => Task.FromResult(next.GetObjectId()), token);
			Assert.Equal(0, fight.Result.Deaths);
			AssertNoRefusedCastRepeats(fight);
			kills += fight.Result.Killed ? 1 : 0;
			lifts += fight.Casts.Count(cast => cast.SkillId == aerial);
			// Crashing Blow is cast right after an Aerial Lockdown, and at no other time.
			Assert.All(Enumerable.Range(0, fight.Casts.Count).Where(index => fight.Casts[index].SkillId == crashing),
				index => Assert.True(index > 0 && fight.Casts[index - 1].SkillId == aerial, $"Crashing Blow did not follow Aerial Lockdown: {fight.Order}."));
			if (fight.Run([aerial], [crashing]) >= 0) shown = fight;
		}
		Assert.True(shown != null, $"Crashing Blow never followed Aerial Lockdown in {fights} fights with {lifts} Aerial Lockdowns.");
		int run = shown.Run([aerial], [crashing]);
		TimeSpan gap = shown.Casts[run + 1].At - shown.Casts[run].At;
		Assert.True(gap <= TimeSpan.FromSeconds(2), $"Crashing Blow came {gap.TotalMilliseconds:F0} ms after Aerial Lockdown: {shown.Order}.");
		StarterTraceRecord decided = shown.Decided.First(record => Decided(record, "cast-target", crashing));
		Assert.Contains(slaughter, probe.World.ActiveToggles);
		Console.WriteLine($"{id}: level {probe.World.Level} Gladiator, max HP {probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}, weapon {weapon}. " +
			$"Buff check: casts {string.Join(" ", buffCasts)}; toggles on {string.Join(" ", probe.World.ActiveToggles.Order())}. " +
			$"{fights} fight(s), {kills} kill(s), {lifts} Aerial Lockdown(s). The fight that showed it: {Outcome(shown)}; " +
			$"Crashing Blow {gap.TotalMilliseconds:F0} ms after Aerial Lockdown, decided because: {decided.Fields.GetProperty("reason").GetString()} " +
			$"HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}.");
	}

	private const string TemplarTable = "natural-templar-v1:";
	private const int TemplarShield = 115000024, LesserOdellaPowder = 169300003, OdellaPowder = 169300004;

	/// <summary>NR-51: prepared by the director: items put into the character's bag. The journey's equipment check wears
	/// what its gear rules choose.</summary>
	private static async Task GiveAsync(StarterProbe probe, CancellationToken token, params (int ItemId, long Count)[] items)
	{
		foreach ((int itemId, long count) in items)
			Assert.Equal(0, Aion.GameServer.Services.Items.ItemService.AddItem(probe.Server, itemId, count, allowInventoryOverflow: true));
		await probe.Session.SynchronizeAsync(token);
	}

	private static int Held(StarterProbe probe, long slot) =>
		probe.World.Inventory.Values.SingleOrDefault(item => item.Details.EquippedSlot == slot)?.ItemId ?? 0;

	/// <summary>NR-51: no skill was decided three times running, which is what a cast the server refuses looks like (a cast
	/// that is carried out starts its cooldown and is not chosen again), and the server never said a shield was missing.</summary>
	private static void AssertNoRefusedCastRepeats(ClericFight fight)
	{
		int run = 0, longest = 0, last = 0;
		foreach (StarterTraceRecord record in fight.Decided)
		{
			int skill = record.Fields.GetProperty("action").GetString() is "cast-target" or "cast-self" &&
				record.Fields.GetProperty("skillId") is { ValueKind: JsonValueKind.Number } id ? id.GetInt32() : 0;
			run = skill != 0 && skill == last ? run + 1 : skill != 0 ? 1 : 0;
			last = skill;
			longest = Math.Max(longest, run);
		}
		Assert.True(longest <= 2, $"A skill was decided {longest} times running: {fight.Order}; decisions {fight.Counts}.");
		Assert.DoesNotContain(fight.Records, record => record is { Direction: "<", Packet: "SM_SYSTEM_MESSAGE" } &&
			record.Fields.GetProperty("name").GetString() is "STR_SKILL_NEED_SHIELD" or "STR_SKILL_NEED_DUAL_WEAPON");
	}

	/// <summary>What the table said of a skill at the fight's first decision: legal, or the reasons it was refused.</summary>
	private static string CandidateAtStart(ClericFight fight, int skillId)
	{
		JsonElement candidate = fight.Decided[0].Fields.GetProperty("candidateActions").EnumerateArray()
			.Single(entry => entry.GetProperty("SkillId") is { ValueKind: JsonValueKind.Number } id && id.GetInt32() == skillId);
		return candidate.GetProperty("Legal").GetBoolean() ? "legal"
			: string.Join(" ", candidate.GetProperty("IllegalReasons").EnumerateArray().Select(reason => reason.GetString()));
	}

	/// <summary>The journey's rest in its own step, with the powder skills it cast.</summary>
	private static async Task<(IReadOnlyList<StarterTraceRecord> Records, int[] Casts)> RestStepAsync(StarterProbe probe, string step, string name,
		CancellationToken token)
	{
		probe.Session.BeginStep(step, name);
		await probe.Journey.RunObservedRestAsync(token);
		return (probe.TraceOf(step), probe.CastsOf(step).Select(cast => cast.SkillId).ToArray());
	}

	private static string Outcome(ClericFight fight) => $"{fight.Result}; casts {fight.Order}; decisions {fight.Counts}";

	/// <summary>
	/// NR-51, row templar-10. Prepared by the director: the Warrior is made a level-10 Templar with the skills of every
	/// level up to it, is given the ceremony's sword (100000640) and is placed in Altgard by the ice crasaurs (210415,
	/// level 11), where the Cleric's row fights. The first fight is fought with nothing in the off hand: Shield Bash is
	/// refused by the table and never sent. Then the director gives Raider's Shield, the journey's equipment check wears
	/// it, and the second fight casts Shield Bash. Last the director gives powder and halves the HP, and the journey's
	/// rest casts Herb Treatment. A kill and a retreat are recorded outcomes.
	/// </summary>
	private async Task TemplarLevelTenRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int crasaur = 210415, sword = 100000640, dazing = 3055, bash = 3072, strike = 2865, robust = 2878, herb = 246;
		probe.Session.BeginStep("s01", "director-makes-a-level-ten-templar-in-altgard");
		await probe.BecomeAsync(PlayerClass.TEMPLAR, 10);
		Assert.All(new[] { dazing, bash, strike, robust, 2891, 2903, 3019, herb, 249 }, skill => Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 10."));
		await GiveAsync(probe, token, (sword, 1));
		await probe.MoveToMapAsync(ClericProbeMap, 1473.2f, 1765.2f, 247.5f);
		probe.Session.BeginStep("s02", "equipment-check-with-the-sword");
		await probe.Journey.RunObservedEquipmentCheckAsync(token);
		Assert.Equal((sword, 0), (Held(probe, 1), Held(probe, 2)));

		Npc first = NearestLiving(probe, crasaur);
		ClericFight bare = await TableFightAsync(probe, TemplarTable, "s03", "fight-an-ice-crasaur-with-no-shield", () => Task.FromResult(first.GetObjectId()), token);
		AssertNoRefusedCastRepeats(bare);
		Assert.DoesNotContain(bare.Casts, cast => cast.SkillId == bash);
		string refused = CandidateAtStart(bare, bash);
		Assert.Contains("needs a shield", refused);
		Assert.Contains(bare.Casts, cast => cast.SkillId == dazing);
		Assert.True(bare.Run([strike], [robust]) >= 0, $"Robust Blow did not follow Ferocious Strike at once: {bare.Order}.");
		Assert.True(bare.Result.Killed || bare.Result.Retreats > 0, $"The first fight ended neither way: {bare.Result}.");

		probe.Session.BeginStep("s04", "director-gives-a-shield-then-equipment-check");
		await GiveAsync(probe, token, (TemplarShield, 1));
		IReadOnlyList<NaturalGearUpgrade> worn = await probe.Journey.RunObservedEquipmentCheckAsync(token);
		Assert.Equal((sword, TemplarShield), (Held(probe, 1), Held(probe, 2)));
		Assert.True(probe.Server.GetEquipment().IsShieldEquipped());

		Npc second = NearestLiving(probe, crasaur);
		ClericFight shielded = await TableFightAsync(probe, TemplarTable, "s05", "fight-an-ice-crasaur-with-the-shield", () => Task.FromResult(second.GetObjectId()), token);
		AssertNoRefusedCastRepeats(shielded);
		Assert.Contains(shielded.Casts, cast => cast.SkillId == bash);
		Assert.True(shielded.Result.Killed || shielded.Result.Retreats > 0, $"The second fight ended neither way: {shielded.Result}.");

		// Prepared by the director: powder, and half HP. The rest is the journey's.
		await GiveAsync(probe, token, (LesserOdellaPowder, 20));
		await probe.CutHpAsync(50);
		int hpBefore = probe.World.CurrentHp;
		(IReadOnlyList<StarterTraceRecord> rest, int[] restCasts) = await RestStepAsync(probe, "s06", "director-gives-powder-and-halves-hp-then-rest", token);
		Assert.Contains(herb, restCasts);
		Assert.True(probe.Owned(LesserOdellaPowder) < 20, "No powder was spent.");
		Assert.True(probe.World.CurrentHp * 100 >= probe.World.MaxHp * 90, $"The rest ended at {probe.World.CurrentHp}/{probe.World.MaxHp}.");
		Console.WriteLine($"{id}: level {probe.World.Level} Templar, max HP {probe.World.MaxHp}, MP {probe.World.MaxMp}. No shield: {Outcome(bare)}; Shield Bash at the first decision: {refused} " +
			$"With the shield (the check asked for {string.Join(", ", worn.Select(upgrade => $"{upgrade.ItemId} to slot {upgrade.Slot}"))}): {Outcome(shielded)}. " +
			$"Rest from {hpBefore} HP: casts {string.Join(" ", restCasts)}, powder {probe.Owned(LesserOdellaPowder)} of 20 left, " +
			$"life potions drunk {rest.Count(record => record is { Direction: "action", Packet: "rest-life-potion" })}, sits {rest.Count(record => record is { Direction: "action", Packet: "rest-sit-for-health" })}. " +
			$"HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}.");
	}

	/// <summary>
	/// NR-51, row templar-16. Prepared by the director: a level-16 Templar with the sword of Q24013, the plate shoes and
	/// breastplate of Q24011 and Q24012 and Raider's Shield in the bag, by the tusked mosbears (210437, level 14) of the
	/// Cleric's row. The journey's equipment check wears them. Before the first fight begins the director cuts HP to 30%:
	/// by the ladder the life potion is drunk and Empyrean Armor is cast in the emergency. Then Divine Blow follows
	/// Dazing Severe Blow, and Shield Bash is cast. A kill, a retreat and a death are recorded outcomes: the spot brings
	/// two more monsters, and with three on it the Templar leaves, as the Cleric does there.
	/// </summary>
	private async Task TemplarLevelSixteenRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int mosbear = 210437, sword = 100001735, shoes = 114601575, torso = 110601622, armor = 3129, dazing = 3056, divine = 3132, bash = 3073;
		probe.Session.BeginStep("s01", "director-makes-a-level-sixteen-templar-in-altgard");
		await probe.BecomeAsync(PlayerClass.TEMPLAR, 16);
		Assert.All(new[] { armor, dazing, divine, bash, 2867, 2879, 2904 }, skill => Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 16."));
		await GiveAsync(probe, token, (sword, 1), (TemplarShield, 1), (shoes, 1), (torso, 1));
		await probe.MoveToMapAsync(ClericProbeMap, 1427.2f, 789.9f, 249.9f);
		probe.Session.BeginStep("s02", "equipment-check");
		IReadOnlyList<NaturalGearUpgrade> worn = await probe.Journey.RunObservedEquipmentCheckAsync(token);
		Assert.Equal((sword, TemplarShield), (Held(probe, 1), Held(probe, 2)));
		Assert.All(new[] { shoes, torso }, piece => Assert.Contains(probe.World.Inventory.Values, item => item.ItemId == piece && item.Details.EquippedSlot.GetValueOrDefault() != 0));

		Npc first = NearestLiving(probe, mosbear);
		ClericFight emergency = await TableFightAsync(probe, TemplarTable, "s03", "fight-a-tusked-mosbear-from-three-tenths-hp", async () =>
		{
			// Prepared by the director, after the journey's own rest: 30% HP as the fight begins.
			await probe.CutHpAsync(30);
			return first.GetObjectId();
		}, token);
		AssertNoRefusedCastRepeats(emergency);
		StarterTraceRecord[] armored = emergency.Decided.Where(record => Decided(record, "cast-self", armor)).ToArray();
		Assert.True(armored.Length > 0, $"Empyrean Armor was not cast: {emergency.Order}; decisions {emergency.Counts}.");
		JsonElement at = armored[0].Fields.GetProperty("observedState");
		Assert.True(at.GetProperty("InEmergency").GetBoolean(), "Empyrean Armor was decided outside an emergency.");
		int armoredAt = at.GetProperty("Hp").GetInt32() * 100 / at.GetProperty("MaxHp").GetInt32();
		Assert.Contains(emergency.Casts, cast => cast.SkillId == armor);

		Assert.All(armored, record => Assert.True(record.Fields.GetProperty("observedState").GetProperty("InEmergency").GetBoolean()));
		Assert.True(emergency.Run([dazing], [divine]) >= 0, $"Divine Blow did not follow Dazing Severe Blow at once: {emergency.Order}.");
		Assert.Contains(emergency.Casts, cast => cast.SkillId == bash);
		StarterTraceRecord? left = emergency.Decided.FirstOrDefault(record => record.Fields.GetProperty("action").GetString() == "retreat");
		Console.WriteLine($"{id}: level {probe.World.Level} Templar, max HP {probe.World.MaxHp}, MP {probe.World.MaxMp}; the check asked for " +
			$"{string.Join(", ", worn.Select(upgrade => $"{upgrade.ItemId} to slot {upgrade.Slot}"))}. From 30% HP: {Outcome(emergency)}; Empyrean Armor decided at {armoredAt}% HP in an emergency. " +
			$"It left: {left?.Fields.GetProperty("reason").GetString() ?? "no"} HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}, dead {probe.Server.IsDead()}.");
	}

	/// <summary>
	/// NR-51, row templar-20. Prepared by the director: a level-20 Templar with the sword of Q24016 and Raider's Shield, by
	/// the starved mosbears (210564, level 13) of the Cleric's row, with powder in the bag. The first fight is the
	/// journey's alone: Wrath Strike follows Robust Blow, which follows Ferocious Strike. Before the second begins the
	/// director gives 2,000 DP and cuts HP to 45%: Empyrean Chastisement goes first, and Rage, when the fight lasts to its
	/// place in the chain, is cast only while it is hurt. Then the director cuts MP to 10% and the journey's rest casts MP
	/// Recovery. NR-53b: every fight of the Templar's rows opens with its pull, Taunt or Aether Leash from range.
	/// </summary>
	private async Task TemplarLevelTwentyRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int mosbear = 210564, sword = 100001737, strike = 2867, robust = 2880, wrath = 3038, rage = 2905, chastise = 3021, recovery = 252;
		probe.Session.BeginStep("s01", "director-makes-a-level-twenty-templar-in-altgard");
		await probe.BecomeAsync(PlayerClass.TEMPLAR, 20);
		Assert.All(new[] { strike, robust, wrath, rage, chastise, recovery, 251, 3057, 3074 }, skill => Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 20."));
		await GiveAsync(probe, token, (sword, 1), (TemplarShield, 1), (LesserOdellaPowder, 20));
		await probe.MoveToMapAsync(ClericProbeMap, 1867.3f, 456.0f, 270.2f);
		probe.Session.BeginStep("s02", "equipment-check");
		await probe.Journey.RunObservedEquipmentCheckAsync(token);
		Assert.Equal((sword, TemplarShield), (Held(probe, 1), Held(probe, 2)));

		Npc first = NearestLiving(probe, mosbear);
		ClericFight plain = await TableFightAsync(probe, TemplarTable, "s03", "fight-a-starved-mosbear", () => Task.FromResult(first.GetObjectId()), token);
		AssertNoRefusedCastRepeats(plain);
		int chain = plain.Run([strike], [robust], [wrath]);
		Assert.True(chain >= 0, $"Wrath Strike did not follow Robust Blow and Ferocious Strike at once: {plain.Order}.");
		TimeSpan second = plain.Casts[chain + 1].At - plain.Casts[chain].At, third = plain.Casts[chain + 2].At - plain.Casts[chain + 1].At;
		Assert.True(second <= TimeSpan.FromSeconds(3) && third <= TimeSpan.FromSeconds(3), $"The chain's steps came {second.TotalMilliseconds:F0} ms and {third.TotalMilliseconds:F0} ms apart: {plain.Order}.");
		Assert.DoesNotContain(plain.Casts, cast => cast.SkillId == chastise);
		Assert.Equal(0, plain.Result.Deaths);

		Npc next = NearestLiving(probe, mosbear);
		ClericFight hurt = await TableFightAsync(probe, TemplarTable, "s04", "fight-a-starved-mosbear-from-under-half-hp-with-dp", async () =>
		{
			// Prepared by the director, after the journey's own rest: 2,000 DP and 45% HP as the fight begins.
			await probe.SetDpAsync(2000);
			await probe.CutHpAsync(45);
			return next.GetObjectId();
		}, token);
		AssertNoRefusedCastRepeats(hurt);
		int HpPercent(StarterTraceRecord record) => record.Fields.GetProperty("observedState").GetProperty("Hp").GetInt32() * 100 /
			record.Fields.GetProperty("observedState").GetProperty("MaxHp").GetInt32();
		StarterTraceRecord[] raged = hurt.Decided.Where(record => Decided(record, "cast-self", rage)).ToArray();
		StarterTraceRecord[] chastised = hurt.Decided.Where(record => Decided(record, "cast-target", chastise)).ToArray();
		Assert.All(raged, record => Assert.True(HpPercent(record) <= 80, $"Rage was decided at {HpPercent(record)}% HP."));
		Assert.All(chastised, record => Assert.True(HpPercent(record) <= 70, $"Empyrean Chastisement was decided at {HpPercent(record)}% HP."));
		Assert.True(chastised.Length > 0, $"Empyrean Chastisement was not cast while hurt with 2,000 DP: {hurt.Order}; decisions {hurt.Counts}.");
		Assert.True(probe.World.CurrentDp < 2000, $"The DP was not spent: {probe.World.CurrentDp}.");
		Assert.Equal(0, hurt.Result.Deaths);

		// Prepared by the director: a tenth of its mana. The rest is the journey's.
		probe.Session.BeginStep("s05", "director-cuts-mp");
		await probe.CutMpAsync(10);
		int mpBefore = probe.World.CurrentMp;
		(IReadOnlyList<StarterTraceRecord> rest, int[] restCasts) = await RestStepAsync(probe, "s06", "rest-from-a-tenth-of-its-mana", token);
		Assert.Contains(recovery, restCasts);
		Assert.True(probe.World.CurrentMp * 100 >= probe.World.MaxMp * 50, $"The rest ended at {probe.World.CurrentMp}/{probe.World.MaxMp} MP.");
		Console.WriteLine($"{id}: level {probe.World.Level} Templar, max HP {probe.World.MaxHp}, MP {probe.World.MaxMp}. Unhurt: {Outcome(plain)}; " +
			$"Robust Blow {second.TotalMilliseconds:F0} ms after Ferocious Strike, Wrath Strike {third.TotalMilliseconds:F0} ms after Robust Blow. " +
			$"From 45% HP with 2,000 DP: {Outcome(hurt)}; Rage decided at {string.Join(", ", raged.Select(HpPercent))}% HP, " +
			$"Empyrean Chastisement at {string.Join(", ", chastised.Select(HpPercent))}% HP; DP left {probe.World.CurrentDp}. " +
			$"Rest from {mpBefore} MP: casts {string.Join(" ", restCasts)}, powder {probe.Owned(LesserOdellaPowder)} of 20 left, " +
			$"mana sits {rest.Count(record => record is { Direction: ">", Packet: "CM_EMOTION" })}. " +
			$"HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}.");
	}

	/// <summary>
	/// NR-51, row templar-25. Prepared by the director: a level-25 Templar with the sword of Q24016 and Raider's Shield, by
	/// the starved mosbears (210564, level 13), with Odella Powder in the bag. Three fights are the journey's alone, for
	/// the numbers. Then, before one more begins, the director spawns three starved mosbears 4 m from the Templar and sets
	/// them on it: with three attackers the journey leaves. Last the director halves the HP and the rest casts the fourth
	/// rank of Herb Treatment, which spends Odella Powder.
	/// </summary>
	private async Task TemplarLevelTwentyFiveRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int mosbear = 210564, sword = 100001737, herb = 253;
		probe.Session.BeginStep("s01", "director-makes-a-level-twenty-five-templar-in-altgard");
		await probe.BecomeAsync(PlayerClass.TEMPLAR, 25);
		Assert.All(new[] { 2868, 2881, 3039, 2906, 2894, 3022, 3058, 3133, 3075, herb, 254 }, skill => Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 25."));
		await GiveAsync(probe, token, (sword, 1), (TemplarShield, 1), (OdellaPowder, 20));
		BotNavigationGeometry geometry = await probe.MoveToMapAsync(ClericProbeMap, 1867.3f, 456.0f, 270.2f);
		probe.Session.BeginStep("s02", "equipment-check");
		await probe.Journey.RunObservedEquipmentCheckAsync(token);
		Assert.Equal((sword, TemplarShield), (Held(probe, 1), Held(probe, 2)));

		var lines = new List<string>();
		for (int fights = 1; fights <= 3; fights++)
		{
			Npc next = NearestLiving(probe, mosbear);
			int hp = probe.World.CurrentHp;
			ClericFight fight = await TableFightAsync(probe, TemplarTable, $"s03-{fights}", $"fight-starved-mosbear-{fights}", () => Task.FromResult(next.GetObjectId()), token);
			AssertNoRefusedCastRepeats(fight);
			Assert.Equal(0, fight.Result.Deaths);
			Assert.True(fight.Result.Killed || fight.Result.Retreats > 0, $"Fight {fights} ended neither way: {fight.Result}.");
			lines.Add($"fight {fights}: {Outcome(fight)}, HP {hp} to {probe.World.CurrentHp}/{probe.World.MaxHp}");
		}

		Npc[] pack = [];
		ClericFight swarm = await TableFightAsync(probe, TemplarTable, "s04", "fight-a-pack-of-three-starved-mosbears", async () =>
		{
			// Prepared by the director, after the journey's own rest: three starved mosbears 4 m away, set on the Templar.
			pack = await SpawnSetOnAsync(probe, geometry, mosbear, 3, token);
			return pack[0].GetObjectId();
		}, token);
		RemoveSetOn(pack);
		int left = Array.FindIndex(swarm.Decided, record => record.Fields.GetProperty("action").GetString() == "retreat");
		Assert.True(left >= 0, $"The Templar did not leave three attackers: {swarm.Order}; decisions {swarm.Counts}.");
		int attackers = swarm.Decided[left].Fields.GetProperty("observedState").GetProperty("NearbyAggressors").GetInt32();
		Assert.True(attackers >= 3, $"It left with {attackers} attackers: {swarm.Decided[left].Fields.GetProperty("reason").GetString()}");

		// Prepared by the director: half HP. The rest is the journey's.
		probe.Session.BeginStep("s05", "director-halves-hp");
		if (!probe.Server.IsDead()) await probe.CutHpAsync(50);
		(IReadOnlyList<StarterTraceRecord> _, int[] restCasts) = await RestStepAsync(probe, "s06", "rest-from-half-hp-with-odella-powder", token);
		Assert.Contains(herb, restCasts);
		Assert.True(probe.Owned(OdellaPowder) < 20, "No Odella Powder was spent.");
		Console.WriteLine($"{id}: level {probe.World.Level} Templar, max HP {probe.World.MaxHp}, MP {probe.World.MaxMp}. " + string.Join("; ", lines) +
			$". Then against {pack.Length} starved mosbears the director spawned 4 m away and set on it: {Outcome(swarm)}; it left with {attackers} attackers: " +
			$"{swarm.Decided[left].Fields.GetProperty("reason").GetString()} Rest: casts {string.Join(" ", restCasts)}, Odella Powder {probe.Owned(OdellaPowder)} of 20 left. " +
			$"HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}, dead {probe.Server.IsDead()}.");
	}

	private const string SorcererTable = "natural-sorcerer-v1:";

	/// <summary>
	/// NR-61a, row sorcerer-blast. Prepared by the director: a level-25 Sorcerer with the spellbook of Q24016, by the
	/// starved mosbears (210564, level 13). It fights one mosbear after another by its table until Delayed Blast is cast
	/// on one that it will kill. The server then takes no skill at that mosbear, which is about to die. The fight waits
	/// for the blast to land, where it gave the target up before, and ends in the kill.
	/// </summary>
	private async Task SorcererBlastRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int mosbear = 210564, book = 100601431, blast = 1422, mostFights = 6;
		probe.Session.BeginStep("s01", "director-makes-a-level-twenty-five-sorcerer-in-altgard");
		await probe.BecomeAsync(PlayerClass.SORCERER, 25);
		Assert.True(probe.World.Skills.ContainsKey(blast), "Delayed Blast II was not learned by level 25.");
		await GiveAsync(probe, token, (book, 1));
		await probe.MoveToMapAsync(ClericProbeMap, 1867.3f, 456.0f, 270.2f);
		probe.Session.BeginStep("s02", "equipment-check");
		await probe.Journey.RunObservedEquipmentCheckAsync(token);

		ClericFight? shown = null;
		StarterTraceRecord? waited = null;
		int fights = 0;
		while (shown == null && fights < mostFights)
		{
			fights++;
			Npc next = NearestLiving(probe, mosbear);
			ClericFight fight = await TableFightAsync(probe, SorcererTable, $"s03-{fights:D2}", $"fight-starved-mosbear-{fights}", () => Task.FromResult(next.GetObjectId()), token);
			AssertNoRefusedCastRepeats(fight);
			Assert.DoesNotContain(fight.Records, record => record is { Direction: "action", Packet: "combat-target-taken" });
			Assert.True(fight.Result is { Killed: true, Deaths: 0 }, $"Fight {fights} was no kill: {Outcome(fight)}.");
			waited = fight.Records.FirstOrDefault(record => record is { Direction: "action", Packet: "combat-target-about-to-die" });
			if (waited != null) shown = fight;
		}
		Assert.True(shown != null && waited != null, $"No fight of {fights} had a cast refused for a target about to die.");
		Assert.Equal(blast, waited.Fields.GetProperty("hitSkillId").GetInt32());
		long wait = waited.Fields.GetProperty("waitMillis").GetInt64();
		Assert.InRange(wait, 1, 5100);
		// The blast was cast, a cast after it was refused, and nothing was cast at the target after the wait.
		(int SkillId, TimeSpan At) blasted = shown.Casts.Last(cast => cast.SkillId == blast);
		Assert.True(blasted.At < waited.VirtualTime, $"Delayed Blast was cast at {blasted.At.TotalSeconds:F1} s and the wait began at {waited.VirtualTime.TotalSeconds:F1} s.");
		Assert.Contains(shown.Records, record => record is { Direction: "<", Packet: "SM_SYSTEM_MESSAGE" } && record.VirtualTime == waited.VirtualTime &&
			record.Fields.GetProperty("name").GetString() == "STR_SKILL_TARGET_IS_NOT_VALID");
		Assert.DoesNotContain(shown.Casts, cast => cast.At > waited.VirtualTime);
		Console.WriteLine($"{id}: level {probe.World.Level} Sorcerer, max HP {probe.World.MaxHp}, MP {probe.World.MaxMp}. {fights} fight(s), every one a kill. " +
			$"The fight that showed it: {Outcome(shown)}. Delayed Blast was cast at {blasted.At.TotalSeconds:F1} s; at {waited.VirtualTime.TotalSeconds:F1} s the server refused skill " +
			$"{waited.Fields.GetProperty("skillId").GetInt32()} at the mosbear, and the fight waited {wait} ms for the blast. HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}.");
	}

	/// <summary>NR-61: every cast of a step is one of the skills named: the table's at the row's level, with what the
	/// rest and the buff check before the fight cast.</summary>
	private static void AssertOnlyCasts(ClericFight fight, params int[] allowed) =>
		Assert.All(fight.Casts, cast => Assert.True(allowed.Contains(cast.SkillId), $"Skill {cast.SkillId} is outside the row's table: {fight.Order}."));

	/// <summary>The item in the main hand, one-hand or two-hand.</summary>
	private static int MainHand(StarterProbe probe) =>
		probe.World.Inventory.Values.SingleOrDefault(item => item.Details.EquippedSlot is 1 or 3)?.ItemId ?? 0;

	private static int HpPercentAt(StarterTraceRecord record) => record.Fields.GetProperty("observedState").GetProperty("Hp").GetInt32() * 100 /
		record.Fields.GetProperty("observedState").GetProperty("MaxHp").GetInt32();

	/// <summary>NR-71: the swings of the weapon the fight decided.</summary>
	private static int Swings(ClericFight fight) => fight.Decisions.GetValueOrDefault("attack");

	private static int MpPercentAt(StarterTraceRecord record) => record.Fields.GetProperty("observedState").GetProperty("Mp").GetInt32() * 100 /
		record.Fields.GetProperty("observedState").GetProperty("MaxMp").GetInt32();

	/// <summary>
	/// NR-61, row sorcerer-10. Prepared by the director: the Mage is made a level-10 Sorcerer with the skills of every
	/// level up to it, is given the ceremony's spellbook (100600532) and is placed in Altgard by the ice crasaurs (210415,
	/// level 11), where the Cleric's row fights. The journey's equipment check takes the book and its buff check puts
	/// Stone Skin and Robe of Flame up. Then it fights one crasaur after another by its table until both chain pairs are
	/// seen: Frozen Shock after Ice Chain and Blaze after Flame Bolt, each inside 3 s. Last the director gives powder and
	/// halves the HP, and the journey's rest casts Herb Treatment.
	/// </summary>
	private async Task SorcererLevelTenRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int crasaur = 210415, book = 100600532, bolt = 1282, blaze = 1404, ice = 1363, shock = 1226, erosion = 1447, skin = 1155, robe = 1296,
			empyrean = 1494, root = 1328, gain = 1192, herb = 246, mostFights = 6;
		int[] table = [bolt, blaze, ice, shock, erosion, skin, robe, empyrean, root, gain, herb, 249];
		probe.Session.BeginStep("s01", "director-makes-a-level-ten-sorcerer-in-altgard");
		await probe.BecomeAsync(PlayerClass.SORCERER, 10);
		Assert.All(table, skill => Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 10."));
		await GiveAsync(probe, token, (book, 1));
		await probe.MoveToMapAsync(ClericProbeMap, 1473.2f, 1765.2f, 247.5f);
		probe.Session.BeginStep("s02", "equipment-check-takes-the-spellbook");
		await probe.Journey.RunObservedEquipmentCheckAsync(token);
		Assert.Equal(book, MainHand(probe));

		(_, int[] buffCasts) = await BuffCheckStepAsync(probe, "s03", "buff-check-puts-stone-skin-and-the-robe-up", token);
		Assert.Contains(skin, buffCasts);
		Assert.Contains(robe, buffCasts);
		Assert.All(new[] { skin, robe }, buff => Assert.True(probe.Server.GetEffectController().FindBySkillId(buff) != null, $"Buff {buff} is not on the Sorcerer."));

		var lines = new List<string>();
		TimeSpan? shockGap = null, blazeGap = null;
		int fights = 0, kills = 0, deaths = 0;
		while ((shockGap == null || blazeGap == null) && fights < mostFights)
		{
			fights++;
			Npc next = NearestLiving(probe, crasaur);
			int mp = probe.World.CurrentMp;
			ClericFight fight = await TableFightAsync(probe, SorcererTable, $"s04-{fights:D2}", $"fight-ice-crasaur-{fights}", () => Task.FromResult(next.GetObjectId()), token);
			AssertNoRefusedCastRepeats(fight);
			AssertOnlyCasts(fight, table);
			Assert.True(fight.Result.Killed || fight.Result.Retreats > 0 || fight.Result.Deaths > 0, $"Fight {fights} ended no way: {fight.Result}.");
			kills += fight.Result.Killed ? 1 : 0;
			deaths += fight.Result.Deaths;
			if (fight.Run([ice], [shock]) is >= 0 and int first) shockGap ??= fight.Casts[first + 1].At - fight.Casts[first].At;
			if (fight.Run([bolt], [blaze]) is >= 0 and int second) blazeGap ??= fight.Casts[second + 1].At - fight.Casts[second].At;
			lines.Add($"fight {fights}: {Outcome(fight)}, MP {mp} to {probe.World.CurrentMp}/{probe.World.MaxMp}, HP {probe.World.CurrentHp}/{probe.World.MaxHp}");
		}
		Assert.True(shockGap is { } afterIce && afterIce <= TimeSpan.FromSeconds(3), $"Frozen Shock did not follow Ice Chain inside 3 s in {fights} fights: {string.Join("; ", lines)}.");
		Assert.True(blazeGap is { } afterBolt && afterBolt <= TimeSpan.FromSeconds(3), $"Blaze did not follow Flame Bolt inside 3 s in {fights} fights: {string.Join("; ", lines)}.");

		// Prepared by the director: powder, and half HP. The rest is the journey's.
		await GiveAsync(probe, token, (LesserOdellaPowder, 20));
		if (!probe.Server.IsDead()) await probe.CutHpAsync(50);
		int hpBefore = probe.World.CurrentHp;
		(_, int[] restCasts) = await RestStepAsync(probe, "s05", "director-gives-powder-and-halves-hp-then-rest", token);
		Assert.Contains(herb, restCasts);
		Assert.True(probe.Owned(LesserOdellaPowder) < 20, "No powder was spent.");
		Console.WriteLine($"{id}: level {probe.World.Level} Sorcerer, max HP {probe.World.MaxHp}, MP {probe.World.MaxMp}, book {book}. Buff check: casts {string.Join(" ", buffCasts)}. " +
			$"{fights} fight(s), {kills} kill(s), {deaths} death(s): {string.Join("; ", lines)}. Frozen Shock {shockGap.Value.TotalMilliseconds:F0} ms after Ice Chain, " +
			$"Blaze {blazeGap.Value.TotalMilliseconds:F0} ms after Flame Bolt. Rest from {hpBefore} HP: casts {string.Join(" ", restCasts)}, powder {probe.Owned(LesserOdellaPowder)} of 20 left. " +
			$"HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}.");
	}

	/// <summary>
	/// NR-61, row sorcerer-16. Prepared by the director: a level-16 Sorcerer with the spellbook of Q24013 and the robe
	/// shoes and tunic of Q24011 and Q24012 in the bag, by the tusked mosbears (210437, level 14) of the Cleric's row. The
	/// journey's equipment check wears them. Then it fights one mosbear by its table: Flame Harpoon comes before Flame
	/// Bolt, and Flame Cage has taken Erosion's place. A kill, a retreat and a death are recorded outcomes: the spot
	/// brings more monsters, and with two on it the Sorcerer casts Root and leaves.
	/// </summary>
	private async Task SorcererLevelSixteenRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int mosbear = 210437, book = 100601429, shoes = 114101696, tunic = 110101836, bolt = 1285, blaze = 1405, ice = 1365, shock = 1227, cage = 1510,
			erosion = 1447, skin = 1156, robe = 1297, empyrean = 1495, root = 1328, gain = 1193, harpoon = 1271;
		int[] table = [bolt, blaze, ice, shock, cage, skin, robe, empyrean, root, gain, harpoon, 247, 250];
		probe.Session.BeginStep("s01", "director-makes-a-level-sixteen-sorcerer-in-altgard");
		await probe.BecomeAsync(PlayerClass.SORCERER, 16);
		Assert.All(table, skill => Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 16."));
		await GiveAsync(probe, token, (book, 1), (shoes, 1), (tunic, 1));
		await probe.MoveToMapAsync(ClericProbeMap, 1427.2f, 789.9f, 249.9f);
		probe.Session.BeginStep("s02", "equipment-check");
		IReadOnlyList<NaturalGearUpgrade> worn = await probe.Journey.RunObservedEquipmentCheckAsync(token);
		Assert.Equal(book, MainHand(probe));
		Assert.All(new[] { shoes, tunic }, piece => Assert.Contains(probe.World.Inventory.Values, item => item.ItemId == piece && item.Details.EquippedSlot.GetValueOrDefault() != 0));

		Npc first = NearestLiving(probe, mosbear);
		int mp = probe.World.CurrentMp;
		ClericFight fight = await TableFightAsync(probe, SorcererTable, "s03", "fight-a-tusked-mosbear", () => Task.FromResult(first.GetObjectId()), token);
		AssertNoRefusedCastRepeats(fight);
		AssertOnlyCasts(fight, table);
		Assert.DoesNotContain(fight.Casts, cast => cast.SkillId == erosion);
		int firstHarpoon = fight.Casts.FindIndex(cast => cast.SkillId == harpoon), firstBolt = fight.Casts.FindIndex(cast => cast.SkillId == bolt);
		Assert.True(firstHarpoon >= 0 && (firstBolt < 0 || firstHarpoon < firstBolt), $"Flame Harpoon did not come before Flame Bolt: {fight.Order}.");
		Assert.True(fight.Result.Killed || fight.Result.Retreats > 0 || fight.Result.Deaths > 0, $"The fight ended no way: {fight.Result}.");
		StarterTraceRecord? left = fight.Decided.FirstOrDefault(record => record.Fields.GetProperty("action").GetString() == "retreat");
		int rooted = Array.FindIndex(fight.Decided, record => Decided(record, "cast-target", root));
		Console.WriteLine($"{id}: level {probe.World.Level} Sorcerer, max HP {probe.World.MaxHp}, MP {probe.World.MaxMp}; the check asked for " +
			$"{string.Join(", ", worn.Select(upgrade => $"{upgrade.ItemId} to slot {upgrade.Slot}"))}. One tusked mosbear: {Outcome(fight)}; MP {mp} to {probe.World.CurrentMp}. " +
			$"Flame Harpoon was cast {fight.Casts.Count(cast => cast.SkillId == harpoon)} time(s), Flame Bolt {fight.Casts.Count(cast => cast.SkillId == bolt)}, " +
			$"Flame Cage {fight.Casts.Count(cast => cast.SkillId == cage)}, in {fight.Decided.Length} decisions. " +
			$"Root {(rooted >= 0 ? "decided: " + fight.Decided[rooted].Fields.GetProperty("reason").GetString() : "not decided.")} " +
			$"It left: {left?.Fields.GetProperty("reason").GetString() ?? "no."} HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}, dead {probe.Server.IsDead()}.");
	}

	/// <summary>
	/// NR-61, row sorcerer-20. Prepared by the director: a level-20 Sorcerer with the spellbook of Q24016, by the starved
	/// mosbears (210564, level 13) of the Cleric's row, with powder, two shield scrolls and three life potions in the bag.
	/// The first fight is the journey's alone: Delayed Blast is cast. Before the second the director gives 2,000 DP and
	/// cuts HP to 45%: Empyrean Fire is cast and the ladder answers. Before the third the director cuts MP to 45%: Gain
	/// Mana is cast in the fight. Last the director cuts MP to a tenth and the journey's rest casts MP Recovery.
	/// </summary>
	private async Task SorcererLevelTwentyRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int mosbear = 210564, book = 100601431, bolt = 1285, blaze = 1406, ice = 1366, shock = 1228, cage = 1510, skin = 1157, robe = 1298,
			empyrean = 1496, root = 1328, gain = 1194, harpoon = 1272, blast = 1421, recovery = 252, scroll = 164000068, potion = 162000003;
		int[] table = [bolt, blaze, ice, shock, cage, skin, robe, empyrean, root, gain, harpoon, blast, 251, recovery];
		probe.Session.BeginStep("s01", "director-makes-a-level-twenty-sorcerer-in-altgard");
		await probe.BecomeAsync(PlayerClass.SORCERER, 20);
		Assert.All(table, skill => Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 20."));
		await GiveAsync(probe, token, (book, 1), (LesserOdellaPowder, 20), (scroll, 2), (potion, 3));
		await probe.MoveToMapAsync(ClericProbeMap, 1867.3f, 456.0f, 270.2f);
		probe.Session.BeginStep("s02", "equipment-check");
		await probe.Journey.RunObservedEquipmentCheckAsync(token);
		Assert.Equal(book, MainHand(probe));

		Npc first = NearestLiving(probe, mosbear);
		ClericFight plain = await TableFightAsync(probe, SorcererTable, "s03", "fight-a-starved-mosbear", () => Task.FromResult(first.GetObjectId()), token);
		AssertNoRefusedCastRepeats(plain);
		AssertOnlyCasts(plain, table);
		Assert.Contains(plain.Casts, cast => cast.SkillId == blast);
		Assert.DoesNotContain(plain.Casts, cast => cast.SkillId == empyrean);
		Assert.True(plain.Result is { Killed: true, Deaths: 0 }, $"The first fight was no kill: {Outcome(plain)}.");

		Npc second = NearestLiving(probe, mosbear);
		ClericFight hurt = await TableFightAsync(probe, SorcererTable, "s04", "fight-a-starved-mosbear-from-under-half-hp-with-dp", async () =>
		{
			// Prepared by the director, after the journey's own rest: 2,000 DP and 45% HP as the fight begins.
			await probe.SetDpAsync(2000);
			await probe.CutHpAsync(45);
			return second.GetObjectId();
		}, token);
		AssertNoRefusedCastRepeats(hurt);
		AssertOnlyCasts(hurt, table);
		Assert.Contains(hurt.Casts, cast => cast.SkillId == empyrean);
		Assert.True(probe.World.CurrentDp < 2000, $"The DP was not spent: {probe.World.CurrentDp}.");
		StarterTraceRecord[] ladder = hurt.Decided.Where(record => record.Fields.GetProperty("action").GetString() is "shield-scroll" or "hot-potion").ToArray();
		Assert.True(ladder.Length > 0, $"The ladder did not answer 45% HP: decisions {hurt.Counts}.");
		Assert.Equal("shield-scroll", ladder[0].Fields.GetProperty("action").GetString());
		Assert.True(HpPercentAt(ladder[0]) <= 50, $"The shield scroll was decided at {HpPercentAt(ladder[0])}% HP.");
		Assert.Equal(0, hurt.Result.Deaths);

		Npc third = NearestLiving(probe, mosbear);
		ClericFight thirsty = await TableFightAsync(probe, SorcererTable, "s05", "fight-a-starved-mosbear-from-under-half-mana", async () =>
		{
			// Prepared by the director, after the journey's own rest: 45% of its mana as the fight begins.
			await probe.CutMpAsync(45);
			return third.GetObjectId();
		}, token);
		AssertNoRefusedCastRepeats(thirsty);
		AssertOnlyCasts(thirsty, table);
		Assert.True(thirsty.Result.Killed || thirsty.Result.Retreats > 0, $"The third fight ended neither way: {Outcome(thirsty)}.");
		StarterTraceRecord[] gained = thirsty.Decided.Where(record => Decided(record, "cast-self", gain)).ToArray();
		Assert.True(gained.Length > 0, $"Gain Mana was not cast in the fight: {thirsty.Order}; decisions {thirsty.Counts}.");
		Assert.All(gained, record => Assert.True(MpPercentAt(record) <= 50, $"Gain Mana was decided at {MpPercentAt(record)}% MP."));
		Assert.Contains(thirsty.Casts, cast => cast.SkillId == gain);

		// Prepared by the director: a tenth of its mana. The rest is the journey's; Gain Mana is cooling down.
		probe.Session.BeginStep("s06", "director-cuts-mp");
		await probe.CutMpAsync(10);
		int mpBefore = probe.World.CurrentMp;
		(_, int[] restCasts) = await RestStepAsync(probe, "s07", "rest-from-a-tenth-of-its-mana", token);
		Assert.Contains(recovery, restCasts);
		Assert.True(probe.Owned(LesserOdellaPowder) < 20, "No powder was spent.");
		Console.WriteLine($"{id}: level {probe.World.Level} Sorcerer, max HP {probe.World.MaxHp}, MP {probe.World.MaxMp}. Unhurt: {Outcome(plain)}. " +
			$"From 45% HP with 2,000 DP: {Outcome(hurt)}; the ladder: {string.Join(", ", ladder.Select(record => $"{record.Fields.GetProperty("action").GetString()} at {HpPercentAt(record)}% HP"))}; " +
			$"DP left {probe.World.CurrentDp}. From 45% MP: {Outcome(thirsty)}; Gain Mana decided at {string.Join(", ", gained.Select(MpPercentAt))}% MP. " +
			$"Rest from {mpBefore} MP: casts {string.Join(" ", restCasts)}, powder {probe.Owned(LesserOdellaPowder)} of 20 left. " +
			$"HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}.");
	}

	/// <summary>
	/// NR-61, row sorcerer-25. Prepared by the director: a level-25 Sorcerer with the spellbook of Q24016, by the starved
	/// mosbears (210564, level 13), with Odella Powder in the bag. Three fights are the journey's alone, for the numbers.
	/// Then the director spawns one starved mosbear 4 m from the Sorcerer and sets it on it: with the monster on it, the
	/// table's instants come first, Freezing Wind before the others. Then two are spawned and set on it: with two
	/// attackers the journey casts Root on its target and leaves. Last the director halves the HP and the rest casts the
	/// fourth rank of Herb Treatment, which spends Odella Powder.
	/// </summary>
	private async Task SorcererLevelTwentyFiveRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int mosbear = 210564, book = 100601431, bolt = 1286, blaze = 1407, ice = 1367, shock = 1229, cage = 1511, skin = 1158, robe = 1299,
			empyrean = 1497, root = 1328, gain = 1195, harpoon = 1273, blast = 1422, frost = 1217, herb = 253;
		int[] table = [bolt, blaze, ice, shock, cage, skin, robe, empyrean, root, gain, harpoon, blast, frost, herb, 254];
		probe.Session.BeginStep("s01", "director-makes-a-level-twenty-five-sorcerer-in-altgard");
		await probe.BecomeAsync(PlayerClass.SORCERER, 25);
		Assert.All(table, skill => Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 25."));
		await GiveAsync(probe, token, (book, 1), (OdellaPowder, 20));
		BotNavigationGeometry geometry = await probe.MoveToMapAsync(ClericProbeMap, 1867.3f, 456.0f, 270.2f);
		probe.Session.BeginStep("s02", "equipment-check");
		await probe.Journey.RunObservedEquipmentCheckAsync(token);
		Assert.Equal(book, MainHand(probe));

		var lines = new List<string>();
		for (int fights = 1; fights <= 3; fights++)
		{
			Npc next = NearestLiving(probe, mosbear);
			(int hp, int mp) = (probe.World.CurrentHp, probe.World.CurrentMp);
			ClericFight fight = await TableFightAsync(probe, SorcererTable, $"s03-{fights}", $"fight-starved-mosbear-{fights}", () => Task.FromResult(next.GetObjectId()), token);
			AssertNoRefusedCastRepeats(fight);
			AssertOnlyCasts(fight, table);
			Assert.Equal(0, fight.Result.Deaths);
			Assert.True(fight.Result.Killed || fight.Result.Retreats > 0, $"Fight {fights} ended neither way: {fight.Result}.");
			lines.Add($"fight {fights}: {Outcome(fight)}, HP {hp} to {probe.World.CurrentHp}/{probe.World.MaxHp}, MP {mp} to {probe.World.CurrentMp}/{probe.World.MaxMp}");
		}

		Npc[] one = [];
		int mpClose = 0;
		ClericFight close = await TableFightAsync(probe, SorcererTable, "s04", "fight-a-starved-mosbear-that-is-on-the-sorcerer", async () =>
		{
			// Prepared by the director, after the journey's own rest: one starved mosbear 4 m away, set on the Sorcerer.
			one = await SpawnSetOnAsync(probe, geometry, mosbear, 1, token);
			mpClose = probe.World.CurrentMp;
			return one[0].GetObjectId();
		}, token);
		RemoveSetOn(one);
		AssertNoRefusedCastRepeats(close);
		AssertOnlyCasts(close, table);
		int frosts = close.Casts.Count(cast => cast.SkillId == frost), mpAfterClose = probe.World.CurrentMp;
		Assert.True(frosts > 0, $"Freezing Wind was not cast with the mosbear on the Sorcerer: {close.Order}; decisions {close.Counts}.");
		Assert.Equal(0, close.Result.Deaths);
		// What Freezing Wind did: the target's HP as the fight saw it at the decision after the cast.
		int frostAt = Array.FindIndex(close.Decided, record => Decided(record, "cast-target", frost));
		string frostLeft = frostAt >= 0 && frostAt + 1 < close.Decided.Length &&
			close.Decided[frostAt + 1].Fields.GetProperty("observedState").GetProperty("TargetHpPercent") is { ValueKind: JsonValueKind.Number } after
			? after.GetInt32() + "%" : "unseen";

		Npc[] pack = [];
		ClericFight swarm = await TableFightAsync(probe, SorcererTable, "s05", "fight-a-pack-of-two-starved-mosbears", async () =>
		{
			// Prepared by the director, after the journey's own rest: two starved mosbears 4 m away, set on the Sorcerer.
			pack = await SpawnSetOnAsync(probe, geometry, mosbear, 2, token);
			return pack[0].GetObjectId();
		}, token);
		RemoveSetOn(pack);
		AssertOnlyCasts(swarm, table);
		int rooted = Array.FindIndex(swarm.Decided, record => Decided(record, "cast-target", root));
		Assert.True(rooted >= 0, $"Root was not cast: {swarm.Order}; decisions {swarm.Counts}.");
		string? reason = swarm.Decided[rooted].Fields.GetProperty("reason").GetString();
		Assert.StartsWith("Hold the target before retreating", reason);
		Assert.Contains(swarm.Decided.Skip(rooted + 1), record => record.Fields.GetProperty("action").GetString() == "retreat");

		// Prepared by the director: half HP. The rest is the journey's.
		probe.Session.BeginStep("s06", "director-halves-hp");
		if (!probe.Server.IsDead()) await probe.CutHpAsync(50);
		(_, int[] restCasts) = await RestStepAsync(probe, "s07", "rest-from-half-hp-with-odella-powder", token);
		Assert.Contains(herb, restCasts);
		Assert.True(probe.Owned(OdellaPowder) < 20, "No Odella Powder was spent.");
		Console.WriteLine($"{id}: level {probe.World.Level} Sorcerer, max HP {probe.World.MaxHp}, MP {probe.World.MaxMp}. " + string.Join("; ", lines) +
			$". With one mosbear set on it: {Outcome(close)}; Freezing Wind cast {frosts} time(s), the mosbear at {frostLeft} HP after the first, MP {mpClose} to {mpAfterClose}. " +
			$"Then against {pack.Length} the director spawned 4 m away and set on it: {Outcome(swarm)}; Root decided with " +
			$"{swarm.Decided[rooted].Fields.GetProperty("observedState").GetProperty("NearbyAggressors").GetInt32()} attackers: {reason} " +
			$"Rest: casts {string.Join(" ", restCasts)}, Odella Powder {probe.Owned(OdellaPowder)} of 20 left. " +
			$"HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}, dead {probe.Server.IsDead()}.");
	}

	private const string ChanterTable = "natural-chanter-v1:";

	/// <summary>
	/// NR-71, row chanter-10. Prepared by the director: the Priest is made a level-10 Chanter with the skills of every
	/// level up to it, is given the ceremony's staff (101500498) and is placed in Altgard by the ice crasaurs (210415,
	/// level 11), where the Cleric's row fights. The journey's equipment check takes the staff, and its buff check casts
	/// Protectorate's Prayer and turns Celerity Mantra on. Then it fights one crasaur after another by its table until
	/// both pairs are seen: Thunderbolt Strike after Infernal Blaze, from range, and Booming Strike after Hallowed Strike,
	/// with the crasaur on it. Last the director gives powder and halves the HP, and the journey's rest casts Herb
	/// Treatment.
	/// </summary>
	private async Task ChanterLevelTenRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int crasaur = 210415, staff = 101500498, infernal = 1814, thunderbolt = 1715, hallowed = 1615, booming = 1562, meteor = 1778,
			blessing = 1685, celerity = 1809, herb = 246, mostFights = 6;
		probe.Session.BeginStep("s01", "director-makes-a-level-ten-chanter-in-altgard");
		await probe.BecomeAsync(PlayerClass.CHANTER, 10);
		int[] table = probe.CatalogOf(PlayerClass.CHANTER);
		Assert.All(new[] { infernal, thunderbolt, hallowed, booming, meteor, blessing, celerity, herb, 249, 1638 }, skill =>
			Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 10."));
		await GiveAsync(probe, token, (staff, 1));
		await probe.MoveToMapAsync(ClericProbeMap, 1473.2f, 1765.2f, 247.5f);
		probe.Session.BeginStep("s02", "equipment-check-takes-the-staff");
		await probe.Journey.RunObservedEquipmentCheckAsync(token);
		Assert.Equal(staff, MainHand(probe));

		(_, int[] buffCasts) = await BuffCheckStepAsync(probe, "s03", "buff-check-casts-the-prayer-and-turns-the-mantra-on", token);
		Assert.Contains(blessing, buffCasts);
		Assert.Contains(celerity, buffCasts);
		Assert.Contains(celerity, probe.World.ActiveToggles);

		var lines = new List<string>();
		TimeSpan? boltGap = null, boomGap = null;
		double firedFrom = 0;
		int fights = 0, kills = 0, deaths = 0, swings = 0;
		while ((boltGap == null || boomGap == null) && fights < mostFights)
		{
			fights++;
			Npc next = NearestLiving(probe, crasaur);
			(int hp, int mp) = (probe.World.CurrentHp, probe.World.CurrentMp);
			ClericFight fight = await TableFightAsync(probe, ChanterTable, $"s04-{fights:D2}", $"fight-ice-crasaur-{fights}", () => Task.FromResult(next.GetObjectId()), token);
			AssertNoRefusedCastRepeats(fight);
			AssertOnlyCasts(fight, table);
			Assert.True(fight.Result.Killed || fight.Result.Retreats > 0 || fight.Result.Deaths > 0, $"Fight {fights} ended no way: {fight.Result}.");
			kills += fight.Result.Killed ? 1 : 0;
			deaths += fight.Result.Deaths;
			if (boltGap == null && fight.Run([infernal], [thunderbolt]) is >= 0 and int first)
			{
				boltGap = fight.Casts[first + 1].At - fight.Casts[first].At;
				firedFrom = fight.Decided.First(record => Decided(record, "cast-target", infernal)).Fields.GetProperty("targetDistance").GetDouble();
			}
			if (fight.Run([hallowed], [booming]) is >= 0 and int second) boomGap ??= fight.Casts[second + 1].At - fight.Casts[second].At;
			swings += Swings(fight);
			lines.Add($"fight {fights}: {Outcome(fight)}, HP {hp} to {probe.World.CurrentHp}/{probe.World.MaxHp}, MP {mp} to {probe.World.CurrentMp}/{probe.World.MaxMp}");
		}
		Assert.True(boltGap is { } afterBlaze && afterBlaze <= TimeSpan.FromSeconds(3), $"Thunderbolt Strike did not follow Infernal Blaze inside 3 s in {fights} fights: {string.Join("; ", lines)}.");
		Assert.True(boomGap is { } afterStrike && afterStrike <= TimeSpan.FromSeconds(3), $"Booming Strike did not follow Hallowed Strike inside 3 s in {fights} fights: {string.Join("; ", lines)}.");
		Assert.True(firedFrom > 6, $"Infernal Blaze was decided at {firedFrom:F1} m.");

		// Prepared by the director: powder, and half HP. The rest is the journey's.
		await GiveAsync(probe, token, (LesserOdellaPowder, 20));
		if (!probe.Server.IsDead()) await probe.CutHpAsync(50);
		int hpBefore = probe.World.CurrentHp;
		(_, int[] restCasts) = await RestStepAsync(probe, "s05", "director-gives-powder-and-halves-hp-then-rest", token);
		Assert.Contains(herb, restCasts);
		Assert.True(probe.Owned(LesserOdellaPowder) < 20, "No powder was spent.");
		Console.WriteLine($"{id}: level {probe.World.Level} Chanter, max HP {probe.World.MaxHp}, MP {probe.World.MaxMp}, staff {staff}. Buff check: casts {string.Join(" ", buffCasts)}; " +
			$"toggles on {string.Join(" ", probe.World.ActiveToggles.Order())}. {fights} fight(s), {kills} kill(s), {deaths} death(s): {string.Join("; ", lines)}. " +
			$"Infernal Blaze from {firedFrom:F1} m, Thunderbolt Strike {boltGap.Value.TotalMilliseconds:F0} ms after it; Booming Strike {boomGap.Value.TotalMilliseconds:F0} ms after Hallowed Strike; " +
			$"the staff swung {swings} time(s). " +
			$"Rest from {hpBefore} HP: casts {string.Join(" ", restCasts)}, powder {probe.Owned(LesserOdellaPowder)} of 20 left. " +
			$"HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}.");
	}

	/// <summary>
	/// NR-71, row chanter-16. Prepared by the director: a level-16 Chanter with the staff of Q24013 and the chain shoes
	/// and hauberk of Q24011 and Q24012 in the bag, by the tusked mosbears (210437, level 14) of the Cleric's row. The
	/// journey's equipment check wears them, and its buff check casts Protectorate's Prayer and Promise of Earth and turns
	/// two mantras on. Then it fights one tusked mosbear by its table: Word of Revival is cast, and only while the
	/// Chanter is being hit. A kill, a retreat and a death are recorded outcomes: the spot brings more monsters, and with
	/// three on it the Chanter leaves, as the Cleric does there.
	/// </summary>
	private async Task ChanterLevelSixteenRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int mosbear = 210437, staff = 101501355, shoes = 114501726, hauberk = 110551139, hallowed = 1616, booming = 1563, crashing = 1703,
			revival = 1735, promise = 1627, blessing = 1686, celerity = 1809, shieldMantra = 1657;
		probe.Session.BeginStep("s01", "director-makes-a-level-sixteen-chanter-in-altgard");
		await probe.BecomeAsync(PlayerClass.CHANTER, 16);
		int[] table = probe.CatalogOf(PlayerClass.CHANTER);
		Assert.All(new[] { hallowed, booming, crashing, revival, promise, blessing, celerity, shieldMantra }, skill =>
			Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 16."));
		await GiveAsync(probe, token, (staff, 1), (shoes, 1), (hauberk, 1));
		await probe.MoveToMapAsync(ClericProbeMap, 1427.2f, 789.9f, 249.9f);
		probe.Session.BeginStep("s02", "equipment-check");
		IReadOnlyList<NaturalGearUpgrade> worn = await probe.Journey.RunObservedEquipmentCheckAsync(token);
		Assert.Equal(staff, MainHand(probe));
		Assert.All(new[] { shoes, hauberk }, piece => Assert.Contains(probe.World.Inventory.Values, item => item.ItemId == piece && item.Details.EquippedSlot.GetValueOrDefault() != 0));

		(_, int[] buffCasts) = await BuffCheckStepAsync(probe, "s03", "buff-check-casts-the-buffs-and-turns-the-mantras-on", token);
		Assert.All(new[] { blessing, promise, celerity, shieldMantra }, buff => Assert.Contains(buff, buffCasts));
		Assert.Equal([shieldMantra, celerity], probe.World.ActiveToggles.Order());

		Npc first = NearestLiving(probe, mosbear);
		(int hp, int mp) = (probe.World.CurrentHp, probe.World.CurrentMp);
		ClericFight fight = await TableFightAsync(probe, ChanterTable, "s04", "fight-a-tusked-mosbear", () => Task.FromResult(first.GetObjectId()), token);
		AssertNoRefusedCastRepeats(fight);
		AssertOnlyCasts(fight, table);
		Assert.True(fight.Result.Killed || fight.Result.Retreats > 0 || fight.Result.Deaths > 0, $"The fight ended no way: {fight.Result}.");
		StarterTraceRecord[] revived = fight.Decided.Where(record => Decided(record, "cast-self", revival)).ToArray();
		Assert.True(revived.Length > 0, $"Word of Revival was not cast: {Outcome(fight)}.");
		Assert.All(revived, record => Assert.True(record.Fields.GetProperty("observedState").GetProperty("Aggro").GetBoolean(), "Word of Revival was decided while the Chanter was not being hit."));
		StarterTraceRecord? left = fight.Decided.FirstOrDefault(record => record.Fields.GetProperty("action").GetString() == "retreat");
		Console.WriteLine($"{id}: level {probe.World.Level} Chanter, max HP {probe.World.MaxHp}, MP {probe.World.MaxMp}; the check asked for " +
			$"{string.Join(", ", worn.Select(upgrade => $"{upgrade.ItemId} to slot {upgrade.Slot}"))}. Buff check: casts {string.Join(" ", buffCasts)}; " +
			$"toggles on {string.Join(" ", probe.World.ActiveToggles.Order())}. One tusked mosbear: {Outcome(fight)}, HP {hp} to {probe.World.CurrentHp}/{probe.World.MaxHp}, " +
			$"MP {mp} to {probe.World.CurrentMp}/{probe.World.MaxMp}; the staff swung {Swings(fight)} time(s), the chain's skills were cast " +
			$"{fight.Casts.Count(cast => cast.SkillId is hallowed or booming or crashing)} time(s). " +
			$"Word of Revival decided {revived.Length} time(s), under attack each time, at {string.Join(", ", revived.Select(HpPercentAt))}% HP. " +
			$"It left: {left?.Fields.GetProperty("reason").GetString() ?? "no."} HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}, dead {probe.Server.IsDead()}.");
	}

	/// <summary>
	/// NR-71, row chanter-20. Prepared by the director: a level-20 Chanter with the staff of Q24016, by the starved
	/// mosbears (210564, level 13) of the Cleric's row, with powder, two shield scrolls and three life potions in the bag.
	/// The buff check casts Rage Spell, which costs 379 MP, beside the prayer and the promise. It fights by its table
	/// until the chain of three is seen, Hallowed Strike, Booming Strike and Crashing Strike, and Incandescent Blow has
	/// followed Meteor Strike. Before one more fight the director gives 2,000 DP and cuts HP
	/// to 45%: Winter Circle is cast with the mosbear on it, and the ladder answers. Last the director cuts MP to a tenth
	/// and the journey's rest casts MP Recovery.
	/// </summary>
	private async Task ChanterLevelTwentyRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int mosbear = 210564, staff = 101501357, meteor = 1780, incandescent = 1667, circle = 1638, rage = 1561, promise = 1627, blessing = 1687,
			binding = 1574, recovery = 252, scroll = 164000068, potion = 162000003, hallowed = 1617, booming = 1564, crashing = 1704, mostFights = 5;
		probe.Session.BeginStep("s01", "director-makes-a-level-twenty-chanter-in-altgard");
		await probe.BecomeAsync(PlayerClass.CHANTER, 20);
		int[] table = probe.CatalogOf(PlayerClass.CHANTER);
		Assert.All(new[] { meteor, incandescent, circle, rage, promise, blessing, binding, recovery, 251 }, skill =>
			Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 20."));
		await GiveAsync(probe, token, (staff, 1), (LesserOdellaPowder, 20), (scroll, 2), (potion, 3));
		await probe.MoveToMapAsync(ClericProbeMap, 1867.3f, 456.0f, 270.2f);
		probe.Session.BeginStep("s02", "equipment-check");
		await probe.Journey.RunObservedEquipmentCheckAsync(token);
		Assert.Equal(staff, MainHand(probe));

		int mpFull = probe.World.CurrentMp;
		(_, int[] buffCasts) = await BuffCheckStepAsync(probe, "s03", "buff-check-casts-rage-spell", token);
		Assert.All(new[] { blessing, promise, rage }, buff => Assert.Contains(buff, buffCasts));
		Assert.True(probe.Server.GetEffectController().FindBySkillId(rage) != null, "Rage Spell is not on the Chanter.");
		int mpBuffed = probe.World.CurrentMp;

		var lines = new List<string>();
		ClericFight? shown = null, chain = null;
		int fights = 0, swings = 0;
		while ((shown == null || chain == null) && fights < mostFights)
		{
			fights++;
			Npc next = NearestLiving(probe, mosbear);
			(int hp, int mp) = (probe.World.CurrentHp, probe.World.CurrentMp);
			ClericFight fight = await TableFightAsync(probe, ChanterTable, $"s04-{fights:D2}", $"fight-starved-mosbear-{fights}", () => Task.FromResult(next.GetObjectId()), token);
			AssertNoRefusedCastRepeats(fight);
			AssertOnlyCasts(fight, table);
			Assert.True(fight.Result is { Killed: true, Deaths: 0 }, $"Fight {fights} was no kill: {Outcome(fight)}.");
			Assert.DoesNotContain(fight.Casts, cast => cast.SkillId == circle);
			swings += Swings(fight);
			if (fight.Run([meteor], [incandescent]) >= 0) shown ??= fight;
			if (fight.Run([hallowed], [booming], [crashing]) >= 0) chain ??= fight;
			lines.Add($"fight {fights}: {Outcome(fight)}, HP {hp} to {probe.World.CurrentHp}/{probe.World.MaxHp}, MP {mp} to {probe.World.CurrentMp}/{probe.World.MaxMp}");
		}
		Assert.True(shown != null, $"Incandescent Blow never followed Meteor Strike in {fights} fights: {string.Join("; ", lines)}.");
		int pair = shown.Run([meteor], [incandescent]);
		TimeSpan gap = shown.Casts[pair + 1].At - shown.Casts[pair].At;
		Assert.True(gap <= TimeSpan.FromSeconds(3), $"Incandescent Blow came {gap.TotalMilliseconds:F0} ms after Meteor Strike: {shown.Order}.");
		Assert.True(chain != null, $"Crashing Strike never followed Booming Strike and Hallowed Strike in {fights} fights: {string.Join("; ", lines)}.");
		int run = chain.Run([hallowed], [booming], [crashing]);
		TimeSpan second = chain.Casts[run + 1].At - chain.Casts[run].At, third = chain.Casts[run + 2].At - chain.Casts[run + 1].At;
		Assert.True(second <= TimeSpan.FromSeconds(3) && third <= TimeSpan.FromSeconds(3), $"The chain's steps came {second.TotalMilliseconds:F0} ms and {third.TotalMilliseconds:F0} ms apart: {chain.Order}.");

		Npc hurtTarget = NearestLiving(probe, mosbear);
		ClericFight hurt = await TableFightAsync(probe, ChanterTable, "s05", "fight-a-starved-mosbear-from-under-half-hp-with-dp", async () =>
		{
			// Prepared by the director, after the journey's own rest: 2,000 DP and 45% HP as the fight begins.
			await probe.SetDpAsync(2000);
			await probe.CutHpAsync(45);
			return hurtTarget.GetObjectId();
		}, token);
		AssertNoRefusedCastRepeats(hurt);
		AssertOnlyCasts(hurt, table);
		Assert.Contains(hurt.Casts, cast => cast.SkillId == circle);
		Assert.True(probe.World.CurrentDp < 2000, $"The DP was not spent: {probe.World.CurrentDp}.");
		StarterTraceRecord[] ladder = hurt.Decided.Where(record => record.Fields.GetProperty("action").GetString() is "shield-scroll" or "hot-potion" ||
			record.Fields.GetProperty("action").GetString() == "cast-self" && record.Fields.GetProperty("reason").GetString()?.Contains("HP is at or below") == true).ToArray();
		Assert.True(ladder.Length > 0, $"The ladder did not answer 45% HP: decisions {hurt.Counts}.");
		Assert.Equal("shield-scroll", ladder[0].Fields.GetProperty("action").GetString());
		Assert.Equal(0, hurt.Result.Deaths);

		// Prepared by the director: a tenth of its mana. The rest is the journey's.
		probe.Session.BeginStep("s06", "director-cuts-mp");
		await probe.CutMpAsync(10);
		int mpBefore = probe.World.CurrentMp;
		(_, int[] restCasts) = await RestStepAsync(probe, "s07", "rest-from-a-tenth-of-its-mana", token);
		Assert.Contains(recovery, restCasts);
		Assert.True(probe.Owned(LesserOdellaPowder) < 20, "No powder was spent.");
		Console.WriteLine($"{id}: level {probe.World.Level} Chanter, max HP {probe.World.MaxHp}, MP {probe.World.MaxMp}. Buff check: casts {string.Join(" ", buffCasts)}; MP {mpFull} to {mpBuffed}. " +
			$"{fights} fight(s): {string.Join("; ", lines)}. Incandescent Blow {gap.TotalMilliseconds:F0} ms after Meteor Strike; Booming Strike {second.TotalMilliseconds:F0} ms " +
			$"after Hallowed Strike and Crashing Strike {third.TotalMilliseconds:F0} ms after that; the staff swung {swings} time(s). " +
			$"From 45% HP with 2,000 DP: {Outcome(hurt)}; the ladder: {string.Join(", ", ladder.Select(record => $"{record.Fields.GetProperty("action").GetString()}" +
				$"{(record.Fields.GetProperty("skillId") is { ValueKind: JsonValueKind.Number } skill ? " " + skill.GetInt32() : "")} at {HpPercentAt(record)}% HP"))}; DP left {probe.World.CurrentDp}. " +
			$"Rest from {mpBefore} MP: casts {string.Join(" ", restCasts)}, powder {probe.Owned(LesserOdellaPowder)} of 20 left. " +
			$"HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}.");
	}

	/// <summary>
	/// NR-71, row chanter-25. Prepared by the director: a level-25 Chanter with the staff of Q24016, by the starved
	/// mosbears (210564, level 13), with Odella Powder in the bag. The buff check turns the three mantras on. Three fights
	/// are the journey's alone, for the numbers and for what Smite does once the mosbear is on the Chanter. Before one
	/// more the director cuts HP to 55%: Protective Ward is cast, by the ladder. Then the director spawns three starved
	/// mosbears 4 m from the Chanter and sets them on it: with three attackers the journey casts Binding Word on its
	/// target and leaves. Last the director halves the HP and the rest spends Odella Powder on a fourth-rank powder
	/// skill: MP Recovery when its mana is short too, with Healing Light for the HP while the powder cools down.
	/// </summary>
	private async Task ChanterLevelTwentyFiveRowAsync(StarterProbe probe, string id, CancellationToken token)
	{
		const int mosbear = 210564, staff = 101501357, ward = 1690, binding = 1575, herb = 253, recovery = 254, smite = 4013, celerity = 1809, shieldMantra = 1659,
			revivalMantra = 1746, rage = 1561, promise = 1628, blessing = 1688;
		probe.Session.BeginStep("s01", "director-makes-a-level-twenty-five-chanter-in-altgard");
		await probe.BecomeAsync(PlayerClass.CHANTER, 25);
		int[] table = probe.CatalogOf(PlayerClass.CHANTER);
		Assert.All(new[] { ward, binding, herb, smite, celerity, shieldMantra, revivalMantra, rage, promise, blessing, 1618, 1565, 1705, 1781, 1668, 1817, 1718 }, skill =>
			Assert.True(probe.World.Skills.ContainsKey(skill), $"Skill {skill} was not learned by level 25."));
		await GiveAsync(probe, token, (staff, 1), (OdellaPowder, 20));
		BotNavigationGeometry geometry = await probe.MoveToMapAsync(ClericProbeMap, 1867.3f, 456.0f, 270.2f);
		probe.Session.BeginStep("s02", "equipment-check");
		await probe.Journey.RunObservedEquipmentCheckAsync(token);
		Assert.Equal(staff, MainHand(probe));

		(_, int[] buffCasts) = await BuffCheckStepAsync(probe, "s03", "buff-check-casts-the-buffs-and-turns-the-mantras-on", token);
		Assert.All(new[] { blessing, promise, rage }, buff => Assert.Contains(buff, buffCasts));
		Assert.Equal([shieldMantra, revivalMantra, celerity], probe.World.ActiveToggles.Order());

		var lines = new List<string>();
		int smitesOn = 0, smitesOff = 0, swings = 0;
		for (int fights = 1; fights <= 3; fights++)
		{
			Npc next = NearestLiving(probe, mosbear);
			(int hp, int mp) = (probe.World.CurrentHp, probe.World.CurrentMp);
			ClericFight fight = await TableFightAsync(probe, ChanterTable, $"s04-{fights}", $"fight-starved-mosbear-{fights}", () => Task.FromResult(next.GetObjectId()), token);
			AssertNoRefusedCastRepeats(fight);
			AssertOnlyCasts(fight, table);
			Assert.Equal(0, fight.Result.Deaths);
			Assert.True(fight.Result.Killed || fight.Result.Retreats > 0, $"Fight {fights} ended neither way: {fight.Result}.");
			StarterTraceRecord[] smites = fight.Decided.Where(record => Decided(record, "cast-target", smite)).ToArray();
			smitesOn += smites.Count(record => record.Fields.GetProperty("observedState").GetProperty("TargetAdjacent").GetBoolean());
			smitesOff += smites.Count(record => !record.Fields.GetProperty("observedState").GetProperty("TargetAdjacent").GetBoolean());
			swings += Swings(fight);
			lines.Add($"fight {fights}: {Outcome(fight)}, HP {hp} to {probe.World.CurrentHp}/{probe.World.MaxHp}, MP {mp} to {probe.World.CurrentMp}/{probe.World.MaxMp}");
		}

		Npc hurtTarget = NearestLiving(probe, mosbear);
		ClericFight hurt = await TableFightAsync(probe, ChanterTable, "s05", "fight-a-starved-mosbear-from-just-over-half-hp", async () =>
		{
			// Prepared by the director, after the journey's own rest: 55% HP as the fight begins.
			await probe.CutHpAsync(55);
			return hurtTarget.GetObjectId();
		}, token);
		AssertNoRefusedCastRepeats(hurt);
		AssertOnlyCasts(hurt, table);
		StarterTraceRecord[] warded = hurt.Decided.Where(record => Decided(record, "cast-self", ward)).ToArray();
		Assert.True(warded.Length > 0, $"Protective Ward was not cast: {hurt.Order}; decisions {hurt.Counts}.");
		Assert.All(warded, record => Assert.True(HpPercentAt(record) <= 60, $"Protective Ward was decided at {HpPercentAt(record)}% HP."));
		Assert.Contains(hurt.Casts, cast => cast.SkillId == ward);
		Assert.Equal(0, hurt.Result.Deaths);

		Npc[] pack = [];
		ClericFight swarm = await TableFightAsync(probe, ChanterTable, "s06", "fight-a-pack-of-three-starved-mosbears", async () =>
		{
			// Prepared by the director, after the journey's own rest: three starved mosbears 4 m away, set on the Chanter.
			pack = await SpawnSetOnAsync(probe, geometry, mosbear, 3, token);
			return pack[0].GetObjectId();
		}, token);
		RemoveSetOn(pack);
		AssertOnlyCasts(swarm, table);
		int bound = Array.FindIndex(swarm.Decided, record => Decided(record, "cast-target", binding));
		Assert.True(bound >= 0, $"Binding Word was not cast: {swarm.Order}; decisions {swarm.Counts}.");
		string? reason = swarm.Decided[bound].Fields.GetProperty("reason").GetString();
		Assert.StartsWith("Hold the target before retreating", reason);
		Assert.Contains(swarm.Decided.Skip(bound + 1), record => record.Fields.GetProperty("action").GetString() == "retreat");

		// Prepared by the director: half HP. The rest is the journey's.
		probe.Session.BeginStep("s07", "director-halves-hp");
		if (!probe.Server.IsDead()) await probe.CutHpAsync(50);
		(_, int[] restCasts) = await RestStepAsync(probe, "s08", "rest-from-half-hp-with-odella-powder", token);
		Assert.True(restCasts.Contains(herb) || restCasts.Contains(recovery), $"The rest cast no fourth-rank powder skill: {string.Join(" ", restCasts)}.");
		Assert.True(probe.Owned(OdellaPowder) < 20, "No Odella Powder was spent.");
		Assert.True(probe.World.CurrentHp * 100 >= probe.World.MaxHp * 90, $"The rest ended at {probe.World.CurrentHp}/{probe.World.MaxHp}.");
		Console.WriteLine($"{id}: level {probe.World.Level} Chanter, max HP {probe.World.MaxHp}, MP {probe.World.MaxMp}. Buff check: casts {string.Join(" ", buffCasts)}; " +
			$"toggles on {string.Join(" ", probe.World.ActiveToggles.Order())}. " + string.Join("; ", lines) +
			$". Smite decided {smitesOff} time(s) from range and {smitesOn} with the mosbear on the Chanter; the staff swung {swings} time(s). From 55% HP: {Outcome(hurt)}; Protective Ward decided at " +
			$"{string.Join(", ", warded.Select(HpPercentAt))}% HP. Then against {pack.Length} the director spawned 4 m away and set on it: {Outcome(swarm)}; Binding Word decided with " +
			$"{swarm.Decided[bound].Fields.GetProperty("observedState").GetProperty("NearbyAggressors").GetInt32()} attackers: {reason} " +
			$"Rest: casts {string.Join(" ", restCasts)}, Odella Powder {probe.Owned(OdellaPowder)} of 20 left. " +
			$"HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}, MP {probe.World.CurrentMp}/{probe.World.MaxMp}, dead {probe.Server.IsDead()}.");
	}

	/// <summary>The living monster of a template nearest to another monster.</summary>
	private Npc NearestLiving(StarterProbe probe, int templateId, Npc near) => probe.World.Objects.Values
		.Where(known => known.TemplateId == templateId)
		.Select(known => fixture.World.FindVisibleObject(known.ObjectId)).OfType<Npc>().Where(npc => !npc.IsDead())
		.OrderBy(npc => MathF.Abs(npc.GetX() - near.GetX()) + MathF.Abs(npc.GetY() - near.GetY()))
		.First();
}
