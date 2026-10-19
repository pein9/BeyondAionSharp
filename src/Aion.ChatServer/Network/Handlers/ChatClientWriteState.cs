namespace Aion.ChatServer.Network.Handlers;

// Native counterpart of Netty's default outbound-buffer watermarks and entry accounting.
// Caller holds the connection's flow-control lock. An entry stays pending until its whole frame finishes.
internal sealed class ChatClientWriteState
{
    internal const int LowWaterMark = 32 * 1024;
    internal const int HighWaterMark = 64 * 1024;
    internal const int EntryOverhead = 96;
    internal const int MaximumBytesBeforeWritable = 256 * 1024;
    internal long PendingBytes { get; private set; }
    internal bool IsWritable { get; private set; } = true;
    internal int WritabilityChanges { get; private set; }
    internal bool CanEnqueue => IsWritable || PendingBytes - LowWaterMark + 1 <= MaximumBytesBeforeWritable;

    internal bool Enqueue(int frameBytes)
    {
        PendingBytes += frameBytes + EntryOverhead;
        if (IsWritable && PendingBytes > HighWaterMark)
        {
            IsWritable = false;
            WritabilityChanges++;
            return true;
        }
        return false;
    }

    internal bool Complete(int frameBytes)
    {
        PendingBytes -= frameBytes + EntryOverhead;
        if (!IsWritable && PendingBytes < LowWaterMark)
        {
            IsWritable = true;
            WritabilityChanges++;
            return true;
        }
        return false;
    }
}
