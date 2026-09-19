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
	private static async Task<int> RunE9Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		await using var seller = new L0Actor(options, problems, 1, Race.ELYOS, characterName: "Aeliveseller");
		await using var buyer = new L0Actor(options, problems, 2, Race.ELYOS, characterName: "Aelivebuyer");
		await using var director = new L0Actor(options, problems, 99, Race.ELYOS, bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		try
		{
			foreach (var actor in new[] { seller, buyer, director })
			{
				actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "E9" });
				await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, token);
				await actor.StepAsync("create-character", actor.Session.CreateCharacterAsync, token);
				await actor.StepAsync("enter-world", actor.Session.EnterWorldAsync, token);
			}
			var gm = director.Session.CreateLiveGmFacade();
			var drivers = new List<LiveBrokerDriver>();
			foreach (var actor in new[] { seller, buyer })
				await actor.StepAsync("prepare-raw-items-and-walk-to-broker", async ct =>
				{
					var point = BrokerScenario.Position;
					await MoveSubjectWithDirectorAsync(director, actor, gm, BrokerScenario.MapId, point.X - 5, point.Y, point.Z, "setup-broker-position", ct);
					await gm.ExecuteAsync(new GmCommand("add", [actor.Session.CharacterName, "182400001", "100000"], "You gave"), cancellationToken: ct);
					if (actor == seller) await gm.ExecuteAsync(new GmCommand("add", [actor.Session.CharacterName, "152000102", "10"], "You gave"), cancellationToken: ct);
					int npc = await actor.Session.WaitForNearestObjectExceptAsync(BotKnownObjectKind.Npc, BrokerScenario.NpcId, new HashSet<int>(), ct);
					await actor.Session.MoveToKnownObjectAsync(npc, ct);
					drivers.Add(new LiveBrokerDriver(actor, npc));
				}, token);
			await BrokerScenario.RunAsync(drivers[0], drivers[1], token);
			foreach (var actor in new[] { seller, buyer, director })
			{
				if (actor != director) await actor.StepAsync("verify-final-inventory-oracle", actor.Session.VerifyInventoryAsync, token);
				await actor.StepAsync("quit", actor.Session.QuitAsync, token);
				actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "E9" });
			}
			Console.WriteLine("LIVE E9 broker registration, partial/full sales, settlement, cancellation and relog persistence passed. Scheduled expiry is covered in SIM.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception) { Console.Error.WriteLine($"E9 failed: {exception}"); return 1; }
	}

	private sealed class LiveBrokerDriver(L0Actor actor, int npcObjectId) : IBrokerScenarioDriver
	{
		public BotApi Api => actor.Session.Api;
		public int NpcObjectId => npcObjectId;
		public string CharacterName => actor.Session.CharacterName;
		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token) => actor.StepAsync(action, operation, token);
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => actor.Session.SendPacketAsync(packet, token);
		public Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token) => actor.Session.WaitForPacketAsync(type, token, predicate);
		public Task DelayAsync(TimeSpan delay, CancellationToken token) => Task.Delay(delay, token);
		public async Task SynchronizeAsync(CancellationToken token)
		{
			await SendAsync(GameClientPackets.TimeCheck(unchecked((int)Environment.TickCount64)), token); await WaitAsync(typeof(SM_TIME_CHECK), _ => true, token);
		}
		public async Task VerifyPersistenceAsync(CancellationToken token)
		{
			await actor.StepAsync("wait-for-broker-save-tick", async ct =>
			{
				await DelayAsync(TimeSpan.FromSeconds(7), ct);
				await SynchronizeAsync(ct);
			}, token);
			var expected = Api.World.Inventory.OrderBy(p => p.Key).ToArray();
			await actor.StepAsync("verify-broker-inventory-before-logout", actor.Session.VerifyInventoryAsync, token);
			await actor.StepAsync("quit-and-confirm-offline", async ct =>
			{
				await actor.Session.QuitAsync(ct); await actor.Session.VerifyOfflineAsync(ct);
			}, token);
			await actor.StepAsync("honor-reentry-delay", actor.Session.WaitForReentryAsync, token);
			await actor.StepAsync("fresh-login-and-enter-world", async ct =>
			{
				await actor.Session.ReloginAndVerifyPersistenceAsync(ct); await actor.Session.EnterWorldAsync(ct); await SynchronizeAsync(ct);
				if (!expected.SequenceEqual(Api.World.Inventory.OrderBy(p => p.Key))) throw new InvalidDataException("Broker inventory changed across logout and fresh login.");
			}, token);
			await actor.StepAsync("verify-broker-inventory-after-relogin", actor.Session.VerifyInventoryAsync, token);
		}
	}
}
