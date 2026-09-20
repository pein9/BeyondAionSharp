using Aion.Bots.Api;
using Aion.Bots.Gm;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static async Task<int> RunS7Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		var actors = Enumerable.Range(0, 2).Select(i => new L0Actor(options, problems, i + 1, Race.ELYOS,
			characterName: $"Aeliveextended{(char)('a' + i)}")).ToArray();
		await using var director = new L0Actor(options, problems, 99, Race.ELYOS,
			bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		try
		{
			foreach (var actor in actors.Append(director))
			{
				actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "S7" });
				await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, token);
				await actor.StepAsync("create-character", ct => actor.Session.CreateCharacterAsync(ct,
					actor == director ? PlayerClass.WARRIOR : PlayerClass.MAGE), token);
				await actor.StepAsync("enter-world", actor.Session.EnterWorldAsync, token);
			}
			var gm = director.Session.CreateLiveGmFacade();
			foreach (var actor in actors)
			{
				var point = SocialBasicsScenario.Registrar;
				await MoveSubjectWithDirectorAsync(director, actor, gm, ExtendedSocialScenario.MapId, point.X - 5, point.Y, point.Z, "setup-extended-social-position", token);
				await actor.StepAsync("director-prepare-spiritmaster-level-funds-reagents", async ct =>
				{
					await gm.ExecuteVerifiedAsync(new GmCommand("set", ["class", "spirit_master"], "replyless class change"),
						new GmCommand("set", ["level", "23"], "level to 23"), new GmSubject(actor.Session.CharacterId, actor.Session.CharacterName), ct);
					await gm.ExecuteAsync(new GmCommand("add", [actor.Session.CharacterName, "182400001", ExtendedSocialScenario.SetupKinah.ToString(System.Globalization.CultureInfo.InvariantCulture)], "You gave"), cancellationToken: ct);
					await gm.ExecuteAsync(new GmCommand("add", [actor.Session.CharacterName, "169300011", "2"], "You gave"), cancellationToken: ct);
				}, token);
			}
			await ExtendedSocialScenario.RunAsync(actors.Select(a => (IExtendedSocialDriver)new LiveSocialDriver(a)).ToArray(), "LiveExtended",
				ct => director.StepAsync("director-setup-legion-level-two", inner => gm.ExecuteAsync(
					new GmCommand("legion", ["setlevel", "LiveExtended", "2"], "level was changed from 1 to 2"), cancellationToken: inner), ct),
				async ct =>
				{
					var point = ExtendedSocialScenario.WarehousePoint;
					foreach (var actor in actors)
						await MoveSubjectWithDirectorAsync(director, actor, gm, ExtendedSocialScenario.MapId, point.X - 3, point.Y, point.Z, "director-setup-warehouse-position", ct);
				}, token);
			foreach (var actor in actors.Append(director))
			{
				await actor.StepAsync("quit", actor.Session.QuitAsync, token);
				actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "S7" });
			}
			Console.WriteLine("LIVE S7 group-finder, real recall and legion emblem/history/kinah passed.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception) { Console.Error.WriteLine($"S7 failed: {exception}"); return 1; }
		finally { foreach (var actor in actors) await actor.DisposeAsync(); }
	}

	private static async Task<int> RunS6Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		var actors = Enumerable.Range(0, 2).Select(i => new L0Actor(options, problems, i + 1, Race.ELYOS,
			characterName: $"Aelivegrpquest{(char)('a' + i)}")).ToArray();
		await using var director = new L0Actor(options, problems, 99, Race.ELYOS,
			bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		try
		{
			foreach (var actor in actors.Append(director))
			{
				actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "S6" });
				await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, token);
				await actor.StepAsync("create-character", actor.Session.CreateCharacterAsync, token);
				await actor.StepAsync("enter-world", actor.Session.EnterWorldAsync, token);
			}
			var gm = director.Session.CreateLiveGmFacade();
			var point = GroupQuestScenario.GiverPoint;
			foreach (var actor in actors)
			{
				await MoveSubjectWithDirectorAsync(director, actor, gm, GroupQuestScenario.MapId, point.X - 2, point.Y, point.Z, "setup-group-quest-position", token);
				await director.StepAsync("setup-group-quest-level", ct => gm.ExecuteAsync(new GmCommand("set", ["level", "9"], "level to 9"),
					new GmSubject(actor.Session.CharacterId, actor.Session.CharacterName), ct), token);
			}
			await director.StepAsync("director-move-to-quest-spawn-point", ct => director.Session.MoveToPositionAsync(GroupQuestScenario.SpawnPoint, ct), token);
			await GroupQuestScenario.RunAsync(actors.Select(actor => (IGroupQuestDriver)new LiveSocialDriver(actor)).ToArray(),
				(npcId, ct) => director.StepAsync("director-spawn-temporary-quest-target", async inner =>
				{
					await gm.ExecuteVerifiedAsync(new GmCommand("spawn", [npcId.ToString(System.Globalization.CultureInfo.InvariantCulture)], "replyless temporary spawn"),
						new GmCommand("coords", [], "'s position:"), cancellationToken: inner);
				}, ct), token);
			foreach (var actor in actors.Append(director))
			{
				await actor.StepAsync("quit", actor.Session.QuitAsync, token);
				actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "S6" });
			}
			Console.WriteLine("LIVE S6 quest sharing, ten group-credit kills and both quest turn-ins passed.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception) { Console.Error.WriteLine($"S6 failed: {exception}"); return 1; }
		finally { foreach (var actor in actors) await actor.DisposeAsync(); }
	}

	private static async Task<int> RunS5Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		var actors = Enumerable.Range(0, 3).Select(i => new L0Actor(options, problems, i + 1, Race.ASMODIANS,
			characterName: $"Aslivegrouploot{(char)('a' + i)}")).ToArray();
		await using var director = new L0Actor(options, problems, 99, Race.ASMODIANS,
			bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		try
		{
			foreach (var actor in actors.Append(director))
			{
				actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "S5" });
				await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, token);
				await actor.StepAsync("create-character", actor.Session.CreateCharacterAsync, token);
				await actor.StepAsync("enter-world", actor.Session.EnterWorldAsync, token);
			}
			var gm = director.Session.CreateLiveGmFacade();
			var point = GroupLootScenario.SpawnPoint;
			foreach (var actor in actors)
			{
				await MoveSubjectWithDirectorAsync(director, actor, gm, GroupLootScenario.MapId, point.X - 2, point.Y, point.Z, "setup-loot-position", token);
				await director.StepAsync("setup-loot-subject-level", ct => gm.ExecuteAsync(new GmCommand("set", ["level", "2"], "level to 2"),
					new GmSubject(actor.Session.CharacterId, actor.Session.CharacterName), ct), token);
			}
			await director.StepAsync("director-move-to-spawn-point", ct => director.Session.MoveToPositionAsync(point, ct), token);
			await GroupLootScenario.RunAsync(actors.Select(actor => (IGroupLootDriver)new LiveSocialDriver(actor)).ToArray(),
				ct => director.StepAsync("director-spawn-temporary-loot-target", async inner =>
				{
					await gm.ExecuteVerifiedAsync(new GmCommand("spawn", [GroupLootScenario.NpcId.ToString(System.Globalization.CultureInfo.InvariantCulture)], "replyless temporary spawn"),
						new GmCommand("coords", [], "'s position:"), cancellationToken: inner);
				}, ct), token);
			foreach (var actor in actors.Append(director))
			{
				await actor.StepAsync("quit", actor.Session.QuitAsync, token);
				actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "S5" });
			}
			Console.WriteLine("LIVE S5 free-for-all, round-robin, leader-only and three-player roll conservation passed.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception) { Console.Error.WriteLine($"S5 failed: {exception}"); return 1; }
		finally { foreach (var actor in actors) await actor.DisposeAsync(); }
	}

	private static async Task<int> RunS4Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		var actors = Enumerable.Range(0, 8).Select(i => new L0Actor(options, problems, i + 1, Race.ELYOS,
			characterName: $"Aelivealliance{(char)('a' + i)}")).ToArray();
		try
		{
			foreach (var actor in actors)
			{
				actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "S4" });
				await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, token);
				await actor.StepAsync("create-character", actor.Session.CreateCharacterAsync, token);
				await actor.StepAsync("enter-world", actor.Session.EnterWorldAsync, token);
			}
			await AllianceLeagueScenario.RunAsync(actors.Select(actor => (IAllianceLeagueDriver)new LiveSocialDriver(actor)).ToArray(), token);
			foreach (var actor in actors)
			{
				await actor.StepAsync("quit", actor.Session.QuitAsync, token);
				actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "S4" });
			}
			Console.WriteLine("LIVE S4 groups, alliance conversion, captain changes and league formation/cleanup passed.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception) { Console.Error.WriteLine($"S4 failed: {exception}"); return 1; }
		finally { foreach (var actor in actors) await actor.DisposeAsync(); }
	}

	private static async Task<int> RunS3Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		await using var first = new L0Actor(options, problems, 1, Race.ELYOS, characterName: "Aelivefrienda");
		await using var second = new L0Actor(options, problems, 2, Race.ELYOS, characterName: "Aelivefriendb");
		try
		{
			foreach (var actor in new[] { first, second })
			{
				actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "S3" });
				await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, token);
				await actor.StepAsync("create-character", actor.Session.CreateCharacterAsync, token);
				await actor.StepAsync("enter-world", actor.Session.EnterWorldAsync, token);
			}
			await FriendsAndBlocksScenario.RunAsync(new LiveSocialDriver(first), new LiveSocialDriver(second), token);
			foreach (var actor in new[] { first, second })
			{
				await actor.StepAsync("quit", actor.Session.QuitAsync, token);
				actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "S3" });
			}
			Console.WriteLine("LIVE S3 friendship, memos, relog notifications and persistent block lists passed.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception) { Console.Error.WriteLine($"S3 failed: {exception}"); return 1; }
	}

	private static async Task<int> RunS2Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		await using var elyos = new L0Actor(options, problems, 1, Race.ELYOS, characterName: "Aelivepvp");
		await using var asmodian = new L0Actor(options, problems, 2, Race.ASMODIANS, characterName: "Aslivepvp");
		await using var director = new L0Actor(options, problems, 99, Race.ELYOS,
			bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		try
		{
			foreach (var actor in new[] { elyos, asmodian, director })
			{
				actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "S2" });
				await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, token);
				await actor.StepAsync("create-character", ct => actor.Session.CreateCharacterAsync(ct,
					actor == director ? PlayerClass.WARRIOR : PlayerClass.MAGE), token);
				await actor.StepAsync("enter-world", actor.Session.EnterWorldAsync, token);
			}
			var gm = director.Session.CreateLiveGmFacade();
			foreach (var (actor, point) in new[] { (elyos, PvpFlightScenario.ElyosStart), (asmodian, PvpFlightScenario.AsmodianStart) })
			{
				await MoveSubjectWithDirectorAsync(director, actor, gm, PvpFlightScenario.MapId, point.X, point.Y, point.Z, "setup-pvp-flight-position", token);
				await actor.StepAsync("prepare-pvp-level-and-abyss-points", async ct =>
				{
					var subject = new GmSubject(actor.Session.CharacterId, actor.Session.CharacterName);
					await gm.ExecuteVerifiedAsync(new GmCommand("set", ["class", "sorcerer"], "replyless class change"),
						new GmCommand("set", ["level", "10"], "level to 10"), subject, ct);
					await gm.ExecuteAsync(new GmCommand("set", ["ap", "500"], "abyss points to 500"), subject, ct);
				}, token);
			}
			await PvpFlightScenario.RunAsync(new LiveSocialDriver(elyos), new LiveSocialDriver(asmodian), token);
			foreach (var actor in new[] { elyos, asmodian, director })
			{
				await actor.StepAsync("quit", actor.Session.QuitAsync, token);
				actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "S2" });
			}
			Console.WriteLine("LIVE S2 flight, cross-race PvP death and exact abyss-point rewards passed.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception) { Console.Error.WriteLine($"S2 failed: {exception}"); return 1; }
	}

	private static async Task<int> RunS1Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		await using var first = new L0Actor(options, problems, 1, Race.ELYOS, characterName: "Aelivesociala");
		await using var second = new L0Actor(options, problems, 2, Race.ELYOS, characterName: "Aelivesocialb");
		await using var director = new L0Actor(options, problems, 99, Race.ELYOS,
			bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		try
		{
			foreach (var actor in new[] { first, second, director })
			{
				actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "S1" });
				await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, token);
				await actor.StepAsync("create-character", ct => actor.Session.CreateCharacterAsync(ct,
					actor == director ? PlayerClass.WARRIOR : PlayerClass.MAGE), token);
				await actor.StepAsync("enter-world", actor.Session.EnterWorldAsync, token);
			}
			var gm = director.Session.CreateLiveGmFacade();
			int offset = 0;
			foreach (var actor in new[] { first, second })
			{
				var point = SocialBasicsScenario.Registrar;
				await MoveSubjectWithDirectorAsync(director, actor, gm, 110010000, point.X - 5 - offset, point.Y, point.Z, "setup-social-position", token);
				await actor.StepAsync("prepare-social-level-and-kinah", async ct =>
				{
					await gm.ExecuteVerifiedAsync(new GmCommand("set", ["class", "sorcerer"], "replyless class change"),
						new GmCommand("set", ["level", "10"], "level to 10"),
						new GmSubject(actor.Session.CharacterId, actor.Session.CharacterName), ct);
					await gm.ExecuteAsync(new GmCommand("add", [actor.Session.CharacterName, "182400001", "20000"], "You gave"), cancellationToken: ct);
					await actor.Session.MoveToPositionAsync(point with { X = point.X - 4 - offset }, ct);
				}, token);
				offset++;
			}
			await SocialBasicsScenario.RunAsync(new LiveSocialDriver(first), new LiveSocialDriver(second), "LiveSocial", token);
			foreach (var actor in new[] { first, second, director })
			{
				await actor.StepAsync("quit", actor.Session.QuitAsync, token);
				actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "S1" });
			}
			Console.WriteLine("LIVE S1 group, whispers, legion membership and duel passed.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception) { Console.Error.WriteLine($"S1 failed: {exception}"); return 1; }
	}

	private sealed class LiveSocialDriver(L0Actor actor) : ISocialBasicsDriver, IPvpFlightDriver, IFriendsAndBlocksDriver, IAllianceLeagueDriver, IGroupLootDriver, IGroupQuestDriver, IExtendedSocialDriver
	{
		public BotApi Api => actor.Session.Api;
		public int CharacterId => actor.Session.CharacterId;
		public string CharacterName => actor.Session.CharacterName;
		public BotPosition CurrentPosition => actor.Session.CurrentPosition;
		public IReadOnlyList<DecodedBotServerPacket> PacketHistory => actor.Session.PacketHistory;
		public Task CompleteRecallTeleportAsync(CancellationToken token) => actor.Session.CompleteTeleportAsync(ExtendedSocialScenario.MapId, token);
		// Shared assertions cover both packet views; SIM additionally inspects server state and reloads history from MySQL.
		public Task VerifyExtendedSocialAsync(int legionId, CancellationToken token) => Task.CompletedTask;
		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token) => actor.StepAsync(action, operation, token);
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => actor.Session.SendPacketAsync(packet, token);
		public Task<DecodedBotServerPacket> WaitAnyAsync(Func<DecodedBotServerPacket, bool> predicate, CancellationToken token) =>
			actor.Session.WaitForAnyPacketAsync(predicate, token);
		public Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token) =>
			actor.Session.WaitForPacketAsync(type, token, predicate);
		public Task DelayAsync(TimeSpan duration, CancellationToken token) => Task.Delay(duration, token);
		public Task MoveAsync(BotPosition position, CancellationToken token) => actor.Session.MoveToPositionAsync(position, token);
		public Task FlyAsync(BotPosition position, CancellationToken token) => actor.Session.ExecuteMovementAsync(
			new Aion.Bots.Movement.BotMover(Api.World, Api.Timing).CreateFlightPlan([position]), token);
		public Task VerifyPvpAsync(bool winner, CancellationToken token) => Task.CompletedTask;
		// Membership is asserted from every client's decoded packets; SIM also inspects the server team graph.
		public Task VerifyAllianceAsync(CancellationToken token) => Task.CompletedTask;
		// The shared scenario checks packet-derived rights and inventory; SIM additionally inspects DropNpc and actual storage.
		public Task VerifyLootAsync(int corpseId, IReadOnlyList<int> allowedLooters, bool collected, CancellationToken token) => Task.CompletedTask;
		// Shared packet assertions cover both clients; SIM also checks the real quest and single-attacker damage list.
		public Task VerifyGroupQuestAsync(int? corpseId, int? attackerId, CancellationToken token) => Task.CompletedTask;
		public async Task LogoutAsync(CancellationToken token)
		{
			await actor.Session.QuitAsync(token);
			await actor.Session.VerifyOfflineAsync(token);
		}
		public async Task ReenterAsync(CancellationToken token)
		{
			int packetStart = actor.Session.PacketHistory.Count;
			await actor.Session.WaitForReentryAsync(token);
			await actor.Session.ReloginAndVerifyPersistenceAsync(token);
			await actor.Session.EnterWorldAsync(token);
			await SynchronizeAsync(token);
			FriendsAndBlocksScenario.AssertReloadPackets(actor.Session.PacketHistory.Skip(packetStart));
		}
		// LIVE's shared assertions observe a fresh server list after reconnect; SIM also reloads the DAOs.
		public Task VerifyFriendsAndBlocksAsync(CancellationToken token) => Task.CompletedTask;
		public async Task SynchronizeAsync(CancellationToken token)
		{
			await SendAsync(GameClientPackets.TimeCheck(unchecked((int)Environment.TickCount64)), token);
			await WaitAsync(typeof(SM_TIME_CHECK), _ => true, token);
		}
		// The shared scenario checks both client views; SIM additionally checks server and persisted membership.
		public Task VerifyAsync(int otherId, string legionName, CancellationToken token) => Task.CompletedTask;
	}
}
