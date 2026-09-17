using System.Diagnostics;
using System.Text.Json;
using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.Timing;
using Aion.Bots.Transport;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Dao;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.Simulation.Tests;

[Collection(SimulationWorldCollection.Name)]
public sealed class SimulationFastScenarioTests(SimulationWorldFixture fixture)
{
	private readonly string allowlistPath = Path.Combine(
		Aion.GameServer.TestKit.RealStaticData.RepoRoot(), "parity-artifacts", "e2e", "log-allowlist.json");

	[SkippableFact]
	public async Task ManifestScenariosRunInFixedProcessOrder()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		ScenarioTier tier = ReadTier();
		int shardCount = ReadPositiveInt("AION_SIM_SHARD_COUNT", 1);
		string processKey = Environment.GetEnvironmentVariable("AION_SIM_PROCESS_KEY") ?? "shard-00";
		ScenarioManifest manifest = ScenarioManifest.Load(ScenarioManifest.FindDefaultPath());
		IReadOnlyList<ScenarioExecution> plan = ScenarioIsolationPlanner.Plan(
			manifest.Scenarios,
			ScenarioMode.Sim,
			tier,
			shardCount,
			_ => TimeSpan.Zero);
		ScenarioExecution[] processPlan = plan
			.Where(execution => string.Equals(execution.ProcessKey, processKey, StringComparison.Ordinal))
			.ToArray();

		Assert.NotEmpty(processPlan);
		Assert.Equal(processPlan.OrderBy(execution => execution.Order), processPlan);
		var driver = new SimulationDriver(fixture.Clock);
		bool includeHistory = true;
		foreach (ScenarioExecution execution in processPlan)
		{
			await driver.PrepareScenarioAsync(execution);
			switch (execution.Scenario.Id)
			{
				case "S0":
					await RunS0Async(execution.Scenario, includeHistory);
					break;
				case "L0":
					await RunL0Async(execution, includeHistory);
					break;
				default:
					throw new InvalidOperationException($"SIM scenario '{execution.Scenario.Id}' has no runner.");
			}
			includeHistory = false;
		}
	}

	private async Task RunS0Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		Assert.True(fixture.Bootstrap.IsStarted);
		Assert.True(fixture.World.IsInitialized);
		Assert.True(fixture.World.ObjectCount > 0);
		Assert.NotNull(fixture.World.GetWorldMap(scenario.Map!.Value).GetMainWorldMapInstance().GetNpc(210119));

		IReadOnlyList<string> first = await RunS0ProbeAsync(scenario.VirtualDuration);
		IReadOnlyList<string> second = await RunS0ProbeAsync(scenario.VirtualDuration);

		Assert.Equal(first, second);
		Assert.Equal([nameof(SM_KEY)], first);
		policy.AssertClean();
	}

	private async Task<IReadOnlyList<string>> RunS0ProbeAsync(TimeSpan duration)
	{
		Rnd.SetProcessSeed(fixture.Seed);
		await using var transport = new InProcessBotTransport(elapsed => fixture.Clock.Advance(elapsed));
		await using var packets = transport.ReceiveAsync().GetAsyncEnumerator();
		Assert.True(await packets.MoveNextAsync());
		var stream = new List<string> { packets.Current.PacketType.Name };
		await transport.AdvanceAsync(duration);
		return stream;
	}

	private async Task RunL0Async(ScenarioExecution execution, bool includeHistory)
	{
		using var policy = NewPolicy(execution.Scenario.Id, includeHistory);
		var actors = Enumerable.Range(1, execution.Scenario.Bots)
			.Select(index => new SimulationL0Actor(fixture, policy, index))
			.ToArray();
		long started = Stopwatch.GetTimestamp();
		try
		{
			await L0Scenario.RunAsync(actors, execution.Channel, includeChat: false);
			WriteL0PacketArtifact(actors);
			TimeSpan elapsed = Stopwatch.GetElapsedTime(started);
			Assert.True(elapsed < SimulationDriver.DefaultWallTimeBudget,
				$"L0 scenario body took {elapsed.TotalSeconds:F3}s; budget is {SimulationDriver.DefaultWallTimeBudget.TotalSeconds:F0}s.");
			policy.AssertClean();
		}
		finally
		{
			foreach (SimulationL0Actor actor in actors)
				await actor.DisposeAsync();
		}
	}

	private static void WriteL0PacketArtifact(IReadOnlyList<SimulationL0Actor> actors)
	{
		string? runDirectory = Environment.GetEnvironmentVariable("AION_E2E_RUN_DIR");
		if (string.IsNullOrWhiteSpace(runDirectory))
			return;
		string path = Path.Combine(Path.GetFullPath(runDirectory), "l0-packets.json");
		var artifact = actors.Select(actor => new
		{
			bot = actor.Bot,
			characterId = actor.Session.CharacterId,
			packets = actor.Session.PacketObservations,
		});
		File.WriteAllText(path, JsonSerializer.Serialize(artifact, new JsonSerializerOptions { WriteIndented = true }) + "\n");
	}

	private SimulationLogPolicy NewPolicy(string scenario, bool includeHistory) => new(
		"sim-fast",
		scenario,
		fixture.Clock,
		allowlistPath,
		captureProvider: fixture.LogCapture,
		loggerFactory: fixture.LoggerFactory,
		includeHistory: includeHistory);

	private static ScenarioTier ReadTier() =>
		Enum.TryParse(Environment.GetEnvironmentVariable("AION_SIM_TIER") ?? nameof(ScenarioTier.Fast),
			ignoreCase: true,
			out ScenarioTier tier) && tier is ScenarioTier.Fast or ScenarioTier.Full
			? tier
			: throw new InvalidOperationException("AION_SIM_TIER must be Fast or Full.");

	private static int ReadPositiveInt(string name, int defaultValue) =>
		int.TryParse(Environment.GetEnvironmentVariable(name) ?? defaultValue.ToString(), out int value) && value > 0
			? value
			: throw new InvalidOperationException($"{name} must be a positive integer.");

	private sealed class SimulationL0Actor : IL0ScenarioActor, IAsyncDisposable
	{
		private readonly SimulationLogPolicy policy;
		private int stepNumber;

		public SimulationL0Actor(SimulationWorldFixture fixture, SimulationLogPolicy policy, int index)
		{
			this.policy = policy;
			Bot = $"b{index:D2}";
			Session = new SimulationL0Session(fixture, policy, Bot, index, $"Aesim{(char)('a' + index - 1)}a");
		}

		public string Bot { get; }
		public SimulationL0Session Session { get; }
		IL0ScenarioSession IL0ScenarioActor.Session => Session;

		public async Task StepAsync(
			string action,
			Func<IL0ScenarioSession, CancellationToken, Task> operation,
			CancellationToken cancellationToken)
		{
			string step = $"s{++stepNumber:D2}";
			Session.BeginStep(step, action);
			using (policy.BeginBotStep(Bot, step))
				await operation(Session, cancellationToken);
		}

		public ValueTask DisposeAsync() => Session.DisposeAsync();
	}

	private sealed class SimulationL0Session : IL0ScenarioSession, IAsyncDisposable
	{
		private readonly SimulationWorldFixture fixture;
		private readonly SimulationLogPolicy policy;
		private readonly string bot;
		private readonly int accountId;
		private readonly string accountName;
		private readonly string characterName;
		private readonly string macAddress;
		private readonly BotApi api;
		private InProcessBotTransport? transport;
		private IAsyncEnumerator<DecodedBotServerPacket>? packets;
		private AionConnection.State state = AionConnection.State.CONNECTED;
		private string currentStep = "startup";
		private string currentAction = "startup";
		private int characterId;
		private PersistedPosition? expectedPosition;

		public SimulationL0Session(
			SimulationWorldFixture fixture,
			SimulationLogPolicy policy,
			string bot,
			int accountId,
			string characterName)
		{
			this.fixture = fixture;
			this.policy = policy;
			this.bot = bot;
			this.accountId = accountId;
			accountName = $"sim-player-{accountId}";
			this.characterName = characterName;
			macAddress = $"02-00-00-00-00-{accountId:X2}";
			api = new BotApi(timing: new BotTimingContract(new SimulationTimeProvider()));
		}

		public List<string> PacketTypes { get; } = [];
		public List<SimulationPacketObservation> PacketObservations { get; } = [];
		public int CharacterId => characterId;

		public void BeginStep(string step, string action)
		{
			currentStep = step;
			currentAction = action;
		}

		public async Task LoginAndAuthenticateAsync(CancellationToken cancellationToken)
		{
			await OpenAsync(cancellationToken);
			RequirePacket(await ReadNextAsync(cancellationToken), typeof(SM_KEY));
			await SendAsync(GameClientPackets.VersionCheck(207, 0, 65001, 10, 0, 2), cancellationToken);
			DecodedBotServerPacket version = await WaitForAsync(typeof(SM_VERSION_CHECK), cancellationToken);
			if (version.Get<byte>("answerId") != 0)
				throw new InvalidDataException("Game server rejected client version 207.");

			await SendAsync(GameClientPackets.L2AuthLoginCheck(2000 + accountId, 1000 + accountId, accountId, 3000 + accountId), cancellationToken);
			await SendAsync(GameClientPackets.MacAddress(macAddress, $"SIM-{bot.ToUpperInvariant()}"), cancellationToken);
			DecodedBotServerPacket auth = await WaitForAsync(typeof(SM_L2AUTH_LOGIN_CHECK), cancellationToken);
			if (!auth.Get<bool>("ok"))
				throw new InvalidDataException("Game-server simulation authentication failed.");
			state = AionConnection.State.AUTHED;
			await SendAsync(api.ListCharacters(2000 + accountId), cancellationToken);
			DecodedBotServerPacket list = await WaitForAsync(typeof(SM_CHARACTER_LIST), cancellationToken);
			if (list.Get<byte>("characterCount") != 0)
				throw new InvalidDataException($"Fresh simulation account {accountName} already has a character.");
		}

		public async Task CreateCharacterAsync(CancellationToken cancellationToken)
		{
			await SendAsync(api.CreateCharacter(new CharacterCreationData
			{
				AccountId = accountId,
				AccountName = accountName,
				CharacterName = characterName,
				Gender = 0,
				Race = (int)Race.ELYOS,
				PlayerClass = (int)PlayerClass.WARRIOR,
				Height = 1,
			}), cancellationToken);
			DecodedBotServerPacket response = await WaitForAsync(typeof(SM_CREATE_CHARACTER), cancellationToken);
			if (response.Get<int>("responseCode") != 0)
				throw new InvalidDataException($"Character creation failed with response {response.Get<int>("responseCode")}.");
			IReadOnlyDictionary<string, object?> character = response.Get<IReadOnlyDictionary<string, object?>>("character");
			characterId = Get<int>(character, "objectId");
			if (!string.Equals(Get<string>(character, "name"), characterName, StringComparison.Ordinal))
				throw new InvalidDataException("SM_CREATE_CHARACTER returned a different simulation character name.");
		}

		public async Task EnterWorldAsync(CancellationToken cancellationToken)
		{
			await SendAsync(api.EnterWorld(characterId), cancellationToken);
			state = AionConnection.State.IN_GAME;
			DecodedBotServerPacket spawn = await WaitForAsync(typeof(SM_PLAYER_SPAWN), cancellationToken);
			expectedPosition = Position(spawn);
			await WaitForAsync(typeof(SM_PLAYER_INFO), cancellationToken);
		}

		public async Task ChangeChannelAsync(int channel, CancellationToken cancellationToken)
		{
			await SendAsync(api.ChangeChannel(channel), cancellationToken);
			await WaitForAsync(typeof(SM_CHANNEL_INFO), cancellationToken);
			DecodedBotServerPacket spawn = await WaitForAsync(typeof(SM_PLAYER_SPAWN), cancellationToken);
			if (spawn.Get<int>("worldChannel") % 10 != channel)
				throw new InvalidDataException($"Expected channel {channel}, got world channel {spawn.Get<int>("worldChannel")}.");
			expectedPosition = Position(spawn);
		}

		public Task ConnectChatAsync(CancellationToken cancellationToken) =>
			throw new NotSupportedException("Chat steps are LIVE-only.");

		public Task SendChatMessageAsync(string message, CancellationToken cancellationToken) =>
			throw new NotSupportedException("Chat steps are LIVE-only.");

		public Task ReceiveChatMessageAsync(string expected, CancellationToken cancellationToken) =>
			throw new NotSupportedException("Chat steps are LIVE-only.");

		public async Task WalkTenMetersAsync(CancellationToken cancellationToken)
		{
			PersistedPosition start = expectedPosition ?? throw new InvalidOperationException("Enter the world before moving.");
			PersistedPosition target = start with { X = start.X + 10f };
			await SendAsync(api.MoveTo(new MovementPacketData(start.X, start.Y, start.Z, 0,
				MovementMask.POSITION | MovementMask.MANUAL | MovementMask.ABSOLUTE,
				X2: target.X, Y2: target.Y, Z2: target.Z)), cancellationToken);
			await transport!.AdvanceAsync(TimeSpan.FromSeconds(2), cancellationToken);
			await SendAsync(api.MoveTo(new MovementPacketData(target.X, target.Y, target.Z, 0, MovementMask.IMMEDIATE)), cancellationToken);
			expectedPosition = target;
		}

		public async Task PingAsync(CancellationToken cancellationToken)
		{
			await SendAsync(GameClientPackets.Ping(), cancellationToken);
			await WaitForAsync(typeof(SM_PONG), cancellationToken);
		}

		public async Task QuitAsync(CancellationToken cancellationToken)
		{
			await SendAsync(api.Quit(stayConnected: false), cancellationToken);
			await WaitForAsync(typeof(SM_QUIT_RESPONSE), cancellationToken);
			await CloseAsync(cancellationToken);
		}

		public Task VerifyOfflineAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (PlayerDAO.IsOnline(characterId))
				throw new InvalidDataException($"PlayerDAO still reports {characterName} online after CM_QUIT.");
			PlayerCommonData persisted = PlayerDAO.LoadPlayerCommonData(characterId)
				?? throw new InvalidDataException($"PlayerDAO could not reload {characterName}.");
			AssertPosition(persisted.GetMapId(), persisted.GetX(), persisted.GetY(), persisted.GetZ());
			return Task.CompletedTask;
		}

		public Task WaitForReentryAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			TimeSpan remaining = api.Timing.TimeUntilEnterWorld();
			if (remaining > TimeSpan.Zero)
				fixture.Clock.Advance(remaining + TimeSpan.FromMilliseconds(1));
			return Task.CompletedTask;
		}

		public async Task ReloginAndVerifyPersistenceAsync(CancellationToken cancellationToken)
		{
			await OpenAsync(cancellationToken);
			RequirePacket(await ReadNextAsync(cancellationToken), typeof(SM_KEY));
			await SendAsync(GameClientPackets.VersionCheck(207, 0, 65001, 10, 0, 2), cancellationToken);
			await WaitForAsync(typeof(SM_VERSION_CHECK), cancellationToken);
			await SendAsync(GameClientPackets.L2AuthLoginCheck(2000 + accountId, 1000 + accountId, accountId, 3000 + accountId), cancellationToken);
			await SendAsync(GameClientPackets.MacAddress(macAddress, $"SIM-{bot.ToUpperInvariant()}"), cancellationToken);
			DecodedBotServerPacket auth = await WaitForAsync(typeof(SM_L2AUTH_LOGIN_CHECK), cancellationToken);
			if (!auth.Get<bool>("ok"))
				throw new InvalidDataException("Game-server simulation reauthentication failed.");
			state = AionConnection.State.AUTHED;
			await SendAsync(api.ListCharacters(2000 + accountId), cancellationToken);
			DecodedBotServerPacket list = await WaitForAsync(typeof(SM_CHARACTER_LIST), cancellationToken);
			IReadOnlyDictionary<string, object?> character = list
				.Get<List<IReadOnlyDictionary<string, object?>>>("characters")
				.SingleOrDefault(entry => string.Equals(Get<string>(entry, "name"), characterName, StringComparison.Ordinal))
				?? throw new InvalidDataException($"Character list did not contain {characterName} after relogin.");
			if (Get<int>(character, "objectId") != characterId)
				throw new InvalidDataException("Character object id changed after relogin.");
			AssertPosition(Get<int>(character, "mapId"), Get<float>(character, "x"),
				Get<float>(character, "y"), Get<float>(character, "z"));
		}

		public async ValueTask DisposeAsync() => await CloseAsync(CancellationToken.None);

		private async Task OpenAsync(CancellationToken cancellationToken)
		{
			await CloseAsync(CancellationToken.None);
			transport = new InProcessBotTransport(elapsed => fixture.Clock.Advance(elapsed), ip: $"127.0.0.{accountId}");
			packets = transport.ReceiveAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
			state = AionConnection.State.CONNECTED;
		}

		private async Task CloseAsync(CancellationToken cancellationToken)
		{
			if (transport != null)
			{
				await transport.CloseAsync(cancellationToken);
				await transport.DisposeAsync();
				transport = null;
			}
			if (packets != null)
			{
				await packets.DisposeAsync();
				packets = null;
			}
		}

		private Task SendAsync(BotClientPacket packet, CancellationToken cancellationToken)
		{
			InProcessBotTransport active = transport ?? throw new InvalidOperationException("Simulation game connection is not open.");
			return active.SendAsync(packet.Encode(active.Codec, state), cancellationToken).AsTask();
		}

		private async Task<DecodedBotServerPacket> WaitForAsync(Type packetType, CancellationToken cancellationToken)
		{
			while (true)
			{
				DecodedBotServerPacket packet = await ReadNextAsync(cancellationToken);
				BotClientPacket? response = api.Observe(packet);
				if (response != null)
					await SendAsync(response, cancellationToken);
				if (packet.PacketType == typeof(SM_ENTER_WORLD_CHECK) && packet.Get<byte>("msg") != 0)
					throw new InvalidDataException($"SM_ENTER_WORLD_CHECK refused entry with message {packet.Get<byte>("msg")}.");
				if (packet.PacketType == packetType)
					return packet;
			}
		}

		private async Task<DecodedBotServerPacket> ReadNextAsync(CancellationToken cancellationToken)
		{
			IAsyncEnumerator<DecodedBotServerPacket> active = packets
				?? throw new EndOfStreamException("Simulation game transport ended before the expected packet.");
			if (!await active.MoveNextAsync().AsTask().WaitAsync(cancellationToken))
				throw new EndOfStreamException("Simulation game transport ended before the expected packet.");
			DecodedBotServerPacket packet = active.Current;
			PacketTypes.Add(packet.PacketType.Name);
			int? objectId = packet.Fields.TryGetValue("objectId", out object? value) && value is int id ? id : null;
			PacketObservations.Add(new SimulationPacketObservation(currentAction, packet.PacketType.Name, objectId));
			policy.ObservePacket(bot, currentStep, packet);
			return packet;
		}

		private void AssertPosition(int mapId, float x, float y, float z)
		{
			PersistedPosition expected = expectedPosition ?? throw new InvalidOperationException("No expected position was recorded.");
			if (mapId != expected.MapId || Math.Abs(x - expected.X) > 0.05f ||
				Math.Abs(y - expected.Y) > 0.05f || Math.Abs(z - expected.Z) > 0.05f)
				throw new InvalidDataException(
					$"Persisted position ({mapId}, {x:F3}, {y:F3}, {z:F3}) did not match " +
					$"expected ({expected.MapId}, {expected.X:F3}, {expected.Y:F3}, {expected.Z:F3}).");
		}

		private static PersistedPosition Position(DecodedBotServerPacket packet) => new(
			packet.Get<int>("worldId"), packet.Get<float>("x"), packet.Get<float>("y"), packet.Get<float>("z"));

		private static void RequirePacket(DecodedBotServerPacket packet, Type expected)
		{
			if (packet.PacketType != expected)
				throw new InvalidDataException($"Expected {expected.Name}, received {packet.PacketType.Name}.");
		}

		private static T Get<T>(IReadOnlyDictionary<string, object?> fields, string name) =>
			fields.TryGetValue(name, out object? value) && value is T typed
				? typed
				: throw new InvalidDataException($"Decoded field '{name}' was missing or was not {typeof(T).Name}.");

		private sealed class SimulationTimeProvider : TimeProvider
		{
			public override DateTimeOffset GetUtcNow() => SystemClock.UtcNow();
		}

		private sealed record PersistedPosition(int MapId, float X, float Y, float Z);

		public sealed record SimulationPacketObservation(string Action, string Packet, int? ObjectId);
	}
}
