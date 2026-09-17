using Aion.LiveBots;

try
{
	var options = LiveBotOptions.Parse(args);
	return await LiveBotRunner.RunAsync(options);
}
catch (ArgumentException ex)
{
	Console.Error.WriteLine(ex.Message);
	Console.Error.WriteLine(LiveBotOptions.Usage);
	return 2;
}
