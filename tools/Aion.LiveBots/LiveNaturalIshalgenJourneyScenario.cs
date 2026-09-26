using System.Diagnostics;
using System.Text.Json;
using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.GameServer.Model;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static async Task<int> RunNaturalIshalgenJourneyAsync(LiveBotOptions options,
		LiveBotProblemWriter problems, CancellationToken cancellationToken)
	{
		if (options.Profile != "docker-bots-natural")
			throw new InvalidDataException("NI-09 requires the isolated ordinary-rate docker-bots-natural profile.");
		using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		lifetime.CancelAfter(TimeSpan.FromHours(8));
		CancellationToken token = lifetime.Token;
		var elapsed = Stopwatch.StartNew();
		DateTimeOffset epoch = DateTimeOffset.UtcNow;
		NaturalIshalgenIdentity identity = NaturalIshalgenIdentityScenario.Identity;
		await using var actor = new L0Actor(options, problems, 1, Race.ASMODIANS, bot: "b01",
			account: identity.AccountName, characterName: identity.CharacterName);
		LiveBotSession session = actor.Session;
		session.EnableNaturalJourney();
		actor.Trace.WriteAction("ni09", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "NI-09" });
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
			// The shared driver proves all 41 completions, level 9, Munin proximity and untouched Ascension before quitting.
			await session.VerifyOfflineAsync(token);
			NaturalIshalgenContract contract = NaturalIshalgenContract.LoadDefault();
			NaturalJourneyCheckpoint beforeRelog = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
				session.ConnectionGeneration, contract, session.CurrentPosition);
			session.BeforeSend = null;
			session.AfterSynchronize = null;
			session.BeginStep("ni09-relog-persistence", "relog-and-verify-the-completed-priest");
			await session.WaitForReentryAsync(token);
			await session.ReloginExistingCharacterAsync(token);
			await session.EnterWorldAsync(token);
			await session.SynchronizeAsync(token);
			NaturalJourneyCheckpoint checkpoint = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
				session.ConnectionGeneration, contract, session.CurrentPosition);
			NaturalJourneyPersistence.Verify(beforeRelog, checkpoint);
			if (checkpoint.Next.Outcome != "complete" || checkpoint.Next.SelectedAction != "journey-complete")
				throw new InvalidDataException("Relogged Priest no longer satisfies the complete Ishalgen contract.");
			await new LiveNaturalIshalgenIdentityDriver(options, actor, identity)
				.VerifyOrdinaryOnlineIdentityAsync(session.CharacterId, 9, token);
			await File.WriteAllTextAsync(Path.Combine(options.OutputDirectory, "natural-ishalgen-persistence.json"),
				JsonSerializer.Serialize(new { beforeRelog, afterRelog = checkpoint, verified = true },
					new JsonSerializerOptions { WriteIndented = true }), token);
			session.TraceDiagnostic("natural-persistence-verified", new Dictionary<string, object?>
			{
				["characterId"] = checkpoint.CharacterId, ["connectionGeneration"] = checkpoint.ConnectionGeneration,
			});
			await session.QuitAsync(token);
			await session.VerifyOfflineAsync(token);
			session.FinishStep("completed");
			await File.WriteAllTextAsync(Path.Combine(options.OutputDirectory, "natural-ishalgen-complete.json"),
				JsonSerializer.Serialize(new { scenario = "NI-09", elapsed = elapsed.Elapsed, checkpoint },
					new JsonSerializerOptions { WriteIndented = true }), token);
			AssertClean();
			actor.Trace.WriteAction(session.CurrentStep, "scenario:complete", new Dictionary<string, object?>
			{
				["scenario"] = "NI-09", ["characterId"] = session.CharacterId, ["elapsedSeconds"] = elapsed.Elapsed.TotalSeconds,
			});
			Console.WriteLine($"LIVE NI-09 completed: {identity.CharacterName}, all 41 quests, level 9 at Munin, Ascension untouched.");
			return 0;
			async Task<bool> EnterAsync(CancellationToken ct)
			{
				var identityDriver = new LiveNaturalIshalgenIdentityDriver(options, actor, identity);
				var subject = await NaturalIshalgenIdentityScenario.EnterAsync(identityDriver, ct);
				await WriteNaturalIdentityReceiptAsync(options, identity, subject, ct);
				if (!subject.CreatedThisRun)
					throw new InvalidDataException("NI-09 acceptance must start with a fresh level-1 Priest in its own isolated world.");
				return false;
			}
			void AssertClean()
			{
				if (problems.Snapshot().Length != 0)
					throw new InvalidDataException("NI-09 has recorded bot problems; inspect bot.problems.jsonl.");
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
		catch (Exception exception)
		{
			session.FinishStep("failed");
			await problems.WriteAsync(options.Run, actor.Bot, actor.Account, session.CurrentStep, "natural-journey-failed",
				exception.Message, exception);
			Console.Error.WriteLine($"NI-09 failed: {exception}");
			return 1;
		}
	}
}
