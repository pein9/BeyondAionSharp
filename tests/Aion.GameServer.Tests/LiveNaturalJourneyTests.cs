using System.Reflection;
using System.Threading.Channels;
using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.Bots.Scenarios;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.LiveBots;

namespace Aion.GameServer.Tests;

public sealed class LiveNaturalJourneyTests
{
	[Fact]
	public void AttachedFreshJourneyUsesASeparateOrdinaryPriest()
	{
		LiveBotOptions options = LiveBotOptions.Parse(["--run", "replay", "--output", "run/replay", "--git-sha", "test",
			"--scenario", "NI-10", "--attach-target", "aion", "--login-port", "2106", "--game-port", "7777",
			"--identity-slot", "2"]);
		Assert.Equal(2, options.AttachIdentitySlot);
		Assert.Equal("niishalgen2", NaturalIshalgenIdentityScenario.IdentityForSlot(2).AccountName);
		Assert.Equal("Ishalgenbottwo", NaturalIshalgenIdentityScenario.IdentityForSlot(2).CharacterName);
		Assert.Throws<ArgumentException>(() => LiveBotOptions.Parse(["--run", "replay", "--output", "run/replay",
			"--scenario", "NI-09", "--identity-slot", "2"]));
	}
	[Theory]
	[InlineData("NI-09", "2")]
	[InlineData("NI-09,connect", "1")]
	public void FullJourneyRequiresOneSubjectAndItsOwnScenario(string scenario, string bots) =>
		Assert.Throws<ArgumentException>(() => LiveBotOptions.Parse(["--run", "test", "--output", "run/test",
			"--scenario", scenario, "--bots", bots, "--git-sha", "test"]));

	[Theory]
	[InlineData("--scenario", "NI-10", "--login-port", "2106", "--game-port", "7777")] // attaching needs a named world
	[InlineData("--scenario", "NI-10", "--attach-target", "aion")] // and never falls back to the isolated stack's ports
	[InlineData("--scenario", "NI-10", "--attach-target", "aion", "--login-port", "2106")]
	[InlineData("--scenario", "NI-09", "--attach-target", "aion", "--login-port", "2106", "--game-port", "7777")]
	[InlineData("--scenario", "NI-10", "--attach-target", "aion;x", "--login-port", "2106", "--game-port", "7777")]
	[InlineData("--scenario", "NI-10", "--attach-target", "aion", "--login-port", "2106", "--game-port", "7777", "--bots", "2")]
	[InlineData("--scenario", "NI-09", "--stop-file", "stop")]
	public void AttachingRequiresAnExplicitWorldAndOnlyAttachScenarios(params string[] selection) =>
		Assert.Throws<ArgumentException>(() => LiveBotOptions.Parse(["--run", "test", "--output", "run/test",
			"--git-sha", "test", .. selection]));

	[Fact]
	public void AttachSelectsTheOperatorsWorldAndStopFile()
	{
		LiveBotOptions options = LiveBotOptions.Parse(["--run", "test", "--output", "run/test", "--git-sha", "test",
			"--scenario", "NI-10", "--attach-target", "aion", "--login-port", "2106", "--game-port", "7777",
			"--stop-file", "run/test/attach.stop"]);
		Assert.Equal("aion", options.AttachTarget);
		Assert.Equal(2106, options.LoginEndPoint.Port);
		Assert.Equal(7777, options.GameEndPoint.Port);
		Assert.Equal(Path.GetFullPath("run/test/attach.stop"), options.StopFile);
		Assert.Null(LiveBotOptions.Parse(["--run", "test", "--output", "run/test", "--git-sha", "test", "--scenario", "NI-09"]).AttachTarget);
	}

	[Fact]
	public void CoexistenceRecordsOnlyOtherPlayersAndLaterProgress()
	{
		var api = new BotApi();
		using var trace = new BotActionTraceWriter(new MemoryStream(), "test", "b01", "test");
		var log = new LiveCoexistenceLog();
		var self = new BotPosition(100, 100, 100, 0);
		void Observe(DecodedBotServerPacket packet)
		{
			api.Observe(packet);
			log.Observe(packet, api.World, 1, self, "s01", trace);
		}
		Observe(PlayerInfo(1, "Ishalgenbot", 100, 100));
		Observe(PlayerInfo(2, "Pilot", 110, 100));
		Observe(new DecodedBotServerPacket(typeof(SM_MOVE), new Dictionary<string, object?>
		{
			["objectId"] = 2, ["x"] = 103f, ["y"] = 104f, ["z"] = 100f, ["heading"] = (byte)0,
		}));
		string json = System.Text.Json.JsonSerializer.Serialize(log.Snapshot(3));
		using var document = System.Text.Json.JsonDocument.Parse(json);
		var root = document.RootElement;
		Assert.Equal(1, root.GetProperty("otherPlayersObserved").GetInt32());
		var pilot = Assert.Single(root.GetProperty("players").EnumerateArray());
		Assert.Equal("Pilot", pilot.GetProperty("Name").GetString());
		Assert.Equal(2, pilot.GetProperty("Observations").GetInt32());
		Assert.Equal(5f, pilot.GetProperty("MinimumDistance").GetSingle(), 3);
		Assert.Equal(3, pilot.GetProperty("questsCompletedSinceFirstSight").GetInt32());

		static DecodedBotServerPacket PlayerInfo(int objectId, string name, float x, float y) =>
			new(typeof(SM_PLAYER_INFO), new Dictionary<string, object?>
			{
				["objectId"] = objectId, ["name"] = name, ["x"] = x, ["y"] = y, ["z"] = 100f, ["heading"] = (byte)0,
				["state"] = (ushort)0, ["race"] = (byte)1, ["playerClass"] = (byte)9, ["movementSpeed"] = 6f,
			});
	}

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
