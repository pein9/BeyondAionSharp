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
	private static async Task<int> RunL2Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		await using var subject = new L0Actor(options, problems, 1, Race.ELYOS, characterName: "Aelivesettings");
		await using var director = new L0Actor(options, problems, 99, Race.ELYOS,
			bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		try
		{
			foreach (var actor in new[] { subject, director })
			{
				actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "L2" });
				await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, token);
				await actor.StepAsync("create-character", actor.Session.CreateCharacterAsync, token);
				await actor.StepAsync("enter-world", actor.Session.EnterWorldAsync, token);
			}
			await CharacterSettingsScenario.RunAsync(new LiveCharacterSettingsDriver(subject, director, director.Session.CreateLiveGmFacade()), token);
			foreach (var actor in new[] { subject, director })
			{
				await actor.StepAsync("quit", actor.Session.QuitAsync, token);
				actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "L2" });
			}
			Console.WriteLine("LIVE L2 ticketed appearance editing, titles, macro create/update/delete and UI settings passed three fresh-login persistence checks.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception) { Console.Error.WriteLine($"L2 failed: {exception}"); return 1; }
	}

	private sealed class LiveCharacterSettingsDriver(L0Actor subject, L0Actor director, LiveGmFacade gm) : ICharacterSettingsDriver
	{
		public BotApi Api => subject.Session.Api;
		public int CharacterId => subject.Session.CharacterId;
		public string CharacterName => subject.Session.CharacterName;
		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token) => subject.StepAsync(action, operation, token);
		public async Task<int> PrepareAsync(CancellationToken token)
		{
			var point = CharacterSettingsScenario.SurgeonPosition;
			await MoveSubjectWithDirectorAsync(director, subject, gm, CharacterSettingsScenario.SurgeonMap, point.X - 5, point.Y, point.Z, "setup-surgeon-position", token);
			await gm.ExecuteAsync(new GmCommand("add", [CharacterName, CharacterSettingsScenario.SurgeryTicket.ToString(), "1"], "You gave"), cancellationToken: token);
			await gm.ExecuteAsync(new GmCommand("addtitle", ["1", CharacterName], "Added title"), cancellationToken: token);
			await gm.ExecuteAsync(new GmCommand("addtitle", ["2", CharacterName], "Added title"), cancellationToken: token);
			int npc = await subject.Session.WaitForNearestObjectExceptAsync(BotKnownObjectKind.Npc, CharacterSettingsScenario.SurgeonNpc, new HashSet<int>(), token);
			await subject.Session.MoveToKnownObjectAsync(npc, token);
			return npc;
		}
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => subject.Session.SendPacketAsync(packet, token);
		public Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token) => subject.Session.WaitForPacketAsync(type, token, predicate);
		public async Task SynchronizeAsync(CancellationToken token)
		{
			await SendAsync(GameClientPackets.TimeCheck(unchecked((int)Environment.TickCount64)), token);
			await WaitAsync(typeof(SM_TIME_CHECK), _ => true, token);
		}
		public Task ReturnToEditScreenAsync(CancellationToken token) => subject.Session.ReturnToSelectionAsync(true, token);
		public Task EditAndEnterAsync(CharacterCreationData appearance, CancellationToken token) => subject.Session.EditAndEnterAsync(appearance, token);
		public async Task<IReadOnlyList<DecodedBotServerPacket>> ReloginAsync(CancellationToken token)
		{
			await subject.StepAsync("quit-and-confirm-offline", async ct => { await subject.Session.QuitAsync(ct); await subject.Session.VerifyOfflineAsync(ct); }, token);
			await subject.StepAsync("honor-reentry-delay", subject.Session.WaitForReentryAsync, token);
			int start = subject.Session.PacketHistory.Count;
			await subject.StepAsync("fresh-login-and-enter-world", async ct =>
			{
				await subject.Session.ReloginAndVerifyPersistenceAsync(ct); await subject.Session.EnterWorldAsync(ct); await SynchronizeAsync(ct);
			}, token);
			return subject.Session.PacketHistory.Skip(start).ToArray();
		}
		public Task VerifyStoredAsync(CharacterSettingsExpected expected, CancellationToken token) => subject.Session.VerifyInventoryAsync(token);
	}
}
