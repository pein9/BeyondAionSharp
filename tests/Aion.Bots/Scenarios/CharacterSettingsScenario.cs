using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public sealed record CharacterSettingsExpected(CharacterCreationData Appearance, ushort DisplayTitle, ushort BonusTitle,
	IReadOnlyDictionary<byte, string> Macros, IReadOnlyDictionary<byte, byte[]> UiSettings);

public interface ICharacterSettingsDriver
{
	BotApi Api { get; }
	int CharacterId { get; }
	string CharacterName { get; }
	Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token);
	Task<int> PrepareAsync(CancellationToken token);
	Task SendAsync(BotClientPacket packet, CancellationToken token);
	Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token);
	Task SynchronizeAsync(CancellationToken token);
	Task ReturnToEditScreenAsync(CancellationToken token);
	Task EditAndEnterAsync(CharacterCreationData appearance, CancellationToken token);
	Task<IReadOnlyList<DecodedBotServerPacket>> ReloginAsync(CancellationToken token);
	Task VerifyStoredAsync(CharacterSettingsExpected expected, CancellationToken token);
}

/// <summary>L2: real surgeon/ticket flow and durable appearance, titles, macros and three opaque UI-setting blobs.</summary>
public static class CharacterSettingsScenario
{
	public const int SurgeonMap = 110010000, SurgeonNpc = 203880, SurgeryTicket = 169650000;
	public static BotPosition SurgeonPosition { get; } = new(1942.78f, 1560.15f, 590.363f, 30);
	public const string FirstMacro = "<macro><name>L2 café</name><command>/where</command></macro>";
	public const string UpdatedMacro = "<macro><name>L2 revised</name><command>/where</command></macro>";
	public const string OtherMacro = "<macro><name>L2 other</name><command>/time</command></macro>";

	public static CharacterCreationData EditedAppearance(string name)
	{
		byte[] features = Enumerable.Range(0, CharacterCreationData.AppearanceFeatureLength).Select(i => (byte)(20 + i)).ToArray();
		features[0] = 1; features[1] = 2; features[2] = 0; features[3] = 0;
		features[6] = 5; features[44] = 0; features[49] = 0; features[50] = 0; features[51] = 0;
		return new CharacterCreationData { CharacterName = name, Race = (int)Race.ELYOS, Gender = 0,
			PlayerClass = (int)PlayerClass.WARRIOR, Voice = 2, SkinRgb = 0x00DECDBA, HairRgb = 0x002E1A09,
			EyeRgb = 0x005599BB, LipRgb = 0x00996677, Height = 1.08f, AppearanceFeatures = features };
	}

	public static async Task RunAsync(ICharacterSettingsDriver driver, CancellationToken token = default)
	{
		int npc = 0;
		await driver.StepAsync("prepare-ticket-titles-and-approach-existing-surgeon", async ct =>
		{
			npc = await driver.PrepareAsync(ct); await driver.SynchronizeAsync(ct);
			Require(driver.Api.World.Inventory.Values.Single(i => i.ItemId == SurgeryTicket).Count == 1, "L2 needs exactly one surgery ticket.");
		}, token);
		var expectedInventory = driver.Api.World.Inventory.ToDictionary();
		var ticket = expectedInventory.Values.Single(i => i.ItemId == SurgeryTicket);
		var expected = new CharacterSettingsExpected(EditedAppearance(driver.CharacterName), 1, 2,
			new Dictionary<byte, string> { [1] = FirstMacro, [7] = OtherMacro, [12] = OtherMacro },
			new Dictionary<byte, byte[]> { [0] = [1, 0, 2, 3, 171, 205, 0], [1] = [12, 4, 0, 0, 3, 1, 2, 0], [2] = [8, 0, 7, 6, 5] });
		await driver.StepAsync("select-display-and-bonus-titles", ct => SetTitlesAsync(driver, expected, ct), token);
		await driver.StepAsync("create-three-macro-slots-and-save-ui-settings", async ct =>
		{
			foreach (var (id, xml) in expected.Macros) await CreateMacroAsync(driver, id, xml, ct);
			await SaveSettingsAsync(driver, expected.UiSettings, ct);
		}, token);
		await driver.StepAsync("open-ticketed-surgery-and-enter-character-edit-screen", async ct =>
		{
			await driver.SendAsync(driver.Api.Target(npc), ct);
			await driver.SendAsync(driver.Api.TalkTo(npc), ct);
			await driver.WaitAsync(typeof(SM_DIALOG_WINDOW), p => p.Get<int>("targetObjectId") == npc, ct);
			await driver.SendAsync(driver.Api.SelectDialog(npc, DialogAction.EDIT_CHARACTER_ALL), ct);
			var window = await driver.WaitAsync(typeof(SM_PLASTIC_SURGERY), p => p.Get<int>("playerObjId") == driver.CharacterId, ct);
			Require(window.Get<bool>("hasTicket") && !window.Get<bool>("isGenderSwitch"), "Surgeon did not offer ticketed appearance editing.");
			await driver.ReturnToEditScreenAsync(ct);
		}, token);
		await driver.StepAsync("apply-edited-appearance-and-consume-exactly-one-ticket", async ct =>
		{
			await driver.EditAndEnterAsync(expected.Appearance, ct); await driver.SynchronizeAsync(ct);
			expectedInventory.Remove(ticket.ObjectId);
			AssertInventory(driver, expectedInventory);
		}, token);
		await VerifyRelogAsync(driver, expected, expectedInventory, token);

		expected = expected with { DisplayTitle = 2, BonusTitle = 1,
			Macros = new Dictionary<byte, string> { [1] = UpdatedMacro, [12] = OtherMacro },
			UiSettings = new Dictionary<byte, byte[]> { [0] = [9, 8, 7, 0], [1] = [3, 0, 2, 1], [2] = [] } };
		await driver.StepAsync("delete-middle-macro-update-first-and-replace-settings", async ct =>
		{
			await DeleteMacroAsync(driver, 7, ct); await CreateMacroAsync(driver, 1, UpdatedMacro, ct);
			await SetTitlesAsync(driver, expected, ct); await SaveSettingsAsync(driver, expected.UiSettings, ct);
		}, token);
		await VerifyRelogAsync(driver, expected, expectedInventory, token);
		await driver.StepAsync("delete-all-remaining-macros", async ct =>
		{
			await DeleteMacroAsync(driver, 1, ct); await DeleteMacroAsync(driver, 12, ct);
		}, token);
		expected = expected with { Macros = new Dictionary<byte, string>() };
		await VerifyRelogAsync(driver, expected, expectedInventory, token);
	}

	private static async Task VerifyRelogAsync(ICharacterSettingsDriver driver, CharacterSettingsExpected expected,
		Dictionary<int, BotInventoryItem> inventory, CancellationToken token)
	{
		var packets = await driver.ReloginAsync(token);
		await driver.StepAsync("verify-fresh-character-settings-and-persistence", async ct =>
		{
			VerifyFreshPackets(driver.CharacterId, packets, expected); AssertInventory(driver, inventory);
			await driver.VerifyStoredAsync(expected, ct);
		}, token);
	}

	public static void VerifyFreshPackets(int characterId, IReadOnlyList<DecodedBotServerPacket> packets, CharacterSettingsExpected expected)
	{
		var lists = packets.Where(p => p.PacketType == typeof(SM_CHARACTER_LIST)).ToArray();
		Require(lists.Length == 1, "Need one fresh character-selection list, not cached settings.");
		var characters = lists[0].Get<List<IReadOnlyDictionary<string, object?>>>("characters");
		Require(characters.Count == 1 && lists[0].Get<byte>("characterCount") == 1, "Unexpected character-selection roster.");
		var character = characters[0];
		Require((int)character["objectId"]! == characterId && (string)character["name"]! == expected.Appearance.CharacterName
			&& (int)character["gender"]! == expected.Appearance.Gender && (int)character["race"]! == expected.Appearance.Race
			&& (int)character["playerClass"]! == expected.Appearance.PlayerClass && (int)character["titleId"]! == expected.DisplayTitle,
			"Character selection did not preserve identity and display title.");
		var appearance = new BotCharacterAppearance(expected.Appearance.Voice, expected.Appearance.SkinRgb, expected.Appearance.HairRgb,
			expected.Appearance.EyeRgb, expected.Appearance.LipRgb, Convert.ToHexString(expected.Appearance.AppearanceFeatures), expected.Appearance.Height);
		Require((BotCharacterAppearance)character["appearance"]! == appearance, "Saved selection appearance differs from the submitted edit.");
		var info = packets.LastOrDefault(p => p.PacketType == typeof(SM_PLAYER_INFO) && p.Get<int>("objectId") == characterId)
			?? throw new InvalidDataException("Missing self appearance after entering the world.");
		Require(info.Get<BotCharacterAppearance>("appearance") == appearance && info.Get<ushort>("titleId") == expected.DisplayTitle,
			"In-world appearance/title differs from the saved character.");
		var titles = packets.Where(p => p.PacketType == typeof(SM_TITLE_INFO)).ToArray();
		Require(titles.Any(p => p.Get<byte>("action") == 1 && p.Get<ushort>("titleId") == expected.DisplayTitle), "Missing persisted display-title receipt.");
		Require(titles.Any(p => p.Get<byte>("action") == 6 && p.Get<ushort>("bonusTitleId") == expected.BonusTitle), "Missing persisted bonus-title receipt.");
		var catalog = titles.LastOrDefault(p => p.Get<byte>("action") == 0)?.Get<BotTitle[]>("titles")
			?? throw new InvalidDataException("Missing title catalog.");
		Require(catalog.Length == 2 && catalog.Select(t => t.Id).Order().SequenceEqual(new[] { 1, 2 }), "Title ownership did not persist.");
		var macros = packets.Where(p => p.PacketType == typeof(SM_MACRO_LIST)).ToArray();
		Require(macros.Length > 0 && macros[0].Get<bool>("clearList") && macros.Skip(1).All(p => !p.Get<bool>("clearList")), "Missing/invalid fresh macro-list pages.");
		Require(macros.All(p => p.Get<int>("playerObjectId") == characterId), "Macro pages belong to another character.");
		var rows = macros.SelectMany(p => p.Get<BotMacro[]>("macros")).OrderBy(m => m.Id).ToArray();
		Require(rows.SequenceEqual(expected.Macros.OrderBy(p => p.Key).Select(p => new BotMacro(p.Key, p.Value))), "Saved macros differ, including deleted slots or XML content.");
		var settings = packets.Where(p => p.PacketType == typeof(SM_UI_SETTINGS)).ToArray();
		Require(settings.Length == 3 && settings.Select(p => p.Get<byte>("type")).Distinct().Count() == 3, "Expected all three fresh UI-setting blobs.");
		foreach (var packet in settings)
		{
			byte type = packet.Get<byte>("type");
			Require(expected.UiSettings.ContainsKey(type), "Unknown settings type.");
			var sent = expected.UiSettings[type]; var padded = packet.Get<byte[]>("paddedData");
			Require(padded.Length == 0x1C00 && padded.Take(sent.Length).SequenceEqual(sent) && padded.Skip(sent.Length).All(b => b == 0),
				$"UI setting {type} did not preserve bytes and padding across relog.");
		}
	}

	private static async Task SetTitlesAsync(ICharacterSettingsDriver driver, CharacterSettingsExpected expected, CancellationToken token)
	{
		await driver.SendAsync(GameClientPackets.SetDisplayTitle(expected.DisplayTitle), token);
		await driver.WaitAsync(typeof(SM_TITLE_INFO), p => p.Get<byte>("action") == 1 && p.Get<ushort>("titleId") == expected.DisplayTitle, token);
		await driver.SendAsync(GameClientPackets.SetBonusTitle(expected.BonusTitle), token);
		await driver.WaitAsync(typeof(SM_TITLE_INFO), p => p.Get<byte>("action") == 6 && p.Get<ushort>("bonusTitleId") == expected.BonusTitle, token);
	}
	private static async Task CreateMacroAsync(ICharacterSettingsDriver driver, byte id, string xml, CancellationToken token)
	{
		await driver.SendAsync(GameClientPackets.CreateMacro(id, xml), token);
		await driver.WaitAsync(typeof(SM_MACRO_RESULT), p => p.Get<byte>("code") == 0, token);
	}
	private static async Task DeleteMacroAsync(ICharacterSettingsDriver driver, byte id, CancellationToken token)
	{
		await driver.SendAsync(GameClientPackets.DeleteMacro(id), token);
		await driver.WaitAsync(typeof(SM_MACRO_RESULT), p => p.Get<byte>("code") == 1, token);
	}
	private static async Task SaveSettingsAsync(ICharacterSettingsDriver driver, IReadOnlyDictionary<byte, byte[]> settings, CancellationToken token)
	{
		foreach (var (type, data) in settings) await driver.SendAsync(GameClientPackets.UiSettings(type, data), token);
		await driver.SynchronizeAsync(token);
	}
	private static void AssertInventory(ICharacterSettingsDriver driver, Dictionary<int, BotInventoryItem> expected) =>
		Require(expected.OrderBy(p => p.Key).SequenceEqual(driver.Api.World.Inventory.OrderBy(p => p.Key)), "Surgery/settings unexpectedly changed inventory beyond the single ticket.");
	private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
