using System.Reflection;
using Aion.Bots.Protocol;
using Aion.GameServer.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotGamePacketRegistryTests
{
	[Fact]
	public void BotRegistryMatchesBothProductionOpcodeTables()
	{
		var registry = GamePacketRegistry.Instance;
		var productionClients = ReflectProductionClients();
		var productionServers = ReflectProductionServers();

		Assert.Equal(186, registry.ClientPackets.Count);
		Assert.Equal(238, registry.ServerPackets.Count);
		Assert.Equal(productionClients.Count, registry.ClientPackets.Count);
		Assert.Equal(productionServers.Count, registry.ServerPackets.Count);

		foreach (var expected in productionClients)
		{
			var actual = registry.GetClient(expected.Key);
			Assert.Equal(expected.Value.Opcode, actual.Opcode);
			Assert.True(expected.Value.States.SetEquals(actual.ValidStates));
			Assert.Same(actual, registry.GetClient(actual.Opcode));
		}

		foreach (var expected in productionServers)
		{
			var actual = registry.GetServer(expected.Key);
			Assert.Equal(expected.Value, actual.Opcode);
			Assert.Same(actual, registry.GetServer(actual.Opcode));
		}
	}

	[Fact]
	public void EncodingClientPacketInInvalidStateFailsBeforeKeyStateChanges()
	{
		var liveCrypt = new Crypt();
		var keyOpcode = GamePacketRegistry.Instance.GetServer(typeof(SM_KEY)).Opcode;
		var keyFrame = CreateKeyFrame(keyOpcode, liveCrypt.EnableKey());
		var codec = new GamePacketCodec();
		codec.RecoverKeyFromSmKeyFrame(keyFrame);

		var error = Assert.Throws<InvalidOperationException>(() =>
			codec.EncodeClientFrame(typeof(CM_ENTER_WORLD), AionConnection.State.CONNECTED, []));

		Assert.Contains(nameof(CM_ENTER_WORLD), error.Message, StringComparison.Ordinal);
		var validFrame = codec.EncodeClientFrame(typeof(CM_ENTER_WORLD), AionConnection.State.AUTHED, []);
		var payload = validFrame[2..];
		Assert.True(liveCrypt.Decrypt(Aion.Commons.Nio.ByteBuffer.Wrap(payload).Order(Aion.Commons.Nio.ByteOrder.LITTLE_ENDIAN)));
	}

	private static Dictionary<Type, (int Opcode, HashSet<AionConnection.State> States)> ReflectProductionClients()
	{
		var packets = (Array)GetField(typeof(AionClientPacketFactory), "packets").GetValue(null)!;
		var result = new Dictionary<Type, (int, HashSet<AionConnection.State>)>();
		for (var opcode = 0; opcode < packets.Length; opcode++)
		{
			var info = packets.GetValue(opcode);
			if (info == null)
				continue;
			var type = (Type)GetField(info.GetType(), "packetType").GetValue(info)!;
			var states = (IEnumerable<AionConnection.State>)GetField(info.GetType(), "validStates").GetValue(info)!;
			result.Add(type, (opcode, states.ToHashSet()));
		}
		return result;
	}

	private static Dictionary<Type, int> ReflectProductionServers()
	{
		var opcodes = (IReadOnlyDictionary<Type, int>)GetField(typeof(ServerPacketsOpcodes), "opcodes").GetValue(null)!;
		return opcodes.ToDictionary();
	}

	private static FieldInfo GetField(Type type, string name) =>
		type.GetField(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new MissingFieldException(type.FullName, name);

	private static byte[] CreateKeyFrame(int opcode, int encodedKey)
	{
		var frame = new byte[11];
		System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)frame.Length);
		var encodedOpcode = unchecked((ushort)Crypt.EncodeServerPacketOpcode(opcode));
		System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), encodedOpcode);
		frame[4] = Crypt.staticServerPacketCode;
		System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(5), unchecked((ushort)~encodedOpcode));
		System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(7), encodedKey);
		return frame;
	}
}
