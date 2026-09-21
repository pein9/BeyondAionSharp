using System.Text.Json;

namespace Aion.Bots.Protocol;

/// <summary>Freezes the loaded game protocol and bot decoder inventory alongside each run's traces.</summary>
public static class PacketCoverageCatalog
{
	public static void Write(string directory, string run, string mode)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(run);
		if (mode is not ("SIM" or "LIVE")) throw new ArgumentException("Expected SIM or LIVE.", nameof(mode));
		var registry = GamePacketRegistry.Instance;
		var decoded = new BotServerPacketDecoder().PacketTypes.ToHashSet();
		Directory.CreateDirectory(directory);
		using var output = new FileStream(Path.Combine(directory, "packet-catalog.json"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
		JsonSerializer.Serialize(output, new
		{
			schemaVersion = 1, run, mode, protocol = "aion-game-4.8",
			gameServerModule = typeof(Aion.GameServer.Network.Aion.AionClientPacketFactory).Module.ModuleVersionId,
			botModule = typeof(PacketCoverageCatalog).Module.ModuleVersionId,
			client = registry.ClientPackets.OrderBy(p => p.Opcode).Select(p => new { name = p.PacketType.Name, opcode = p.Opcode }),
			server = registry.ServerPackets.OrderBy(p => p.Opcode).Select(p => new { name = p.PacketType.Name, opcode = p.Opcode, structuredDecoder = decoded.Contains(p.PacketType) }),
		}, new JsonSerializerOptions { WriteIndented = true });
	}
}
