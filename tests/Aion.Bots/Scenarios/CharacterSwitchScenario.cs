using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public sealed record SwitchCharacter(int Id, string Name, PlayerClass Class);

public interface ICharacterSwitchDriver
{
	IReadOnlyList<DecodedBotServerPacket> Packets { get; }
	int ConnectionGeneration { get; }
	int? SelfObjectId { get; }
	Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token);
	Task LoginAsync(CancellationToken token);
	Task<int> CreateAsync(string name, PlayerClass playerClass, CancellationToken token);
	Task<DecodedBotServerPacket> ListAsync(CancellationToken token);
	Task EnterAsync(SwitchCharacter character, CancellationToken token);
	Task WalkAsync(CancellationToken token);
	Task ReturnToSelectionAsync(CancellationToken token);
	Task VerifyOfflineAsync(CancellationToken token);
	Task WaitForReentryAsync(CancellationToken token);
	Task VerifySwitchedAsync(SwitchCharacter first, SwitchCharacter second, CancellationToken token);
}

/// <summary>L3 uses one authentication and one game transport for both characters. No director or setup mutation.</summary>
public static class CharacterSwitchScenario
{
	public static async Task RunAsync(ICharacterSwitchDriver driver, string firstName, string secondName, CancellationToken token)
	{
		await driver.StepAsync("login-once", driver.LoginAsync, token);
		int generation = driver.ConnectionGeneration;
		SwitchCharacter first = new(0, firstName, PlayerClass.WARRIOR), second = new(0, secondName, PlayerClass.PRIEST);
		await driver.StepAsync("create-two-distinct-characters", async ct =>
		{
			first = first with { Id = await driver.CreateAsync(first.Name, first.Class, ct) };
			second = second with { Id = await driver.CreateAsync(second.Name, second.Class, ct) };
			if (first.Id <= 0 || second.Id <= 0 || first.Id == second.Id) throw new InvalidDataException("Character creation did not return distinct identities.");
			VerifySelection(await driver.ListAsync(ct), first, second);
		}, token);
		int firstStart = driver.Packets.Count;
		await driver.StepAsync("enter-first-character", async ct =>
		{
			await driver.EnterAsync(first, ct);
			VerifyEntry(driver.Packets.Skip(firstStart).ToArray(), first);
			if (driver.SelfObjectId != first.Id) throw new InvalidDataException("Bot perception did not select the first character.");
		}, token);
		var firstSpawn = driver.Packets.Skip(firstStart).Single(p => p.PacketType == typeof(SM_PLAYER_SPAWN));
		var savedPosition = new BotPosition(firstSpawn.Get<float>("x") + 10, firstSpawn.Get<float>("y"), firstSpawn.Get<float>("z"), 0);
		await driver.StepAsync("walk-first-character-ten-meters", driver.WalkAsync, token);
		await driver.StepAsync("stay-connected-quit-and-confirm-offline", async ct =>
		{
			await driver.ReturnToSelectionAsync(ct);
			await driver.VerifyOfflineAsync(ct);
			var list = await driver.ListAsync(ct);
			VerifySelection(list, first, second);
			var row = list.Get<List<IReadOnlyDictionary<string, object?>>>("characters").Single(c => (int)c["objectId"]! == first.Id);
			if ((int)row["mapId"]! != firstSpawn.Get<int>("worldId")
				|| Math.Abs((float)row["x"]! - savedPosition.X) > .01f
				|| Math.Abs((float)row["y"]! - savedPosition.Y) > .01f
				|| Math.Abs((float)row["z"]! - savedPosition.Z) > .01f)
				throw new InvalidDataException("Selection screen did not preserve the first character's real movement.");
		}, token);
		await driver.StepAsync("honor-reentry-delay", driver.WaitForReentryAsync, token);
		int secondStart = driver.Packets.Count;
		await driver.StepAsync("enter-different-character-on-same-connection", async ct =>
		{
			await driver.EnterAsync(second, ct);
			VerifyEntry(driver.Packets.Skip(secondStart).ToArray(), second);
			if (driver.SelfObjectId != second.Id) throw new InvalidDataException("Bot perception retained the first character's identity.");
		}, token);
		await driver.StepAsync("verify-first-offline-second-online", async ct =>
		{
			await driver.VerifySwitchedAsync(first, second, ct);
			VerifyTranscript(driver.Packets, generation, driver.ConnectionGeneration, first, second);
		}, token);
	}

	public static void VerifySelection(DecodedBotServerPacket list, SwitchCharacter first, SwitchCharacter second)
	{
		var rows = list.Get<List<IReadOnlyDictionary<string, object?>>>("characters");
		if (list.PacketType != typeof(SM_CHARACTER_LIST) || list.Get<byte>("characterCount") != 2 || rows.Count != 2)
			throw new InvalidDataException("The same account must list exactly two characters.");
		foreach (var expected in new[] { first, second })
		{
			var matches = rows.Where(c => (int)c["objectId"]! == expected.Id).ToArray();
			if (matches.Length != 1 || (string)matches[0]["name"]! != expected.Name
				|| (int)matches[0]["playerClass"]! != (int)expected.Class || (int)matches[0]["race"]! != (int)Race.ELYOS)
				throw new InvalidDataException("Selection identity/class/race differs from the created characters.");
		}
	}

	public static void VerifyEntry(IReadOnlyList<DecodedBotServerPacket> packets, SwitchCharacter expected)
	{
		var infos = packets.Where(p => p.PacketType == typeof(SM_PLAYER_INFO)).ToArray();
		if (packets.Count(p => p.PacketType == typeof(SM_PLAYER_SPAWN)) != 1 || infos.Length != 1
			|| infos[0].Get<int>("objectId") != expected.Id || infos[0].Get<string>("name") != expected.Name
			|| infos[0].Get<byte>("playerClass") != (byte)expected.Class || infos[0].Get<byte>("race") != (byte)Race.ELYOS)
			throw new InvalidDataException("Fresh world entry does not identify the selected character.");
	}

	public static void VerifyTranscript(IReadOnlyList<DecodedBotServerPacket> packets, int initialGeneration, int finalGeneration,
		SwitchCharacter first, SwitchCharacter second)
	{
		if (initialGeneration != 1 || finalGeneration != initialGeneration
			|| packets.Count(p => p.PacketType == typeof(SM_KEY)) != 1
			|| packets.Count(p => p.PacketType == typeof(SM_L2AUTH_LOGIN_CHECK)) != 1)
			throw new InvalidDataException("Character switching reconnected or reauthenticated the game transport.");
		var lifecycle = packets.Where(p => p.PacketType == typeof(SM_PLAYER_SPAWN) || p.PacketType == typeof(SM_PLAYER_INFO)
			|| p.PacketType == typeof(SM_QUIT_RESPONSE)).ToArray();
		if (lifecycle.Length != 5 || lifecycle[2].PacketType != typeof(SM_QUIT_RESPONSE) || lifecycle[2].Get<int>("mode") != 1)
			throw new InvalidDataException("Expected entry, normal stay-connected quit, then a second entry.");
		VerifyEntry(lifecycle.Take(2).ToArray(), first);
		VerifyEntry(lifecycle.Skip(3).ToArray(), second);
	}
}
