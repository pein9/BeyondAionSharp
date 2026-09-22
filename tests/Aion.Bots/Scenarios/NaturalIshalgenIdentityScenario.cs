using Aion.Bots.Protocol;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public sealed record NaturalIshalgenIdentity(string AccountName, string CharacterName, Race Race, PlayerClass PlayerClass);

public sealed record NaturalIshalgenIdentityResult(int CharacterId, ushort Level, bool CreatedThisRun);

public interface INaturalIshalgenIdentityDriver
{
	NaturalIshalgenIdentity Identity { get; }
	Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token);
	Task<DecodedBotServerPacket> LoginAsync(CancellationToken token);
	Task<DecodedBotServerPacket> CreateAsync(string characterName, PlayerClass playerClass, CancellationToken token);
	Task<DecodedBotServerPacket> ListAsync(CancellationToken token);
	void SelectCharacter(int characterId, string characterName);
	Task EnterWorldAsync(CancellationToken token);
	Task VerifyOrdinaryOnlineIdentityAsync(int characterId, ushort level, CancellationToken token);
	Task QuitAndVerifyOfflineAsync(CancellationToken token);
	Task WaitForReentryAsync(CancellationToken token);
}

/// <summary>
/// NI-01: create or reuse one stable ordinary Asmodian Priest through normal client packets.
/// The character is intentionally retained; subsequent invocations must select the same id.
/// </summary>
public static class NaturalIshalgenIdentityScenario
{
	public static NaturalIshalgenIdentity Identity { get; } =
		new("niishalgen", "Ishalgenbot", Race.ASMODIANS, PlayerClass.PRIEST);

	public static async Task<NaturalIshalgenIdentityResult> RunAsync(
		INaturalIshalgenIdentityDriver driver,
		CancellationToken token = default)
	{
		NaturalIshalgenIdentityResult first = await EnterAsync(driver, token);
		int characterId = first.CharacterId;
		ushort level = first.Level;
		await driver.StepAsync("quit-without-deleting-character", driver.QuitAndVerifyOfflineAsync, token);
		await driver.StepAsync("honor-reentry-delay", driver.WaitForReentryAsync, token);

		await driver.StepAsync("relogin-and-select-retained-priest", async ct =>
		{
			IReadOnlyList<IReadOnlyDictionary<string, object?>> characters = CharacterList(await driver.LoginAsync(ct));
			if (characters.Count != 1 || ValidateCharacter(characters[0]) != new ObservedNaturalCharacter(characterId, level))
				throw new InvalidDataException("NI-01 relogin did not return the same retained Priest identity.");
			driver.SelectCharacter(characterId, Identity.CharacterName);
		}, token);
		await driver.StepAsync("reenter-and-verify-retained-priest", async ct =>
		{
			await driver.EnterWorldAsync(ct);
			await driver.VerifyOrdinaryOnlineIdentityAsync(characterId, level, ct);
		}, token);
		await driver.StepAsync("final-quit-without-deleting-character", driver.QuitAndVerifyOfflineAsync, token);

		return first;
	}

	/// <summary>Shared create-or-reuse entry for the later natural decision loop.</summary>
	public static async Task<NaturalIshalgenIdentityResult> EnterAsync(
		INaturalIshalgenIdentityDriver driver,
		CancellationToken token = default)
	{
		if (driver.Identity != Identity)
			throw new InvalidDataException("NI-01 must use its stable ordinary account and character identity.");

		int characterId = 0;
		ushort level = 0;
		bool created = false;
		await driver.StepAsync("login-and-create-or-select-priest", async ct =>
		{
			DecodedBotServerPacket list = await driver.LoginAsync(ct);
			IReadOnlyList<IReadOnlyDictionary<string, object?>> characters = CharacterList(list);
			if (characters.Count == 0)
			{
				DecodedBotServerPacket response = await driver.CreateAsync(Identity.CharacterName, Identity.PlayerClass, ct);
				if (response.PacketType != typeof(SM_CREATE_CHARACTER) || response.Get<int>("responseCode") != 0)
					throw new InvalidDataException("NI-01 character creation failed.");
				ObservedNaturalCharacter createdCharacter = ValidateCharacter(response.Get<IReadOnlyDictionary<string, object?>>("character"));
				characterId = createdCharacter.Id;
				level = createdCharacter.Level;
				if (level != 1)
					throw new InvalidDataException("A newly created NI-01 Priest was not level 1.");
				created = true;
				IReadOnlyList<IReadOnlyDictionary<string, object?>> afterCreate = CharacterList(await driver.ListAsync(ct));
				if (afterCreate.Count != 1 || ValidateCharacter(afterCreate[0]) != createdCharacter)
					throw new InvalidDataException("The newly created NI-01 Priest was not the account's sole selectable character.");
			}
			else
			{
				if (characters.Count != 1)
					throw new InvalidDataException("The retained NI-01 account must contain exactly one character.");
				ObservedNaturalCharacter retained = ValidateCharacter(characters[0]);
				characterId = retained.Id;
				level = retained.Level;
			}
			driver.SelectCharacter(characterId, Identity.CharacterName);
		}, token);

		await driver.StepAsync("enter-and-verify-ordinary-priest", async ct =>
		{
			await driver.EnterWorldAsync(ct);
			await driver.VerifyOrdinaryOnlineIdentityAsync(characterId, level, ct);
		}, token);
		return new NaturalIshalgenIdentityResult(characterId, level, created);
	}

	private static IReadOnlyList<IReadOnlyDictionary<string, object?>> CharacterList(DecodedBotServerPacket packet)
	{
		if (packet.PacketType != typeof(SM_CHARACTER_LIST))
			throw new InvalidDataException("NI-01 login did not return a character list.");
		var rows = packet.Get<List<IReadOnlyDictionary<string, object?>>>("characters");
		if (packet.Get<byte>("characterCount") != rows.Count)
			throw new InvalidDataException("NI-01 character-list count disagrees with its rows.");
		return rows;
	}

	private static ObservedNaturalCharacter ValidateCharacter(IReadOnlyDictionary<string, object?> character)
	{
		int id = Get<int>(character, "objectId");
		ushort level = Get<ushort>(character, "level");
		if (id <= 0 || !string.Equals(Get<string>(character, "name"), Identity.CharacterName, StringComparison.Ordinal)
			|| Get<int>(character, "race") != (int)Identity.Race
			|| Get<int>(character, "playerClass") != (int)Identity.PlayerClass
			|| level is < 1 or > 9
			|| Get<int>(character, "deletionTimeSeconds") != 0)
			throw new InvalidDataException("NI-01 found a conflicting, post-boundary, or pending-deletion character identity.");
		return new ObservedNaturalCharacter(id, level);
	}

	private readonly record struct ObservedNaturalCharacter(int Id, ushort Level);

	private static T Get<T>(IReadOnlyDictionary<string, object?> fields, string name) =>
		fields.TryGetValue(name, out object? value) && value is T typed
			? typed
			: throw new InvalidDataException($"NI-01 character field '{name}' was missing or was not {typeof(T).Name}.");
}
