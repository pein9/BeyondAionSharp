using System;
using System.Collections.Generic;
using Aion.Commons.Concurrent;
using Aion.Commons.Diagnostics;
using Aion.Commons.Nio;
using Aion.Commons.Nio.Channels;
using Aion.Commons.Options;
using Aion.GameServer.Commons.Network.Packet;

namespace Aion.GameServer.Commons.Network;

/// <summary>
/// Java parity: commons/network/AConnection (-Nemesiss-). Non-generic base holding the socket-layer
/// surface the Dispatcher/NioServer operate on via Java's wildcard <c>AConnection&lt;?&gt;</c> (C# has no
/// wildcard, so wildcard-typed members live here and packet-typed members in AConnection&lt;T&gt;).
/// java.nio.channels -> Aion.Commons.Nio.Channels shim; synchronized -> lock; currentTimeMillis ->
/// UtcNow.ToUnixTimeMilliseconds; Executor.execute(this::onDisconnect) -> Execute(new LambdaRunnable).
/// </summary>
public abstract class AConnection
{
    protected readonly SocketChannel socketChannel;
    protected readonly Dispatcher dispatcher;
    protected SelectionKey key = null!;
    public long pendingCloseUntilMillis;
    protected bool closed;
    protected readonly object guard = new object();
    public readonly ByteBuffer writeBuffer;
    public readonly ByteBuffer readBuffer;
    private readonly string ip;
    private readonly bool socketless;
    private bool locked = false;
    private static readonly DispatchLatencyMetrics writeLatencyMetrics = new();
    private volatile PendingDispatchProbe? writeLatency;

    public static DispatchLatencySnapshot CaptureWriteLatency(IEnumerable<AConnection> connections)
    {
        int pending = 0;
        double oldest = 0;
        foreach (var connection in connections)
            if (connection.writeLatency?.PendingMilliseconds is double age)
            {
                pending++;
                oldest = Math.Max(oldest, age);
            }
        return writeLatencyMetrics.TakeSnapshot() with { PendingConnections = pending, OldestPendingMilliseconds = oldest };
    }

    protected void ObserveWriteQueued() => (writeLatency ??= new PendingDispatchProbe(writeLatencyMetrics)).Queued();
    protected void DiscardWriteObservation() => writeLatency?.Discard();

    protected AConnection(SocketChannel sc, Dispatcher d, int rbSize, int wbSize)
    {
        socketChannel = sc;
        dispatcher = d;
        writeBuffer = ByteBuffer.Allocate(wbSize);
        writeBuffer.Flip();
        writeBuffer.Order(ByteOrder.LITTLE_ENDIAN);
        readBuffer = ByteBuffer.Allocate(rbSize);
        readBuffer.Order(ByteOrder.LITTLE_ENDIAN);

        this.ip = socketChannel.Socket().GetInetAddress().GetHostAddress();
    }

    /// <summary>Gameplay-neutral constructor for in-process transports with no socket, dispatcher or selector.</summary>
    protected AConnection(int rbSize, int wbSize, string ip)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ip);
        socketChannel = null!;
        dispatcher = null!;
        writeBuffer = ByteBuffer.Allocate(wbSize);
        writeBuffer.Flip();
        writeBuffer.Order(ByteOrder.LITTLE_ENDIAN);
        readBuffer = ByteBuffer.Allocate(rbSize);
        readBuffer.Order(ByteOrder.LITTLE_ENDIAN);
        this.ip = ip;
        socketless = true;
    }

    internal void SetKey(SelectionKey key) => this.key = key;

    public SocketChannel GetSocketChannel() => socketChannel;

    /// <summary>Closes the connection and calls onDisconnect() on another thread. Dispatcher thread only.</summary>
    internal void Disconnect(Executor dcExecutor)
    {
        if (Assertion.NetworkAssertion)
        {
            // assert Thread.currentThread() == dispatcher;
        }

        lock (guard)
        {
            if (closed)
                return;
            closed = true;
            DiscardWriteObservation();
        }

        if (socketless)
        {
            OnDisconnect();
            return;
        }

        key.Cancel();
        try
        {
            socketChannel.Close();
        }
        catch (System.IO.IOException)
        {
        }
        key.Attach(null);

        dcExecutor.Execute(new LambdaRunnable(OnDisconnect));
    }

    public bool IsConnected() => IsTransportConnected();

    protected virtual bool IsTransportConnected() => socketless ? !closed : key.IsValid();

    protected bool IsSocketless => socketless;

    public bool IsPendingClose() => pendingCloseUntilMillis != 0 && !closed;

    public bool IsClosed() => closed;

    public string GetIP() => ip;

    internal bool TryLockConnection()
    {
        if (locked)
            return false;
        return locked = true;
    }

    internal void UnlockConnection() => locked = false;

    /// <summary>Closes this connection (regular, no close packet).</summary>
    public abstract void Close();

    /// <summary>True if there are no pending outgoing packets (wildcard-safe accessor for the send queue).</summary>
    internal abstract bool IsSendQueueEmpty();

    protected abstract bool ProcessData(ByteBuffer data);
    internal bool ProcessDataInternal(ByteBuffer data) => ProcessData(data);

    protected abstract bool WriteData(ByteBuffer data);
    internal bool WriteDataInternal(ByteBuffer data)
    {
        writeLatency?.Dispatched();
        return WriteData(data);
    }

    protected abstract void Initialized();
    internal void InitializedInternal() => Initialized();

    protected abstract void OnDisconnect();

    public abstract void OnServerClose();
}

/// <summary>
/// Java parity: AConnection&lt;T extends BaseServerPacket&gt; — adds the packet-typed send/close surface.
/// </summary>
public abstract class AConnection<T> : AConnection where T : BaseServerPacket
{
    protected AConnection(SocketChannel sc, Dispatcher d, int rbSize, int wbSize) : base(sc, d, rbSize, wbSize)
    {
    }

    protected AConnection(int rbSize, int wbSize, string ip) : base(rbSize, wbSize, ip)
    {
    }

    /// <summary>Sends the ServerPacket to this client.</summary>
    public void SendPacket(T serverPacket)
    {
        lock (guard)
        {
            if (pendingCloseUntilMillis != 0 || closed)
                return;

            if (IsConnected())
            {
                EnqueuePacket(serverPacket, closing: false);
            }
            else
            {
                Close();
            }
        }
    }

    public override void Close() => Close(null);

    public virtual void Close(T? closePacket)
    {
        lock (guard)
        {
            if (pendingCloseUntilMillis != 0 || closed)
                return;

#pragma warning disable RS0030 // Socket close grace periods remain tied to real network time.
            pendingCloseUntilMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 2000;
#pragma warning restore RS0030
            if (closePacket != null || !IsConnected())
                ClearPendingPackets();
            if (closePacket != null && IsConnected())
                EnqueuePacket(closePacket, closing: true);
            RequestClose();
        }
    }

    internal override bool IsSendQueueEmpty() => GetSendMsgQueue().Count == 0;

    protected virtual void ClearPendingPackets()
    {
        GetSendMsgQueue().Clear();
        DiscardWriteObservation();
    }

    /// <summary>Queues a packet and signals the transport. Socketless subclasses override this to capture packets.</summary>
    protected virtual void EnqueuePacket(T packet, bool closing)
    {
        GetSendMsgQueue().Enqueue(packet);
        if (IsSocketless)
            return;
        ObserveWriteQueued();
        key.InterestOps(closing ? SelectionKey.OP_WRITE : key.InterestOps() | SelectionKey.OP_WRITE);
        if (!closing)
            key.Selector().Wakeup();
    }

    private void RequestClose()
    {
        if (IsSocketless)
        {
            Disconnect(new InlineExecutor());
            return;
        }
        dispatcher.CloseConnection(this);
        key.Selector().Wakeup(); // notify dispatcher
    }

    private sealed class InlineExecutor : Executor
    {
        public void Execute(Runnable command) => command.Run();
    }

    protected abstract Queue<T> GetSendMsgQueue();
}
