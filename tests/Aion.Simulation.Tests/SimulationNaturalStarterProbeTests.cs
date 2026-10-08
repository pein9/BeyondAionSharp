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

		/// <summary>The director places the character on its map; the client takes the position the teleport gave it.</summary>
		public async Task PlaceAsync(float x, float y, float z)
		{
			await placeAsync(x, y, z);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
		}

		public long Owned(int itemId) => session.Api.World.Inventory.Values.Where(item => item.ItemId == itemId).Sum(item => item.Count);

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
		await using var dashboardHost = new LiveBotDashboardHost(run, [id], dashboard,
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

	/// <summary>One fight of a Cleric row, with what the trace says of it.</summary>
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
	private static async Task<ClericFight> ClericFightAsync(StarterProbe probe, string step, string name, Func<Task<int>> target, CancellationToken token)
	{
		probe.Session.BeginStep(step, name);
		NaturalCombatDiagnosticResult result = await probe.Journey.RunObservedCombatAsync(_ => target(), token);
		IReadOnlyList<StarterTraceRecord> records = probe.TraceOf(step);
		StarterTraceRecord[] decided = DecidedIn(records);
		Assert.NotEmpty(decided);
		Assert.All(decided, record => Assert.StartsWith(ClericTable, record.Fields.GetProperty("policyVersion").GetString()));
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

	/// <summary>The living monster of a template nearest to another monster.</summary>
	private Npc NearestLiving(StarterProbe probe, int templateId, Npc near) => probe.World.Objects.Values
		.Where(known => known.TemplateId == templateId)
		.Select(known => fixture.World.FindVisibleObject(known.ObjectId)).OfType<Npc>().Where(npc => !npc.IsDead())
		.OrderBy(npc => MathF.Abs(npc.GetX() - near.GetX()) + MathF.Abs(npc.GetY() - near.GetY()))
		.First();
}
