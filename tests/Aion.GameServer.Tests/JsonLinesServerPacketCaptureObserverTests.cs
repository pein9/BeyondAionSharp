using System.Text.Json;
using Aion.Commons.Nio;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Capture;

namespace Aion.GameServer.Tests;

public sealed class JsonLinesServerPacketCaptureObserverTests
{
	[Fact]
	public async Task ObserverOwnsClearFrameBeforeCallerMutatesBackingBuffer()
	{
		var directory = Path.Combine(Path.GetTempPath(), $"aion-packet-tap-{Guid.NewGuid():N}");
		var path = Path.Combine(directory, "packet-tap.jsonl");
		try
		{
			var original = new byte[] { 0x08, 0x00, 0x34, 0x12, 0xA6, 0xCB, 0xED, 0x01 };
			var buffer = ByteBuffer.Wrap(original.ToArray());
			var observer = new JsonLinesServerPacketCaptureObserver(path, "p307-test", capacity: 4);

			observer.OnPacketSerialized(new CaptureConnection(), new CapturePacket(), buffer);
			for (var index = 0; index < original.Length; index++)
				buffer.Put(index, 0xFF);
			await observer.DisposeAsync();

			using var document = JsonDocument.Parse(Assert.Single(File.ReadAllLines(path)));
			var root = document.RootElement;
			Assert.Equal("p307-test", root.GetProperty("run").GetString());
			Assert.Equal(nameof(CapturePacket), root.GetProperty("packet").GetString());
			Assert.Equal("0x123", root.GetProperty("opcode").GetString());
			Assert.Equal(original.Length, root.GetProperty("length").GetInt32());
			Assert.Equal(original, Convert.FromBase64String(root.GetProperty("frameBase64").GetString()!));
		}
		finally
		{
			if (Directory.Exists(directory))
				Directory.Delete(directory, recursive: true);
		}
	}

	private sealed class CaptureConnection() : AionConnection("127.0.0.1");

	private sealed class CapturePacket() : AionServerPacket(0x123);
}
