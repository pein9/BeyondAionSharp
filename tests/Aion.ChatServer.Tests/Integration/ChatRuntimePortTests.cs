using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.ChatServer.Configuration;
using Aion.ChatServer.Handlers;
using Aion.ChatServer.Models.Channels;
using Aion.ChatServer.Network;
using Aion.ChatServer.Network.Handlers;
using Aion.ChatServer.Network.Packets;
using Aion.ChatServer.Services;
using Aion.Commons.Network.Server;
using Microsoft.Extensions.Logging.Abstractions;
using static Aion.ChatServer.Tests.Network.ChatWriteWatermarkPortTests;

namespace Aion.ChatServer.Tests.Integration;

public sealed class ChatRuntimePortTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KnownTruncatedBodiesCloseBothPorts(bool gameServer)
    {
        await using var f = await Fixture.CreateAsync(gameServer);
        await f.Peer.GetStream().WriteAsync(ChatPacketFrameCodec.CreateFrame(new byte[] { gameServer ? (byte)0 : (byte)5 }));
        await f.ExpectClosedAsync();
    }

    [Fact]
    public async Task UnknownOpcodeIsIgnoredAndTheNextCompletePacketStillRuns()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Peer.GetStream().WriteAsync(ChatPacketFrameCodec.CreateFrame(new byte[] { 127 }));
        var init = new byte[16]; init[0] = 0x30;
        await f.Peer.GetStream().WriteAsync(ChatPacketFrameCodec.CreateFrame(init));
        var response = new byte[10];
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await f.Peer.GetStream().ReadExactlyAsync(response, timeout.Token);
        Assert.Equal(0x31, response[2]);
    }

    [Theory]
    [InlineData(59999, false)]
    [InlineData(60000, true)]
    public async Task AuthenticationDeadlineFiresAtTheScheduledBoundary(int elapsed, bool closes)
    {
        await using var f = await Fixture.CreateAsync(authSeconds: 60);
        f.Clock.Advance(elapsed);
        if (closes) await f.ExpectClosedAsync();
        else Assert.False(f.Peer.Client.Poll(0, SelectMode.SelectRead));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task NonPositiveAuthenticationDeadlineIsDisabled(int seconds)
    {
        await using var f = await Fixture.CreateAsync(authSeconds: seconds);
        f.Clock.Advance(600000);
        Assert.False(f.Peer.Client.Poll(0, SelectMode.SelectRead));
    }

    [Fact]
    public async Task AuthenticatedClientIsExemptAndTimerIsCancelledOnClose()
    {
        await using var f = await Fixture.CreateAsync(authSeconds: 60);
        f.SetAuthed(); f.Clock.Advance(60000);
        Assert.False(f.Peer.Client.Poll(0, SelectMode.SelectRead));
        await f.Connection.CloseAsync(); await f.Running.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, f.Clock.ActiveTimers);
    }

    [Fact]
    public async Task OversizedPendingWritesAbortBeforeWaitingForTheSendLock()
    {
        await using var f = await Fixture.CreateAsync();
        await f.SendLock.WaitAsync();
        var sends = new List<Task>();
        try
        {
            for (int i = 0; i < 24; i++) sends.Add(f.ClientConnection.SendPacketAsync(new LargeClientPacket(15997)));
            await f.ExpectClosedAsync(waitForLoop: false);
        }
        finally { f.SendLock.Release(); }
        await Task.WhenAll(sends).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, f.PendingBytes);
    }

    [Fact]
    public async Task SendStallClosesWithoutAnyFurtherSend()
    {
        await using var f = await Fixture.CreateAsync();
        await f.SendLock.WaitAsync();
        var sends = Enumerable.Range(0, 5).Select(_ => f.ClientConnection.SendPacketAsync(new LargeClientPacket(15997))).ToArray();
        try
        {
            f.Clock.Advance(59999); Assert.False(f.Peer.Client.Poll(0, SelectMode.SelectRead));
            f.Clock.Advance(1); await f.ExpectClosedAsync(waitForLoop: false);
        }
        finally { f.SendLock.Release(); }
        await Task.WhenAll(sends).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task BecomingWritableCancelsTheOldStallDeadline()
    {
        await using var f = await Fixture.CreateAsync();
        await f.SendLock.WaitAsync();
        var sends = Enumerable.Range(0, 5).Select(_ => f.ClientConnection.SendPacketAsync(new LargeClientPacket(15997))).ToArray();
        f.Clock.Advance(59000);
        f.SendLock.Release();
        var drain = new byte[5 * 16000];
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await f.Peer.GetStream().ReadExactlyAsync(drain, timeout.Token); await Task.WhenAll(sends);
        await f.SendLock.WaitAsync();
        sends = Enumerable.Range(0, 5).Select(_ => f.ClientConnection.SendPacketAsync(new LargeClientPacket(15997))).ToArray();
        try
        {
            f.Clock.Advance(1000); Assert.False(f.Peer.Client.Poll(0, SelectMode.SelectRead));
            f.Clock.Advance(59000); await f.ExpectClosedAsync(waitForLoop: false);
        }
        finally { f.SendLock.Release(); }
        await Task.WhenAll(sends).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ActualStalledWriteIsAbortedAndEveryWaitingSendCompletes()
    {
        await using var f = await Fixture.CreateAsync();
        f.Peer.ReceiveBufferSize = 1024; f.Accepted.SendBufferSize = 1024;
        var sends = Enumerable.Range(0, 200).Select(_ => f.ClientConnection.SendPacketAsync(new LargeClientPacket(15997))).ToArray();
        await Task.WhenAll(sends).WaitAsync(TimeSpan.FromSeconds(10));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var bytes = new byte[16384];
        while (await f.Peer.GetStream().ReadAsync(bytes, timeout.Token) != 0) { }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EncoderFailureClosesItsConnection(bool gameServer)
    {
        await using var f = await Fixture.CreateAsync(gameServer);
        if (gameServer) await ((GsConnection)f.Connection).SendPacketAsync(new LargeServerPacket(65534));
        else await f.ClientConnection.SendPacketAsync(new LargeClientPacket(16382));
        await f.ExpectClosedAsync();
    }

    [Fact]
    public async Task MaximumAuthenticationTimeoutDoesNotOverflowOrExceedTheTimerApiLimit()
    {
        await using var f = await Fixture.CreateAsync(authSeconds: int.MaxValue);
        Assert.Equal(1, f.Clock.ActiveTimers);
        f.Clock.Advance(1);
        Assert.False(f.Peer.Client.Poll(0, SelectMode.SelectRead));
    }

    [Theory]
    [InlineData(29999, false)]
    [InlineData(30000, true)]
    public async Task GameServerAuthenticationDeadlineUsesThirtySeconds(int elapsed, bool closes)
    {
        await using var f = await Fixture.CreateAsync(gameServer: true);
        f.Clock.Advance(elapsed);
        if (closes) await f.ExpectClosedAsync();
        else Assert.False(f.Peer.Client.Poll(0, SelectMode.SelectRead));
    }

    [Fact]
    public async Task UnauthenticatedTimeoutDoesNotMarkTheRegisteredServerOffline()
    {
        await using var f = await Fixture.CreateAsync(gameServer: true);
        Assert.Equal(GsAuthResponse.Authed, f.GameService.RegisterGameServer(1, "", fromLoopback: true));
        f.Clock.Advance(30000); await f.ExpectClosedAsync();
        Assert.True(f.GameService.IsOnline); Assert.Equal((byte)1, f.GameService.GameServerId);
    }

    [Fact]
    public async Task AuthenticatedGameServerIsExemptFromDeadline()
    {
        await using var f = await Fixture.CreateAsync(gameServer: true);
        await f.Peer.GetStream().WriteAsync(ChatPacketFrameCodec.CreateFrame(new byte[] { 0, 1, 0, 0 }));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var header = new byte[2]; await f.Peer.GetStream().ReadExactlyAsync(header, timeout.Token);
        var body = new byte[System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(header) - 2];
        await f.Peer.GetStream().ReadExactlyAsync(body, timeout.Token); Assert.Equal(0, body[1]);
        f.Clock.Advance(30000); Assert.False(f.Peer.Client.Poll(0, SelectMode.SelectRead));
        Assert.True(f.GameService.IsOnline);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public TcpClient Peer = null!, Accepted = null!;
        public BaseClientConnection Connection = null!;
        public Task Running = null!;
        public ManualClock Clock = new();
        public GameServerService GameService = null!;
        public ClientChannelHandler ClientConnection => (ClientChannelHandler)Connection;
        public SemaphoreSlim SendLock => (SemaphoreSlim)typeof(ClientChannelHandler).GetField("_sendLock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Connection)!;
        public long PendingBytes => ((ChatClientWriteState)typeof(ClientChannelHandler).GetField("_writes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Connection)!).PendingBytes;
        public void SetAuthed() => typeof(ClientChannelHandler).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Connection, ChatClientConnectionState.Authed);
        public static async Task<Fixture> CreateAsync(bool gameServer = false, int authSeconds = 0)
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            var f = new Fixture { Peer = new TcpClient() };
            var connecting = f.Peer.ConnectAsync((IPEndPoint)listener.LocalEndpoint);
            f.Accepted = await listener.AcceptTcpClientAsync(); await connecting;
            var options = new ChatServerOptions { ClientAuthTimeoutSeconds = authSeconds };
            var channels = new ChatChannels(NullLogger<ChatChannels>.Instance);
            var broadcast = new BroadcastService(NullLogger<BroadcastService>.Instance);
            var chat = new ChatService(channels, broadcast, NullLogger<ChatService>.Instance);
            f.GameService = new GameServerService(options, NullLogger<GameServerService>.Instance);
            f.Connection = gameServer
                ? new GsConnection(NullLogger.Instance, f.Accepted, "runtime-gs", f.GameService, chat, options, f.Clock)
                : new ClientChannelHandler(NullLogger.Instance, f.Accepted, "runtime-client", chat, channels, broadcast,
                    new ChatHandlerRegistry([], NullLogger<ChatHandlerRegistry>.Instance), options, f.Clock);
            f.Running = f.Connection.RunAsync();
            return f;
        }
        public async Task ExpectClosedAsync(bool waitForLoop = true)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Assert.Equal(0, await Peer.GetStream().ReadAsync(new byte[1], timeout.Token));
            if (waitForLoop) await Running.WaitAsync(TimeSpan.FromSeconds(5));
        }
        public async ValueTask DisposeAsync()
        {
            Peer.Dispose(); Accepted.Dispose(); await Connection.DisposeAsync(); await Running.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        private readonly object gate = new();
        private readonly List<ManualTimer> timers = new();
        private long now;
        public int ActiveTimers { get { lock (gate) return timers.Count(t => !t.Disposed); } }
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeMilliseconds(now);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            lock (gate)
            {
                var timer = new ManualTimer(this, callback, state); timers.Add(timer); timer.Change(dueTime, period); return timer;
            }
        }
        public void Advance(long millis)
        {
            ManualTimer[] ready;
            lock (gate) { now += millis; ready = timers.Where(t => !t.Disposed && t.Due <= now).ToArray(); foreach (var t in ready) t.Due = long.MaxValue; }
            foreach (var t in ready) t.Callback(t.State);
        }
        private sealed class ManualTimer(ManualClock owner, TimerCallback callback, object? state) : ITimer
        {
            public TimerCallback Callback = callback;
            public object? State = state;
            public long Due;
            public bool Disposed;
            public bool Change(TimeSpan dueTime, TimeSpan period) { lock (owner.gate) { Due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : owner.now + (long)dueTime.TotalMilliseconds; return true; } }
            public void Dispose() { lock (owner.gate) Disposed = true; }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
