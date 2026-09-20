using System.Text.Json;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.GameServer.Model;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static async Task<int> RunL3Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		await using var actor = new L0Actor(options, problems, 1, Race.ELYOS, characterName: "Liveswitcha");
		try
		{
			actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "L3" });
			await CharacterSwitchScenario.RunAsync(new LiveCharacterSwitchDriver(options, actor), "Liveswitcha", "Liveswitchb", token);
			await actor.StepAsync("quit-second-character-and-verify-offline", async ct =>
			{
				await actor.Session.QuitAsync(ct); await actor.Session.VerifyOfflineAsync(ct);
			}, token);
			actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "L3" });
			Console.WriteLine("LIVE L3: one connection/authentication, two created characters, persisted first-character movement and second-character entry passed.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception) { Console.Error.WriteLine($"L3 failed: {exception}"); return 1; }
	}

	private sealed class LiveCharacterSwitchDriver(LiveBotOptions options, L0Actor actor) : ICharacterSwitchDriver
	{
		public IReadOnlyList<DecodedBotServerPacket> Packets => actor.Session.PacketHistory;
		public int ConnectionGeneration => actor.Session.ConnectionGeneration;
		public int? SelfObjectId => actor.Session.Api.World.SelfObjectId;
		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token) => actor.StepAsync(action, operation, token);
		public Task LoginAsync(CancellationToken token) => actor.Session.LoginAndAuthenticateAsync(token);
		public async Task<int> CreateAsync(string name, PlayerClass playerClass, CancellationToken token)
		{
			await actor.Session.CreateCharacterAsync(name, playerClass, token); return actor.Session.CharacterId;
		}
		public Task<DecodedBotServerPacket> ListAsync(CancellationToken token) => actor.Session.ReadCharacterListAsync(token);
		public async Task EnterAsync(SwitchCharacter character, CancellationToken token)
		{
			actor.Session.SelectCharacter(character.Id, character.Name);
			await actor.Session.EnterWorldAsync(token); await actor.Session.SynchronizeAsync(token);
			await VerifyStateAsync(character, true, token);
		}
		public Task WalkAsync(CancellationToken token) => actor.Session.WalkTenMetersAsync(token);
		public Task ReturnToSelectionAsync(CancellationToken token) => actor.Session.ReturnToSelectionAsync(false, token);
		public Task VerifyOfflineAsync(CancellationToken token) => actor.Session.VerifyOfflineAsync(token);
		public Task WaitForReentryAsync(CancellationToken token) => actor.Session.WaitForReentryAsync(token);
		public async Task VerifySwitchedAsync(SwitchCharacter first, SwitchCharacter second, CancellationToken token)
		{
			await VerifyStateAsync(first, false, token); await VerifyStateAsync(second, true, token);
		}
		private async Task VerifyStateAsync(SwitchCharacter character, bool online, CancellationToken token)
		{
			using var request = new HttpRequestMessage(HttpMethod.Get, $"admin/player-state?characterId={character.Id}");
			request.Headers.Add("X-Admin-Token", options.AdminToken);
			using var response = await actor.Session.AdminClient.SendAsync(request, token); response.EnsureSuccessStatusCode();
			await using var content = await response.Content.ReadAsStreamAsync(token);
			using var document = await JsonDocument.ParseAsync(content, cancellationToken: token);
			var root = document.RootElement;
			var player = root.GetProperty(online ? "player" : "lastKnown");
			if (!root.GetProperty("ok").GetBoolean() || root.GetProperty("online").GetBoolean() != online
				|| player.GetProperty("characterId").GetInt32() != character.Id || player.GetProperty("name").GetString() != character.Name
				|| player.GetProperty("playerClass").GetString() != character.Class.ToString()
				|| (online && player.GetProperty("accessLevel").GetInt32() != 0))
				throw new InvalidDataException("Character-switch persistence oracle has the wrong identity/class/online state or staff access.");
		}
	}
}
