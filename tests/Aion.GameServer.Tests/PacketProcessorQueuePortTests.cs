using System.Collections.Concurrent;
using System.Reflection;
using Aion.Bots.Protocol;
using Aion.Commons.Concurrent;
using Aion.Commons.Lang;
using Aion.Commons.Nio;
using Aion.GameServer.Commons.Network;
using Aion.GameServer.Commons.Network.Packet;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Tests;

public sealed class PacketProcessorQueuePortTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PendingLimitMustBePositive(int limit) =>
        Assert.Throws<ArgumentException>(() => new PacketProcessor<AionConnection>(1, 1, 50, 3, limit));

    [Fact]
    public void LimitCountsOnlyWaitingPacketsAndDoesNotBlockAnotherConnection()
    {
        using var h = new Harness(1, 2);
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var done = new CountdownEvent(4);
        var a = new Connection(); var b = new Connection();
        Assert.True(h.Add(a, () => { started.Set(); release.Wait(); done.Signal(); }));
        Wait(started);
        try
        {
            Assert.True(h.Add(a, () => done.Signal()));
            Assert.True(h.Add(a, () => done.Signal()));
            Assert.False(h.Add(a, () => throw new Exception("Rejected packet executed")));
            Assert.True(h.Add(b, () => done.Signal()));
            Assert.Equal(3, h.Processor.QueueDepth);
        }
        finally { release.Set(); }
        Assert.True(done.Wait(TimeSpan.FromSeconds(5)));
        Assert.Equal(0, h.Processor.QueueDepth);
    }

    [Fact]
    public void ReadyConnectionsTakeTurnsWhileKeepingEachClientsReceiveOrder()
    {
        using var h = new Harness(1, 10);
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var done = new CountdownEvent(7);
        var order = new ConcurrentQueue<string>();
        var a = new Connection(); var b = new Connection(); var c = new Connection();
        void Record(string value) { order.Enqueue(value); done.Signal(); }
        Assert.True(h.Add(c, () => { started.Set(); release.Wait(); Record("c0"); }));
        Wait(started);
        try
        {
            foreach (int i in Enumerable.Range(0, 3)) Assert.True(h.Add(a, () => Record("a" + i)));
            foreach (int i in Enumerable.Range(0, 2)) Assert.True(h.Add(b, () => Record("b" + i)));
            Assert.True(h.Add(c, () => Record("c1")));
        }
        finally { release.Set(); }
        Assert.True(done.Wait(TimeSpan.FromSeconds(5)));
        Assert.Equal(new[] { "c0", "a0", "b0", "c1", "a1", "b1", "a2" }, order.ToArray());
    }

    [Fact]
    public void BusyConnectionIsSerialWhileOtherConnectionsUseFreeWorkersAndCanBeScheduledAgain()
    {
        using var h = new Harness(2, 10);
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var secondA = new ManualResetEventSlim();
        using var ranB = new ManualResetEventSlim();
        var a = new Connection(); var b = new Connection();
        Assert.True(h.Add(a, () => { started.Set(); release.Wait(); }));
        Wait(started);
        try
        {
            Assert.True(h.Add(a, secondA.Set));
            Assert.True(h.Add(b, ranB.Set));
            Wait(ranB);
            Assert.False(secondA.IsSet);
        }
        finally { release.Set(); }
        Wait(secondA);
        using var next = new ManualResetEventSlim();
        Assert.True(h.Add(a, next.Set));
        Wait(next);
    }

    [Fact]
    public void RetiringBusyWorkerRequeuesItsConnectionBeforeExiting()
    {
        using var h = new Harness(2, 10);
        using var started = new CountdownEvent(2);
        using var release = new ManualResetEventSlim();
        using var done = new CountdownEvent(2);
        var a = new Connection(); var b = new Connection();
        void Hold()
        {
            started.Signal();
            // A Java uninterruptible waiter preserves the interrupt until the packet returns.
            while (!release.IsSet) { try { release.Wait(); } catch (ThreadInterruptedException) { } }
        }
        Assert.True(h.Add(a, Hold)); Assert.True(h.Add(b, Hold));
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        try
        {
            Assert.True(h.Add(a, () => done.Signal())); Assert.True(h.Add(b, () => done.Signal()));
            var retiring = h.Threads[^1];
            typeof(PacketProcessor<AionConnection>).GetField("minThreads", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(h.Processor, 1);
            typeof(PacketProcessor<AionConnection>).GetMethod("KillThread", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(h.Processor, null);
            release.Set();
            Assert.True(retiring.Join(TimeSpan.FromSeconds(5)));
            Assert.True(done.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(0, h.Processor.QueueDepth);
        }
        finally { release.Set(); }
    }

    [Fact]
    public void GameConnectionReturnsDisconnectRequestWhenTheProcessorRefusesAReadPacket()
    {
        var connection = new RefusingConnection();
        connection.SetState(AionConnection.State.AUTHED);
        var codec = new GamePacketCodec();
        connection.InitializedInternal();
        var keyBuffer = ByteBuffer.Allocate(1024).Order(ByteOrder.LITTLE_ENDIAN);
        Assert.True(connection.WriteDataInternal(keyBuffer));
        var keyFrame = new byte[keyBuffer.Limit()];
        keyBuffer.Get(keyFrame);
        codec.RecoverKeyFromSmKeyFrame(keyFrame);
        var frame = GameClientPackets.Ping().Encode(codec, AionConnection.State.AUTHED);
        Assert.False(connection.ProcessDataInternal(ByteBuffer.Wrap(frame[2..]).Order(ByteOrder.LITTLE_ENDIAN)));
        Assert.Equal(1, connection.Refused);
    }

    private static void Wait(ManualResetEventSlim signal) => Assert.True(signal.Wait(TimeSpan.FromSeconds(5)));
    private sealed class Connection() : AionConnection("127.0.0.1");
    private sealed class RefusingConnection() : AionConnection("127.0.0.1")
    {
        public int Refused;
        protected override bool ExecutePacket(AionClientPacket packet) { Refused++; return false; }
    }
    private sealed class Probe : BaseClientPacket<AionConnection>
    {
        private readonly Action action;
        public Probe(AionConnection connection, Action action) : base(0) { SetConnection(connection); this.action = action; }
        protected override void ReadImpl() { }
        protected override void RunImpl() { }
        public override void Run() => action();
    }
    private sealed class Harness : Executor, IDisposable
    {
        public PacketProcessor<AionConnection> Processor { get; }
        public Thread[] Threads { get; }
        private readonly ConcurrentQueue<Exception> failures = new();
        public Harness(int workers, int limit)
        {
            Processor = new PacketProcessor<AionConnection>(workers, workers, 50, 3, limit, this);
            Threads = ((List<Thread>)typeof(PacketProcessor<AionConnection>).GetField("threads", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Processor)!).ToArray();
        }
        public bool Add(AionConnection connection, Action action) => Processor.ExecutePacket(new Probe(connection, action));
        public void Execute(Runnable command) { try { command.Run(); } catch (Exception ex) { failures.Enqueue(ex); } }
        public void Dispose()
        {
            foreach (var thread in Threads) if (thread.IsAlive) thread.Interrupt();
            foreach (var thread in Threads) Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
            Assert.Empty(failures);
        }
    }
}
