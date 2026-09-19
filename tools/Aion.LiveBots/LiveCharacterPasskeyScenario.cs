using System.Buffers.Binary;
using System.Net.Sockets;
using Aion.Bots.Gm;
using Aion.Bots.Protocol;
using Aion.Bots.Protocol.Login;
using Aion.Bots.Scenarios;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.LoginServer.Network.Aion;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static async Task<int> RunL4Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		await using var subject = new L0Actor(options, problems, 1, Race.ELYOS, characterName: "Livepasskey");
		await using var director = new L0Actor(options, problems, 99, Race.ELYOS, bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		try
		{
			if (options.Profile != "docker-bots-passkey") throw new InvalidOperationException("L4 requires the isolated passkey-enabled profile.");
			foreach (var actor in new[] { subject, director }) actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "L4" });
			await director.StepAsync("director-auth-create-and-passkey-enter", async ct =>
			{
				await director.Session.LoginAndAuthenticateAsync(ct); await director.Session.CreateCharacterAsync(ct);
				await director.Session.SendPacketAsync(director.Session.Api.EnterWorld(director.Session.CharacterId), ct);
				CharacterPasskeyScenario.VerifyWindow(await director.Session.WaitForPacketAsync(typeof(SM_CHARACTER_SELECT), ct), 0);
				var value = CharacterPasskeyScenario.WireValue("654321");
				await director.Session.SendPacketAsync(GameClientPackets.SetCharacterPasskey(value), ct);
				CharacterPasskeyScenario.VerifyResult(await director.Session.WaitForPacketAsync(typeof(SM_CHARACTER_SELECT), ct), 0, 0);
				await director.Session.SendPacketAsync(GameClientPackets.SubmitCharacterPasskey(value), ct);
				CharacterPasskeyScenario.VerifyResult(await director.Session.WaitForPacketAsync(typeof(SM_CHARACTER_SELECT), ct), 3, 0);
				await director.Session.CompleteWorldEntryAsync(ct); await director.Session.SynchronizeAsync(ct);
			}, token);
			await CharacterPasskeyScenario.RunAsync(new LiveCharacterPasskeyDriver(subject, director), token);
			foreach (var actor in new[] { subject, director })
			{
				await actor.StepAsync("quit-after-passkey-recovery", async ct => { await actor.Session.SynchronizeAsync(ct); await actor.Session.QuitAsync(ct); await actor.Session.VerifyOfflineAsync(ct); }, token);
				actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "L4" });
			}
			Console.WriteLine("LIVE L4: passkey create/update, five-attempt kick, blocked-IP login refusal, director reset and fresh-login recovery passed.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception) { Console.Error.WriteLine($"L4 failed: {exception}"); return 1; }
	}

	private sealed class LiveCharacterPasskeyDriver(L0Actor subject, L0Actor director) : ICharacterPasskeyDriver
	{
		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token) => subject.StepAsync(action, operation, token);
		public async Task PrepareAsync(CancellationToken token)
		{
			await subject.Session.LoginAndAuthenticateAsync(token); await subject.Session.CreateCharacterAsync(token);
		}
		public async Task<DecodedBotServerPacket> RequestEntryAsync(CancellationToken token)
		{
			await subject.Session.SendPacketAsync(subject.Session.Api.EnterWorld(subject.Session.CharacterId), token);
			return await subject.Session.WaitForPacketAsync(typeof(SM_CHARACTER_SELECT), token);
		}
		public async Task<DecodedBotServerPacket> ExchangeAsync(BotClientPacket packet, CancellationToken token)
		{
			int start = subject.Session.PacketHistory.Count;
			await subject.Session.SendPacketAsync(packet, token);
			var response = await subject.Session.WaitForPacketAsync(typeof(SM_CHARACTER_SELECT), token);
			if (!response.Get<bool>("accepted"))
			{
				await subject.Session.SynchronizeAsync(token);
				if (subject.Session.PacketHistory.Skip(start).Any(p => p.PacketType == typeof(SM_PLAYER_SPAWN)))
					throw new InvalidDataException("A rejected passkey unexpectedly entered the world.");
			}
			return response;
		}
		public async Task FinishEntryAsync(CancellationToken token)
		{
			await subject.Session.CompleteWorldEntryAsync(token); await subject.Session.SynchronizeAsync(token);
			await subject.Session.VerifyInventoryAsync(token);
		}
		public async Task ReloginAsync(CancellationToken token)
		{
			await subject.StepAsync("quit-confirm-offline", async ct => { await subject.Session.QuitAsync(ct); await subject.Session.VerifyOfflineAsync(ct); }, token);
			await subject.StepAsync("honor-reentry-delay", subject.Session.WaitForReentryAsync, token);
			await subject.StepAsync("fresh-authentication", subject.Session.ReloginAndVerifyPersistenceAsync, token);
		}
		public Task VerifyNoBanAsync(CancellationToken token) => subject.Session.SynchronizeAsync(token);
		public async Task LockoutAsync(byte[] wrongValue, CancellationToken token)
		{
			await subject.Session.ExpectPasskeyLockoutAsync(wrongValue, token);
			await subject.Session.VerifyOfflineAsync(token);
			await subject.Session.VerifyPasskeyLoginRefusedAsync(token);
			await Task.Delay(TimeSpan.FromMilliseconds(250), token);
			await subject.Session.VerifyPasskeyLoginRefusedAsync(token);
		}
		public async Task ResetAsync(string digits, CancellationToken token)
		{
			await director.Session.CreateLiveGmFacade().ExecuteAsync(new GmCommand("passkeyreset", [subject.Session.CharacterName, digits],
				"was successfully removed from block list"), cancellationToken: token);
		}
		public Task LoginAfterResetAsync(CancellationToken token) => subject.Session.ReloginAndVerifyPersistenceAsync(token);
	}
}

internal sealed partial class LiveBotSession
{
	public async Task ExpectPasskeyLockoutAsync(byte[] wrongValue, CancellationToken token)
	{
		quitExpected = true;
		try
		{
			await SendGameAsync(GameClientPackets.SubmitCharacterPasskey(wrongValue), token);
			while (true)
			{
				DecodedBotServerPacket packet;
				try { packet = await ReadNextAsync(token); }
				catch (EndOfStreamException)
				{
					// This read has completed and its expected EOF was consumed here. Do not re-await
					// the same fault during connection disposal; other read failures still fail L4.
					activeMoveNext = null;
					break;
				}
				if (packet.PacketType == typeof(SM_CHARACTER_SELECT)) CharacterPasskeyScenario.VerifyResult(packet, 3, 5);
				if (packet.PacketType == typeof(SM_PLAYER_SPAWN)) throw new InvalidDataException("Wrong passkey entered the world instead of locking out.");
			}
			await CloseConnectionAsync(token);
			trace.WriteAction(currentStep, "passkey:server-disconnected");
		}
		finally { quitExpected = false; }
	}

	public async Task VerifyPasskeyLoginRefusedAsync(CancellationToken token)
	{
		using var client = new TcpClient(options.LoginEndPoint.AddressFamily) { NoDelay = true };
		await client.ConnectAsync(options.LoginEndPoint.Address, options.LoginEndPoint.Port, token);
		var stream = client.GetStream();
		var protocol = await LoginClientProtocol.ReadInitAsync(stream, token);
		await stream.WriteAsync(protocol.Crypto.CreateAuthGameGuardFrame(protocol.Init.SessionId), token);
		byte[] guard = protocol.Crypto.DecryptServerFrame(await LoginClientProtocol.ReadFrameAsync(stream, token));
		if (guard[0] != 0x0B || BinaryPrimitives.ReadInt32LittleEndian(guard.AsSpan(1)) != protocol.Init.SessionId)
			throw new InvalidDataException("Blocked-login probe did not finish game-guard exchange.");
		await stream.WriteAsync(protocol.Crypto.CreateLoginFrame(protocol.PublicParameters, protocol.Init.SessionId, account, Password), token);
		byte[] reply = protocol.Crypto.DecryptServerFrame(await LoginClientProtocol.ReadFrameAsync(stream, token));
		if (reply[0] != 1 || BinaryPrimitives.ReadInt32LittleEndian(reply.AsSpan(1)) != (int)AionAuthResponse.STR_L2AUTH_S_BLOCKED_IP)
			throw new InvalidDataException("Passkey lockout did not produce the expected blocked-IP login refusal.");
		trace.WriteAction(currentStep, "passkey:login-refused", new Dictionary<string, object?> { ["response"] = "STR_L2AUTH_S_BLOCKED_IP", ["code"] = 22 });
	}
}
