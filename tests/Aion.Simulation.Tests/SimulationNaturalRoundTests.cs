using System.Text.Json;
using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.Tracing;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>One bot of a round: its class line, when it starts, how far it plays, and a stop on purpose.</summary>
	private sealed record NaturalRoundBot(string Line, double StartAfterMinutes = 0, string? StopAt = null, double? StopAfterMinutes = null);

	private sealed record NaturalRoundFile(NaturalRoundBot[] Bots);

	private static readonly JsonSerializerOptions RoundJson = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

	/// <summary>
	/// NR-44 (docs/natural-all-classes-ntc.md, rule (w)): several bots, each playing alone, in one world in one run.
	/// NR_ROUND_FILE names a round file: { "bots": [ { "line": "mage", "startAfterMinutes": 0, "stopAt": "2004:5:0" } ] }.
	/// Each bot is a class line's own character, created by packets. The bots take turns on the world's clock
	/// (<see cref="SimulationTurnTable"/>). Each has a folder of its own under the run's evidence, named for its line,
	/// with its trace, the journey's receipts and outcome.json. A bot that stops is an outcome, not a failure of the round.
	/// </summary>
	[SkippableFact]
	public async Task NaturalRoundPlaysItsBotsInOneWorld()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		string? file = Environment.GetEnvironmentVariable("NR_ROUND_FILE");
		Skip.If(string.IsNullOrWhiteSpace(file), "Set NR_ROUND_FILE to a round file to play several bots in one world.");
		NaturalRoundFile round = JsonSerializer.Deserialize<NaturalRoundFile>(File.ReadAllText(file!), RoundJson)
			?? throw new InvalidDataException("The round file is empty.");
		if (round.Bots is not { Length: > 0 }) throw new InvalidDataException("The round file names no bot.");
		NaturalClassLine[] lines = round.Bots.Select(bot => NaturalClassLine.Parse(bot.Line)).ToArray();
		// Every class line plays on the one SIM account of a run alone (CP-Q19), and an account is in a world once. So a
		// round gives each seat an account of its own. The character keeps its line's name, which a world holds once.
		if (lines.Select(line => line.CharacterName).Distinct(StringComparer.Ordinal).Count() != lines.Length)
			throw new InvalidDataException("Two bots of this round would have the same character name; a world holds a name once.");

		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "natural-round";
		string evidence = Environment.GetEnvironmentVariable("AION_NI07_COMBAT_DIR") ??
			Path.Combine(Aion.GameServer.TestKit.RealStaticData.RepoRoot(), "run", "natural-round");
		Directory.CreateDirectory(evidence);
		var dashboard = new LiveBotDashboardState();
		await using var dashboardHost = LiveBotDashboardHost.OpenFirstFree(run, ["NR-ROUND"], dashboard,
			int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880"));
		if (dashboardHost.Enabled)
		{
			Console.WriteLine($"Natural round dashboard: {dashboardHost.Url}");
			File.WriteAllText(Path.Combine(evidence, "monitor.json"), $"{{\"url\":\"{dashboardHost.Url}\",\"port\":{dashboardHost.Port}}}");
		}

		// One journey has 45 minutes of real time. The bots of a world play one after another on one thread.
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(45 * round.Bots.Length));
		var turns = new SimulationTurnTable(fixture.Clock);
		var outcomes = new NaturalRoundOutcome?[round.Bots.Length];
		for (int index = 0; index < round.Bots.Length; index++)
		{
			int seat = index;
			NaturalRoundBot entry = round.Bots[index];
			NaturalClassLine line = lines[index];
			string bot = $"b{index + 1:00}";
			turns.Add(bot, TimeSpan.FromMinutes(entry.StartAfterMinutes),
				async token => outcomes[seat] = await PlayRoundBotAsync(turns, bot, RoundAccountBase + seat, line, entry, run, evidence, dashboard, token),
				entry.StopAfterMinutes is double stop ? TimeSpan.FromMinutes(stop) : null);
		}

		// The round has a thread of its own: a bot's continuation must never run on a thread of the test runner.
		var done = new TaskCompletionSource<IReadOnlyList<(string Bot, Task Played)>>(TaskCreationOptions.RunContinuationsAsynchronously);
		var thread = new Thread(() =>
		{
			try { done.SetResult(turns.Run(timeout.Token)); }
			catch (Exception fault) { done.SetException(fault); }
		}) { IsBackground = true, Name = "natural-round" };
		thread.Start();
		IReadOnlyList<(string Bot, Task Played)> played = await done.Task;

		for (int index = 0; index < round.Bots.Length; index++)
		{
			// A bot the table ended before it began (stopped on purpose, or a fault of the world) wrote no record.
			outcomes[index] ??= new NaturalRoundOutcome($"b{index + 1:00}", lines[index].Id, "stopped", played[index].Played.Status.ToString(),
				played[index].Played.Exception?.GetBaseException().Message ?? "The round ended this bot before it began.", null, 0, 0,
				fixture.Clock.NowMillis, fixture.Clock.NowMillis);
			NaturalRoundOutcome outcome = outcomes[index]!;
			Console.WriteLine($"Natural round {run}: {outcome.Bot} {outcome.Line} {outcome.Outcome}, level {outcome.Level}, " +
				$"{outcome.CompletedQuests} quests, game time {TimeSpan.FromMilliseconds(outcome.EndedAtMillis - outcome.StartedAtMillis):c}" +
				(outcome.Message == null ? "" : $": {outcome.Message}"));
		}
		File.WriteAllText(Path.Combine(evidence, "round-outcome.json"),
			JsonSerializer.Serialize(new { run, seed = fixture.Seed, bots = outcomes }, RoundJson));
	}

	/// <summary>The account of a round's first seat; the next seats follow. Clear of the solo account 41 and the probe accounts.</summary>
	private const int RoundAccountBase = 111;

	private sealed record NaturalRoundOutcome(string Bot, string Line, string Outcome, string? Exception, string? Message, string? Step,
		int Level, int CompletedQuests, long StartedAtMillis, long EndedAtMillis);

	/// <summary>One bot of a round: the journey's own test, for a class line's fresh character, with a turn at the clock.</summary>
	private async Task<NaturalRoundOutcome> PlayRoundBotAsync(SimulationTurnTable turns, string bot, int account, NaturalClassLine line,
		NaturalRoundBot entry, string run, string evidence, LiveBotDashboardState dashboard, CancellationToken token)
	{
		string folder = Path.Combine(evidence, line.Id);
		Directory.CreateDirectory(folder);
		string tracePath = Path.Combine(folder, $"{line.Id}.trace.jsonl");
		long began = fixture.Clock.NowMillis;
		using var trace = BotActionTraceWriter.Open(tracePath, run, bot, $"sim-player-{account}",
			virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		using var policy = NewPolicy($"NR-ROUND-{line.Id}", includeHistory: true);
		SimulationL0Session? session = null;
		string outcome = "stopped";
		Exception? stop = null;
		try
		{
			await using var playing = session = new SimulationL0Session(
				fixture, policy, bot, accountId: account, line.CharacterName, Race.ASMODIANS,
				trace, tracePath) { IdentityClassLine = line, Turns = turns };
			playing.Dashboard = dashboard;
			var legSupplied = new Dictionary<int, long>();
			var runtime = new NaturalJourneyRuntime(Aion.GameServer.TestKit.RealStaticData.RepoRoot(),
				"SIM-natural-round", fixture.Seed, fixture.DataManager.StaticData, () => fixture.Clock.NowMillis, fixture.Epoch,
				() => BotNavigationGeometry.ForServerWorld(fixture.World.GetPlayer(playing.CharacterId).GetInstanceId(), Race.ASMODIANS),
				EnterAsync, policy.AssertClean, () => policy.SnapshotProblems(), trace, dashboard)
			{
				SupplyHelpItemAsync = NaturalHelpItemSupply.Enabled(Environment.GetEnvironmentVariable(NaturalHelpItemSupply.Switch))
					? SupplyHelpItemAsync : null,
			};
			await new NaturalIshalgenJourney(playing, runtime, new NaturalJourneyOptions(StopAt: entry.StopAt, ClassLine: line)).RunAsync(token);
			outcome = "reached";

			async Task SupplyHelpItemAsync(int itemId, long count, CancellationToken supplyToken)
			{
				NaturalHelpItemSupply.RequireApproved(itemId, count, playing.IdentityAltgardLegId, legSupplied.GetValueOrDefault(itemId));
				legSupplied[itemId] = legSupplied.GetValueOrDefault(itemId) + count;
				var player = fixture.World.GetPlayer(playing.CharacterId);
				Assert.Equal(0, Aion.GameServer.Services.Items.ItemService.AddItem(player, itemId, count, allowInventoryOverflow: true));
				await playing.SynchronizeAsync(supplyToken);
			}

			async Task<bool> EnterAsync(CancellationToken enterToken)
			{
				playing.BeginStep("ni07-create", $"create-natural-asmodian-{line.Starter.ToString().ToLowerInvariant()}");
				await playing.LoginAndAuthenticateAsync(enterToken);
				await playing.CreateCharacterAsync(enterToken, line.Starter);
				await playing.EnterWorldAsync(enterToken);
				await playing.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), enterToken);
				await playing.SynchronizeAsync(enterToken);
				var entered = fixture.World.GetPlayer(playing.CharacterId);
				NaturalJourneyIdentityRules.Classify(line, entered.GetPlayerClass(), entered.GetLevel(), entered.GetWorldId(), playing.IdentityAltgardLegId);
				Assert.Equal(line.Starter, entered.GetPlayerClass());
				return false;
			}
		}
		catch (Exception ended)
		{
			// A bot that stops ends alone. What stopped it is its outcome; the others go on.
			stop = ended;
		}
		var record = new NaturalRoundOutcome(bot, line.Id, outcome, stop?.GetType().Name, stop?.Message, session?.CurrentStep,
			session?.Api.World.Level ?? 0, session?.Api.World.CompletedQuestIds.Count ?? 0, began, fixture.Clock.NowMillis);
		File.WriteAllText(Path.Combine(folder, "outcome.json"), JsonSerializer.Serialize(record, RoundJson));
		return record;
	}
}
