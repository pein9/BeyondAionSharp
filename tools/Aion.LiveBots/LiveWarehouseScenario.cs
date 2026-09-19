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
	private static async Task<int> RunE8Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		await using var subject = new L0Actor(options, problems, 1, Race.ELYOS, characterName: "Aelivestorage");
		await using var director = new L0Actor(options, problems, 99, Race.ELYOS,
			bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		try
		{
			foreach (var actor in new[] { subject, director })
			{
				actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "E8" });
				await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, token);
				await actor.StepAsync("create-character", actor.Session.CreateCharacterAsync, token);
				await actor.StepAsync("enter-world", actor.Session.EnterWorldAsync, token);
			}
			await WarehouseScenario.RunAsync(new LiveWarehouseDriver(subject, director, director.Session.CreateLiveGmFacade()), token);
			foreach (var actor in new[] { subject, director })
			{
				await actor.StepAsync("quit", actor.Session.QuitAsync, token);
				actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "E8" });
			}
			Console.WriteLine("LIVE E8 character/account warehouse transfers, expansion and relog persistence passed.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception) { Console.Error.WriteLine($"E8 failed: {exception}"); return 1; }
	}

	private sealed class LiveWarehouseDriver(L0Actor subject, L0Actor director, LiveGmFacade gm) : IWarehouseScenarioDriver
	{
		public BotApi Api => subject.Session.Api;
		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token) => subject.StepAsync(action, operation, token);
		public async Task<int> PrepareAsync(CancellationToken token)
		{
			var point = WarehouseScenario.Position;
			await MoveSubjectWithDirectorAsync(director, subject, gm, WarehouseScenario.MapId, point.X - 5, point.Y, point.Z, "setup-warehouse-position", token);
			foreach (var (id, count) in WarehouseScenario.Grants)
				await gm.ExecuteAsync(new GmCommand("add", [subject.Session.CharacterName, id.ToString(System.Globalization.CultureInfo.InvariantCulture),
					count.ToString(System.Globalization.CultureInfo.InvariantCulture)], "You gave"), cancellationToken: token);
			int npc = await subject.Session.WaitForNearestObjectExceptAsync(BotKnownObjectKind.Npc, WarehouseScenario.NpcId, new HashSet<int>(), token);
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
		public async Task VerifyPersistenceAsync(CancellationToken token)
		{
			var cube = Api.World.Inventory.OrderBy(p => p.Key).ToArray();
			var regular = Api.World.Warehouses[1].Items.OrderBy(p => p.Key).ToArray();
			var account = Api.World.Warehouses[2].Items.OrderBy(p => p.Key).ToArray();
			int? capacity = Api.World.Warehouses[1].CharacterCapacity;
			await subject.StepAsync("verify-warehouse-before-logout", subject.Session.VerifyWarehouseAsync, token);
			await subject.Session.QuitAsync(token);
			await subject.Session.VerifyOfflineAsync(token);
			await subject.Session.WaitForReentryAsync(token);
			await subject.Session.ReloginAndVerifyPersistenceAsync(token);
			await subject.Session.EnterWorldAsync(token);
			await SynchronizeAsync(token);
			if (!cube.SequenceEqual(Api.World.Inventory.OrderBy(p => p.Key)) || !regular.SequenceEqual(Api.World.Warehouses[1].Items.OrderBy(p => p.Key))
				|| !account.SequenceEqual(Api.World.Warehouses[2].Items.OrderBy(p => p.Key)) || capacity != Api.World.Warehouses[1].CharacterCapacity)
				throw new InvalidDataException("Warehouse or inventory changed across a real logout and fresh login.");
			await subject.StepAsync("verify-warehouse-after-relogin", subject.Session.VerifyWarehouseAsync, token);
		}
	}
}
