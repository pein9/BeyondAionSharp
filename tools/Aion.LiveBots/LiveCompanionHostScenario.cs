using System.Collections.Concurrent;
using System.Diagnostics;
using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Aion.GameServer.Model;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	/// <summary>The most seats a host has: a group holds its player and five companions.</summary>
	public const int CompanionHostMaximumSeats = 5;

	/// <summary>The account a seat logs in on. The server is told the same names (AION_COMPANION_ACCOUNTS).</summary>
	public static string CompanionSeatAccount(int seat) => $"companion{seat}";

	/// <summary>
	/// CH-01, the companion host on the operator's already-running world (docs/companion-live-host.md). It speaks the
	/// client protocol and nothing else: no admin API, no database, no Docker lifecycle. Each seat logs in on its own
	/// account and waits at character selection, asking for its character list. A character that appears there is a
	/// companion the server made for a player (D41): the seat enters the world with it, plays it with the code the SIM
	/// rounds play, and goes back to character selection when its play ends, where the server deletes it. A seat that
	/// loses its connection logs in again. The host runs until the operator stops it.
	/// </summary>
	private static async Task<int> RunCompanionHostAsync(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken operatorStop)
	{
		string target = options.AttachTarget
			?? throw new InvalidDataException("CH-01 attaches only to an explicitly selected world (--attach-target).");
		string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
		Console.WriteLine("Companion host: loading the ground the companions walk on. This takes a minute or two.");
		BotNavigationAssets assets = await BotNavigationAssets.LoadAsync(root,
			Path.Combine(options.OutputDirectory, "navigation-cache"), operatorStop);
		var elapsed = Stopwatch.StartNew();
		DateTimeOffset epoch = DateTimeOffset.UtcNow;
		var played = new ConcurrentDictionary<string, int>();
		// Every seat plays on one thread, as the bots of a SIM round do: the companion code and what it shares are never
		// run by two seats at once.
		await Task.Run(() => CompanionHostThread.Run(() => Task.WhenAll(
			Enumerable.Range(1, options.BotCount).Select(seat => PlayCompanionSeatAsync(seat)))), CancellationToken.None);
		await WriteJsonAsync(options, "companion-host.json", new
		{
			scenario = "CH-01", attachTarget = target, elapsed = elapsed.Elapsed, seats = options.BotCount,
			companionsPlayed = played.OrderBy(entry => entry.Key).ToDictionary(entry => entry.Key, entry => entry.Value),
		}, CancellationToken.None);
		Console.WriteLine($"Companion host stopped after {elapsed.Elapsed:hh\\:mm\\:ss}; companions played: {played.Values.Sum()}.");
		return OperatorStoppedExitCode;

		async Task PlayCompanionSeatAsync(int seat)
		{
			// Subject numbers of their own, so that a seat's MAC address is no other bot's.
			string bot = $"b{900 + seat}", account = CompanionSeatAccount(seat);
			played[account] = 0;
			while (!operatorStop.IsCancellationRequested)
			{
				await using var actor = new L0Actor(options, problems, 900 + seat, Race.ASMODIANS, bot: bot, account: account, characterName: "Unused");
				LiveBotSession session = actor.Session;
				session.EnableNaturalJourney();
				try
				{
					session.BeginStep("companion-seat", "log-in");
					DecodedBotServerPacket list = await session.LoginCharacterListAsync(operatorStop);
					// What is on the account as the seat logs in is no companion to play: it is what a server left behind
					// that stopped with companions alive, and the server deletes it when it finds the seat. Any other
					// character means this account is no seat to the server.
					bool told = false;
					long since = elapsed.ElapsedMilliseconds;
					while (Characters(list).Count > 0)
					{
						if (!told || elapsed.ElapsedMilliseconds - since > 60_000)
						{
							Console.WriteLine($"Seat {account} holds {string.Join(", ", Characters(list).Select(found => Get<string>(found, "name")))} from before. " +
								"The server deletes a companion that was left behind; if this stays, the account is not a seat to the server " +
								"(AION_COMPANIONS and AION_COMPANION_ACCOUNTS), or it holds a character that is no companion.");
							told = true;
							since = elapsed.ElapsedMilliseconds;
						}
						await Task.Delay(TimeSpan.FromSeconds(2), operatorStop);
						list = await session.ReadCharacterListAsync(operatorStop);
					}
					Console.WriteLine($"Seat {account} is waiting at character selection.");
					session.BeginStep("companion-seat", "wait-at-character-selection");
					while (true)
					{
						if (Characters(list).Count == 0)
						{
							await Task.Delay(TimeSpan.FromSeconds(2), operatorStop);
							list = await session.ReadCharacterListAsync(operatorStop);
							continue;
						}
						IReadOnlyDictionary<string, object?> made = Characters(list).Single();
						int characterId = Get<int>(made, "objectId");
						string name = Get<string>(made, "name");
						int classId = Get<int>(made, "playerClass");
						PlayerClass playerClass = Enum.GetValues<PlayerClass>().Single(candidate => candidate.GetClassId() == classId);
						NaturalClassLine line = NaturalClassLine.All.Single(candidate => candidate.Second == playerClass);
						session.BeginStep("companion-seat", $"enter-with-{name}");
						Console.WriteLine($"Seat {account}: {name} the {playerClass.ToString().ToLowerInvariant().Replace("_", " ")} goes to its player.");
						// Another character on the same connection: what the client knew of the last one is dropped.
						session.Api.BeginLoginObservation();
						session.ClearPacketHistory();
						session.SelectCharacter(characterId, name);
						var runtime = new NaturalJourneyRuntime(root, options.Profile, options.Seed, assets.Data,
							() => elapsed.ElapsedMilliseconds, epoch,
							() => assets.AnyMapGeometry(Race.ASMODIANS, (session.Api.World.ChannelInfo?.Index ?? 0) + 1),
							async enterToken =>
							{
								// The client's rule: a character enters the world no sooner than ten seconds after the last one of
								// its connection left it. A companion asked for right after one was sent away waits that out here.
								// A seat that did not wait failed, and the player's new companion never came.
								await session.WaitForReentryAsync(enterToken);
								await session.EnterWorldAsync(enterToken);
								await session.SynchronizeAsync(enterToken);
								return true;
							},
							// A host plays for hours and for many companions. A problem is written down; it stops no seat.
							() => { }, () => problems.Snapshot(), actor.Trace, options.Dashboard)
						{
							// A long search for a way runs beside the play, and the seat goes on when it is done: on a real server
							// the clock does not wait while one seat thinks, and the seats share this thread.
							SearchElsewhere = async search =>
							{
								long began = elapsed.ElapsedMilliseconds;
								await Task.Run(search, CancellationToken.None);
								long took = elapsed.ElapsedMilliseconds - began;
								if (took >= 2_000) Console.WriteLine($"Seat {account}: {name} looked for a way for {took / 1000.0:0.0} s.");
							},
						};
						// The Cleric plays support, as in the group stages; every other class fights (CB-Q8).
						await new NaturalIshalgenJourney(session, runtime, new NaturalJourneyOptions(ClassLine: line, Companion: "companion",
							Role: playerClass == PlayerClass.CLERIC ? "healer" : null)).RunAsync(operatorStop);
						session.BeginStep("companion-seat", "back-to-character-selection");
						await session.ReturnToSelectionAsync(false, operatorStop);
						played.AddOrUpdate(account, 1, (_, count) => count + 1);
						Console.WriteLine($"Seat {account}: {name} has ended; the seat is waiting again.");
						session.BeginStep("companion-seat", "wait-at-character-selection");
						list = await session.ReadCharacterListAsync(operatorStop);
					}
				}
				catch (OperationCanceledException) when (operatorStop.IsCancellationRequested)
				{
					session.FinishStep("stopped");
					await LeaveAsync(session, account);
					return;
				}
				catch (Exception failure)
				{
					// The seat's connection is gone, or its companion's play failed. The server ends a companion whose
					// seat is gone; the seat itself comes back.
					session.FinishStep("failed");
					await problems.WriteAsync(options.Run, bot, account, session.CurrentStep, "companion-seat-failed", failure.Message, failure);
					Console.Error.WriteLine($"Seat {account} failed at {session.CurrentStep} ({failure.GetType().Name}: {failure.Message}). It logs in again in 15 seconds.");
					await LeaveAsync(session, account);
				}
				try { await Task.Delay(TimeSpan.FromSeconds(15), operatorStop); }
				catch (OperationCanceledException) { return; }
			}
		}

		// Out of the operator's world the ordinary way when that still works: back to character selection, where the
		// server deletes the companion, and then the connection is closed. Otherwise the connection is closed, and the
		// server ends the companion of a seat that is gone.
		static async Task LeaveAsync(LiveBotSession session, string account)
		{
			if (!session.HasGameConnection) return;
			using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(20));
			try
			{
				session.BeforeSend = null;
				if (session.InGame) await session.ReturnToSelectionAsync(false, budget.Token);
				await session.CloseAsync(budget.Token);
			}
			catch (Exception leaveFailure)
			{
				Console.Error.WriteLine($"Seat {account} could not leave the ordinary way ({leaveFailure.Message}); its connection is closed.");
				try { await session.CloseAsync(CancellationToken.None); } catch { /* Disposal closes it too. */ }
			}
		}

		static List<IReadOnlyDictionary<string, object?>> Characters(DecodedBotServerPacket list) =>
			list.Get<List<IReadOnlyDictionary<string, object?>>>("characters");
	}

	/// <summary>
	/// One thread for every seat of the host. A continuation of a seat's play is run here and nowhere else, so two seats
	/// never run the companion code at the same moment; a seat gives way whenever it waits for the network or the clock.
	/// </summary>
	private sealed class CompanionHostThread : SynchronizationContext
	{
		private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> queue = [];

		public override void Post(SendOrPostCallback d, object? state)
		{
			// After the host has ended, what is left to run (a disposal's tail) runs on the pool.
			try { queue.Add((d, state)); }
			catch (InvalidOperationException) { ThreadPool.QueueUserWorkItem(_ => d(state)); }
		}

		public override void Send(SendOrPostCallback d, object? state) =>
			throw new NotSupportedException("The companion host's thread takes posted work only.");

		public static void Run(Func<Task> play)
		{
			var context = new CompanionHostThread();
			Exception? fault = null;
			var thread = new Thread(() =>
			{
				SetSynchronizationContext(context);
				Task playing;
				try { playing = play(); }
				catch (Exception failure) { fault = failure; return; }
				playing.ContinueWith(_ => context.queue.CompleteAdding(), CancellationToken.None,
					TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
				foreach ((SendOrPostCallback callback, object? state) in context.queue.GetConsumingEnumerable())
					callback(state);
				try { playing.GetAwaiter().GetResult(); }
				catch (Exception failure) { fault = failure; }
			}) { IsBackground = true, Name = "companion-host" };
			thread.Start();
			thread.Join();
			if (fault != null) throw new InvalidOperationException("The companion host's play failed.", fault);
		}
	}
}
