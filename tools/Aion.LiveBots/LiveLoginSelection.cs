using System.Buffers.Binary;

namespace Aion.LiveBots;

// Java SM_SERVER_LIST uses 21-byte entries. AccountController may send list updates while
// the login connection is still on the selection screen; they are not CM_PLAY refusals.
internal static class LiveLoginSelection
{
	internal static void RequireOnlineServer(ReadOnlySpan<byte> payload, byte serverId)
	{
		if (payload.Length < 3 || payload[0] != 4 || payload[1] == 0 || payload.Length < 3 + 21 * payload[1] + 16)
			throw new InvalidDataException("Malformed SM_SERVER_LIST.");
		var ids = new HashSet<byte>();
		bool online = false;
		for (int index = 0; index < payload[1]; index++)
		{
			int start = 3 + 21 * index;
			byte id = payload[start];
			if (!ids.Add(id)) throw new InvalidDataException("Duplicate server id in SM_SERVER_LIST.");
			if (id == serverId) online = payload[start + 15] == 1;
		}
		if (!online) throw new InvalidDataException($"Login server did not advertise online game server {serverId}.");
	}

	internal static async Task<byte[]> ReadPlayOkAsync(Func<CancellationToken, Task<byte[]>> readPayload,
		byte serverId, Action<byte[]> observeList, CancellationToken token)
	{
		for (int updates = 0; updates <= 16; updates++)
		{
			byte[] payload = await readPayload(token);
			if (payload.Length > 0 && payload[0] == 4)
			{
				RequireOnlineServer(payload, serverId);
				observeList(payload);
				continue;
			}
			if (payload.Length >= 10 && payload[0] == 7 && payload[9] == serverId) return payload;
			string opcode = payload.Length == 0 ? "empty" : $"0x{payload[0]:X2}";
			string reason = payload.Length >= 5 && payload[0] is 1 or 6
				? $", refusal={BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(1, 4))}" : "";
			throw new InvalidDataException($"Expected SM_PLAY_OK for game server {serverId}, received {opcode} ({payload.Length} bytes){reason}.");
		}
		throw new InvalidDataException("Too many SM_SERVER_LIST updates while awaiting SM_PLAY_OK.");
	}
}
