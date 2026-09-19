using Aion.Bots.Protocol;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public sealed record CharacterLifecycleCase(Race Race, PlayerClass Class);

public interface ICharacterLifecycleDriver
{
	CharacterLifecycleCase Case { get; }
	int CharacterId { get; }
	string CharacterName { get; }
	DateTimeOffset Now { get; }
	Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token);
	Task<DecodedBotServerPacket> LoginAsync(CancellationToken token);
	Task<DecodedBotServerPacket> CreateAsync(CancellationToken token);
	Task<DecodedBotServerPacket> ListAsync(CancellationToken token);
	Task<DecodedBotServerPacket> DeleteAsync(CancellationToken token);
	Task<DecodedBotServerPacket> RestoreAsync(CancellationToken token);
	Task CloseAsync(CancellationToken token);
	Task DelayAsync(TimeSpan delay, CancellationToken token);
	Task VerifyStoredAsync(bool exists, CancellationToken token);
}

/// <summary>L1: normal character-selection packets, all races/starter classes, and the real five-minute grace period.</summary>
public static class CharacterLifecycleScenario
{
	public static IReadOnlyList<CharacterLifecycleCase> Cases { get; } = Array.AsReadOnly(
		new[] { Race.ELYOS, Race.ASMODIANS }.SelectMany(race =>
			new[] { PlayerClass.WARRIOR, PlayerClass.SCOUT, PlayerClass.MAGE, PlayerClass.PRIEST, PlayerClass.ENGINEER, PlayerClass.ARTIST }
			.Select(pc => new CharacterLifecycleCase(race, pc))).ToArray());

	public static async Task RunAsync(IReadOnlyList<ICharacterLifecycleDriver> actors, CancellationToken token = default)
	{
		Require(actors.Count == 12 && actors.Select(a => a.Case).ToHashSet().SetEquals(Cases), "L1 requires all twelve distinct race/starting-class combinations.");
		long lastDeadline = 0;
		foreach (var actor in actors)
		{
			await actor.StepAsync($"create-{actor.Case.Race}-{actor.Case.Class}", async ct =>
			{
				AssertEmpty(await actor.LoginAsync(ct));
				var created = await actor.CreateAsync(ct);
				Require(created.PacketType == typeof(SM_CREATE_CHARACTER) && created.Get<int>("responseCode") == 0, "Character creation failed.");
				AssertCharacter(actor, created.Get<IReadOnlyDictionary<string, object?>>("character"), 0);
				AssertList(actor, await actor.ListAsync(ct), 0);
				await actor.VerifyStoredAsync(true, ct);
			}, token);

			await actor.StepAsync("delete-reconnect-and-restore-within-grace", async ct =>
			{
				int deadline = await DeleteAsync(actor, ct);
				AssertPersistedDeletion(actor, await ReconnectAsync(actor, ct), deadline);
				AssertRestore(actor, await actor.RestoreAsync(ct), true);
				AssertList(actor, await actor.ListAsync(ct), 0);
				AssertList(actor, await ReconnectAsync(actor, ct), 0);
				await actor.VerifyStoredAsync(true, ct);
			}, token);

			await actor.StepAsync("delete-again-and-persist-original-deadline", async ct =>
			{
				int deadline = await DeleteAsync(actor, ct);
				var repeat = await actor.DeleteAsync(ct);
				Require(repeat.Get<int>("responseCode") == 0 && repeat.Get<int>("playerObjId") == actor.CharacterId
					&& repeat.Get<int>("deletionTime") == deadline, "Repeated deletion must not extend the deadline.");
				int storedDeadline = AssertPersistedDeletion(actor, await ReconnectAsync(actor, ct), deadline);
				await actor.VerifyStoredAsync(true, ct);
				lastDeadline = Math.Max(lastDeadline, storedDeadline);
				await actor.CloseAsync(ct);
			}, token);
		}
		Require(actors.Select(a => a.CharacterId).Distinct().Count() == 12, "Creation reused a character id.");

		// All accounts wait together, disconnected, with unchanged production deletion settings.
		// The wire deadline is truncated to seconds, so cross the following second as well.
		var end = DateTimeOffset.FromUnixTimeSeconds(lastDeadline + 1);
		while (actors[0].Now < end)
		{
			var delay = end - actors[0].Now;
			if (delay > TimeSpan.FromSeconds(5)) delay = TimeSpan.FromSeconds(5);
			await actors[0].StepAsync("honor-character-deletion-grace", ct => actors[0].DelayAsync(delay, ct), token);
		}
		foreach (var actor in actors)
			await actor.StepAsync("expire-delete-reject-restore-and-confirm-durable-removal", async ct =>
			{
				AssertEmpty(await actor.LoginAsync(ct)); // Real account authentication performs the expired-character cleanup.
				AssertRestore(actor, await actor.RestoreAsync(ct), false);
				await actor.VerifyStoredAsync(false, ct);
				AssertEmpty(await ReconnectAsync(actor, ct));
				await actor.VerifyStoredAsync(false, ct);
				await actor.CloseAsync(ct);
			}, token);
	}

	private static async Task<int> DeleteAsync(ICharacterLifecycleDriver actor, CancellationToken token)
	{
		// A normal client pause also exercises fractional-second persistence in virtual time.
		await actor.DelayAsync(TimeSpan.FromMilliseconds(750), token);
		long before = actor.Now.ToUnixTimeSeconds();
		var packet = await actor.DeleteAsync(token);
		Require(packet.PacketType == typeof(SM_DELETE_CHARACTER) && packet.Get<int>("responseCode") == 0
			&& packet.Get<int>("playerObjId") == actor.CharacterId, "Deletion did not identify the requested character.");
		int deadline = packet.Get<int>("deletionTime");
		Require(deadline >= before + 299 && deadline <= actor.Now.ToUnixTimeSeconds() + 301, "L1 must use the normal five-minute deletion grace period.");
		AssertList(actor, await actor.ListAsync(token), deadline);
		return deadline;
	}
	private static async Task<DecodedBotServerPacket> ReconnectAsync(ICharacterLifecycleDriver actor, CancellationToken token)
	{
		await actor.CloseAsync(token);
		return await actor.LoginAsync(token);
	}
	private static void AssertRestore(ICharacterLifecycleDriver actor, DecodedBotServerPacket packet, bool success) =>
		Require(packet.PacketType == typeof(SM_RESTORE_CHARACTER) && packet.Get<int>("chaOid") == actor.CharacterId
			&& packet.Get<int>("responseCode") == (success ? 0 : 0x10) && packet.Get<bool>("success") == success, "Unexpected character restoration result.");
	private static void AssertEmpty(DecodedBotServerPacket packet) =>
		Require(packet.PacketType == typeof(SM_CHARACTER_LIST) && packet.Get<byte>("characterCount") == 0
			&& packet.Get<List<IReadOnlyDictionary<string, object?>>>("characters").Count == 0, "Character list should be empty.");
	private static void AssertList(ICharacterLifecycleDriver actor, DecodedBotServerPacket packet, int deadline)
	{
		var characters = packet.Get<List<IReadOnlyDictionary<string, object?>>>("characters");
		Require(packet.Get<byte>("characterCount") == 1 && characters.Count == 1, "Expected exactly one character.");
		AssertCharacter(actor, characters[0], deadline);
	}
	private static int AssertPersistedDeletion(ICharacterLifecycleDriver actor, DecodedBotServerPacket packet, int wireDeadline)
	{
		var characters = packet.Get<List<IReadOnlyDictionary<string, object?>>>("characters");
		Require(packet.Get<byte>("characterCount") == 1 && characters.Count == 1, "Pending deletion disappeared before its deadline.");
		int storedDeadline = (int)characters[0]["deletionTimeSeconds"]!;
		// players.deletion_date is TIMESTAMP(0): MySQL rounds fractional seconds on storage, whereas
		// SM_DELETE_CHARACTER truncates the in-memory milliseconds. Only a reload may add this one second.
		Require(storedDeadline == wireDeadline || storedDeadline == wireDeadline + 1,
			$"Persisted deletion deadline {storedDeadline} differs from wire deadline {wireDeadline} beyond TIMESTAMP(0) precision.");
		AssertCharacter(actor, characters[0], storedDeadline);
		return storedDeadline;
	}
	private static void AssertCharacter(ICharacterLifecycleDriver actor, IReadOnlyDictionary<string, object?> character, int deadline) =>
		Require(actor.CharacterId > 0 && (int)character["objectId"]! == actor.CharacterId && (string)character["name"]! == actor.CharacterName
			&& (int)character["race"]! == (int)actor.Case.Race && (int)character["playerClass"]! == (int)actor.Case.Class
			&& (ushort)character["level"]! == 1 && (int)character["deletionTimeSeconds"]! == deadline,
			$"Incorrect character for {actor.Case}; expected id={actor.CharacterId}, name={actor.CharacterName}, level=1, deadline={deadline}; " +
			$"received {System.Text.Json.JsonSerializer.Serialize(character)}.");
	private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
