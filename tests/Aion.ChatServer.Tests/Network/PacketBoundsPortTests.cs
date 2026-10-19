using Aion.ChatServer.Network;
using Aion.ChatServer.Network.Packets;
using Aion.ChatServer.Network.Packets.GameServer;

namespace Aion.ChatServer.Tests.Network;

public sealed class PacketBoundsPortTests
{
    [Theory]
    [InlineData(0, new byte[] { 65, 0 })] // unterminated auth key
    [InlineData(2, new byte[] { 1, 2 })] // incomplete player id
    [InlineData(3, new byte[] { 1, 0, 0, 0 })] // no gag duration: must not become an ungag
    public void TruncatedGameserverPacketsAreRejectedInsteadOfReceivingDefaults(byte opcode, byte[] body)
    {
        using var payload = ChatPacketFrameCodec.CreatePayloadBuffer(ChatPacketFrameCodec.CreateFrame(new[] { opcode }.Concat(body).ToArray()));
        Assert.Null(GsPacketFactory.Create(payload, opcode == 0 ? GameServerConnectionState.Connected : GameServerConnectionState.Authed));
    }

    [Fact]
    public void TruncatedChatClientAuthIsRejected()
    {
        using var payload = ChatPacketFrameCodec.CreatePayloadBuffer(ChatPacketFrameCodec.CreateFrame(new byte[] { ClientPacketFactory.CmPlayerAuth }));
        Assert.Null(ClientPacketFactory.Create(payload, ChatClientConnectionState.Connected));
    }
}
