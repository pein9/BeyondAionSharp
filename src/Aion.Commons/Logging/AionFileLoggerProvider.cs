using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Aion.Commons.Logging;

public sealed class AionFileLoggerProvider : ILoggerProvider
{
	private static readonly IReadOnlyDictionary<string, CategoryRoute> CategoryRoutes =
		new Dictionary<string, CategoryRoute>(StringComparer.Ordinal)
		{
			["ADMINAUDIT_LOG"] = new("adminaudit.log", Additive: false),
			["AUDIT_LOG"] = new("audit.log", Additive: true),
			["CHAT_LOG"] = new("chat.log", Additive: false),
			["CRAFT_LOG"] = new("craft.log", Additive: false),
			["EXCHANGE_LOG"] = new("exchange.log", Additive: true),
			["GAMECONNECTION_LOG"] = new("gameconnections.log", Additive: true),
			["EVENT_LOG"] = new("event.log", Additive: true),
			["INSTANCE_LOG"] = new("instance.log", Additive: true),
			["TAMPERING_LOG"] = new("tampering.log", Additive: true),
			["ITEM_LOG"] = new("item.log", Additive: false),
			["ITEM_HTML_LOG"] = new("item_htmls.log", Additive: false),
			["KILL_LOG"] = new("kill.log", Additive: true),
			["MAIL_LOG"] = new("mail.log", Additive: true),
			["SIEGE_LOG"] = new("siege.log", Additive: true),
			["SYSMAIL_LOG"] = new("sysmail.log", Additive: true),
			["WEB_REWARDS_LOG"] = new("webrewards.log", Additive: false),
			["HOUSE_AUCTION_LOG"] = new("auction.log", Additive: true),
			["GMITEMRESTRICTION"] = new("gm_item_restriction.log", Additive: true),
			["PLAYERTRANSFER"] = new("playertransfer.log", Additive: true),
		};

	private readonly string _logDirectory;
	private readonly object _writeLock = new();

	public AionFileLoggerProvider(string logDirectory)
	{
		if (string.IsNullOrWhiteSpace(logDirectory))
			throw new ArgumentException("Log directory cannot be empty.", nameof(logDirectory));
		_logDirectory = logDirectory;
		Directory.CreateDirectory(_logDirectory);
	}

	public ILogger CreateLogger(string categoryName)
	{
		return new AionFileLogger(_logDirectory, categoryName, _writeLock);
	}

	public void Dispose()
	{
	}

	private sealed class AionFileLogger : ILogger
	{
		private readonly string _logDirectory;
		private readonly string _categoryName;
		private readonly object _writeLock;

		public AionFileLogger(string logDirectory, string categoryName, object writeLock)
		{
			_logDirectory = logDirectory;
			_categoryName = categoryName;
			_writeLock = writeLock;
		}

		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull
		{
			return NullScope.Instance;
		}

		public bool IsEnabled(LogLevel logLevel)
		{
			return logLevel != LogLevel.None;
		}

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			if (!IsEnabled(logLevel))
				return;

			var message = formatter(state, exception);
			if (string.IsNullOrEmpty(message) && exception == null)
				return;

			var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:ss,fffzzz", CultureInfo.InvariantCulture);
			var exceptionText = exception == null ? string.Empty : Environment.NewLine + exception;
			var consoleLine = $"{timestamp} {FormatLevel(logLevel),-5} [{Environment.CurrentManagedThreadId}] {_categoryName} - {message}{exceptionText}{Environment.NewLine}";
			var hasCategoryRoute = CategoryRoutes.TryGetValue(_categoryName, out var categoryRoute);

			lock (_writeLock)
			{
				if (hasCategoryRoute)
					Append(categoryRoute.FileName, $"{timestamp} {message}{exceptionText}{Environment.NewLine}");

				if (hasCategoryRoute && !categoryRoute.Additive)
					return;

				Append("server_console.log", consoleLine);

				if (logLevel == LogLevel.Warning)
					Append("server_warnings.log", $"{timestamp} {_categoryName} - {message}{exceptionText}{Environment.NewLine}");
				else if (logLevel >= LogLevel.Error)
					Append("server_errors.log", $"{timestamp} {_categoryName} - {message}{exceptionText}{Environment.NewLine}");
			}
		}

		private void Append(string fileName, string line)
		{
			File.AppendAllText(Path.Combine(_logDirectory, fileName), line, Encoding.UTF8);
		}

		private static string FormatLevel(LogLevel logLevel)
		{
			return logLevel switch
			{
				LogLevel.Trace => "TRACE",
				LogLevel.Debug => "DEBUG",
				LogLevel.Information => "INFO",
				LogLevel.Warning => "WARN",
				LogLevel.Error => "ERROR",
				LogLevel.Critical => "ERROR",
				_ => logLevel.ToString().ToUpperInvariant(),
			};
		}
	}

	private readonly record struct CategoryRoute(string FileName, bool Additive);

	private sealed class NullScope : IDisposable
	{
		public static readonly NullScope Instance = new();

		public void Dispose()
		{
		}
	}
}
