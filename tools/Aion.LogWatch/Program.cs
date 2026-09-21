using Aion.LogWatch;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
	eventArgs.Cancel = true;
	cancellation.Cancel();
};

try
{
	if (args.FirstOrDefault() == "promote-full")
	{
		if (args.Length != 4) throw new ArgumentException("Usage: promote-full <run-directory> <repository-directory> <ledger-path>");
		await FullRunPromotion.RunAsync(args[1], args[2], args[3], cancellation.Token);
		return 0;
	}
	var options = WatchOptions.Parse(args);
	return await ProblemWatcher.RunAsync(options, cancellation.Token);
}
catch (Exception ex) when (args.FirstOrDefault() == "promote-full" && ex is not OperationCanceledException)
{
	Console.Error.WriteLine($"Full ledger promotion rejected: {ex.Message}");
	return 2;
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
