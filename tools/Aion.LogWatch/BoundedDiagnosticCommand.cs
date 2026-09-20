using System.Diagnostics;
using System.Text;

namespace Aion.LogWatch;

internal sealed record DiagnosticCommandResult(int? ExitCode, string StandardOutput, string StandardError,
	bool TimedOut = false, bool Truncated = false, string? Failure = null)
{
	internal bool Succeeded => ExitCode == 0 && !TimedOut && !Truncated && Failure == null;
}

internal interface IDiagnosticCommand
{
	Task<DiagnosticCommandResult> RunAsync(IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken token);
}

/// <summary>Bounds the Docker CLI itself; container-side probes have their own timeout too.</summary>
internal sealed class BoundedDiagnosticCommand : IDiagnosticCommand
{
	internal const int OutputLimit = 128 * 1024;
	internal const int ErrorLimit = 16 * 1024;

	public Task<DiagnosticCommandResult> RunAsync(IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken token) =>
		RunProcessAsync("docker", arguments, timeout, token);

	internal static async Task<DiagnosticCommandResult> RunProcessAsync(string fileName, IReadOnlyList<string> arguments,
		TimeSpan timeout, CancellationToken token, int outputLimit = OutputLimit)
	{
		using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
		lifetime.CancelAfter(timeout);
		using var process = new Process { StartInfo = new ProcessStartInfo(fileName)
		{
			UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
		} };
		foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
		var stdout = new StringBuilder();
		var stderr = new StringBuilder();
		int truncated = 0;
		bool timedOut = false;
		bool started = false;
		Task reads = Task.CompletedTask;
		try
		{
			token.ThrowIfCancellationRequested();
			if (!process.Start()) return new(null, "", "", Failure: "Diagnostic process did not start.");
			started = true;
			async Task ReadAsync(StreamReader reader, StringBuilder target, int limit)
			{
				var buffer = new char[4096];
				try
				{
					while (true)
					{
						int count = await reader.ReadAsync(buffer.AsMemory(), lifetime.Token);
						if (count == 0) break;
						int take = Math.Min(count, limit - target.Length);
						target.Append(buffer, 0, take);
						if (take == count) continue;
						Interlocked.Exchange(ref truncated, 1);
						await lifetime.CancelAsync();
						break;
					}
				}
				catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
			}
			reads = Task.WhenAll(ReadAsync(process.StandardOutput, stdout, outputLimit),
				ReadAsync(process.StandardError, stderr, ErrorLimit));
			try { await process.WaitForExitAsync(lifetime.Token); }
			catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
			{
				timedOut = Volatile.Read(ref truncated) == 0;
				try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
				catch (InvalidOperationException) { }
			}
			await reads.WaitAsync(TimeSpan.FromSeconds(2));
			return new(process.HasExited ? process.ExitCode : null, stdout.ToString(), stderr.ToString(), timedOut, truncated != 0);
		}
		catch (Exception exception) when (exception is not OutOfMemoryException)
		{
			return new(null, "", "", lifetime.IsCancellationRequested, truncated != 0,
				$"{exception.GetType().Name}: {exception.Message}");
		}
		finally
		{
			await lifetime.CancelAsync();
			try { if (started && !process.HasExited) process.Kill(entireProcessTree: true); }
			catch (InvalidOperationException) { }
			try { await reads.WaitAsync(TimeSpan.FromSeconds(2)); }
			catch (Exception exception) when (exception is not OutOfMemoryException) { }
		}
	}
}
