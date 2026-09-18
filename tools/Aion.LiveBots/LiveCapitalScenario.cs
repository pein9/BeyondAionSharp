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
	private static async Task<int> RunCapitalAsync(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		await using var subject = new L0Actor(options, problems, 1, Race.ELYOS, characterName: "Aelivecapital");
		await using var director = new L0Actor(options, problems, 99, Race.ELYOS,
			bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		try
		{
			subject.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "CAPITAL" });
			foreach (var actor in new[] { subject, director })
			{
				await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, token);
				await actor.StepAsync("create-character", actor.Session.CreateCharacterAsync, token);
				await actor.StepAsync("enter-world", actor.Session.EnterWorldAsync, token);
			}
			await CapitalAscensionScenario.RunAsync(new LiveCapitalDriver(subject, director, director.Session.CreateLiveGmFacade()), token);
			QuestCoverageReceipt.Save(options.OutputDirectory, "LIVE", "CAPITAL", subject.Session.Api.World);
			await subject.StepAsync("quit", subject.Session.QuitAsync, token);
			await director.StepAsync("quit", director.Session.QuitAsync, token);
			subject.Trace.WriteAction(subject.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "CAPITAL" });
			Console.WriteLine("LIVE CAPITAL: Ascension and Ceremony completed through client actions.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception)
		{
			Console.Error.WriteLine($"CAPITAL failed: {exception}");
			return 1;
		}
	}

	private sealed class LiveCapitalDriver(L0Actor subject, L0Actor director, LiveGmFacade gm) : ICapitalAscensionDriver
	{
		public BotApi Api => subject.Session.Api;
		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token) => subject.StepAsync(action, operation, token);
		public async Task PrepareAsync(CancellationToken token)
		{
			var point = CapitalAscensionScenario.Pernos;
			await MoveSubjectWithDirectorAsync(director, subject, gm, 210010000, point.X - 5, point.Y, point.Z, "setup-ascension-start", token);
			await gm.ExecuteAsync(new GmCommand("set", ["level", "9"], "level to 9"),
				new GmSubject(subject.Session.CharacterId, subject.Session.CharacterName), token);
		}
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => subject.Session.SendPacketAsync(packet, token);
		public Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token) => subject.Session.WaitForPacketAsync(type, token, predicate);
		public Task MoveAsync(BotPosition position, CancellationToken token) => subject.Session.MoveToPositionAsync(position, token);
		public Task DelayAsync(TimeSpan duration, CancellationToken token) => Task.Delay(duration, token);
		public Task CompleteTeleportAsync(int mapId, CancellationToken token)
		{
			Api.World.BeginWorldReload();
			return subject.Session.CompleteTeleportAsync(mapId, token);
		}
		public Task FlyAsync(BotPosition destination, TimeSpan duration, CancellationToken token) => subject.Session.ExecuteMovementAsync(
			CapitalAscensionScenario.CreateQuestFlight(subject.Session.CurrentPosition, destination, Api.World.MapId!.Value, duration), token);
		public async Task SynchronizeAsync(CancellationToken token)
		{
			await SendAsync(GameClientPackets.TimeCheck(unchecked((int)Environment.TickCount64)), token);
			await WaitAsync(typeof(SM_TIME_CHECK), _ => true, token);
		}
		public Task VerifyAsync(CancellationToken token)
		{
			if (Api.Timing.BlockingActivities.Count != 0) throw new InvalidDataException("Capital journey left an unfinished interaction.");
			return Task.CompletedTask;
		}
	}
}
