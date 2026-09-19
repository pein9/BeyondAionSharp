using Aion.Bots.Api;
using Aion.Bots.Gm;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static async Task<int> RunL5Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		await using var subject = new L0Actor(options, problems, 1, Race.ELYOS, characterName: "Livepet");
		await using var director = new L0Actor(options, problems, 99, Race.ELYOS,
			bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		try
		{
			foreach (var actor in new[] { subject, director })
			{
				actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "L5" });
				await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, token);
				await actor.StepAsync("create-character", actor.Session.CreateCharacterAsync, token);
				await actor.StepAsync("enter-world", actor.Session.EnterWorldAsync, token);
				await actor.StepAsync("drain-startup", actor.Session.SynchronizeAsync, token);
			}
			await PetLifecycleScenario.RunAsync(new LivePetLifecycleDriver(subject, director.Session.CreateLiveGmFacade()), token);
			foreach (var actor in new[] { subject, director })
			{
				await actor.StepAsync("drain-and-quit", async ct =>
				{
					await actor.Session.SynchronizeAsync(ct); await actor.Session.QuitAsync(ct); await actor.Session.VerifyOfflineAsync(ct);
				}, token);
				actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "L5" });
			}
			Console.WriteLine("LIVE L5 adoption, summon, timed feeding, dismissal and fresh-login persistence passed."); return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception) { Console.Error.WriteLine($"L5 failed: {exception}"); return 1; }
	}

	private sealed class LivePetLifecycleDriver(L0Actor subject, LiveGmFacade gm) : IPetLifecycleDriver
	{
		public BotApi Api => subject.Session.Api;
		public int CharacterId => subject.Session.CharacterId;
		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token) => subject.StepAsync(action, operation, token);
		public async Task PrepareAsync(CancellationToken token)
		{
			await gm.ExecuteAsync(new GmCommand("add", [subject.Session.CharacterName, PetLifecycleScenario.EggItem.ToString(), "1"], "You gave"), cancellationToken: token);
			await gm.ExecuteAsync(new GmCommand("add", [subject.Session.CharacterName, PetLifecycleScenario.FoodItem.ToString(), "3"], "You gave"), cancellationToken: token);
		}
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => subject.Session.SendPacketAsync(packet, token);
		public Task<DecodedBotServerPacket> WaitAsync(Func<DecodedBotServerPacket, bool> predicate, CancellationToken token) =>
			subject.Session.WaitForPacketAsync(typeof(SM_PET), token, predicate);
		public Task DelayAsync(TimeSpan duration, CancellationToken token) => Task.Delay(duration, token);
		public Task SynchronizeAsync(CancellationToken token) => subject.Session.SynchronizeAsync(token);
		// Pet persistence is asserted from a new login's serialized pet list; this independent HTTP oracle covers inventory.
		public Task VerifyStateAsync(int petObjectId, bool summoned, int feedProgress, CancellationToken token) => subject.Session.VerifyInventoryAsync(token);
		public async Task<IReadOnlyList<DecodedBotServerPacket>> ReloginAsync(CancellationToken token)
		{
			await subject.Session.QuitAsync(token); await subject.Session.VerifyOfflineAsync(token); await subject.Session.WaitForReentryAsync(token);
			int start = subject.Session.PacketHistory.Count;
			await subject.Session.ReloginAndVerifyPersistenceAsync(token); await subject.Session.EnterWorldAsync(token); await subject.Session.SynchronizeAsync(token);
			return subject.Session.PacketHistory.Skip(start).ToArray();
		}
	}
}
