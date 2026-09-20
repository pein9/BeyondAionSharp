using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text.Json;
using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.Protocol.Login;
using Aion.Bots.Scenarios;
using Aion.Bots.Timing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.LoginServer.Network.Aion;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static async Task<int> RunO1Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		if (options.BotCount != 1 || options.Profile != "docker-bots-lifecycle" || options.StepTimeout < TimeSpan.FromSeconds(1050))
			throw new InvalidOperationException("O1 needs one subject, the owned lifecycle profile and at least 1050 seconds per step.");
		string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
		var assets = await BotNavigationAssets.LoadAsync(repoRoot, Path.Combine(options.OutputDirectory, "navigation-cache"), token);
		await using var subject = new L0Actor(options, problems, 1, Race.ASMODIANS, characterName: "Aslifecycle");
		subject.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "O1" });
		try
		{
			await subject.StepAsync("login-and-create", async ct =>
			{
				await subject.Session.LoginAndAuthenticateAsync(ct);
				await subject.Session.CreateCharacterAsync(ct);
				await subject.Session.EnterWorldAsync(ct);
				await subject.Session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), ct);
				await subject.Session.SynchronizeAsync(ct);
			}, token);
			subject.Session.Navigation = assets.StarterRoute(Race.ASMODIANS, 1);
			BotPosition initial = subject.Session.CurrentPosition;
			await subject.StepAsync("walk-first-position", async ct =>
			{
				int asak = await subject.Session.WaitForNpcAsync(203500, ct);
				await subject.Session.MoveToNpcAsync(asak, ct);
				await subject.Session.VerifyLifecyclePositionAsync(ct);
			}, token);
			BotPosition saved = subject.Session.CurrentPosition;
			await subject.StepAsync("observe-natural-periodic-save", ct => subject.Session.WaitForLifecycleSaveAsync(initial, saved, ct), token);
			await subject.StepAsync("walk-unsaved-position", async ct =>
			{
				int vandar = await subject.Session.WaitForNpcAsync(203504, ct);
				await subject.Session.MoveToNpcAsync(vandar, ct);
				await subject.Session.VerifyLifecyclePositionAsync(ct);
				await subject.Session.VerifyInventoryAsync(ct);
			}, token);
			ushort level = subject.Session.Api.World.Level;
			long kinah = subject.Session.Api.World.Kinah;
			BotInventoryItem[] inventory = subject.Session.Api.World.Inventory.Values.OrderBy(item => item.ObjectId).ToArray();
			await subject.StepAsync("hard-server-crash-and-restart", ct => subject.Session.RequestLifecycleCrashAsync(saved, ct), token);
			await subject.StepAsync("relogin-saved-checkpoint", async ct =>
			{
				await subject.Session.VerifyOfflineAsync(ct);
				await subject.Session.WaitForReentryAsync(ct);
				await subject.Session.ReloginAndVerifyPersistenceAsync(ct);
				await subject.Session.EnterWorldAsync(ct);
				await subject.Session.SynchronizeAsync(ct);
				LiveLifecycleContract.RequirePosition(subject.Session.CurrentPosition, saved);
				await subject.Session.VerifyLifecyclePositionAsync(ct);
				await subject.Session.VerifyInventoryAsync(ct);
				if (subject.Session.Api.World.Level != level || subject.Session.Api.World.Kinah != kinah ||
					!inventory.SequenceEqual(subject.Session.Api.World.Inventory.Values.OrderBy(item => item.ObjectId)))
					throw new InvalidDataException("Level, kinah or inventory changed across the server crash.");
			}, token);
			await subject.StepAsync("duplicate-login-refusal-and-kick", subject.Session.VerifyDuplicateLoginKickAsync, token);
			await subject.StepAsync("fresh-login-after-duplicate-kick", async ct =>
			{
				await subject.Session.VerifyOfflineAsync(ct);
				await subject.Session.WaitForReentryAsync(ct);
				await subject.Session.ReloginAndVerifyPersistenceAsync(ct);
				await subject.Session.EnterWorldAsync(ct);
				await subject.Session.VerifyInventoryAsync(ct);
				await subject.Session.QuitAsync(ct);
				await subject.Session.VerifyOfflineAsync(ct);
			}, token);
			subject.Trace.WriteAction(subject.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "O1" });
			Console.WriteLine("LIVE O1: natural save, hard server crash, saved-position recovery and duplicate-login kick passed.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception) { Console.Error.WriteLine($"O1 failed: {exception}"); return 1; }
	}
}

internal static class LiveLifecycleContract
{
	internal static void RequirePosition(BotPosition actual, BotPosition expected)
	{
		if (!float.IsFinite(actual.X) || !float.IsFinite(actual.Y) || !float.IsFinite(actual.Z) ||
			Math.Abs(actual.X - expected.X) > 0.1f || Math.Abs(actual.Y - expected.Y) > 0.1f || Math.Abs(actual.Z - expected.Z) > 0.1f)
			throw new InvalidDataException($"Lifecycle position differs: expected {expected}, received {actual}.");
	}

	internal static bool IsPeerClose(Exception exception) => exception is EndOfStreamException ||
		exception is SocketException { SocketErrorCode: SocketError.ConnectionReset or SocketError.ConnectionAborted } ||
		exception is IOException { InnerException: SocketException { SocketErrorCode: SocketError.ConnectionReset or SocketError.ConnectionAborted } };

	internal static void RequireDuplicateRefusal(byte[] reply)
	{
		if (reply.Length < 5 || reply[0] != 1 || BinaryPrimitives.ReadInt32LittleEndian(reply.AsSpan(1)) != (int)AionAuthResponse.STR_L2AUTH_S_ALREADY_LOGIN)
			throw new InvalidDataException("Duplicate login did not receive ALREADY_LOGIN (7).");
	}
}

internal sealed partial class LiveBotSession
{
	// Keep artifacts atomic: the owner must never parse half a checkpoint.
	private async Task PublishAsync(string name, object value, CancellationToken token)
	{
		string path = Path.Combine(options.OutputDirectory, name);
		await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(value), token);
		File.Move(path + ".tmp", path);
	}

	private object LifecyclePositionRequest(BotPosition initial, BotPosition saved)
	{
		if (Math.Abs(initial.X - saved.X) <= 0.1f && Math.Abs(initial.Y - saved.Y) <= 0.1f && Math.Abs(initial.Z - saved.Z) <= 0.1f)
			throw new InvalidDataException("Lifecycle movement did not change position.");
		return new { schemaVersion = 1, run = options.Run, characterId, characterName, worldId = 220010000,
			initial = new { x = initial.X, y = initial.Y, z = initial.Z }, saved = new { x = saved.X, y = saved.Y, z = saved.Z } };
	}

	private async Task<JsonDocument> ReadLifecycleReceiptAsync(string name, bool drain, CancellationToken token)
	{
		string path = Path.Combine(options.OutputDirectory, name);
		while (!File.Exists(path))
		{
			if (drain) await SynchronizeAsync(token);
			await Task.Delay(drain ? 1000 : 250, token);
		}
		var document = JsonDocument.Parse(await File.ReadAllTextAsync(path, token));
		if (document.RootElement.GetProperty("schemaVersion").GetInt32() != 1 || document.RootElement.GetProperty("run").GetString() != options.Run)
		{
			document.Dispose();
			throw new InvalidDataException("Lifecycle receipt run/schema mismatch.");
		}
		return document;
	}

	public async Task WaitForLifecycleSaveAsync(BotPosition initial, BotPosition saved, CancellationToken token)
	{
		await PublishAsync("lifecycle-position.json", LifecyclePositionRequest(initial, saved), token);
		using var receipt = await ReadLifecycleReceiptAsync("lifecycle-saved.json", drain: true, token);
		JsonElement row = receipt.RootElement.GetProperty("row");
		if (row.GetProperty("id").GetInt32() != characterId || row.GetProperty("online").GetInt32() != 1)
			throw new InvalidDataException("Saved checkpoint subject differs.");
		LiveLifecycleContract.RequirePosition(new BotPosition(row.GetProperty("x").GetSingle(), row.GetProperty("y").GetSingle(), row.GetProperty("z").GetSingle(), 0), saved);
		trace.WriteAction(currentStep, "lifecycle:periodic-save-observed");
	}

	private async Task StopLifecyclePingAsync()
	{
		if (pingLifetime == null || pingTask == null) throw new InvalidOperationException("No lifecycle game connection.");
		await pingLifetime.CancelAsync();
		try { await pingTask; } catch (OperationCanceledException) when (pingLifetime.IsCancellationRequested) { }
		pingTask = null;
	}

	private async Task ObserveLifecycleCloseAsync(bool requireKickMessage, CancellationToken token)
	{
		bool kickSeen = false;
		while (true)
		{
			DecodedBotServerPacket packet;
			try { packet = await ReadNextAsync(token); }
			catch (Exception exception) when (LiveLifecycleContract.IsPeerClose(exception))
			{
				activeMoveNext = null; // This completed read's expected close has been consumed.
				break;
			}
			if (packet.PacketType == typeof(SM_SYSTEM_MESSAGE) && packet.Get<string>("name") == "STR_KICK_ANOTHER_USER_TRY_LOGIN") kickSeen = true;
		}
		if (requireKickMessage && !kickSeen) throw new InvalidDataException("Duplicate login closed the original session without the expected kick message.");
		await CloseConnectionAsync(token);
		trace.WriteAction(currentStep, requireKickMessage ? "lifecycle:duplicate-kick-observed" : "lifecycle:server-close-observed");
	}

	public async Task RequestLifecycleCrashAsync(BotPosition saved, CancellationToken token)
	{
		await SynchronizeAsync(token);
		await StopLifecyclePingAsync();
		quitExpected = true;
		try
		{
			await PublishAsync("game-server-crash-request.json", LifecyclePositionRequest(saved, CurrentPosition), token);
			await ObserveLifecycleCloseAsync(requireKickMessage: false, token);
			using var killed = await ReadLifecycleReceiptAsync("game-server-killed.json", drain: false, token);
			using var restarted = await ReadLifecycleReceiptAsync("game-server-restarted.json", drain: false, token);
			if (killed.RootElement.GetProperty("exitCode").GetInt32() != 137 ||
				killed.RootElement.GetProperty("containerId").GetString() != restarted.RootElement.GetProperty("containerId").GetString() ||
				killed.RootElement.GetProperty("imageId").GetString() != restarted.RootElement.GetProperty("imageId").GetString())
				throw new InvalidDataException("Lifecycle kill/restart identity differs.");
			expectedPosition = new PersistedPosition(220010000, saved.X, saved.Y, saved.Z);
		}
		finally { quitExpected = false; }
	}

	public async Task VerifyDuplicateLoginKickAsync(CancellationToken token)
	{
		await SynchronizeAsync(token);
		await StopLifecyclePingAsync();
		quitExpected = true;
		try
		{
			using var client = new TcpClient(options.LoginEndPoint.AddressFamily) { NoDelay = true };
			await client.ConnectAsync(options.LoginEndPoint.Address, options.LoginEndPoint.Port, token);
			var stream = client.GetStream();
			var protocol = await LoginClientProtocol.ReadInitAsync(stream, token);
			await stream.WriteAsync(protocol.Crypto.CreateAuthGameGuardFrame(protocol.Init.SessionId), token);
			byte[] guard = protocol.Crypto.DecryptServerFrame(await LoginClientProtocol.ReadFrameAsync(stream, token));
			if (guard.Length < 5 || guard[0] != 0x0B || BinaryPrimitives.ReadInt32LittleEndian(guard.AsSpan(1)) != protocol.Init.SessionId)
				throw new InvalidDataException("Duplicate-login probe failed game guard exchange.");
			await stream.WriteAsync(protocol.Crypto.CreateLoginFrame(protocol.PublicParameters, protocol.Init.SessionId, account, Password), token);
			LiveLifecycleContract.RequireDuplicateRefusal(protocol.Crypto.DecryptServerFrame(await LoginClientProtocol.ReadFrameAsync(stream, token)));
			trace.WriteAction(currentStep, "lifecycle:duplicate-login-refused", new Dictionary<string, object?> { ["code"] = 7 });
			await ObserveLifecycleCloseAsync(requireKickMessage: true, token);
			// Java AionConnection.onDisconnect schedules ordinary leave up to ten seconds later.
			// The kick packet/EOF is not proof that the player has already been saved and removed.
			await Task.Delay(TimeSpan.FromSeconds(BotTimingContract.MaximumCrashLeaveDelaySeconds + 1), token);
		}
		finally { quitExpected = false; }
	}

	public async Task VerifyLifecyclePositionAsync(CancellationToken token)
	{
		await SynchronizeAsync(token);
		using var request = new HttpRequestMessage(HttpMethod.Get, $"admin/player-state?characterName={Uri.EscapeDataString(characterName)}");
		request.Headers.Add("X-Admin-Token", options.AdminToken);
		using var response = await AdminClient.SendAsync(request, token);
		response.EnsureSuccessStatusCode();
		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
		JsonElement root = document.RootElement;
		JsonElement player = root.GetProperty("player");
		if (!root.GetProperty("online").GetBoolean() || player.GetProperty("characterId").GetInt32() != characterId ||
			player.GetProperty("accessLevel").GetInt32() != 0 || player.GetProperty("worldId").GetInt32() != 220010000)
			throw new InvalidDataException("Lifecycle subject is not the ordinary online Ishalgen player.");
		LiveLifecycleContract.RequirePosition(new BotPosition(player.GetProperty("x").GetSingle(), player.GetProperty("y").GetSingle(), player.GetProperty("z").GetSingle(), 0), CurrentPosition);
	}
}
