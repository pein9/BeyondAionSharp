using System.Text.Json;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static async Task<int> RunNaturalIshalgenIdentityAsync(
		LiveBotOptions options,
		LiveBotProblemWriter problems,
		CancellationToken token)
	{
		NaturalIshalgenIdentity identity = NaturalIshalgenIdentityScenario.Identity;
		await using var actor = new L0Actor(options, problems, 1, Race.ASMODIANS, bot: "b01",
			account: identity.AccountName, characterName: identity.CharacterName);
		actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "NI-01" });
		try
		{
			var driver = new LiveNaturalIshalgenIdentityDriver(options, actor, identity);
			NaturalIshalgenIdentityResult result = await NaturalIshalgenIdentityScenario.RunAsync(driver, token);
			await WriteNaturalIdentityReceiptAsync(options, identity, result, token);
			actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?>
			{
				["scenario"] = "NI-01",
				["characterId"] = result.CharacterId,
				["createdThisRun"] = result.CreatedThisRun,
			});
			Console.WriteLine($"LIVE NI-01: {(result.CreatedThisRun ? "created" : "reused")} retained ordinary " +
				$"Asmodian Priest {identity.CharacterName} ({result.CharacterId}).");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception)
		{
			Console.Error.WriteLine($"NI-01 failed: {exception}");
			return 1;
		}
	}

	private static async Task WriteNaturalIdentityReceiptAsync(
		LiveBotOptions options,
		NaturalIshalgenIdentity identity,
		NaturalIshalgenIdentityResult result,
		CancellationToken token)
	{
		string path = Path.Combine(options.OutputDirectory, "natural-ishalgen-identity.json");
		await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(new
		{
			schemaVersion = 1,
			run = options.Run,
			accountName = identity.AccountName,
			characterName = identity.CharacterName,
				characterId = result.CharacterId,
				level = result.Level,
			race = identity.Race.ToString(),
			playerClass = identity.PlayerClass.ToString(),
			accessLevel = 0,
			createdThisRun = result.CreatedThisRun,
			retained = true,
		}, new JsonSerializerOptions { WriteIndented = true }), token);
		File.Move(path + ".tmp", path);
	}

	private sealed class LiveNaturalIshalgenIdentityDriver(
		LiveBotOptions options,
		L0Actor actor,
		NaturalIshalgenIdentity identity) : INaturalIshalgenIdentityDriver
	{
		public NaturalIshalgenIdentity Identity => identity;
		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token) =>
			actor.StepAsync(action, operation, token);
		public Task<DecodedBotServerPacket> LoginAsync(CancellationToken token) => actor.Session.LoginCharacterListAsync(token);
		public async Task<DecodedBotServerPacket> CreateAsync(string characterName, PlayerClass playerClass, CancellationToken token)
		{
			await actor.Session.CreateCharacterAsync(characterName, playerClass, token);
			return actor.Session.PacketHistory.Last(packet => packet.PacketType == typeof(SM_CREATE_CHARACTER));
		}
		public Task<DecodedBotServerPacket> ListAsync(CancellationToken token) => actor.Session.ReadCharacterListAsync(token);
		public void SelectCharacter(int characterId, string characterName) => actor.Session.SelectCharacter(characterId, characterName);
		public Task EnterWorldAsync(CancellationToken token) => actor.Session.EnterWorldAsync(token);
		public async Task VerifyOrdinaryOnlineIdentityAsync(int characterId, ushort level, CancellationToken token)
		{
			await actor.Session.SynchronizeAsync(token);
			using var request = new HttpRequestMessage(HttpMethod.Get,
				$"admin/player-state?characterId={characterId}");
			request.Headers.Add("X-Admin-Token", options.AdminToken);
			using var response = await actor.Session.AdminClient.SendAsync(request, token);
			response.EnsureSuccessStatusCode();
			using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
			JsonElement root = document.RootElement;
			JsonElement player = root.GetProperty("player");
			if (!root.GetProperty("ok").GetBoolean() || !root.GetProperty("online").GetBoolean()
				|| player.GetProperty("characterId").GetInt32() != characterId
				|| player.GetProperty("name").GetString() != identity.CharacterName
				|| player.GetProperty("accountName").GetString() != identity.AccountName
				|| player.GetProperty("accessLevel").GetInt32() != 0
				|| player.GetProperty("race").GetString() != identity.Race.ToString()
				|| player.GetProperty("playerClass").GetString() != identity.PlayerClass.ToString()
				|| player.GetProperty("level").GetInt32() != level
				|| player.GetProperty("worldId").GetInt32() != 220010000)
				throw new InvalidDataException("NI-01 online identity is not the retained ordinary pre-Ascension Ishalgen Priest.");
		}
		public async Task QuitAndVerifyOfflineAsync(CancellationToken token)
		{
			await actor.Session.QuitAsync(token);
			await actor.Session.VerifyOfflineAsync(token);
		}
		public Task WaitForReentryAsync(CancellationToken token) => actor.Session.WaitForReentryAsync(token);
	}
}
