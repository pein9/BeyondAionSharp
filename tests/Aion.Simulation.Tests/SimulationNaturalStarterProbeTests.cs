using System.Text.Json;
using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private sealed record StarterProbeRow(string Name, NaturalClassLine Line, int Account, string CharacterName);

	// CP-42: one row for now. A later item adds its row here; each row plays on one of the two probe accounts.
	private static readonly StarterProbeRow[] StarterProbeRows =
	[
		new("warrior-rest", NaturalClassLine.Warrior, ProbeAccountA, "Asimrestwar"),
	];

	public static TheoryData<string> StarterProbeRowNames => new(StarterProbeRows.Select(row => row.Name));

	/// <summary>
	/// CP-42: what every row of the starter field probe starts from. Prepared here, and nowhere in a journey: a character
	/// of the row's class line created by packets on a probe account, with no kit supplied, and a journey runtime over its
	/// session. The director's two setup acts, a level change and an HP cut, are the only GM input a row may add.
	/// </summary>
	private sealed class StarterProbe(SimulationWorldFixture fixture, SimulationL0Session session, Player player, NaturalJourneyRuntime runtime,
		NaturalClassLine line, string tracePath, CancellationToken token)
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

		public long Owned(int itemId) => session.Api.World.Inventory.Values.Where(item => item.ItemId == itemId).Sum(item => item.Count);

		/// <summary>The trace records of one step: client packets by name, and diagnostics by kind.</summary>
		public IReadOnlyList<(string Direction, string Packet)> TraceOf(string step)
		{
			var records = new List<(string, string)>();
			using var stream = new FileStream(tracePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			using var reader = new StreamReader(stream);
			while (reader.ReadLine() is { } line)
			{
				using JsonDocument record = JsonDocument.Parse(line);
				JsonElement root = record.RootElement;
				if (root.TryGetProperty("step", out JsonElement at) && at.GetString() == step)
					records.Add((root.GetProperty("dir").GetString() ?? "", root.GetProperty("packet").GetString() ?? ""));
			}
			return records;
		}
	}

	/// <summary>
	/// CP-42, row warrior-rest: the potion-and-sit rest of CP-37, played. A level-1 Warrior created by packets rests twice,
	/// and before each rest the director halves its HP. The first rest drinks one of the 100 Minor Life Potions a starter
	/// owns. The second cut comes while the potion's 30 s delay still runs, so that rest sits to the HP target and drinks
	/// nothing.
	/// </summary>
	[SkippableTheory]
	[MemberData(nameof(StarterProbeRowNames))]
	public async Task NaturalStarterFieldProbe(string rowName)
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		Skip.IfNot(ProbeRowNamed(rowName), $"Row {rowName} is not named in CP_PROBE_ROWS.");
		StarterProbeRow row = StarterProbeRows.Single(candidate => candidate.Name == rowName);
		string id = "CP42-" + row.Name;
		using var policy = NewEconomyPolicy(id, includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
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
		var probe = new StarterProbe(fixture, session, player, runtime, row.Line, tracePath, token);
		switch (row.Name)
		{
			case "warrior-rest": await WarriorRestRowAsync(probe, id, token); break;
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
		Assert.Equal(1, first.Count(record => record is ("action", "rest-life-potion")));
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
		Assert.DoesNotContain(second, record => record is ("action", "rest-life-potion"));
		int sits = second.Count(record => record is ("action", "rest-sit-for-health"));
		Assert.True(sits >= 1, "The second rest did not sit for health.");
		Assert.True(probe.World.CurrentHp * 100 >= probe.World.MaxHp * 90, $"The second rest ended at {probe.World.CurrentHp}/{probe.World.MaxHp}.");
		Assert.False(probe.Server.IsDead());
		Console.WriteLine($"{id}: level {probe.World.Level} Warrior, max HP {probe.World.MaxHp}. First rest: one Minor Life Potion " +
			$"({owned} to {probe.Owned(potion)}), {first.Count(record => record is ("action", "rest-sit-for-health"))} sits for health, {firstTook} ms. " +
			$"Second rest, {secondStarted - firstStarted} ms after the first began: no potion, {sits} sits for health, {secondTook} ms. " +
			$"HP at the end {probe.World.CurrentHp}/{probe.World.MaxHp}.");
	}
}
