using Aion.Bots.Api;
using Aion.Bots.Gm;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;
using System.Text.Json;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static async Task<int> RunE7Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		await using var first = new L0Actor(options, problems, 1, Race.ELYOS, characterName: "Aelivemaila");
		await using var second = new L0Actor(options, problems, 2, Race.ELYOS, characterName: "Aelivemailb");
		await using var director = new L0Actor(options, problems, 99, Race.ELYOS,
			bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		try
		{
			foreach (var actor in new[] { first, second, director })
			{
				actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "E7" });
				await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, token);
				await actor.StepAsync("create-character", actor.Session.CreateCharacterAsync, token);
				await actor.StepAsync("enter-world", actor.Session.EnterWorldAsync, token);
			}
			await first.StepAsync("prepare-mail-stack", async stepToken =>
			{
				await director.Session.CreateLiveGmFacade().ExecuteAsync(new GmCommand("add", [first.Session.CharacterName,
					MailScenario.ItemId.ToString(System.Globalization.CultureInfo.InvariantCulture), "40"], "You gave"), cancellationToken: stepToken);
				await first.Session.WaitForInventoryItemAsync(MailScenario.ItemId, stepToken);
			}, token);
			await MailScenario.RunAsync(new LiveMailDriver(first), new LiveMailDriver(second), token);
			await first.StepAsync("verify-mail-audit", stepToken => VerifyMailAuditAsync(options, first, second, stepToken), token);
			foreach (var actor in new[] { first, second, director })
			{
				if (actor != director)
					await actor.StepAsync("verify-inventory-oracle", actor.Session.VerifyInventoryAsync, token);
				await actor.StepAsync("quit", actor.Session.QuitAsync, token);
				actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "E7" });
			}
			Console.WriteLine("LIVE E7 normal/express mail, attachments, fees and duplicate-claim checks passed.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception)
		{
			Console.Error.WriteLine($"E7 failed: {exception}");
			return 1;
		}
	}

	private sealed class LiveMailDriver(L0Actor actor) : IMailScenarioDriver
	{
		public BotApi Api => actor.Session.Api;
		public int CharacterId => actor.Session.CharacterId;
		public string CharacterName => actor.Session.CharacterName;
		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token) => actor.StepAsync(action, operation, token);
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => actor.Session.SendPacketAsync(packet, token);
		public Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token) =>
			actor.Session.WaitForPacketAsync(type, token, predicate);
		public async Task SynchronizeAsync(CancellationToken token)
		{
			await SendAsync(GameClientPackets.TimeCheck(unchecked((int)Environment.TickCount64)), token);
			await WaitAsync(typeof(SM_TIME_CHECK), _ => true, token);
		}
		public async Task VerifyMailboxAsync(int letterId, long itemCount, long kinah, bool deleted, CancellationToken token)
		{
			if (deleted)
			{
				await SendAsync(Api.CheckMailList(), token);
				while (true)
				{
					var list = await WaitAsync(typeof(SM_MAIL_SERVICE), packet => packet.Get<byte>("serviceId") == 2, token);
					if (list.Get<List<IReadOnlyDictionary<string, object?>>>("letters").Any(row => (int)row["letterId"]! == letterId))
						throw new InvalidDataException("Deleted mail reappeared in the mailbox.");
					if (list.Get<bool>("lastPacket")) break;
				}
			}
			else
			{
				await SendAsync(Api.ReadMail(letterId), token);
				var read = await WaitAsync(typeof(SM_MAIL_SERVICE), packet => packet.Get<byte>("serviceId") == 3 && packet.Get<int>("letterId") == letterId, token);
				if (read.Get<long>("itemCount") != itemCount || read.Get<int>("kinah") != kinah)
					throw new InvalidDataException("Mailbox attachment state did not match the transfer.");
			}
		}
		public Task VerifyInventoryAsync(CancellationToken token)
		{
			if (Api.Timing.BlockingActivities.Count != 0) throw new InvalidDataException("Mail left a blocking client interaction.");
			// The runner cross-checks server storage after the shared scenario, before logout.
			return Task.CompletedTask;
		}
	}

	private static async Task VerifyMailAuditAsync(LiveBotOptions options, L0Actor first, L0Actor second, CancellationToken token)
	{
		string path = Path.Combine(options.OutputDirectory, "logs", "gs", "gs.events.jsonl");
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
					if (root.GetProperty("cat").GetString() == "MAIL_LOG") entries.Add(root.GetProperty("msg").GetString()!);
				}
			}
			if (entries.Count == 2)
			{
				foreach (var (sender, recipient) in new[] { (first, second), (second, first) })
					if (entries.Count(message => message.StartsWith($"Player: {sender.Session.CharacterName} sent item {MailScenario.ItemId} [", StringComparison.Ordinal) &&
						message.EndsWith($"(count: {MailScenario.AttachmentCount}) to player {recipient.Session.CharacterName}", StringComparison.Ordinal)) != 1)
						throw new InvalidDataException("Mail audit did not match both attachment transfers.");
				return;
			}
			if (entries.Count > 2) throw new InvalidDataException("Mail produced duplicate audit entries.");
			await Task.Delay(250, token);
		}
		throw new InvalidDataException("Mail audit did not record both item transfers.");
	}
}
