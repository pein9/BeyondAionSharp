using System.Text.Json;
using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.Tracing;
using Aion.GameServer.Custom.Companion;
using Aion.GameServer.Dao;
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
	/// NR-R1a: <c>Stage</c> is how far the bot plays. Unset, it is the plain journey, to Munin. "bridge" goes on through the
	/// trial, the class choice, the ceremony, the capital pass and the dispatch to the Altgard bind.
	/// NR-R3a: "l1" to "l11", "cg", "l12" and "ax" are the Altgard legs, the coin gear, Haramel and the Abyss entry, each
	/// played by a character that stands at the end of the stage before it. "muster" follows "ax" (docs/natural-muster.md).
	/// Companion bots (docs/companion-bots.md): "companion-lead" is the test's stand-in for the player, which asks for
	/// <c>Companions</c> and plays as far as <c>CompanionUntil</c>. "companion" is a seat of the host: an account with no
	/// character and no class line (its <c>Line</c> says "companion"), which logs in, waits at character selection for
	/// the server to make a companion on it, and then plays that character (docs/companion-live-host.md).
	/// </summary>
	private sealed record NaturalRoundBot(string Line, double StartAfterMinutes = 0, string? StopAt = null, double? StopAfterMinutes = null,
		int? Account = null, string? Name = null, int? ResumeCharacter = null, string? Stage = null, int? TargetNpcId = null,
		string? Role = null, string? SupportInvite = null, bool AcceptSupportInvite = false,
		string[]? GroupRoster = null, string? GroupLeader = null, bool GroupLoot = false, string? GroupUntil = null,
		string[]? Companions = null, string? CompanionUntil = null, string? CompanionEnd = null,
		string? CompanionTest = null, int? CompanionStockMinutes = null, int[]? GroupQuests = null, int? GroupBind = null,
		NaturalGroupKill[]? GroupKills = null, NaturalGroupInstance? GroupInstance = null, NaturalGroupBoss[]? GroupBosses = null,
		string? SoloCompanion = null, int[]? OnlyQuests = null);

	/// <summary>A seat of the companion host; it has no class line until the server makes its character.</summary>
	private static bool IsCompanionSeat(NaturalRoundBot entry) => entry.Stage == "companion";

	/// <summary>
	/// NR-R1a: the journey's options for a bot's stage. A run of one bot takes them from its environment
	/// (NaturalIshalgenPriestCompletesFrozenJourneyWithoutSetup: NA_ASCENSION and the rest); a round gives them for each bot
	/// from its round file (Survey C1, part 7). The bridge is what NA_ASCENSION=1 is there.
	/// </summary>
	private static NaturalJourneyOptions RoundOptions(NaturalRoundBot entry, NaturalClassLine line)
	{
		if (entry.Role is not (null or "" or "healer")) throw new InvalidDataException($"Unknown bot role '{entry.Role}'.");
		return (entry.Stage switch
		{
		null or "" => new NaturalJourneyOptions(StopAt: entry.StopAt, ClassLine: line),
		"bridge" => new NaturalJourneyOptions(StopAt: entry.StopAt, AscensionBridge: true, ClassLine: line),
		// NR-R3a: a leg, as sim-snapshot.ps1 -AltgardLeg1 -Leg <id> -LaterCapital plays it for one bot. Leg 1 starts on the
		// bridge's end, which is no later-capital snapshot: AF_ALTGARD=1 and RC_CAPITAL=1 alone. Every later stage starts on
		// a later-capital one and has NA_ASCENSION=1 as well (the script's Restore-Snapshot and Get-LegEnvironment).
		"l1" => new NaturalJourneyOptions(StopAt: entry.StopAt, AltgardLeg1: true, LaterCapital: true, ClassLine: line),
		// M2 to M7: "mf", "mk", "md", "mc", "ms", "ma" and "mg" are the Morheim legs (docs/natural-morheim-leveling.md), from the group world.
		// NL-2: "mb" and "bs" take the bots to Brusthonin and play its Settlers Campsite (docs/natural-leg-36.md); NL-3: "rw"
		// takes them into Reshanta, "rp" plays Primum's quests, "re" the east, "rs" the south, "ru" the upper layer, "rq" the siege quests, "rt" Tigraki
		// "ro" the errands out of the Abyss and "rb" Q2773 to Beluslan; NL-4: "va" Alsig Village's quests from 31 to 35 and
		// "vo" Morheim Observatory's, "vh" Halabana Outpost's, "vs" the campaign's Q24023 in the Sky Temple Interior.
		"l2" or "l3" or "l4" or "l5" or "l6" or "l7" or "l8" or "l9" or "l10" or "l11" or "cg" or "l12" or "ax" or "mf" or "mk" or "md" or "mc" or "ms" or "ma" or "mg" or "mgk" or "mgd" or "mgs" or "mb" or "bs" or "rw" or "rp" or "re" or "rs" or "ru" or "rq" or "rt" or "ro" or "rb" or "va" or "vo" or "vh" or "vs" =>
			new NaturalJourneyOptions(StopAt: entry.StopAt, AscensionBridge: true, AltgardLegId: entry.Stage, LaterCapital: true, ClassLine: line),
		// The muster (2026-10-12): a character that stands at the Abyss entry's end, in that leg's fortress.
		"muster" => new NaturalJourneyOptions(StopAt: entry.StopAt, AscensionBridge: true, AltgardLegId: NaturalAbyssEntry.Leg, LaterCapital: true,
			ClassLine: line, Muster: true),
		// MR-i, step M7 (docs/natural-morheim-leveling.md): the gear check-in at 31, the muster's coin step in the Silver Coin
		// tier, by a character the Morheim legs brought to the fortress at that level.
		"check-in" => new NaturalJourneyOptions(StopAt: entry.StopAt, AscensionBridge: true, AltgardLegId: NaturalAbyssEntry.Leg, LaterCapital: true,
			ClassLine: line, Muster: true, CheckInLevel: 31),
		// NL-4 (docs/natural-leg-36.md): the gear check-in at 36, Elite Rank 5 from the same Silver Coin vendors, by a character
		// leg mg brought back to the fortress.
		"check-in-36" => new NaturalJourneyOptions(StopAt: entry.StopAt, AscensionBridge: true, AltgardLegId: NaturalAbyssEntry.Leg, LaterCapital: true,
			ClassLine: line, Muster: true, CheckInLevel: 36),
		// GH-01: one uncaptured field encounter from the group world's muster endpoint, with the ordinary fight table.
		"gh-survey" => new NaturalJourneyOptions(AscensionBridge: true, AltgardLegId: NaturalAbyssEntry.Leg, LaterCapital: true,
			ClassLine: line, Muster: true, SurveyNpcId: entry.TargetNpcId ?? 212644),
		"support" => new NaturalJourneyOptions(AscensionBridge: true, AltgardLegId: NaturalAbyssEntry.Leg, LaterCapital: true,
			ClassLine: line, SupportWatch: true),
		"group-coin" => new NaturalJourneyOptions(AscensionBridge: true, AltgardLegId: NaturalAbyssEntry.Leg, LaterCapital: true,
			ClassLine: line, GroupRoster: entry.GroupRoster ?? throw new InvalidDataException("group-coin needs a roster."),
			GroupLeader: entry.GroupLeader ?? throw new InvalidDataException("group-coin needs a leader."), GroupLoot: entry.GroupLoot),
		// The Group NTC leg (docs/natural-group-ntc.md), played as far as groupUntil names.
		"group-ntc" => new NaturalJourneyOptions(AscensionBridge: true, AltgardLegId: NaturalAbyssEntry.Leg, LaterCapital: true,
			ClassLine: line, GroupRoster: entry.GroupRoster ?? throw new InvalidDataException("group-ntc needs a roster."),
			GroupLeader: entry.GroupLeader ?? throw new InvalidDataException("group-ntc needs a leader."), GroupLoot: entry.GroupLoot,
			GroupStage: "ntc", GroupUntil: entry.GroupUntil),
		// M6 (docs/natural-morheim-leveling.md): a group plays a list of Morheim field quests, bound at groupBind.
		"group-field" => new NaturalJourneyOptions(AscensionBridge: true, AltgardLegId: NaturalAbyssEntry.Leg, LaterCapital: true,
			ClassLine: line, GroupRoster: entry.GroupRoster ?? throw new InvalidDataException("group-field needs a roster."),
			GroupLeader: entry.GroupLeader ?? throw new InvalidDataException("group-field needs a leader."), GroupLoot: entry.GroupLoot,
			GroupStage: "field", GroupQuests: entry.GroupQuests ?? throw new InvalidDataException("group-field names its quests."),
			GroupBind: entry.GroupBind, GroupKills: entry.GroupKills, GroupInstance: entry.GroupInstance, GroupBosses: entry.GroupBosses),
		// The stand-in for the player who leads companions: a test tool, one of the group world's own characters.
		"companion-lead" => new NaturalJourneyOptions(AscensionBridge: true, AltgardLegId: NaturalAbyssEntry.Leg, LaterCapital: true,
			ClassLine: line, Companion: "stand-in", Companions: entry.Companions ?? throw new InvalidDataException("companion-lead names its companions."),
			CompanionUntil: entry.CompanionUntil, CompanionEnd: entry.CompanionEnd),
		_ => throw new InvalidDataException($"Stage '{entry.Stage}' is not a stage of a round: none (the plain journey), bridge, l1 to l11, cg, l12, ax, the Morheim legs, muster, check-in, check-in-36, gh-survey, support, group-coin, group-ntc, group-field, companion-lead or companion."),
		}) with { Role = entry.Role, SoloCompanion = entry.SoloCompanion,
			// NL-2: a diagnostic trial of some of a leg's quests (AM-06's limited run), never a captured round's.
			AltgardOnlyQuests = entry.OnlyQuests };
	}

	/// <summary>
	/// NR-R3a: the receipt the journey writes at a stage's end, beside its trace, once the end has been checked again
	/// across a relog. A run of one bot is captured only when it says verified for the character played
	/// (scripts/sim/sim-snapshot.ps1), and a bot of a round has reached its stage on the same word. Null for the plain
	/// journey and for a bot stopped on purpose, which write none.
	/// </summary>
	private static string? StageReceipt(NaturalRoundBot entry) => entry.Stage == "group-coin" ? "group-coin-completion.json"
		: entry.Stage == "group-field" ? "group-field-completion.json"
		: entry.Stage == "group-ntc" ? "group-ntc-completion.json" : entry.Stage == "companion-lead" ? "companion-completion.json"
		: IsCompanionSeat(entry) ? null : entry.Role == "healer" || entry.StopAt is { Length: > 0 } ? null : entry.Stage switch
	{
		null or "" or "support" => null,
		"bridge" => "bridge-completion.json",
		"muster" or "check-in" or "check-in-36" => NaturalAbyssEntry.MusterReceipt,
		"gh-survey" => "gh-survey-completion.json",
		_ => $"altgard-{entry.Stage}-completion.json",
	};

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
		NaturalClassLine?[] lines = round.Bots.Select(bot => IsCompanionSeat(bot) ? null : NaturalClassLine.Parse(bot.Line)).ToArray();
		// NR-R1a: a stage the round cannot play is refused before any bot begins.
		for (int index = 0; index < round.Bots.Length; index++)
			if (lines[index] is { } checkedLine) _ = RoundOptions(round.Bots[index], checkedLine);
			else if (round.Bots[index].Account == null) throw new InvalidDataException("A companion seat names the account it is played on.");
		// Every class line plays on the one SIM account of a run alone (CP-Q19), and an account is in a world once. So a
		// round gives each seat an account of its own. The character has its line's name unless the seat names it: the
		// Cleric's and the Chanter's lines share a name, and a world holds a name once.
		int[] accounts = round.Bots.Select((bot, index) => bot.Account ?? RoundAccountBase + index).ToArray();
		string[] names = round.Bots.Select((bot, index) => lines[index] is not { } named ? $"companion-{accounts[index]}"
			: string.IsNullOrWhiteSpace(bot.Name) ? named.CharacterName : bot.Name!).ToArray();
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

		// NR-44a: the world's own problems, raised while its clock runs between turns. They are written into the round's
		// record and stop no bot. A bot answers for the problems raised in its own play (PlayRoundBotAsync).
		using var worldPolicy = NewPolicy("NR-ROUND-world", includeHistory: true, owns: string.IsNullOrEmpty);
		// One journey has 45 minutes of real time. The bots of a world play one after another on one thread.
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(45 * round.Bots.Length));
		var turns = new SimulationTurnTable(fixture.Clock);
		var outcomes = new NaturalRoundOutcome?[round.Bots.Length];
		// The host of the companions (docs/companion-live-host.md): this test, with the protocol the live host uses. The
		// server is told the names of the seats' accounts and nothing more; a seat logs in and waits at character
		// selection until a player's request makes a character on its account, and ends when no player is left.
		int[] seatAccounts = [.. round.Bots.Select((bot, index) => (bot, index)).Where(seat => IsCompanionSeat(seat.bot)).Select(seat => accounts[seat.index])];
		int playersLeft = round.Bots.Count(bot => !IsCompanionSeat(bot));
		CompanionService companions = CompanionService.GetInstance();
		if (seatAccounts.Length > 0)
		{
			companions.Enable(seatAccounts: seatAccounts.Select(account => $"sim-player-{account}"));
		}
		for (int index = 0; index < round.Bots.Length; index++)
		{
			int seat = index;
			NaturalRoundBot entry = round.Bots[index];
			string bot = $"b{index + 1:00}";
			if (lines[index] is not { } line)
			{
				turns.Add(bot, TimeSpan.FromMinutes(entry.StartAfterMinutes),
					async token => outcomes[seat] = await PlayCompanionSeatAsync(turns, bot, accounts[seat], entry, run, evidence, dashboard, () => playersLeft == 0, token),
					entry.StopAfterMinutes is double seatStop ? TimeSpan.FromMinutes(seatStop) : null);
				continue;
			}
			turns.Add(bot, TimeSpan.FromMinutes(entry.StartAfterMinutes),
				async token =>
				{
					try { outcomes[seat] = await PlayRoundBotAsync(turns, bot, accounts[seat], names[seat], line, entry, run, evidence, dashboard, token); }
					finally { playersLeft--; }
				},
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
		IReadOnlyList<(string Bot, Task Played)> played;
		// C9: when the round is over the server knows no companion, and no character is left on a seat's account.
		string[] companionsKnown = [];
		var charactersOnSeats = new Dictionary<string, int>();
		try
		{
			played = await done.Task;
			companionsKnown = [.. companions.All().Select(companion => $"{companion.Name}:{companion.State}")];
			foreach (int account in seatAccounts) charactersOnSeats[account.ToString()] = PlayerDAO.GetPlayerOidsOnAccount(account).Count;
		}
		finally
		{
			if (seatAccounts.Length > 0)
			{
				companions.Disable();
			}
		}

		for (int index = 0; index < round.Bots.Length; index++)
		{
			// A bot the table ended before it began (stopped on purpose, or a fault of the world) wrote no record.
			outcomes[index] ??= new NaturalRoundOutcome($"b{index + 1:00}", lines[index]?.Id ?? "companion", "stopped", played[index].Played.Status.ToString(),
				played[index].Played.Exception?.GetBaseException().Message ?? "The round ended this bot before it began.", null, 0, 0,
				fixture.Clock.NowMillis, fixture.Clock.NowMillis, accounts[index], names[index], round.Bots[index].ResumeCharacter ?? 0,
				round.Bots[index].Stage);
			NaturalRoundOutcome outcome = outcomes[index]!;
			Console.WriteLine($"Natural round {run}: {outcome.Bot} {outcome.Line} {outcome.Outcome}, level {outcome.Level}, " +
				$"{outcome.CompletedQuests} quests, game time {TimeSpan.FromMilliseconds(outcome.EndedAtMillis - outcome.StartedAtMillis):c}" +
				(outcome.Message == null ? "" : $": {outcome.Message}"));
		}
		// The world's clock: what it had when it was restored, and what this round played on top.
		long restoredAt = long.TryParse(Environment.GetEnvironmentVariable("AION_SIM_NI08_ELAPSED_MS"), out long elapsed) && elapsed >= 0 ? elapsed : 0;
		var worldProblems = worldPolicy.SnapshotUnallowlisted()
			.Select(problem => new { problem.Fingerprint, problem.Kind, Level = problem.Level.ToString(), problem.Message }).ToArray();
		foreach (var problem in worldProblems)
			Console.WriteLine($"Natural round {run}: a problem of the world, {problem.Kind} {problem.Level}: {problem.Message}");
		File.WriteAllText(Path.Combine(evidence, "round-outcome.json"),
			JsonSerializer.Serialize(new { run, seed = fixture.Seed, worldElapsedMillis = restoredAt + fixture.Clock.NowMillis, worldProblems, bots = outcomes,
				companionSeats = seatAccounts.Length == 0 ? null : new { companionsKnown, charactersOnSeats } }, RoundJson));
		if (companionsKnown.Length > 0 || charactersOnSeats.Values.Any(count => count > 0))
			throw new InvalidDataException($"The round ended with companions left: known [{string.Join(", ", companionsKnown)}], characters on seats " +
				string.Join(", ", charactersOnSeats.Select(seat => $"{seat.Key}:{seat.Value}")) + ".");
	}

	/// <summary>The account of a round's first seat; the next seats follow. Clear of the solo account 41 and the probe accounts.</summary>
	private const int RoundAccountBase = 111;

	private sealed record NaturalRoundOutcome(string Bot, string Line, string Outcome, string? Exception, string? Message, string? Step,
		int Level, int CompletedQuests, long StartedAtMillis, long EndedAtMillis, int Account, string Name, int CharacterId, string? Stage = null);

	/// <summary>
	/// A seat of the companion host, with the client protocol and nothing else. It logs in on its account and waits at
	/// character selection, asking for its character list once a second. A character in the list is a companion the
	/// server made on this seat: it enters the world with it and plays it until its play ends, goes back to character
	/// selection, where the server deletes the companion, and waits again. It ends when no player of the round is left.
	/// </summary>
	private async Task<NaturalRoundOutcome> PlayCompanionSeatAsync(SimulationTurnTable turns, string bot, int account, NaturalRoundBot entry,
		string run, string evidence, LiveBotDashboardState dashboard, Func<bool> playersDone, CancellationToken token)
	{
		// A seat has a folder of its own: it may play several companions, one after another.
		string folder = Path.Combine(evidence, $"companion-seat-{account}");
		Directory.CreateDirectory(folder);
		string tracePath = Path.Combine(folder, "seat.trace.jsonl");
		long began = fixture.Clock.NowMillis;
		using var trace = BotActionTraceWriter.Open(tracePath, run, bot, $"sim-player-{account}",
			virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		using var policy = NewPolicy($"NR-ROUND-seat-{account}", includeHistory: true, owns: raisedBy => raisedBy == bot);
		using var turn = policy.BeginBotStep(bot, "round");
		SimulationL0Session? session = null;
		string outcome = "unused", lineId = "companion", name = $"companion-{account}";
		int characterId = 0, level = 0;
		Exception? stop = null;
		try
		{
			await using var seat = session = new SimulationL0Session(fixture, policy, bot, accountId: account, name, Race.ASMODIANS,
				trace, tracePath) { Turns = turns };
			seat.Dashboard = dashboard;
			seat.BeginStep("companion-seat", "wait-at-character-selection");
			var list = await seat.LoginCharacterListAsync(token);
			while (true)
			{
				var listed = list.Get<List<IReadOnlyDictionary<string, object?>>>("characters");
				if (listed.Count == 0)
				{
					if (playersDone()) break;
					await seat.AdvanceAsync(TimeSpan.FromSeconds(1), token);
					list = await seat.ReadCharacterListAsync(token);
					continue;
				}
				var made = listed.Single();
				characterId = Get<int>(made, "objectId");
				name = Get<string>(made, "name");
				int classId = Get<int>(made, "playerClass");
				PlayerClass playerClass = Enum.GetValues<PlayerClass>().Single(candidate => candidate.GetClassId() == classId);
				NaturalClassLine line = NaturalClassLine.All.Single(candidate => candidate.Second == playerClass);
				lineId = line.Id;
				seat.IdentityClassLine = line;
				seat.BeginStep("companion-seat", $"enter-with-{name}");
				// Another character on the same connection: what the client knew of the last one is dropped.
				seat.Api.BeginLoginObservation();
				seat.ClearPacketHistory();
				seat.SelectCharacter(characterId, name);
				var companionRuntime = new NaturalJourneyRuntime(Aion.GameServer.TestKit.RealStaticData.RepoRoot(),
					"SIM-natural-round", fixture.Seed, fixture.DataManager.StaticData, () => fixture.Clock.NowMillis, fixture.Epoch,
					() => BotNavigationGeometry.ForServerWorld(fixture.World.GetPlayer(seat.CharacterId).GetInstanceId(), Race.ASMODIANS),
					async enterToken =>
					{
						// As the live host's seat: no sooner than the client's ten seconds after its last companion left.
						await seat.WaitForReentryAsync(enterToken);
						await seat.EnterWorldAsync(enterToken);
						await seat.SynchronizeAsync(enterToken);
						return true;
					}, policy.CheckClean, () => policy.SnapshotProblems(), trace, dashboard);
				// The Cleric plays support, as in the group stages; every other class fights (CB-Q8).
				await new NaturalIshalgenJourney(seat, companionRuntime, new NaturalJourneyOptions(ClassLine: line, Companion: "companion",
					Role: playerClass == PlayerClass.CLERIC ? "healer" : null,
					CompanionTest: entry.CompanionTest, CompanionStockMinutes: entry.CompanionStockMinutes)).RunAsync(token);
				level = seat.Api.World.Level;
				seat.BeginStep("companion-seat", "back-to-character-selection");
				await seat.ReturnToSelectionAsync(editing: false, token);
				outcome = "reached";
				list = await seat.ReadCharacterListAsync(token);
			}
			// NL-0: each companion was checked as it left; the seat's policy is completed once, here.
			policy.AssertClean();
		}
		catch (Exception ended)
		{
			// A seat that stops ends alone. What stopped it is its outcome; the others go on.
			stop = ended;
			outcome = "stopped";
		}
		var record = new NaturalRoundOutcome(bot, lineId, outcome, stop?.GetType().Name, stop?.Message, session?.CurrentStep, level, 0,
			began, fixture.Clock.NowMillis, account, name, characterId, entry.Stage);
		File.WriteAllText(Path.Combine(folder, "outcome.json"), JsonSerializer.Serialize(record, RoundJson));
		return record;
	}

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
		// NR-44a: this bot answers for the problems raised in its own play, and for no other's. Its play is marked in
		// the log's scope for as long as it lasts; the mark follows the bot from turn to turn.
		using var policy = NewPolicy($"NR-ROUND-{line.Id}", includeHistory: true, owns: raisedBy => raisedBy == bot);
		using var turn = policy.BeginBotStep(bot, "round");
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
			await new NaturalIshalgenJourney(playing, runtime, RoundOptions(entry, line)).RunAsync(token);
			if (StageReceipt(entry) is { } receipt)
			{
				string receiptPath = Path.Combine(folder, receipt);
				if (!File.Exists(receiptPath)) throw new InvalidDataException($"Stage {entry.Stage} ended with no {receipt}; the stage's end was not checked.");
				using JsonDocument written = JsonDocument.Parse(File.ReadAllText(receiptPath));
				if (!written.RootElement.GetProperty("verified").GetBoolean() || written.RootElement.GetProperty("CharacterId").GetInt32() != playing.CharacterId)
					throw new InvalidDataException($"Stage {entry.Stage}: {receipt} does not say verified for character {playing.CharacterId}.");
			}
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
				if (!resuming) await playing.WaitForPrologueAsync(enterToken);
				await playing.SynchronizeAsync(enterToken);
				var entered = fixture.World.GetPlayer(playing.CharacterId);
				NaturalJourneyIdentityRules.Classify(line, entered.GetPlayerClass(), entered.GetLevel(), entered.GetWorldId(), playing.IdentityAltgardLegId);
				if (!resuming) Assert.Equal(line.Starter, entered.GetPlayerClass());
				// Brief smoke setup through existing client group packets, separate from the support policy.
				if (entry.Stage == "support" && entry.SupportInvite is { Length: > 0 } invited)
				{
					while (!playing.Api.World.Objects.Values.Any(known => known.Name == invited))
					{ await playing.AdvanceAsync(TimeSpan.FromMilliseconds(250), enterToken); await playing.SynchronizeAsync(enterToken); }
					await playing.SendPacketAsync(playing.Api.InviteToGroup(invited), enterToken);
				}
				if (entry.Stage == "support" && entry.AcceptSupportInvite)
				{
					while (playing.Api.World.Question?.Code != 60000)
					{ await playing.AdvanceAsync(TimeSpan.FromMilliseconds(250), enterToken); await playing.SynchronizeAsync(enterToken); }
					await playing.SendPacketAsync(playing.Api.Answer(1), enterToken);
				}
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
			account, name, session?.CharacterId ?? entry.ResumeCharacter ?? 0, entry.Stage);
		File.WriteAllText(Path.Combine(folder, "outcome.json"), JsonSerializer.Serialize(record, RoundJson));
		return record;
	}
}
