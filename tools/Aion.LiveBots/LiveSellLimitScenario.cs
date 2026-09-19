using Aion.Bots.Api;
using Aion.Bots.Gm;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static async Task<int> RunL7Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		await using var subject = new L0Actor(options, problems, 1, Race.ELYOS, characterName: "Liveselllimit");
		await using var director = new L0Actor(options, problems, 99, Race.ELYOS,
			bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		try
		{
			foreach (var actor in new[] { subject, director })
			{
				actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "L7" });
				await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, token);
				await actor.StepAsync("create-character", actor.Session.CreateCharacterAsync, token);
				await actor.StepAsync("enter-world", actor.Session.EnterWorldAsync, token);
				await actor.StepAsync("drain-startup", actor.Session.SynchronizeAsync, token);
			}
			await SellLimitScenario.RunAsync(new LiveSellLimitDriver(subject, director, director.Session.CreateLiveGmFacade()), token);
			foreach (var actor in new[] { subject, director })
			{
				await actor.StepAsync("drain-and-quit", async ct =>
				{
					await actor.Session.SynchronizeAsync(ct); await actor.Session.QuitAsync(ct); await actor.Session.VerifyOfflineAsync(ct);
				}, token);
				actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "L7" });
			}
			Console.WriteLine("LIVE L7 daily sales cap, partial fulfillment and refusal before/after relog passed."); return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception) { Console.Error.WriteLine($"L7 failed: {exception}"); return 1; }
	}
	private sealed class LiveSellLimitDriver(L0Actor subject, L0Actor director, LiveGmFacade gm) : ISellLimitScenarioDriver
	{
		public BotApi Api => subject.Session.Api;
		public long BasePrice { get; } = VendorScenario.ReadBasePrice(SellLimitScenario.ItemId);
		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token) => subject.StepAsync(action, operation, token);
		public async Task<int> PrepareAsync(CancellationToken token)
		{
			await gm.ExecuteAsync(new GmCommand("add", [subject.Session.CharacterName, SellLimitScenario.ItemId.ToString(), SellLimitScenario.SetupCount.ToString()], "You gave"), cancellationToken: token);
			var point = VendorScenario.Position;
			await MoveSubjectWithDirectorAsync(director, subject, gm, 210010000, point.X - 5, point.Y, point.Z, "setup-vendor-position", token);
			int npc = await subject.Session.WaitForNearestObjectExceptAsync(BotKnownObjectKind.Npc, VendorScenario.VendorId, new HashSet<int>(), token);
			await subject.Session.MoveToKnownObjectAsync(npc, token); return npc;
		}
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => subject.Session.SendPacketAsync(packet, token);
		public Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token) => subject.Session.WaitForPacketAsync(type, token, predicate);
		public Task SynchronizeAsync(CancellationToken token) => subject.Session.SynchronizeAsync(token);
		public Task VerifyServerStateAsync(IReadOnlyDictionary<int, long> expected, CancellationToken token) => subject.Session.VerifyInventoryAsync(token);
		public async Task ReloginAsync(CancellationToken token)
		{
			await subject.Session.QuitAsync(token); await subject.Session.VerifyOfflineAsync(token); await subject.Session.WaitForReentryAsync(token);
			await subject.Session.ReloginAndVerifyPersistenceAsync(token); await subject.Session.EnterWorldAsync(token); await subject.Session.SynchronizeAsync(token);
		}
	}
}
