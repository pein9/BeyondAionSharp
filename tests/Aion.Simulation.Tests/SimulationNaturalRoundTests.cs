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
	/// <summary>
	/// One bot of a round: its class line, when it starts, how far it plays, and a stop on purpose.
	/// NR-47: <c>Account</c> and <c>Name</c> are the seat's own when given; a seat's account is otherwise 111 and up, and its
	/// name its line's. <c>ResumeCharacter</c> is a character of the round snapshot the world was restored from: the bot
	/// logs it in on its account instead of creating one.
	/// </summary>
	private sealed record NaturalRoundBot(string Line, double StartAfterMinutes = 0, string? StopAt = null, double? StopAfterMinutes = null,
		int? Account = null, string? Name = null, int? ResumeCharacter = null);

	private sealed record NaturalRoundFile(NaturalRoundBot[] Bots);

	private static readonly JsonSerializerOptions RoundJson = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

	/// <summary>
	/// NR-44 (docs/natural-all-classes-ntc.md, rule (w)): several bots, each playing alone, in one world in one run.
	/// NR_ROUND_FILE names a round file: { "bots": [ { "line": "mage", "startAfterMinutes": 0, "stopAt": "2004:5:0" } ] }.
	/// Each bot is a class line's own character, created by packets. The bots take turns on the world's clock
	/// (<see cref="SimulationTurnTable"/>). Each has a folder of its own under the run's evidence, named for its line,
	/// with its trace, the journey's receipts and outcome.json. A bot that stops is an outcome, not a failure of the round.
	/// NR-47: round-outcome.json also has the world's clock and each character's id, account and name, which is what a
	/// round snapshot records and what the next round resumes from (scripts/sim/run-round.ps1).
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
		// round gives each seat an account of its own. The character has its line's name unless the seat names it: the
		// Cleric's and the Chanter's lines share a name, and a world holds a name once.
		int[] accounts = round.Bots.Select((bot, index) => bot.Account ?? RoundAccountBase + index).ToArray();
		string[] names = round.Bots.Select((bot, index) => string.IsNullOrWhiteSpace(bot.Name) ? lines[index].CharacterName : bot.Name!).ToArray();
		if (names.Distinct(StringComparer.Ordinal).Count() != names.Length)
			throw new InvalidDataException("Two bots of this round would have the same character name; a world holds a name once.");
		if (accounts.Distinct().Count() != accounts.Length || accounts.Any(account => account is < 1 or > 250))
			throw new InvalidDataException("Each seat of a round needs an account of its own between 1 and 250.");

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
				async token => outcomes[seat] = await PlayRoundBotAsync(turns, bot, accounts[seat], names[seat], line, entry, run, evidence, dashboard, token),
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
				fixture.Clock.NowMillis, fixture.Clock.NowMillis, accounts[index], names[index], round.Bots[index].ResumeCharacter ?? 0);
			NaturalRoundOutcome outcome = outcomes[index]!;
			Console.WriteLine($"Natural round {run}: {outcome.Bot} {outcome.Line} {outcome.Outcome}, level {outcome.Level}, " +
				$"{outcome.CompletedQuests} quests, game time {TimeSpan.FromMilliseconds(outcome.EndedAtMillis - outcome.StartedAtMillis):c}" +
				(outcome.Message == null ? "" : $": {outcome.Message}"));
		}
		// The world's clock: what it had when it was restored, and what this round played on top.
		long restoredAt = long.TryParse(Environment.GetEnvironmentVariable("AION_SIM_NI08_ELAPSED_MS"), out long elapsed) && elapsed >= 0 ? elapsed : 0;
		File.WriteAllText(Path.Combine(evidence, "round-outcome.json"),
			JsonSerializer.Serialize(new { run, seed = fixture.Seed, worldElapsedMillis = restoredAt + fixture.Clock.NowMillis, bots = outcomes }, RoundJson));
	}

	/// <summary>The account of a round's first seat; the next seats follow. Clear of the solo account 41 and the probe accounts.</summary>
	private const int RoundAccountBase = 111;

	private sealed record NaturalRoundOutcome(string Bot, string Line, string Outcome, string? Exception, string? Message, string? Step,
		int Level, int CompletedQuests, long StartedAtMillis, long EndedAtMillis, int Account, string Name, int CharacterId);

	/// <summary>One bot of a round: the journey's own test, for a class line's fresh character, with a turn at the clock.</summary>
	private async Task<NaturalRoundOutcome> PlayRoundBotAsync(SimulationTurnTable turns, string bot, int account, string name, NaturalClassLine line,
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
				fixture, policy, bot, accountId: account, name, Race.ASMODIANS,
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
				string starter = line.Starter.ToString().ToLowerInvariant();
				bool resuming = entry.ResumeCharacter is > 0;
				playing.BeginStep(resuming ? "ni08-login-existing" : "ni07-create",
					resuming ? $"reconstruct-retained-{starter}" : $"create-natural-asmodian-{starter}");
				if (resuming)
				{
					// NR-47: the character of a round snapshot, on the account and under the name it was made with.
					int retainedId = entry.ResumeCharacter!.Value;
					var list = await playing.LoginCharacterListAsync(enterToken);
					var retained = list.Get<List<IReadOnlyDictionary<string, object?>>>("characters")
						.SingleOrDefault(c => Get<int>(c, "objectId") == retainedId)
						?? throw new InvalidDataException($"The round's character {retainedId} is missing; refusing to create a replacement.");
					if (Get<string>(retained, "name") != name || Get<int>(retained, "race") != (int)Race.ASMODIANS ||
						Get<int>(retained, "deletionTimeSeconds") != 0)
						throw new InvalidDataException($"The round's character {retainedId} is not {name} as its snapshot recorded it.");
					NaturalJourneyIdentityRules.Classify(line, Get<int>(retained, "playerClass"), Get<ushort>(retained, "level"),
						Get<int>(retained, "mapId"), playing.IdentityAltgardLegId);
					playing.SelectCharacter(retainedId, name);
				}
				else
				{
					await playing.LoginAndAuthenticateAsync(enterToken);
					await playing.CreateCharacterAsync(enterToken, line.Starter);
				}
				await playing.EnterWorldAsync(enterToken);
				if (!resuming) await playing.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), enterToken);
				await playing.SynchronizeAsync(enterToken);
				var entered = fixture.World.GetPlayer(playing.CharacterId);
				NaturalJourneyIdentityRules.Classify(line, entered.GetPlayerClass(), entered.GetLevel(), entered.GetWorldId(), playing.IdentityAltgardLegId);
				if (!resuming) Assert.Equal(line.Starter, entered.GetPlayerClass());
				return resuming;
			}
		}
		catch (Exception ended)
		{
			// A bot that stops ends alone. What stopped it is its outcome; the others go on.
			stop = ended;
		}
		var record = new NaturalRoundOutcome(bot, line.Id, outcome, stop?.GetType().Name, stop?.Message, session?.CurrentStep,
			session?.Api.World.Level ?? 0, session?.Api.World.CompletedQuestIds.Count ?? 0, began, fixture.Clock.NowMillis,
			account, name, session?.CharacterId ?? entry.ResumeCharacter ?? 0);
		File.WriteAllText(Path.Combine(folder, "outcome.json"), JsonSerializer.Serialize(record, RoundJson));
		return record;
	}
}
