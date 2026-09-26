using System.Reflection;
using System.Threading.Channels;
using Aion.Bots.Protocol;
using Aion.Bots.Tracing;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.LiveBots;

namespace Aion.GameServer.Tests;

public sealed class LiveNaturalJourneyTests
{
	[Theory]
	[InlineData("NI-09", "2")]
	[InlineData("NI-09,connect", "1")]
	public void FullJourneyRequiresOneSubjectAndItsOwnScenario(string scenario, string bots) =>
		Assert.Throws<ArgumentException>(() => LiveBotOptions.Parse(["--run", "test", "--output", "run/test",
			"--scenario", scenario, "--bots", bots, "--git-sha", "test"]));

	[Fact]
	public async Task CancelledPacketWaitResumesSameReadWithoutDroppingPacket()
	{
		string directory = Path.Combine(Path.GetTempPath(), "aion-natural-session-" + Guid.NewGuid().ToString("N"));
		try
		{
			var options = LiveBotOptions.Parse(["--run", "test", "--output", directory, "--scenario", "NI-09", "--git-sha", "test"]);
			await using var problems = new LiveBotProblemWriter(Path.Combine(directory, "problems.jsonl"));
			using var trace = new BotActionTraceWriter(new MemoryStream(), "test", "b01", "test");
			await using var session = new LiveBotSession(options, problems, trace, "b01", "test", "Priest");
			session.EnableNaturalJourney();
			var incoming = Channel.CreateUnbounded<DecodedBotServerPacket>();
			var reading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			await using var packets = ReadPackets().GetAsyncEnumerator();
			// Supply the network iterator without a real server; exercise the actual public packet wait path.
			typeof(LiveBotSession).GetField("packets", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, packets);
			using var timeout = new CancellationTokenSource();
			Task<DecodedBotServerPacket> abandoned = session.WaitForPacketAsync(typeof(SM_PONG), timeout.Token);
			await reading.Task;
			timeout.Cancel();
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => abandoned);
			var expected = new DecodedBotServerPacket(typeof(SM_PONG), new Dictionary<string, object?>());
			await incoming.Writer.WriteAsync(expected);
			Assert.Same(expected, await session.WaitForPacketAsync(typeof(SM_PONG), CancellationToken.None));
			Assert.Same(expected, Assert.Single(session.PacketHistory));
			Assert.Empty(problems.Snapshot());
			async IAsyncEnumerable<DecodedBotServerPacket> ReadPackets()
			{
				reading.SetResult();
				yield return await incoming.Reader.ReadAsync();
			}
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
	}
}
