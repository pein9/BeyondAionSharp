using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotCharacterSettingsScenarioTests
{
	[Fact]
	public void CompleteFreshPacketsProveBothPopulatedAndEmptyMacroLists()
	{
		var expected = Expected();
		CharacterSettingsScenario.VerifyFreshPackets(123, Packets(expected), expected);
		expected = expected with { Macros = new Dictionary<byte, string>() };
		CharacterSettingsScenario.VerifyFreshPackets(123, Packets(expected), expected);
	}

	[Theory]
	[InlineData(typeof(SM_CHARACTER_LIST))]
	[InlineData(typeof(SM_PLAYER_INFO))]
	[InlineData(typeof(SM_TITLE_INFO))]
	[InlineData(typeof(SM_MACRO_LIST))]
	[InlineData(typeof(SM_UI_SETTINGS))]
	public void MissingFreshPacketFamiliesCannotPassUsingStaleState(Type missing)
	{
		var expected = Expected();
		Assert.Throws<InvalidDataException>(() => CharacterSettingsScenario.VerifyFreshPackets(123,
			Packets(expected).Where(p => p.PacketType != missing).ToArray(), expected));
	}

	[Fact]
	public void ReappearingDeletedMacrosAndChangedOpaqueUiBytesFailPersistenceVerification()
	{
		var expected = Expected();
		var stale = expected with { Macros = new Dictionary<byte, string> { [1] = "old XML" } };
		Assert.Throws<InvalidDataException>(() => CharacterSettingsScenario.VerifyFreshPackets(123, Packets(stale), expected));
		var extra = expected with { Macros = new Dictionary<byte, string>(expected.Macros) { [7] = "deleted XML" } };
		Assert.Throws<InvalidDataException>(() => CharacterSettingsScenario.VerifyFreshPackets(123, Packets(extra), expected));
		var changed = expected with { UiSettings = new Dictionary<byte, byte[]>(expected.UiSettings) { [1] = [7, 8, 9] } };
		Assert.Throws<InvalidDataException>(() => CharacterSettingsScenario.VerifyFreshPackets(123, Packets(changed), expected));
	}

	[Fact]
	public void CharacterAndWorldAppearanceAndBonusTitleMustAllAgreeAfterReload()
	{
		var expected = Expected();
		var changed = expected with { Appearance = expected.Appearance with { Voice = 3 } };
		Assert.Throws<InvalidDataException>(() => CharacterSettingsScenario.VerifyFreshPackets(123, Packets(changed), expected));
		var packets = Packets(expected);
		packets[1] = Packets(changed)[1];
		Assert.Throws<InvalidDataException>(() => CharacterSettingsScenario.VerifyFreshPackets(123, packets, expected));
		Assert.Throws<InvalidDataException>(() => CharacterSettingsScenario.VerifyFreshPackets(123,
			Packets(expected with { BonusTitle = 1 }), expected));
	}

	private static CharacterSettingsExpected Expected() => new(CharacterSettingsScenario.EditedAppearance("Settings"), 1, 2,
		new Dictionary<byte, string> { [1] = CharacterSettingsScenario.FirstMacro, [12] = CharacterSettingsScenario.OtherMacro },
		new Dictionary<byte, byte[]> { [0] = [1, 2, 0], [1] = [4, 5, 6], [2] = [] });

	private static List<DecodedBotServerPacket> Packets(CharacterSettingsExpected expected)
	{
		var data = expected.Appearance;
		var appearance = new BotCharacterAppearance(data.Voice, data.SkinRgb, data.HairRgb, data.EyeRgb, data.LipRgb,
			Convert.ToHexString(data.AppearanceFeatures), data.Height);
		var fields = new Dictionary<string, object?> { ["objectId"] = 123, ["name"] = data.CharacterName,
			["gender"] = data.Gender, ["race"] = data.Race, ["playerClass"] = data.PlayerClass, ["titleId"] = (int)expected.DisplayTitle,
			["appearance"] = appearance };
		var packets = new List<DecodedBotServerPacket>
		{
			Packet<SM_CHARACTER_LIST>(("characterCount", (byte)1), ("characters", new List<IReadOnlyDictionary<string, object?>> { fields })),
			Packet<SM_PLAYER_INFO>(("objectId", 123), ("appearance", appearance), ("titleId", expected.DisplayTitle)),
			Packet<SM_TITLE_INFO>(("action", (byte)1), ("titleId", expected.DisplayTitle)),
			Packet<SM_TITLE_INFO>(("action", (byte)6), ("bonusTitleId", expected.BonusTitle)),
			Packet<SM_TITLE_INFO>(("action", (byte)0), ("titles", new[] { new BotTitle(1, 0), new BotTitle(2, 0) })),
			Packet<SM_MACRO_LIST>(("playerObjectId", 123), ("clearList", true), ("macros", expected.Macros.Select(p => new BotMacro(p.Key, p.Value)).ToArray()))
		};
		foreach (var (type, value) in expected.UiSettings)
		{
			var padded = new byte[0x1C00]; value.CopyTo(padded, 0);
			packets.Add(Packet<SM_UI_SETTINGS>(("type", type), ("paddedData", padded)));
		}
		return packets;
	}
	private static DecodedBotServerPacket Packet<T>(params (string Key, object? Value)[] fields) =>
		new(typeof(T), fields.ToDictionary(p => p.Key, p => p.Value));
}
