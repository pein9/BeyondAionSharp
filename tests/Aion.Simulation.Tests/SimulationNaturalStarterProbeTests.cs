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
		NaturalClassLine line, string tracePath, Func<float, float, float, Task> placeAsync, CancellationToken token)
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
			(x, y, z) => TeleportForSetupAsync(session, player, player.GetWorldId(), x, y, z, token), token);
		switch (row.Name)
		{
			case "warrior-rest": await WarriorRestRowAsync(probe, id, token); break;
			case "warrior-1": await WarriorLevelOneRowAsync(probe, id, geometry, token); break;
			case "warrior-7": await WarriorLevelSevenRowAsync(probe, id, geometry, token); break;
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
}
