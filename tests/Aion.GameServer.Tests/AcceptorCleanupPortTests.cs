using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Concurrent;
using Aion.Commons.Lang;
using Aion.Commons.Nio;
using Aion.Commons.Nio.Channels;
using Aion.GameServer.Commons.Network;
using Aion.GameServer.Commons.Network.Packet;

namespace Aion.GameServer.Tests;

public sealed class AcceptorCleanupPortTests
{
    [Theory]
    [InlineData("io")]
    [InlineData("runtime")]
    [InlineData("initialized")]
    [InlineData("null")]
    public async Task RejectedAndFailedAcceptanceClosesTheRealAcceptedSocket(string mode)
    {
        var dispatcher = new AcceptReadWriteDispatcherImpl("accept-test", new InlineExecutor());
        var server = new NioServer(0);
        typeof(NioServer).GetField("acceptDispatcher", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(server, dispatcher);
        var listening = ServerSocketChannel.Open(); listening.ConfigureBlocking(false);
        listening.Socket().Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var raw = (Socket)typeof(SelectableChannel).GetField("socket", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(listening)!;
        using var peer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            var acceptor = new Acceptor(new FailingFactory(mode), server);
            var key = listening.Register(dispatcher.Selector(), SelectionKey.OP_ACCEPT, acceptor);
            await peer.ConnectAsync(raw.LocalEndPoint!);
            Exception? error = Record.Exception(() => acceptor.Accept(key));
            if (mode == "null") Assert.Null(error);
            else if (mode == "io") Assert.IsType<IOException>(error);
            else Assert.IsType<InvalidOperationException>(error);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Assert.Equal(0, await peer.ReceiveAsync(new byte[1], SocketFlags.None, timeout.Token));
        }
        finally { raw.Dispose(); dispatcher.Selector().Close(); }
    }
    private sealed class FailingFactory(string mode) : ConnectionFactory
    {
        public AConnection Create(SocketChannel channel, Dispatcher dispatcher) => mode switch
        {
            "io" => throw new IOException("factory probe"),
            "runtime" => throw new InvalidOperationException("factory probe"),
            "null" => null!,
            _ => new FailingConnection(channel, dispatcher)
        };
    }
    private sealed class InlineExecutor : Executor { public void Execute(Runnable command) => command.Run(); }
    private sealed class Packet : BaseServerPacket;
    private sealed class FailingConnection(SocketChannel channel, Dispatcher dispatcher) : AConnection<Packet>(channel, dispatcher, 16, 16)
    {
        private readonly Queue<Packet> pending = new();
        protected override Queue<Packet> GetSendMsgQueue() => pending;
        protected override bool ProcessData(ByteBuffer data) => true;
        protected override bool WriteData(ByteBuffer data) => false;
        protected override void Initialized() => throw new InvalidOperationException("initialized probe");
        protected override void OnDisconnect() { }
        public override void OnServerClose() => Close();
    }
}
