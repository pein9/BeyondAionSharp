using Aion.Commons.Nio;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Capture;

/// <summary>
/// C#-only instrumentation seam for observing serialized server packets before in-place encryption.
/// The Java 4.8 server has no packet-capture observer.
/// </summary>
public interface ServerPacketCaptureObserver
{
    bool IsEnabled();
    void OnPacketSerialized(AionConnection con, AionServerPacket packet, ByteBuffer clearFrame);
}
