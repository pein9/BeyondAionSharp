using Aion.LogWatch;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
	eventArgs.Cancel = true;
	cancellation.Cancel();
};

try
{
	var options = WatchOptions.Parse(args);
	return await ProblemWatcher.RunAsync(options, cancellation.Token);
}
catch (ArgumentException ex)
{
	Console.Error.WriteLine(ex.Message);
	Console.Error.WriteLine(WatchOptions.Usage);
	return 2;
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
	return 130;
}
