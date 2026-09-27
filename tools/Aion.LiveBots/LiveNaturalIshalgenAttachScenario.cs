using System.Diagnostics;
using System.Text.Json;
using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.GameServer.Model;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	/// <summary>Exit code for an operator stop: the Priest left the world normally and resumes on the next attach.</summary>
	public const int OperatorStoppedExitCode = 3;

	/// <summary>
	/// NI-10: the NI-09 journey on the operator's already-running world. It only speaks the client protocol to the
	/// selected endpoints. It never uses the admin API and never creates, drops, restarts or reconfigures anything;
	/// the retained Priest is created once and afterwards resumed from whatever state the client observes.
	/// </summary>
	private static async Task<int> RunNaturalIshalgenAttachAsync(LiveBotOptions options,
		LiveBotProblemWriter problems, CancellationToken operatorStop)
	{
		string target = options.AttachTarget
			?? throw new InvalidDataException("NI-10 attaches only to an explicitly selected world (--attach-target).");
		using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(operatorStop);
		lifetime.CancelAfter(TimeSpan.FromHours(8));
		CancellationToken token = lifetime.Token;
		var elapsed = Stopwatch.StartNew();
		DateTimeOffset epoch = DateTimeOffset.UtcNow;
		NaturalIshalgenIdentity identity = NaturalIshalgenIdentityScenario.IdentityForSlot(options.AttachIdentitySlot);
		await using var actor = new L0Actor(options, problems, 1, Race.ASMODIANS, bot: "b01",
			account: identity.AccountName, characterName: identity.CharacterName);
		LiveBotSession session = actor.Session;
		session.EnableNaturalJourney();
		var coexistence = new LiveCoexistenceLog();
		session.PacketObserved = packet => coexistence.Observe(packet, session.Api.World, session.CharacterId,
			session.ObservedPosition, session.CurrentStep, actor.Trace);
		NaturalIshalgenIdentityResult? subject = null;
		actor.Trace.WriteAction("ni10", "scenario:start", new Dictionary<string, object?>
		{
			["scenario"] = "NI-10", ["attachTarget"] = target, ["profile"] = options.Profile,
		});
		try
		{
			string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
			BotNavigationAssets assets = await BotNavigationAssets.LoadAsync(root,
				Path.Combine(options.OutputDirectory, "navigation-cache"), token);
			var runtime = new NaturalJourneyRuntime(root, options.Profile, options.Seed, assets.Data,
				() => elapsed.ElapsedMilliseconds, epoch,
				() => assets.NaturalJourneyGeometry(identity.Race, (session.Api.World.ChannelInfo?.Index ?? 0) + 1),
				EnterAsync, AssertClean, () => problems.Snapshot(), actor.Trace, options.Dashboard);
			await new NaturalIshalgenJourney(session, runtime, new NaturalJourneyOptions()).RunAsync(token);

			// The shared driver ends offline after proving the contract. Prove persistence the way a player would see
			// it: the selection list shows where the quit left the Priest, and re-entry shows the same character.
			NaturalIshalgenContract contract = NaturalIshalgenContract.LoadDefault();
			NaturalJourneyCheckpoint beforeRelog = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
				session.ConnectionGeneration, contract, session.CurrentPosition);
			session.BeforeSend = null;
			session.AfterSynchronize = null;
			session.BeginStep("ni10-relog-persistence", "relog-and-verify-the-completed-priest");
			await session.WaitForReentryAsync(token);
			await session.ReloginExistingCharacterAsync(verifySavedPosition: true, token);
			await session.EnterWorldAsync(token);
			await session.SynchronizeAsync(token);
			NaturalJourneyCheckpoint checkpoint = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
				session.ConnectionGeneration, contract, session.CurrentPosition);
			NaturalJourneyPersistence.Verify(beforeRelog, checkpoint);
			if (checkpoint.Next.Outcome != "complete" || checkpoint.Next.SelectedAction != "journey-complete")
				throw new InvalidDataException("Relogged Priest no longer satisfies the complete Ishalgen contract.");
			await new LiveNaturalIshalgenIdentityDriver(options, actor, identity)
				.VerifyOrdinaryOnlineIdentityAsync(session.CharacterId, 9, token);
			await WriteJsonAsync(options, "natural-ishalgen-persistence.json",
				new { beforeRelog, afterRelog = checkpoint, verified = true, oracle = "client-selection-list-and-reentry" }, token);
			await session.QuitAsync(token);
			session.FinishStep("completed");
			await WriteJsonAsync(options, "natural-ishalgen-complete.json",
				new { scenario = "NI-10", attachTarget = target, elapsed = elapsed.Elapsed, checkpoint }, token);
			AssertClean();
			actor.Trace.WriteAction(session.CurrentStep, "scenario:complete", new Dictionary<string, object?>
			{
				["scenario"] = "NI-10", ["characterId"] = session.CharacterId, ["elapsedSeconds"] = elapsed.Elapsed.TotalSeconds,
			});
			await WriteAttachResultAsync("complete", null);
			Console.WriteLine($"NI-10 completed on '{target}': {identity.CharacterName}, all 41 quests, level 9 at Munin, Ascension untouched.");
			return 0;
		}
		catch (OperationCanceledException) when (operatorStop.IsCancellationRequested)
		{
			session.FinishStep("stopped");
			bool leftNormally = await LeaveWorldAsync();
			await WriteAttachResultAsync("stopped", leftNormally ? null : "The quit was not acknowledged; the connection was closed instead.");
			Console.WriteLine($"NI-10 stopped by the operator after {elapsed.Elapsed:hh\\:mm\\:ss}; " +
				$"{identity.CharacterName} {(leftNormally ? "quit normally" : "was disconnected")}. Run again to resume.");
			return OperatorStoppedExitCode;
		}
		catch (Exception exception)
		{
			session.FinishStep("failed");
			await problems.WriteAsync(options.Run, actor.Bot, actor.Account, session.CurrentStep, "natural-journey-failed",
				exception.Message, exception);
			await LeaveWorldAsync();
			await WriteAttachResultAsync("failed", exception.Message);
			Console.Error.WriteLine($"NI-10 failed: {exception}");
			return 1;
		}

		async Task<bool> EnterAsync(CancellationToken ct)
		{
			var identityDriver = new LiveNaturalIshalgenIdentityDriver(options, actor, identity);
			subject = await NaturalIshalgenIdentityScenario.EnterAsync(identityDriver, ct);
			await WriteJsonAsync(options, "natural-ishalgen-identity.json", new
			{
				schemaVersion = 1, run = options.Run, attachTarget = target,
				accountName = identity.AccountName, characterName = identity.CharacterName,
				characterId = subject.CharacterId, level = subject.Level,
				race = identity.Race.ToString(), playerClass = identity.PlayerClass.ToString(),
				// The client cannot see access level; the attach runner records it read-only beside this receipt.
				accessLevelSource = "attach runner read-only check (target-identity-*.json)",
				createdThisRun = subject.CreatedThisRun, retained = true,
			}, ct);
			return !subject.CreatedThisRun;
		}

		void AssertClean()
		{
			if (problems.Snapshot().Length != 0)
				throw new InvalidDataException("NI-10 has recorded bot problems; inspect bot.problems.jsonl.");
		}

		// Leave the operator's world the ordinary way so the server saves and despawns the Priest normally.
		async Task<bool> LeaveWorldAsync()
		{
			if (!session.HasGameConnection) return true;
			using var quitBudget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
			try
			{
				session.BeforeSend = null;
				session.BeginStep("ni10-leave-world", "quit-without-touching-the-server");
				if (session.InGame)
				{
					await session.QuitAsync(quitBudget.Token);
					return true;
				}
				await session.CloseAsync(quitBudget.Token);
				return true;
			}
			catch (Exception quitFailure)
			{
				Console.Error.WriteLine($"NI-10 could not quit normally: {quitFailure.Message}");
				try { await session.CloseAsync(CancellationToken.None); } catch { /* Disposal closes it too. */ }
				return false;
			}
		}

		async Task WriteAttachResultAsync(string outcome, string? detail)
		{
			int completed = NaturalIshalgenContract.LoadDefault().Quests.Count(quest => session.Api.World.CompletedQuestIds.Contains(quest.Id));
			await WriteJsonAsync(options, "coexistence.json", coexistence.Snapshot(completed), CancellationToken.None);
			await WriteJsonAsync(options, "attach-result.json", new
			{
				schemaVersion = 1, scenario = "NI-10", run = options.Run, attachTarget = target, outcome, detail,
				characterId = subject?.CharacterId, createdThisRun = subject?.CreatedThisRun,
				resumedLevel = subject?.Level, levelNow = session.Api.World.Level,
				completedContractQuestsNow = completed,
				elapsedSeconds = elapsed.Elapsed.TotalSeconds,
				botProblems = problems.Snapshot().Length,
			}, CancellationToken.None);
		}
	}

	private static async Task WriteJsonAsync(LiveBotOptions options, string fileName, object value, CancellationToken token)
	{
		string path = Path.Combine(options.OutputDirectory, fileName);
		await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(value,
			new JsonSerializerOptions { WriteIndented = true }), token);
		File.Move(path + ".tmp", path, overwrite: true);
	}
}
