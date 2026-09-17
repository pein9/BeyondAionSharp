using Aion.Commons.Logging;
using Microsoft.Extensions.Logging;

namespace Aion.Commons.Tests;

public sealed class AionFileLoggerProviderTests
{
	[Fact]
	public void FileLogger_WritesConsoleWarningAndErrorFiles()
	{
		var logDirectory = Path.Combine(Path.GetTempPath(), "aion-file-logger-" + Guid.NewGuid().ToString("N"));
		try
		{
			using var loggerFactory = LoggerFactory.Create(
				builder =>
				{
					builder.ClearProviders();
					builder.SetMinimumLevel(LogLevel.Trace);
					builder.AddProvider(new AionFileLoggerProvider(logDirectory));
				});
			var logger = loggerFactory.CreateLogger("Test.Category");

			logger.LogInformation("startup ok");
			logger.LogWarning("warn {Code}", 7);
			logger.LogError(new InvalidOperationException("boom"), "error {Code}", 9);

			var console = File.ReadAllText(Path.Combine(logDirectory, "server_console.log"));
			var warnings = File.ReadAllText(Path.Combine(logDirectory, "server_warnings.log"));
			var errors = File.ReadAllText(Path.Combine(logDirectory, "server_errors.log"));

			Assert.Contains("INFO", console);
			Assert.Contains("WARN", console);
			Assert.Contains("ERROR", console);
			Assert.Contains("Test.Category - startup ok", console);
			Assert.Contains("Test.Category - warn 7", warnings);
			Assert.DoesNotContain("error 9", warnings);
			Assert.Contains("Test.Category - error 9", errors);
			Assert.Contains("InvalidOperationException: boom", errors);
		}
		finally
		{
			if (Directory.Exists(logDirectory))
				Directory.Delete(logDirectory, recursive: true);
		}
	}

	[Fact]
	public void FileLogger_RoutesJavaNamedCategoriesWithMatchingAdditivity()
	{
		var logDirectory = Path.Combine(Path.GetTempPath(), "aion-file-logger-" + Guid.NewGuid().ToString("N"));
		try
		{
			using var loggerFactory = LoggerFactory.Create(
				builder =>
				{
					builder.ClearProviders();
					builder.SetMinimumLevel(LogLevel.Trace);
					builder.AddProvider(new AionFileLoggerProvider(logDirectory));
				});

			var expectedRoutes = new Dictionary<string, string>
			{
				["ADMINAUDIT_LOG"] = "adminaudit.log",
				["AUDIT_LOG"] = "audit.log",
				["CHAT_LOG"] = "chat.log",
				["CRAFT_LOG"] = "craft.log",
				["EXCHANGE_LOG"] = "exchange.log",
				["GAMECONNECTION_LOG"] = "gameconnections.log",
				["EVENT_LOG"] = "event.log",
				["INSTANCE_LOG"] = "instance.log",
				["TAMPERING_LOG"] = "tampering.log",
				["ITEM_LOG"] = "item.log",
				["ITEM_HTML_LOG"] = "item_htmls.log",
				["KILL_LOG"] = "kill.log",
				["MAIL_LOG"] = "mail.log",
				["SIEGE_LOG"] = "siege.log",
				["SYSMAIL_LOG"] = "sysmail.log",
				["WEB_REWARDS_LOG"] = "webrewards.log",
				["HOUSE_AUCTION_LOG"] = "auction.log",
				["GMITEMRESTRICTION"] = "gm_item_restriction.log",
				["PLAYERTRANSFER"] = "playertransfer.log",
			};

			foreach (var (category, fileName) in expectedRoutes)
			{
				loggerFactory.CreateLogger(category).LogInformation("route {Category}", category);
				Assert.Contains(category, File.ReadAllText(Path.Combine(logDirectory, fileName)));
			}

			var console = File.ReadAllText(Path.Combine(logDirectory, "server_console.log"));
			Assert.Contains("EXCHANGE_LOG", console);
			Assert.DoesNotContain("CRAFT_LOG", console);
		}
		finally
		{
			if (Directory.Exists(logDirectory))
				Directory.Delete(logDirectory, recursive: true);
		}
	}
}
