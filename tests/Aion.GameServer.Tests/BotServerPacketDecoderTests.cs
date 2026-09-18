using System.Collections;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aion.Bots.Protocol;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotServerPacketDecoderTests
{
	private readonly BotServerPacketDecoder decoder = new();

	[Fact]
	public void DecoderInventoryContainsFortySevenBotPerceptionPackets()
	{
		Assert.Equal(47, decoder.PacketTypes.Count);
		Assert.Contains(typeof(SM_MESSAGE), decoder.PacketTypes);
		Assert.Contains(typeof(SM_EMOTION), decoder.PacketTypes);
		Assert.Contains(typeof(SM_SYSTEM_MESSAGE), decoder.PacketTypes);
		Assert.Contains(typeof(SM_PLAYER_SPAWN), decoder.PacketTypes);
		Assert.Contains(typeof(SM_INVENTORY_ADD_ITEM), decoder.PacketTypes);
	}

	[Fact]
	public void MovementSpeedComesFromPlayerInfoAndEmotionRatherThanStatsInfo()
	{
		using var playerFixture = LoadFixture("SM_PLAYER_INFO.json");
		var playerCase = playerFixture.RootElement.GetProperty("cases")[0];
		var player = decoder.Decode(typeof(SM_PLAYER_INFO),
			Convert.FromHexString(playerCase.GetProperty("payloadHex").GetString()!));
		Assert.Equal(6f, player.Get<float>("movementSpeed"));

		using var emotionFixture = LoadFixture("SM_EMOTION.json");
		var speedCase = emotionFixture.RootElement.GetProperty("cases")[2];
		var emotion = decoder.Decode(typeof(SM_EMOTION),
			Convert.FromHexString(speedCase.GetProperty("payloadHex").GetString()!));
		Assert.Equal(6f, emotion.Get<float>("movementSpeed"));
		Assert.Equal((ushort)1000, emotion.Get<ushort>("currentAttackSpeed"));
	}

	[Fact]
	public void EveryGoldenCaseForEveryDecodedPacketDecodesToRecordedPrimitiveInputs()
	{
		foreach (var packetType in decoder.PacketTypes.OrderBy(type => type.Name, StringComparer.Ordinal))
		{
			using var fixture = LoadFixture(packetType.Name + ".json");
			var root = fixture.RootElement;
			Assert.Equal("Java", root.GetProperty("source").GetString());
			foreach (var fixtureCase in root.GetProperty("cases").EnumerateArray())
			{
				var body = Convert.FromHexString(fixtureCase.GetProperty("payloadHex").GetString()!);
				var decoded = decoder.Decode(packetType, body);
				var comparisons = AssertPrimitiveInputs(fixtureCase.GetProperty("inputs"), decoded.Fields);
				AssertCaseWithoutTopLevelPrimitive(packetType, decoded, comparisons);
			}
		}
	}

	[Fact]
	public void ItemPacketDecodersRespectTheJavaLengthPrefixedBlobBoundary()
	{
		using var addFixture = LoadFixture("SM_INVENTORY_ADD_ITEM.json");
		var addCase = addFixture.RootElement.GetProperty("cases")[0];
		var add = decoder.Decode(typeof(SM_INVENTORY_ADD_ITEM), Convert.FromHexString(addCase.GetProperty("payloadHex").GetString()!));
		var addItems = add.Get<List<IReadOnlyDictionary<string, object?>>>("items");
		Assert.Single(addItems);
		Assert.Equal(268700001, addItems[0]["objectId"]);
		Assert.Equal(1L, addItems[0]["itemCount"]);
		Assert.Equal("Smith", addItems[0]["itemCreator"]);
		Assert.NotEmpty(Assert.IsType<byte[]>(addItems[0]["blob"]));

		using var updateFixture = LoadFixture("SM_INVENTORY_UPDATE_ITEM.json");
		foreach (var fixtureCase in updateFixture.RootElement.GetProperty("cases").EnumerateArray())
		{
			var update = decoder.Decode(typeof(SM_INVENTORY_UPDATE_ITEM),
				Convert.FromHexString(fixtureCase.GetProperty("payloadHex").GetString()!));
			Assert.NotEmpty(update.Get<byte[]>("blob"));
			Assert.Equal(7L, update.Get<long>("itemCount"));
			Assert.True(update.Fields.ContainsKey("updateMask"));
		}
	}

	[Fact]
	public void GatherableDecoderDistinguishesGatherablesAndStaticDoors()
	{
		using var fixture = LoadFixture("SM_GATHERABLE_INFO.json");
		var body = Convert.FromHexString(fixture.RootElement.GetProperty("cases")[0].GetProperty("payloadHex").GetString()!);

		var gatherable = decoder.Decode(typeof(SM_GATHERABLE_INFO), body);
		Assert.False(gatherable.Get<bool>("isStatic"));
		Assert.Null(gatherable.Fields["open"]);

		body[24] = 0x09;
		var openDoor = decoder.Decode(typeof(SM_GATHERABLE_INFO), body);
		Assert.True(openDoor.Get<bool>("isStatic"));
		Assert.True(openDoor.Get<bool>("open"));

		body[24] = 0x0A;
		var closedDoor = decoder.Decode(typeof(SM_GATHERABLE_INFO), body);
		Assert.True(closedDoor.Get<bool>("isStatic"));
		Assert.False(closedDoor.Get<bool>("open"));
	}

	[Fact]
	public void CharacterSelectionDecodersExposeIdentityAndPersistedPosition()
	{
		var summary = CreateCharacterSummary(100_001, "Aelivea", 210010000, 121.25f, 132.5f, 144.75f);
		using var listBody = new MemoryStream();
		using (var writer = new BinaryWriter(listBody, Encoding.Unicode, leaveOpen: true))
		{
			writer.Write(0x11223344);
			writer.Write((byte)1);
			writer.Write(summary);
		}

		var list = decoder.Decode(typeof(SM_CHARACTER_LIST), listBody.ToArray());
		var characters = list.Get<List<IReadOnlyDictionary<string, object?>>>("characters");
		var character = Assert.Single(characters);
		Assert.Equal(100_001, character["objectId"]);
		Assert.Equal("Aelivea", character["name"]);
		Assert.Equal(210010000, character["mapId"]);
		Assert.Equal(121.25f, character["x"]);
		Assert.Equal(132.5f, character["y"]);
		Assert.Equal(144.75f, character["z"]);

		using var createBody = new MemoryStream();
		using (var writer = new BinaryWriter(createBody, Encoding.Unicode, leaveOpen: true))
		{
			writer.Write(0);
			writer.Write(summary);
		}
		var create = decoder.Decode(typeof(SM_CREATE_CHARACTER), createBody.ToArray());
		var created = create.Get<IReadOnlyDictionary<string, object?>>("character");
		Assert.Equal(100_001, created["objectId"]);
		Assert.Equal("Aelivea", created["name"]);
	}

	[Fact]
	public void SystemMessageNameTableIsCurrentWithTheFactoryCatalog()
	{
		var methods = typeof(SM_SYSTEM_MESSAGE).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
			.Where(method => method.Name.StartsWith("STR_", StringComparison.Ordinal))
			.ToArray();
		Assert.Equal(4_115, methods.Length);
		Assert.Equal(SystemMessageNames.FactoryCount, methods.Length);
		Assert.Equal(4_110, SystemMessageNames.All.Count);

		var source = File.ReadAllText(Path.Combine(RepoRoot(),
			"src", "Aion.GameServer", "Network", "Aion", "ServerPackets", "SM_SYSTEM_MESSAGE.cs"));
		var normalizedSource = source.Replace("\r\n", "\n", StringComparison.Ordinal);
		var sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedSource)));
		Assert.Equal(SystemMessageNames.SourceSha256, sourceHash);
		Assert.Equal("STR_MOVE_PORTAL_ERROR_INVALID_RACE", SystemMessageNames.GetNameOrNull(901354));
	}

	private static int AssertPrimitiveInputs(JsonElement inputs, IReadOnlyDictionary<string, object?> fields)
	{
		var comparisons = 0;
		foreach (var property in inputs.EnumerateObject())
		{
			if (!fields.TryGetValue(property.Name, out var actual) || actual == null)
				continue;
			switch (property.Value.ValueKind)
			{
				case JsonValueKind.True:
				case JsonValueKind.False:
					Assert.Equal(property.Value.GetBoolean(), Assert.IsType<bool>(actual));
					comparisons++;
					break;
				case JsonValueKind.Number when IsNumeric(actual):
					Assert.Equal(property.Value.GetDecimal(), Convert.ToDecimal(actual, System.Globalization.CultureInfo.InvariantCulture));
					comparisons++;
					break;
				case JsonValueKind.String when actual is string text:
					Assert.Equal(property.Value.GetString(), text);
					comparisons++;
					break;
				case JsonValueKind.Array when actual is byte[] bytes:
					Assert.Equal(property.Value.EnumerateArray().Select(value => value.GetByte()), bytes);
					comparisons++;
					break;
			}
		}
		return comparisons;
	}

	private static bool IsNumeric(object value) => value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

	private static byte[] CreateCharacterSummary(int objectId, string name, int mapId, float x, float y, float z)
	{
		using var output = new MemoryStream();
		using var writer = new BinaryWriter(output, Encoding.Unicode, leaveOpen: true);
		writer.Write(objectId);
		WriteFixedString(writer, name, 25);
		writer.Write(0); // gender
		writer.Write(0); // Elyos
		writer.Write(0); // warrior
		for (var i = 0; i < 5; i++) writer.Write(0); // voice and colours
		writer.Write(new byte[52]);
		writer.Write(1f);
		writer.Write(0); // template
		writer.Write(mapId);
		writer.Write(x);
		writer.Write(y);
		writer.Write(z);
		writer.Write(32); // heading
		writer.Write((ushort)1);
		writer.Write((ushort)0);
		writer.Write(0); // title
		writer.Write(0); // legion id
		WriteFixedString(writer, "", 40);
		writer.Write((ushort)0);
		writer.Write(1_700_000_000); // last online
		writer.Write(new byte[16 * 13]);
		writer.Write(new byte[(6 * sizeof(int)) + 68]);
		writer.Write(0); // deletion time
		writer.Write(new byte[2 * sizeof(ushort)]);
		writer.Write(new byte[4 * sizeof(int)]);
		writer.Write(0L); // broker proceeds
		writer.Write(new byte[7 * sizeof(int)]);
		writer.Write((ushort)0); // empty ban reason
		return output.ToArray();
	}

	private static void WriteFixedString(BinaryWriter writer, string value, int characters)
	{
		for (var i = 0; i < characters; i++)
			writer.Write(i < value.Length ? value[i] : '\0');
		writer.Write('\0');
	}

	private static void AssertCaseWithoutTopLevelPrimitive(Type packetType, DecodedBotServerPacket decoded, int comparisons)
	{
		if (comparisons > 0)
			return;
		if (packetType == typeof(SM_KEY))
		{
			Assert.Equal(0x1A3D5948, decoded.Get<int>("encodedKey"));
			return;
		}
		if (packetType == typeof(SM_SKILL_LIST))
		{
			Assert.NotEmpty(decoded.Get<List<IReadOnlyDictionary<string, object?>>>("skills"));
			return;
		}
		if (packetType == typeof(SM_SKILL_COOLDOWN))
		{
			Assert.NotEmpty(decoded.Get<List<IReadOnlyDictionary<string, object?>>>("cooldowns"));
			return;
		}
		if (packetType == typeof(SM_QUEST_LIST))
		{
			Assert.Empty(decoded.Get<List<IReadOnlyDictionary<string, object?>>>("quests"));
			return;
		}
		if (packetType == typeof(SM_ENTER_WORLD_CHECK))
		{
			Assert.InRange(decoded.Get<byte>("msg"), (byte)0, (byte)6);
			return;
		}
		if (packetType == typeof(SM_INVENTORY_ADD_ITEM))
		{
			Assert.NotEmpty(decoded.Get<List<IReadOnlyDictionary<string, object?>>>("items"));
			return;
		}
		if (packetType == typeof(SM_QUEST_ACTION))
		{
			Assert.True(decoded.Get<bool>("suppressed"));
			return;
		}
		Assert.Fail($"{packetType.Name} fixture did not exercise a decoded primitive field.");
	}

	private static JsonDocument LoadFixture(string fileName)
	{
		var path = Path.Combine(RepoRoot(), "parity-artifacts", "golden", "packets", fileName);
		Assert.True(File.Exists(path), $"Missing Java golden fixture: {path}");
		return JsonDocument.Parse(File.ReadAllText(path));
	}

	private static string RepoRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "AionServer.slnx")))
				return directory.FullName;
			directory = directory.Parent;
		}
		throw new DirectoryNotFoundException("Could not find the repository root above " + AppContext.BaseDirectory);
	}
}
