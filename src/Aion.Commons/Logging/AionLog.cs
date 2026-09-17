using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.Commons.Logging;

/// <summary>
/// Gives Java-style static loggers a late-bound <see cref="ILoggerFactory"/>.
/// </summary>
/// <remarks>
/// Java creates most loggers in static initializers, before the .NET host and its logging factory exist.
/// A logger returned by <see cref="For"/> therefore stores only its category. It resolves the current factory
/// for every operation, so installing the host factory later also activates loggers that already exist.
/// </remarks>
public static class AionLog
{
	internal const string CallerTypeKey = "aion.caller.type";
	internal const string CallerMemberKey = "aion.caller.member";

	private static readonly AsyncLocal<ILoggerFactory?> ScopedFactory = new();
	private static ILoggerFactory _factory = NullLoggerFactory.Instance;

	/// <summary>Creates a logger whose target is resolved when it is used, not when it is created.</summary>
	public static ILogger For(string category)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(category);
		return new ForwardingLogger(category);
	}

	/// <summary>Creates a typed logger using the Java-style simple class name as its category.</summary>
	public static ILogger<T> For<T>() => new ForwardingLogger<T>(For(typeof(T).Name));

	/// <summary>Sets the process-wide factory used outside a scoped override.</summary>
	public static void SetFactory(ILoggerFactory factory)
	{
		ArgumentNullException.ThrowIfNull(factory);
		Volatile.Write(ref _factory, factory);
	}

	/// <summary>
	/// Overrides the factory for the current asynchronous execution context. Nested scopes restore their parent.
	/// </summary>
	public static IDisposable OverrideFactory(ILoggerFactory factory)
	{
		ArgumentNullException.ThrowIfNull(factory);
		var previous = ScopedFactory.Value;
		ScopedFactory.Value = factory;
		return new FactoryOverride(previous);
	}

	private static ILoggerFactory CurrentFactory => ScopedFactory.Value ?? Volatile.Read(ref _factory);

	private sealed class ForwardingLogger : ILogger
	{
		private readonly string _category;

		public ForwardingLogger(string category) => _category = category;

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
			CurrentFactory.CreateLogger(_category).BeginScope(state);

		public bool IsEnabled(LogLevel logLevel) => CurrentFactory.CreateLogger(_category).IsEnabled(logLevel);

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			var target = CurrentFactory.CreateLogger(_category);
			if (!target.IsEnabled(logLevel))
				return;

			var callSite = FindCallSite();
			var forwardedState = new CallSiteState<TState>(state, callSite.Type, callSite.Member);
			target.Log(
				logLevel,
				eventId,
				forwardedState,
				exception,
				(s, e) => formatter(s.OriginalState, e));
		}
	}

	private sealed class ForwardingLogger<T>(ILogger inner) : ILogger<T>
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);

		public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter) => inner.Log(logLevel, eventId, state, exception, formatter);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static (string Type, string Member) FindCallSite()
	{
		foreach (var frame in new StackTrace().GetFrames())
		{
			var method = frame.GetMethod();
			var declaringType = method?.DeclaringType;
			if (method == null || declaringType == null || IsLoggingInfrastructure(declaringType))
				continue;

			return (declaringType.FullName ?? declaringType.Name, method.Name);
		}

		return ("<unknown>", "<unknown>");
	}

	private static bool IsLoggingInfrastructure(Type type)
	{
		if (type == typeof(AionLog) || type == typeof(ForwardingLogger) || type == typeof(LoggerExtensions))
			return true;

		return type.Namespace?.StartsWith("Microsoft.Extensions.Logging", StringComparison.Ordinal) == true;
	}

	private sealed class CallSiteState<TState> : IReadOnlyList<KeyValuePair<string, object?>>
	{
		private readonly IReadOnlyList<KeyValuePair<string, object?>>? _structuredState;
		private readonly string _callerType;
		private readonly string _callerMember;

		public CallSiteState(TState originalState, string callerType, string callerMember)
		{
			OriginalState = originalState;
			_structuredState = originalState as IReadOnlyList<KeyValuePair<string, object?>>;
			_callerType = callerType;
			_callerMember = callerMember;
		}

		public TState OriginalState { get; }

		public int Count => (_structuredState?.Count ?? 0) + 2;

		public KeyValuePair<string, object?> this[int index]
		{
			get
			{
				var originalCount = _structuredState?.Count ?? 0;
				if (index < 0 || index >= Count)
					throw new ArgumentOutOfRangeException(nameof(index));
				if (index < originalCount)
					return _structuredState![index];
				return index == originalCount
					? new KeyValuePair<string, object?>(CallerTypeKey, _callerType)
					: new KeyValuePair<string, object?>(CallerMemberKey, _callerMember);
			}
		}

		public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
		{
			for (var i = 0; i < Count; i++)
				yield return this[i];
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public override string? ToString() => OriginalState is null ? null : OriginalState.ToString();
	}

	private sealed class FactoryOverride : IDisposable
	{
		private readonly ILoggerFactory? _previous;
		private int _disposed;

		public FactoryOverride(ILoggerFactory? previous) => _previous = previous;

		public void Dispose()
		{
			if (Interlocked.Exchange(ref _disposed, 1) == 0)
				ScopedFactory.Value = _previous;
		}
	}
}
