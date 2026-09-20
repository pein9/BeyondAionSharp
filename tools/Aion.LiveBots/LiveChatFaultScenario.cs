using System.Diagnostics;
using System.Text.Json;
using Aion.Bots.Protocol;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static async Task<int> RunB2FAsync(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		if (options.BotCount != 2 || options.Profile != "docker-bots-chat-fault" || options.StepTimeout < TimeSpan.FromSeconds(180))
			throw new InvalidOperationException("B2F requires two subjects, the owned Chat fault profile and 180 seconds per step.");
		await using var first = new L0Actor(options, problems, 1, Race.ELYOS, characterName: "Aelivefaulta");
		await using var second = new L0Actor(options, problems, 2, Race.ELYOS, characterName: "Aelivefaultb");
		L0Actor[] actors = [first, second];
		try
		{
			foreach (var actor in actors)
			{
				actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "B2F" });
				await actor.StepAsync("create-enter-and-chat", async ct =>
				{
					await actor.Session.LoginAndAuthenticateAsync(ct);
					await actor.Session.CreateCharacterAsync(ct);
					await actor.Session.EnterWorldAsync(ct);
					await actor.Session.ConnectChatAsync(ct);
				}, token);
			}
			await DeliverAsync("before-fault");
			await first.StepAsync("request-owned-chat-crash", ct => first.Session.RequestChatCrashAsync(second.Session.CharacterId, ct), token);
			foreach (var actor in actors)
				await actor.StepAsync("observe-chat-close-and-live-game", actor.Session.ObserveChatCrashAsync, token);
			await first.StepAsync("chat-auth-unavailable-with-live-game-barriers", first.Session.VerifyChatUnavailableAsync, token);
			foreach (var actor in actors)
			{
				await actor.StepAsync("await-owned-chat-bridge-recovery", actor.Session.WaitForChatRecoveryAsync, token);
				await actor.StepAsync("fresh-chat-auth-after-restart", actor.Session.ConnectChatAsync, token);
			}
			await DeliverAsync("after-fault");
			foreach (var actor in actors)
			{
				await actor.StepAsync("quit-and-verify-offline", async ct =>
				{
					await actor.Session.QuitAsync(ct);
					await actor.Session.VerifyOfflineAsync(ct);
				}, token);
				actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "B2F" });
			}
			Console.WriteLine("LIVE B2F: owned Chat SIGKILL, client disconnects, bounded unavailable auth, unchanged live GS and fresh Chat recovery/delivery passed.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception) { Console.Error.WriteLine($"B2F failed: {exception}"); return 1; }

		Task DeliverAsync(string marker) => first.StepAsync(marker, async ct =>
		{
			await first.Session.SendChatMessageAsync("B2F " + marker, ct);
			await first.Session.ReceiveChatMessageAsync("B2F " + marker, ct);
			await second.Session.ReceiveChatMessageAsync("B2F " + marker, ct);
		}, token);
	}
}

internal sealed partial class LiveBotSession
{
	public async Task RequestChatCrashAsync(int otherSubject, CancellationToken token)
	{
		await PublishAsync("chat-crash-request.json", new { schemaVersion = 1, run = options.Run, subjects = new[] { characterId, otherSubject } }, token);
		using var receipt = await ReadLifecycleReceiptAsync("chat-server-killed.json", drain: true, token);
		if (receipt.RootElement.GetProperty("exitCode").GetInt32() != 137) throw new InvalidDataException("Chat was not killed by the planned fault.");
	}

	public async Task ObserveChatCrashAsync(CancellationToken token)
	{
		try
		{
			await ReadChatPayloadAsync(token);
			throw new InvalidDataException("Chat returned a packet instead of closing after SIGKILL.");
		}
		catch (Exception exception) when (LiveLifecycleContract.IsPeerClose(exception))
		{
			trace.WriteAction(currentStep, "chat:crashed-peer-close");
		}
		await CloseChatAsync();
		await SynchronizeAsync(token);
	}

	public async Task VerifyChatUnavailableAsync(CancellationToken token)
	{
		await SendGameAsync(GameClientPackets.ChatAuth(characterId, macBytes), token);
		var clock = Stopwatch.StartNew();
		int replies = 0;
		do
		{
			await SendGameAsync(GameClientPackets.TimeCheck(unchecked((int)Environment.TickCount64)), token);
			while (true)
			{
				var packet = await ReadNextAsync(token);
				var reflex = api.Observe(packet);
				if (reflex != null) await SendGameAsync(reflex, token);
				if (packet.PacketType == typeof(SM_CHAT_INIT)) throw new InvalidDataException("Unavailable Chat bridge delivered an auth token.");
				if (packet.PacketType == typeof(SM_TIME_CHECK)) { replies++; break; }
			}
			await Task.Delay(100, token);
		} while (clock.Elapsed < TimeSpan.FromSeconds(3));
		await PublishAsync("chat-outage-observed.json", new { schemaVersion = 1, run = options.Run,
			characterId, gameReplies = replies, elapsedSeconds = clock.Elapsed.TotalSeconds }, token);
		trace.WriteAction(currentStep, "chat:unavailable-auth-deadline", new Dictionary<string, object?> { ["gameReplies"] = replies, ["elapsedSeconds"] = clock.Elapsed.TotalSeconds });
	}

	public async Task WaitForChatRecoveryAsync(CancellationToken token)
	{
		using var killed = await ReadLifecycleReceiptAsync("chat-server-killed.json", drain: true, token);
		using var restarted = await ReadLifecycleReceiptAsync("chat-server-restarted.json", drain: true, token);
		foreach (string field in new[] { "containerId", "imageId", "gameId", "gameStartedAt" })
			if (killed.RootElement.GetProperty(field).GetString() != restarted.RootElement.GetProperty(field).GetString())
				throw new InvalidDataException("Chat recovery receipt changed " + field + ".");
		trace.WriteAction(currentStep, "chat:owned-bridge-recovered");
	}
}
