using System.Collections.Concurrent;
using Aion.Commons.Logging;
using Aion.Commons.Network;
using Microsoft.Extensions.Logging;

namespace Aion.ChatServer.Network.Packets;

internal static class ChatPacketReads
{
    private static readonly ILogger Log = AionLog.For("BaseClientPacket");
    private static readonly ConcurrentDictionary<int, byte> PartialWarnings = new();
    private static readonly ConcurrentDictionary<int, byte> TooShortWarnings = new();
    internal static bool TryRead(PacketBuffer payload, int opcode, string name, Action read)
    {
        int start = payload.Position;
        try
        {
            read();
            if (payload.Remaining > 0 && PartialWarnings.TryAdd(opcode, 0))
                Log.LogWarning("{Packet} was not fully read! Last {Remaining} bytes were not read from buffer: {Content}", name, payload.Remaining,
                    Convert.ToHexString(payload.GetBuffer().AsSpan(start)));
            return true;
        }
        catch (EndOfStreamException)
        {
            if (TooShortWarnings.TryAdd(opcode, 0))
                Log.LogWarning("{Packet} is shorter than expected: {Content}", name, Convert.ToHexString(payload.GetBuffer().AsSpan(start)));
            return false;
        }
        catch (Exception ex)
        {
            Log.LogError(ex, "Reading failed for packet {Packet}: {Content}", name, Convert.ToHexString(payload.GetBuffer().AsSpan(start)));
            return false;
        }
    }
}
