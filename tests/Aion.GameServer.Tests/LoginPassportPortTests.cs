using System.Reflection;
using Aion.Commons.Nio;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public sealed class LoginPassportPortTests
{
    [Theory]
    [InlineData(0L)]
    [InlineData(-8613303245920329199L)] // 0x8877665544332211
    public void LoginCheckReadsOnePassportLongWithoutChangingTheFourSessionKeys(long passport)
    {
        var bytes = BitConverter.GetBytes(11).Concat(BitConverter.GetBytes(22))
            .Concat(BitConverter.GetBytes(33)).Concat(BitConverter.GetBytes(44))
            .Concat(BitConverter.GetBytes(passport)).ToArray();
        var buffer = ByteBuffer.Wrap(bytes).Order(ByteOrder.LITTLE_ENDIAN);
        var packet = new CM_L2AUTH_LOGIN_CHECK(0, new HashSet<AionConnection.State>());
        packet.SetConnection(new SocketlessConnection());
        packet.SetBuffer(buffer);
        Assert.True(packet.Read());
        object Field(string name) => typeof(CM_L2AUTH_LOGIN_CHECK).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(packet)!;
        Assert.Equal(11, Field("playOk2"));
        Assert.Equal(22, Field("playOk1"));
        Assert.Equal(33, Field("accountId"));
        Assert.Equal(44, Field("loginOk"));
        Assert.Equal(passport, Field("passportKey"));
        Assert.Equal(0, buffer.Remaining());
    }

    private sealed class SocketlessConnection() : AionConnection("127.0.0.1");
}
