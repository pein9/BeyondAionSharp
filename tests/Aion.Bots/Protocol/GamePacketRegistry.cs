using System.Collections.Frozen;
using System.Reflection;
using Aion.GameServer.Network.Aion;

namespace Aion.Bots.Protocol;

public sealed class GamePacketRegistry
{
	private static readonly Lazy<GamePacketRegistry> LazyInstance = new(() => new GamePacketRegistry());

	private readonly FrozenDictionary<Type, ClientPacketDefinition> clientByType;
	private readonly FrozenDictionary<int, ClientPacketDefinition> clientByOpcode;
	private readonly FrozenDictionary<Type, ServerPacketDefinition> serverByType;
	private readonly FrozenDictionary<int, ServerPacketDefinition> serverByOpcode;

	private GamePacketRegistry()
	{
		var clients = ReflectClientPackets();
		var servers = ReflectServerPackets();
		clientByType = clients.ToFrozenDictionary(packet => packet.PacketType);
		clientByOpcode = clients.ToFrozenDictionary(packet => packet.Opcode);
		serverByType = servers.ToFrozenDictionary(packet => packet.PacketType);
		serverByOpcode = servers.ToFrozenDictionary(packet => packet.Opcode);
	}

	public static GamePacketRegistry Instance => LazyInstance.Value;

	public IReadOnlyCollection<ClientPacketDefinition> ClientPackets => clientByOpcode.Values;

	public IReadOnlyCollection<ServerPacketDefinition> ServerPackets => serverByOpcode.Values;

	public ClientPacketDefinition GetClient(Type packetType) =>
		clientByType.TryGetValue(packetType, out var packet)
			? packet
			: throw new KeyNotFoundException($"No client opcode is registered for {packetType.FullName}.");

	public ClientPacketDefinition GetClient(int opcode) =>
		clientByOpcode.TryGetValue(opcode, out var packet)
			? packet
			: throw new KeyNotFoundException($"No client packet is registered for opcode {opcode}.");

	public ServerPacketDefinition GetServer(Type packetType) =>
		serverByType.TryGetValue(packetType, out var packet)
			? packet
			: throw new KeyNotFoundException($"No server opcode is registered for {packetType.FullName}.");

	public ServerPacketDefinition GetServer(int opcode) =>
		serverByOpcode.TryGetValue(opcode, out var packet)
			? packet
			: throw new KeyNotFoundException($"No server packet is registered for opcode {opcode}.");

	private static ClientPacketDefinition[] ReflectClientPackets()
	{
		var packetsField = RequireField(typeof(AionClientPacketFactory), "packets");
		var packets = packetsField.GetValue(null) as Array
			?? throw new InvalidOperationException("AionClientPacketFactory.packets is not an array.");
		var definitions = new List<ClientPacketDefinition>();

		for (var opcode = 0; opcode < packets.Length; opcode++)
		{
			var packetInfo = packets.GetValue(opcode);
			if (packetInfo == null)
				continue;

			var packetInfoType = packetInfo.GetType();
			var packetType = RequireField(packetInfoType, "packetType").GetValue(packetInfo) as Type
				?? throw new InvalidOperationException($"{packetInfoType.FullName}.packetType is not a Type.");
			var states = RequireField(packetInfoType, "validStates").GetValue(packetInfo) as IEnumerable<AionConnection.State>
				?? throw new InvalidOperationException($"{packetInfoType.FullName}.validStates is not a state set.");
			definitions.Add(new ClientPacketDefinition(packetType, opcode, states.ToFrozenSet()));
		}

		return definitions.ToArray();
	}

	private static ServerPacketDefinition[] ReflectServerPackets()
	{
		var opcodesField = RequireField(typeof(ServerPacketsOpcodes), "opcodes");
		var opcodes = opcodesField.GetValue(null) as IReadOnlyDictionary<Type, int>
			?? throw new InvalidOperationException("ServerPacketsOpcodes.opcodes is not a dictionary.");

		return opcodes
			.Select(entry => new ServerPacketDefinition(entry.Key, entry.Value))
			.ToArray();
	}

	private static FieldInfo RequireField(Type declaringType, string name) =>
		declaringType.GetField(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new MissingFieldException(declaringType.FullName, name);
}

public sealed record ClientPacketDefinition(
	Type PacketType,
	int Opcode,
	IReadOnlySet<AionConnection.State> ValidStates)
{
	public void EnsureValid(AionConnection.State state)
	{
		if (!ValidStates.Contains(state))
			throw new InvalidOperationException(
				$"{PacketType.Name} cannot be sent while the game connection is {state}; valid states: {string.Join(", ", ValidStates)}.");
	}
}

public sealed record ServerPacketDefinition(Type PacketType, int Opcode);
