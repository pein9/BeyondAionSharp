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
	private static async Task<int> RunE11Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		await using var first = new L0Actor(options, problems, 1, Race.ELYOS, characterName: "Aelivevenda");
		await using var second = new L0Actor(options, problems, 2, Race.ELYOS, characterName: "Aelivevendb");
		await using var third = new L0Actor(options, problems, 3, Race.ELYOS, characterName: "Aelivevendc");
		await using var director = new L0Actor(options, problems, 99, Race.ELYOS, bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		var buyers = new[] { first, second, third };
		try
		{
			foreach (var actor in buyers.Append(director))
			{
				actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "E11" });
				await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, token);
				await actor.StepAsync("create-character", actor.Session.CreateCharacterAsync, token);
				await actor.StepAsync("enter-world", actor.Session.EnterWorldAsync, token);
			}
			var gm = director.Session.CreateLiveGmFacade();
			foreach (var actor in buyers)
				await actor.StepAsync("prepare-trade-in-materials-and-vendor-funds", async ct =>
				{
					await gm.ExecuteAsync(new GmCommand("add", [actor.Session.CharacterName, "182400001", "20000000"], "You gave"), cancellationToken: ct);
					if (actor == first)
					{
						await gm.ExecuteAsync(new GmCommand("add", [actor.Session.CharacterName, "186000130", "29400"], "You gave"), cancellationToken: ct);
						await gm.ExecuteAsync(new GmCommand("add", [actor.Session.CharacterName, "186000137", "1200"], "You gave"), cancellationToken: ct);
					}
				}, token);
			await TradeInVendorScenario.RunAsync(buyers.Select(a => (ITradeInVendorScenarioDriver)new LiveTradeInDriver(a, director, gm)).ToArray(), token);
			foreach (var actor in buyers.Append(director))
			{
				if (actor != director) await actor.StepAsync("verify-final-vendor-inventory", actor.Session.VerifyInventoryAsync, token);
				await actor.StepAsync("quit", actor.Session.QuitAsync, token);
				actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "E11" });
			}
			Console.WriteLine("LIVE E11 trade-in, character purchase limits, shared stock exhaustion and relog persistence passed.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception) { Console.Error.WriteLine($"E11 failed: {exception}"); return 1; }
	}

	private sealed class LiveTradeInDriver(L0Actor actor, L0Actor director, LiveGmFacade gm) : ITradeInVendorScenarioDriver
	{
		public BotApi Api => actor.Session.Api;
		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token) => actor.StepAsync(action, operation, token);
		public async Task<int> ApproachAsync(int mapId, int npcId, BotPosition position, CancellationToken token)
		{
			await MoveSubjectWithDirectorAsync(director, actor, gm, mapId, position.X - 5, position.Y, position.Z, "setup-vendor-position", token);
			int npc = await actor.Session.WaitForNearestObjectExceptAsync(BotKnownObjectKind.Npc, npcId, new HashSet<int>(), token);
			await actor.Session.MoveToKnownObjectAsync(npc, token); await SynchronizeAsync(token); return npc;
		}
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => actor.Session.SendPacketAsync(packet, token);
		public Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token) => actor.Session.WaitForPacketAsync(type, token, predicate);
		public async Task SynchronizeAsync(CancellationToken token)
		{
			await SendAsync(GameClientPackets.TimeCheck(unchecked((int)Environment.TickCount64)), token); await WaitAsync(typeof(SM_TIME_CHECK), _ => true, token);
		}
		public async Task VerifyPersistenceAsync(CancellationToken token)
		{
			await actor.StepAsync("verify-vendor-inventory-before-logout", actor.Session.VerifyInventoryAsync, token);
			var expected = Api.World.Inventory.OrderBy(p => p.Key).ToArray();
			await actor.StepAsync("quit-and-confirm-offline", async ct =>
			{
				await actor.Session.QuitAsync(ct); await actor.Session.VerifyOfflineAsync(ct);
			}, token);
			await actor.StepAsync("honor-reentry-delay", actor.Session.WaitForReentryAsync, token);
			await actor.StepAsync("fresh-login-and-verify-vendor-persistence", async ct =>
			{
				await actor.Session.ReloginAndVerifyPersistenceAsync(ct); await actor.Session.EnterWorldAsync(ct); await SynchronizeAsync(ct);
				if (!expected.SequenceEqual(Api.World.Inventory.OrderBy(p => p.Key))) throw new InvalidDataException("Vendor inventory changed across relog.");
			}, token);
			await actor.StepAsync("verify-vendor-inventory-after-relogin", actor.Session.VerifyInventoryAsync, token);
		}
	}
}
