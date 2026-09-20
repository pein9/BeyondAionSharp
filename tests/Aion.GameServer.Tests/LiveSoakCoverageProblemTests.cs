using System.Text.Json;
using Aion.LiveBots;

namespace Aion.GameServer.Tests;

public sealed class LiveSoakCoverageProblemTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CoverageFailureReachesWatcherWithOriginMissingActivitiesAndOriginalStack(bool empty)
	{
		string path = Path.Combine(Path.GetTempPath(), "aion-soak-coverage-" + Guid.NewGuid().ToString("N") + ".jsonl");
		try
		{
			await using (var problems = new LiveBotProblemWriter(path))
			{
				await LiveBotRunner.ValidateSoakCoverageAsync("coverage-test", problems, 96, "b191", "account191",
					new Dictionary<string, long> { ["Quest"] = 1, ["Relog"] = 2 });
				Assert.Equal(0, new FileInfo(path).Length);
				var counts = empty ? new Dictionary<string, long>() :
					new Dictionary<string, long> { ["Quest"] = 1, ["Relog"] = 0, ["Duel"] = 0, ["Trade"] = 0 };
				await Assert.ThrowsAsync<InvalidDataException>(() => LiveBotRunner.ValidateSoakCoverageAsync(
					"coverage-test", problems, 96, "b191", "account191", counts));
			}
			using var document = JsonDocument.Parse(Assert.Single(await File.ReadAllLinesAsync(path)));
			var row = document.RootElement;
			Assert.Equal("coverage-test", row.GetProperty("run").GetString());
			Assert.Equal("b191", row.GetProperty("bot").GetString());
			Assert.Equal("account191", row.GetProperty("account").GetString());
			Assert.Equal("soak-coverage", row.GetProperty("step").GetString());
			Assert.Equal("activity-coverage", row.GetProperty("kind").GetString());
			Assert.Contains(empty ? "all (empty coverage)" : "Duel, Relog, Trade", row.GetProperty("msg").GetString());
			Assert.Contains(nameof(LiveBotRunner.ValidateSoakCoverageAsync), row.GetProperty("stack").GetString());
		}
		finally { File.Delete(path); }
	}
}
