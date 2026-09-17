using System.Text.Json;
using Aion.Bots.Protocol;
using Aion.Bots.Tracing;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotActionTraceWriterTests
{
	[Fact]
	public void TraceUsesFixedSchemaForActionsSentPacketsAndDecodedPackets()
	{
		var wall = new FixedTimeProvider(new DateTimeOffset(2026, 9, 17, 12, 34, 56, 789, TimeSpan.Zero));
		var virtualNow = TimeSpan.FromMinutes(7) + TimeSpan.FromSeconds(12.4);
		using var output = new MemoryStream();
		using (var trace = new BotActionTraceWriter(output, "r0917a", "b01", "b01r0917", wall,
			() => virtualNow, leaveOpen: true))
		{
			trace.WriteAction("s14", "SelectDialog", Fields(("questId", 1103), ("actionId", 39)));
			trace.WriteSent("s14", new BotClientPacket(typeof(CM_DIALOG_SELECT), [0x27, 0x00]),
				Fields(("questId", 1103), ("actionId", 39)));
			trace.WriteReceived("s14", new DecodedBotServerPacket(typeof(SM_SYSTEM_MESSAGE), Fields(
				("msgId", 1400129), ("name", "STR_QUEST_ACQUIRE"),
				("params", new[] { "1103" }), ("specialParams", Array.Empty<string>()))));
		}

		var lines = ReadLines(output);
		Assert.Equal(3, lines.Length);
		Assert.StartsWith("{\"ts\":\"2026-09-17T12:34:56.789Z\",\"vt\":\"00:07:12.400\",\"run\":\"r0917a\",\"bot\":\"b01\",\"account\":\"b01r0917\",\"step\":\"s14\",\"dir\":\"action\",\"packet\":\"SelectDialog\",\"fields\":", lines[0]);

		using var action = JsonDocument.Parse(lines[0]);
		Assert.Equal(1103, action.RootElement.GetProperty("fields").GetProperty("questId").GetInt32());
		using var sent = JsonDocument.Parse(lines[1]);
		Assert.Equal(BotActionTraceWriter.SentDirection, sent.RootElement.GetProperty("dir").GetString());
		Assert.Equal("CM_DIALOG_SELECT", sent.RootElement.GetProperty("packet").GetString());
		using var received = JsonDocument.Parse(lines[2]);
		Assert.Equal(BotActionTraceWriter.ReceivedDirection, received.RootElement.GetProperty("dir").GetString());
		Assert.Equal("STR_QUEST_ACQUIRE", received.RootElement.GetProperty("fields").GetProperty("name").GetString());
		Assert.Equal("1103", received.RootElement.GetProperty("fields").GetProperty("params")[0].GetString());
	}

	[Fact]
	public void SentPacketWithoutSemanticFieldsCarriesItsBodyAndLiveTraceFlushesImmediately()
	{
		var directory = Path.Combine(Path.GetTempPath(), $"aion-bot-trace-{Guid.NewGuid():N}");
		var path = Path.Combine(directory, "b01.trace.jsonl");
		try
		{
			using var trace = BotActionTraceWriter.Open(path, "run", "b01", "account");
			trace.WriteSent("s01", new BotClientPacket(typeof(CM_ATTACK), [0x01, 0xAB]));

			var line = Assert.Single(ReadLinesWhileOpen(path));
			using var document = JsonDocument.Parse(line);
			Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("vt").ValueKind);
			Assert.Equal("01AB", document.RootElement.GetProperty("fields").GetProperty("bodyHex").GetString());
		}
		finally
		{
			if (Directory.Exists(directory))
				Directory.Delete(directory, recursive: true);
		}
	}

	[Fact]
	public void SystemMessageTraceRejectsARecordWithoutNameOrParameters()
	{
		using var output = new MemoryStream();
		using var trace = new BotActionTraceWriter(output, "run", "bot", "account");
		var packet = new DecodedBotServerPacket(typeof(SM_SYSTEM_MESSAGE), Fields(("msgId", 1)));

		var error = Assert.Throws<InvalidDataException>(() => trace.WriteReceived("s01", packet));
		Assert.Contains("STR_ name and parameters", error.Message, StringComparison.Ordinal);
	}

	private static IReadOnlyDictionary<string, object?> Fields(params (string Name, object? Value)[] values) =>
		values.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);

	private static string[] ReadLines(MemoryStream stream)
	{
		stream.Position = 0;
		using var reader = new StreamReader(stream, leaveOpen: true);
		return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
	}

	private static string[] ReadLinesWhileOpen(string path)
	{
		using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		using var reader = new StreamReader(stream);
		return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
	}

	private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
	{
		public override DateTimeOffset GetUtcNow() => now;
	}
}
