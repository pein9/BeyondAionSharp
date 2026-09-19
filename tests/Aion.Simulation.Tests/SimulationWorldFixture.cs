using System.Diagnostics;
using System.Text.Json;
using Aion.Commons.Configuration;
using Aion.Commons.Database;
using Aion.Commons.Logging;
using Aion.GameServer.Configuration;
using Aion.GameServer.Configs;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Configs.Network;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Network.ChatServer;
using Aion.GameServer.Network.LoginServer;
using Aion.GameServer.Services;
using Aion.GameServer.TestKit;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GameWorld = Aion.GameServer.World.World;
using ChatServerFacade = Aion.GameServer.Network.ChatServer.ChatServer;
using LoginServerFacade = Aion.GameServer.Network.LoginServer.LoginServer;

namespace Aion.Simulation.Tests;

public sealed class SimulationWorldFixture : IAsyncLifetime
{
	private static readonly DateTimeOffset FixedEpoch = new(2026, 9, 16, 8, 59, 0, TimeSpan.Zero);
	// L6 exercises shipped reward periods without changing their dates or the host clock.
	public static DateTimeOffset EpochForProcess(string processKey) => processKey switch
	{
		"reset-L6" => new DateTimeOffset(2020, 12, 16, 8, 58, 0, TimeSpan.Zero),
		"reset-L8C" => new DateTimeOffset(2026, 12, 16, 12, 0, 0, TimeSpan.Zero),
		"reset-L8" => new DateTimeOffset(2026, 8, 9, 23, 50, 0, TimeSpan.Zero),
		_ => FixedEpoch,
	};
	public DateTimeOffset Epoch { get; private set; }
	private readonly string _repoRoot = RealStaticData.RepoRoot();
	private ServiceProvider? _services;
	private IDisposable? _configLoadScope;
	private string? _scratchDirectory;
	private SimDatabase? _database;
	private EventHandler? _processExitHandler;

	public bool IsAvailable { get; private set; }

	public string SkipReason { get; private set; } = "Set AION_SIM_DB_INTEGRATION=1 to run Docker-backed simulation tests.";

	public VirtualThreadPool Clock { get; private set; } = null!;

	public GameServerBootstrapService Bootstrap { get; private set; } = null!;

	public GameWorld World { get; private set; } = null!;

	public DataManager DataManager { get; private set; } = null!;

	public LoginServerFacade LoginServer { get; private set; } = null!;

	public SimulationLoginServerLink LoginLink { get; private set; } = null!;

	public CapturingLoggerProvider LogCapture { get; private set; } = null!;

	public ILoggerFactory LoggerFactory { get; private set; } = null!;

	public int Seed { get; private set; }

	public async Task InitializeAsync()
	{
		if (Environment.GetEnvironmentVariable("AION_SIM_DB_INTEGRATION") != "1")
			return;
		if (!await DockerIsAvailableAsync())
		{
			SkipReason = "Docker is unavailable; simulation scenarios require the Docker MySQL container.";
			return;
		}

		_scratchDirectory = Path.Combine(Path.GetTempPath(), $"aion-sim-{Environment.ProcessId}-{Guid.NewGuid():N}");
		Directory.CreateDirectory(_scratchDirectory);
		try
		{
			if (!int.TryParse(Environment.GetEnvironmentVariable("AION_SIM_SEED") ?? "1",
				System.Globalization.NumberStyles.Integer,
				System.Globalization.CultureInfo.InvariantCulture,
				out int seed))
				throw new InvalidOperationException("AION_SIM_SEED must be an integer.");
			Seed = seed;
			Rnd.SetProcessSeed(seed);
			_database = await RunDatabaseScriptAsync("Create");
			_processExitHandler = (_, _) => DropDatabaseAtProcessExit();
			AppDomain.CurrentDomain.ProcessExit += _processExitHandler;

			string configRoot = Path.Combine(_scratchDirectory, "config");
			CopyConfigDirectory("administration", configRoot);
			CopyConfigDirectory("main", configRoot);
			CopyConfigDirectory("network", configRoot);
			string cacheDirectory = Path.Combine(_scratchDirectory, "cache");

			Clock = new VirtualThreadPool(strict: true);
			Epoch = EpochForProcess(Environment.GetEnvironmentVariable("AION_SIM_PROCESS_KEY") ?? "shard-00");
			SystemClock.SetProcessSource(() => Epoch.ToUnixTimeMilliseconds() + Clock.NowMillis);
			ServerTime.Initialize(TimeZoneInfo.Utc);

			var databaseOptions = new DatabaseOptions
			{
				Server = _database.Host,
				Port = _database.Port,
				Database = _database.Database,
				UserId = _database.User,
				Password = _database.Password,
				ConnectionTimeZone = "UTC",
			};
			var configLoadOptions = new GameServerConfigLoadOptions
			{
				ConfigRoot = configRoot,
				PostLoadOverride = () =>
				{
					GSConfig.ANALYZE_QUESTHANDLERS = false;
					GSConfig.TIME_ZONE_ID = TimeZoneInfo.Utc;
					NetworkConfig.LOG_UNKNOWN_PACKETS = true;
					NetworkConfig.LOG_IGNORED_PACKETS = true;
					WorldConfig.WORLD_MAX_TWINS_USUAL = 5;
					WorldConfig.WORLD_EMULATE_FASTTRACK = false;
					GeoDataConfig.GEO_ENABLE = true;
					GeoDataConfig.CANSEE_ENABLE = true;
					GeoDataConfig.FEAR_ENABLE = true;
					GeoDataConfig.GEO_NPC_MOVE = true;
					GeoDataConfig.GEO_MAP_IDS = "";
					EventsConfig.DISABLED_EVENTS = new HashSet<string>(StringComparer.Ordinal)
					{
						"Beyond Aion Server Buffs",
						"Increased XP Rates",
						"Increased Gathering & Crafting XP Rates",
						"Increased Drop Rates",
						"Increased Drop Rates 50%",
					};
					CustomConfig.ENABLE_RANDOM_QUEST_BONUS_REWARDS = false;
					if (Environment.GetEnvironmentVariable("AION_SIM_PROCESS_KEY") == "reset-L8")
						EventsConfig.DISABLED_EVENTS.Remove("Increased XP Rates");
					if (Environment.GetEnvironmentVariable("AION_SIM_PROCESS_KEY") == "reset-L8C")
					{
						EventsConfig.ENABLE_ADVENT_CALENDAR = true;
						foreach (string alias in Aion.Bots.Scenarios.PlayerCommandScenario.EnabledCommands)
							Aion.GameServer.Configs.Administration.CommandsConfig.ACCESS_LEVELS[alias] = 0;
					}
					ServerTime.Initialize(TimeZoneInfo.Utc);
				},
			};
			_configLoadScope = Config.UseRuntimeLoadOptions(configLoadOptions);

			var services = new ServiceCollection();
			LogCapture = new CapturingLoggerProvider();
			services.AddLogging(builder =>
			{
				builder.SetMinimumLevel(LogLevel.Trace);
				builder.AddProvider(LogCapture);
			});
			services.AddGameServer(new GameServerOptions(), databaseOptions, configLoadOptions);
			services.RemoveAll<ThreadPoolManager>();
			services.AddSingleton(Clock);
			services.AddSingleton<ThreadPoolManager>(serviceProvider => serviceProvider.GetRequiredService<VirtualThreadPool>());
			services.RemoveAll<IStaticDataLoader>();
			services.AddSingleton<IStaticDataLoader>(new SimulationStaticDataLoader(cacheDirectory));
			services.RemoveAll<IHostedService>();

			var accounts = Enumerable.Range(1, 94).Concat(Enumerable.Range(101, 32))
				.ToDictionary(id => id, id => new SimulationLoginAccount($"sim-player-{id}", AccessLevel: 0));
			accounts[99] = new("director", AccessLevel: 9);
			services.RemoveAll<LoginServerFacade>();
			services.AddSingleton(
				serviceProvider => new LoginServerFacade(
					serviceProvider.GetRequiredService<ILogger<LoginServerFacade>>(),
					serviceProvider.GetRequiredService<GameServerOptions>(),
					serviceProvider.GetRequiredService<ICharacterSelectionRepository>(),
					owner => LoginLink = new SimulationLoginServerLink(owner, accounts)));

			_services = services.BuildServiceProvider();
			LoggerFactory = _services.GetRequiredService<ILoggerFactory>();
			AionLog.SetFactory(LoggerFactory);
			DatabaseFactory.Initialize(databaseOptions);
			ThreadPoolManager.RegisterInstance(Clock);
			LoginServer = _services.GetRequiredService<LoginServerFacade>();
			_ = _services.GetRequiredService<ChatServerFacade>();

			Bootstrap = _services.GetRequiredService<GameServerBootstrapService>();
			using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
			await Bootstrap.StartAsync(timeout.Token);
			Clock.Advance(TimeSpan.Zero);
			World = _services.GetRequiredService<GameWorld>();
			DataManager = _services.GetRequiredService<GameServerRuntimeContext>().DataManager
				?? throw new InvalidOperationException("Simulation bootstrap did not register static data.");
			foreach (int mapId in new[] { 210010000, 220010000 })
			{
				var geo = Aion.GameServer.World.Geo.GeoService.GetInstance().GetMap(mapId);
				if (!geo.HasTerrain() || geo.GetEntityCount() == 0)
					throw new InvalidOperationException($"SIM geo profile did not load terrain and placements for {mapId}.");
			}
			IsAvailable = true;
			SkipReason = string.Empty;
		}
		catch
		{
			await DisposeAsync();
			throw;
		}
	}

	public async Task DisposeAsync()
	{
		if (_processExitHandler != null)
		{
			AppDomain.CurrentDomain.ProcessExit -= _processExitHandler;
			_processExitHandler = null;
		}

		if (Bootstrap?.IsStarted == true)
			await Bootstrap.StopAsync(CancellationToken.None);
		if (_services != null)
		{
			await _services.DisposeAsync();
			_services = null;
		}
		else if (Clock != null)
		{
			await Clock.DisposeAsync();
		}
		_configLoadScope?.Dispose();
		_configLoadScope = null;

		DatabaseFactory.Dispose();
		SystemClock.UseSystemClockProcessWide();
		ServerTime.Initialize(TimeZoneInfo.Local);
		Rnd.UseProductionRandom();
		Rnd.UseProductionRandomProcessWide();
		if (_database != null)
		{
			await RunDatabaseScriptAsync("Drop", _database.Database);
			_database = null;
		}
		if (_scratchDirectory != null && Directory.Exists(_scratchDirectory))
		{
			Directory.Delete(_scratchDirectory, recursive: true);
			_scratchDirectory = null;
		}
	}

	private void CopyConfigDirectory(string name, string destinationRoot)
	{
		string source = Path.Combine(_repoRoot, "game-server", "config", name);
		string destination = Path.Combine(destinationRoot, name);
		CopyDirectory(source, destination);
	}

	private static void CopyDirectory(string source, string destination)
	{
		Directory.CreateDirectory(destination);
		foreach (string file in Directory.EnumerateFiles(source))
			File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
		foreach (string child in Directory.EnumerateDirectories(source))
			CopyDirectory(child, Path.Combine(destination, Path.GetFileName(child)));
	}

	private async Task<SimDatabase> RunDatabaseScriptAsync(string action, string? databaseName = null)
	{
		string runId = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? $"p{Environment.ProcessId}";
		string shard = Environment.GetEnvironmentVariable("AION_SIM_SHARD") ?? "0";
		var arguments = new List<string>
		{
			"-NoProfile", "-File", Path.Combine(_repoRoot, "scripts", "sim", "new-sim-db.ps1"),
			"-Action", action, "-RunId", runId, "-Shard", shard,
		};
		if (databaseName != null)
		{
			arguments.Add("-DatabaseName");
			arguments.Add(databaseName);
		}
		ProcessResult result = await RunProcessAsync("pwsh", arguments);
		if (result.ExitCode != 0)
			throw new InvalidOperationException($"Simulation database {action} failed: {result.StandardError}\n{result.StandardOutput}");
		if (action == "Drop")
			return _database ?? new SimDatabase(databaseName!, "127.0.0.1", 3306, "root", "aion", "aion-mysql");
		return JsonSerializer.Deserialize<SimDatabase>(
			result.StandardOutput.Trim(),
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
			?? throw new InvalidOperationException("Simulation database helper returned no connection details.");
	}

	private static async Task<bool> DockerIsAvailableAsync()
	{
		try
		{
			ProcessResult result = await RunProcessAsync("docker", ["info", "--format", "{{.ServerVersion}}"]);
			return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput);
		}
		catch
		{
			return false;
		}
	}

	private void DropDatabaseAtProcessExit()
	{
		if (_database == null)
			return;
		try
		{
			RunDatabaseScriptAsync("Drop", _database.Database).GetAwaiter().GetResult();
		}
		catch
		{
			// Process teardown cannot surface cleanup errors; the normal IAsyncLifetime path still does.
		}
	}

	private static async Task<ProcessResult> RunProcessAsync(string fileName, IEnumerable<string> arguments)
	{
		var startInfo = new ProcessStartInfo(fileName)
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};
		foreach (string argument in arguments)
			startInfo.ArgumentList.Add(argument);
		using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {fileName}.");
		Task<string> output = process.StandardOutput.ReadToEndAsync();
		Task<string> error = process.StandardError.ReadToEndAsync();
		await process.WaitForExitAsync();
		return new ProcessResult(process.ExitCode, await output, await error);
	}

	private sealed class SimulationStaticDataLoader(string cacheDirectory) : IStaticDataLoader
	{
		public Task<DataManager> LoadAsync(CancellationToken cancellationToken = default) =>
			RealStaticData.LoadAsync(cacheDirectory);
	}

	private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

	private sealed record SimDatabase(
		string Database,
		string Host,
		int Port,
		string User,
		string Password,
		string Container);
}
