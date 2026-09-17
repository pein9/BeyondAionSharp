using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Aion.Commons.Logging;

/// <summary>Writes machine-readable server events and Warning+ problems using the Phase 1 log contract.</summary>
public sealed class JsonLinesLoggerProvider : ILoggerProvider, ISupportExternalScope
{
	private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

	private readonly string _serverName;
	private readonly string? _runId;
	private readonly StreamWriter _events;
	private readonly StreamWriter _problems;
	private readonly object _writeLock = new();
	private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();
	private bool _disposed;

	public JsonLinesLoggerProvider(string directory, string serverName, string? runId = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(directory);
		ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
		if (serverName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
			throw new ArgumentException("Server name cannot contain file-name separators or invalid characters.", nameof(serverName));

		Directory.CreateDirectory(directory);
		_serverName = serverName;
		_runId = runId ?? Environment.GetEnvironmentVariable("AION_RUN_ID");
		_events = Open(Path.Combine(directory, $"{serverName}.events.jsonl"));
		_problems = Open(Path.Combine(directory, $"{serverName}.problems.jsonl"));
	}

	public ILogger CreateLogger(string categoryName) => new JsonLinesLogger(this, categoryName);

	public void SetScopeProvider(IExternalScopeProvider scopeProvider) =>
		_scopeProvider = scopeProvider ?? throw new ArgumentNullException(nameof(scopeProvider));

	public void Dispose()
	{
		lock (_writeLock)
		{
			if (_disposed)
				return;
			_disposed = true;
			_events.Dispose();
			_problems.Dispose();
		}
	}

	private void Write<TState>(
		string category,
		LogLevel level,
		TState state,
		Exception? exception,
		Func<TState, Exception?, string> formatter)
	{
		var scopes = CaptureScopes();
		var message = formatter(state, exception);
		var template = GetTemplate(state) ?? message;
		var fingerprint = LogFingerprint.Create(state, exception, message);
		var timer = GetTimer(scopes);
		var line = Serialize(
			level,
			category,
			template,
			message,
			exception,
			scopes.GetValueOrDefault("account") ?? scopes.GetValueOrDefault("acct"),
			scopes.GetValueOrDefault("player"),
			scopes.GetValueOrDefault("packet") ?? scopes.GetValueOrDefault("opcode"),
			timer,
			fingerprint);

		lock (_writeLock)
		{
			if (_disposed)
				return;
			_events.WriteLine(line);
			if (level >= LogLevel.Warning)
			{
				_problems.WriteLine(line);
				_events.Flush();
				_problems.Flush();
			}
		}
	}

	private Dictionary<string, string> CaptureScopes()
	{
		var values = new Dictionary<string, string>(StringComparer.Ordinal);
		_scopeProvider.ForEachScope(
			(scope, target) =>
			{
				if (scope is IEnumerable<KeyValuePair<string, string>> stringValues)
				{
					foreach (var value in stringValues)
						target[value.Key] = value.Value;
				}
				else if (scope is IEnumerable<KeyValuePair<string, object?>> objectValues)
				{
					foreach (var value in objectValues)
					{
						if (value.Value != null)
							target[value.Key] = Convert.ToString(value.Value, CultureInfo.InvariantCulture) ?? string.Empty;
					}
				}
			},
			values);
		return values;
	}

	private string Serialize(
		LogLevel level,
		string category,
		string template,
		string message,
		Exception? exception,
		string? account,
		string? player,
		string? operation,
		string? timer,
		LogFingerprintResult fingerprint)
	{
		using var stream = new MemoryStream();
		using (var json = new Utf8JsonWriter(stream))
		{
			json.WriteStartObject();
			json.WriteString("lvl", FormatLevel(level));
			json.WriteString("ts", DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
			json.WriteString("srv", _serverName);
			WriteNullableString(json, "run", _runId);
			WriteNullableString(json, "acct", account);
			WriteNullableString(json, "player", player);
			WriteNullableString(json, "op", operation);
			json.WriteString("cat", category);
			json.WriteString("thr", Thread.CurrentThread.Name ?? Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture));
			WriteNullableString(json, "timer", timer);
			json.WriteString("fp", fingerprint.Value);
			json.WriteString("tpl", template);
			json.WriteString("msg", message);
			WriteNullableString(json, "exType", exception?.GetType().FullName);
			WriteNullableString(json, "exMsg", exception?.Message);
			json.WriteString("frame", fingerprint.Frame);
			WriteNullableString(json, "stack", exception?.ToString());
			json.WriteEndObject();
		}
		return Utf8WithoutBom.GetString(stream.GetBuffer(), 0, checked((int)stream.Length));
	}

	private static string? GetTemplate<TState>(TState state)
	{
		if (state is IEnumerable<KeyValuePair<string, object?>> values)
		{
			foreach (var value in values)
			{
				if (value.Key == "{OriginalFormat}")
					return value.Value?.ToString();
			}
		}
		return null;
	}

	private static string? GetTimer(IReadOnlyDictionary<string, string> scopes)
	{
		if (!scopes.TryGetValue("timer", out var kind))
			return null;
		return scopes.TryGetValue("timerScheduledAt", out var scheduledAt)
			? $"{kind}@{scheduledAt}"
			: kind;
	}

	private static void WriteNullableString(Utf8JsonWriter writer, string name, string? value)
	{
		if (value == null)
			writer.WriteNull(name);
		else
			writer.WriteString(name, value);
	}

	private static string FormatLevel(LogLevel level) => level switch
	{
		LogLevel.Trace => "TRACE",
		LogLevel.Debug => "DEBUG",
		LogLevel.Information => "INFO",
		LogLevel.Warning => "WARN",
		LogLevel.Error => "ERROR",
		LogLevel.Critical => "ERROR",
		_ => level.ToString().ToUpperInvariant(),
	};

	private static StreamWriter Open(string path) => new(
		new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
		Utf8WithoutBom)
	{
		NewLine = "\n",
	};

	private sealed class JsonLinesLogger(JsonLinesLoggerProvider provider, string category) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => provider._scopeProvider.Push(state);

		public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			if (IsEnabled(logLevel))
				provider.Write(category, logLevel, state, exception, formatter);
		}
	}
}
