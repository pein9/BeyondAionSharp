using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Protocol.Chat;
using Aion.Bots.Protocol.Login;
using Aion.Bots.Reflexes;
using Aion.Bots.Tracing;
using Aion.Bots.Transport;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static class LiveBotRunner
{
	public static async Task<int> RunAsync(LiveBotOptions options, CancellationToken cancellationToken = default)
	{
		Directory.CreateDirectory(options.OutputDirectory);
		Directory.CreateDirectory(Path.Combine(options.OutputDirectory, "bots"));
		await WriteRunMetadataAsync(options, cancellationToken);
		await using var problems = new LiveBotProblemWriter(Path.Combine(options.OutputDirectory, "bot.problems.jsonl"));
		if (options.Scenarios.SequenceEqual(["L0"], StringComparer.Ordinal))
			return await RunL0Async(options, problems, cancellationToken);

		var tasks = Enumerable.Range(1, options.BotCount)
			.Select(index => RunConnectBotAsync(options, problems, index, cancellationToken))
			.ToArray();
		var results = await Task.WhenAll(tasks);
		var failed = results.Count(result => !result);
		Console.WriteLine($"LIVE bots completed: {results.Length - failed} passed, {failed} failed.");
		return failed == 0 ? 0 : 1;
	}

	private static async Task<int> RunL0Async(LiveBotOptions options, LiveBotProblemWriter problems,
		CancellationToken cancellationToken)
	{
		var actors = Enumerable.Range(1, options.BotCount)
			.Select(index => new L0Actor(options, problems, index))
			.ToArray();
		try
		{
			foreach (var actor in actors)
				actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "L0" });

			await Task.WhenAll(actors.Select(a => a.StepAsync("login-game-auth", a.Session.LoginAndAuthenticateAsync, cancellationToken)));
			await Task.WhenAll(actors.Select(a => a.StepAsync("create-elyos-warrior", a.Session.CreateCharacterAsync, cancellationToken)));
			await Task.WhenAll(actors.Select(a => a.StepAsync("enter-world", a.Session.EnterWorldAsync, cancellationToken)));
			await Task.WhenAll(actors.Select(a => a.StepAsync("chat-auth-and-region-join", a.Session.ConnectChatAsync, cancellationToken)));

			const string message = "L0 channel delivery";
			await actors[0].StepAsync("send-region-message", token => actors[0].Session.SendChatMessageAsync(message, token), cancellationToken);
			await Task.WhenAll(actors.Skip(1).Select(a =>
				a.StepAsync("receive-region-message", token => a.Session.ReceiveChatMessageAsync(message, token), cancellationToken)));

			await actors[0].StepAsync("walk-10m", actors[0].Session.WalkTenMetersAsync, cancellationToken);
			await actors[0].StepAsync("ping", actors[0].Session.PingAsync, cancellationToken);
			await Task.WhenAll(actors.Select(a => a.StepAsync("quit", a.Session.QuitAsync, cancellationToken)));
			await actors[0].StepAsync("verify-offline", actors[0].Session.VerifyOfflineAsync, cancellationToken);
			await actors[0].StepAsync("wait-reentry", actors[0].Session.WaitForReentryAsync, cancellationToken);
			await actors[0].StepAsync("relogin-character-list", actors[0].Session.ReloginAndVerifyPersistenceAsync, cancellationToken);

			foreach (var actor in actors)
				actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "L0" });
			Console.WriteLine($"LIVE L0 completed: {actors.Length} bots passed.");
			return 0;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"L0 failed: {ex}");
			return 1;
		}
		finally
		{
			foreach (var actor in actors)
				await actor.DisposeAsync();
		}
	}

	private static async Task<bool> RunConnectBotAsync(LiveBotOptions options, LiveBotProblemWriter problems,
		int index, CancellationToken cancellationToken)
	{
		var bot = $"b{index:D2}";
		var account = $"{bot}r{DateTimeOffset.Now:MMdd}";
		using var trace = BotActionTraceWriter.Open(Path.Combine(options.OutputDirectory, "bots", $"{bot}.trace.jsonl"),
			options.Run, bot, account);
		await using var session = new LiveBotSession(options, problems, trace, bot, account, CharacterName(index));
		var stepNumber = 0;
		try
		{
			foreach (var scenario in options.Scenarios)
			{
				var step = $"s{++stepNumber:D2}";
				trace.WriteAction(step, "scenario:start", new Dictionary<string, object?> { ["scenario"] = scenario });
				session.BeginStep(step);
				await RunStepAsync(options, problems, trace, bot, account, step, "connect", cancellationToken,
					session.ConnectAndReadKeyAsync);
				step = $"s{++stepNumber:D2}";
				session.BeginStep(step);
				await RunStepAsync(options, problems, trace, bot, account, step, "close", cancellationToken, session.CloseAsync);
				trace.WriteAction(step, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = scenario });
			}
			return true;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"{bot} failed: {ex}");
			return false;
		}
	}

	internal static async Task RunStepAsync(LiveBotOptions options, LiveBotProblemWriter problems,
		BotActionTraceWriter trace, string bot, string account, string step, string action,
		CancellationToken cancellationToken, Func<CancellationToken, Task> operation)
	{
		trace.WriteAction(step, action);
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(options.StepTimeout);
		try
		{
			await operation(timeout.Token);
		}
		catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
		{
			await problems.WriteAsync(options.Run, bot, account, step, "timeout",
				$"Step '{action}' exceeded {options.StepTimeout.TotalSeconds:n0} seconds.", ex);
			throw new LiveBotFailureException($"{bot} {step} '{action}' timed out.", ex);
		}
		catch (TimeoutException ex)
		{
			await problems.WriteAsync(options.Run, bot, account, step, "timeout", ex.Message, ex);
			throw new LiveBotFailureException($"{bot} {step} '{action}' timed out.", ex);
		}
		catch (LiveBotFailureException)
		{
			throw;
		}
		catch (Exception ex)
		{
			await problems.WriteAsync(options.Run, bot, account, step, "step-failure", ex.Message, ex);
			throw;
		}
	}

	private static string CharacterName(int index)
	{
		var suffix = new string([(char)('a' + ((index - 1) / 26)), (char)('a' + ((index - 1) % 26))]);
		return "Aelive" + suffix;
	}

	private static async Task WriteRunMetadataAsync(LiveBotOptions options, CancellationToken cancellationToken)
	{
		var metadata = new
		{
			run = options.Run,
			gitSha = options.GitSha,
			seed = options.Seed,
			virtualEpoch = (string?)null,
			timeZone = options.TimeZone,
			configProfile = options.Profile,
			scenarios = options.Scenarios,
			bots = options.BotCount,
			loginEndPoint = options.LoginEndPoint.ToString(),
			gameEndPoint = options.GameEndPoint.ToString(),
			chatEndPoint = options.ChatEndPoint.ToString(),
			adminBaseUri = options.AdminBaseUri.ToString(),
			reentrySeconds = options.ReentryDelay.TotalSeconds,
		};
		await using var output = File.Create(Path.Combine(options.OutputDirectory, "bots-run.json"));
		await JsonSerializer.SerializeAsync(output, metadata, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
	}

	private sealed class L0Actor : IAsyncDisposable
	{
		private readonly LiveBotOptions options;
		private readonly LiveBotProblemWriter problems;
		private int stepNumber;

		public L0Actor(LiveBotOptions options, LiveBotProblemWriter problems, int index)
		{
			this.options = options;
			this.problems = problems;
			Bot = $"b{index:D2}";
			Account = $"{Bot}r{DateTimeOffset.Now:MMdd}";
			Trace = BotActionTraceWriter.Open(Path.Combine(options.OutputDirectory, "bots", $"{Bot}.trace.jsonl"),
				options.Run, Bot, Account);
			Session = new LiveBotSession(options, problems, Trace, Bot, Account, CharacterName(index));
		}

		public string Bot { get; }
		public string Account { get; }
		public BotActionTraceWriter Trace { get; }
		public LiveBotSession Session { get; }
		public string LastStep => $"s{stepNumber:D2}";

		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
		{
			var step = $"s{++stepNumber:D2}";
			Session.BeginStep(step);
			return RunStepAsync(options, problems, Trace, Bot, Account, step, action, cancellationToken, operation);
		}

		public async ValueTask DisposeAsync()
		{
			await Session.DisposeAsync();
			Trace.Dispose();
		}
	}
}

internal sealed class LiveBotSession : IAsyncDisposable
{
	private const string Password = "aion-bots";
	private const string RegionChannel = "@\u0001public_ALL\u00011.0.AION.KOR";
	private readonly LiveBotOptions options;
	private readonly LiveBotProblemWriter problems;
	private readonly BotActionTraceWriter trace;
	private readonly string bot;
	private readonly string account;
	private readonly string characterName;
	private readonly string macAddress;
	private readonly byte[] macBytes;
	private readonly SemaphoreSlim sendLock = new(1, 1);
	private readonly BotApi api = new();
	private TcpBotTransport? transport;
	private IAsyncEnumerator<DecodedBotServerPacket>? packets;
	private Task<bool>? activeMoveNext;
	private CancellationTokenSource? connectionLifetime;
	private Task? pingTask;
	private TcpClient? chatClient;
	private NetworkStream? chatStream;
	private ChatClientProtocol? chatProtocol;
	private string currentStep = "startup";
	private AionConnection.State state = AionConnection.State.CONNECTED;
	private bool quitExpected;
	private int accountId;
	private int loginOk;
	private int playOk1;
	private int playOk2;
	private int characterId;
	private int chatChannelId;
	private PersistedPosition? expectedPosition;

	public LiveBotSession(LiveBotOptions options, LiveBotProblemWriter problems, BotActionTraceWriter trace,
		string bot, string account, string characterName)
	{
		this.options = options;
		this.problems = problems;
		this.trace = trace;
		this.bot = bot;
		this.account = account;
		this.characterName = characterName;
		var botNumber = byte.Parse(bot.AsSpan(1), System.Globalization.CultureInfo.InvariantCulture);
		macAddress = $"02-00-00-00-00-{botNumber:X2}";
		macBytes = [0x02, 0x00, 0x00, 0x00, 0x00, botNumber];
	}

	public void BeginStep(string step) => currentStep = step;

	public async Task ConnectAndReadKeyAsync(CancellationToken cancellationToken)
	{
		await OpenConnectionAsync(cancellationToken);
		AssertPacketType(await ReadNextAsync(cancellationToken), typeof(SM_KEY));
	}

	public async Task LoginAndAuthenticateAsync(CancellationToken cancellationToken)
	{
		await LoginServerAsync(cancellationToken);
		await OpenConnectionAsync(cancellationToken);
		AssertPacketType(await ReadNextAsync(cancellationToken), typeof(SM_KEY));
		await SendGameAsync(GameClientPackets.VersionCheck(207, 0, 65001, 10, 0, 2), cancellationToken);
		var version = await WaitForGamePacketAsync(typeof(SM_VERSION_CHECK), cancellationToken);
		if (version.Get<byte>("answerId") != 0)
			throw new InvalidDataException("Game server rejected client version 207.");
		await SendGameAsync(GameClientPackets.L2AuthLoginCheck(playOk2, playOk1, accountId, loginOk), cancellationToken);
		await SendGameAsync(GameClientPackets.MacAddress(macAddress, $"E2E-{bot.ToUpperInvariant()}"), cancellationToken);
		var auth = await WaitForGamePacketAsync(typeof(SM_L2AUTH_LOGIN_CHECK), cancellationToken);
		if (!auth.Get<bool>("ok"))
			throw new InvalidDataException("Game-server authentication failed.");
		state = AionConnection.State.AUTHED;
		await SendGameAsync(api.ListCharacters(playOk2), cancellationToken);
		var list = await WaitForGamePacketAsync(typeof(SM_CHARACTER_LIST), cancellationToken);
		if (list.Get<byte>("characterCount") != 0)
			throw new InvalidDataException($"Fresh L0 account {account} unexpectedly already has a character.");
	}

	public async Task CreateCharacterAsync(CancellationToken cancellationToken)
	{
		var creation = new CharacterCreationData
		{
			AccountId = accountId,
			AccountName = account,
			CharacterName = characterName,
			Gender = 0,
			Race = (int)Race.ELYOS,
			PlayerClass = (int)PlayerClass.WARRIOR,
			Height = 1,
		};
		await SendGameAsync(api.CreateCharacter(creation), cancellationToken);
		var response = await WaitForGamePacketAsync(typeof(SM_CREATE_CHARACTER), cancellationToken);
		if (response.Get<int>("responseCode") != 0)
			throw new InvalidDataException($"Character creation failed with response {response.Get<int>("responseCode")}.");
		var character = response.Get<IReadOnlyDictionary<string, object?>>("character");
		characterId = Get<int>(character, "objectId");
		if (!string.Equals(Get<string>(character, "name"), characterName, StringComparison.Ordinal))
			throw new InvalidDataException("SM_CREATE_CHARACTER returned a different character name.");
	}

	public async Task EnterWorldAsync(CancellationToken cancellationToken)
	{
		if (characterId == 0)
			throw new InvalidOperationException("Create the character before entering the world.");
		await SendGameAsync(api.EnterWorld(characterId), cancellationToken);
		state = AionConnection.State.IN_GAME;
		var spawn = await WaitForGamePacketAsync(typeof(SM_PLAYER_SPAWN), cancellationToken);
		expectedPosition = new PersistedPosition(spawn.Get<int>("worldId"), spawn.Get<float>("x"),
			spawn.Get<float>("y"), spawn.Get<float>("z"));
		await WaitForGamePacketAsync(typeof(SM_PLAYER_INFO), cancellationToken);
	}

	public async Task ConnectChatAsync(CancellationToken cancellationToken)
	{
		await SendGameAsync(GameClientPackets.ChatAuth(characterId, macBytes), cancellationToken);
		var chatInit = await WaitForGamePacketAsync(typeof(SM_CHAT_INIT), cancellationToken);
		chatProtocol = ChatClientProtocol.FromChatInit(chatInit);
		chatClient = new TcpClient(options.ChatEndPoint.AddressFamily) { NoDelay = true };
		await chatClient.ConnectAsync(options.ChatEndPoint.Address, options.ChatEndPoint.Port, cancellationToken);
		chatStream = chatClient.GetStream();
		await WriteChatAsync(chatProtocol.CreateChatInitFrame(), "CM_CHAT_INI", cancellationToken);
		RequireChatOpcode(await ReadChatPayloadAsync(cancellationToken), 0x31, "SM_CHAT_INI");
		await WriteChatAsync(chatProtocol.CreatePlayerAuthFrame(characterId, account, characterName, RegionChannel),
			"CM_PLAYER_AUTH", cancellationToken);
		RequireChatOpcode(await ReadChatPayloadAsync(cancellationToken), 0x02, "SM_PLAYER_AUTH_RESPONSE");
		await WriteChatAsync(chatProtocol.CreateChannelRequestFrame(1, RegionChannel), "CM_CHANNEL_REQUEST", cancellationToken);
		var channelResponse = await ReadChatPayloadAsync(cancellationToken);
		RequireChatOpcode(channelResponse, 0x11, "SM_CHANNEL_RESPONSE");
		if (channelResponse.Length < 12 || BinaryPrimitives.ReadInt32LittleEndian(channelResponse.AsSpan(2, 4)) != 1)
			throw new InvalidDataException("Chat channel response did not match request 1.");
		chatChannelId = BinaryPrimitives.ReadInt32LittleEndian(channelResponse.AsSpan(8, 4));
		if (chatChannelId == 0)
			throw new InvalidDataException("Chat server returned channel id 0.");
	}

	public Task SendChatMessageAsync(string message, CancellationToken cancellationToken)
	{
		if (chatProtocol == null || chatChannelId == 0)
			throw new InvalidOperationException("Join the region channel before sending a message.");
		return WriteChatAsync(chatProtocol.CreateChannelMessageFrame(chatChannelId, message), "CM_CHANNEL_MESSAGE", cancellationToken);
	}

	public async Task ReceiveChatMessageAsync(string expected, CancellationToken cancellationToken)
	{
		while (true)
		{
			var payload = await ReadChatPayloadAsync(cancellationToken);
			if (payload[0] != 0x1A)
				continue;
			var actual = ExtractChannelMessageText(payload);
			trace.WriteAction(currentStep, "SM_CHANNEL_MESSAGE", new Dictionary<string, object?> { ["message"] = actual });
			if (!string.Equals(actual, expected, StringComparison.Ordinal))
				throw new InvalidDataException($"Expected channel message '{expected}', received '{actual}'.");
			return;
		}
	}

	public async Task WalkTenMetersAsync(CancellationToken cancellationToken)
	{
		var start = expectedPosition ?? throw new InvalidOperationException("Enter the world before moving.");
		var target = start with { X = start.X + 10f };
		await SendGameAsync(api.MoveTo(new MovementPacketData(start.X, start.Y, start.Z, 0,
			MovementMask.POSITION | MovementMask.MANUAL | MovementMask.ABSOLUTE,
			X2: target.X, Y2: target.Y, Z2: target.Z)), cancellationToken);
		await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
		await SendGameAsync(api.MoveTo(new MovementPacketData(target.X, target.Y, target.Z, 0, MovementMask.IMMEDIATE)), cancellationToken);
		// Java CM_MOVE broadcasts SM_MOVE to sighted players, excluding the sender. Persistence is asserted after
		// CM_QUIT and again from SM_CHARACTER_LIST on relogin, so those are the authoritative L0 movement oracles.
		expectedPosition = target;
	}

	public async Task PingAsync(CancellationToken cancellationToken)
	{
		await SendGameAsync(GameClientPackets.Ping(), cancellationToken);
		await WaitForGamePacketAsync(typeof(SM_PONG), cancellationToken);
	}

	public async Task QuitAsync(CancellationToken cancellationToken)
	{
		quitExpected = true;
		await CloseChatAsync();
		await SendGameAsync(api.Quit(stayConnected: false), cancellationToken);
		await WaitForGamePacketAsync(typeof(SM_QUIT_RESPONSE), cancellationToken);
		await CloseConnectionAsync(cancellationToken);
		quitExpected = false;
	}

	public async Task VerifyOfflineAsync(CancellationToken cancellationToken)
	{
		using var client = new HttpClient { BaseAddress = options.AdminBaseUri };
		using var request = new HttpRequestMessage(HttpMethod.Get,
			$"admin/player-state?characterName={Uri.EscapeDataString(characterName)}");
		request.Headers.Add("X-Admin-Token", options.AdminToken);
		using var response = await client.SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();
		await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
		using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
		var root = document.RootElement;
		if (root.GetProperty("online").GetBoolean())
			throw new InvalidDataException($"Admin player-state still reports {characterName} online after CM_QUIT.");
		var lastKnown = root.GetProperty("lastKnown");
		AssertPersistedPosition(lastKnown.GetProperty("worldId").GetInt32(), lastKnown.GetProperty("x").GetSingle(),
			lastKnown.GetProperty("y").GetSingle(), lastKnown.GetProperty("z").GetSingle());
	}

	public async Task WaitForReentryAsync(CancellationToken cancellationToken)
	{
		var remaining = api.Timing.TimeUntilEnterWorld();
		if (remaining > TimeSpan.Zero)
			await Task.Delay(remaining + TimeSpan.FromMilliseconds(250), cancellationToken);
	}

	public async Task ReloginAndVerifyPersistenceAsync(CancellationToken cancellationToken)
	{
		await LoginServerAsync(cancellationToken);
		await OpenConnectionAsync(cancellationToken);
		AssertPacketType(await ReadNextAsync(cancellationToken), typeof(SM_KEY));
		await SendGameAsync(GameClientPackets.VersionCheck(207, 0, 65001, 10, 0, 2), cancellationToken);
		await WaitForGamePacketAsync(typeof(SM_VERSION_CHECK), cancellationToken);
		await SendGameAsync(GameClientPackets.L2AuthLoginCheck(playOk2, playOk1, accountId, loginOk), cancellationToken);
		await SendGameAsync(GameClientPackets.MacAddress(macAddress, $"E2E-{bot.ToUpperInvariant()}"), cancellationToken);
		var auth = await WaitForGamePacketAsync(typeof(SM_L2AUTH_LOGIN_CHECK), cancellationToken);
		if (!auth.Get<bool>("ok"))
			throw new InvalidDataException("Game-server reauthentication failed.");
		state = AionConnection.State.AUTHED;
		await SendGameAsync(api.ListCharacters(playOk2), cancellationToken);
		var list = await WaitForGamePacketAsync(typeof(SM_CHARACTER_LIST), cancellationToken);
		var characters = list.Get<List<IReadOnlyDictionary<string, object?>>>("characters");
		var character = characters.SingleOrDefault(entry => string.Equals(Get<string>(entry, "name"), characterName, StringComparison.Ordinal))
			?? throw new InvalidDataException($"Character list did not contain {characterName} after relogin.");
		if (Get<int>(character, "objectId") != characterId)
			throw new InvalidDataException("Character object id changed after relogin.");
		AssertPersistedPosition(Get<int>(character, "mapId"), Get<float>(character, "x"),
			Get<float>(character, "y"), Get<float>(character, "z"));
	}

	public Task CloseAsync(CancellationToken cancellationToken)
	{
		quitExpected = true;
		return CloseConnectionAsync(cancellationToken);
	}

	public async ValueTask DisposeAsync()
	{
		quitExpected = true;
		await CloseChatAsync();
		await CloseConnectionAsync(CancellationToken.None);
		sendLock.Dispose();
	}

	private async Task LoginServerAsync(CancellationToken cancellationToken)
	{
		using var client = new TcpClient(options.LoginEndPoint.AddressFamily) { NoDelay = true };
		await client.ConnectAsync(options.LoginEndPoint.Address, options.LoginEndPoint.Port, cancellationToken);
		var stream = client.GetStream();
		var login = await api.Login(stream, account, Password, cancellationToken);
		accountId = login.Result.AccountId;
		loginOk = login.Result.LoginOk;
		await stream.WriteAsync(login.Protocol.Crypto.CreateServerListFrame(accountId, loginOk), cancellationToken);
		var serverList = login.Protocol.Crypto.DecryptServerFrame(await LoginClientProtocol.ReadFrameAsync(stream, cancellationToken));
		if (serverList.Length < 19 || serverList[0] != 0x04 || serverList[1] == 0 || serverList[3] != 1 || serverList[18] != 1)
			throw new InvalidDataException("Login server did not advertise online game server 1.");
		await stream.WriteAsync(login.Protocol.Crypto.CreatePlayFrame(accountId, loginOk, 1), cancellationToken);
		var play = login.Protocol.Crypto.DecryptServerFrame(await LoginClientProtocol.ReadFrameAsync(stream, cancellationToken));
		if (play.Length < 10 || play[0] != 0x07 || play[9] != 1)
			throw new InvalidDataException("Login server did not return SM_PLAY_OK for game server 1.");
		playOk1 = BinaryPrimitives.ReadInt32LittleEndian(play.AsSpan(1, 4));
		playOk2 = BinaryPrimitives.ReadInt32LittleEndian(play.AsSpan(5, 4));
	}

	private async Task SendGameAsync(BotClientPacket packet, CancellationToken cancellationToken)
	{
		var activeTransport = transport ?? throw new InvalidOperationException("The game connection is not open.");
		await sendLock.WaitAsync(cancellationToken);
		try
		{
			trace.WriteSent(currentStep, packet);
			await activeTransport.SendAsync(activeTransport.Codec.EncodeClientFrame(packet, state), cancellationToken);
		}
		finally
		{
			sendLock.Release();
		}
	}

	private async Task<DecodedBotServerPacket> WaitForGamePacketAsync(Type packetType, CancellationToken cancellationToken,
		Func<DecodedBotServerPacket, bool>? predicate = null)
	{
		while (true)
		{
			var packet = await ReadNextAsync(cancellationToken);
			var response = api.Observe(packet);
			if (response != null)
				await SendGameAsync(response, cancellationToken);
			if (packet.PacketType == packetType && (predicate == null || predicate(packet)))
				return packet;
		}
	}

	private async Task<DecodedBotServerPacket> ReadNextAsync(CancellationToken cancellationToken)
	{
		try
		{
			if (packets == null)
				throw new EndOfStreamException("Game transport ended before the expected packet.");
			var moveNext = packets.MoveNextAsync().AsTask();
			activeMoveNext = moveNext;
			if (!await moveNext.WaitAsync(cancellationToken))
				throw new EndOfStreamException("Game transport ended before the expected packet.");
			activeMoveNext = null;
			var packet = packets.Current;
			trace.WriteReceived(currentStep, packet);
			if (packet.PacketType == typeof(SM_ENTER_WORLD_CHECK) && packet.Get<byte>("msg") != 0)
				throw new LiveBotFailureException($"SM_ENTER_WORLD_CHECK refused entry with message {packet.Get<byte>("msg")}.");
			if (packet.PacketType == typeof(SM_QUIT_RESPONSE) && !quitExpected)
			{
				await problems.WriteAsync(options.Run, bot, account, currentStep, "unexpected-quit-response",
					"Received SM_QUIT_RESPONSE before the scenario requested quit.");
				throw new LiveBotFailureException("Unexpected SM_QUIT_RESPONSE.");
			}
			return packet;
		}
		catch (Exception ex) when (ex is EndOfStreamException or IOException or SocketException)
		{
			if (quitExpected)
				throw;
			await AttemptReconnectAsync(ex, cancellationToken);
			throw new LiveBotFailureException("Game connection ended unexpectedly.", ex);
		}
	}

	private async Task AttemptReconnectAsync(Exception failure, CancellationToken cancellationToken)
	{
		await problems.WriteAsync(options.Run, bot, account, currentStep, "unexpected-disconnect", failure.Message, failure);
		try
		{
			await CloseConnectionAsync(CancellationToken.None);
			await OpenConnectionAsync(cancellationToken);
			await problems.WriteAsync(options.Run, bot, account, currentStep, "automatic-reconnect",
				"Automatically reconnected after an unexpected disconnect; the run still fails.");
		}
		catch (Exception reconnectFailure)
		{
			await problems.WriteAsync(options.Run, bot, account, currentStep, "automatic-reconnect",
				"Automatic reconnect failed; the run still fails.", reconnectFailure);
		}
	}

	private async Task OpenConnectionAsync(CancellationToken cancellationToken)
	{
		if (transport != null)
			throw new InvalidOperationException("The bot already has an open game connection.");
		using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		connectTimeout.CancelAfter(options.ConnectTimeout);
		try
		{
			transport = await TcpBotTransport.ConnectAsync(options.GameEndPoint, connectTimeout.Token);
		}
		catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && connectTimeout.IsCancellationRequested)
		{
			throw new TimeoutException($"Connecting to {options.GameEndPoint} exceeded {options.ConnectTimeout.TotalSeconds:n0} seconds.", ex);
		}
		connectionLifetime = new CancellationTokenSource();
		packets = transport.ReceiveAsync(connectionLifetime.Token).GetAsyncEnumerator(connectionLifetime.Token);
		pingTask = RunPingLoopAsync(connectionLifetime.Token);
	}

	private async Task CloseConnectionAsync(CancellationToken cancellationToken)
	{
		if (transport == null && connectionLifetime == null)
			return;
		if (transport != null)
			await transport.CloseAsync(cancellationToken);
		if (connectionLifetime != null)
			await connectionLifetime.CancelAsync();
		if (activeMoveNext != null)
		{
			try { await activeMoveNext; }
			catch (OperationCanceledException) when (connectionLifetime?.IsCancellationRequested == true) { }
			catch (ObjectDisposedException) when (connectionLifetime?.IsCancellationRequested == true) { }
			activeMoveNext = null;
		}
		if (pingTask != null)
		{
			try { await pingTask; }
			catch (OperationCanceledException) when (connectionLifetime?.IsCancellationRequested == true) { }
		}
		if (packets != null)
			await packets.DisposeAsync();
		if (transport != null)
			await transport.DisposeAsync();
		connectionLifetime?.Dispose();
		connectionLifetime = null;
		packets = null;
		activeMoveNext = null;
		pingTask = null;
		transport = null;
		state = AionConnection.State.CONNECTED;
	}

	private async Task RunPingLoopAsync(CancellationToken cancellationToken)
	{
		var scheduler = new LiveBotPingScheduler();
		while (!cancellationToken.IsCancellationRequested)
		{
			var delay = scheduler.NextDueAt - DateTimeOffset.UtcNow;
			if (delay > TimeSpan.Zero)
				await Task.Delay(delay, cancellationToken);
			var ping = scheduler.Poll();
			if (ping != null && state == AionConnection.State.IN_GAME && transport != null)
				await SendGameAsync(ping, cancellationToken);
		}
	}

	private async Task WriteChatAsync(byte[] frame, string packet, CancellationToken cancellationToken)
	{
		var stream = chatStream ?? throw new InvalidOperationException("The chat connection is not open.");
		trace.WriteAction(currentStep, packet, new Dictionary<string, object?> { ["frameLength"] = frame.Length });
		await stream.WriteAsync(frame, cancellationToken);
		await stream.FlushAsync(cancellationToken);
	}

	private async Task<byte[]> ReadChatPayloadAsync(CancellationToken cancellationToken)
	{
		var stream = chatStream ?? throw new InvalidOperationException("The chat connection is not open.");
		var header = await ReadExactAsync(stream, sizeof(ushort), cancellationToken);
		var length = BinaryPrimitives.ReadUInt16LittleEndian(header);
		if (length < 3)
			throw new InvalidDataException($"Invalid chat frame length {length}.");
		var payload = await ReadExactAsync(stream, length - sizeof(ushort), cancellationToken);
		trace.WriteAction(currentStep, $"chat:0x{payload[0]:X2}",
			new Dictionary<string, object?> { ["payloadHex"] = Convert.ToHexString(payload) });
		return payload;
	}

	private Task CloseChatAsync()
	{
		if (chatClient == null)
			return Task.CompletedTask;
		try { chatClient.Client.Shutdown(SocketShutdown.Both); }
		catch (SocketException) { }
		chatClient.Dispose();
		chatClient = null;
		chatStream = null;
		chatProtocol = null;
		chatChannelId = 0;
		return Task.CompletedTask;
	}

	private static async Task<byte[]> ReadExactAsync(Stream stream, int length, CancellationToken cancellationToken)
	{
		var buffer = new byte[length];
		var offset = 0;
		while (offset < length)
		{
			var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
			if (read == 0)
				throw new EndOfStreamException("Socket closed in the middle of a frame.");
			offset += read;
		}
		return buffer;
	}

	private void AssertPersistedPosition(int worldId, float x, float y, float z)
	{
		var expected = expectedPosition ?? throw new InvalidOperationException("No expected persisted position was recorded.");
		if (worldId != expected.WorldId || Math.Abs(x - expected.X) > 0.1f || Math.Abs(y - expected.Y) > 0.1f || Math.Abs(z - expected.Z) > 0.1f)
			throw new InvalidDataException($"Persisted position mismatch: expected {expected.WorldId} ({expected.X:n2},{expected.Y:n2},{expected.Z:n2}), received {worldId} ({x:n2},{y:n2},{z:n2}).");
	}

	private static void RequireChatOpcode(byte[] payload, byte opcode, string packet)
	{
		if (payload.Length == 0 || payload[0] != opcode)
			throw new InvalidDataException($"Expected {packet} opcode 0x{opcode:X2}, received {(payload.Length == 0 ? "empty payload" : $"0x{payload[0]:X2}")}.");
	}

	private static string ExtractChannelMessageText(byte[] payload)
	{
		var offset = 1 + 1 + (5 * sizeof(int)) + 1;
		if (payload.Length < offset + sizeof(ushort))
			throw new InvalidDataException("SM_CHANNEL_MESSAGE is truncated before its sender identifier.");
		var identifierChars = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset, 2));
		offset += sizeof(ushort) + (identifierChars * sizeof(char));
		if (payload.Length < offset + sizeof(ushort))
			throw new InvalidDataException("SM_CHANNEL_MESSAGE is truncated before its text.");
		var textChars = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset, 2));
		offset += sizeof(ushort);
		if (payload.Length < offset + (textChars * sizeof(char)))
			throw new InvalidDataException("SM_CHANNEL_MESSAGE text is truncated.");
		return Encoding.Unicode.GetString(payload.AsSpan(offset, textChars * sizeof(char)));
	}

	private static void AssertPacketType(DecodedBotServerPacket packet, Type expected)
	{
		if (packet.PacketType != expected)
			throw new InvalidDataException($"Expected {expected.Name}, received {packet.PacketType.Name}.");
	}

	private static T Get<T>(IReadOnlyDictionary<string, object?> fields, string name) =>
		fields.TryGetValue(name, out var value) && value is T typed
			? typed
			: throw new InvalidDataException($"Decoded field '{name}' was missing or was not {typeof(T).Name}.");

	private sealed record PersistedPosition(int WorldId, float X, float Y, float Z);
}

internal sealed class LiveBotFailureException : Exception
{
	public LiveBotFailureException(string message, Exception? innerException = null) : base(message, innerException) { }
}
