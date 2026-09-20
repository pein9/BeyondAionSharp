using System.Net.Sockets;
using Aion.Commons.Concurrent;
using Aion.Commons.Lang;
using Aion.Commons.Nio;
using Aion.Commons.Nio.Channels;
using Aion.GameServer.Commons.Network;
using Aion.GameServer.Commons.Network.Packet;

namespace Aion.GameServer.Tests;

[Collection("LoopbackSockets")]
public sealed class ConnectionWriteTelemetryTests
{
    [Fact]
    public void SocketQueuePreparationAndDiscardAreObservedButSocketlessTransportIsExcluded()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var channel = SocketChannel.Open(socket);
        var dispatcher = new UnstartedDispatcher();
        var connection = new Connection(channel, dispatcher);
        dispatcher.Register(channel, SelectionKey.OP_READ, connection);
        _ = AConnection.CaptureWriteLatency([]);
        try
        {
            connection.SendPacket(new Packet()); connection.SendPacket(new Packet());
            var pending = AConnection.CaptureWriteLatency([connection]);
            Assert.Equal(1, pending.PendingConnections);
            Assert.True(pending.OldestPendingMilliseconds >= 0);
            Assert.Equal(0, pending.Count);
            Assert.True(connection.WriteDataInternal(ByteBuffer.Allocate(8)));
            var dispatched = AConnection.CaptureWriteLatency([connection]);
            Assert.Equal(1, dispatched.Count);
            Assert.Equal(0, dispatched.PendingConnections);
            connection.Clear(); // No pending observation remains from the dispatched batch.
            connection.SendPacket(new Packet()); connection.Clear();
            Assert.Equal(1, AConnection.CaptureWriteLatency([connection]).Abandoned);
            connection.SendPacket(new Packet()); connection.Disconnect(new InlineExecutor());
            Assert.Equal(1, AConnection.CaptureWriteLatency([connection]).Abandoned);

            var socketless = new Connection();
            socketless.SendPacket(new Packet());
            Assert.True(socketless.WriteDataInternal(ByteBuffer.Allocate(8)));
            var simulation = AConnection.CaptureWriteLatency([socketless]);
            Assert.Equal(0, simulation.Count);
            Assert.Equal(0, simulation.PendingConnections);
            socketless.Disconnect(new InlineExecutor());
        }
        finally { dispatcher.Selector().Close(); _ = AConnection.CaptureWriteLatency([]); }
    }

    private sealed class InlineExecutor : Executor { public void Execute(Runnable command) => command.Run(); }
    private sealed class UnstartedDispatcher() : Dispatcher("telemetry-test", new InlineExecutor())
    {
        internal override void CloseConnection(AConnection con) => con.Disconnect(new InlineExecutor());
        internal override void Dispatch() => throw new InvalidOperationException("No dispatcher thread is started in this wiring test.");
    }
    private sealed class Packet : BaseServerPacket { }
    private sealed class Connection : AConnection<Packet>
    {
        private readonly Queue<Packet> queue = new();
        public Connection(SocketChannel channel, Dispatcher dispatcher) : base(channel, dispatcher, 8, 8) { }
        public Connection() : base(8, 8, "127.0.0.1") { }
        public void Clear() => ClearPendingPackets();
        protected override Queue<Packet> GetSendMsgQueue() => queue;
        protected override bool WriteData(ByteBuffer data) { if (!queue.TryDequeue(out _)) return false; data.Put((byte)1); data.Flip(); return true; }
        protected override bool ProcessData(ByteBuffer data) => true;
        protected override void Initialized() { }
        protected override void OnDisconnect() { }
        public override void OnServerClose() => Close();
    }
}
