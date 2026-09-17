using Aion.Commons.Configuration;
using Aion.Commons.Database;
using Aion.Commons.Diagnostics;
using Aion.GameServer.Configuration;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// NOTE: the working directory is re-rooted to <repo>/game-server inside GameServerBootstrapService.StartAsync (the
// boot entry shared by the host and the integration tests) so Java-relative data/config paths resolve consistently.

var builder = Host.CreateDefaultBuilder(args)
	.ConfigureAppConfiguration(
		(hostContext, config) =>
		{
			config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
			config.AddJsonFile($"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", optional: true);
			config.AddEnvironmentVariables();
		})
	.ConfigureHostOptions(
		options =>
		{
			// Java's shutdown hook has no host deadline. Leave enough time for the sequential player, legion,
			// server-variable and game-time persistence chain to complete.
			options.ShutdownTimeout = TimeSpan.FromSeconds(60);
		})
	.ConfigureServices(
		(_, services) =>
		{
			var options = GameServerOptions.LoadFromJavaConfig(AppContext.BaseDirectory);
			var databaseOptions = GameServerOptions.LoadDatabaseOptionsFromJavaConfig(AppContext.BaseDirectory);
			services.AddGameServer(options, databaseOptions);
		})
	.ConfigureLogging(
		(hostContext, logging) =>
		{
			logging.ClearProviders();
			logging.AddConsole();
			logging.AddProvider(new AionFileLoggerProvider(ResolveJavaModuleLogDirectory("game-server")));
			var jsonLinesDirectory = Environment.GetEnvironmentVariable("AION_LOG_JSONL_DIR");
			if (!string.IsNullOrWhiteSpace(jsonLinesDirectory))
				logging.AddProvider(new JsonLinesLoggerProvider(jsonLinesDirectory, "gs"));
			if (hostContext.HostingEnvironment.IsDevelopment())
				logging.AddDebug();
		});

using var host = builder.Build();
AionLog.SetFactory(host.Services.GetRequiredService<ILoggerFactory>());
using var processExceptionHandler = AionProcessExceptionHandler.Install();

// Database initialization is a host-lifecycle action, not service registration. Keeping it after Build lets the
// SIM host reuse AddGameServer with its per-run Docker database without touching a developer database.
DatabaseFactory.Initialize(host.Services.GetRequiredService<DatabaseOptions>());

// Bind the DI-created scheduler to the Java-style singleton accessor before hosted services start.
ThreadPoolManager.RegisterInstance(host.Services.GetRequiredService<ThreadPoolManager>());

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Aion Game Server starting...");

try
{
	await host.RunAsync();
}
finally
{
	DatabaseFactory.Dispose();
	logger.LogInformation("Aion Game Server stopped.");
}

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
