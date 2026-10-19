using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using Aion.Commons.Concurrent;
using Aion.Commons.Lang;
using Aion.GameServer.Commons.Network.Packet;

namespace Aion.GameServer.Commons.Network;

/// <summary>
/// Java parity: commons/network/PacketProcessor (-Nemesiss-). Executes client packets in receive order
/// with one-packet-per-client-at-a-time. &lt;T extends AConnection&lt;?&gt;&gt; -> where T : AConnection.
/// ReentrantLock+Condition -> Monitor (lock + Wait/Pulse); Thread.ofPlatform -> Thread; thread kill via
/// Interrupt()/ThreadInterruptedException. Platform-boundary threading adaptation.
/// </summary>
public class PacketProcessor<T> where T : AConnection
{
    private static readonly ILogger log = AionLog.For("PacketProcessor");

    private readonly int threadSpawnThreshold;
    private readonly int threadKillThreshold;
    private readonly int maxPendingPacketsPerConnection;

    private readonly object lockObj = new object();
    private readonly Queue<AConnection> readyConnections = new();
    private int pendingPacketCount;
    private readonly HashSet<Thread> retiringThreads = new();
    private readonly List<Thread> threads = new List<Thread>();

    private readonly int minThreads;
    private readonly int maxThreads;
    private readonly Executor executor;

    private sealed class DummyExecutor : Executor
    {
        public void Execute(Runnable command) => command.Run();
    }

    public PacketProcessor(int minThreads, int maxThreads, int threadSpawnThreshold, int threadKillThreshold, int maxPendingPacketsPerConnection)
        : this(minThreads, maxThreads, threadSpawnThreshold, threadKillThreshold, maxPendingPacketsPerConnection, new DummyExecutor())
    {
    }

    public PacketProcessor(int minThreads, int maxThreads, int threadSpawnThreshold, int threadKillThreshold, int maxPendingPacketsPerConnection, Executor executor)
    {
        CheckArgument(minThreads > 0, "Min Threads must be positive");
        CheckArgument(maxThreads >= minThreads, "Max Threads must be >= Min Threads");
        CheckArgument(threadSpawnThreshold > 0, "Thread Spawn Threshold must be positive");
        CheckArgument(threadKillThreshold > 0, "Thread Kill Threshold must be positive");
        CheckArgument(maxPendingPacketsPerConnection > 0, "Max pending packets per connection must be positive");

        this.minThreads = minThreads;
        this.maxThreads = maxThreads;
        this.threadSpawnThreshold = threadSpawnThreshold;
        this.threadKillThreshold = threadKillThreshold;
        this.maxPendingPacketsPerConnection = maxPendingPacketsPerConnection;
        this.executor = executor;

        if (minThreads != maxThreads)
            StartCheckerThread();

        for (int i = 0; i < minThreads; i++)
            NewThread();
    }

    private void CheckArgument(bool condition, string errorMessage)
    {
        if (!condition)
            throw new ArgumentException(errorMessage);
    }

    private void StartCheckerThread()
    {
        Thread t = new Thread(CheckerTask) { Name = "PacketProcessor:Checker", IsBackground = true };
        t.Start();
    }

    private bool NewThread()
    {
        if (threads.Count >= maxThreads)
            return false;

        string name = "PacketProcessor:" + threads.Count;
        log.LogDebug("Creating new PacketProcessor Thread: " + name);

        Thread t = new Thread(PacketProcessorTask) { Name = name, IsBackground = true };
        threads.Add(t);
        t.Start();

        return true;
    }

    private void KillThread()
    {
        lock (lockObj)
        {
            if (threads.Count <= minThreads) return;
            Thread t = threads[^1];
            threads.RemoveAt(threads.Count - 1);
            retiringThreads.Add(t);
            log.LogDebug("Killing PacketProcessor Thread: {Name}", t.Name);
            t.Interrupt();
        }
    }

    /// <summary>Queues in receive order; false means the per-connection pending limit was reached.</summary>
    public bool ExecutePacket(BaseClientPacket<T> packet)
    {
        AConnection connection = packet.GetConnection();
        lock (lockObj)
        {
            if (connection.pendingPackets.Count >= maxPendingPacketsPerConnection)
                return false;
            connection.pendingPackets.Enqueue(packet);
            pendingPacketCount++;
            if (!connection.scheduledForProcessing)
            {
                connection.scheduledForProcessing = true;
                readyConnections.Enqueue(connection);
                Monitor.Pulse(lockObj);
            }
            return true;
        }
    }

    /// <summary>Number waiting, excluding packets currently executing.</summary>
    public int QueueDepth { get { lock (lockObj) return pendingPacketCount; } }

    // Caller holds the processor lock. A connection remains scheduled during execution.
    private (AConnection Connection, Runnable Packet) TakeNextPacket()
    {
        while (readyConnections.Count == 0)
            Monitor.Wait(lockObj);
        pendingPacketCount--;
        AConnection connection = readyConnections.Dequeue();
        return (connection, connection.pendingPackets.Dequeue());
    }

    private void FinishPacket(AConnection connection)
    {
        if (connection.pendingPackets.Count == 0)
            connection.scheduledForProcessing = false;
        else
        {
            readyConnections.Enqueue(connection);
            Monitor.Pulse(lockObj);
        }
    }

    private void PacketProcessorTask()
    {
        AConnection? connection = null;
        try
        {
            for (;;)
            {
                Runnable packet;
                lock (lockObj)
                {
                    if (connection != null)
                    {
                        FinishPacket(connection);
                        connection = null;
                    }
                    // C# interrupt flags are observed only at blocking calls. Track retirement
                    // explicitly so a busy worker finishes/requeues its connection before exiting.
                    if (retiringThreads.Remove(Thread.CurrentThread)) return;
                    (connection, packet) = TakeNextPacket();
                }
                executor.Execute(packet);
            }
        }
        catch (ThreadInterruptedException) { }
        finally
        {
            lock (lockObj)
            {
                retiringThreads.Remove(Thread.CurrentThread);
                if (connection != null) FinishPacket(connection);
            }
        }
    }

    private void CheckerTask()
    {
        int previousPacketCount = 0;
        for (;;)
        {
            try
            {
                Thread.Sleep(TimeSpan.FromMinutes(1));
            }
            catch (ThreadInterruptedException)
            {
                return;
            }

            int packetsWaitingForExecution;
            lock (lockObj) packetsWaitingForExecution = pendingPacketCount;
            if (packetsWaitingForExecution <= previousPacketCount && packetsWaitingForExecution <= threadKillThreshold)
            {
                KillThread();
            }
            else if (packetsWaitingForExecution > threadSpawnThreshold)
            {
                if (!NewThread() && packetsWaitingForExecution >= threadSpawnThreshold * 3)
                    log.LogWarning("Lag detected! [" + packetsWaitingForExecution
                        + " client packets are waiting for execution]. Consider increasing PacketProcessor maxThreads or a hardware upgrade.");
            }
            previousPacketCount = packetsWaitingForExecution;
        }
    }
}
