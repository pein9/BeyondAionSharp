using System.Text.Json;
using Aion.Commons.Nio;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Capture;

namespace Aion.GameServer.Tests;

public sealed class SessionRecorderTests
{
	[Fact]
	public async Task RecordsEveryPacketInOrderWithExactBytesOnceTheAccountMatches()
	{
		string directory = NewDirectory();
		try
		{
			var recorder = new SessionRecorder(directory, ["tester"], run: "unit");
			var con = new RecordedConnection();

			// Before login: buffered, not yet written.
			byte[] clientPayload = [0x0A, 0x01, 0x65, 0xF5, 0xFE, 0x11, 0x22, 0x33];
			byte[]? copy = recorder.OnClientPayload(con, ByteBuffer.Wrap(clientPayload.ToArray()));
			Assert.NotNull(copy);
			recorder.OnClientPacketHandled(con, copy!, new ProbeClientPacket { Count = 3, Label = "hello" }, "executed", "CONNECTED");
			byte[] serverFrame = [0x09, 0x00, 0x34, 0x12, 0x44, 0xCB, 0xED, 0x07, 0x08];
			var frame = ByteBuffer.Wrap(serverFrame.ToArray());
			recorder.OnServerPacket(con, new ProbeServerPacket(), frame);
			Assert.Empty(Directory.GetFiles(directory));

			// The account is known: the buffer and everything after it go to the session's file.
			var account = new Account(7);
			account.SetName("Tester");
			con.SetAccount(account);
			recorder.OnServerPacket(con, new ProbeServerPacket(), ByteBuffer.Wrap(serverFrame.ToArray()));
			recorder.OnDisconnect(con);
			await recorder.DisposeAsync();

			string file = Assert.Single(Directory.GetFiles(directory, "*.recording.jsonl"));
			JsonElement[] lines = File.ReadAllLines(file).Select(l => JsonDocument.Parse(l).RootElement).ToArray();
			Assert.Equal(["session-open", "C", "S", "account", "S", "session-close"],
				lines.Select(l => l.GetProperty("dir").GetString() == "E" ? l.GetProperty("event").GetString() : l.GetProperty("dir").GetString()));
			Assert.Equal(Enumerable.Range(1, lines.Length).Select(i => (long)i), lines.Select(l => l.GetProperty("seq").GetInt64()));

			JsonElement client = lines[1];
			Assert.Equal(clientPayload, Convert.FromBase64String(client.GetProperty("payloadBase64").GetString()!));
			Assert.Equal("executed", client.GetProperty("outcome").GetString());
			Assert.Equal(nameof(ProbeClientPacket), client.GetProperty("packet").GetString());
			Assert.Equal(3, client.GetProperty("fields").GetProperty("Count").GetInt32());
			Assert.Equal("hello", client.GetProperty("fields").GetProperty("Label").GetString());

			JsonElement server = lines[2];
			Assert.Equal(serverFrame, Convert.FromBase64String(server.GetProperty("frameBase64").GetString()!));
			Assert.Equal("0x123", server.GetProperty("opcodeHex").GetString());
			Assert.Equal(42, server.GetProperty("fields").GetProperty("value").GetInt32());
			Assert.Equal("Tester", lines[3].GetProperty("account").GetString());
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[Fact]
	public async Task OtherAccountsAreNeverWritten()
	{
		string directory = NewDirectory();
		try
		{
			var recorder = new SessionRecorder(directory, ["tester"]);
			var con = new RecordedConnection();
			recorder.OnServerPacket(con, new ProbeServerPacket(), ByteBuffer.Wrap(new byte[] { 0x07, 0, 0x34, 0x12, 0x44, 0xCB, 0xED }));
			var account = new Account(8);
			account.SetName("someone-else");
			con.SetAccount(account);
			recorder.OnServerPacket(con, new ProbeServerPacket(), ByteBuffer.Wrap(new byte[] { 0x07, 0, 0x34, 0x12, 0x44, 0xCB, 0xED }));
			Assert.Null(recorder.OnClientPayload(con, ByteBuffer.Wrap(new byte[] { 1, 2, 3, 4, 5 })));
			recorder.OnDisconnect(con);
			await recorder.DisposeAsync();
			Assert.Empty(Directory.GetFiles(directory));
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[Fact]
	public void RecorderIsOffUnlessRequested()
	{
		string? previous = Environment.GetEnvironmentVariable("AION_RECORD");
		try
		{
			Environment.SetEnvironmentVariable("AION_RECORD", null);
			Assert.Null(SessionRecorder.FromEnvironment());
			Environment.SetEnvironmentVariable("AION_RECORD", "false");
			Assert.Null(SessionRecorder.FromEnvironment());
		}
		finally
		{
			Environment.SetEnvironmentVariable("AION_RECORD", previous);
		}
	}

	private static string NewDirectory() => Path.Combine(Path.GetTempPath(), $"aion-recorder-{Guid.NewGuid():N}");

	private sealed class RecordedConnection() : AionConnection("127.0.0.1");

	private sealed class ProbeServerPacket() : AionServerPacket(0x123)
	{
#pragma warning disable CS0414 // read by the recorder through reflection
		private readonly int value = 42;
#pragma warning restore CS0414
	}

	private sealed class ProbeClientPacket() : AionClientPacket(0x10A, new HashSet<AionConnection.State> { AionConnection.State.CONNECTED })
	{
		public int Count { get; init; }
		public string? Label { get; init; }
		protected override void ReadImpl() { }
		protected override void RunImpl() { }
	}
}
