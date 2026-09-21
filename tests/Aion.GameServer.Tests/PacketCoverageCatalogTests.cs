using System.Text.Json;
using Aion.Bots.Protocol;

namespace Aion.GameServer.Tests;

public sealed class PacketCoverageCatalogTests
{
	[Theory]
	[InlineData("SIM")]
	[InlineData("LIVE")]
	public void CatalogFreezesActualRegistrationAndStructuredDecodersWithoutOverwriting(string mode)
	{
		string directory = Path.Combine(Path.GetTempPath(), "aion-packet-catalog-" + Guid.NewGuid().ToString("N"));
		try
		{
			PacketCoverageCatalog.Write(directory, "catalog-test", mode);
			using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "packet-catalog.json")));
			var root = document.RootElement;
			Assert.Equal(mode, root.GetProperty("mode").GetString());
			Assert.Equal("catalog-test", root.GetProperty("run").GetString());
			Assert.Equal(typeof(PacketCoverageCatalog).Module.ModuleVersionId, root.GetProperty("botModule").GetGuid());
			var registry = GamePacketRegistry.Instance;
			Assert.Equal(registry.ClientPackets.OrderBy(p => p.Opcode).Select(p => (p.PacketType.Name, p.Opcode)),
				root.GetProperty("client").EnumerateArray().Select(p => (p.GetProperty("name").GetString()!, p.GetProperty("opcode").GetInt32())));
			Assert.Equal(registry.ServerPackets.Count, root.GetProperty("server").GetArrayLength());
			Assert.Equal(new BotServerPacketDecoder().PacketTypes.Select(p => p.Name).Order(),
				root.GetProperty("server").EnumerateArray().Where(p => p.GetProperty("structuredDecoder").GetBoolean()).Select(p => p.GetProperty("name").GetString()!).Order());
			Assert.Throws<IOException>(() => PacketCoverageCatalog.Write(directory, "another-run", mode));
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}
}
