using System.Diagnostics;
using System.Text.Json;
using Aion.Bots.Gm;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static readonly SoakActivity[] ImplementedSoakActivities =
		[SoakActivity.Group, SoakActivity.Trade, SoakActivity.Relog, SoakActivity.CrashDisconnect, SoakActivity.Vendor, SoakActivity.Craft];

	private static async Task<int> RunSoakAsync(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		var unavailable = options.SoakActivities.Except(ImplementedSoakActivities).ToArray();
		if (unavailable.Length != 0)
			throw new InvalidOperationException($"SOAK activities not implemented: {string.Join(", ", unavailable)}. Select explicit diagnostic activities; full soak acceptance is unavailable.");
		var cohorts = SoakLifePolicy.CreatePopulation(options.BotCount, ScenarioManifest.Load(ScenarioManifest.FindDefaultPath()))
			.Select(cohort => cohort with { Actions = cohort.Actions.Where(action => options.SoakActivities.Contains(action.Activity)).ToArray() }).ToArray();
		if (cohorts.Any(cohort => cohort.Actions.Count == 0))
			throw new InvalidOperationException("SOAK selection leaves a cohort without work; include a lifecycle activity.");
		var actors = new List<L0Actor>();
		var loops = new List<Task<SoakCohortResult>>();
		using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
		var start = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
		await using var director = new L0Actor(options, problems, 99, Race.ELYOS,
			bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		try
		{
			await InitializeAsync(director, stop.Token);
			var gm = director.Session.CreateLiveGmFacade();
			foreach (var cohort in cohorts)
			{
				var first = new L0Actor(options, problems, cohort.FirstSubject, RaceOf(cohort.FirstRace));
				var second = new L0Actor(options, problems, cohort.SecondSubject, RaceOf(cohort.SecondRace));
				actors.AddRange([first, second]);
				int offset = 0;
				foreach (var actor in new[] { first, second })
				{
					await InitializeAsync(actor, stop.Token);
					var point = cohort.Actions.Any(action => action.Activity == SoakActivity.Vendor) ? VendorScenario.Position : SoakStart(cohort.MapId);
					await MoveSubjectWithDirectorAsync(director, actor, gm, cohort.MapId, point.X + offset++, point.Y, point.Z, "soak-initial-position", stop.Token);
					await actor.StepAsync("soak-initial-supplies", async ct =>
					{
						await gm.ExecuteVerifiedAsync(new GmCommand("set", ["class", "sorcerer"], "replyless class change"),
							new GmCommand("set", ["level", "10"], "level to 10"), new GmSubject(actor.Session.CharacterId, actor.Session.CharacterName), ct);
						await gm.ExecuteAsync(new GmCommand("add", [actor.Session.CharacterName, "182400001", "1000000"], "You gave"), cancellationToken: ct);
						await gm.ExecuteAsync(new GmCommand("add", [actor.Session.CharacterName, "169300002", "100"], "You gave"), cancellationToken: ct);
						await actor.Session.WaitForInventoryItemAsync(169300002, ct);
						await actor.Session.SynchronizeAsync(ct);
					}, stop.Token);
				}
				loops.Add(RunPairAsync(cohort, first, second));
				Console.WriteLine($"SOAK prepared {actors.Count}/{options.BotCount} subjects.");
			}
			await director.StepAsync("director-quit-after-setup", director.Session.QuitAsync, stop.Token);
			start.SetResult(Stopwatch.GetTimestamp());
			SoakCohortResult[] results = await Task.WhenAll(loops);
			await File.WriteAllTextAsync(Path.Combine(options.OutputDirectory, "soak-runtime.json"), JsonSerializer.Serialize(new
			{
				options.Seed, options.BotCount, options.SoakSeconds, Activities = options.SoakActivities.Select(value => value.ToString()),
				Acceptance = false, Scope = "diagnostic activity subset; capacity telemetry and full workload pending", Cohorts = results,
			}, new JsonSerializerOptions { WriteIndented = true }), token);
			Console.WriteLine($"SOAK diagnostic passed: {options.BotCount} subjects, {options.SoakSeconds}s, {results.Sum(result => result.Actions)} actions. Not full P10-02 acceptance.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception error) { Console.Error.WriteLine($"SOAK failed: {error}"); return 1; }
		finally
		{
			await stop.CancelAsync();
			try { await Task.WhenAll(loops); } catch { /* Original failure is recorded by the owning step. */ }
			foreach (var actor in actors) await actor.DisposeAsync();
		}

		async Task InitializeAsync(L0Actor actor, CancellationToken ct)
		{
			actor.Session.BoundSoakHistory();
			actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "SOAK" });
			await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, ct);
			await actor.StepAsync("create-character", inner => actor.Session.CreateCharacterAsync(inner, PlayerClass.MAGE), ct);
			await actor.StepAsync("enter-world", actor.Session.EnterWorldAsync, ct);
		}

		async Task<SoakCohortResult> RunPairAsync(SoakCohort cohort, L0Actor first, L0Actor second)
		{
			try
			{
				while (!start.Task.IsCompleted)
					await first.StepAsync("wait-for-population", ct => SoakDrainAsync(first, second, TimeSpan.FromSeconds(1), ct), stop.Token);
				long began = await start.Task;
				var policy = new SoakLifePolicy(options.Seed, cohort);
				var counts = cohort.Actions.ToDictionary(action => action.Activity.ToString(), _ => 0L);
				while (Stopwatch.GetElapsedTime(began) < TimeSpan.FromSeconds(options.SoakSeconds))
				{
					var decision = policy.Next();
					foreach (var actor in new[] { first, second })
						actor.Trace.WriteAction(actor.LastStep, "soak:decision", new Dictionary<string, object?>
						{ ["cohort"] = cohort.Number, ["sequence"] = decision.Sequence, ["activity"] = decision.Action.Activity.ToString(), ["source"] = decision.Action.SourceScenario });
					await first.StepAsync("soak-" + decision.Action.Activity, ct => second.StepAsync("soak-" + decision.Action.Activity, async inner =>
					{
						switch (decision.Action.Activity)
						{
							case SoakActivity.Group: await SoakGroupAsync(first.Session, second.Session, inner); break;
							case SoakActivity.Trade: await SoakTradeAsync(first.Session, second.Session, inner); break;
							case SoakActivity.Vendor:
								await Task.WhenAll(VendorScenario.RunAsync(new SoakVendorDriver(first), inner), VendorScenario.RunAsync(new SoakVendorDriver(second), inner)); break;
							case SoakActivity.Craft:
								var master = cohort.MapId == CookingMaster.Hestia.MapId ? CookingMaster.Hestia : CookingMaster.Lainita;
								await SoakIndependentPairAsync(first, second, (actor, ct) => SoakCookingAsync(actor, master, ct), inner); break;
							case SoakActivity.Relog:
							case SoakActivity.CrashDisconnect:
								bool crash = decision.Action.Activity == SoakActivity.CrashDisconnect;
								await Task.WhenAll(first.Session.ReenterForSoakAsync(crash, inner), second.Session.ReenterForSoakAsync(crash, inner)); break;
							default: throw new InvalidOperationException("Unimplemented soak activity.");
						}
					}, ct), stop.Token);
					counts[decision.Action.Activity.ToString()]++;
					await first.StepAsync("soak-think", ct => SoakDrainAsync(first, second, decision.ThinkTime, ct), stop.Token);
				}
				if (counts.Values.Any(count => count == 0)) throw new InvalidDataException($"Cohort {cohort.Number} did not exercise every selected activity; increase diagnostic duration.");
				foreach (var actor in new[] { first, second })
				{
					await actor.StepAsync("verify-final-inventory", actor.Session.VerifyInventoryAsync, stop.Token);
					await actor.StepAsync("quit", actor.Session.QuitAsync, stop.Token);
					await actor.StepAsync("verify-offline", actor.Session.VerifyOfflineAsync, stop.Token);
					actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "SOAK" });
				}
				return new SoakCohortResult(cohort.Number, counts.Values.Sum(), counts);
			}
			catch (Exception error)
			{
				if (error is not OperationCanceledException || !stop.IsCancellationRequested)
					Console.Error.WriteLine($"SOAK cohort {cohort.Number} failed: {error}");
				await stop.CancelAsync();
				throw;
			}
		}
	}

	private sealed record SoakCohortResult(int Cohort, long Actions, Dictionary<string, long> Counts);
	private static Race RaceOf(ScenarioRace race) => race == ScenarioRace.Elyos ? Race.ELYOS : Race.ASMODIANS;
	private static BotPosition SoakStart(int map) => map switch
	{
		210010000 => new(1212, 1040, 140.756f, 0),
		220010000 => new(577.529f, 2817.34f, 303.613f, 0),
		110010000 => new(1844.07f, 1543.97f, 590.158f, 0),
		120010000 => new(1163.16f, 1540.53f, 214.174f, 0),
		400010000 => new(3130, 2400, 1800, 0),
		_ => throw new ArgumentOutOfRangeException(nameof(map)),
	};
	private static async Task SoakDrainAsync(L0Actor first, L0Actor second, TimeSpan duration, CancellationToken token)
	{
		long began = Stopwatch.GetTimestamp();
		do
		{
			await Task.WhenAll(first.Session.SynchronizeAsync(token), second.Session.SynchronizeAsync(token));
			await Task.Delay(TimeSpan.FromSeconds(1), token);
		} while (Stopwatch.GetElapsedTime(began) < duration);
	}

	private static async Task SoakGroupAsync(LiveBotSession first, LiveBotSession second, CancellationToken token)
	{
		await first.SendPacketAsync(first.Api.InviteToGroup(second.CharacterName), token);
		await second.WaitForPacketAsync(typeof(SM_QUESTION_WINDOW), token, packet => packet.Get<int>("code") == 60000);
		await second.SendPacketAsync(second.Api.Answer(1), token);
		foreach (var session in new[] { first, second })
			await session.WaitForPacketAsync(typeof(SM_GROUP_INFO), token, packet => packet.Get<int>("leaderId") == first.CharacterId);
		await Task.WhenAll(first.SynchronizeAsync(token), second.SynchronizeAsync(token));
		if (first.Api.World.GroupId is not > 0 || first.Api.World.GroupId != second.Api.World.GroupId ||
			!first.Api.World.GroupMembers.ContainsKey(second.CharacterId) || !second.Api.World.GroupMembers.ContainsKey(first.CharacterId))
			throw new InvalidDataException("Soak group identity or reciprocal roster mismatch.");
		await second.SendPacketAsync(second.Api.LeaveGroup(), token);
		foreach (var session in new[] { first, second })
		{
			await session.WaitForPacketAsync(typeof(SM_LEAVE_GROUP_MEMBER), token);
			await session.SynchronizeAsync(token);
			if (session.Api.World.GroupId != null || session.Api.World.GroupMembers.Count != 0)
				throw new InvalidDataException("Soak group cleanup failed.");
		}
	}

	private static async Task SoakTradeAsync(LiveBotSession first, LiveBotSession second, CancellationToken token)
	{
		await Task.WhenAll(first.SynchronizeAsync(token), second.SynchronizeAsync(token));
		var firstBefore = Totals(first); var secondBefore = Totals(second);
		// Cancel and commit every time, so short runs cannot accidentally cover only one outcome.
		foreach (bool cancel in new[] { true, false })
		{
			await first.SendPacketAsync(first.Api.TradeRequest(second.CharacterId), token);
			var question = await second.WaitForPacketAsync(typeof(SM_QUESTION_WINDOW), token, packet => packet.Get<int>("code") == 90001);
			if (question.Get<string[]>("params")[0] != first.CharacterName) throw new InvalidDataException("Wrong soak trade requester.");
			await second.SendPacketAsync(second.Api.Answer(1), token);
			await first.WaitForPacketAsync(typeof(SM_EXCHANGE_REQUEST), token, packet => packet.Get<string>("receiver") == second.CharacterName);
			await second.WaitForPacketAsync(typeof(SM_EXCHANGE_REQUEST), token, packet => packet.Get<string>("receiver") == first.CharacterName);
			foreach (var (giver, receiver, money) in new[] { (first, second, 25L), (second, first, 17L) })
			{
				var item = giver.Api.World.Inventory.Values.First(value => value.ItemId == 169300002 && value.Count >= 1);
				await giver.SendPacketAsync(giver.Api.TradeAddItem(item.ObjectId, 1), token);
				foreach (var (session, side) in new[] { (giver, 0), (receiver, 1) })
				{
					var offered = await session.WaitForPacketAsync(typeof(SM_EXCHANGE_ADD_ITEM), token, packet => packet.Get<byte>("action") == side);
					if (offered.Get<int>("itemId") != 169300002 || offered.Get<long>("itemCount") != 1) throw new InvalidDataException("Wrong soak trade item offer.");
				}
				await giver.SendPacketAsync(giver.Api.TradeAddKinah(money), token);
				foreach (var (session, side) in new[] { (giver, 0), (receiver, 1) })
				{
					var offered = await session.WaitForPacketAsync(typeof(SM_EXCHANGE_ADD_KINAH), token, packet => packet.Get<byte>("action") == side);
					if (offered.Get<long>("kinahCount") != money) throw new InvalidDataException("Wrong soak trade kinah offer.");
				}
			}
			await first.SendPacketAsync(first.Api.TradeLock(), token); await Confirmation(second, 3);
			await second.SendPacketAsync(second.Api.TradeLock(), token); await Confirmation(first, 3);
			if (cancel) { await first.SendPacketAsync(first.Api.TradeCancel(), token); await Confirmation(second, 1); }
			else
			{
				await first.SendPacketAsync(first.Api.TradeAccept(), token); await Confirmation(second, 2);
				await second.SendPacketAsync(second.Api.TradeAccept(), token); await Confirmation(first, 0); await Confirmation(second, 0);
				await first.WaitForPacketAsync(typeof(SM_INVENTORY_UPDATE_ITEM), token, _ => first.Api.World.Kinah == firstBefore[BotWorldModel.KinahItemId] - 8);
				await second.WaitForPacketAsync(typeof(SM_INVENTORY_UPDATE_ITEM), token, _ => second.Api.World.Kinah == secondBefore[BotWorldModel.KinahItemId] + 8);
				firstBefore[BotWorldModel.KinahItemId] -= 8; secondBefore[BotWorldModel.KinahItemId] += 8;
			}
			await Task.Delay(250, token);
			await Task.WhenAll(first.SynchronizeAsync(token), second.SynchronizeAsync(token));
			if (!firstBefore.OrderBy(pair => pair.Key).SequenceEqual(Totals(first).OrderBy(pair => pair.Key)) ||
				!secondBefore.OrderBy(pair => pair.Key).SequenceEqual(Totals(second).OrderBy(pair => pair.Key)))
				throw new InvalidDataException("Soak exchange violated exact per-subject conservation.");
			if (first.Api.Timing.BlockingActivities.Count != 0 || second.Api.Timing.BlockingActivities.Count != 0)
				throw new InvalidDataException("Soak exchange left a blocking interaction.");
			await Task.WhenAll(first.ConsolidateSoakStacksAsync(VendorScenario.ItemId, SoakBandageStack.Value, token),
				second.ConsolidateSoakStacksAsync(VendorScenario.ItemId, SoakBandageStack.Value, token));
		}
		Task Confirmation(LiveBotSession session, byte action) => session.WaitForPacketAsync(typeof(SM_EXCHANGE_CONFIRMATION), token, packet => packet.Get<byte>("action") == action);
		static Dictionary<int, long> Totals(LiveBotSession session) => session.Api.World.Inventory.Values.GroupBy(item => item.ItemId)
			.ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
	}
}
