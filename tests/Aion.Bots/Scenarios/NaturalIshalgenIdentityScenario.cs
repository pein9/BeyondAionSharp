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

	/// <summary>Separate ordinary identities let an observer replay the journey without erasing a completed Priest.</summary>
	public static NaturalIshalgenIdentity IdentityForSlot(int slot) => slot switch
	{
		1 => Identity,
		>= 2 and <= 9 => new NaturalIshalgenIdentity($"niishalgen{slot}",
			$"Ishalgenbot{new[] { "", "", "two", "three", "four", "five", "six", "seven", "eight", "nine" }[slot]}",
			Race.ASMODIANS, PlayerClass.PRIEST),
		_ => throw new ArgumentOutOfRangeException(nameof(slot), "Attach identity slot must be 1 through 9."),
	};

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
			if (characters.Count != 1 || ValidateCharacter(characters[0], driver.Identity) != new ObservedNaturalCharacter(characterId, level))
				throw new InvalidDataException("NI-01 relogin did not return the same retained Priest identity.");
			driver.SelectCharacter(characterId, driver.Identity.CharacterName);
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
		NaturalIshalgenIdentity identity = driver.Identity;
		if (!Enumerable.Range(1, 9).Any(slot => IdentityForSlot(slot) == identity))
			throw new InvalidDataException("Natural Ishalgen needs a designated ordinary Priest identity.");

		int characterId = 0;
		ushort level = 0;
		bool created = false;
		await driver.StepAsync("login-and-create-or-select-priest", async ct =>
		{
			DecodedBotServerPacket list = await driver.LoginAsync(ct);
			IReadOnlyList<IReadOnlyDictionary<string, object?>> characters = CharacterList(list);
			if (characters.Count == 0)
			{
				DecodedBotServerPacket response = await driver.CreateAsync(identity.CharacterName, identity.PlayerClass, ct);
				if (response.PacketType != typeof(SM_CREATE_CHARACTER) || response.Get<int>("responseCode") != 0)
					throw new InvalidDataException("NI-01 character creation failed.");
				ObservedNaturalCharacter createdCharacter = ValidateCharacter(response.Get<IReadOnlyDictionary<string, object?>>("character"), identity);
				characterId = createdCharacter.Id;
				level = createdCharacter.Level;
				if (level != 1)
					throw new InvalidDataException("A newly created NI-01 Priest was not level 1.");
				created = true;
				IReadOnlyList<IReadOnlyDictionary<string, object?>> afterCreate = CharacterList(await driver.ListAsync(ct));
				if (afterCreate.Count != 1 || ValidateCharacter(afterCreate[0], identity) != createdCharacter)
					throw new InvalidDataException("The newly created NI-01 Priest was not the account's sole selectable character.");
			}
			else
			{
				if (characters.Count != 1)
					throw new InvalidDataException("The retained NI-01 account must contain exactly one character.");
				ObservedNaturalCharacter retained = ValidateCharacter(characters[0], identity);
				characterId = retained.Id;
				level = retained.Level;
			}
			driver.SelectCharacter(characterId, identity.CharacterName);
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

	private static ObservedNaturalCharacter ValidateCharacter(IReadOnlyDictionary<string, object?> character,
		NaturalIshalgenIdentity identity)
	{
		int id = Get<int>(character, "objectId");
		ushort level = Get<ushort>(character, "level");
		if (id <= 0 || !string.Equals(Get<string>(character, "name"), identity.CharacterName, StringComparison.Ordinal)
			|| Get<int>(character, "race") != (int)identity.Race
			|| Get<int>(character, "playerClass") != (int)identity.PlayerClass
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
