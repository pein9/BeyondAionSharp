using Aion.Commons.Configuration;
using Aion.Commons.Diagnostics;
using Aion.GameServer.Commons.Network;
using Aion.GameServer.Configs;
using Aion.GameServer.Data;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Configuration;

/// <summary>Shared production/SIM composition for the game-server object graph.</summary>
public static class GameServerServiceCollectionExtensions
{
	public static IServiceCollection AddGameServer(
		this IServiceCollection services,
		GameServerOptions options,
		DatabaseOptions databaseOptions,
		GameServerConfigLoadOptions? configLoadOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(databaseOptions);

		services.AddSingleton(options);
		services.AddSingleton(databaseOptions);
		services.AddSingleton(configLoadOptions ?? GameServerConfigLoadOptions.Default);
		services.AddSingleton<ThreadPoolMetrics>();
		services.AddSingleton<Action<ThreadPoolScheduleObservation>>(
			serviceProvider => serviceProvider.GetRequiredService<ThreadPoolMetrics>().Observe);
		services.AddSingleton<ThreadPoolManager>();
		services.AddSingleton<IDFactory>();
		services.AddSingleton<GameServerRuntimeContext>();
		services.AddSingleton<GameWorld>();
		services.AddSingleton<GameTimeService>();

		// Faithful static services remain bootstrapped by GameServerBootstrapService after DataManager is registered.
		// Only the DI-native periodic engines belong in this reusable graph.
		services.AddSingleton<PeriodicInstanceRegistrationService>();
		services.AddSingleton<LimitedItemTradeSchedulerService>();
		services.AddSingleton<Aion.GameServer.Model.GameEngine>(
			serviceProvider => serviceProvider.GetRequiredService<LimitedItemTradeSchedulerService>());
		services.AddSingleton<HouseAuctionTimingService>();
		services.AddSingleton<HouseMaintenanceTimingService>();
		services.AddSingleton<ShutdownHook>();

		services.AddSingleton<IStaticDataLoader, StaticDataService>();
		services.AddSingleton<Aion.GameServer.Network.LoginServer.LoginServer>();
		services.AddSingleton<Aion.GameServer.Network.ChatServer.ChatServer>();
		services.AddSingleton<IUsedIdRepository, MySqlUsedIdRepository>();
		services.AddSingleton<IPlayerOnlineStateRepository, PlayerDaoOnlineStateRepository>();
		services.AddSingleton<IServerVariablesRepository, MySqlServerVariablesRepository>();
		services.AddSingleton<ICharacterSelectionRepository, MySqlCharacterSelectionRepository>();
		services.AddSingleton<IMailRepository, MySqlMailRepository>();
		services.AddSingleton<ISocialRepository, MySqlSocialRepository>();
		services.AddSingleton<IMotionRepository, MySqlMotionRepository>();
		services.AddSingleton<PlayerEnterWorldService>();

		// Register bootstrap by concrete type as well as IHostedService so SIM can resolve and start only the shared
		// bootstrap while omitting the client listener and outbound links.
		services.TryAddSingleton<GameServerBootstrapService>();
		services.AddSingleton<IHostedService>(
			serviceProvider => serviceProvider.GetRequiredService<GameServerBootstrapService>());
		services.AddHostedService<GameServerHostedService>();
		services.AddHostedService<OutboundLinkHostedService>();
		services.AddSingleton<IServerHeartbeatMetrics>(serviceProvider => new DelegateServerHeartbeatMetrics(
			() => NioServer.GetRegisteredInstance()?.GetAllConnections().Count ?? 0,
			() => AionConnection.PacketQueueDepth,
			() => serviceProvider.GetRequiredService<ThreadPoolMetrics>().ArmedTimerCount));
		services.AddHostedService<ServerHeartbeatService>();
		services.AddHostedService<Aion.GameServer.Services.Admin.AdminHttpService>();

		return services;
	}
}
