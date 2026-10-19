using System.Net;
using System.Net.Sockets;
using System.Text;
using Aion.Commons.Concurrent;
using Aion.Commons.Lang;
using Aion.Commons.Nio;
using Aion.Commons.Nio.Channels;
using Aion.GameServer.Commons.Network;
using Aion.GameServer.Network;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Tests;

public sealed class ClientPacketBoundsPortTests
{
    [Theory]
    [InlineData("D", 3)]
    [InlineData("C", 0)]
    [InlineData("UC", 0)]
    [InlineData("H", 1)]
    [InlineData("UH", 1)]
    [InlineData("DF", 7)]
    [InlineData("F", 3)]
    [InlineData("Q", 7)]
    [InlineData("S", 0)]
    [InlineData("S", 2)]
    [InlineData("S", 3)]
    public void TruncatedFieldAbortsThePacketBeforeLaterReadsOrExecution(string kind, int bytes)
    {
        var packet = new Probe(kind);
        packet.SetConnection(new SocketlessConnection());
        packet.SetBuffer(ByteBuffer.Wrap(Enumerable.Repeat((byte)1, bytes).ToArray()).Order(ByteOrder.LITTLE_ENDIAN));
        Assert.False(packet.Read());
        Assert.False(packet.ReadCompleted);
        Assert.Null(packet.Value);
    }

    [Theory]
    [MemberData(nameof(ValidFields))]
    public void CompleteFieldsKeepTheirExactWireValues(string kind, byte[] body, object expected)
    {
        var packet = new Probe(kind);
        packet.SetConnection(new SocketlessConnection());
        packet.SetBuffer(ByteBuffer.Wrap(body).Order(ByteOrder.LITTLE_ENDIAN));
        Assert.True(packet.Read());
        Assert.True(packet.ReadCompleted);
        if (expected is byte[] bytes) Assert.Equal(bytes, Assert.IsType<byte[]>(packet.Value));
        else Assert.Equal(expected, packet.Value);
    }

    public static TheoryData<string, byte[], object> ValidFields => new()
    {
        { "D", BitConverter.GetBytes(unchecked((int)0x81234567)), unchecked((int)0x81234567) },
        { "C", new byte[] { 254 }, (byte)254 },
        { "UC", new byte[] { 254 }, 254 },
        { "H", new byte[] { 254, 255 }, (short)-2 },
        { "UH", new byte[] { 254, 255 }, 65534 },
        { "DF", BitConverter.GetBytes(-4.5), -4.5 },
        { "F", BitConverter.GetBytes(1.25f), 1.25f },
        { "Q", BitConverter.GetBytes(long.MinValue + 7), long.MinValue + 7 },
        { "S", Encoding.Unicode.GetBytes("A\U0001F600\0"), "A\U0001F600" },
        { "B", new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 3 } },
    };

    [Theory]
    [InlineData(-1)]
    [InlineData(4 * 1024 * 1024)]
    public void ForgedBlobLengthIsRejectedBeforeAllocatingTheRequestedArray(int length)
    {
        var packet = new Probe("counted");
        packet.SetConnection(new SocketlessConnection());
        packet.SetBuffer(ByteBuffer.Wrap(BitConverter.GetBytes(length)).Order(ByteOrder.LITTLE_ENDIAN));
        long before = GC.GetAllocatedBytesForCurrentThread();
        Assert.False(packet.Read());
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.False(packet.ReadCompleted);
        Assert.True(allocated < 1024 * 1024, $"Rejected blob allocated {allocated} bytes.");
    }

    [Theory]
    [InlineData(17)]
    [InlineData(65535)]
    public async Task OversizedUnsignedLengthClosesTheRealSocketWithoutWaitingForItsBody(int size)
    {
        await using var fixture = await SocketFixture.CreateAsync();
        await fixture.SendAsync(new byte[] { (byte)size, (byte)(size >> 8), 0xAA });
        await fixture.ReadOnceAsync(3);
        Assert.True(fixture.Connection.IsClosed());
        Assert.Equal(1, fixture.Connection.Disconnects);
        Assert.Empty(fixture.Connection.Bodies);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.Equal(0, await fixture.Client.ReceiveAsync(new byte[1], SocketFlags.None, timeout.Token));
    }

    [Fact]
    public async Task FragmentedAndConsecutiveFramesWaitForCompleteBodiesAndRetainOrder()
    {
        await using var fixture = await SocketFixture.CreateAsync();
        await fixture.SendAsync(new byte[] { 7, 0 });
        await fixture.ReadOnceAsync(2);
        Assert.Empty(fixture.Connection.Bodies);
        await fixture.SendAsync(new byte[] { 1, 2 });
        await fixture.ReadOnceAsync(2);
        Assert.Empty(fixture.Connection.Bodies);
        await fixture.SendAsync(new byte[] { 3, 4, 5 });
        await fixture.ReadOnceAsync(3);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, Assert.Single(fixture.Connection.Bodies));
        await fixture.SendAsync(new byte[] { 4, 0, 6, 7, 4, 0, 8, 9 });
        await fixture.ReadOnceAsync(8);
        Assert.Equal(3, fixture.Connection.Bodies.Count);
        Assert.Equal(new byte[] { 6, 7 }, fixture.Connection.Bodies[1]);
        Assert.Equal(new byte[] { 8, 9 }, fixture.Connection.Bodies[2]);
        Assert.False(fixture.Connection.IsClosed());
    }

    [Fact]
    public async Task AFrameExactlyAtBufferCapacityIsAccepted()
    {
        await using var fixture = await SocketFixture.CreateAsync();
        var frame = Enumerable.Repeat((byte)0x5A, 16).ToArray();
        frame[0] = 16; frame[1] = 0;
        await fixture.SendAsync(frame);
        await fixture.ReadOnceAsync(16);
        Assert.Equal(frame[2..], Assert.Single(fixture.Connection.Bodies));
        Assert.False(fixture.Connection.IsClosed());
    }

    [Fact]
    public void NativeServerFrameAdapterUsesStrictReadsByDefault()
    {
        using var payload = ServerPacketFrameCodec.CreatePayloadBuffer(ServerPacketFrameCodec.CreateFrame(new byte[] { 1 }));
        Assert.Throws<EndOfStreamException>(() => payload.ReadD());
    }

    private sealed class Probe(string kind) : AionClientPacket(0, new HashSet<AionConnection.State>())
    {
        public object? Value;
        public bool ReadCompleted;
        protected override void ReadImpl()
        {
            Value = kind switch
            {
                "D" => ReadD(), "C" => ReadC(), "UC" => ReadUC(), "H" => ReadH(), "UH" => ReadUH(),
                "DF" => ReadDF(), "F" => ReadF(), "Q" => ReadQ(), "S" => ReadS(), "B" => ReadB(3),
                "counted" => ReadB(ReadD()), _ => throw new InvalidOperationException(kind)
            };
            ReadCompleted = true;
        }
        protected override void RunImpl() { }
    }

    private sealed class SocketlessConnection() : AionConnection("127.0.0.1");
    private sealed class InlineExecutor : Executor { public void Execute(Runnable command) => command.Run(); }

    private sealed class RecordingConnection(SocketChannel channel, Dispatcher dispatcher) : AConnection(channel, dispatcher, 16, 16)
    {
        public List<byte[]> Bodies { get; } = new();
        public int Disconnects;
        public override void Close() => Disconnect(new InlineExecutor());
        internal override bool IsSendQueueEmpty() => true;
        protected override bool ProcessData(ByteBuffer data)
        {
            var body = new byte[data.Remaining()]; data.Get(body); Bodies.Add(body); return true;
        }
        protected override bool WriteData(ByteBuffer data) => false;
        protected override void Initialized() { }
        protected override void OnDisconnect() => Disconnects++;
        public override void OnServerClose() => Close();
    }

    private sealed class SocketFixture : IAsyncDisposable
    {
        public Socket Client = null!;
        private Socket server = null!;
        private AcceptReadWriteDispatcherImpl dispatcher = null!;
        private SocketChannel channel = null!;
        public RecordingConnection Connection = null!;
        public static async Task<SocketFixture> CreateAsync()
        {
            using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0)); listener.Listen(1);
            var fixture = new SocketFixture { Client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) };
            var connect = fixture.Client.ConnectAsync(listener.LocalEndPoint!);
            fixture.server = await listener.AcceptAsync(); await connect;
            fixture.channel = SocketChannel.Open(fixture.server); fixture.channel.ConfigureBlocking(false);
            fixture.dispatcher = new AcceptReadWriteDispatcherImpl("packet-bounds-test", new InlineExecutor());
            fixture.Connection = new RecordingConnection(fixture.channel, fixture.dispatcher);
            fixture.dispatcher.Register(fixture.channel, SelectionKey.OP_READ, fixture.Connection);
            return fixture;
        }
        public async Task SendAsync(byte[] bytes)
        {
            int offset = 0;
            while (offset < bytes.Length) offset += await Client.SendAsync(bytes.AsMemory(offset), SocketFlags.None);
        }
        public async Task ReadOnceAsync(int available)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (server.Available < available) await Task.Delay(1, timeout.Token);
            dispatcher.Read(channel.KeyFor(dispatcher.Selector())!);
        }
        public ValueTask DisposeAsync()
        {
            Connection.Close(); Client.Dispose(); server.Dispose(); dispatcher.Selector().Close();
            return ValueTask.CompletedTask;
        }
    }
}
