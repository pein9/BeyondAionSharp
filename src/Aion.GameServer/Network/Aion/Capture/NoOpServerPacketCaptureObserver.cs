using Aion.Commons.Nio;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Capture;

/// <summary>
/// C#-only disabled implementation of the packet-capture instrumentation seam.
/// The Java 4.8 server has no corresponding observer.
/// </summary>
public sealed class NoOpServerPacketCaptureObserver : ServerPacketCaptureObserver
{
    public static readonly NoOpServerPacketCaptureObserver INSTANCE = new NoOpServerPacketCaptureObserver();

    private NoOpServerPacketCaptureObserver() { }

    public bool IsEnabled() => false;

    public void OnPacketSerialized(AionConnection con, AionServerPacket packet, ByteBuffer clearFrame) { }
}
