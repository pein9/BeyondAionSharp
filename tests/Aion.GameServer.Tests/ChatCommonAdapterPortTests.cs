using System.Net;
using Aion.Commons.Nio;
using Aion.GameServer.Commons.Network;
using Aion.GameServer.Commons.Network.Packet;
using Aion.GameServer.Commons.Utils;

namespace Aion.GameServer.Tests;

public sealed class ChatCommonAdapterPortTests
{
    [Theory]
    [InlineData("0.0.0.0", "all addresses on port 9021")]
    [InlineData("::", "all addresses on port 9021")]
    [InlineData("127.0.0.1", "127.0.0.1:9021")]
    public void ServerConfigurationDelegatesTheSharedAddressFormatter(string address, string expected)
    {
        var endpoint = new IPEndPoint(IPAddress.Parse(address), 9021);
        Assert.Equal(expected, NetworkUtils.GetAddressInfo(endpoint));
        Assert.Equal(expected, new ServerCfg(endpoint, "test", null!).GetAddressInfo());
    }

    [Fact]
    public void SharedPacketReaderCanBelongToANonNioConnection()
    {
        var owner = new object(); var packet = new Probe();
        packet.SetConnection(owner); packet.SetBuffer(ByteBuffer.Wrap(new byte[] { 255 }));
        Assert.True(packet.Read()); Assert.Equal(255, packet.Value); Assert.Same(owner, packet.GetConnection());
    }
    private sealed class Probe() : BaseClientPacket<object>(0)
    {
        public int Value;
        protected override void ReadImpl() => Value = ReadUC();
        protected override void RunImpl() { }
        public override void Run() { }
    }
}
