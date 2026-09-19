using Aion.Bots.Api;
using Aion.Bots.Gm;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;
using System.Text.Json;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static async Task<int> RunE4Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
		=> await RunCookingAsync(options, problems, workOrders: false, token);
	private static async Task<int> RunE5Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
		=> await RunCookingAsync(options, problems, workOrders: true, token);

	private static async Task<int> RunCookingAsync(LiveBotOptions options, LiveBotProblemWriter problems, bool workOrders, CancellationToken token)
	{
		string scenarioId = workOrders ? "E5" : "E4";
		await using var director = new L0Actor(options, problems, 99, Race.ELYOS,
			bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		try
		{
			await director.StepAsync("login-game-auth", director.Session.LoginAndAuthenticateAsync, token);
			await director.StepAsync("create-character", director.Session.CreateCharacterAsync, token);
			await director.StepAsync("enter-world", director.Session.EnterWorldAsync, token);
			foreach (var (master, index, name) in new[]
			{
				(CookingMaster.Hestia, 1, "Aelivecooking"),
				(CookingMaster.Lainita, 2, "Aslivecooking"),
			})
			{
				await using var subject = new L0Actor(options, problems, index, master.Race, characterName: name);
				subject.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = scenarioId });
				await subject.StepAsync("login-game-auth", subject.Session.LoginAndAuthenticateAsync, token);
				await subject.StepAsync("create-character", subject.Session.CreateCharacterAsync, token);
				await subject.StepAsync("enter-world", subject.Session.EnterWorldAsync, token);
				var driver = new LiveCookingDriver(subject, director, director.Session.CreateLiveGmFacade(), options);
				await CookingLearnScenario.RunAsync(driver, master, token);
				if (workOrders)
					foreach (var order in CookingWorkOrder.For(master))
						await CookingWorkOrderScenario.RunAsync(driver, order, token);
				await subject.StepAsync("verify-inventory-oracle", subject.Session.VerifyInventoryAsync, token);
				await subject.StepAsync("quit", subject.Session.QuitAsync, token);
				subject.Trace.WriteAction(subject.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = scenarioId });
			}
			await director.StepAsync("quit", director.Session.QuitAsync, token);
			Console.WriteLine($"LIVE {scenarioId} Cooking {(workOrders ? "work orders" : "refusal/learning")} passed for both races.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception)
		{
			Console.Error.WriteLine($"{scenarioId} failed: {exception}");
			return 1;
		}
	}

	private sealed class LiveCookingDriver(L0Actor subject, L0Actor director, LiveGmFacade gm, LiveBotOptions options) : ICookingWorkOrderDriver
	{
		public BotApi Api => subject.Session.Api;
		public IReadOnlyList<DecodedBotServerPacket> History => subject.Session.PacketHistory;
		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token) =>
			subject.StepAsync(action, operation, token);
		public async Task<int> PrepareAsync(CookingMaster master, CancellationToken token)
		{
			var point = master.Position;
			await MoveSubjectWithDirectorAsync(director, subject, gm, master.MapId, point.X - 5, point.Y, point.Z, "setup-cooking-master", token);
			await gm.ExecuteAsync(new GmCommand("set", ["level", "9"], "level to 9"),
				new GmSubject(subject.Session.CharacterId, subject.Session.CharacterName), token);
			await gm.ExecuteAsync(new GmCommand("add", [subject.Session.CharacterName, "182400001", "5000"], "You gave"), cancellationToken: token);
			int npc = await subject.Session.WaitForNearestObjectExceptAsync(BotKnownObjectKind.Npc, master.NpcId, new HashSet<int>(), token);
			await subject.Session.MoveToKnownObjectAsync(npc, token);
			return npc;
		}
		public async Task MakeLevelTenAsync(CancellationToken token) => await gm.ExecuteVerifiedAsync(
			new GmCommand("set", ["class", "gladiator"], "replyless class change"),
			new GmCommand("set", ["level", "10"], "level to 10"),
			new GmSubject(subject.Session.CharacterId, subject.Session.CharacterName), token);
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => subject.Session.SendPacketAsync(packet, token);
		public Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token) =>
			subject.Session.WaitForPacketAsync(type, token, predicate);
		public Task DelayAsync(TimeSpan delay, CancellationToken token) => Task.Delay(delay, token);
		public async Task SynchronizeAsync(CancellationToken token)
		{
			await SendAsync(GameClientPackets.TimeCheck(unchecked((int)Environment.TickCount64)), token);
			await WaitAsync(typeof(SM_TIME_CHECK), _ => true, token);
		}
		// The runner cross-checks server storage after the shared scenario, before logout.
		public Task VerifyServerStateAsync(CancellationToken token) => Task.CompletedTask;
		public async Task PrepareWorkOrderAsync(CookingWorkOrder order, CancellationToken token)
		{
			if (order.NeedsSalt)
			{
				await gm.ExecuteAsync(new GmCommand("addskill", ["40001", "10"], "You have success add skill"),
					new GmSubject(subject.Session.CharacterId, subject.Session.CharacterName), token);
				await gm.ExecuteAsync(new GmCommand("add", [subject.Session.CharacterName, "169400096",
					(order.DeliverCount + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)], "You gave"), cancellationToken: token);
			}
		}
		public Task MoveBesideAsync(int objectId, CancellationToken token) => subject.Session.MoveToKnownObjectAsync(objectId, token);
		// The shared packet wait observes real-time task completion; SIM advances its virtual scheduler here.
		public Task AwaitCraftAsync(CancellationToken token) => Task.CompletedTask;
		public async Task VerifyWorkOrderAsync(CookingWorkOrder order, CancellationToken token)
		{
			// The JSON log provider routes the legacy craft.log category into this run's GS event stream.
			string path = Path.Combine(options.OutputDirectory, "logs", "gs", "gs.events.jsonl");
			string prefix = $"Player {subject.Session.CharacterName} crafted item {order.ProductId} [";
			for (int attempt = 0; attempt < 20; attempt++)
			{
				int count = 0;
				using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				using (var reader = new StreamReader(stream))
				{
					while (await reader.ReadLineAsync(token) is { } line)
					{
						// A writer may be halfway through its final line; retry on the next observation.
						if (!line.EndsWith('}')) break;
						using var entry = JsonDocument.Parse(line);
						var root = entry.RootElement;
						if (root.GetProperty("cat").GetString() == "CRAFT_LOG" &&
							root.GetProperty("msg").GetString() is { } message && message.StartsWith(prefix, StringComparison.Ordinal))
						{
							if (!message.EndsWith("(count: 1)", StringComparison.Ordinal))
								throw new InvalidDataException("Craft audit has an unexpected product count or critical result.");
							count++;
						}
					}
				}
				if (count == order.DeliverCount) return;
				if (count > order.DeliverCount) throw new InvalidDataException("Craft audit recorded duplicate products.");
				await Task.Delay(250, token);
			}
			throw new InvalidDataException($"Craft audit did not record all {order.DeliverCount} products for quest {order.QuestId}.");
		}
	}
}
