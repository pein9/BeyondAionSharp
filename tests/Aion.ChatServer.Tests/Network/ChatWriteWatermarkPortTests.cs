using System.Buffers.Binary;
using Aion.ChatServer.Network.Handlers;
using Aion.ChatServer.Network.Packets;
using Aion.ChatServer.Network.Packets.GameServer;
using Aion.Commons.Network;

namespace Aion.ChatServer.Tests.Network;

public sealed class ChatWriteWatermarkPortTests
{
    [Fact]
    public void WatermarksHaveStrictBoundariesAndHysteresis()
    {
        var state = new ChatClientWriteState();
        for (int i = 0; i < 4; i++) Assert.False(state.Enqueue(16288)); // 4*(16288+96) = 65536
        Assert.True(state.IsWritable);
        Assert.True(state.Enqueue(3));
        Assert.False(state.IsWritable); Assert.Equal(1, state.WritabilityChanges);
        Assert.False(state.Complete(3));
        Assert.False(state.Complete(16288));
        Assert.False(state.Complete(16288)); // exactly32768 still unwritable
        Assert.Equal(32768, state.PendingBytes);
        Assert.True(state.Complete(16288));
        Assert.True(state.IsWritable); Assert.Equal(2, state.WritabilityChanges);
        state.Complete(16288); Assert.Equal(0, state.PendingBytes);
    }

    [Fact]
    public void ByteLimitUsesBytesBeforeWritableAndAllowsTheExactLimit()
    {
        var state = new ChatClientWriteState();
        for (int i = 0; i < 18; i++) state.Enqueue(16000);
        state.Enqueue(5087); // total294911; minus32768 plus1 =262144
        Assert.Equal(294911, state.PendingBytes);
        Assert.True(state.CanEnqueue);
        state.Enqueue(3);
        Assert.False(state.CanEnqueue);
    }

    [Theory]
    [InlineData(16381, true)]
    [InlineData(16382, false)]
    public void ClientEncoderCapacityIncludesLengthAndOpcode(int body, bool accepted)
    {
        var packet = new LargeClientPacket(body);
        if (!accepted) { Assert.Throws<InvalidOperationException>(() => packet.SerializeFrame()); return; }
        var frame = packet.SerializeFrame();
        Assert.Equal(16384, frame.Length);
        Assert.Equal(frame.Length, BinaryPrimitives.ReadUInt16LittleEndian(frame));
    }

    [Fact]
    public void ServerEncoderSupportsItsLargerReadBufferCapacity()
    {
        var frame = new LargeServerPacket(65532).SerializeFrame();
        Assert.Equal(65535, frame.Length);
        Assert.Equal(65535, BinaryPrimitives.ReadUInt16LittleEndian(frame));
    }

    internal sealed class LargeClientPacket(int body) : AbstractServerPacket(0x31)
    {
        protected override void WritePayload(PacketBuffer buffer) => buffer.WriteB(new byte[body]);
    }
    internal sealed class LargeServerPacket(int body) : GsServerPacket
    {
        protected override void WritePayload(PacketBuffer buffer) { buffer.WriteC(0); buffer.WriteB(new byte[body]); }
    }
}
