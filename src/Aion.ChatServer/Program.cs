using Aion.ChatServer.Configuration;
using Aion.ChatServer.Data.Repositories;
using Aion.ChatServer.Handlers;
using Aion.ChatServer.Handlers.BuiltIn;
using Aion.ChatServer.Models.Channels;
using Aion.ChatServer.Network;
using Aion.ChatServer.Services;
using Aion.Commons.Database;
using Aion.Commons.Diagnostics;
using Aion.Commons.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateDefaultBuilder(args)
	.ConfigureAppConfiguration(
		(hostContext, config) =>
		{
			config.AddEnvironmentVariables();
		}
	)
	.ConfigureServices(
		(hostContext, services) =>
		{
			var options = ChatServerOptions.LoadFromJavaConfig(Directory.GetCurrentDirectory());
			var databaseOptions = ChatServerOptions.LoadDatabaseOptionsFromJavaConfig(Directory.GetCurrentDirectory());
			DatabaseFactory.Initialize(databaseOptions);

			services.AddSingleton(options);
			services.AddSingleton<ChatChannels>();
			services.AddSingleton<IChatLogRepository, ChatLogRepository>();
			services.AddSingleton<IChatMessageHandler, FloodProtectionHandler>();
			services.AddSingleton<IChatMessageHandler, FilterHandler>();
			services.AddSingleton<IChatMessageHandler, LoggingHandler>();
			services.AddSingleton<ChatHandlerRegistry>();
			services.AddSingleton<IBroadcastService, BroadcastService>();
			services.AddSingleton<IChatService, ChatService>();
			services.AddSingleton<IGameServerService, GameServerService>();
			services.AddSingleton<ClientSocketServer>();
			services.AddSingleton<GameServerSocketServer>();
			services.AddHostedService<ChatServerHostedService>();
			services.AddSingleton<IServerHeartbeatMetrics>(serviceProvider => new DelegateServerHeartbeatMetrics(
				() => serviceProvider.GetRequiredService<ClientSocketServer>().GetActiveConnections()
					+ serviceProvider.GetRequiredService<GameServerSocketServer>().GetActiveConnections(),
				() => 0,
				() => 0));
			services.AddHostedService<ServerHeartbeatService>();
		}
	)
	.ConfigureLogging(
		(hostContext, logging) =>
		{
			logging.ClearProviders();
			logging.AddConsole();
			logging.AddProvider(new AionFileLoggerProvider(ResolveJavaModuleLogDirectory("chat-server")));
			var jsonLinesDirectory = Environment.GetEnvironmentVariable("AION_LOG_JSONL_DIR");
			if (!string.IsNullOrWhiteSpace(jsonLinesDirectory))
				logging.AddProvider(new JsonLinesLoggerProvider(jsonLinesDirectory, "cs"));
			if (hostContext.HostingEnvironment.IsDevelopment())
			{
				logging.AddDebug();
			}
		}
	);

using var host = builder.Build();
AionLog.SetFactory(host.Services.GetRequiredService<ILoggerFactory>());
using var processExceptionHandler = AionProcessExceptionHandler.Install();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Aion Chat Server starting...");

await host.RunAsync();

logger.LogInformation("Aion Chat Server stopped.");

static string ResolveJavaModuleLogDirectory(string moduleName)
{
	var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
	while (directory != null)
	{
		if (Directory.Exists(Path.Combine(directory.FullName, moduleName, "config")))
			return Path.Combine(directory.FullName, moduleName, "log");
		directory = directory.Parent;
	}

	return Path.Combine(Directory.GetCurrentDirectory(), "log");
}
