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
	public void DecoderInventoryContainsFortyFiveBotPerceptionPackets()
	{
		Assert.Equal(45, decoder.PacketTypes.Count);
		Assert.Contains(typeof(SM_SYSTEM_MESSAGE), decoder.PacketTypes);
		Assert.Contains(typeof(SM_PLAYER_SPAWN), decoder.PacketTypes);
		Assert.Contains(typeof(SM_INVENTORY_ADD_ITEM), decoder.PacketTypes);
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
