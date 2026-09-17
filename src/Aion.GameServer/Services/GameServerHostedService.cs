using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Aion.Commons.Concurrent;
using Aion.GameServer.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Capture;

namespace Aion.GameServer.Services;

/// <summary>
/// Boots the faithful NioServer game-client listener (Java parity: GameServer.initNioServer -> NioServer +
/// ServerCfg + GameConnectionFactoryImpl creating AionConnections). Infrastructure boundary: a clean C#
/// IHostedService shell around the faithful reactor (gameplay-faithful / infra-idiomatic principle), with
/// the bind endpoint taken from idiomatic GameServerOptions rather than Java's static NetworkConfig.
/// </summary>
public sealed class GameServerHostedService : IHostedService
{
	private readonly GameServerOptions _options;
	private readonly ILogger<GameServerHostedService> _logger;
	private NioServer? _nioServer;
	private JsonLinesServerPacketCaptureObserver? _packetTap;

	public GameServerHostedService(GameServerOptions options, ILogger<GameServerHostedService> logger)
	{
		_options = options;
		_logger = logger;
	}

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		var packetTapPath = ResolvePacketTapPath();
		if (packetTapPath != null)
		{
			_packetTap = new JsonLinesServerPacketCaptureObserver(packetTapPath);
			AionServerPacket.SetCaptureObserver(_packetTap);
			_logger.LogInformation("Server packet tap enabled at {Path}", packetTapPath);
		}
		try
		{
			_logger.LogInformation("Starting game-server client listener on {EndPoint}", _options.ClientEndPoint);
			// Java game server is single-threaded for read/write (NIO_READ_WRITE_THREADS must be 1).
			_nioServer = new NioServer(1, new ServerCfg(_options.ClientEndPoint, "Aion game clients", new GameConnectionFactoryImpl()));
			// Register the running reactor so admincommands/Debug can enumerate live client connections
			// (Java reaches this via reflection on the static GameServer.nioServer field).
			NioServer.RegisterInstance(_nioServer);
			_nioServer.Connect(new ThreadPoolExecutor());
			var processStartSeconds = new DateTimeOffset(Process.GetCurrentProcess().StartTime.ToUniversalTime()).ToUnixTimeSeconds();
			var elapsedSeconds = Math.Max(0, DateTimeOffset.UtcNow.ToUnixTimeSeconds() - processStartSeconds);
			_logger.LogInformation("Game server started in {ElapsedSeconds} seconds.", elapsedSeconds);
		}
		catch
		{
			AionServerPacket.SetCaptureObserver(NoOpServerPacketCaptureObserver.INSTANCE);
			if (_packetTap != null)
			{
				await _packetTap.DisposeAsync();
				_packetTap = null;
			}
			throw;
		}
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Stopping game-server client listener");
		_nioServer?.Shutdown();
		AionServerPacket.SetCaptureObserver(NoOpServerPacketCaptureObserver.INSTANCE);
		if (_packetTap != null)
		{
			await _packetTap.DisposeAsync();
			_packetTap = null;
		}
	}

	private static string? ResolvePacketTapPath()
	{
		var setting = Environment.GetEnvironmentVariable("AION_PACKET_TAP");
		if (string.IsNullOrWhiteSpace(setting))
			return null;
		if (!bool.TryParse(setting, out var enabled))
			throw new InvalidOperationException("AION_PACKET_TAP must be true or false.");
		if (!enabled)
			return null;
		var logDirectory = Environment.GetEnvironmentVariable("AION_LOG_JSONL_DIR");
		if (string.IsNullOrWhiteSpace(logDirectory))
			logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "log");
		return Path.GetFullPath(Path.Combine(logDirectory, "packet-tap.jsonl"));
	}
}
