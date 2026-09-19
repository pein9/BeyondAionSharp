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
	private static async Task<int> RunE6Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		await using var first = new L0Actor(options, problems, 1, Race.ELYOS, characterName: "Aelivetradea");
		await using var second = new L0Actor(options, problems, 2, Race.ELYOS, characterName: "Aelivetradeb");
		await using var director = new L0Actor(options, problems, 99, Race.ELYOS,
			bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		try
		{
			foreach (var actor in new[] { first, second, director })
			{
				actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "E6" });
				await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, token);
				await actor.StepAsync("create-character", actor.Session.CreateCharacterAsync, token);
				await actor.StepAsync("enter-world", actor.Session.EnterWorldAsync, token);
			}
			var gm = director.Session.CreateLiveGmFacade();
			int offset = 0;
			foreach (var (actor, itemId) in new[] { (first, ExchangeScenario.FirstItem), (second, ExchangeScenario.SecondItem) })
			{
				await MoveSubjectWithDirectorAsync(director, actor, gm, 210010000, 1212 + offset, 1040, 140.756f, "setup-exchange-position", token);
				await actor.StepAsync("prepare-exchange-stack", async stepToken =>
				{
					await gm.ExecuteAsync(new GmCommand("add", [actor.Session.CharacterName,
						itemId.ToString(System.Globalization.CultureInfo.InvariantCulture), "10"], "You gave"), cancellationToken: stepToken);
					await actor.Session.WaitForInventoryItemAsync(itemId, stepToken);
					await actor.Session.MoveToPositionAsync(new BotPosition(1213 + offset, 1040, 140.756f, 0), stepToken);
				}, token);
				offset++;
			}
			await ExchangeScenario.RunAsync(new LiveExchangeDriver(first), new LiveExchangeDriver(second), token);
			await first.StepAsync("verify-exchange-audit", stepToken => VerifyExchangeAuditAsync(options, first, second, stepToken), token);
			foreach (var actor in new[] { first, second, director })
			{
				if (actor != director)
					await actor.StepAsync("verify-inventory-oracle", actor.Session.VerifyInventoryAsync, token);
				await actor.StepAsync("quit", actor.Session.QuitAsync, token);
				actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "E6" });
			}
			Console.WriteLine("LIVE E6 exchange conservation and cancellation passed.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception)
		{
			Console.Error.WriteLine($"E6 failed: {exception}");
			return 1;
		}
	}

	private sealed class LiveExchangeDriver(L0Actor actor) : IExchangeScenarioDriver
	{
		public BotApi Api => actor.Session.Api;
		public int CharacterId => actor.Session.CharacterId;
		public string CharacterName => actor.Session.CharacterName;
		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token) => actor.StepAsync(action, operation, token);
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => actor.Session.SendPacketAsync(packet, token);
		public Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token) =>
			actor.Session.WaitForPacketAsync(type, token, predicate);
		public Task DelayAsync(TimeSpan delay, CancellationToken token) => Task.Delay(delay, token);
		public async Task SynchronizeAsync(CancellationToken token)
		{
			await SendAsync(GameClientPackets.TimeCheck(unchecked((int)Environment.TickCount64)), token);
			await WaitAsync(typeof(SM_TIME_CHECK), _ => true, token);
		}
		public Task VerifyReleasedAsync(CancellationToken token)
		{
			if (Api.Timing.BlockingActivities.Count != 0) throw new InvalidDataException("Exchange left a blocking client interaction.");
			// The runner cross-checks storage before logout; reopening the canceled trade exercises server cleanup.
			return Task.CompletedTask;
		}
	}

	private static async Task VerifyExchangeAuditAsync(LiveBotOptions options, L0Actor first, L0Actor second, CancellationToken token)
	{
		string path = Path.Combine(options.OutputDirectory, "logs", "gs", "gs.events.jsonl");
		string firstName = first.Session.CharacterName, secondName = second.Session.CharacterName;
		for (int attempt = 0; attempt < 20; attempt++)
		{
			var entries = new List<string>();
			using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			using (var reader = new StreamReader(stream))
			{
				while (await reader.ReadLineAsync(token) is { } line)
				{
					if (!line.EndsWith('}')) break;
					using var document = JsonDocument.Parse(line);
					var root = document.RootElement;
					// Broker startup shares this category; only exchange-service transactions belong to this oracle.
					if (root.GetProperty("cat").GetString() == "EXCHANGE_LOG" &&
						root.GetProperty("frame").GetString()?.StartsWith("Aion.GameServer.Services.ExchangeService.", StringComparison.Ordinal) == true)
						entries.Add(root.GetProperty("msg").GetString()!);
				}
			}
			if (entries.Count == 4)
			{
				bool correct = entries.Count(message => message.StartsWith($"Player {firstName} exchanged item {ExchangeScenario.FirstItem} [", StringComparison.Ordinal) &&
					message.EndsWith($"(count: 3) with player {secondName}", StringComparison.Ordinal)) == 1 &&
					entries.Count(message => message.StartsWith($"Player {secondName} exchanged item {ExchangeScenario.SecondItem} [", StringComparison.Ordinal) &&
					message.EndsWith($"(count: 10) with player {firstName}", StringComparison.Ordinal)) == 1 &&
					entries.Contains($"Player {firstName} exchanged {ExchangeScenario.FirstKinah} Kinah with player {secondName}") &&
					entries.Contains($"Player {secondName} exchanged {ExchangeScenario.SecondKinah} Kinah with player {firstName}");
				if (!correct) throw new InvalidDataException("Exchange audit did not match both item and kinah transfers.");
				return;
			}
			if (entries.Count > 4) throw new InvalidDataException("Canceled exchange or duplicate transaction produced extra audit entries.");
			await Task.Delay(250, token);
		}
		throw new InvalidDataException("Exchange audit did not record both item and kinah transfers.");
	}
}
