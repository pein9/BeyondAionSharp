using Aion.Commons.Network;
using Aion.Commons.Logging;
using Aion.LoginServer.Network.Aion.ClientPackets;
using Microsoft.Extensions.Logging;

namespace Aion.LoginServer.Network.Aion;

public static class AionClientPacketFactory
{
	private static readonly ILogger Log = AionLog.For(nameof(AionClientPacketFactory));

	public static AionClientPacket? Create(PacketBuffer payload, LoginClientState state)
	{
		var opCode = payload.ReadC();
		var payloadStart = payload.Position;
		AionClientPacket? packet = state switch
		{
			LoginClientState.Connected => opCode switch
			{
				0x07 => new CmAuthGameGuard(opCode),
				0x08 => new CmUpdateSession(opCode),
				_ => null
			},
			LoginClientState.AuthedGameGuard => opCode switch
			{
				0x00 => new CmLogin(opCode),
				_ => null
			},
			LoginClientState.AuthedLogin => opCode switch
			{
				0x05 => new CmServerList(opCode),
				0x02 => new CmPlay(opCode),
				_ => null
			},
			_ => null
		};

		if (packet == null)
		{
			var unknownData = FormatHex(payload, payloadStart);
			Log.LogWarning(
				"Unknown packet received from client: opCode=0x{Opcode:X2} state={State} length={Length} data=[{Data}]",
				opCode,
				state,
				payload.Remaining,
				unknownData);
			return null;
		}

		try
		{
			packet.Read(payload);
			return packet;
		}
		catch (Exception ex)
		{
			var remaining = payload.Remaining > 0
				? $" (last {payload.Remaining} bytes were not read)"
				: string.Empty;
			Log.LogError(
				ex,
				"Reading failed for packet [{Opcode:D3}] {Packet}. Buffer Info{Remaining}:{NewLine}{Content}",
				opCode,
				packet.GetType().Name,
				remaining,
				Environment.NewLine,
				FormatHex(payload, payloadStart));
			return null;
		}
	}

	private static string FormatHex(PacketBuffer payload, int start)
	{
		var bytes = payload.GetBuffer().AsSpan(start, payload.Capacity - start);
		return string.Join(' ', bytes.ToArray().Select(static value => value.ToString("X2")));
	}
}
