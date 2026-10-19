using Aion.Commons.Nio;
using Aion.GameServer.Commons.Network;
using Aion.GameServer.Commons.Network.Packet;
using Aion.GameServer.Configs.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed partial class SendQueuePortTests
{
    [Fact]
    public void QueueAtCapacityIsAcceptedAndTheNextPacketClearsAndClosesIt()
    {
        WithClock(now =>
        {
            var con = new Link(2, 60000);
            con.SendPacket(new Packet()); con.SendPacket(new Packet());
            Assert.Equal(2, con.Queue.Count); Assert.False(con.IsClosed());
            con.SendPacket(new Packet());
            Assert.True(con.IsClosed()); Assert.Empty(con.Queue); Assert.Equal(1, con.Disconnects);
            con.SendPacket(new Packet()); Assert.Empty(con.Queue);
        });
    }

    [Fact]
    public void StallLimitIsStrictlyGreaterAndQueuingMoreDoesNotRefreshProgress()
    {
        WithClock(now =>
        {
            var con = new Link(10, 60000);
            con.SendPacket(new Packet()); now.Value += 60000;
            con.SendPacket(new Packet()); Assert.False(con.IsClosed());
            now.Value++; con.SendPacket(new Packet());
            Assert.True(con.IsClosed()); Assert.Empty(con.Queue);
        });
    }

    [Fact]
    public void PositiveWriteProgressAndAnEmptyQueueEachStartANewStallWindow()
    {
        WithClock(now =>
        {
            var con = new Link(10, 60000);
            con.SendPacket(new Packet()); now.Value += 60001;
            con.OnDataWritten(); con.SendPacket(new Packet()); Assert.False(con.IsClosed());
            now.Value += 60000; con.SendPacket(new Packet()); Assert.False(con.IsClosed());
            con.Queue.Clear(); now.Value += 600000;
            con.SendPacket(new Packet()); Assert.False(con.IsClosed()); Assert.Single(con.Queue);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void JavaStillAcceptsTheFirstPacketWhenConfiguredCapacityIsNonPositive(int capacity)
    {
        WithClock(now =>
        {
            var con = new Link(capacity, 60000);
            con.SendPacket(new Packet()); Assert.Single(con.Queue); Assert.False(con.IsClosed());
            con.SendPacket(new Packet()); Assert.True(con.IsClosed()); Assert.Empty(con.Queue);
        });
    }

    [Fact]
    public void ServerToServerDefaultsDoNotAcquireClientLimits()
    {
        WithClock(now =>
        {
            var con = new Link();
            for (int i = 0; i < 20001; i++) con.SendPacket(new Packet());
            now.Value += 600000; con.SendPacket(new Packet());
            Assert.False(con.IsClosed()); Assert.Equal(20002, con.Queue.Count);
        });
    }

    [Fact]
    public void GameClientUsesConfiguredLimitsWithLongMillisecondMultiplication()
    {
        int oldCount = NetworkConfig.MAX_PENDING_SERVER_PACKETS, oldSeconds = NetworkConfig.MAX_SEND_STALL_SECONDS;
        try
        {
            NetworkConfig.MAX_PENDING_SERVER_PACKETS = 7;
            NetworkConfig.MAX_SEND_STALL_SECONDS = int.MaxValue;
            var con = new GameConnection();
            Assert.Equal((7, int.MaxValue * 1000L), con.Limits);
        }
        finally { NetworkConfig.MAX_PENDING_SERVER_PACKETS = oldCount; NetworkConfig.MAX_SEND_STALL_SECONDS = oldSeconds; }
    }

    private static void WithClock(Action<Clock> action)
    {
        var now = new Clock(); SystemClock.UseSource(() => now.Value);
        try { action(now); } finally { SystemClock.UseSystemClock(); }
    }
    private sealed class Clock { public long Value = 1000; }
    private sealed class Packet : BaseServerPacket;
    private sealed class GameConnection() : AionConnection("127.0.0.1")
    {
        public (bool, long) Authentication => (IsAuthenticated(), GetAuthTimeoutMillis());
        public (int, long) Limits => (GetMaxSendQueueSize(), GetMaxSendStallMillis());
    }
    private sealed class Link(int maxPackets = int.MaxValue, long stallMillis = long.MaxValue, long authMillis = 0, bool authenticated = true) : AConnection<Packet>(16, 16, "127.0.0.1")
    {
        public Queue<Packet> Queue { get; } = new();
        public int Disconnects;
        protected override bool IsAuthenticated() => authenticated;
        protected override long GetAuthTimeoutMillis() => authMillis;
        protected override int GetMaxSendQueueSize() => maxPackets;
        protected override long GetMaxSendStallMillis() => stallMillis;
        protected override Queue<Packet> GetSendMsgQueue() => Queue;
        protected override bool ProcessData(ByteBuffer data) => true;
        protected override bool WriteData(ByteBuffer data) => false;
        protected override void Initialized() { }
        protected override void OnDisconnect() => Disconnects++;
        public override void OnServerClose() => Close();
    }
}
