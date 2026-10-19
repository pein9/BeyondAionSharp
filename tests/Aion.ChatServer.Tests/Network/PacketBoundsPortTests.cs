using Aion.ChatServer.Network;
using Aion.ChatServer.Network.Packets;
using Aion.ChatServer.Network.Packets.GameServer;
using System.Collections.Concurrent;
using System.Reflection;
using Aion.Commons.Logging;
using Microsoft.Extensions.Logging;

namespace Aion.ChatServer.Tests.Network;

[CollectionDefinition("Chat packet warning state", DisableParallelization = true)]
public sealed class ChatPacketWarningCollection { }

// The warning dictionaries are process-wide; other packet tests can consume the same opcode's first warning.
[Collection("Chat packet warning state")]
public sealed class PacketBoundsPortTests
{
    [Fact]
    public void ShortPacketWarningsAreOncePerOpcodeAndAllRepeatsRemainRejected()
    {
        var reads = typeof(GsPacketFactory).Assembly.GetType("Aion.ChatServer.Network.Packets.ChatPacketReads")!;
        ((ConcurrentDictionary<int, byte>)reads.GetField("TooShortWarnings", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).Clear();
        using var capture = new WarningCapture();
        using var factory = LoggerFactory.Create(b => b.ClearProviders().SetMinimumLevel(LogLevel.Trace).AddProvider(capture));
        using var logging = AionLog.OverrideFactory(factory);
        for (int i = 0; i < 3; i++)
        {
            foreach (byte opcode in new byte[] { 0, 2 })
            {
                using var payload = ChatPacketFrameCodec.CreatePayloadBuffer(ChatPacketFrameCodec.CreateFrame(new byte[] { opcode }));
                Assert.Null(GsPacketFactory.Create(payload, opcode == 0 ? GameServerConnectionState.Connected : GameServerConnectionState.Authed));
            }
        }
        Assert.Equal(2, capture.Messages.Count(m => m.Contains("shorter than expected")));
    }

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

    private sealed class WarningCapture : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Logger(Messages);
        public void Dispose() { }
        private sealed class Logger(ConcurrentQueue<string> messages) : ILogger
        {
            public bool IsEnabled(LogLevel level) => true;
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> format)
            { if (level == LogLevel.Warning) messages.Enqueue(format(state, error)); }
        }
    }
}
