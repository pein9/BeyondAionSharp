using System.Text.Json;
using Aion.Bots.Protocol;
using Aion.Bots.Tracing;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.LiveBots;

namespace Aion.GameServer.Tests;

public sealed class LiveEntryProblemTests
{
	[Fact]
	public async Task EveryEntryRefusalIsRecordedOnceWithItsOriginBeforeStepPropagation()
	{
		string directory = Path.Combine(Path.GetTempPath(), "aion-entry-problems-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		try
		{
			string path = Path.Combine(directory, "problems.jsonl");
			var options = LiveBotOptions.Parse(["--run", "entry-errors", "--output", directory, "--scenario", "connect", "--git-sha", "test"]);
			await using (var problems = new LiveBotProblemWriter(path))
			using (var trace = new BotActionTraceWriter(new MemoryStream(), "entry-errors", "b157", "account157"))
			await using (var session = new LiveBotSession(options, problems, trace, "b157", "account157", "Testplayer"))
			{
				await session.CheckEntryResponseAsync(new(typeof(SM_ENTER_WORLD_CHECK), new Dictionary<string, object?> { ["msg"] = (byte)0 }));
				await session.CheckEntryResponseAsync(new(typeof(SM_QUIT_RESPONSE), new Dictionary<string, object?>()));
				Assert.Equal(0, new FileInfo(path).Length);
				for (byte code = 1; code <= 6; code++)
				{
					string step = $"s{code:D2}";
					session.BeginStep(step);
					var packet = new DecodedBotServerPacket(typeof(SM_ENTER_WORLD_CHECK), new Dictionary<string, object?> { ["msg"] = code });
					var error = await Assert.ThrowsAsync<LiveBotFailureException>(() => LiveBotRunner.RunStepAsync(
						options, problems, trace, "b157", "account157", step, "enter-world", CancellationToken.None,
						_ => session.CheckEntryResponseAsync(packet)));
					Assert.Equal($"SM_ENTER_WORLD_CHECK refused entry with message {code}.", error.Message);
				}
			}
			var lines = await File.ReadAllLinesAsync(path);
			Assert.Equal(6, lines.Length);
			for (int index = 0; index < lines.Length; index++)
			{
				using var record = JsonDocument.Parse(lines[index]);
				var row = record.RootElement;
				Assert.Equal("entry-errors", row.GetProperty("run").GetString());
				Assert.Equal("b157", row.GetProperty("bot").GetString());
				Assert.Equal("account157", row.GetProperty("account").GetString());
				Assert.Equal($"s{index + 1:D2}", row.GetProperty("step").GetString());
				Assert.Equal("enter-world-refused", row.GetProperty("kind").GetString());
				Assert.Equal($"SM_ENTER_WORLD_CHECK refused entry with message {index + 1}.", row.GetProperty("msg").GetString());
				Assert.Equal(typeof(LiveBotFailureException).FullName, row.GetProperty("exType").GetString());
				Assert.Contains(nameof(LiveBotSession.CheckEntryResponseAsync), row.GetProperty("stack").GetString());
			}
		}
		finally { Directory.Delete(directory, recursive: true); } // This test owns the exact GUID directory.
	}
}
