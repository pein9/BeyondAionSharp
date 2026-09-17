using Aion.GameServer.Model;
using Aion.GameServer.Data;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Time.Gametime;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Services;

public sealed class GameTimeService : GameEngine
{
	// Java parity: SingletonHolder pattern — set in constructor, accessed via GetInstance().
	private static GameTimeService? _instance;

	private const string GameTimeVariable = "time";
	private static readonly TimeSpan DefaultTickDelay = TimeSpan.FromSeconds(5);
	private static readonly TimeSpan DefaultTickPeriod = TimeSpan.FromSeconds(5);
	private static readonly TimeSpan DefaultSaveDelay = TimeSpan.FromMinutes(3);
	private static readonly TimeSpan DefaultSavePeriod = TimeSpan.FromMinutes(3);
	private readonly ILogger<GameTimeService> _logger;
	private readonly ThreadPoolManager _threadPoolManager;
	private readonly IServerVariablesRepository? _serverVariablesRepository;
	private readonly TimeSpan _tickDelay;
	private readonly TimeSpan _tickPeriod;
	private readonly TimeSpan _saveDelay;
	private readonly TimeSpan _savePeriod;
	private int _isInitialized;
	private int _isStarted;
	private GameTime _gameTime;
	private Task? _clockTask;
	private Task? _saveTask;
	private Func<AionServerPacket, CancellationToken, Task<int>>? _broadcastToWorld;

	public event Action? HourChanged;

	public event Action? ClockDayTimeChanged;

	public GameTimeService(ILogger<GameTimeService> logger, ThreadPoolManager threadPoolManager)
		: this(logger, threadPoolManager, null, DefaultTickDelay, DefaultTickPeriod, DefaultSaveDelay, DefaultSavePeriod)
	{
	}

	public GameTimeService(
		ILogger<GameTimeService> logger,
		ThreadPoolManager threadPoolManager,
		IServerVariablesRepository serverVariablesRepository)
		: this(logger, threadPoolManager, serverVariablesRepository, DefaultTickDelay, DefaultTickPeriod, DefaultSaveDelay, DefaultSavePeriod)
	{
	}

	public GameTimeService(ILogger<GameTimeService> logger, ThreadPoolManager threadPoolManager, TimeSpan tickDelay, TimeSpan tickPeriod)
		: this(logger, threadPoolManager, null, tickDelay, tickPeriod, DefaultSaveDelay, DefaultSavePeriod)
	{
	}

	public GameTimeService(
		ILogger<GameTimeService> logger,
		ThreadPoolManager threadPoolManager,
		IServerVariablesRepository? serverVariablesRepository,
		TimeSpan tickDelay,
		TimeSpan tickPeriod,
		TimeSpan saveDelay,
		TimeSpan savePeriod)
	{
		_logger = logger;
		_threadPoolManager = threadPoolManager;
		_serverVariablesRepository = serverVariablesRepository;
		_tickDelay = tickDelay;
		_tickPeriod = tickPeriod;
		_saveDelay = saveDelay;
		_savePeriod = savePeriod;
		_gameTime = CreateGameTime(null);
		_instance = this; // Java parity: SingletonHolder registers on construction
	}

	// Java parity: GameTimeService.getInstance()
	public static GameTimeService GetInstance() =>
		_instance ?? throw new InvalidOperationException("GameTimeService has not been initialized yet.");

	// Java parity: GameTimeService.getGameTime() returns the live mutable clock. The //time command mutates this
	// object directly, and GameTime's callbacks preserve the temporary-spawn/weather behavior for that mutation.
	public GameTime GetGameTime() => Volatile.Read(ref _gameTime);

	public string Name => "GameTimeService";

	public bool IsStarted => Volatile.Read(ref _isStarted) != 0;

	public int GameMinutes => GetGameTime().GetTime();

	public void SetWorldBroadcaster(Func<AionServerPacket, CancellationToken, Task<int>> broadcastToWorld)
	{
		// Java parity: PacketSendUtility.broadcastToWorld hook used by GameTimeService periodic update.
		_broadcastToWorld = broadcastToWorld;
	}

	public async ValueTask InitAsync(CancellationToken cancellationToken = default)
	{
		// Java parity: services/GameTimeService init during GameServer bootstrap.
		if (Interlocked.Exchange(ref _isInitialized, 1) == 0)
		{
			var persistedTime = _serverVariablesRepository == null
				? null
				: await _serverVariablesRepository.LoadIntAsync(GameTimeVariable, cancellationToken);
			Volatile.Write(ref _gameTime, CreateGameTime(persistedTime));
			_logger.LogInformation("Initialized GameTime");
		}
	}

	public void StartClock()
	{
		// Java parity: GameTimeService starts periodic game-time update task.
		if (Interlocked.Exchange(ref _isStarted, 1) != 0)
			throw new InvalidOperationException("Tried to start game time twice.");

		_clockTask = _threadPoolManager.ScheduleAtFixedRate(
			_ =>
			{
				GetGameTime().AddMinutes(1);
				return ValueTask.CompletedTask;
			},
			_tickDelay,
			_tickPeriod);
		if (_serverVariablesRepository != null)
		{
			_saveTask = _threadPoolManager.ScheduleAtFixedRate(
				async cancellationToken =>
				{
					var broadcastToWorld = _broadcastToWorld;
					if (broadcastToWorld != null)
					{
						_logger.LogInformation("Sending current game time to all players");
						await broadcastToWorld(new SM_GAME_TIME(), cancellationToken);
					}

					if (await SaveGameTimeAsync())
						_logger.LogInformation("Game time saved...");
					else
						_logger.LogWarning("Error saving game time");
				},
				_saveDelay,
				_savePeriod);
		}
		_logger.LogInformation("GameTime started. Update interval: {Seconds}s", (int)_savePeriod.TotalSeconds);
	}

	public async Task<bool> SaveGameTimeAsync(CancellationToken cancellationToken = default)
	{
		// Java parity: services/GameTimeService.saveGameTime -> ServerVariablesDAO.store("time", gameTime.getTime()).
		return _serverVariablesRepository == null
			|| await _serverVariablesRepository.StoreAsync(GameTimeVariable, GameMinutes, cancellationToken);
	}

	public async ValueTask ShutdownAsync(CancellationToken cancellationToken = default)
	{
		Volatile.Write(ref _isStarted, 0);
		await SaveGameTimeAsync(cancellationToken);
	}

	private GameTime CreateGameTime(int? minutes) => new(
		minutes,
		() => HourChanged?.Invoke(),
		() => ClockDayTimeChanged?.Invoke());
}
