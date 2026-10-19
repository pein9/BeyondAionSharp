using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Aion.Commons.Configuration;
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
            ["CM_LOGIN"] = new("cm_login.log", Additive: false),
            ["com.aionemu.loginserver.network.aion.clientpackets.CM_LOGIN"] = new("cm_login.log", Additive: false),
		};

    private static readonly char[] JavaTrimChars = Enumerable.Range(0, 33).Select(i => (char)i).ToArray();
    private readonly string _logDirectory;
    private readonly object _writeLock = new();
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, long> _nextSizeCheck = new(StringComparer.Ordinal);
    public long MaxFileSizeBytes { get; }
    public int MaxRolledFiles { get; }

    public AionFileLoggerProvider(string logDirectory, long maxFileSizeBytes = 100 * 1024 * 1024,
        int maxRolledFiles = 5, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        _logDirectory = Path.GetFullPath(logDirectory);
        MaxFileSizeBytes = maxFileSizeBytes;
        // Logback FixedWindowRollingPolicy: minIndex=1, maximum window difference=20.
        MaxRolledFiles = Math.Clamp(maxRolledFiles, 1, 21);
        _timeProvider = timeProvider ?? TimeProvider.System;
        LogFileArchive.Archive(_logDirectory);
    }

    public static AionFileLoggerProvider FromJavaConfig(string logDirectory, string moduleName)
    {
        var (prefix, overrides) = moduleName switch
        {
            "game-server" => ("gameserver", "mygs.properties"),
            "login-server" => ("loginserver", "myls.properties"),
            "chat-server" => ("chatserver", "mycs.properties"),
            _ => throw new ArgumentException("Unknown server module.", nameof(moduleName)),
        };
        var root = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(logDirectory))!, "config");
        var loader = new ConfigLoader();
        // These are the property imports in each module's logback.xml, in that exact order.
        if (moduleName == "game-server") loader.LoadFromFile(Path.Combine(root, "main", "gameserver.properties"));
        loader.LoadFromFile(Path.Combine(root, "main", "logging.properties"));
        loader.LoadFromFile(Path.Combine(root, overrides));
        string Resolve(string value, HashSet<string> visiting) => Regex.Replace(value, @"\$\{([^{}]+)\}", match =>
        {
            var parts = match.Groups[1].Value.Split(":-", 2, StringSplitOptions.None);
            string key = parts[0];
            if (!visiting.Add(key)) throw new FormatException($"Circular logging property '{key}'.");
            try { return Resolve(loader.Get(key) ?? Environment.GetEnvironmentVariable(key) ?? (parts.Length > 1 ? parts[1] : key + "_IS_UNDEFINED"), visiting); }
            finally { visiting.Remove(key); }
        });
        // A failed Logback property conversion leaves the policy's own defaults (10MB and index7).
        long size = 10 * 1024 * 1024;
        int count = 7;
        try { size = ParseFileSize(Resolve(loader.Get(prefix + ".log.max_file_size", "100MB"), new(StringComparer.Ordinal)).Trim(JavaTrimChars)); }
        catch (Exception ex) when (ex is FormatException or OverflowException) { Console.Error.WriteLine($"Invalid {prefix}.log.max_file_size: {ex.Message}"); }
        try
        {
            string text = Resolve(loader.Get(prefix + ".log.max_rolled_files", "5"), new(StringComparer.Ordinal)).Trim(JavaTrimChars);
            // Integer.valueOf is decimal (not ConfigurableProcessor's Integer.decode), including BMP decimal digits.
            text = string.Concat(text.Select(c => char.IsDigit(c) ? (char)('0' + CharUnicodeInfo.GetDecimalDigitValue(c)) : c));
            count = int.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException) { Console.Error.WriteLine($"Invalid {prefix}.log.max_rolled_files: {ex.Message}"); }
        return new AionFileLoggerProvider(logDirectory, size, count);
    }

    private static long ParseFileSize(string text)
    {
        // Logback FileSize: whole ASCII digits, optional ASCII whitespace, KB/MB/GB and optional s.
        var match = Regex.Match(text, @"\A([0-9]+)[ \t\n\v\f\r]*(|kb|mb|gb)s?\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success) throw new FormatException($"Invalid log file size '{text}'.");
        long size = long.Parse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture);
        long coefficient = match.Groups[2].Value.ToLowerInvariant() switch
        {
            "kb" => 1024L,
            "mb" => 1024L * 1024,
            "gb" => 1024L * 1024 * 1024,
            _ => 1,
        };
        return unchecked(size * coefficient);
    }

    public ILogger CreateLogger(string categoryName) => new AionFileLogger(this, categoryName);

    // All category loggers share one lock and one size-check gate per output file.
    private void Append(string fileName, string line)
    {
        var path = Path.Combine(_logDirectory, fileName);
        long now = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        long next = _nextSizeCheck.GetValueOrDefault(fileName);
        if (now == -1 || now >= next)
        {
            if (now != -1) _nextSizeCheck[fileName] = unchecked(now + 60000);
            if (File.Exists(path) && new FileInfo(path).Length >= MaxFileSizeBytes)
            {
                string Rolled(int index) => Path.Combine(_logDirectory, $"{Path.GetFileNameWithoutExtension(fileName)}.{index}.log");
                File.Delete(Rolled(MaxRolledFiles));
                for (int i = MaxRolledFiles - 1; i >= 1; i--)
                    if (File.Exists(Rolled(i))) File.Move(Rolled(i), Rolled(i + 1));
                File.Move(path, Rolled(1));
            }
        }
        File.AppendAllText(path, line, Encoding.UTF8);
    }

	public void Dispose()
	{
	}

	private sealed class AionFileLogger : ILogger
	{
        private readonly AionFileLoggerProvider _owner;
        private readonly string _categoryName;
        public AionFileLogger(AionFileLoggerProvider owner, string categoryName)
        {
            _owner = owner;
            _categoryName = categoryName;
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

			lock (_owner._writeLock)
			{
				if (hasCategoryRoute)
					_owner.Append(categoryRoute.FileName, $"{timestamp} {message}{exceptionText}{Environment.NewLine}");

				if (hasCategoryRoute && !categoryRoute.Additive)
					return;

				_owner.Append("server_console.log", consoleLine);

				if (logLevel == LogLevel.Warning)
					_owner.Append("server_warnings.log", $"{timestamp} {_categoryName} - {message}{exceptionText}{Environment.NewLine}");
				else if (logLevel >= LogLevel.Error)
					_owner.Append("server_errors.log", $"{timestamp} {_categoryName} - {message}{exceptionText}{Environment.NewLine}");
			}
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
