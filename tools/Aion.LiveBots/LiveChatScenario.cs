using System.Globalization;
using Aion.Bots.Gm;
using Aion.Bots.Protocol;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	// BA-002 player-facing cases. Bridge outage/pending-request timeout needs a separate owned fault run.
	private static async Task<int> RunB2Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		if (options.BotCount != 2) throw new InvalidOperationException("B2 requires two subjects plus one director.");
		await using var first = new L0Actor(options, problems, 1, Race.ELYOS, characterName: "Aelivechata");
		await using var second = new L0Actor(options, problems, 2, Race.ELYOS, characterName: "Aelivechatb");
		await using var director = new L0Actor(options, problems, 99, Race.ELYOS,
			bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		L0Actor[] actors = [first, second, director];
		try
		{
			foreach (var actor in actors)
			{
				actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "B2" });
				await actor.StepAsync("login-create-enter", async ct =>
				{
					await actor.Session.LoginAndAuthenticateAsync(ct);
					await actor.Session.CreateCharacterAsync(ct);
					await actor.Session.EnterWorldAsync(ct);
				}, token);
			}
			await first.StepAsync("duplicate-auth-latest-token-and-channel", first.Session.ConnectDuplicateChatAsync, token);
			await second.StepAsync("ordinary-auth-and-channel", second.Session.ConnectChatAsync, token);
			await DeliverAsync("initial-delivery");
			await first.StepAsync("chat-client-disconnect-confirmed", first.Session.DisconnectChatVerifiedAsync, token);
			await first.StepAsync("chat-client-reconnect-fresh-auth", first.Session.ConnectDuplicateChatAsync, token);
			await DeliverAsync("delivery-after-client-reconnect");

			var gm = director.Session.CreateLiveGmFacade();
			await director.StepAsync("gag-ordinary-player-five-minutes", ct => gm.ExecuteAsync(
				new GmCommand("gag", [first.Session.CharacterName, "5", "B2 bridge verification"], "is now gagged for 5 minute(s)."),
				cancellationToken: ct), token);
			// Authenticate again while GS still holds the gag: the auth response must replay it to Chat.
			await first.StepAsync("gagged-client-disconnect-confirmed", first.Session.DisconnectChatVerifiedAsync, token);
			await first.StepAsync("gagged-duplicate-auth-and-channel", first.Session.ConnectDuplicateChatAsync, token);
			await first.StepAsync("gag-enforced-after-auth", ct => first.Session.VerifyGagEnforcementAsync(second.Session, ct), token);
			await director.StepAsync("remove-gag", ct => gm.ExecuteAsync(
				new GmCommand("gag", [first.Session.CharacterName, "remove"], "from all chats."), cancellationToken: ct), token);
			await first.StepAsync("ungagged-client-disconnect-confirmed", first.Session.DisconnectChatVerifiedAsync, token);
			await first.StepAsync("ungagged-auth-and-channel", first.Session.ConnectChatAsync, token);
			await DeliverAsync("delivery-after-ungag-and-auth");
			foreach (var actor in actors)
			{
				await actor.StepAsync("quit", actor.Session.QuitAsync, token);
				actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "B2" });
			}
			Console.WriteLine("LIVE B2: Chat auth, duplicate token refresh, client disconnect/reconnect and gag replay passed; bridge fault cases are separate.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception) { Console.Error.WriteLine($"B2 failed: {exception}"); return 1; }

		Task DeliverAsync(string marker) => first.StepAsync(marker, async ct =>
		{
			await first.Session.SendChatMessageAsync("B2 " + marker, ct);
			await first.Session.ReceiveChatMessageAsync("B2 " + marker, ct);
			await second.Session.ReceiveChatMessageAsync("B2 " + marker, ct);
		}, token);
	}
}

internal static class LiveChatContract
{
	internal static void RequireAuthResponse(ReadOnlySpan<byte> payload)
	{
		if (!payload.SequenceEqual(new byte[] { 2, 0x40, 1, 0, 0, 0, 0, 0, 0x22, 8 }))
			throw new InvalidDataException("Chat authentication did not return the complete SM_PLAYER_AUTH_RESPONSE.");
	}

	internal static void RequireTokenRefresh(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
	{
		// Java ChatService generates 16 random bytes followed by a stable 32-byte account digest.
		if (first.Length != 48 || second.Length != 48 || first[..16].SequenceEqual(second[..16]) ||
			!first[16..].SequenceEqual(second[16..]))
			throw new InvalidDataException("Duplicate Chat auth did not return distinct valid tokens for the same account.");
	}

	internal static void RequireGagResponse(string text)
	{
		const string prefix = "You have been gagged for ", suffix = " minutes.";
		if (!text.StartsWith(prefix, StringComparison.Ordinal) || !text.EndsWith(suffix, StringComparison.Ordinal) ||
			!int.TryParse(text.AsSpan(prefix.Length, text.Length - prefix.Length - suffix.Length), NumberStyles.None,
				CultureInfo.InvariantCulture, out int minutes) || minutes is < 0 or > 5)
			throw new InvalidDataException($"Expected a five-minute gag refusal, received '{text}'.");
	}

	internal static void RequireControlBarrier(IReadOnlyList<string> messages, string barrier)
	{
		if (messages.Count != 1 || messages[0] != barrier)
			throw new InvalidDataException("Gagged text leaked to the control player, or the positive delivery barrier was missing.");
	}
}

internal sealed partial class LiveBotSession
{
	public async Task ConnectDuplicateChatAsync(CancellationToken token)
	{
		if (chatClient != null) throw new InvalidOperationException("Duplicate auth probe requires no existing Chat socket.");
		await SendGameAsync(GameClientPackets.ChatAuth(characterId, macBytes), token);
		await SendGameAsync(GameClientPackets.ChatAuth(characterId, macBytes), token);
		var first = await WaitForGamePacketAsync(typeof(SM_CHAT_INIT), token);
		var second = await WaitForGamePacketAsync(typeof(SM_CHAT_INIT), token);
		LiveChatContract.RequireTokenRefresh(first.Get<byte[]>("token"), second.Get<byte[]>("token"));
		trace.WriteAction(currentStep, "chat:duplicate-auth-token-refresh", new Dictionary<string, object?> { ["responses"] = 2 });
		await OpenChatAsync(second, token);
	}

	public async Task DisconnectChatVerifiedAsync(CancellationToken token)
	{
		var client = chatClient ?? throw new InvalidOperationException("No Chat client to disconnect.");
		client.Client.Shutdown(System.Net.Sockets.SocketShutdown.Send);
		if (await chatStream!.ReadAsync(new byte[1], token) != 0)
			throw new InvalidDataException("Unexpected pending Chat bytes before disconnect acknowledgment.");
		trace.WriteAction(currentStep, "chat:peer-closed-after-client-fin", new Dictionary<string, object?>());
		await CloseChatAsync();
	}

	public async Task VerifyGagEnforcementAsync(LiveBotSession observer, CancellationToken token)
	{
		const string forbidden = "B2 must not broadcast while gagged", barrier = "B2 control barrier";
		await SendChatMessageAsync(forbidden, token);
		string reply = await ReadChatMessageTextAsync(token);
		// Await a positive marker, not a silence timeout. Retain leaked text as failed evidence.
		await observer.SendChatMessageAsync(barrier, token);
		var control = new List<string> { await observer.ReadChatMessageTextAsync(token) };
		if (control[0] != barrier) control.Add(await observer.ReadChatMessageTextAsync(token));
		string subjectBarrier = await ReadChatMessageTextAsync(token);
		await PublishAsync("chat-gag-evidence.json", new { schemaVersion = 1, run = options.Run,
			subject = characterId, control = observer.characterId, forbidden, reply, barrier,
			controlMessages = control, subjectBarrier }, token);
		try
		{
			LiveChatContract.RequireGagResponse(reply);
			LiveChatContract.RequireControlBarrier(control, barrier);
			LiveChatContract.RequireControlBarrier([subjectBarrier], barrier);
		}
		catch (InvalidDataException exception)
		{
			await problems.WriteAsync(options.Run, bot, account, currentStep, "chat-gag-not-enforced", exception.Message, exception);
			throw new LiveBotFailureException("Chat gag enforcement failed; see chat-gag-evidence.json.", exception);
		}
	}

	private async Task<string> ReadChatMessageTextAsync(CancellationToken token)
	{
		var payload = await ReadChatPayloadAsync(token);
		RequireChatOpcode(payload, 0x1A, "SM_CHANNEL_MESSAGE");
		string text = ExtractChannelMessageText(payload);
		trace.WriteAction(currentStep, "SM_CHANNEL_MESSAGE", new Dictionary<string, object?> { ["message"] = text });
		return text;
	}
}
