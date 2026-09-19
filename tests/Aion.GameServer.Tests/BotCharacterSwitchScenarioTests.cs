using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotCharacterSwitchScenarioTests
{
	private static readonly SwitchCharacter First = new(101, "Switcha", PlayerClass.WARRIOR);
	private static readonly SwitchCharacter Second = new(102, "Switchb", PlayerClass.PRIEST);

	[Fact]
	public void OneConnectionWithNormalQuitAndDistinctEntriesPasses()
	{
		CharacterSwitchScenario.VerifyTranscript(Transcript(), 1, 1, First, Second);
		CharacterSwitchScenario.VerifySelection(Selection(First, Second), First, Second);
	}

	[Theory]
	[InlineData(typeof(SM_KEY))]
	[InlineData(typeof(SM_L2AUTH_LOGIN_CHECK))]
	public void ExtraAuthenticationPacketsCannotMasqueradeAsStayConnected(Type type)
	{
		var packets = Transcript(); packets.Add(new DecodedBotServerPacket(type, new Dictionary<string, object?>()));
		Assert.Throws<InvalidDataException>(() => CharacterSwitchScenario.VerifyTranscript(packets, 1, 1, First, Second));
	}

	[Theory]
	[InlineData(0, 1)]
	[InlineData(1, 2)]
	[InlineData(2, 2)]
	public void TransportRecreationFailsEvenWithoutExtraPackets(int before, int after) =>
		Assert.Throws<InvalidDataException>(() => CharacterSwitchScenario.VerifyTranscript(Transcript(), before, after, First, Second));

	[Fact]
	public void MissingQuitOrEditModeQuitCannotPass()
	{
		var packets = Transcript(); packets.RemoveAt(4);
		Assert.Throws<InvalidDataException>(() => CharacterSwitchScenario.VerifyTranscript(packets, 1, 1, First, Second));
		packets = Transcript(); packets[4] = Packet<SM_QUIT_RESPONSE>(("mode", 2));
		Assert.Throws<InvalidDataException>(() => CharacterSwitchScenario.VerifyTranscript(packets, 1, 1, First, Second));
	}

	[Fact]
	public void EnteringTheFirstCharacterAgainOrWrongClassFails()
	{
		var packets = Transcript(); packets[^1] = Info(First);
		Assert.Throws<InvalidDataException>(() => CharacterSwitchScenario.VerifyTranscript(packets, 1, 1, First, Second));
		packets[^1] = Info(Second with { Class = PlayerClass.WARRIOR });
		Assert.Throws<InvalidDataException>(() => CharacterSwitchScenario.VerifyTranscript(packets, 1, 1, First, Second));
	}

	[Fact]
	public void MissingDuplicatedOrRenamedSelectionCharactersFail()
	{
		Assert.Throws<InvalidDataException>(() => CharacterSwitchScenario.VerifySelection(Selection(First), First, Second));
		Assert.Throws<InvalidDataException>(() => CharacterSwitchScenario.VerifySelection(Selection(First, First), First, Second));
		Assert.Throws<InvalidDataException>(() => CharacterSwitchScenario.VerifySelection(Selection(First, Second with { Name = "Wrong" }), First, Second));
	}

	private static List<DecodedBotServerPacket> Transcript() =>
	[
		Packet<SM_KEY>(), Packet<SM_L2AUTH_LOGIN_CHECK>(), Packet<SM_PLAYER_SPAWN>(), Info(First),
		Packet<SM_QUIT_RESPONSE>(("mode", 1)), Packet<SM_PLAYER_SPAWN>(), Info(Second)
	];
	private static DecodedBotServerPacket Info(SwitchCharacter character) => Packet<SM_PLAYER_INFO>(
		("objectId", character.Id), ("name", character.Name), ("race", (byte)Race.ELYOS), ("playerClass", (byte)character.Class));
	private static DecodedBotServerPacket Selection(params SwitchCharacter[] characters) => Packet<SM_CHARACTER_LIST>(
		("characterCount", (byte)characters.Length), ("characters", characters.Select(c => (IReadOnlyDictionary<string, object?>)
			new Dictionary<string, object?> { ["objectId"] = c.Id, ["name"] = c.Name, ["playerClass"] = (int)c.Class, ["race"] = (int)Race.ELYOS }).ToList()));
	private static DecodedBotServerPacket Packet<T>(params (string Name, object? Value)[] fields) =>
		new(typeof(T), fields.ToDictionary(p => p.Name, p => p.Value));
}
