using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Aion.Commons.Logging;

/// <summary>In-memory structured log capture used by deterministic scenario scopes.</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
	private readonly ConcurrentQueue<CapturedLogEntry> entries = new();
	private IExternalScopeProvider scopeProvider = new LoggerExternalScopeProvider();

	public IReadOnlyList<CapturedLogEntry> Entries => entries.ToArray();

	public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

	public void SetScopeProvider(IExternalScopeProvider provider) =>
		scopeProvider = provider ?? throw new ArgumentNullException(nameof(provider));

	public void Dispose()
	{
	}

	private void Capture<TState>(
		string category,
		LogLevel level,
		EventId eventId,
		TState state,
		Exception? exception,
		Func<TState, Exception?, string> formatter)
	{
		var scopes = new Dictionary<string, string>(StringComparer.Ordinal);
		scopeProvider.ForEachScope(
			(scope, target) =>
			{
				if (scope is IEnumerable<KeyValuePair<string, string>> stringValues)
				{
					foreach (var (key, value) in stringValues)
						target[key] = value;
				}
				else if (scope is IEnumerable<KeyValuePair<string, object?>> objectValues)
				{
					foreach (var (key, value) in objectValues)
					{
						if (value != null)
							target[key] = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
					}
				}
			},
			scopes);
		string message = formatter(state, exception);
		string template = GetTemplate(state) ?? message;
		entries.Enqueue(new CapturedLogEntry(
			DateTimeOffset.UtcNow,
			category,
			level,
			eventId,
			template,
			message,
			exception,
			LogFingerprint.Create(state, exception, message),
			scopes));
	}

	private static string? GetTemplate<TState>(TState state)
	{
		if (state is IEnumerable<KeyValuePair<string, object?>> values)
		{
			foreach (var (key, value) in values)
			{
				if (key == "{OriginalFormat}")
					return value?.ToString();
			}
		}
		return null;
	}

	private sealed class CapturingLogger(CapturingLoggerProvider provider, string category) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => provider.scopeProvider.Push(state);

		public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			if (IsEnabled(logLevel))
				provider.Capture(category, logLevel, eventId, state, exception, formatter);
		}
	}
}

public sealed record CapturedLogEntry(
	DateTimeOffset Timestamp,
	string Category,
	LogLevel Level,
	EventId EventId,
	string Template,
	string Message,
	Exception? Exception,
	LogFingerprintResult Fingerprint,
	IReadOnlyDictionary<string, string> Scopes);
