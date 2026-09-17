using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Aion.Commons.Logging;

/// <summary>Installs the .NET equivalents of Java's process-wide uncaught-exception handler.</summary>
public static class AionProcessExceptionHandler
{
	private static readonly ILogger Log = AionLog.For("UncaughtExceptionHandler");

	public static IDisposable Install()
	{
		UnhandledExceptionEventHandler unhandled = OnUnhandledException;
		EventHandler<UnobservedTaskExceptionEventArgs> unobserved = OnUnobservedTaskException;
		AppDomain.CurrentDomain.UnhandledException += unhandled;
		TaskScheduler.UnobservedTaskException += unobserved;
		return new Registration(unhandled, unobserved);
	}

	private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args) =>
		LogUnhandledException(args.ExceptionObject, CurrentThreadName());

	private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args) =>
		LogUnobservedTaskException(args, CurrentThreadName());

	internal static void LogUnhandledException(object exceptionObject, string threadName)
	{
		var exception = exceptionObject as Exception
			?? new Exception("Unhandled exception object: " + exceptionObject);
		Log.LogError(exception, "Critical Error - Thread [{ThreadName}] terminated abnormally:", threadName);
	}

	internal static void LogUnobservedTaskException(UnobservedTaskExceptionEventArgs args, string threadName)
	{
		Log.LogError(args.Exception, "Critical Error - Thread [{ThreadName}] terminated abnormally:", threadName);
		args.SetObserved();
	}

	private static string CurrentThreadName() =>
		Thread.CurrentThread.Name ?? Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture);

	private sealed class Registration(
		UnhandledExceptionEventHandler unhandled,
		EventHandler<UnobservedTaskExceptionEventArgs> unobserved) : IDisposable
	{
		private int _disposed;

		public void Dispose()
		{
			if (Interlocked.Exchange(ref _disposed, 1) != 0)
				return;

			AppDomain.CurrentDomain.UnhandledException -= unhandled;
			TaskScheduler.UnobservedTaskException -= unobserved;
		}
	}
}
