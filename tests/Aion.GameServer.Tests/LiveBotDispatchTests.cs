using Aion.LiveBots;

namespace Aion.GameServer.Tests;

public sealed class LiveBotDispatchTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task UnsupportedSelectionNeverFallsThroughToSuccessfulConnectionSmoke(bool multipleKnownScenarios)
	{
		string output = Path.Combine(Path.GetTempPath(), "aion-dispatch-test-" + Guid.NewGuid().ToString("N"));
		try
		{
			LiveBotOptions options = LiveBotOptions.Parse([
				"--run", "dispatch-test", "--output", output, "--scenario", "connect", "--git-sha", "test"]);
			var template = options.ScenarioDefinitions.Single();
			string[] ids = multipleKnownScenarios ? ["M1", "M6"] : ["FUTURE-UNIMPLEMENTED"];
			options = options with
			{
				Scenarios = ids,
				ScenarioDefinitions = ids.Select(id => template with { Id = id }).ToArray(),
			};
			InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => LiveBotRunner.RunAsync(options));
			Assert.Contains("No LIVE dispatcher", error.Message);
			Assert.All(ids, id => Assert.Contains(id, error.Message));
			Assert.Empty(Directory.GetFiles(Path.Combine(output, "bots")));
		}
		finally
		{
			// This method alone creates this exact GUID-named temporary directory.
			if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
		}
	}
}
