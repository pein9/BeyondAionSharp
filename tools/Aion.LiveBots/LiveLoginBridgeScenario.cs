using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text.Json;
using Aion.Bots.Gm;
using Aion.Bots.Protocol;
using Aion.Bots.Protocol.Login;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static async Task<int> RunB3Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		await using var subject = new L0Actor(options, problems, 1, Race.ELYOS, characterName: "Aelivebridge");
		await using var director = new L0Actor(options, problems, 99, Race.ELYOS,
			bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		L0Actor[] actors = [subject, director];
		try
		{
			foreach (var actor in actors)
			{
				actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "B3" });
				await actor.StepAsync("login-create-enter", async ct =>
				{
					await actor.Session.LoginAndAuthenticateAsync(ct);
					await actor.Session.CreateCharacterAsync(ct);
					await actor.Session.EnterWorldAsync(ct);
				}, token);
			}
			await subject.StepAsync("duplicate-login-refusal-and-ls-kick", subject.Session.VerifyDuplicateLoginKickAsync, token);
			await ReenterAsync();
			await subject.StepAsync("return-to-selection-for-fast-reconnect", async ct =>
			{
				await subject.Session.ReturnToSelectionAsync(false, ct);
				await subject.Session.VerifyOfflineAsync(ct);
			}, token);
			await subject.StepAsync("fast-reconnect-key-consumption-and-replay-refusal", subject.Session.VerifyFastReconnectAsync, token);
			await subject.StepAsync("reenter-after-fast-reconnect", async ct =>
			{
				await subject.Session.WaitForReentryAsync(ct);
				await subject.Session.EnterWorldAsync(ct);
			}, token);
			await subject.StepAsync("ordinary-access-before-grant", ct => subject.Session.VerifyBridgeAccessAsync(0, ct), token);
			var gm = director.Session.CreateLiveGmFacade();
			// This tests account control itself, not privileged gameplay. Never relog or perform game actions at level 1.
			foreach (int level in new[] { 1, 0 })
			{
				await director.StepAsync("grant-access-" + level, ct => gm.ExecuteAsync(new GmCommand("grant",
					["a", level.ToString(System.Globalization.CultureInfo.InvariantCulture), subject.Session.CharacterName],
					$"Account of {subject.Session.CharacterName} has been granted access level {level}."), cancellationToken: ct), token);
				await subject.StepAsync("observe-access-" + level, async ct =>
				{
					await subject.Session.WaitForAnyPacketAsync(p => p.PacketType == typeof(SM_MESSAGE) &&
						p.Get<string>("message").Contains($"You have been granted access level {level} by ", StringComparison.Ordinal), ct);
					await subject.Session.VerifyBridgeAccessAsync(level, ct);
				}, token);
			}
			await subject.StepAsync("quit-after-access-revocation", async ct =>
			{
				await subject.Session.QuitAsync(ct); await subject.Session.VerifyOfflineAsync(ct);
			}, token);
			await ReenterAsync();
			await subject.StepAsync("revocation-persists-through-relogin", ct => subject.Session.VerifyBridgeAccessAsync(0, ct), token);
			await subject.StepAsync("prepare-expected-account-ban-kick", subject.Session.PrepareAccountBanKickAsync, token);
			await director.StepAsync("ban-account-only-one-minute", ct => gm.ExecuteAsync(new GmCommand("ban",
				[subject.Session.CharacterName, "account", "1"], $"Account ID {subject.Session.AccountId} was successfully banned for 1 minutes"), cancellationToken: ct), token);
			await subject.StepAsync("ls-ban-kick-and-repeated-login-refusal", async ct =>
			{
				await subject.Session.ObserveAccountBanKickAsync(ct);
				await subject.Session.VerifyAccountBanRefusalAsync(ct);
				await subject.Session.VerifyAccountBanRefusalAsync(ct);
			}, token);
			await subject.StepAsync("natural-ban-expiry-no-state-reset", async ct =>
			{
				// Start after the ban acknowledgement/refusals: never shorten the server's real one-minute penalty.
				await Task.Delay(TimeSpan.FromSeconds(61), ct);
				await subject.Session.VerifyOfflineAsync(ct);
				await subject.Session.WaitForReentryAsync(ct);
				await subject.Session.ReloginAndVerifyPersistenceAsync(ct);
				await subject.Session.EnterWorldAsync(ct);
				await subject.Session.VerifyBridgeAccessAsync(0, ct);
			}, token);
			foreach (var actor in actors)
			{
				await actor.StepAsync("quit-and-verify-offline", async ct =>
				{
					await actor.Session.QuitAsync(ct); await actor.Session.VerifyOfflineAsync(ct);
				}, token);
				actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "B3" });
			}
			Console.WriteLine("LIVE B3: duplicate-login kick, consumed reconnect key, access grant/revoke, account-only ban and natural expiry passed.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception) { Console.Error.WriteLine($"B3 failed: {exception}"); return 1; }

		Task ReenterAsync() => subject.StepAsync("ordinary-relogin-and-reenter", async ct =>
		{
			await subject.Session.VerifyOfflineAsync(ct);
			await subject.Session.WaitForReentryAsync(ct);
			await subject.Session.ReloginAndVerifyPersistenceAsync(ct);
			await subject.Session.EnterWorldAsync(ct);
		}, token);
	}
}

internal static class LiveLoginBridgeContract
{
	internal static int ReadUpdatedSession(ReadOnlySpan<byte> payload, int accountId)
	{
		// Login crypto retains checksum/padding; only the ten-byte Java response prefix is application data.
		if (payload.Length < 10 || payload[0] != 0x0C || BinaryPrimitives.ReadInt32LittleEndian(payload[1..]) != accountId || payload[9] != 0)
			throw new InvalidDataException("Fast reconnect did not return this account's successful SM_UPDATE_SESSION.");
		return BinaryPrimitives.ReadInt32LittleEndian(payload[5..]);
	}

	internal static void RequireBanned(ReadOnlySpan<byte> payload)
	{
		if (payload.Length < 1 || payload[0] != 9) throw new InvalidDataException("Expected SM_ACCOUNT_BANNED_2, not another login refusal.");
	}
}

internal sealed partial class LiveBotSession
{
	public int AccountId => accountId;

	public async Task VerifyBridgeAccessAsync(int expected, CancellationToken token)
	{
		await SynchronizeAsync(token);
		using var request = new HttpRequestMessage(HttpMethod.Get, $"admin/player-state?characterName={Uri.EscapeDataString(characterName)}");
		request.Headers.Add("X-Admin-Token", options.AdminToken);
		using var response = await AdminClient.SendAsync(request, token);
		response.EnsureSuccessStatusCode();
		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
		var root = document.RootElement;
		if (!root.GetProperty("online").GetBoolean() || root.GetProperty("player").GetProperty("characterId").GetInt32() != characterId ||
			root.GetProperty("player").GetProperty("accessLevel").GetInt32() != expected)
			throw new InvalidDataException("LS access grant/revoke did not update the live subject.");
		trace.WriteAction(currentStep, "login-bridge:access-observed", new Dictionary<string, object?> { ["accessLevel"] = expected });
	}

	public async Task VerifyFastReconnectAsync(CancellationToken token)
	{
		await StopLifecyclePingAsync();
		quitExpected = true;
		int key;
		try
		{
			await SendGameAsync(GameClientPackets.ReconnectAuth(), token);
			key = (await WaitForGamePacketAsync(typeof(SM_RECONNECT_KEY), token)).Get<int>("key");
			await ObserveLifecycleCloseAsync(false, token);
		}
		finally { quitExpected = false; }
		using var client = new TcpClient(options.LoginEndPoint.AddressFamily) { NoDelay = true };
		await client.ConnectAsync(options.LoginEndPoint.Address, options.LoginEndPoint.Port, token);
		var stream = client.GetStream();
		var protocol = await LoginClientProtocol.ReadInitAsync(stream, token);
		int originalLoginOk = loginOk;
		await stream.WriteAsync(protocol.Crypto.CreateUpdateSessionFrame(accountId, originalLoginOk, key), token);
		loginOk = LiveLoginBridgeContract.ReadUpdatedSession(protocol.Crypto.DecryptServerFrame(await LoginClientProtocol.ReadFrameAsync(stream, token)), accountId);
		trace.WriteAction(currentStep, "login-bridge:fast-session-authenticated", new Dictionary<string, object?> { ["accountId"] = accountId });
		// A consumed key must close a new Login connection without authenticating it.
		using (var replay = new TcpClient(options.LoginEndPoint.AddressFamily) { NoDelay = true })
		{
			await replay.ConnectAsync(options.LoginEndPoint.Address, options.LoginEndPoint.Port, token);
			var replayStream = replay.GetStream();
			var replayProtocol = await LoginClientProtocol.ReadInitAsync(replayStream, token);
			await replayStream.WriteAsync(replayProtocol.Crypto.CreateUpdateSessionFrame(accountId, originalLoginOk, key), token);
			if (await replayStream.ReadAsync(new byte[1], token) != 0) throw new InvalidDataException("Consumed reconnect key returned data instead of closing.");
		}
		trace.WriteAction(currentStep, "login-bridge:reconnect-key-replay-closed");
		// Continue the original key-authenticated socket, with no password login fallback.
		await stream.WriteAsync(protocol.Crypto.CreateServerListFrame(accountId, loginOk), token);
		LiveLoginSelection.RequireOnlineServer(protocol.Crypto.DecryptServerFrame(await LoginClientProtocol.ReadFrameAsync(stream, token)), 1);
		await stream.WriteAsync(protocol.Crypto.CreatePlayFrame(accountId, loginOk, 1), token);
		var play = await LiveLoginSelection.ReadPlayOkAsync(async ct => protocol.Crypto.DecryptServerFrame(await LoginClientProtocol.ReadFrameAsync(stream, ct)), 1,
			_ => trace.WriteAction(currentStep, "login-server-list-update"), token);
		playOk1 = BinaryPrimitives.ReadInt32LittleEndian(play.AsSpan(1, 4));
		playOk2 = BinaryPrimitives.ReadInt32LittleEndian(play.AsSpan(5, 4));
		var list = await AuthenticateGameCharacterListAsync(token);
		var character = list.Get<List<IReadOnlyDictionary<string, object?>>>("characters").Single();
		if (Get<int>(character, "objectId") != characterId || Get<string>(character, "name") != characterName)
			throw new InvalidDataException("Fast reconnect lost or switched the existing character.");
		AssertPersistedPosition(Get<int>(character, "mapId"), Get<float>(character, "x"), Get<float>(character, "y"), Get<float>(character, "z"));
	}

	public async Task PrepareAccountBanKickAsync(CancellationToken token)
	{
		await SynchronizeAsync(token); await StopLifecyclePingAsync(); quitExpected = true;
	}

	public async Task ObserveAccountBanKickAsync(CancellationToken token)
	{
		try { await ObserveLifecycleCloseAsync(false, token); }
		finally { quitExpected = false; }
		trace.WriteAction(currentStep, "login-bridge:account-ban-kick");
	}

	public async Task VerifyAccountBanRefusalAsync(CancellationToken token)
	{
		using var client = new TcpClient(options.LoginEndPoint.AddressFamily) { NoDelay = true };
		await client.ConnectAsync(options.LoginEndPoint.Address, options.LoginEndPoint.Port, token);
		var stream = client.GetStream();
		var protocol = await LoginClientProtocol.ReadInitAsync(stream, token);
		await stream.WriteAsync(protocol.Crypto.CreateAuthGameGuardFrame(protocol.Init.SessionId), token);
		byte[] guard = protocol.Crypto.DecryptServerFrame(await LoginClientProtocol.ReadFrameAsync(stream, token));
		if (guard.Length < 5 || guard[0] != 0x0B || BinaryPrimitives.ReadInt32LittleEndian(guard.AsSpan(1)) != protocol.Init.SessionId)
			throw new InvalidDataException("Banned-login probe failed game guard exchange.");
		await stream.WriteAsync(protocol.Crypto.CreateLoginFrame(protocol.PublicParameters, protocol.Init.SessionId, account, Password), token);
		LiveLoginBridgeContract.RequireBanned(protocol.Crypto.DecryptServerFrame(await LoginClientProtocol.ReadFrameAsync(stream, token)));
		if (await stream.ReadAsync(new byte[1], token) != 0) throw new InvalidDataException("Banned Login connection did not close.");
		trace.WriteAction(currentStep, "login-bridge:account-banned-refusal", new Dictionary<string, object?> { ["opcode"] = 9 });
	}
}
