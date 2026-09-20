using Aion.LiveBots;

try
{
	if (args.Length == 2 && args[0] == "--soak-evidence") return SoakWorkloadEvidence.WriteReport(args[1]);
	var options = LiveBotOptions.Parse(args);
	return await LiveBotRunner.RunAsync(options);
}
catch (ArgumentException ex)
{
	Console.Error.WriteLine(ex.Message);
	Console.Error.WriteLine(LiveBotOptions.Usage);
	return 2;
}
