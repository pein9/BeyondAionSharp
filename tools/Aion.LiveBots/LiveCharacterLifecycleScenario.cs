using System.Net;
using System.Text.Json;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static async Task<int> RunL1Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		var actors = CharacterLifecycleScenario.Cases.Select((value, index) =>
			new LiveCharacterLifecycleDriver(options, new L0Actor(options, problems, index + 1, value.Race,
				characterName: "Livelife" + (char)('a' + index)), value)).ToArray();
		try
		{
			foreach (var driver in actors) driver.Actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "L1" });
			await CharacterLifecycleScenario.RunAsync(actors, token);
			foreach (var driver in actors) driver.Actor.Trace.WriteAction(driver.Actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "L1" });
			Console.WriteLine("LIVE L1: all twelve race/starting-class creations, grace-period restores and durable deletions passed.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception) { Console.Error.WriteLine($"L1 failed: {exception}"); return 1; }
		finally { foreach (var driver in actors) await driver.Actor.DisposeAsync(); }
	}

	private sealed class LiveCharacterLifecycleDriver(LiveBotOptions options, L0Actor actor, CharacterLifecycleCase value) : ICharacterLifecycleDriver
	{
		public L0Actor Actor => actor;
		public CharacterLifecycleCase Case => value;
		public int CharacterId => actor.Session.CharacterId;
		public string CharacterName => actor.Session.CharacterName;
		public DateTimeOffset Now => DateTimeOffset.UtcNow;
		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token) => actor.StepAsync(action, operation, token);
		public Task<DecodedBotServerPacket> LoginAsync(CancellationToken token) => actor.Session.LoginCharacterListAsync(token);
		public async Task<DecodedBotServerPacket> CreateAsync(CancellationToken token)
		{
			await actor.Session.CreateCharacterAsync(token, Case.Class);
			return actor.Session.PacketHistory.Last(p => p.PacketType == typeof(SM_CREATE_CHARACTER));
		}
		public Task<DecodedBotServerPacket> ListAsync(CancellationToken token) => actor.Session.ReadCharacterListAsync(token);
		public async Task<DecodedBotServerPacket> DeleteAsync(CancellationToken token)
		{
			await actor.Session.DeleteCharacterAsync(token); return await actor.Session.WaitForPacketAsync(typeof(SM_DELETE_CHARACTER), token);
		}
		public async Task<DecodedBotServerPacket> RestoreAsync(CancellationToken token)
		{
			await actor.Session.RestoreCharacterAsync(token); return await actor.Session.WaitForPacketAsync(typeof(SM_RESTORE_CHARACTER), token);
		}
		public async Task CloseAsync(CancellationToken token)
		{
			await actor.Session.CloseAsync(token);
			// Allow the normal GS -> LS logout notification to arrive before a fresh login-server handshake.
			await Task.Delay(TimeSpan.FromMilliseconds(250), token);
		}
		public Task DelayAsync(TimeSpan delay, CancellationToken token) => Task.Delay(delay, token);
		public async Task VerifyStoredAsync(bool exists, CancellationToken token)
		{
			using var client = new HttpClient { BaseAddress = options.AdminBaseUri };
			using var request = new HttpRequestMessage(HttpMethod.Get, $"admin/player-state?characterId={CharacterId}");
			request.Headers.Add("X-Admin-Token", options.AdminToken);
			using var response = await client.SendAsync(request, token);
			await using var content = await response.Content.ReadAsStreamAsync(token);
			using var document = await JsonDocument.ParseAsync(content, cancellationToken: token);
			var root = document.RootElement;
			if (!exists)
			{
				if (response.StatusCode != HttpStatusCode.NotFound || root.GetProperty("error").GetString() != "Character was not found.")
					throw new InvalidDataException("Deleted character still exists in the persistence oracle.");
				return;
			}
			response.EnsureSuccessStatusCode();
			var saved = root.GetProperty("lastKnown");
			if (!root.GetProperty("ok").GetBoolean() || root.GetProperty("online").GetBoolean()
				|| saved.GetProperty("characterId").GetInt32() != CharacterId || saved.GetProperty("name").GetString() != CharacterName
				|| saved.GetProperty("race").GetString() != Case.Race.ToString() || saved.GetProperty("playerClass").GetString() != Case.Class.ToString()
				|| saved.GetProperty("level").GetInt32() != 1)
				throw new InvalidDataException("Character persistence oracle differs from the creation/selection packets.");
		}
	}
}
