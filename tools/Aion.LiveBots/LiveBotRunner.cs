using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aion.Bots.Api;
using Aion.Bots.Gm;
using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.Protocol.Chat;
using Aion.Bots.Protocol.Login;
using Aion.Bots.Reflexes;
using Aion.Bots.Scenarios;
using Aion.Bots.Timing;
using Aion.Bots.Tracing;
using Aion.Bots.Transport;
using Aion.Bots.World;
using Aion.ChatServer.Network;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	public static async Task<int> RunAsync(LiveBotOptions options, CancellationToken cancellationToken = default)
	{
		Directory.CreateDirectory(options.OutputDirectory);
		Directory.CreateDirectory(Path.Combine(options.OutputDirectory, "bots"));
		await WriteRunMetadataAsync(options, cancellationToken);
		await using var problems = new LiveBotProblemWriter(Path.Combine(options.OutputDirectory, "bot.problems.jsonl"));
		if (options.ScenarioDefinitions is [{ Id: "SOAK" }])
			return await RunSoakAsync(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "L0" }])
			return await RunL0Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "L1" }])
			return await RunL1Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "L2" }])
			return await RunL2Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "L3" }])
			return await RunL3Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "L4" }])
			return await RunL4Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "L5" }])
			return await RunL5Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "L7" }])
			return await RunL7Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "canaries" }])
			return await RunCanariesAsync(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "C1" }])
			return await RunC1Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "Q1" }])
			return await RunQ1Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "Q2" }])
			return await RunQ2Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "Q3" }])
			return await RunQ3Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "E1" }])
			return await RunGatheringAsync(options, problems, false, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "E2" }])
			return await RunGatheringAsync(options, problems, true, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "E3" }])
			return await RunE3Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "E4" }])
			return await RunE4Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "E5" }])
			return await RunE5Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "E6" }])
			return await RunE6Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "E7" }])
			return await RunE7Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "S1" }])
			return await RunS1Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "S2" }])
			return await RunS2Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "S3" }])
			return await RunS3Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "S4" }])
			return await RunS4Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "S5" }])
			return await RunS5Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "S6" }])
			return await RunS6Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "S7" }])
			return await RunS7Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "G1" }])
			return await RunG1Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "G2" }])
			return await RunG2Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "G3" }])
			return await RunG3Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "G4" }])
			return await RunG4Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "G5" }])
			return await RunG5Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "G6" }])
			return await RunG6Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "E8" }])
			return await RunE8Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "E9" }])
			return await RunE9Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "E10" }])
			return await RunE10Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "E11" }])
			return await RunE11Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "CAPITAL" }])
			return await RunCapitalAsync(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "Q4P" }])
			return await RunQuestPlanZoneAsync(options, problems, "Poeta", Race.ELYOS, "Aeliveqp", cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "Q4I" }])
			return await RunQuestPlanZoneAsync(options, problems, "Ishalgen", Race.ASMODIANS, "Asliveqi", cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "M1" }])
			return await RunM1Async(options, problems, cancellationToken);
		if (options.ScenarioDefinitions is [{ Id: "M6" }])
			return await RunM6Async(options, problems, cancellationToken);

		if (options.ScenarioDefinitions is not [{ Id: "connect" }])
			throw new InvalidOperationException("No LIVE dispatcher is implemented for this scenario selection: " +
				string.Join(", ", options.ScenarioDefinitions.Select(scenario => scenario.Id)) +
				". Run one supported scenario at a time; never substitute the connection smoke test.");

		var tasks = Enumerable.Range(1, options.BotCount)
			.Select(index => RunConnectBotAsync(options, problems, index, cancellationToken))
			.ToArray();
		var results = await Task.WhenAll(tasks);
		var failed = results.Count(result => !result);
		Console.WriteLine($"LIVE bots completed: {results.Length - failed} passed, {failed} failed.");
		return failed == 0 ? 0 : 1;
	}

	private static async Task<int> RunC1Async(LiveBotOptions options, LiveBotProblemWriter problems,
		CancellationToken cancellationToken)
	{
		await using var actor = new L0Actor(options, problems, 1, Race.ASMODIANS, characterName: "Asliveac");
		actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "C1" });
		try
		{
			await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, cancellationToken);
			await actor.StepAsync("create-asmodian-warrior", actor.Session.CreateCharacterAsync, cancellationToken);
			await actor.StepAsync("enter-world-and-finish-prologue", async token =>
			{
				await actor.Session.EnterWorldAsync(token);
				await actor.Session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
				await actor.Session.WaitForPacketAsync(typeof(SM_STATUPDATE_EXP), token,
					packet => packet.Get<long>("currentExp") == 1);
			}, cancellationToken);
			if (actor.Session.Api.World.Level != 1 || actor.Session.Api.World.CurrentExperience != 1)
				throw new InvalidDataException(
					$"C1 requires a fresh level-1 character with 1 XP; observed level " +
					$"{actor.Session.Api.World.Level}, XP {actor.Session.Api.World.CurrentExperience}.");

			int? channel = PlannedChannel(options, "C1");
			if (channel != null)
				await actor.StepAsync("isolate-channel", token => actor.Session.ChangeChannelAsync(channel.Value, token), cancellationToken);

			var defeated = new HashSet<int>();
			for (int kill = 1; kill <= 5; kill++)
			{
				int sprigg = await actor.Session.WaitForNpcExceptAsync(210363, defeated, cancellationToken);
				await actor.StepAsync($"move-to-sprigg-worker-{kill}",
					token => actor.Session.MoveToNpcAsync(sprigg, token), cancellationToken);
				await actor.StepAsync($"kill-sprigg-worker-{kill}", async token =>
				{
					long expectedExperience = kill == 5 ? 1 : 1 + kill * 80;
					Task<DecodedBotServerPacket> experience = actor.Session.WaitForPacketAsync(
						typeof(SM_STATUPDATE_EXP), token,
						packet => packet.Get<long>("currentExp") == expectedExperience);
					await Task.Delay(TimeSpan.FromMilliseconds(100), token);
					await actor.Session.MoveToNpcAsync(sprigg, token);
					await actor.Session.SendPacketAsync(actor.Session.Api.Target(sprigg), token);
					for (byte attack = 0; attack < 8 && !experience.IsCompleted; attack++)
					{
						if (attack > 0 && attack % 2 == 0)
							await actor.Session.MoveToNpcAsync(sprigg, token);
						await actor.Session.SendPacketAsync(actor.Session.Api.Attack(sprigg, 1400, attack), token);
						await Task.WhenAny(experience, Task.Delay(TimeSpan.FromMilliseconds(1450), token));
					}
					DecodedBotServerPacket update = await experience;
					if (update.Get<long>("currentExp") != expectedExperience)
						throw new InvalidDataException($"Sprigg kill {kill} did not award exactly 80 XP.");
				}, cancellationToken);
				defeated.Add(sprigg);
			}

			if (actor.Session.Api.World.Level != 2 || actor.Session.Api.World.CurrentExperience != 1)
				throw new InvalidDataException(
					$"C1 expected level 2 with 1 shown XP; observed level {actor.Session.Api.World.Level}, " +
					$"XP {actor.Session.Api.World.CurrentExperience}.");
			await actor.StepAsync("quit", actor.Session.QuitAsync, cancellationToken);
			actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "C1" });
			Console.WriteLine("LIVE C1 completed.");
			return 0;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"C1 failed: {ex}");
			return 1;
		}
	}

	private static async Task<int> RunM1Async(LiveBotOptions options, LiveBotProblemWriter problems,
		CancellationToken cancellationToken)
	{
		string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
		var assets = await BotNavigationAssets.LoadAsync(repoRoot, Path.Combine(options.OutputDirectory, "navigation-cache"), cancellationToken);
		await using var actor = new L0Actor(options, problems, 1, Race.ASMODIANS, characterName: "Asliveaa");
		actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "M1" });
		try
		{
			await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, cancellationToken);
			await actor.StepAsync("create-asmodian-warrior", actor.Session.CreateCharacterAsync, cancellationToken);
			await actor.StepAsync("enter-world-and-finish-prologue", async token =>
			{
				await actor.Session.EnterWorldAsync(token);
				await actor.Session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
			}, cancellationToken);
			int? channel = PlannedChannel(options, "M1");
			if (channel != null)
				await actor.StepAsync("isolate-channel", token => actor.Session.ChangeChannelAsync(channel.Value, token), cancellationToken);
			actor.Session.Navigation = assets.StarterRoute(Race.ASMODIANS, (channel ?? 0) + 1);
			int asak = await actor.Session.WaitForNpcAsync(203500, cancellationToken);
			await actor.StepAsync("walk-to-asak", token => actor.Session.MoveToNpcAsync(asak, token), cancellationToken);
			await actor.StepAsync("accept-quest-2101", token => actor.Session.StartQuestAsync(asak, 2101, token), cancellationToken);
			int vandar = await actor.Session.WaitForNpcAsync(203504, cancellationToken);
			await actor.StepAsync("walk-to-vandar", token => actor.Session.MoveToNpcAsync(vandar, token), cancellationToken);
			await actor.StepAsync("report-to-vandar", token => actor.Session.FinishQuestAsync(vandar, 2101, token), cancellationToken);
			if (!actor.Session.Api.World.Quests.TryGetValue(2000, out BotQuestState? prologue) || prologue.Status != 5 ||
				!actor.Session.Api.World.Quests.TryGetValue(2101, out BotQuestState? firstSteps) || firstSteps.Status != 5)
				throw new InvalidDataException("M1 did not complete quests 2000 and 2101.");
			await actor.StepAsync("quit", actor.Session.QuitAsync, cancellationToken);
			actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "M1" });
			Console.WriteLine("LIVE M1 completed.");
			return 0;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"M1 failed: {ex}");
			return 1;
		}
	}

	private static async Task<int> RunM6Async(LiveBotOptions options, LiveBotProblemWriter problems,
		CancellationToken cancellationToken)
	{
		await using var subject = new L0Actor(options, problems, 1, Race.ASMODIANS, characterName: "Asliveaa");
		await using var director = new L0Actor(options, problems, 99, Race.ELYOS,
			bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		subject.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "M6" });
		director.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "M6-director" });
		try
		{
			foreach (L0Actor actor in new[] { subject, director })
			{
				await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, cancellationToken);
				await actor.StepAsync("create-character", actor.Session.CreateCharacterAsync, cancellationToken);
				await actor.StepAsync("enter-world", actor.Session.EnterWorldAsync, cancellationToken);
			}

			LiveGmFacade gm = director.Session.CreateLiveGmFacade();
			var gmSubject = new GmSubject(subject.Session.CharacterId, subject.Session.CharacterName);
			await director.StepAsync("move-director-to-subject", async token =>
			{
				await gm.ExecuteAsync(new GmCommand("moveto", [subject.Session.CharacterName], "Teleported to"), cancellationToken: token);
				await director.Session.CompleteTeleportAsync(220010000, token);
			}, cancellationToken);
			await director.StepAsync("make-subject-daeva", async token =>
			{
				await gm.ExecuteAsync(new GmCommand("set", ["level", "9"], "level to 9"), gmSubject, token);
				await gm.ExecuteVerifiedAsync(
					new GmCommand("set", ["class", "gladiator"], "replyless class change"),
					new GmCommand("set", ["level", "9"], "level to 9"), gmSubject, token);
			}, cancellationToken);
			await director.StepAsync("observe-subject-daeva-class",
				token => director.Session.WaitForPlayerClassAsync(subject.Session.CharacterId, PlayerClass.GLADIATOR, token),
				cancellationToken);

			await MoveSubjectWithDirectorAsync(director, subject, gm, 400010000, 940f, 2695f, 1628.3f,
				"setup-reshanta-flight", cancellationToken);
			int initialFlightTime = subject.Session.Api.World.CurrentFlightTime;
			await subject.StepAsync("fly-up-and-drain-flight-time", async token =>
			{
				await subject.Session.SendPacketAsync(subject.Session.Api.Fly(), token);
				await subject.Session.WaitForPacketAsync(typeof(SM_EMOTION), token,
					packet => packet.Get<int>("senderObjectId") == subject.Session.CharacterId &&
						packet.Get<byte>("emotionType") == (byte)EmotionType.FLY);
				BotPosition start = subject.Session.CurrentPosition;
				await subject.Session.ExecuteMovementAsync(new BotMover(subject.Session.Api.World)
					.CreateFlightPlan([start with { Z = start.Z + 10 }]), token);
				DecodedBotServerPacket drained = await subject.Session.WaitForPacketAsync(typeof(SM_FLY_TIME), token,
					packet => packet.Get<int>("currentFp") < initialFlightTime);
				if (drained.Get<int>("currentFp") >= initialFlightTime)
					throw new InvalidDataException("Flight time did not drain.");
			}, cancellationToken);
			await subject.StepAsync("land", async token =>
			{
				await subject.Session.SendPacketAsync(subject.Session.Api.Land(), token);
				await subject.Session.WaitForPacketAsync(typeof(SM_EMOTION), token,
					packet => packet.Get<int>("senderObjectId") == subject.Session.CharacterId &&
						packet.Get<byte>("emotionType") == (byte)EmotionType.LAND);
			}, cancellationToken);
			await subject.StepAsync("flight-cooldown", token => Task.Delay(TimeSpan.FromSeconds(10), token), cancellationToken);

			var glideStart = new BotPosition(958.03f, 2703.21f, 1634.66f, 0);
			var glideEnd = new BotPosition(961.23f, 2687.78f, 1621.88f, 0);
			await MoveSubjectWithDirectorAsync(director, subject, gm, 400010000,
				glideStart.X, glideStart.Y, glideStart.Z, "setup-ring-ledge", cancellationToken);
			await subject.StepAsync("glide-through-fly-ring", async token =>
			{
				float speed = subject.Session.Api.World.MovementSpeed
					?? throw new InvalidOperationException("SM_PLAYER_INFO did not provide movement speed.");
				await subject.Session.ExecuteMovementAsync(new BotMover(subject.Session.Api.World)
					.CreateGlidePlan([glideEnd], glideStart, speed), token);
				await subject.Session.WaitForPacketAsync(typeof(SM_ABNORMAL_STATE), token,
					packet => packet.Get<List<IReadOnlyDictionary<string, object?>>>("effects")
						.Any(effect => Get<ushort>(effect, "skillId") == 265));
			}, cancellationToken);

			int announcementStart = subject.Session.PacketHistory.Count;
			await MoveSubjectWithDirectorAsync(director, subject, gm, 220070000,
				1888.25f, 2847.27f, 554.99f, "setup-gelkmaros-windstream", cancellationToken);
			if (!subject.Session.PacketHistory.Skip(announcementStart).Any(packet =>
				packet.PacketType == typeof(SM_WINDSTREAM_ANNOUNCE) && packet.Get<int>("mapId") == 220070000 &&
				packet.Get<int>("streamId") == 1))
				throw new InvalidDataException("Gelkmaros entry did not announce windstream 1.");
			await subject.StepAsync("ride-windstream", async token =>
			{
				await subject.Session.SendPacketAsync(GameClientPackets.Windstream(1, 0, 0), token);
				await subject.Session.WaitForPacketAsync(typeof(SM_WINDSTREAM), token,
					packet => packet.Get<int>("state") == 0);
				await subject.Session.SendPacketAsync(GameClientPackets.Windstream(1, 0, 1), token);
				await subject.Session.WaitForPacketAsync(typeof(SM_EMOTION), token,
					packet => packet.Get<int>("senderObjectId") == subject.Session.CharacterId &&
						packet.Get<byte>("emotionType") == (byte)EmotionType.WINDSTREAM);
				BotPosition start = subject.Session.CurrentPosition;
				await subject.Session.ExecuteMovementAsync(new BotMover(subject.Session.Api.World)
					.CreateFlightPlan([start with { X = start.X + 10 }]), token);
				await subject.Session.SendPacketAsync(GameClientPackets.Windstream(1, 10, 3), token);
				await subject.Session.WaitForPacketAsync(typeof(SM_WINDSTREAM), token,
					packet => packet.Get<int>("state") == 3);
			}, cancellationToken);
			await subject.StepAsync("quit", subject.Session.QuitAsync, cancellationToken);
			await director.StepAsync("quit", director.Session.QuitAsync, cancellationToken);
			subject.Trace.WriteAction(subject.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "M6" });
			Console.WriteLine("LIVE M6 completed.");
			return 0;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"M6 failed: {ex}");
			return 1;
		}
	}

	private static async Task MoveSubjectWithDirectorAsync(L0Actor director, L0Actor subject, LiveGmFacade gm,
		int mapId, float x, float y, float z, string action, CancellationToken cancellationToken)
	{
		await director.StepAsync("director-" + action, async token =>
		{
			await gm.ExecuteAsync(new GmCommand("moveto",
				[mapId.ToString(System.Globalization.CultureInfo.InvariantCulture),
					x.ToString(System.Globalization.CultureInfo.InvariantCulture),
					y.ToString(System.Globalization.CultureInfo.InvariantCulture),
					z.ToString(System.Globalization.CultureInfo.InvariantCulture)], "Teleported to"), cancellationToken: token);
			await director.Session.CompleteTeleportAsync(mapId, token);
		}, cancellationToken);
		await director.StepAsync("director-movetome-" + action, token =>
			gm.ExecuteAsync(new GmCommand("movetome", [subject.Session.CharacterName], "Teleported"), cancellationToken: token),
			cancellationToken);
		await subject.StepAsync(action, token => subject.Session.CompleteTeleportAsync(mapId, token), cancellationToken);
	}

	private static async Task<int> RunL0Async(LiveBotOptions options, LiveBotProblemWriter problems,
		CancellationToken cancellationToken)
	{
		var actors = Enumerable.Range(1, options.BotCount)
			.Select(index => new L0Actor(options, problems, index))
			.ToArray();
		try
		{
			foreach (var actor in actors)
				actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "L0" });
			int? channel = PlannedChannel(options, "L0");
			await L0Scenario.RunAsync(actors, channel, includeChat: true, cancellationToken);

			foreach (var actor in actors)
				actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "L0" });
			Console.WriteLine($"LIVE L0 completed: {actors.Length} bots passed.");
			return 0;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"L0 failed: {ex}");
			return 1;
		}
		finally
		{
			foreach (var actor in actors)
				await actor.DisposeAsync();
		}
	}

	private static async Task<int> RunCanariesAsync(LiveBotOptions options, LiveBotProblemWriter problems,
		CancellationToken cancellationToken)
	{
		await using var actor = new L0Actor(options, problems, 1);
		actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "canaries" });
		try
		{
			await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, cancellationToken);
			await actor.StepAsync("create-elyos-warrior", actor.Session.CreateCharacterAsync, cancellationToken);
			await actor.StepAsync("enter-world", actor.Session.EnterWorldAsync, cancellationToken);
			int? channel = PlannedChannel(options, "canaries");
			if (channel != null)
				await actor.StepAsync("isolate-channel", token => actor.Session.ChangeChannelAsync(channel.Value, token), cancellationToken);
			await actor.StepAsync("gs-friend-status-canary", actor.Session.SendFriendStatusCanaryAsync, cancellationToken);
			await actor.StepAsync("gs-emotion-canary", actor.Session.SendEmotionCanaryAsync, cancellationToken);
			await actor.StepAsync("ls-bad-checksum-canary", actor.Session.SendBadLoginChecksumCanaryAsync, cancellationToken);
			await actor.StepAsync("cs-unknown-opcode-canary", actor.Session.SendUnknownChatOpcodeCanaryAsync, cancellationToken);
			await actor.StepAsync("quit", actor.Session.QuitAsync, cancellationToken);
			actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "canaries" });
			Console.WriteLine("LIVE watcher canaries completed.");
			return 0;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"Watcher canaries failed: {ex}");
			return 1;
		}
	}

	private static async Task<bool> RunConnectBotAsync(LiveBotOptions options, LiveBotProblemWriter problems,
		int index, CancellationToken cancellationToken)
	{
		var bot = $"b{index:D2}";
		var account = $"{bot}r{DateTimeOffset.Now:MMdd}";
		using var trace = BotActionTraceWriter.Open(Path.Combine(options.OutputDirectory, "bots", $"{bot}.trace.jsonl"),
			options.Run, bot, account);
		await using var session = new LiveBotSession(options, problems, trace, bot, account, CharacterName(index));
		var stepNumber = 0;
		try
		{
			foreach (ScenarioDefinition scenario in options.ScenarioDefinitions)
			{
				var step = $"s{++stepNumber:D2}";
				trace.WriteAction(step, "scenario:start", new Dictionary<string, object?> { ["scenario"] = scenario.Id });
				session.BeginStep(step);
				await RunStepAsync(options, problems, trace, bot, account, step, "connect", cancellationToken,
					session.ConnectAndReadKeyAsync);
				step = $"s{++stepNumber:D2}";
				session.BeginStep(step);
				await RunStepAsync(options, problems, trace, bot, account, step, "close", cancellationToken, session.CloseAsync);
				trace.WriteAction(step, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = scenario.Id });
			}
			return true;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"{bot} failed: {ex}");
			return false;
		}
	}

	internal static async Task RunStepAsync(LiveBotOptions options, LiveBotProblemWriter problems,
		BotActionTraceWriter trace, string bot, string account, string step, string action,
		CancellationToken cancellationToken, Func<CancellationToken, Task> operation)
	{
		trace.WriteAction(step, action);
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(options.StepTimeout);
		try
		{
			await operation(timeout.Token);
		}
		catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
		{
			await problems.WriteAsync(options.Run, bot, account, step, "timeout",
				$"Step '{action}' exceeded {options.StepTimeout.TotalSeconds:n0} seconds.", ex);
			throw new LiveBotFailureException($"{bot} {step} '{action}' timed out.", ex);
		}
		catch (TimeoutException ex)
		{
			await problems.WriteAsync(options.Run, bot, account, step, "timeout", ex.Message, ex);
			throw new LiveBotFailureException($"{bot} {step} '{action}' timed out.", ex);
		}
		catch (LiveBotFailureException)
		{
			throw;
		}
		catch (Exception ex)
		{
			await problems.WriteAsync(options.Run, bot, account, step, "step-failure", ex.Message, ex);
			throw;
		}
	}

	private static int? PlannedChannel(LiveBotOptions options, string scenarioId) => ScenarioIsolationPlanner.Plan(
		options.ScenarioDefinitions,
		ScenarioMode.Live,
		ScenarioTier.Soak,
		shardCount: 1,
		_ => TimeSpan.Zero)
		.Single(execution => execution.Scenario.Id == scenarioId)
		.Channel;

	private static string CharacterName(int index)
	{
		return BotIdentity.CharacterName(index);
	}

	private static T Get<T>(IReadOnlyDictionary<string, object?> fields, string name) =>
		fields.TryGetValue(name, out object? value) && value is T typed
			? typed
			: throw new InvalidDataException($"Decoded field '{name}' was missing or was not {typeof(T).Name}.");

	private static async Task WriteRunMetadataAsync(LiveBotOptions options, CancellationToken cancellationToken)
	{
		var metadata = new
		{
			run = options.Run,
			gitSha = options.GitSha,
			seed = options.Seed,
			virtualEpoch = (string?)null,
			timeZone = options.TimeZone,
			configProfile = options.Profile,
			scenarios = options.Scenarios,
			scenarioDefinitions = options.ScenarioDefinitions,
			bots = options.BotCount,
			loginEndPoint = options.LoginEndPoint.ToString(),
			gameEndPoint = options.GameEndPoint.ToString(),
			chatEndPoint = options.ChatEndPoint.ToString(),
			adminBaseUri = options.AdminBaseUri.ToString(),
			reentrySeconds = options.ReentryDelay.TotalSeconds,
			connectTimeoutSeconds = options.ConnectTimeout.TotalSeconds,
			stepTimeoutSeconds = options.StepTimeout.TotalSeconds,
			soakSeconds = options.Scenarios.Contains("SOAK") ? (int?)options.SoakSeconds : null,
			soakActivities = options.Scenarios.Contains("SOAK") ? options.SoakActivities.Select(activity => activity.ToString()).ToArray() : null,
		};
		await using var output = File.Create(Path.Combine(options.OutputDirectory, "bots-run.json"));
		await JsonSerializer.SerializeAsync(output, metadata, new JsonSerializerOptions
		{
			WriteIndented = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
		}, cancellationToken);
	}

	private sealed class L0Actor : IL0ScenarioActor, IAsyncDisposable
	{
		private readonly LiveBotOptions options;
		private readonly LiveBotProblemWriter problems;
		private int stepNumber;

		public L0Actor(LiveBotOptions options, LiveBotProblemWriter problems, int index,
			Race race = Race.ELYOS, string? bot = null, string? account = null, string? characterName = null)
		{
			this.options = options;
			this.problems = problems;
			Bot = bot ?? $"b{index:D2}";
			Account = account ?? $"{Bot}r{DateTimeOffset.Now:MMdd}";
			Trace = BotActionTraceWriter.Open(Path.Combine(options.OutputDirectory, "bots", $"{Bot}.trace.jsonl"),
				options.Run, Bot, Account);
			Session = new LiveBotSession(options, problems, Trace, Bot, Account,
				characterName ?? CharacterName(index), race);
		}

		public string Bot { get; }
		public string Account { get; }
		public BotActionTraceWriter Trace { get; }
		public LiveBotSession Session { get; }
		IL0ScenarioSession IL0ScenarioActor.Session => Session;
		public string LastStep => $"s{stepNumber:D2}";

		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
		{
			var step = $"s{++stepNumber:D2}";
			Session.BeginStep(step);
			return RunStepAsync(options, problems, Trace, Bot, Account, step, action, cancellationToken, operation);
		}

		Task IL0ScenarioActor.StepAsync(
			string action,
			Func<IL0ScenarioSession, CancellationToken, Task> operation,
			CancellationToken cancellationToken) =>
			StepAsync(action, token => operation(Session, token), cancellationToken);

		public async ValueTask DisposeAsync()
		{
			await Session.DisposeAsync();
			Trace.Dispose();
		}
	}
}

internal sealed partial class LiveBotSession : IL0ScenarioSession, IAsyncDisposable
{
	private const string Password = "aion-bots";
	private const string RegionChannel = "@\u0001public_ALL\u00011.0.AION.KOR";
	private readonly LiveBotOptions options;
	private readonly LiveBotProblemWriter problems;
	private readonly BotActionTraceWriter trace;
	private readonly string bot;
	private readonly string account;
	private string characterName;
	private readonly Race race;
	private readonly string macAddress;
	private readonly byte[] macBytes;
	private readonly SemaphoreSlim sendLock = new(1, 1);
	private readonly BotApi api = new();
	private TcpBotTransport? transport;
	private IAsyncEnumerator<DecodedBotServerPacket>? packets;
	private Task<bool>? activeMoveNext;
	private CancellationTokenSource? connectionLifetime;
	private Task? pingTask;
	private TcpClient? chatClient;
	private NetworkStream? chatStream;
	private ChatClientProtocol? chatProtocol;
	private string currentStep = "startup";
	private AionConnection.State state = AionConnection.State.CONNECTED;
	private bool quitExpected;
	private int accountId;
	private int loginOk;
	private int playOk1;
	private int playOk2;
	private int characterId;
	private int chatChannelId;
	private PersistedPosition? expectedPosition;
	private DateTimeOffset? persistedLastOnline;
	private BotPosition? currentPosition;
	private readonly List<DecodedBotServerPacket> packetHistory = [];

	public LiveBotSession(LiveBotOptions options, LiveBotProblemWriter problems, BotActionTraceWriter trace,
		string bot, string account, string characterName, Race race = Race.ELYOS)
	{
		this.options = options;
		this.problems = problems;
		this.trace = trace;
		this.bot = bot;
		this.account = account;
		this.characterName = characterName;
		this.race = race;
		bool director = string.Equals(account, LiveGmFacade.DirectorAccount, StringComparison.Ordinal);
		macBytes = BotIdentity.MacBytes(director ? 1 : BotIdentity.ParseSubjectNumber(bot), director);
		macAddress = BotIdentity.MacAddress(macBytes);
	}

	public void BeginStep(string step) => currentStep = step;
	public int CharacterId => characterId;
	public int ConnectionGeneration { get; private set; }
	public string CharacterName => characterName;
	public BotApi Api => api;
	public BotPosition CurrentPosition => currentPosition ?? api.World.Position
		?? throw new InvalidOperationException("The bot has not observed its position.");
	public IReadOnlyList<DecodedBotServerPacket> PacketHistory => packetHistory;

	/// <summary>Creates the LIVE setup facade after the seeded director has entered the game.</summary>
	public IGmFacade CreateGmFacade()
	{
		if (state != AionConnection.State.IN_GAME)
			throw new InvalidOperationException("The director must enter the world before executing GM commands.");
		return new LiveGmFacade(account, SendGameAsync, ReadNextAsync, api);
	}

	public LiveGmFacade CreateLiveGmFacade() => (LiveGmFacade)CreateGmFacade();

	public async Task ConnectAndReadKeyAsync(CancellationToken cancellationToken)
	{
		await OpenConnectionAsync(cancellationToken);
		AssertPacketType(await ReadNextAsync(cancellationToken), typeof(SM_KEY));
	}

	public async Task LoginAndAuthenticateAsync(CancellationToken cancellationToken)
	{
		var list = await LoginCharacterListAsync(cancellationToken);
		if (list.Get<byte>("characterCount") != 0)
			throw new InvalidDataException($"Fresh L0 account {account} unexpectedly already has a character.");
	}

	public async Task<DecodedBotServerPacket> LoginCharacterListAsync(CancellationToken cancellationToken)
	{
		await LoginServerAsync(cancellationToken);
		await OpenConnectionAsync(cancellationToken);
		quitExpected = false;
		AssertPacketType(await ReadNextAsync(cancellationToken), typeof(SM_KEY));
		await SendGameAsync(GameClientPackets.VersionCheck(207, 0, 65001, 10, 0, 2), cancellationToken);
		var version = await WaitForGamePacketAsync(typeof(SM_VERSION_CHECK), cancellationToken);
		if (version.Get<byte>("answerId") != 0)
			throw new InvalidDataException("Game server rejected client version 207.");
		await SendGameAsync(GameClientPackets.L2AuthLoginCheck(playOk2, playOk1, accountId, loginOk), cancellationToken);
		await SendGameAsync(GameClientPackets.MacAddress(macAddress, $"E2E-{bot.ToUpperInvariant()}"), cancellationToken);
		var auth = await WaitForGamePacketAsync(typeof(SM_L2AUTH_LOGIN_CHECK), cancellationToken);
		if (!auth.Get<bool>("ok"))
			throw new InvalidDataException("Game-server authentication failed.");
		state = AionConnection.State.AUTHED;
		return await ReadCharacterListAsync(cancellationToken);
	}

	public async Task<DecodedBotServerPacket> ReadCharacterListAsync(CancellationToken cancellationToken)
	{
		await SendGameAsync(api.ListCharacters(playOk2), cancellationToken);
		return await WaitForGamePacketAsync(typeof(SM_CHARACTER_LIST), cancellationToken);
	}

	public Task DeleteCharacterAsync(CancellationToken token) => SendGameAsync(GameClientPackets.DeleteCharacter(playOk2, characterId), token);
	public Task RestoreCharacterAsync(CancellationToken token) => SendGameAsync(GameClientPackets.RestoreCharacter(playOk2, characterId), token);

	public Task CreateCharacterAsync(CancellationToken cancellationToken) =>
		CreateCharacterAsync(cancellationToken, PlayerClass.WARRIOR);

	public Task CreateCharacterAsync(CancellationToken cancellationToken, PlayerClass playerClass) =>
		CreateCharacterAsync(characterName, playerClass, cancellationToken);

	public async Task CreateCharacterAsync(string name, PlayerClass playerClass, CancellationToken cancellationToken)
	{
		var creation = new CharacterCreationData
		{
			AccountId = accountId,
			AccountName = account,
			CharacterName = name,
			Gender = 0,
			Race = (int)race,
			PlayerClass = (int)playerClass,
			Height = 1,
		};
		await SendGameAsync(api.CreateCharacter(creation), cancellationToken);
		var response = await WaitForGamePacketAsync(typeof(SM_CREATE_CHARACTER), cancellationToken);
		if (response.Get<int>("responseCode") != 0)
			throw new InvalidDataException($"Character creation failed with response {response.Get<int>("responseCode")}.");
		var character = response.Get<IReadOnlyDictionary<string, object?>>("character");
		if (!string.Equals(Get<string>(character, "name"), name, StringComparison.Ordinal))
			throw new InvalidDataException("SM_CREATE_CHARACTER returned a different character name.");
		SelectCharacter(Get<int>(character, "objectId"), name);
	}

	public void SelectCharacter(int id, string name)
	{
		if (state != AionConnection.State.AUTHED) throw new InvalidOperationException("Character selection requires the selection screen.");
		characterId = id;
		characterName = name;
		expectedPosition = null;
		currentPosition = null;
		persistedLastOnline = null;
	}

	public async Task EnterWorldAsync(CancellationToken cancellationToken)
	{
		if (characterId == 0)
			throw new InvalidOperationException("Create the character before entering the world.");
		await SendGameAsync(api.EnterWorld(characterId), cancellationToken);
		await CompleteWorldEntryAsync(cancellationToken);
	}

	public async Task EditAndEnterAsync(CharacterCreationData data, CancellationToken token)
	{
		await SendGameAsync(GameClientPackets.EditCharacter(characterId, data), token);
		await CompleteWorldEntryAsync(token);
	}

	public async Task ReturnToSelectionAsync(bool editing, CancellationToken token)
	{
		await CloseChatAsync();
		quitExpected = true;
		try
		{
			await SendGameAsync(api.Quit(stayConnected: true), token);
			var response = await WaitForGamePacketAsync(typeof(SM_QUIT_RESPONSE), token);
			if (response.Get<int>("mode") != (editing ? 2 : 1)) throw new InvalidDataException("Unexpected quit destination.");
			state = AionConnection.State.AUTHED;
		}
		finally { quitExpected = false; }
	}

	public async Task CompleteWorldEntryAsync(CancellationToken cancellationToken)
	{
		state = AionConnection.State.IN_GAME;
		var spawn = await WaitForGamePacketAsync(typeof(SM_PLAYER_SPAWN), cancellationToken);
		expectedPosition = new PersistedPosition(spawn.Get<int>("worldId"), spawn.Get<float>("x"),
			spawn.Get<float>("y"), spawn.Get<float>("z"));
		currentPosition = new BotPosition(spawn.Get<float>("x"), spawn.Get<float>("y"),
			spawn.Get<float>("z"), spawn.Get<byte>("heading"));
		await WaitForGamePacketAsync(typeof(SM_PLAYER_INFO), cancellationToken);
	}

	public async Task ConnectChatAsync(CancellationToken cancellationToken)
	{
		await SendGameAsync(GameClientPackets.ChatAuth(characterId, macBytes), cancellationToken);
		var chatInit = await WaitForGamePacketAsync(typeof(SM_CHAT_INIT), cancellationToken);
		chatProtocol = ChatClientProtocol.FromChatInit(chatInit);
		chatClient = new TcpClient(options.ChatEndPoint.AddressFamily) { NoDelay = true };
		await chatClient.ConnectAsync(options.ChatEndPoint.Address, options.ChatEndPoint.Port, cancellationToken);
		chatStream = chatClient.GetStream();
		await WriteChatAsync(chatProtocol.CreateChatInitFrame(), "CM_CHAT_INI", cancellationToken);
		RequireChatOpcode(await ReadChatPayloadAsync(cancellationToken), 0x31, "SM_CHAT_INI");
		await WriteChatAsync(chatProtocol.CreatePlayerAuthFrame(characterId, account, characterName, RegionChannel),
			"CM_PLAYER_AUTH", cancellationToken);
		RequireChatOpcode(await ReadChatPayloadAsync(cancellationToken), 0x02, "SM_PLAYER_AUTH_RESPONSE");
		await WriteChatAsync(chatProtocol.CreateChannelRequestFrame(1, RegionChannel), "CM_CHANNEL_REQUEST", cancellationToken);
		var channelResponse = await ReadChatPayloadAsync(cancellationToken);
		RequireChatOpcode(channelResponse, 0x11, "SM_CHANNEL_RESPONSE");
		if (channelResponse.Length < 12 || BinaryPrimitives.ReadInt32LittleEndian(channelResponse.AsSpan(2, 4)) != 1)
			throw new InvalidDataException("Chat channel response did not match request 1.");
		chatChannelId = BinaryPrimitives.ReadInt32LittleEndian(channelResponse.AsSpan(8, 4));
		if (chatChannelId == 0)
			throw new InvalidDataException("Chat server returned channel id 0.");
	}

	public async Task ChangeChannelAsync(int channel, CancellationToken cancellationToken)
	{
		if (channel < 0)
			throw new ArgumentOutOfRangeException(nameof(channel));
		api.World.BeginWorldReload();
		await SendGameAsync(api.ChangeChannel(channel), cancellationToken);
		await WaitForGamePacketAsync(typeof(SM_CHANNEL_INFO), cancellationToken);
		DecodedBotServerPacket spawn = await WaitForGamePacketAsync(typeof(SM_PLAYER_SPAWN), cancellationToken);
		expectedPosition = new PersistedPosition(spawn.Get<int>("worldId"), spawn.Get<float>("x"),
			spawn.Get<float>("y"), spawn.Get<float>("z"));
		currentPosition = new BotPosition(spawn.Get<float>("x"), spawn.Get<float>("y"),
			spawn.Get<float>("z"), spawn.Get<byte>("heading"));
	}

	public Task<DecodedBotServerPacket> WaitForPacketAsync(Type packetType, CancellationToken cancellationToken,
		Func<DecodedBotServerPacket, bool>? predicate = null) =>
		WaitForGamePacketAsync(packetType, cancellationToken, predicate);

	public Task SendPacketAsync(BotClientPacket packet, CancellationToken cancellationToken) =>
		SendGameAsync(packet, cancellationToken);

	public async Task<int> WaitForNpcAsync(int templateId, CancellationToken cancellationToken)
	{
		return await WaitForNpcExceptAsync(templateId, new HashSet<int>(), cancellationToken);
	}

	public async Task<int> WaitForNpcExceptAsync(
		int templateId,
		IReadOnlySet<int> excludedObjectIds,
		CancellationToken cancellationToken)
	{
		BotPosition position = CurrentPosition;
		BotKnownObject? known = api.World.Objects.Values
			.Where(candidate => candidate.Kind == BotKnownObjectKind.Npc && candidate.TemplateId == templateId &&
				!excludedObjectIds.Contains(candidate.ObjectId))
			.OrderBy(candidate => DistanceSquared(position, candidate.Position))
			.FirstOrDefault();
		if (known != null)
			return known.ObjectId;
		DecodedBotServerPacket packet = await WaitForGamePacketAsync(typeof(SM_NPC_INFO), cancellationToken,
			candidate => candidate.Get<int>("npcId") == templateId &&
				!excludedObjectIds.Contains(candidate.Get<int>("objectId")));
		return packet.Get<int>("objectId");
	}

	public async Task MoveToNpcAsync(int objectId, CancellationToken cancellationToken)
	{
		if (!api.World.Objects.TryGetValue(objectId, out BotKnownObject? target))
			throw new InvalidOperationException($"NPC object {objectId} is not in the bot's known list.");
		BotPosition start = CurrentPosition;
		float speed = api.World.MovementSpeed
			?? throw new InvalidOperationException("SM_PLAYER_INFO did not provide movement speed.");
		IReadOnlyList<BotPosition> route = SegmentRoute(start, target.Position);
		if (Navigation is { } navigation)
		{
			int mapId = api.World.MapId ?? throw new InvalidOperationException("Bot has no observed map.");
			route = navigation.Graph.FindPath(mapId, start, target.Position);
			if (route.Count == 0) route = navigation.Geometry.FindLocalPath(mapId, start, target.Position);
			if (route.Count == 0) throw new InvalidOperationException($"No collision-checked route from {start} to {target.Position}.");
		}
		await ExecuteMovementAsync(new BotMover(api.World).CreateGroundPlan(route, start, speed),
			cancellationToken);
	}

	public (BotNavigationGraph Graph, BotNavigationGeometry Geometry)? Navigation { get; set; }

	public async Task ExecuteMovementAsync(BotMovementPlan plan, CancellationToken cancellationToken)
	{
		await BotMover.ExecuteAsync(plan,
			(packet, token) => new ValueTask(SendGameAsync(packet, token)),
			(delay, token) => new ValueTask(Task.Delay(delay, token)), cancellationToken);
		if (plan.Frames.Count > 0)
		{
			BotPosition position = plan.Frames[^1].Position;
			currentPosition = position;
			int mapId = api.World.MapId ?? throw new InvalidOperationException("The bot has not observed its map.");
			expectedPosition = new PersistedPosition(mapId, position.X, position.Y, position.Z);
		}
	}

	public async Task StartQuestAsync(int npcObjectId, int questId, CancellationToken cancellationToken)
	{
		await SendGameAsync(api.TalkTo(npcObjectId), cancellationToken);
		await WaitForGamePacketAsync(typeof(SM_DIALOG_WINDOW), cancellationToken,
			packet => packet.Get<int>("targetObjectId") == npcObjectId &&
				packet.Get<ushort>("dialogPageId") == 10 && packet.Get<int>("questId") == 0);
		await SendGameAsync(api.SelectDialog(npcObjectId, 31, questId: questId), cancellationToken);
		await WaitForGamePacketAsync(typeof(SM_DIALOG_WINDOW), cancellationToken,
			packet => packet.Get<int>("targetObjectId") == npcObjectId && packet.Get<int>("questId") == questId);
		await SendGameAsync(api.SelectDialog(npcObjectId, 1002, questId: questId), cancellationToken);
		await WaitForGamePacketAsync(typeof(SM_QUEST_ACTION), cancellationToken,
			packet => packet.Get<int>("questId") == questId);
		await WaitForGamePacketAsync(typeof(SM_DIALOG_WINDOW), cancellationToken,
			packet => packet.Get<int>("targetObjectId") == npcObjectId);
	}

	public async Task FinishQuestAsync(int npcObjectId, int questId, CancellationToken cancellationToken)
	{
		await SendGameAsync(api.TalkTo(npcObjectId), cancellationToken);
		await WaitForGamePacketAsync(typeof(SM_DIALOG_WINDOW), cancellationToken);
		await SendGameAsync(api.SelectDialog(npcObjectId, 31, questId: questId), cancellationToken);
		await WaitForGamePacketAsync(typeof(SM_DIALOG_WINDOW), cancellationToken);
		await SendGameAsync(api.SelectDialog(npcObjectId, 1009, questId: questId), cancellationToken);
		await WaitForGamePacketAsync(typeof(SM_DIALOG_WINDOW), cancellationToken);
		await SendGameAsync(api.SelectDialog(npcObjectId, 23, questId: questId), cancellationToken);
		await WaitForGamePacketAsync(typeof(SM_QUEST_ACTION), cancellationToken,
			packet => packet.Get<int>("questId") == questId && packet.Get<byte>("status") == 5);
	}

	public async Task WaitForPlayerClassAsync(PlayerClass playerClass, CancellationToken cancellationToken)
	{
		await WaitForPlayerClassAsync(characterId, playerClass, cancellationToken);
	}

	public async Task WaitForPlayerClassAsync(int objectId, PlayerClass playerClass, CancellationToken cancellationToken)
	{
		if (api.World.Objects.TryGetValue(objectId, out BotKnownObject? player) &&
			player.PlayerClass == playerClass.GetClassId())
			return;
		await WaitForGamePacketAsync(typeof(SM_PLAYER_INFO), cancellationToken,
			packet => packet.Get<int>("objectId") == objectId &&
				packet.Get<byte>("playerClass") == playerClass.GetClassId());
	}

	public async Task CompleteTeleportAsync(int expectedMapId, CancellationToken cancellationToken)
	{
		bool reloadMap = api.World.MapId != expectedMapId;
		if (reloadMap)
		{
			DecodedBotServerPacket spawn = await WaitForGamePacketAsync(typeof(SM_PLAYER_SPAWN), cancellationToken,
				packet => packet.Get<int>("worldId") == expectedMapId);
			expectedPosition = new PersistedPosition(expectedMapId, spawn.Get<float>("x"),
				spawn.Get<float>("y"), spawn.Get<float>("z"));
			await WaitForGamePacketAsync(typeof(SM_PLAYER_INFO), cancellationToken,
				packet => packet.Get<int>("objectId") == characterId);
			await WaitForGamePacketAsync(typeof(SM_CUBE_UPDATE), cancellationToken);
		}
		else
		{
			await WaitForGamePacketAsync(typeof(SM_CHANNEL_INFO), cancellationToken);
			await WaitForGamePacketAsync(typeof(SM_PLAYER_INFO), cancellationToken,
				packet => packet.Get<int>("objectId") == characterId);
			await WaitForGamePacketAsync(typeof(SM_ABNORMAL_STATE), cancellationToken);
		}
		BotPosition position = api.World.Position
			?? throw new InvalidOperationException("Teleport response did not provide a destination.");
		currentPosition = position;
		expectedPosition = new PersistedPosition(expectedMapId, position.X, position.Y, position.Z);
	}

	private static IReadOnlyList<BotPosition> SegmentRoute(BotPosition start, BotPosition destination)
	{
		float distance = MathF.Sqrt(
			MathF.Pow(destination.X - start.X, 2) + MathF.Pow(destination.Y - start.Y, 2) +
			MathF.Pow(destination.Z - start.Z, 2));
		int segments = Math.Max(1, (int)MathF.Ceiling(distance / 15f));
		return Enumerable.Range(1, segments)
			.Select(index => new BotPosition(
				start.X + (destination.X - start.X) * index / segments,
				start.Y + (destination.Y - start.Y) * index / segments,
				start.Z + (destination.Z - start.Z) * index / segments,
				destination.Heading))
			.ToArray();
	}

	public Task SendChatMessageAsync(string message, CancellationToken cancellationToken)
	{
		if (chatProtocol == null || chatChannelId == 0)
			throw new InvalidOperationException("Join the region channel before sending a message.");
		return WriteChatAsync(chatProtocol.CreateChannelMessageFrame(chatChannelId, message), "CM_CHANNEL_MESSAGE", cancellationToken);
	}

	public async Task ReceiveChatMessageAsync(string expected, CancellationToken cancellationToken)
	{
		while (true)
		{
			var payload = await ReadChatPayloadAsync(cancellationToken);
			if (payload[0] != 0x1A)
				continue;
			var actual = ExtractChannelMessageText(payload);
			trace.WriteAction(currentStep, "SM_CHANNEL_MESSAGE", new Dictionary<string, object?> { ["message"] = actual });
			if (!string.Equals(actual, expected, StringComparison.Ordinal))
				throw new InvalidDataException($"Expected channel message '{expected}', received '{actual}'.");
			return;
		}
	}

	public async Task WalkTenMetersAsync(CancellationToken cancellationToken)
	{
		var start = expectedPosition ?? throw new InvalidOperationException("Enter the world before moving.");
		var target = start with { X = start.X + 10f };
		await SendGameAsync(api.MoveTo(new MovementPacketData(start.X, start.Y, start.Z, 0,
			MovementMask.POSITION | MovementMask.MANUAL | MovementMask.ABSOLUTE,
			X2: target.X, Y2: target.Y, Z2: target.Z)), cancellationToken);
		await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
		await SendGameAsync(api.MoveTo(new MovementPacketData(target.X, target.Y, target.Z, 0, MovementMask.IMMEDIATE)), cancellationToken);
		// Java CM_MOVE broadcasts SM_MOVE to sighted players, excluding the sender. Persistence is asserted after
		// CM_QUIT and again from SM_CHARACTER_LIST on relogin, so those are the authoritative L0 movement oracles.
		expectedPosition = target;
	}

	public async Task PingAsync(CancellationToken cancellationToken)
	{
		await SendGameAsync(GameClientPackets.Ping(), cancellationToken);
		await WaitForGamePacketAsync(typeof(SM_PONG), cancellationToken);
	}

	public async Task SynchronizeAsync(CancellationToken token)
	{
		await SendGameAsync(GameClientPackets.TimeCheck(unchecked((int)Environment.TickCount64)), token);
		await WaitForGamePacketAsync(typeof(SM_TIME_CHECK), token);
	}

	public async Task SendFriendStatusCanaryAsync(CancellationToken cancellationToken)
	{
		await SendGameAsync(GameClientPackets.FriendStatus(2), cancellationToken);
		await WaitForGamePacketAsync(typeof(SM_FRIEND_STATUS), cancellationToken);
	}

	public async Task SendEmotionCanaryAsync(CancellationToken cancellationToken)
	{
		// Give the 100 ms watcher poll loop time to ingest this step before the read-path log arrives.
		await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
		await SendGameAsync(GameClientPackets.Emotion(0xFF), cancellationToken);
		await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
	}

	public async Task SendBadLoginChecksumCanaryAsync(CancellationToken cancellationToken)
	{
		using var client = new TcpClient(options.LoginEndPoint.AddressFamily) { NoDelay = true };
		await client.ConnectAsync(options.LoginEndPoint.Address, options.LoginEndPoint.Port, cancellationToken);
		var stream = client.GetStream();
		var protocol = await LoginClientProtocol.ReadInitAsync(stream, cancellationToken);
		var frame = protocol.Crypto.CreateAuthGameGuardFrame(protocol.Init.SessionId);
		frame[^1] ^= 0x01;
		await stream.WriteAsync(frame, cancellationToken);
		var closeProbe = new byte[1];
		if (await stream.ReadAsync(closeProbe, cancellationToken) != 0)
			throw new InvalidDataException("Login checksum canary was not rejected by closing its connection.");
	}

	public async Task SendUnknownChatOpcodeCanaryAsync(CancellationToken cancellationToken)
	{
		using var client = new TcpClient(options.ChatEndPoint.AddressFamily) { NoDelay = true };
		await client.ConnectAsync(options.ChatEndPoint.Address, options.ChatEndPoint.Port, cancellationToken);
		var frame = ChatPacketFrameCodec.CreateFrame([0x7E]);
		await client.GetStream().WriteAsync(frame, cancellationToken);
		await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
	}

	public async Task QuitAsync(CancellationToken cancellationToken)
	{
		quitExpected = true;
		await CloseChatAsync();
		await SendGameAsync(api.Quit(stayConnected: false), cancellationToken);
		await WaitForGamePacketAsync(typeof(SM_QUIT_RESPONSE), cancellationToken);
		await CloseConnectionAsync(cancellationToken);
		quitExpected = false;
	}

	public async Task VerifyOfflineAsync(CancellationToken cancellationToken)
	{
		using var client = new HttpClient { BaseAddress = options.AdminBaseUri };
		using var request = new HttpRequestMessage(HttpMethod.Get,
			$"admin/player-state?characterName={Uri.EscapeDataString(characterName)}");
		request.Headers.Add("X-Admin-Token", options.AdminToken);
		using var response = await client.SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();
		await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
		using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
		var root = document.RootElement;
		if (root.GetProperty("online").GetBoolean())
			throw new InvalidDataException($"Admin player-state still reports {characterName} online after CM_QUIT.");
		var lastKnown = root.GetProperty("lastKnown");
		AssertPersistedPosition(lastKnown.GetProperty("worldId").GetInt32(), lastKnown.GetProperty("x").GetSingle(),
			lastKnown.GetProperty("y").GetSingle(), lastKnown.GetProperty("z").GetSingle());
		persistedLastOnline = lastKnown.GetProperty("lastOnline").GetDateTimeOffset();
	}

	public async Task WaitForReentryAsync(CancellationToken cancellationToken)
	{
		TimeSpan remaining = api.Timing.TimeUntilEnterWorld();
		if (persistedLastOnline is DateTimeOffset lastOnline)
		{
			TimeSpan serverRemaining = lastOnline + TimeSpan.FromSeconds(BotTimingContract.ConfiguredReentrySeconds) -
				DateTimeOffset.UtcNow;
			if (serverRemaining > remaining)
				remaining = serverRemaining;
		}
		if (remaining > TimeSpan.Zero)
			await Task.Delay(remaining + TimeSpan.FromMilliseconds(500), cancellationToken);
	}

	public async Task ReloginAndVerifyPersistenceAsync(CancellationToken cancellationToken)
	{
		var list = await LoginCharacterListAsync(cancellationToken);
		var characters = list.Get<List<IReadOnlyDictionary<string, object?>>>("characters");
		var character = characters.SingleOrDefault(entry => string.Equals(Get<string>(entry, "name"), characterName, StringComparison.Ordinal))
			?? throw new InvalidDataException($"Character list did not contain {characterName} after relogin.");
		if (Get<int>(character, "objectId") != characterId)
			throw new InvalidDataException("Character object id changed after relogin.");
		AssertPersistedPosition(Get<int>(character, "mapId"), Get<float>(character, "x"),
			Get<float>(character, "y"), Get<float>(character, "z"));
	}

	public Task CloseAsync(CancellationToken cancellationToken)
	{
		quitExpected = true;
		return CloseConnectionAsync(cancellationToken);
	}

	public async ValueTask DisposeAsync()
	{
		quitExpected = true;
		await CloseChatAsync();
		await CloseConnectionAsync(CancellationToken.None);
		sendLock.Dispose();
	}

	private async Task LoginServerAsync(CancellationToken cancellationToken)
	{
		using var client = new TcpClient(options.LoginEndPoint.AddressFamily) { NoDelay = true };
		await client.ConnectAsync(options.LoginEndPoint.Address, options.LoginEndPoint.Port, cancellationToken);
		var stream = client.GetStream();
		var login = await api.Login(stream, account, Password, cancellationToken);
		accountId = login.Result.AccountId;
		loginOk = login.Result.LoginOk;
		await stream.WriteAsync(login.Protocol.Crypto.CreateServerListFrame(accountId, loginOk), cancellationToken);
		var serverList = login.Protocol.Crypto.DecryptServerFrame(await LoginClientProtocol.ReadFrameAsync(stream, cancellationToken));
		if (serverList.Length < 19 || serverList[0] != 0x04 || serverList[1] == 0 || serverList[3] != 1 || serverList[18] != 1)
			throw new InvalidDataException("Login server did not advertise online game server 1.");
		await stream.WriteAsync(login.Protocol.Crypto.CreatePlayFrame(accountId, loginOk, 1), cancellationToken);
		var play = login.Protocol.Crypto.DecryptServerFrame(await LoginClientProtocol.ReadFrameAsync(stream, cancellationToken));
		if (play.Length < 10 || play[0] != 0x07 || play[9] != 1)
			throw new InvalidDataException("Login server did not return SM_PLAY_OK for game server 1.");
		playOk1 = BinaryPrimitives.ReadInt32LittleEndian(play.AsSpan(1, 4));
		playOk2 = BinaryPrimitives.ReadInt32LittleEndian(play.AsSpan(5, 4));
	}

	private async Task SendGameAsync(BotClientPacket packet, CancellationToken cancellationToken)
	{
		var activeTransport = transport ?? throw new InvalidOperationException("The game connection is not open.");
		await sendLock.WaitAsync(cancellationToken);
		try
		{
			trace.WriteSent(currentStep, packet);
			await activeTransport.SendAsync(activeTransport.Codec.EncodeClientFrame(packet, state), cancellationToken);
		}
		finally
		{
			sendLock.Release();
		}
	}

	public Task<DecodedBotServerPacket> WaitForAnyPacketAsync(Func<DecodedBotServerPacket, bool> predicate,
		CancellationToken token) => WaitForGamePacketAsync(null, token, predicate);

	private async Task<DecodedBotServerPacket> WaitForGamePacketAsync(Type? packetType, CancellationToken cancellationToken,
		Func<DecodedBotServerPacket, bool>? predicate = null)
	{
		while (true)
		{
			var packet = await ReadNextAsync(cancellationToken);
			var response = api.Observe(packet);
			if (response != null)
				await SendGameAsync(response, cancellationToken);
			if ((packetType == null || packet.PacketType == packetType) && (predicate == null || predicate(packet)))
				return packet;
		}
	}

	private async Task<DecodedBotServerPacket> ReadNextAsync(CancellationToken cancellationToken)
	{
		try
		{
			if (packets == null)
				throw new EndOfStreamException("Game transport ended before the expected packet.");
			var moveNext = packets.MoveNextAsync().AsTask();
			activeMoveNext = moveNext;
			if (!await moveNext.WaitAsync(cancellationToken))
				throw new EndOfStreamException("Game transport ended before the expected packet.");
			activeMoveNext = null;
			var packet = packets.Current;
			packetHistory.Add(packet);
			TrimHistory();
			trace.WriteReceived(currentStep, packet);
			if (packet.PacketType == typeof(SM_ENTER_WORLD_CHECK) && packet.Get<byte>("msg") != 0)
				throw new LiveBotFailureException($"SM_ENTER_WORLD_CHECK refused entry with message {packet.Get<byte>("msg")}.");
			if (packet.PacketType == typeof(SM_QUIT_RESPONSE) && !quitExpected)
			{
				await problems.WriteAsync(options.Run, bot, account, currentStep, "unexpected-quit-response",
					"Received SM_QUIT_RESPONSE before the scenario requested quit.");
				throw new LiveBotFailureException("Unexpected SM_QUIT_RESPONSE.");
			}
			return packet;
		}
		catch (Exception ex) when (ex is EndOfStreamException or IOException or SocketException)
		{
			if (quitExpected)
				throw;
			await AttemptReconnectAsync(ex, cancellationToken);
			throw new LiveBotFailureException("Game connection ended unexpectedly.", ex);
		}
	}

	private async Task AttemptReconnectAsync(Exception failure, CancellationToken cancellationToken)
	{
		await problems.WriteAsync(options.Run, bot, account, currentStep, "unexpected-disconnect", failure.Message, failure);
		try
		{
			await CloseConnectionAsync(CancellationToken.None);
			await OpenConnectionAsync(cancellationToken);
			await problems.WriteAsync(options.Run, bot, account, currentStep, "automatic-reconnect",
				"Automatically reconnected after an unexpected disconnect; the run still fails.");
		}
		catch (Exception reconnectFailure)
		{
			await problems.WriteAsync(options.Run, bot, account, currentStep, "automatic-reconnect",
				"Automatic reconnect failed; the run still fails.", reconnectFailure);
		}
	}

	private async Task OpenConnectionAsync(CancellationToken cancellationToken)
	{
		if (transport != null)
			throw new InvalidOperationException("The bot already has an open game connection.");
		using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		connectTimeout.CancelAfter(options.ConnectTimeout);
		try
		{
			transport = await TcpBotTransport.ConnectAsync(options.GameEndPoint, connectTimeout.Token);
			ConnectionGeneration++;
		}
		catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && connectTimeout.IsCancellationRequested)
		{
			throw new TimeoutException($"Connecting to {options.GameEndPoint} exceeded {options.ConnectTimeout.TotalSeconds:n0} seconds.", ex);
		}
		connectionLifetime = new CancellationTokenSource();
		packets = transport.ReceiveAsync(connectionLifetime.Token).GetAsyncEnumerator(connectionLifetime.Token);
		pingTask = RunPingLoopAsync(connectionLifetime.Token);
	}

	private async Task CloseConnectionAsync(CancellationToken cancellationToken)
	{
		if (transport == null && connectionLifetime == null)
			return;
		if (transport != null)
			await transport.CloseAsync(cancellationToken);
		if (connectionLifetime != null)
			await connectionLifetime.CancelAsync();
		if (activeMoveNext != null)
		{
			try { await activeMoveNext; }
			catch (OperationCanceledException) when (connectionLifetime?.IsCancellationRequested == true) { }
			catch (ObjectDisposedException) when (connectionLifetime?.IsCancellationRequested == true) { }
			activeMoveNext = null;
		}
		if (pingTask != null)
		{
			try { await pingTask; }
			catch (OperationCanceledException) when (connectionLifetime?.IsCancellationRequested == true) { }
		}
		if (packets != null)
			await packets.DisposeAsync();
		if (transport != null)
			await transport.DisposeAsync();
		connectionLifetime?.Dispose();
		connectionLifetime = null;
		packets = null;
		activeMoveNext = null;
		pingTask = null;
		transport = null;
		state = AionConnection.State.CONNECTED;
	}

	private async Task RunPingLoopAsync(CancellationToken cancellationToken)
	{
		var scheduler = new LiveBotPingScheduler();
		while (!cancellationToken.IsCancellationRequested)
		{
			var delay = scheduler.NextDueAt - DateTimeOffset.UtcNow;
			if (delay > TimeSpan.Zero)
				await Task.Delay(delay, cancellationToken);
			var ping = scheduler.Poll();
			if (ping != null && state == AionConnection.State.IN_GAME && transport != null)
				await SendGameAsync(ping, cancellationToken);
		}
	}

	private async Task WriteChatAsync(byte[] frame, string packet, CancellationToken cancellationToken)
	{
		var stream = chatStream ?? throw new InvalidOperationException("The chat connection is not open.");
		trace.WriteAction(currentStep, packet, new Dictionary<string, object?> { ["frameLength"] = frame.Length });
		await stream.WriteAsync(frame, cancellationToken);
		await stream.FlushAsync(cancellationToken);
	}

	private async Task<byte[]> ReadChatPayloadAsync(CancellationToken cancellationToken)
	{
		var stream = chatStream ?? throw new InvalidOperationException("The chat connection is not open.");
		var header = await ReadExactAsync(stream, sizeof(ushort), cancellationToken);
		var length = BinaryPrimitives.ReadUInt16LittleEndian(header);
		if (length < 3)
			throw new InvalidDataException($"Invalid chat frame length {length}.");
		var payload = await ReadExactAsync(stream, length - sizeof(ushort), cancellationToken);
		trace.WriteAction(currentStep, $"chat:0x{payload[0]:X2}",
			new Dictionary<string, object?> { ["payloadHex"] = Convert.ToHexString(payload) });
		return payload;
	}

	private Task CloseChatAsync()
	{
		if (chatClient == null)
			return Task.CompletedTask;
		try { chatClient.Client.Shutdown(SocketShutdown.Both); }
		catch (SocketException) { }
		chatClient.Dispose();
		chatClient = null;
		chatStream = null;
		chatProtocol = null;
		chatChannelId = 0;
		return Task.CompletedTask;
	}

	private static async Task<byte[]> ReadExactAsync(Stream stream, int length, CancellationToken cancellationToken)
	{
		var buffer = new byte[length];
		var offset = 0;
		while (offset < length)
		{
			var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
			if (read == 0)
				throw new EndOfStreamException("Socket closed in the middle of a frame.");
			offset += read;
		}
		return buffer;
	}

	private void AssertPersistedPosition(int worldId, float x, float y, float z)
	{
		var expected = expectedPosition ?? throw new InvalidOperationException("No expected persisted position was recorded.");
		if (worldId != expected.WorldId || Math.Abs(x - expected.X) > 0.1f || Math.Abs(y - expected.Y) > 0.1f || Math.Abs(z - expected.Z) > 0.1f)
			throw new InvalidDataException($"Persisted position mismatch: expected {expected.WorldId} ({expected.X:n2},{expected.Y:n2},{expected.Z:n2}), received {worldId} ({x:n2},{y:n2},{z:n2}).");
	}

	private static void RequireChatOpcode(byte[] payload, byte opcode, string packet)
	{
		if (payload.Length == 0 || payload[0] != opcode)
			throw new InvalidDataException($"Expected {packet} opcode 0x{opcode:X2}, received {(payload.Length == 0 ? "empty payload" : $"0x{payload[0]:X2}")}.");
	}

	private static string ExtractChannelMessageText(byte[] payload)
	{
		var offset = 1 + 1 + (5 * sizeof(int)) + 1;
		if (payload.Length < offset + sizeof(ushort))
			throw new InvalidDataException("SM_CHANNEL_MESSAGE is truncated before its sender identifier.");
		var identifierChars = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset, 2));
		offset += sizeof(ushort) + (identifierChars * sizeof(char));
		if (payload.Length < offset + sizeof(ushort))
			throw new InvalidDataException("SM_CHANNEL_MESSAGE is truncated before its text.");
		var textChars = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset, 2));
		offset += sizeof(ushort);
		if (payload.Length < offset + (textChars * sizeof(char)))
			throw new InvalidDataException("SM_CHANNEL_MESSAGE text is truncated.");
		return Encoding.Unicode.GetString(payload.AsSpan(offset, textChars * sizeof(char)));
	}

	private static void AssertPacketType(DecodedBotServerPacket packet, Type expected)
	{
		if (packet.PacketType != expected)
			throw new InvalidDataException($"Expected {expected.Name}, received {packet.PacketType.Name}.");
	}

	private static T Get<T>(IReadOnlyDictionary<string, object?> fields, string name) =>
		fields.TryGetValue(name, out var value) && value is T typed
			? typed
			: throw new InvalidDataException($"Decoded field '{name}' was missing or was not {typeof(T).Name}.");

	private sealed record PersistedPosition(int WorldId, float X, float Y, float Z);
}

internal sealed class LiveBotFailureException : Exception
{
	public LiveBotFailureException(string message, Exception? innerException = null) : base(message, innerException) { }
}
