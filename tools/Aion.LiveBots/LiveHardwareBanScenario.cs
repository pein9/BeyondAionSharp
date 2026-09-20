using System.Diagnostics;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static async Task<int> RunB4Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		if (options.BotCount != 5 || options.Profile != "docker-bots-hardware" || options.StepTimeout < TimeSpan.FromSeconds(180))
			throw new InvalidOperationException("B4 requires five subjects and the owned hardware restart profile.");
		L0Actor[] actors = Enumerable.Range(1, 5).Select(i => new L0Actor(options, problems, i)).ToArray();
		var control = actors[4];
		try
		{
			foreach (var actor in actors) actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "B4" });
			await control.StepAsync("unbanned-control-create-enter", async ct =>
			{
				await control.Session.LoginAndAuthenticateAsync(ct);
				await control.Session.CreateCharacterAsync(ct);
				await control.Session.EnterWorldAsync(ct);
				await control.Session.VerifyBridgeAccessAsync(0, ct);
			}, token);
			await RefusalsAsync("before-restart");
			await control.StepAsync("owned-login-kill-live-game-and-fresh-reload", control.Session.VerifyHardwareRestartAsync, token);
			await RefusalsAsync("after-restart");
			await control.StepAsync("unbanned-control-quit", async ct =>
			{
				await control.Session.QuitAsync(ct); await control.Session.VerifyOfflineAsync(ct);
			}, token);
			await control.StepAsync("unbanned-control-fresh-login-after-reload", async ct =>
			{
				await control.Session.WaitForReentryAsync(ct);
				await control.Session.ReloginAndVerifyPersistenceAsync(ct);
				await control.Session.EnterWorldAsync(ct);
				await control.Session.VerifyBridgeAccessAsync(0, ct);
				await control.Session.QuitAsync(ct); await control.Session.VerifyOfflineAsync(ct);
			}, token);
			foreach (var actor in actors) actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "B4" });
			Console.WriteLine("LIVE B4: seasonal MAC/HDD refusal before/after owned Login restart, fresh synchronization and unbanned control passed.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception) { Console.Error.WriteLine($"B4 failed: {exception}"); return 1; }
		finally { foreach (var actor in actors) await actor.DisposeAsync(); }

		async Task RefusalsAsync(string phase)
		{
			foreach (var actor in actors.Take(4))
				await actor.StepAsync(phase + "-hardware-refusal", actor.Session.VerifyHardwareBanRefusalAsync, token);
			await control.StepAsync(phase + "-control-still-online", ct => control.Session.VerifyBridgeAccessAsync(0, ct), token);
		}
	}
}

internal static class LiveHardwareBanContract
{
	internal static void RequireRefusal(bool ok, string? actualAccount, string expectedAccount)
	{
		// Java includes the authenticated account name on hardware refusal; generic LS failure is insufficient.
		if (ok || !string.Equals(actualAccount, expectedAccount, StringComparison.Ordinal))
			throw new InvalidDataException("Expected this authenticated account's hardware-ban refusal.");
	}
}

internal sealed partial class LiveBotSession
{
	public async Task VerifyHardwareBanRefusalAsync(CancellationToken token)
	{
		await LoginServerAsync(token);
		try
		{
			var auth = await AuthenticateGameAsync(true, token);
			LiveHardwareBanContract.RequireRefusal(auth.Get<bool>("ok"), auth.Get<string>("accountName"), account);
			await StopLifecyclePingAsync();
			await ObserveLifecycleCloseAsync(false, token);
			trace.WriteAction(currentStep, "hardware:authenticated-account-refused-and-closed", new Dictionary<string, object?>
				{ ["accountId"] = accountId, ["mac"] = macAddress, ["hdd"] = $"E2E-{bot.ToUpperInvariant()}" });
		}
		finally { quitExpected = false; }
	}

	public async Task VerifyHardwareRestartAsync(CancellationToken token)
	{
		await PublishAsync("login-crash-request.json", new { schemaVersion = 1, run = options.Run, characterId, refusedBots = new[] { "b01", "b02", "b03", "b04" } }, token);
		using var killed = await ReadLifecycleReceiptAsync("login-server-killed.json", drain: true, token);
		if (killed.RootElement.GetProperty("exitCode").GetInt32() != 137) throw new InvalidDataException("Login did not exit by planned SIGKILL.");
		var clock = Stopwatch.StartNew(); int replies = 0;
		do { await SynchronizeAsync(token); replies++; await Task.Delay(100, token); } while (clock.Elapsed < TimeSpan.FromSeconds(3));
		await PublishAsync("login-outage-observed.json", new { schemaVersion = 1, run = options.Run, characterId, gameReplies = replies, elapsedSeconds = clock.Elapsed.TotalSeconds }, token);
		using var restarted = await ReadLifecycleReceiptAsync("login-server-restarted.json", drain: true, token);
		foreach (string field in new[] { "containerId", "imageId" })
			if (killed.RootElement.GetProperty(field).GetString() != restarted.RootElement.GetProperty(field).GetString()) throw new InvalidDataException("Login restart changed " + field);
		int before = restarted.RootElement.GetProperty("before").GetProperty("generation").GetInt32();
		int after = restarted.RootElement.GetProperty("after").GetProperty("generation").GetInt32();
		if (before <= 0 || after <= before) throw new InvalidDataException("No fresh hardware synchronization generation.");
		trace.WriteAction(currentStep, "hardware:owned-login-reload", new Dictionary<string, object?> { ["beforeGeneration"] = before, ["afterGeneration"] = after, ["outageGameReplies"] = replies });
	}
}
