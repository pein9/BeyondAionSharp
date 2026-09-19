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
	private static async Task<int> RunE10Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		await using var seller = new L0Actor(options, problems, 1, Race.ELYOS, characterName: "Aeliveshopa");
		await using var buyer = new L0Actor(options, problems, 2, Race.ELYOS, characterName: "Aeliveshopb");
		await using var director = new L0Actor(options, problems, 99, Race.ELYOS, bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		try
		{
			foreach (var actor in new[] { seller, buyer, director })
			{
				actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "E10" });
				await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, token);
				await actor.StepAsync("create-character", actor.Session.CreateCharacterAsync, token);
				await actor.StepAsync("enter-world", actor.Session.EnterWorldAsync, token);
			}
			var gm = director.Session.CreateLiveGmFacade(); int offset = 0;
			foreach (var actor in new[] { seller, buyer })
			{
				await actor.StepAsync("prepare-private-store-items-and-walk-to-peer", async ct =>
				{
					var p = PrivateStoreScenario.Position;
					await MoveSubjectWithDirectorAsync(director, actor, gm, PrivateStoreScenario.MapId, p.X - 1 + offset, p.Y, p.Z, "setup-private-store-position", ct);
					await gm.ExecuteAsync(new GmCommand("add", [actor.Session.CharacterName, "182400001", "100000"], "You gave"), cancellationToken: ct);
					if (actor == seller) await gm.ExecuteAsync(new GmCommand("add", [actor.Session.CharacterName, "152000102", "10"], "You gave"), cancellationToken: ct);
					await actor.Session.MoveToPositionAsync(p with { X = p.X + offset }, ct);
				}, token);
				offset += 2;
			}
			await PrivateStoreScenario.RunAsync(new LivePrivateStoreDriver(seller), new LivePrivateStoreDriver(buyer), token);
			foreach (var actor in new[] { seller, buyer, director })
			{
				if (actor != director) await actor.StepAsync("verify-final-inventory-oracle", actor.Session.VerifyInventoryAsync, token);
				await actor.StepAsync("quit", actor.Session.QuitAsync, token);
				actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "E10" });
			}
			Console.WriteLine("LIVE E10 private-store naming, partial purchase, explicit close before movement, sell-out and relog persistence passed.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception) { Console.Error.WriteLine($"E10 failed: {exception}"); return 1; }
	}

	private sealed class LivePrivateStoreDriver(L0Actor actor) : IPrivateStoreScenarioDriver
	{
		public BotApi Api => actor.Session.Api;
		public int CharacterId => actor.Session.CharacterId;
		public BotPosition CurrentPosition => actor.Session.CurrentPosition;
		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token) => actor.StepAsync(action, operation, token);
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => actor.Session.SendPacketAsync(packet, token);
		public Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token) => actor.Session.WaitForPacketAsync(type, token, predicate);
		public Task MoveAsync(BotPosition position, CancellationToken token) => actor.Session.MoveToPositionAsync(position, token);
		public async Task SynchronizeAsync(CancellationToken token)
		{
			await SendAsync(GameClientPackets.TimeCheck(unchecked((int)Environment.TickCount64)), token); await WaitAsync(typeof(SM_TIME_CHECK), _ => true, token);
		}
		public async Task VerifyPersistenceAsync(CancellationToken token)
		{
			await actor.StepAsync("verify-private-store-inventory-before-logout", actor.Session.VerifyInventoryAsync, token);
			var expected = Api.World.Inventory.OrderBy(p => p.Key).ToArray();
			await actor.StepAsync("quit-and-confirm-offline", async ct =>
			{
				await actor.Session.QuitAsync(ct); await actor.Session.VerifyOfflineAsync(ct);
			}, token);
			await actor.StepAsync("honor-reentry-delay", actor.Session.WaitForReentryAsync, token);
			await actor.StepAsync("fresh-login-and-verify-private-store-persistence", async ct =>
			{
				await actor.Session.ReloginAndVerifyPersistenceAsync(ct); await actor.Session.EnterWorldAsync(ct); await SynchronizeAsync(ct);
				if (!expected.SequenceEqual(Api.World.Inventory.OrderBy(p => p.Key))) throw new InvalidDataException("Private-store inventory changed across relog.");
			}, token);
			await actor.StepAsync("verify-private-store-inventory-after-relogin", actor.Session.VerifyInventoryAsync, token);
		}
	}
}
